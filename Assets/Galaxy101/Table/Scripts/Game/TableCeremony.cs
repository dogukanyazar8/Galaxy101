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
        private sealed class Flight
        {
            public GameObject Go;
            public Pose From, To;
            public float Start, Duration, Arc, Spin;
            public bool Begun, Done;
        }

        private readonly TableScene scene;
        private readonly TableFx fx;
        private readonly Func<float, Task> wait;
        private readonly List<Flight> flights = new List<Flight>();
        private readonly List<GameObject> pieces = new List<GameObject>();
        private GameObject die;
        private Pose dieFrom, dieTo; private Quaternion dieFinal; private Vector3 dieAxis; private float dieStart = -1, dieSpin;
        private int dieBounces;

        /// <summary>Step key for the HUD card (stack, roll, rolling, indicator, extra, deal, centre, start).</summary>
        public string Step { get; private set; }
        public int[] Dice { get; } = new int[2];
        public int LastDie { get; private set; } = -1;
        public bool Revealed { get; private set; }
        public bool Running { get; private set; }
        public event Action Changed;

        public TableCeremony(TableScene scene, TableFx fx, Func<float, Task> wait) { this.scene = scene; this.fx = fx; this.wait = wait; }

        private void Set(string step) { Step = step; Changed?.Invoke(); }

        // ---------- poses (three formulas from spec/21, in a seat's frame) ----------
        private Pose StackPose(int dealer, float si, int level) => scene.SeatPose(dealer, (si - 7) * 0.057f, TY + TD / 2 + level * TD, 0.36f, false);
        private Pose DeliveryPose(int seat, int slot, int level) => scene.SeatPose(seat, (slot - 1) * (TW + 0.016f), TY + TD / 2 + level * TD, 0.53f, false);

        private GameObject Tile(Pose at)
        {
            var go = scene.Tiles.Create(null, scene.TilesRoot); go.name = "toren_tas";
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(at.position, at.rotation); pieces.Add(go); return go;
        }

        private void Fly(GameObject go, Pose to, float delay, float duration, float arc, float spin = 0)
        {
            flights.RemoveAll(f => f.Go == go);
            flights.Add(new Flight { Go = go, To = to, Start = Time.time + delay, Duration = duration, Arc = arc, Spin = spin });
        }

        /// <summary>Per frame (TableGame.Update): tile flights (bezier lift, ¼ turn, tiny bounce) and the die.</summary>
        public void Tick()
        {
            float now = Time.time;
            foreach (var f in flights)
            {
                if (f.Done || f.Go == null || now < f.Start) continue;
                // A flight starts from wherever the tile is when its delay ends (chained moves never jump back).
                if (!f.Begun) { f.Begun = true; f.From = new Pose(f.Go.transform.position, f.Go.transform.rotation); }
                float u = Mathf.Clamp01((now - f.Start) / f.Duration), k = EaseInOutCubic(u);
                var pos = Vector3.Lerp(f.From.position, f.To.position, k) + Vector3.up * (f.Arc * 4 * k * (1 - k));
                if (u < 1 && u > .85f) pos += Vector3.up * (0.004f * Mathf.Sin((u - .85f) / .15f * Mathf.PI));
                var rot = Quaternion.Slerp(f.From.rotation, f.To.rotation, k) * Quaternion.AngleAxis(f.Spin * (1 - k), Vector3.up);
                f.Go.transform.SetPositionAndRotation(pos, rot);
                if (u >= 1) f.Done = true;
            }
            flights.RemoveAll(f => f.Done);
            if (dieStart >= 0 && die != null) RollStep(now);
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
            DealProgress D() => model.Deal;
            bool Past(string step) => D() == null || Rank(D().Step) >= Rank(step);
            async Task Until(Func<bool> done) { while (!done()) { await wait(0.05f); token.ThrowIfCancellationRequested(); } }
            int dealerAbs = D()?.Dealer ?? model.Dealer;
            int dealer = local(dealerAbs);
            try
            {
                // 1. 106 tiles from the centre heap into 15 stacks of 7 + the extra tile (450 ms, 15 ms apart, 0.6–0.9 s flights).
                Set("stack");
                var stacks = new List<GameObject>[G101Rules.Const.Stacks];
                for (int s = 0; s < stacks.Length; s++) stacks[s] = new List<GameObject>();
                int n = 0;
                for (int level = 0; level < G101Rules.Const.StackH; level++)
                    for (int s = 0; s < G101Rules.Const.Stacks; s++)
                    {
                        var go = Tile(scene.DeckPose(105 - n, 106));
                        Fly(go, StackPose(dealer, s, level), 0.45f + n * 0.015f, 0.6f + (n % 7) * 0.05f, 0.08f, 90); stacks[s].Add(go); n++;
                    }
                var extraTile = Tile(scene.DeckPose(0, 106));
                Fly(extraTile, StackPose(dealer, 15.3f, 0), 0.45f + n * 0.015f, 0.8f, 0.08f, 90);
                await wait(0.45f + n * 0.015f + 1.0f); token.ThrowIfCancellationRequested();

                // 2. Two rolls, each when the server announces its die (dealDie).
                for (int i = 0; i < 2; i++)
                {
                    Set("roll");
                    int k = i;
                    await Until(() => Past("indicator") || D().Dice.Count > k);
                    bool announced = D() != null && D().Dice.Count > i;
                    int value = announced ? D().Dice[i].Value : Mathf.Max(1, Dice[i]);
                    if (announced && !Past("indicator")) { Set("rolling"); await Roll(dealer, i, value); }
                    Dice[i] = value; LastDie = i; Changed?.Invoke();
                    if (!Past("indicator")) { await wait(0.35f); token.ThrowIfCancellationRequested(); }
                }

                // 3. The indicator: out of stack a at height b, a 1.15 s somersault onto the same stack; the tiles above settle.
                await Until(() => Past("indicator"));
                var d = D();
                int a = d != null && d.IndicatorStack >= 0 ? d.IndicatorStack : Mathf.Clamp(Dice[0] - 1, 0, 14);
                int b = Mathf.Clamp(d != null && d.IndicatorLevel >= 0 ? d.IndicatorLevel : Dice[1] - 1, 0, stacks[a].Count - 1);
                Fake = d != null && d.FakeSkip;
                fx?.MarkStack(dealer, a);
                var indicator = stacks[a][b]; stacks[a].RemoveAt(b);
                var outside = StackPose(dealer, a, b); outside.position += scene.SeatDirection(dealer) * 0.07f;
                Fly(indicator, outside, 0, 0.3f, 0.01f);
                for (int j = b; j < stacks[a].Count; j++) Fly(stacks[a][j], StackPose(dealer, a, j), 0.42f + (j - b) * 0.045f, 0.25f, 0.004f);
                await wait(0.32f);
                scene.Tiles.SetFace(indicator, model.Indicator != null ? TileIds.Code(TileIds.ToInt(model.Indicator)) : null); scene.Tiles.SetLying(indicator, true);
                var top = StackPose(dealer, a, stacks[a].Count); top.rotation = scene.FlatForMe;
                Fly(indicator, top, 0, 1.15f, 0.12f, 360); stacks[a].Add(indicator);
                fx?.Wave(StackPose(dealer, a, 0).position, new Color(1, .84f, .35f), 0.9f);
                await wait(1.2f); token.ThrowIfCancellationRequested();
                Revealed = true; Set("indicator");

                // 4. The extra tile onto stack a+1.
                await Until(() => Past("extra"));
                Set("extra");
                int extraStack = D() != null && D().ExtraStack >= 0 ? D().ExtraStack : (a + 1) % 15;
                fx?.MarkStack(dealer, extraStack);
                Fly(extraTile, StackPose(dealer, extraStack, stacks[extraStack].Count), 0, 0.65f, 0.05f); stacks[extraStack].Add(extraTile);
                await wait(0.65f); token.ThrowIfCancellationRequested();

                // 5. Hand out (dealStep distribute's plan): S(1+j) and S(5+j), then S(9+j), to the dealer's right first.
                await Until(() => Past("distribute"));
                fx?.MarkStack(null);
                Set("deal");
                var delivered = new List<GameObject>[4];
                for (int k = 0; k < 4; k++) delivered[k] = new List<GameObject>();
                var plan = D()?.Plan;
                for (int step = 0; step < 8; step++)
                {
                    int round = step / 4, j = step % 4;
                    bool planned = plan != null && step < plan.Count;
                    int receiver = local(planned ? plan[step].Seat : (dealerAbs + 1 + j) % 4);
                    var picks = planned ? plan[step].Stacks.ToArray() : round == 0 ? new[] { (a + 1 + j) % 15, (a + 5 + j) % 15 } : new[] { (a + 9 + j) % 15 };
                    if (round == 0) fx?.MarkStack(dealer, picks[0]);
                    for (int p = 0; p < picks.Length; p++)
                    {
                        int slot = round == 0 ? p : 2; var stack = stacks[picks[p]];
                        for (int level = 0; level < stack.Count; level++)
                            Fly(stack[level], DeliveryPose(receiver, slot, level), p * 0.11f, 0.5f + 0.3f * Mathf.Abs(receiver - dealer) / 3f, 0.03f + 0.04f * (receiver == dealer ? 0 : 1));
                        delivered[receiver].AddRange(stack); stack.Clear();
                    }
                    if (!Past("center")) { await wait(round == 0 ? 0.62f : 0.48f); token.ThrowIfCancellationRequested(); }
                }

                // 6. The last two stacks onto each other, then with the indicator stack to the centre (they become the deck).
                await Until(() => Past("center"));
                fx?.MarkStack(null);
                Set("centre");
                var centre = D()?.CenterStacks;
                int right = centre != null && centre.Count == 3 ? centre[1] : Mathf.Max((a + 13) % 15, (a + 14) % 15);
                int leftStack = centre != null && centre.Count == 3 ? centre[2] : Mathf.Min((a + 13) % 15, (a + 14) % 15);
                var left = stacks[leftStack]; var rightTiles = stacks[right];
                int baseCount = rightTiles.Count;
                for (int j = 0; j < left.Count; j++) Fly(left[j], StackPose(dealer, right, baseCount + j), 0, 0.6f, 0.06f);
                await wait(0.8f);
                var rest = rightTiles.Concat(left).Concat(stacks[a].Where(t => t != indicator)).ToList();
                int stock = Mathf.Max(model.Deck, 0);
                for (int j = 0; j < rest.Count; j++)
                {
                    if (j < stock) Fly(rest[j], scene.DeckPose(j, stock), 0, 0.85f, 0.05f);
                    else rest[j].SetActive(false);
                }
                Fly(indicator, scene.IndicatorPose(), 0.1f, 0.85f, 0.06f);
                await wait(1.0f); token.ThrowIfCancellationRequested();

                // 7. To the racks (handDealt): mine turn their faces to me (48 ms apart), the others' stay hidden (40 ms).
                await Until(() => Past("fill"));
                Set("fill");
                var state = fill();
                var mine = state.Rack.Tiles.OrderBy(id => { state.Rack.TryFind(id, out var s); return s.Row * RackGrid.Columns + s.Index; }).ToList();
                float last = 0;
                for (int k = 0; k < 4; k++)
                {
                    var tiles = delivered[k]; float step = k == 0 ? 0.048f : 0.040f;
                    if (k == 0)
                    {
                        for (int j = 0; j < tiles.Count; j++)
                        {
                            if (j >= mine.Count) { tiles[j].SetActive(false); continue; }
                            state.Rack.TryFind(mine[j], out var slot);
                            scene.Tiles.SetFace(tiles[j], TileCodes.Code(mine[j]));
                            Fly(tiles[j], scene.SlotPose(0, slot.Row, slot.Index), j * step, 0.55f, 0.07f); last = Mathf.Max(last, j * step + 0.55f);
                        }
                    }
                    else
                    {
                        var order = TableBoard.OpponentFillOrder;
                        for (int j = 0; j < tiles.Count; j++)
                        {
                            if (j >= state.HandCounts[k] || j >= order.Count) { tiles[j].SetActive(false); continue; }
                            Fly(tiles[j], scene.SlotPose(k, order[j].Row, order[j].Index), j * step, 0.5f, 0.06f); last = Mathf.Max(last, j * step + 0.5f);
                        }
                    }
                }
                await wait(last + 0.25f); token.ThrowIfCancellationRequested();
                Set("start");
            }
            finally { fx?.MarkStack(null); Running = false; }
        }

        /// <summary>Removes the ceremony's own objects (the board has taken over the same poses).</summary>
        public void Clear()
        {
            fx?.MarkStack(null);
            foreach (var go in pieces) if (go != null) UnityEngine.Object.Destroy(go);
            pieces.Clear(); flights.Clear();
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
        /// rollDie: 1.45 s from the dealer's hand across the felt, three bounces (at 32 / 56 / 72 %, 5 / 1.8 / 0.6 cm), spin
        /// A = 10–15 rad damped by (1−u)^2.2 ending with the wanted face up; dust on each bounce, a gold ring on landing.
        /// The second roll first returns the die to the hand (0.42 s).
        /// </summary>
        private async Task Roll(int dealer, int index, int value)
        {
            if (die == null) die = BuildDie();
            var hand = scene.SeatPose(dealer, 0.16f, TY + 0.14f, 0.44f, true);
            if (index == 1)
            {
                Fly(die, new Pose(hand.position, die.transform.rotation), 0, 0.42f, 0.05f); await wait(0.45f);
            }
            else die.transform.SetPositionAndRotation(hand.position, Quaternion.identity);
            var land = scene.SeatPose(dealer, -0.04f + index * 0.07f, TY + 0.015f, 0.17f, true);
            dieFrom = new Pose(die.transform.position, die.transform.rotation); dieTo = land;
            var rng = new System.Random(dealer * 31 + index * 7 + value);
            dieFinal = Quaternion.AngleAxis((float)rng.NextDouble() * 360, Vector3.up) * Quaternion.FromToRotation(FaceNormal[value], Vector3.up);
            dieAxis = new Vector3((float)rng.NextDouble() - .5f, (float)rng.NextDouble() * .3f, (float)rng.NextDouble() - .5f).normalized;
            if (dieAxis == Vector3.zero) dieAxis = Vector3.right;
            dieSpin = 10 + (float)rng.NextDouble() * 5; dieBounces = 0; dieStart = Time.time;
            await wait(1.5f);
            dieStart = -1; die.transform.SetPositionAndRotation(land.position, dieFinal);
            fx?.Wave(land.position, new Color(1, .84f, .35f));
        }

        private void RollStep(float now)
        {
            float u = Mathf.Clamp01((now - dieStart) / 1.45f);
            float k = 1 - Mathf.Pow(1 - u, 2.4f);
            var pos = Vector3.Lerp(dieFrom.position, dieTo.position, k);
            float start = dieFrom.position.y - dieTo.position.y, y;
            if (u < .32f) { float s = u / .32f; y = start * (1 - s * s); }
            else if (u < .56f) y = Hop(u, .32f, .56f, .05f);
            else if (u < .72f) y = Hop(u, .56f, .72f, .018f);
            else y = Hop(u, .72f, 1f, .006f);
            pos.y = dieTo.position.y + y;
            int bounce = u >= .72f ? 3 : u >= .56f ? 2 : u >= .32f ? 1 : 0;
            if (bounce > dieBounces) { dieBounces = bounce; fx?.Burst(new Vector3(pos.x, dieTo.position.y - 0.01f, pos.z), new Color(1, .95f, .85f, .7f), 8, 0.18f); }
            var spin = Quaternion.AngleAxis(dieSpin * Mathf.Pow(1 - u, 2.2f) * Mathf.Rad2Deg, dieAxis);
            die.transform.SetPositionAndRotation(pos, spin * dieFinal);
        }

        private static float Hop(float u, float a, float b, float height) { float s = (u - a) / (b - a); return height * 4 * s * (1 - s); }
    }
}
