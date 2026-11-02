using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Okey101.Core.V4;
using UnityEngine;
using static Galaxy101.Table.TableConstants;

namespace Galaxy101.Table
{
    /// <summary>
    /// The real 101 deal (spec/21, G02–G12; table3d.js rollDie / markStack): 15 stacks of 7 face down in front of the
    /// dealer plus the extra tile, two dice, the indicator pulled from stack a at height b and turned face up on its stack,
    /// the extra tile onto stack a+1, the stacks handed out (two, two, two, two, then one each), the last two stacks and the
    /// indicator stack to the centre, and every hand flying to its rack. The ceremony's tiles are its own; when it ends the
    /// board takes over with identical poses (<see cref="TableBoard.Settle"/>), so nothing jumps.
    /// </summary>
    public sealed class TableCeremony
    {
        private sealed class DealSkippedException : Exception { }

        private readonly TableScene scene;
        private readonly TableFx fx;
        private readonly Func<float, Task> wait;
        private readonly List<GameObject> pieces = new List<GameObject>();
        private readonly List<GameObject> heap = new List<GameObject>();
        private float heapStart;
        private int heapShown = -1;
        private bool heapAnimating;
        private GameObject die;
        private DiceRollMotion dieMotion;
        private float dieStart = -1;
        private int dieBounces;
        private bool dieLanded;

        /// <summary>Step key for the HUD card (stack, roll, rolling, indicator, extra, deal, centre, start).</summary>
        public string Step { get; private set; }
        public int[] Dice { get; } = new int[2];
        public int LastDie { get; private set; } = -1;
        public bool Revealed { get; private set; }
        public bool Running { get; private set; }
        public event Action Changed;
        public event Action<int, int> DieRolling;

        public TableCeremony(TableScene scene, TableFx fx, Func<float, Task> wait) { this.scene = scene; this.fx = fx; this.wait = wait; }

        private void Set(string step) { Step = step; Changed?.Invoke(); }

        // ---------- poses (three formulas from spec/21, in a seat's frame) ----------
        private Pose StackPose(int dealer, float si, int level, string key, bool faceUp = false)
        {
            var pose = scene.SeatPose(dealer, (si - 7) * .057f + (si >= 15 ? .014f : 0), TY + TD / 2 + level * TD, .36f, faceUp);
            if (!faceUp) pose.rotation = Quaternion.AngleAxis(-(Hash(key) - .5f) * .05f * Mathf.Rad2Deg, Vector3.up) * pose.rotation;
            return pose;
        }
        private Pose DeliveryPose(int seat, int slot, int level, string key)
        {
            var pose = scene.SeatPose(seat, (slot - 1) * (TW + .016f), TY + TD / 2 + level * TD, .53f, false);
            pose.rotation = Quaternion.AngleAxis(-(Hash(key) - .5f) * .05f * Mathf.Rad2Deg, Vector3.up) * pose.rotation;
            return pose;
        }

        private Pose PilePose(int level)
        {
            bool under = level < 6;
            return new Pose(ThreeSpace.ToUnity(under ? TableScene.PileX0 : -TableScene.PileX0, TY + TD / 2 + (under ? level : level - 6) * TD, 0),
                Quaternion.AngleAxis(-((level * .37f) % 1 - .5f) * .05f * Mathf.Rad2Deg, Vector3.up) * scene.SeatPose(0, 0, 0, 0, false).rotation);
        }
        private Pose CentreIndicatorPose() => new Pose(ThreeSpace.ToUnity(TableScene.PileX0, TY + TD / 2 + 6 * TD, 0), Quaternion.AngleAxis(-.04f * Mathf.Rad2Deg, Vector3.up) * scene.FlatForMe);

        private GameObject Tile(Pose at, int serial)
        {
            var go = scene.Tiles.Create(null, scene.TilesRoot); go.name = "toren_tas_" + serial;
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(at.position, at.rotation);
            var tile = go.AddComponent<TileView>(); tile.MotionKey = "t" + serial; tile.Fx = fx;
            pieces.Add(go); return go;
        }

        private void Fly(GameObject go, Pose to, TileView.Motion motion, float delay = 0, float duration = 0, string effect = null)
        {
            go.GetComponent<TileView>().MoveTo(to, motion, delay, duration, effect);
        }

        /// <summary>The source heap shrinks while the separate dealt tiles fly; TileView owns their shared frame sampler.</summary>
        public void Tick()
        {
            float now = Time.time;
            if (heapAnimating)
            {
                int count = Mathf.RoundToInt(106 * (1 - Mathf.Clamp01((now - heapStart - .4f) / 1.522f)));
                if (count != heapShown)
                {
                    heapShown = count;
                    for (int i = 0; i < heap.Count; i++)
                    {
                        if (heap[i] == null) continue;
                        heap[i].SetActive(i < count);
                        if (i < count) { var pose = scene.DeckPose(i, count); heap[i].transform.SetPositionAndRotation(pose.position, pose.rotation); }
                    }
                }
                if (count == 0) heapAnimating = false;
            }
            if (dieStart >= 0 && die != null) RollStep(now - dieStart);
        }

        // ---------- the ceremony ----------
        private static readonly string[] Order = { "build", "dice", "afterDice", "indicator", "extra", "distribute", "center", "fill" };
        private static int Rank(string step) { int i = Array.IndexOf(Order, step); return i < 0 ? 0 : i; }

        /// <summary>The indicator was a fake okey's neighbour (D-03, dealStep indicator fakeSkip).</summary>
        public bool Fake { get; private set; }

        /// <summary>
        /// Plays the server's deal (D-24) as its steps arrive: the client never times the ceremony, it animates each step when
        /// the model reaches it (dealStart → dealStep dice → dealDie × 2 → indicator → extra → distribute → center →
        /// handDealt) and catches up when the server is ahead (timings.md §2: a later event skips what is left).
        /// </summary>
        /// <param name="model">The seat's model; its <see cref="TableClient.Deal"/> carries the announced steps.</param>
        /// <param name="local">Absolute seat → the table's seat (0 = me at the bottom).</param>
        /// <param name="fill">The board as the hand starts (my rack laid out, the others' counts, the stock).</param>
        public async Task Run(TableClient model, Func<int, int> local, Func<BoardState> fill, CancellationToken token)
        {
            Running = true; Revealed = false; Fake = false; Dice[0] = Dice[1] = 0; LastDie = -1;
            fx?.MarkStack(null);
            var progress = model.Deal;
            var applied = new TaskCompletionSource<bool>();
            var skipped = new TaskCompletionSource<bool>();
            var cancelled = new TaskCompletionSource<bool>();
            bool handReady = progress != null && progress.Step == "fill" && (model.Rack != null || model.Seat < 0);
            void Applied(ClientMessage message)
            {
                if (model.Deal != null) progress = model.Deal;
                if (message.Type == "handDealt") handReady = true;
                if (progress?.Skipped == true) skipped.TrySetResult(true);
                var previous = applied; applied = new TaskCompletionSource<bool>(); previous.TrySetResult(true);
            }
            if (progress?.Skipped == true) skipped.TrySetResult(true);
            model.Applied += Applied;
            using var cancellation = token.Register(() => cancelled.TrySetResult(true));
            async Task Await(Task task, bool interrupt = true)
            {
                token.ThrowIfCancellationRequested();
                if (interrupt && progress?.Skipped == true) throw new DealSkippedException();
                await Task.WhenAny(task, interrupt ? skipped.Task : cancelled.Task, cancelled.Task);
                token.ThrowIfCancellationRequested();
                if (interrupt && progress?.Skipped == true) throw new DealSkippedException();
                await task;
            }
            Task PhaseWait(float seconds) => Await(wait(seconds));
            bool Past(string step) => model.Deal == null || progress == null || Rank(progress.Step) >= Rank(step);
            async Task Until(Func<bool> done, bool interrupt = true)
            {
                while (true) { var next = applied.Task; if (done()) return; await Await(next, interrupt); }
            }
            int dealerAbs = progress?.Dealer ?? model.Dealer, dealer = local(dealerAbs);
            var stacks = new List<GameObject>[G101Rules.Const.Stacks];
            for (int i = 0; i < stacks.Length; i++) stacks[i] = new List<GameObject>();
            GameObject indicator = null, extraTile = null;
            int indicatorStack = -1;
            bool extraPlaced = false;
            string Key(GameObject go) => go.GetComponent<TileView>().MotionKey;
            void ComposeIndicator()
            {
                if (indicator != null) return;
                indicatorStack = Mathf.Clamp(progress != null && progress.IndicatorStack >= 0 ? progress.IndicatorStack : Dice[0] - 1, 0, 14);
                var stack = stacks[indicatorStack];
                int level = Mathf.Clamp(progress != null && progress.IndicatorLevel >= 0 ? progress.IndicatorLevel : Dice[1] - 1, 0, stack.Count - 1);
                indicator = stack[level]; stack.RemoveAt(level); stack.Add(indicator);
                Fake = progress?.FakeSkip == true;
                scene.Tiles.SetFace(indicator, model.Indicator != null ? TileIds.Code(TileIds.ToInt(model.Indicator)) : null);
                scene.Tiles.SetLying(indicator, true);
            }
            void ComposeExtra()
            {
                if (extraPlaced) return;
                int target = progress != null && progress.ExtraStack >= 0 ? progress.ExtraStack : (indicatorStack + 1) % 15;
                stacks[target].Add(extraTile); extraPlaced = true;
            }
            IEnumerable<(int Seat, int[] Stacks)> Plan()
            {
                if (progress?.Plan != null && progress.Plan.Count > 0)
                {
                    foreach (var p in progress.Plan) yield return (local(p.Seat), p.Stacks.ToArray());
                    yield break;
                }
                for (int j = 0; j < 4; j++) yield return (local((dealerAbs + 1 + j) % 4), new[] { (indicatorStack + 1 + j) % 15, (indicatorStack + 5 + j) % 15 });
                for (int j = 0; j < 4; j++) yield return (local((dealerAbs + 1 + j) % 4), new[] { (indicatorStack + 9 + j) % 15 });
            }
            try
            {
                // startDeal: the old heap is separate from the fresh, delayed tiles. Every fresh tile starts at deckTop.
                Set("stack"); scene.DeckHeap = true;
                var spawn = scene.DeckTop(106);
                spawn.rotation = scene.SeatPose(0, 0, 0, 0, false).rotation;
                for (int i = 0; i < 106; i++)
                {
                    var at = scene.DeckPose(i, 106);
                    var go = scene.Tiles.Create(null, scene.TilesRoot); go.name = "toren_deste_" + i;
                    UnityEngine.Object.Destroy(go.GetComponent<Collider>()); go.transform.SetPositionAndRotation(at.position, at.rotation);
                    heap.Add(go); pieces.Add(go);
                }
                heapStart = Time.time; heapShown = 106; heapAnimating = true;
                for (int si = 0; si < 15; si++)
                    for (int level = 0; level < 7; level++)
                    {
                        int n = si * 7 + level;
                        var go = Tile(spawn, n + 1); stacks[si].Add(go);
                        Fly(go, StackPose(dealer, si, level, Key(go)), TileView.Motion.Deal, .4f + n * .012f);
                    }
                extraTile = Tile(spawn, 106);
                Fly(extraTile, StackPose(dealer, 15.3f, 0, Key(extraTile)), TileView.Motion.Deal, .4f + 105 * .012f);
                try
                {
                    await PhaseWait(.4f + 106 * .012f + .9f);
                    for (int i = 0; i < 2; i++)
                    {
                        Set("roll"); int index = i;
                        await Until(() => Past("indicator") || progress.Dice.Count > index);
                        bool announced = progress != null && progress.Dice.Count > i;
                        int value = announced ? progress.Dice[i].Value : Mathf.Max(1, Dice[i]);
                        if (announced && !Past("indicator")) { Set("rolling"); DieRolling?.Invoke(dealer, value); await Roll(dealer, i, value, PhaseWait); }
                        Dice[i] = value; LastDie = i; Changed?.Invoke();
                    }
                    await Until(() => Past("indicator"));
                    ComposeIndicator();
                    fx?.MarkStack(dealer, indicatorStack);
                    var stack = stacks[indicatorStack];
                    int removedLevel = Mathf.Clamp(progress != null && progress.IndicatorLevel >= 0 ? progress.IndicatorLevel : Dice[1] - 1, 0, 6);
                    for (int j = removedLevel; j < stack.Count - 1; j++)
                        Fly(stack[j], StackPose(dealer, indicatorStack, j, Key(stack[j])), TileView.Motion.Drop, .42f + (j - removedLevel) * .045f);
                    Fly(indicator, StackPose(dealer, indicatorStack, 6, Key(indicator), true), TileView.Motion.Flip, 0, 1.15f, "reveal");
                    Revealed = true; Set("indicator");
                    await Until(() => Past("extra"));
                    Set("extra"); ComposeExtra();
                    int extraStack = progress != null && progress.ExtraStack >= 0 ? progress.ExtraStack : (indicatorStack + 1) % 15;
                    fx?.MarkStack(dealer, extraStack);
                    Fly(extraTile, StackPose(dealer, extraStack, 7, Key(extraTile)), TileView.Motion.Block, 0, .65f);
                    await Until(() => Past("distribute"));
                    fx?.MarkStack(null); Set("deal");
                    var plan = Plan().ToList();
                    if (!Past("center")) await PhaseWait(.25f);
                    for (int step = 0; step < plan.Count; step++)
                    {
                        var delivery = plan[step];
                        if (step < 4) fx?.MarkStack(dealer, delivery.Stacks[0]);
                        for (int p = 0; p < delivery.Stacks.Length; p++)
                        {
                            var tiles = stacks[delivery.Stacks[p]];
                            for (int level = 0; level < tiles.Count; level++)
                                Fly(tiles[level], DeliveryPose(delivery.Seat, step < 4 ? p : 2, level, Key(tiles[level])), TileView.Motion.Block, p * .1f);
                        }
                        if (step + 1 < plan.Count && !Past("center")) await PhaseWait(step < 4 ? .48f : .38f);
                    }
                    await Until(() => Past("center"));
                    fx?.MarkStack(null); Set("centre");
                    int a = (indicatorStack + 13) % 15, b = (indicatorStack + 14) % 15;
                    int right = Mathf.Max(a, b), left = Mathf.Min(a, b);
                    for (int j = 0; j < stacks[left].Count; j++)
                        Fly(stacks[left][j], StackPose(dealer, right, 7 + j, Key(stacks[left][j])), TileView.Motion.Block, 0, .6f);
                    if (!Past("fill")) await PhaseWait(.8f);
                    for (int j = 0; j < stacks[right].Count; j++) Fly(stacks[right][j], PilePose(6 + j), TileView.Motion.Block, 0, .85f);
                    for (int j = 0; j < stacks[left].Count; j++) Fly(stacks[left][j], PilePose(13 + j), TileView.Motion.Block, 0, .85f);
                    for (int j = 0; j < stacks[indicatorStack].Count; j++)
                        Fly(stacks[indicatorStack][j], j < 6 ? PilePose(j) : CentreIndicatorPose(), TileView.Motion.Block, .26f, .85f);
                    await Until(() => Past("fill"));
                }
                catch (DealSkippedException)
                {
                    // dealSkip cancels phase timers and dice; startTween retargets rack tiles from their live poses.
                    if (die != null) UnityEngine.Object.Destroy(die); die = null; dieStart = -1;
                }
                // The skip vote never supplies a private hand. Bind faces only once handDealt (or its snapshot) arrived.
                await Until(() => handReady, false);
                ComposeIndicator(); ComposeExtra();
                bool fast = progress?.Skipped == true;
                if (fast)
                {
                    Dice[0] = progress.Dice.Count > 0 ? progress.Dice[0].Value : 0;
                    Dice[1] = progress.Dice.Count > 1 ? progress.Dice[1].Value : 0;
                    LastDie = 1; Revealed = true;
                }
                fx?.MarkStack(null); Set("fill");
                var state = fill();
                var mine = state.Rack.Tiles.OrderBy(id => { state.Rack.TryFind(id, out var slot); return slot.Row * RackGrid.Columns + slot.Index; }).ToList();
                var delivered = new List<GameObject>[4]; for (int k = 0; k < 4; k++) delivered[k] = new List<GameObject>();
                foreach (var delivery in Plan()) foreach (int si in delivery.Stacks) delivered[delivery.Seat].AddRange(stacks[si].AsEnumerable().Reverse());
                float factor = fast ? .3f : 1;
                for (int k = 0; k < 4; k++)
                    for (int j = 0; j < delivered[k].Count; j++)
                    {
                        var go = delivered[k][j];
                        if (k == 0 && mine.Count > 0)
                        {
                            if (j >= mine.Count) { go.SetActive(false); continue; }
                            state.Rack.TryFind(mine[j], out var slot); scene.Tiles.SetFace(go, TileCodes.Code(mine[j]));
                            Fly(go, scene.SlotPose(0, slot.Row, slot.Index), TileView.Motion.Deal, (.08f + j * .048f) * factor);
                        }
                        else
                        {
                            if (j >= state.HandCounts[k] || j >= TableBoard.OpponentFillOrder.Count) { go.SetActive(false); continue; }
                            // A spectator's bottom rack is also anonymous; never derive an opponent face from engine ids.
                            scene.Tiles.SetFace(go, null);
                            var slot = k == 0 ? new RackSlot(j < 11 ? 0 : 1, 2 + j % 11) : TableBoard.OpponentFillOrder[j];
                            Fly(go, scene.SlotPose(k, slot.Row, slot.Index), TileView.Motion.Deal, (.08f + j * (k == 0 ? .048f : .04f)) * factor);
                        }
                    }
                // FQ changes the stagger and finish deadline, not startTween's distance-based duration.
                await Await(wait((.08f + 22 * .048f) * factor + (fast ? .65f : 1)), false);
                Set("start");
            }
            finally
            {
                model.Applied -= Applied; heapAnimating = false; scene.DeckHeap = false;
                fx?.MarkStack(null); Running = false;
            }
        }

        /// <summary>Removes only this ceremony's objects after the board takes over, or when the scene cancels.</summary>
        public void Clear()
        {
            fx?.MarkStack(null);
            foreach (var go in pieces) if (go != null) UnityEngine.Object.Destroy(go);
            pieces.Clear(); heap.Clear(); heapAnimating = false; heapShown = -1;
            if (die != null) UnityEngine.Object.Destroy(die); die = null; dieStart = -1;
        }

        // ---------- the die (30 mm, 17 % corner radius, ivory with navy pips, the one big and red) ----------
        /// <summary>Unity-space face normals: +y 1, −y 6, +x 2, −x 5, +z 3, −z 4 (opposite faces add up to 7).</summary>
        private static readonly Vector3[] FaceNormal = { Vector3.zero, Vector3.up, Vector3.right, Vector3.forward, Vector3.back, Vector3.left, Vector3.down };

        private GameObject BuildDie()
        {
            if (scene.DiePrefab != null)
            {
                var model = UnityEngine.Object.Instantiate(scene.DiePrefab, scene.TilesRoot, false); model.name = "zar";
                foreach (var c in model.GetComponentsInChildren<Collider>()) UnityEngine.Object.Destroy(c);
                pieces.Add(model); return model;
            }
            // Fallback (catalog built before the die model): a cube with flattened sphere pips.
            var root = new GameObject("zar"); root.transform.SetParent(scene.TilesRoot, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.name = "zar_govde"; UnityEngine.Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false); body.transform.localScale = Vector3.one * 0.03f;
            var ivory = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "zar_fildisi" }; ivory.SetColor("_BaseColor", new Color32(0xF6, 0xF0, 0xE0, 255)); ivory.SetFloat("_Smoothness", .72f);
            body.GetComponent<MeshRenderer>().sharedMaterial = ivory;
            var navy = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "zar_nokta" }; navy.SetColor("_BaseColor", new Color32(0x1B, 0x24, 0x40, 255));
            var red = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "zar_bir" }; red.SetColor("_BaseColor", new Color32(0xD9, 0x34, 0x3F, 255));
            var layout = new Dictionary<int, Vector2[]>
            {
                [1] = new[] { Vector2.zero },
                [2] = new[] { new Vector2(-1, -1), new Vector2(1, 1) },
                [3] = new[] { new Vector2(-1, -1), Vector2.zero, new Vector2(1, 1) },
                [4] = new[] { new Vector2(-1, -1), new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1) },
                [5] = new[] { new Vector2(-1, -1), new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), Vector2.zero },
                [6] = new[] { new Vector2(-1, -1), new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, 0), new Vector2(1, 0) },
            };
            for (int value = 1; value <= 6; value++)
            {
                var normal = FaceNormal[value]; var turn = Quaternion.FromToRotation(Vector3.up, normal);
                foreach (var p in layout[value])
                {
                    var pip = GameObject.CreatePrimitive(PrimitiveType.Sphere); pip.name = "zar_nokta_" + value; UnityEngine.Object.Destroy(pip.GetComponent<Collider>());
                    pip.transform.SetParent(root.transform, false);
                    float size = value == 1 ? 0.0085f : 0.0055f;
                    pip.transform.localPosition = turn * new Vector3(p.x * 0.0078f, 0.0151f, p.y * 0.0078f);
                    pip.transform.localRotation = turn; pip.transform.localScale = new Vector3(size, 0.0012f, size);
                    var r = pip.GetComponent<MeshRenderer>(); r.sharedMaterial = value == 1 ? red : navy; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            pieces.Add(root); return root;
        }

        /// <summary>
        /// rollDie: the source's 1.75 s segmented throw, two spin axes and final wobble, ending on the server's face.
        /// An existing die first returns to the dealer's hand over 0.45 s without jumping or restarting its orientation.
        /// </summary>
        private async Task Roll(int dealer, int index, int value, Func<float, Task> pause)
        {
            bool returning = die != null && die.activeSelf;
            if (die == null) die = BuildDie();
            var resting = new Pose(die.transform.position, die.transform.rotation);
            var rng = new System.Random(dealer * 31 + index * 7 + value);
            float Next() => (float)rng.NextDouble();
            var start = scene.SeatPose(dealer, .1f + (Next() - .5f) * .08f, TY + .42f, .6f, true).position;
            var land = scene.SeatPose(dealer, (Next() - .5f) * .14f, TY + DiceRollMotion.Size / 2, -.04f + Next() * .08f, true).position;
            var final = Quaternion.AngleAxis(Next() * 6.283f * Mathf.Rad2Deg, Vector3.up) * Quaternion.FromToRotation(FaceNormal[value], Vector3.up);
            var direction = land - start; direction.y = 0; direction.Normalize();
            var axis = Vector3.Cross(Vector3.up, direction).normalized;
            // Mirroring three-space X also mirrors a rotation's handedness (ThreeSpace.ToUnity quaternion).
            var secondary = new Vector3(Next() - .5f, -(Next() - .5f) * .4f, -(Next() - .5f)).normalized;
            float spin = 11 + Next() * 4, secondarySpin = 2.2f + Next() * 1.6f;
            float wobble = (Next() < .5f ? 1 : -1) * (.22f + Next() * .1f);
            dieMotion = new DiceRollMotion(start, land, final, axis, secondary, spin, secondarySpin, wobble, resting, returning);
            dieBounces = 0; dieLanded = false; dieStart = Time.time;
            float began = dieStart; var rollingDie = die;
            // The first pose is assigned immediately; the second throw's return starts from the actual resting pose.
            var first = returning ? resting : dieMotion.InitialPose;
            die.transform.SetPositionAndRotation(first.position, first.rotation);
            await pause(dieMotion.TotalDuration);
            if (die != rollingDie || die == null || dieStart != began) return;
            // A wait may complete before this frame's Tick; sample the exact endpoint and emit landing effects once.
            RollStep(dieMotion.TotalDuration);
            dieStart = -1;
        }

        private void RollStep(float elapsed)
        {
            elapsed = Mathf.Max(0, elapsed);
            var pose = dieMotion.Sample(elapsed);
            die.transform.SetPositionAndRotation(pose.position, pose.rotation);
            int impacts = dieMotion.ImpactsAt(elapsed);
            while (dieBounces < impacts)
            {
                bool first = dieBounces++ == 0;
                var at = new Vector3(pose.position.x, TY + .004f, pose.position.z);
                fx?.DiceDust(at, first ? 9 : 4, first ? .09f : .05f);
                if (first) fx?.DiceShock(new Vector3(at.x, TY + .001f, at.z), new Color32(0xFF, 0xF1, 0xC0, 255), .5f, .06f);
            }
            if (!dieLanded && elapsed >= dieMotion.TotalDuration)
            {
                dieLanded = true;
                var at = new Vector3(pose.position.x, TY + .001f, pose.position.z);
                fx?.DiceShock(at, fx.Accent, .9f, .1f);
                fx?.Burst(new Vector3(at.x, TY + .03f, at.z), new Color32(0xFF, 0xE0, 0x8A, 255), 10, .08f);
            }
        }
    }
}
