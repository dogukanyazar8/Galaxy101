using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static Galaxy101.Table.TableConstants;

namespace Galaxy101.Table
{
    /// <summary>
    /// Table effects (spec/12; table3d.js burst / wave / spark / dealFx / celebrate and the frame pulses):
    /// * bursts (own GlowPoints mesh; the project has no particle module): n additive glow points thrown up (elevation 0.2–1.4 rad, speed × 0.5–1.5), gravity 1.4 m/s², 0.9 s fade;
    /// * sparks: 3 cm glow, 0.5 s fade and shrink; ring waves on the felt: scale 0.03 → 0.19 (easeOut), 0.7 s;
    /// * pulses: discard ring while discarding, deck glow while drawing, the previous player's pile ring, LEDs, holo ring;
    /// * the deal's shuffle beat (three rings, 80 particles, deck spin and bounce, holo boost) and the finish celebration
    ///   (gold spot light 3.4 s, eight confetti bursts 240 ms apart, camera leaning towards an opponent finisher).
    /// Additive materials keep the destination alpha so the HUD composite never shows holes.
    /// </summary>
    public sealed partial class TableFx : MonoBehaviour
    {
        public static readonly Color Gold = Hex("#FFD964"), Spark = Hex("#FFE08A"), Green = Hex("#3CCB6E"), White = Color.white;
        private static readonly Color StackGold = Hex("#FFC94A");

        private TableScene scene;
        private Texture2D glow;
        private GlowPoints bursts, sparks, haze, diceDust;
        private Mesh ringMesh, discMesh, quadMesh, diceShockMesh;
        private Material zoneMaterial, zoneFill, sideMaterial, deckGlowMaterial, holoMaterial, tableHaloMaterial, stackMarkerMaterial;
        private Transform zone, zoneFillTransform, sideRing, deckGlow, holo, deckRoot, tableHalo, stackMarker;
        private readonly List<(Transform T, Material M, Color C, float T0)> waves = new List<(Transform, Material, Color, float)>();
        private readonly List<(Transform T, Material M, Color C, float T0, float Life, float Max)> diceShocks = new List<(Transform, Material, Color, float, float, float)>();
        private readonly List<(Material M, Color Base)> leds = new List<(Material, Color)>();
        private readonly List<(float At, System.Action Run)> scheduled = new List<(float, System.Action)>();
        private Light winSpot;
        private float winSpotT = -9, deckSpinT = -9, holoBoostUntil, pileK, stackAlpha;
        private bool canDiscard, canDraw, sidePulse, stackMarked;
        private int deckCount = 106;

        public Color Accent { get; private set; } = Gold;
        public Transform DeckRoot => deckRoot;
        /// <summary>Whether melds have moved the stock to the left (table3d.js fl.pileLeft).</summary>
        public System.Func<bool> PileLeft;
        /// <summary>Live glow points and ring waves (tests, diagnostics).</summary>
        public int LivePoints => (bursts?.Count ?? 0) + (sparks?.Count ?? 0) + (finishBursts?.Count ?? 0);
        public int LiveHaze => haze?.Count ?? 0;
        public int LiveDiceDust => diceDust?.Count ?? 0;
        public int LiveDiceShocks => diceShocks.Count + LiveFinishShocks;
        public int LiveWaves => waves.Count;
        public float SpotIntensity => winSpot != null ? winSpot.intensity : 0;
        public bool DeckSpinning => Time.time - deckSpinT >= 0 && Time.time - deckSpinT <= 0.9f;
        public float DiscardRingAlpha => zoneMaterial != null ? zoneMaterial.GetColor("_BaseColor").a : 0;
        public float DeckGlowAlpha => deckGlowMaterial != null ? deckGlowMaterial.GetColor("_BaseColor").a : 0;

        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        public void Init(TableScene table)
        {
            scene = table; Accent = table.Skins.Theme.A1;
            glow = GlowTexture();
            bursts = new GlowPoints(Points("Fx_Bursts"), 1.4f, false, pointSized: true);
            sparks = new GlowPoints(Points("Fx_Sparks"), 0, true);
            haze = new GlowPoints(Points("Fx_Duman", true), 0, false);
            InitMotes();
            ringMesh = Ring(0.85f, 1f, 56); discMesh = Disc(1f, 40);
            deckRoot = new GameObject("Deste").transform; deckRoot.SetParent(table.TilesRoot, false);

            zone = table.DiscardZone;
            if (zone != null) zoneMaterial = zone.GetComponent<MeshRenderer>().sharedMaterial;
            zoneFillTransform = Flat("atma_dolgu", discMesh, zoneFill = Additive(Accent, 0), table.DiscardCenter(0) + Vector3.up * 0.0004f, 0.058f);
            sideRing = Flat("yan_halka", ringMesh, sideMaterial = Additive(Accent, 0), table.DiscardCenter(3), 0.066f);
            deckGlow = Flat("deste_parilti", quadMesh = Quad(), deckGlowMaterial = Additive(Accent, 0, glow), new Vector3(0, TY + 0.002f, 0), 0.25f);
            tableHalo = Flat("masa_hale", quadMesh, tableHaloMaterial = Additive(table.Skins.Theme.A2, 0, glow), new Vector3(0, .004f, 0), 1.8f);
            // M.halo was cloned before applyLook: this marker retains its original #FFC94A in every theme.
            stackMarker = Flat("balya_isaret", quadMesh, stackMarkerMaterial = Additive(StackGold, 0, glow), Vector3.zero, 1);
            stackMarker.localScale = new Vector3(.055f, 1, .08f);

            RescanTable();

            winSpot = new GameObject("Kazanan_spot").AddComponent<Light>(); winSpot.transform.SetParent(transform, false);
            winSpot.type = LightType.Spot; winSpot.color = Hex("#FFE7A3"); winSpot.range = 5; winSpot.spotAngle = 0.32f * 2 * Mathf.Rad2Deg; winSpot.innerSpotAngle = winSpot.spotAngle * 0.4f; winSpot.intensity = 0; winSpot.shadows = LightShadows.None;
        }

        /// <summary>Find the holo ring and LED materials of the current table and rack models (again after a theme change).</summary>
        public void RescanTable()
        {
            if (scene == null) return;
            Accent = scene.Skins.Theme.A1;
            float haloAlpha = scene.TableSkin == "wood" ? .12f : scene.TableSkin == "holo" ? .4f : .25f;
            SetAlpha(tableHaloMaterial, scene.TableSkin == "andromeda" ? Hex("#7F9CFF") : scene.Skins.Theme.A2, ThreeBlend.Additive(haloAlpha));
            holo = null; holoMaterial = null; leds.Clear();
            foreach (var r in scene.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.gameObject.activeInHierarchy) continue;
                if (r.name.StartsWith("holo_halka", System.StringComparison.Ordinal) && holo == null)
                {
                    holo = r.transform; holoMaterial = r.sharedMaterial;
                    // table3d.js M.holo is AdditiveBlending (the GLB export only keeps "transparent").
                    holoMaterial.SetFloat("_Blend", 2); holoMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); holoMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
                    holoMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.Zero); holoMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
                    // applyLook: M.holo.color = the planet's a1 (#B79CFF on the Andromeda table), not the exported colour.
                    if (holoMaterial.HasProperty("_BaseColor")) { var c = scene.TableSkin == "andromeda" ? Hex("#B79CFF") : Accent; c.a = holoMaterial.GetColor("_BaseColor").a; holoMaterial.SetColor("_BaseColor", c); }
                }
                foreach (var m in r.sharedMaterials)
                    if (m != null && (m.name.StartsWith("led", System.StringComparison.Ordinal) || m.name.Contains("_led")) && m.HasProperty("_EmissionColor") && !leds.Exists(l => l.M == m))
                        leds.Add((m, m.GetColor("_EmissionColor")));
            }
        }

        /// <summary>Pulse below one of the dealer's 15 stacks; null fades it out (table3d.js markStack).</summary>
        public void MarkStack(int? seat, int stack = 0)
        {
            if (!seat.HasValue) { stackMarked = false; return; }
            if (seat.Value < 0 || seat.Value > 3) throw new System.ArgumentOutOfRangeException(nameof(seat));
            if (stack < 0 || stack > 14) throw new System.ArgumentOutOfRangeException(nameof(stack));
            if (scene == null || stackMarker == null) throw new System.InvalidOperationException("Table effects must be initialized before marking a stack.");
            var pose = scene.SeatPose(seat.Value, (stack - 7) * .057f, TY + .0016f, .36f, false);
            // Quad is already horizontal; only the seat's yaw applies, not a lying tile's X rotation.
            stackMarker.SetPositionAndRotation(pose.position, Quaternion.LookRotation(scene.SeatDirection(seat.Value), Vector3.up));
            stackMarked = true;
        }

        // ---------- primitives ----------
        public void Burst(Vector3 position, Color color, int count = 28, float speed = 0.55f)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * 6.283f, e = Random.value * 1.2f + 0.2f;
                var velocity = new Vector3(Mathf.Cos(a) * Mathf.Cos(e), Mathf.Sin(e) * 1.3f, Mathf.Sin(a) * Mathf.Cos(e)) * (speed * (0.5f + Random.value));
                bursts.Emit(position, velocity, color, 0.022f, 0.9f);
            }
        }

        /// <summary>tickDie dust(): normal-blended 12 → 62 mm sprites, 0.22 fade, no gravity, velocity ×0.94 per frame.</summary>
        public void DiceDust(Vector3 position, int count, float speed)
        {
            if (diceDust == null) diceDust = new GlowPoints(Points("Fx_ZarTozu", true), 0, false, .94f);
            var color = new Color32(0xE9, 0xE4, 0xD6, 255);
            for (int i = 0; i < count; i++)
            {
                var at = position + new Vector3((Random.value - .5f) * .03f, .004f, (Random.value - .5f) * .03f);
                var velocity = new Vector3((Random.value - .5f) * speed, .02f + Random.value * .03f, (Random.value - .5f) * speed);
                var tint = (Color)color; tint.a = .22f;
                diceDust.Emit(at, velocity, tint, .012f, .7f + Random.value * .4f, .062f);
            }
        }

        /// <summary>tickDie shock(): the 72-segment, 0.9-inner-radius ring; source lifetime and expansion are independent.</summary>
        public void DiceShock(Vector3 position, Color color, float life, float maxScale)
        {
            if (diceShockMesh == null) diceShockMesh = Ring(.9f, 1, 72);
            var material = Additive(color, ThreeBlend.Additive(.95f));
            var ring = Flat("zar_sekme_halkasi", diceShockMesh, material, position + Vector3.up * .003f, .02f);
            diceShocks.Add((ring, material, color, Time.time, life, maxScale));
        }

        /// <summary>Rocket smoke: #C9D0E6, 1.3 s, rising 3 cm/s; 0.2 and 2 → 7 cm (the reference 0.32 / 13 cm turns into a solid band where the path passes close to the camera).</summary>
        public void Smoke(Vector3 position)
        {
            var jitter = new Vector3(Random.value - 0.5f, Random.value - 0.5f, Random.value - 0.5f) * 0.02f;
            haze.Emit(position + jitter, new Vector3(0, 0.03f, 0), new Color(0.788f, 0.816f, 0.902f, 0.2f), 0.02f, 1.3f, 0.07f, false);
        }

        /// <summary>Tea / coffee steam puff: white, 1.4 → 4.4 cm, rises 7.5 cm over a 3.1 s cycle at 0.2·sin(π·phase).</summary>
        public void Steam(Vector3 position)
        {
            haze.Emit(position, new Vector3(0, 0.075f / 3.125f, 0), new Color(1, 1, 1, 0.2f), 0.014f, 3.125f, 0.044f, true);
        }

        /// <summary>spec/23 taş izi: one soft glow (50 mm → 35 %, 0.38 s, opacity 0.14·sin(πu)), additive, no sparks.</summary>
        public void Trail(Vector3 position, Color color)
        {
            sparks.Emit(position, Vector3.zero, new Color(color.r, color.g, color.b, 0.14f), 0.05f, 0.38f, 0.0175f, true);
        }
        public static readonly Color TrailGold = new Color32(0xFF, 0xD9, 0x8A, 255), TrailGreen = new Color32(0x8E, 0xF0, 0xB0, 255);

        public void SparkAt(Vector3 position, Color color)
        {
            sparks.Emit(position, Vector3.zero, new Color(color.r, color.g, color.b, 0.8f), 0.03f, 0.5f);
        }

        /// <summary>A ring wave on the felt at <paramref name="position"/>'s x/z.</summary>
        public void Wave(Vector3 position, Color color, float delay = 0)
        {
            if (delay > 0) { scheduled.Add((Time.time + delay, () => Wave(position, color))); return; }
            var m = Additive(color, 0.9f);
            var t = Flat("halka_dalga", ringMesh, m, new Vector3(position.x, TY + 0.002f, position.z), 0.03f);
            waves.Add((t, m, color, Time.time));
        }

        public void After(float seconds, System.Action run) => scheduled.Add((Time.time + seconds, run));

        // ---------- landings (spec/12 §1) ----------
        public void Landed(TileView tile, string kind)
        {
            var p = tile.transform.position; var onFelt = new Vector3(p.x, TY, p.z);
            switch (kind)
            {
                case "disc": Wave(onFelt, Gold); Burst(p, Spark, 12, 0.25f); break;
                case "meld": Wave(onFelt, Green); Burst(p, Spark, 12, 0.25f); break;
                case "mine": Burst(p, Spark, 10, 0.18f); break;
                case "reveal": Burst(p, Spark, 40, 0.5f); Wave(onFelt, Accent); SparkAt(p, White); break;
                case "deal": if (Random.value < 0.35f) Burst(p, Spark, 6, 0.14f); break;
            }
        }

        // ---------- moments ----------
        /// <summary>Shuffle beat (spec/10 §2): rings at 0 / 170 / 340 ms, 50 + 30 particles, deck spin + bounce, holo boost.</summary>
        public void DealFx()
        {
            var c = new Vector3(0, TY, 0);
            Wave(c, Gold); Wave(c, Accent, 0.17f); Wave(c, White, 0.34f);
            Burst(new Vector3(0, TY + 0.1f, 0), Spark, 50, 0.7f); Burst(new Vector3(0, TY + 0.1f, 0), Accent, 30, 0.5f);
            deckSpinT = Time.time; holoBoostUntil = Time.time + 2.6f;
        }

        /// <summary>
        /// Finish (spec/10 §7): gold spot over the finisher's corner (3.4 s sine envelope, peak 40), eight confetti bursts
        /// 240 ms apart over the avatar with rings on every other one.
        /// </summary>
        public void Celebrate(Vector3 tablePoint, Vector3 avatarPoint)
        {
            winSpot.transform.position = tablePoint + Vector3.up * 1.8f; winSpot.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); winSpotT = Time.time;
            Color[] colours = { Hex("#FFD964"), Hex("#FF6A9A"), Hex("#7FE3FF"), Hex("#8EF0B0"), Hex("#B79CFF") };
            for (int i = 0; i < 8; i++)
            {
                int k = i;
                After(i * 0.24f, () =>
                {
                    Burst(avatarPoint + new Vector3((Random.value - 0.5f) * 0.3f, 0.05f, (Random.value - 0.5f) * 0.3f), colours[k % 5], 36, 0.9f);
                    if (k % 2 == 0) Wave(tablePoint, colours[k % 5]);
                });
            }
        }

        /// <summary>
        /// The state router's freeze (g101-clock.js: every tween sits at the canonical 100000 ms): bursts, sparks, smoke, dice
        /// dust, ring waves and dice shocks have long faded. Pulses, LEDs and the finish sequence keep their frame.
        /// </summary>
        public void Settle()
        {
            bursts?.Clear(); sparks?.Clear(); haze?.Clear(); diceDust?.Clear();
            foreach (var w in waves) { Destroy(w.T.gameObject); Destroy(w.M); }
            waves.Clear();
            foreach (var shock in diceShocks) { Destroy(shock.T.gameObject); Destroy(shock.M); }
            diceShocks.Clear();
        }

        /// <summary>Per-frame pulse inputs from the game.</summary>
        public void SetState(bool discarding, bool drawing, bool previousPile, int deck, SideRing ring = default)
        { canDiscard = discarding; canDraw = drawing; sidePulse = previousPile; deckCount = deck; ringState = ring; }

        /// <summary>
        /// table3d.js left-pile ring: shown while untouched, then only while its tile is hovered or dragged or the taken tile is
        /// dragged back (ringGlow's visible); hot (drawable, or returning) it pulses .5–1 and grows to 1.12, hovered (or the
        /// returning tile within 95 px) it is 1 and 1.4×.
        /// </summary>
        public struct SideRing { public bool Hidden, Hover, Returning; }
        /// <summary>RackInput: a rack tile is dragged (ZoneDrag) / sits over my discard ring (ZoneIn).</summary>
        public bool ZoneIn { get; set; }
        public bool ZoneDrag { get; set; }
        private float zoneScale = 1;
        private SideRing ringState;
        private float sideRingScale = 1;

        private void Update()
        {
            if (scene == null) return;
            // Pulses read the clock: at the g101-clock freeze the reference frame sits at the canonical 100 s.
            float now = Time.time, t = GameClock.Frozen ? (float)(GameClock.CanonMs / 1000) : now;
            var cam = scene.CameraRig != null ? scene.CameraRig.Camera : null;
            StepMotes(GameClock.Frozen ? (float)(GameClock.CanonMs / 1000) : now, cam);
            bursts.Step(Time.deltaTime, cam); sparks.Step(Time.deltaTime, cam); haze.Step(Time.deltaTime, cam); diceDust?.Step(Time.deltaTime, cam);
            StepFinish(now, Time.deltaTime, cam);
            for (int i = scheduled.Count - 1; i >= 0; i--) if (scheduled[i].At <= now) { var run = scheduled[i].Run; scheduled.RemoveAt(i); run(); }
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i]; float u = (now - w.T0) / 0.7f;
                if (u >= 1) { Destroy(w.T.gameObject); Destroy(w.M); waves.RemoveAt(i); continue; }
                w.T.localScale = Vector3.one * (0.03f + EaseOutCubic(u) * 0.16f); SetAlpha(w.M, w.C, ThreeBlend.Additive(0.9f * (1 - u)));
            }
            for (int i = diceShocks.Count - 1; i >= 0; i--)
            {
                var shock = diceShocks[i]; float u = (now - shock.T0) / shock.Life;
                if (u >= 1) { Destroy(shock.T.gameObject); Destroy(shock.M); diceShocks.RemoveAt(i); continue; }
                shock.T.localScale = Vector3.one * (.02f + EaseOutCubic(u) * shock.Max);
                SetAlpha(shock.M, shock.C, ThreeBlend.Additive(.95f * Mathf.Pow(1 - u, 1.4f)));
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5);
            // Discard ring (table3d.js frame): a tile over it 1.45× and full, a rack tile dragged 1.2×, hot while discarding
            // (0.55 + 0.45 p, 1 + 0.08 p), quiet at 0.16; the scale eases (dt·12). Fill .42 / .22 / .12 + .12 p / 0.
            if (zone != null && zoneMaterial != null)
            {
                bool zIn = canDiscard && ZoneIn, zDrag = canDiscard && ZoneDrag;
                zoneScale += ((zIn ? 1.45f : zDrag ? 1.2f : canDiscard ? 1 + pulse * 0.08f : 1) - zoneScale) * Mathf.Min(1, Time.deltaTime * 12);
                SetAlpha(zoneMaterial, Accent, ThreeBlend.Normal(zIn ? 1 : canDiscard ? 0.55f + pulse * 0.45f : 0.16f));
                zone.localScale = Vector3.one * zoneScale;
                if (zoneFillTransform != null) zoneFillTransform.localScale = Vector3.one * 0.058f * zoneScale;
                SetAlpha(zoneFill, Accent, ThreeBlend.Additive(zIn ? .42f : zDrag ? .22f : canDiscard ? 0.12f + pulse * 0.12f : 0));
            }
            SetAlpha(deckGlowMaterial, Accent, ThreeBlend.Additive(canDraw ? 0.25f + pulse * 0.35f : 0));
            if (deckGlow != null)
            {
                // table3d.js layoutDeck / frame: the glow follows the deck's x (deckGlow.position.x = deckIM.position.x), 0.7
                // over the two piles (≤ 21, not the deal's heap), else max(0.6, cols · 0.18) of the 0.5 m plane.
                int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.CeilToInt(Mathf.Max(1, deckCount) / 5f) / (float)(deckCount > 60 ? 4 : deckCount > 25 ? 2 : 1)));
                bool piles = deckCount <= 21 && !scene.DeckHeap;
                deckGlow.localScale = Vector3.one * (piles ? 0.7f : Mathf.Max(0.6f, cols * 0.18f)) * 0.25f;   // the 0.5 m plane on a ±1 quad
                deckGlow.position = ThreeSpace.ToUnity(scene.DeckShift, TY + 0.002f, 0);
            }
            {
                bool hot = canDraw && sidePulse || ringState.Returning, hover = hot && ringState.Hover;
                sideRingScale += ((hover ? 1.4f : hot ? 1.12f + pulse * .06f : 1) - sideRingScale) * Mathf.Min(1, Time.deltaTime * 12);
                if (sideRing != null) sideRing.localScale = Vector3.one * 0.066f * sideRingScale;
                SetAlpha(sideMaterial, Accent, ThreeBlend.Normal(ringState.Hidden ? 0 : hover ? 1 : hot ? 0.5f + pulse * 0.5f : 0));
            }
            // LEDs: emissive 2.2 + 0.25·sin(1.2t) relative to their authored 2.4.
            float led = (2.2f + Mathf.Sin(t * 1.2f) * 0.25f) / 2.4f;
            foreach (var (m, c) in leds) if (m != null) m.SetColor("_EmissionColor", c * led);
            // table3d.js frame: stack-marker pulse and the separate 3/s transition when the stock moves left.
            stackAlpha += ((stackMarked ? .6f + .3f * Mathf.Sin(now * 6) : 0) - stackAlpha) * Mathf.Min(1, Time.deltaTime * 8);
            SetAlpha(stackMarkerMaterial, StackGold, ThreeBlend.Additive(stackAlpha));
            pileK += (((PileLeft?.Invoke() ?? false) ? 1f : 0f) - pileK) * Mathf.Min(1, Time.deltaTime * 3);
            // Holo ring: .34 ± .05 at .9 rad/s; the left stock reduces it by 55 %, before the boost.
            float boost = Mathf.Max(0, holoBoostUntil - now);
            if (holo != null) holo.Rotate(Vector3.up, (0.12f + boost * 1.6f) * Time.deltaTime * Mathf.Rad2Deg, Space.World);
            if (holoMaterial != null && holoMaterial.HasProperty("_BaseColor")) { var c = holoMaterial.GetColor("_BaseColor"); c.a = ThreeBlend.Additive((.34f + Mathf.Sin(t * .9f) * .05f) * (1 - pileK * .55f) + Mathf.Min(.5f, boost * .25f)); holoMaterial.SetColor("_BaseColor", c); }
            // Deck spin 0.9 s: one turn, 5 cm bounce.
            float ds = (now - deckSpinT) / 0.9f;
            if (deckRoot != null) { if (ds >= 0 && ds <= 1) { deckRoot.localRotation = Quaternion.AngleAxis(EaseInOutCubic(ds) * 360, Vector3.up); deckRoot.localPosition = Vector3.up * (Mathf.Sin(Mathf.PI * ds) * 0.05f); } else { deckRoot.localRotation = Quaternion.identity; deckRoot.localPosition = Vector3.zero; } }
            // "New tile" bar blinks: opacity 0.55 + 0.45·sin(9t).
            if (TileFactory.NewBar != null) SetAlpha(TileFactory.NewBar, Gold, ThreeBlend.Normal(0.55f + 0.45f * Mathf.Sin(t * 9)));
            float ws = (now - winSpotT) / 3.4f;
            winSpot.intensity = ws >= 0 && ws <= 1 ? TableScene.ThreeLocalIntensity(40, 1.4f, 1.8f) * Mathf.Sin(Mathf.PI * Mathf.Min(1, ws * 1.6f)) * (ws < 0.6f ? 1 : (1 - ws) / 0.4f) : 0;
        }

        // ---------- resources ----------
        private static void SetAlpha(Material m, Color c, float a) { if (m == null) return; c.a = a; m.SetColor("_BaseColor", c); }

        private Transform Flat(string name, Mesh mesh, Material material, Vector3 position, float scale)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false); go.transform.position = position; go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>URP Unlit, additive; alpha factors keep the target's alpha (the HUD composites this camera).</summary>
        private Material Additive(Color color, float alpha, Texture texture = null, bool particles = false)
        {
            // Particles need the particle shader (vertex colour = per-particle colour and fade); the builder keeps a material
            // asset in Resources so player builds include that shader.
            var template = particles ? Resources.Load<Material>("Galaxy101/Table/Fx/fx_particles") : null;
            var m = template != null ? new Material(template) : new Material(Shader.Find(particles ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Unlit"));
            m.name = "fx_toplamali"; m.SetFloat("_Cull", 0);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2); m.SetFloat("_ZWrite", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.Zero); m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent + 10;
            if (texture != null) m.SetTexture("_BaseMap", texture);
            SetAlpha(m, color, alpha); return m;
        }

        // ---------- table3d.js motes: 90 fine specks drifting up through the light cone (depth, very faint) ----------
        private const int MoteCount = 90;
        private Mesh moteMesh;
        private (float A, float R, float Y, float S, float Ph)[] moteSeed;
        private Vector3[] moteVerts; private Color[] moteColors;

        private void InitMotes()
        {
            var filter = Points("Fx_Toz"); moteMesh = new Mesh { name = "toz" }; moteMesh.MarkDynamic(); filter.sharedMesh = moteMesh;
            var random = new System.Random(7); moteSeed = new (float, float, float, float, float)[MoteCount];
            for (int i = 0; i < MoteCount; i++)
                moteSeed[i] = ((float)random.NextDouble() * 6.283f, Mathf.Sqrt((float)random.NextDouble()) * .95f, (float)random.NextDouble(), .15f + (float)random.NextDouble() * .35f, (float)random.NextDouble() * 6.283f);
            moteVerts = new Vector3[MoteCount * 4]; moteColors = new Color[MoteCount * 4]; var uv = new Vector2[MoteCount * 4]; var tri = new int[MoteCount * 6];
            var c = new Color(1, 0xF1 / 255f, 0xD6 / 255f, ThreeBlend.Additive(.35f));
            for (int i = 0; i < MoteCount; i++)
            {
                int b = i * 4;
                uv[b] = new Vector2(0, 0); uv[b + 1] = new Vector2(0, 1); uv[b + 2] = new Vector2(1, 1); uv[b + 3] = new Vector2(1, 0);
                for (int k = 0; k < 4; k++) moteColors[b + k] = c;
                tri[i * 6] = b; tri[i * 6 + 1] = b + 1; tri[i * 6 + 2] = b + 2; tri[i * 6 + 3] = b; tri[i * 6 + 4] = b + 2; tri[i * 6 + 5] = b + 3;
            }
            moteMesh.vertices = moteVerts; moteMesh.colors = moteColors; moteMesh.uv = uv; moteMesh.triangles = tri;
            moteMesh.bounds = new Bounds(new Vector3(0, TY + .8f, 0), new Vector3(2.2f, 1.8f, 2.2f));
        }

        private void StepMotes(float t, Camera cam)
        {
            if (moteMesh == null) return;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right, up = cam != null ? cam.transform.up : Vector3.up;
            const float half = .003f;   // PointsMaterial size 0.006 (world units)
            for (int i = 0; i < MoteCount; i++)
            {
                var m = moteSeed[i]; float yy = Mathf.Repeat(m.Y + t * .012f * m.S, 1);
                var p = ThreeSpace.ToUnity(Mathf.Cos(m.A + t * .03f * m.S) * m.R + Mathf.Sin(t * .4f + m.Ph) * .01f, TY + .08f + yy * 1.4f, Mathf.Sin(m.A + t * .03f * m.S) * m.R * .8f);
                int b = i * 4;
                moteVerts[b] = p - right * half - up * half; moteVerts[b + 1] = p - right * half + up * half; moteVerts[b + 2] = p + right * half + up * half; moteVerts[b + 3] = p + right * half - up * half;
            }
            moteMesh.vertices = moteVerts;
        }

        private MeshFilter Points(string name, bool blended = false)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>(); var r = go.AddComponent<MeshRenderer>();
            var m = Additive(Color.white, 1, glow, true);
            if (blended)
            {
                // Smoke and steam are "normal" sprites: alpha blended, coverage composited over the target's alpha.
                m.name = "fx_duman"; m.SetFloat("_Blend", 0); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            }
            r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return filter;
        }

        /// <summary>
        /// Camera-facing glow points in one dynamic mesh (THREE.Points / sprites): per point position, velocity, colour,
        /// size and life; gravity pulls bursts down; alpha fades to 0; sparks shrink to 40 %. No particle module needed.
        /// </summary>
        private sealed class GlowPoints
        {
            private struct P { public Vector3 Pos, Vel; public Color Col; public float Size, SizeEnd, Life, Age; public bool Sine; }
            private readonly List<P> points = new List<P>(512);
            private readonly MeshFilter filter; private readonly Mesh mesh;
            private readonly float gravity, velocityDamping; private readonly bool shrink, pointSized;
            private const int Capacity = 2000;
            // Per frame only positions and colours change; the quads' uvs and indices are built once for the capacity.
            private readonly List<Vector3> v = new List<Vector3>(Capacity * 4); private readonly List<Color> c = new List<Color>(Capacity * 4);
            private static List<Vector2> uv; private static List<int> tri;
            private int shown = -1;

            public GlowPoints(MeshFilter filter, float gravity, bool shrink, float velocityDamping = 1, bool pointSized = false)
            {
                this.filter = filter; this.gravity = gravity; this.shrink = shrink; this.velocityDamping = velocityDamping; this.pointSized = pointSized;
                mesh = new Mesh { name = filter.name }; mesh.MarkDynamic(); filter.sharedMesh = mesh;
                // The points stay around the table: fixed bounds instead of recalculating them every frame.
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 8);
                if (uv == null)
                {
                    uv = new List<Vector2>(Capacity * 4); tri = new List<int>(Capacity * 6);
                    for (int q = 0; q < Capacity; q++)
                    {
                        int b = q * 4;
                        uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
                        tri.Add(b); tri.Add(b + 1); tri.Add(b + 2); tri.Add(b); tri.Add(b + 2); tri.Add(b + 3);
                    }
                }
            }

            public int Count => points.Count;
            public void Clear() => points.Clear();

            public void Release()
            {
                if (mesh != null) Object.Destroy(mesh);
                var r = filter != null ? filter.GetComponent<MeshRenderer>() : null;
                if (r != null && r.sharedMaterial != null) Object.Destroy(r.sharedMaterial);
            }

            public void Emit(Vector3 pos, Vector3 vel, Color col, float size, float life, float sizeEnd = -1, bool sine = false)
            {
                if (points.Count < Capacity) points.Add(new P { Pos = pos, Vel = vel, Col = col, Size = size, SizeEnd = sizeEnd, Life = life, Sine = sine });
            }

            public void Step(float dt, Camera cam)
            {
                v.Clear(); c.Clear();
                Vector3 right = cam != null ? cam.transform.right : Vector3.right, up = cam != null ? cam.transform.up : Vector3.up;
                // THREE.Points uses viewportHeight/2; sprites additionally include the perspective's cot(fov/2).
                float pointScale = pointSized ? Mathf.Tan((cam != null ? cam.fieldOfView : FieldOfView) * .5f * Mathf.Deg2Rad) : 1;
                for (int i = points.Count - 1; i >= 0; i--)
                {
                    var p = points[i]; p.Age += dt;
                    if (p.Age >= p.Life) { points.RemoveAt(i); continue; }
                    p.Vel.y -= gravity * dt; p.Pos += p.Vel * dt; p.Vel *= velocityDamping; points[i] = p;
                    float u = p.Age / p.Life, size = p.SizeEnd >= 0 ? Mathf.Lerp(p.Size, p.SizeEnd, p.Sine ? u : 1 - (1 - u) * (1 - u) * (1 - u)) : p.Size;
                    float half = size * pointScale * (shrink ? 1 - u * 0.6f : 1) * 0.5f;
                    var col = p.Col; col.a *= p.Sine ? Mathf.Sin(Mathf.PI * u) : 1 - u;
                    int b = v.Count;
                    v.Add(p.Pos - right * half - up * half); v.Add(p.Pos - right * half + up * half); v.Add(p.Pos + right * half + up * half); v.Add(p.Pos + right * half - up * half);
                    c.Add(col); c.Add(col); c.Add(col); c.Add(col);
                }
                int quads = v.Count / 4;
                if (quads == shown)
                {
                    if (quads > 0) { mesh.SetVertices(v); mesh.SetColors(c); }
                    return;
                }
                // The quad count changed: indices first go, so no index points past the new vertex count.
                shown = quads;
                mesh.Clear();
                if (quads == 0) return;
                mesh.SetVertices(v); mesh.SetColors(c); mesh.SetUVs(0, uv, 0, quads * 4);
                mesh.SetTriangles(tri, 0, quads * 6, 0, false);
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 8);
            }
        }

        /// <summary>glowTex(): radial white, 1 → 0.55 at 25 % → 0.</summary>
        private static Texture2D GlowTexture()
        {
            const int size = 128; var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "fx_parilti", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float d = Mathf.Min(1, new Vector2(x - 63.5f, y - 63.5f).magnitude / 64f);
                float a = d < 0.25f ? Mathf.Lerp(1, 0.55f, d / 0.25f) : Mathf.Lerp(0.55f, 0, (d - 0.25f) / 0.75f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            t.SetPixels32(px); t.Apply(); return t;
        }

        private static Mesh Ring(float inner, float outer, int segments)
        {
            var v = new Vector3[segments * 2]; var tri = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v[i * 2] = d * inner; v[i * 2 + 1] = d * outer; int n = (i + 1) % segments;
                tri[i * 6] = i * 2; tri[i * 6 + 1] = n * 2; tri[i * 6 + 2] = i * 2 + 1; tri[i * 6 + 3] = i * 2 + 1; tri[i * 6 + 4] = n * 2; tri[i * 6 + 5] = n * 2 + 1;
            }
            var m = new Mesh { name = "fx_halka", vertices = v, triangles = tri }; m.RecalculateNormals(); return m;
        }

        private static Mesh Disc(float radius, int segments)
        {
            var v = new Vector3[segments + 1]; var tri = new int[segments * 3];
            for (int i = 0; i < segments; i++) { float a = i * Mathf.PI * 2 / segments; v[i + 1] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius; tri[i * 3] = 0; tri[i * 3 + 1] = 1 + (i + 1) % segments; tri[i * 3 + 2] = 1 + i; }
            var m = new Mesh { name = "fx_daire", vertices = v, triangles = tri }; m.RecalculateNormals(); return m;
        }

        private static Mesh Quad()
        {
            var m = new Mesh { name = "fx_kare", vertices = new[] { new Vector3(-1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, -1) },
                uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) }, triangles = new[] { 0, 1, 2, 0, 2, 3 } };
            m.RecalculateNormals(); return m;
        }

        private void OnDestroy()
        {
            PileLeft = null;
            ReleaseFinish();
            if (tableHalo != null) Destroy(tableHalo.gameObject);
            if (stackMarker != null) Destroy(stackMarker.gameObject);
            foreach (var w in waves) { if (w.M != null) Destroy(w.M); if (w.T != null) Destroy(w.T.gameObject); }
            foreach (var shock in diceShocks) { if (shock.M != null) Destroy(shock.M); if (shock.T != null) Destroy(shock.T.gameObject); }
            if (glow != null) Destroy(glow);
            // Everything else this component made at runtime: meshes and materials.
            foreach (var m in new[] { ringMesh, discMesh, quadMesh, diceShockMesh, moteMesh }) if (m != null) Destroy(m);
            foreach (var p in new[] { bursts, sparks, haze, diceDust }) p?.Release();
            foreach (var m in new[] { zoneFill, sideMaterial, deckGlowMaterial, tableHaloMaterial, stackMarkerMaterial }) if (m != null) Destroy(m);
        }
    }
}
