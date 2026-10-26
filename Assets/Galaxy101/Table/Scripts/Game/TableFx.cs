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
    public sealed class TableFx : MonoBehaviour
    {
        public static readonly Color Gold = Hex("#FFD964"), Spark = Hex("#FFE08A"), Green = Hex("#3CCB6E"), White = Color.white;
        private static readonly Color StackGold = Hex("#FFC94A");

        private TableScene scene;
        private Texture2D glow;
        private GlowPoints bursts, sparks, haze;
        private Mesh ringMesh, discMesh, quadMesh;
        private Material zoneMaterial, zoneFill, sideMaterial, deckGlowMaterial, holoMaterial, tableHaloMaterial, stackMarkerMaterial;
        private Transform zone, zoneFillTransform, sideRing, deckGlow, holo, deckRoot, tableHalo, stackMarker;
        private readonly List<(Transform T, Material M, Color C, float T0)> waves = new List<(Transform, Material, Color, float)>();
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
        public int LivePoints => (bursts?.Count ?? 0) + (sparks?.Count ?? 0);
        public int LiveHaze => haze?.Count ?? 0;
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
            bursts = new GlowPoints(Points("Fx_Bursts"), 1.4f, false);
            sparks = new GlowPoints(Points("Fx_Sparks"), 0, true);
            haze = new GlowPoints(Points("Fx_Duman", true), 0, false);
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
        /// <summary>
        /// Elden bitiş (spec/23): the tile rises 0.58 m (0–46 %), trembles while growing ×1.15 → 1.4 with sparks, then
        /// accelerates (cubic) into <paramref name="impact"/> trailing smoke. On impact: white / gold / orange light spheres,
        /// three shock rings, 280 particles, a mushroom cloud of 34 puffs (stem, then cap, orange to grey), a point light
        /// 90 → 0 (e^−3t), a 1.2 s camera shake and every lying tile hopping by distance. Returns seconds to impact.
        /// </summary>
        public float Nuclear(Transform tile, Vector3 impact, TableCameraRig rig, System.Action onImpact)
        {
            StartCoroutine(NuclearRun(tile, impact, rig, onImpact));
            return 1.25f;
        }

        private System.Collections.IEnumerator NuclearRun(Transform tile, Vector3 impact, TableCameraRig rig, System.Action onImpact)
        {
            Vector3 start = tile.position, top = start + Vector3.up * 0.58f; var startScale = tile.localScale; var rotation = tile.rotation;
            float t0 = Time.time, emit = 0; var gold = Hex("#FFC94A");
            // The reference emits once per 60 Hz frame (sparks on half of them): rates, so any frame rate gives the same amount.
            while (Time.time - t0 < 0.8f)
            {
                float u = (Time.time - t0) / 0.8f, rise = Mathf.Clamp01(u / 0.46f), k = 1 - Mathf.Pow(1 - rise, 3);
                float tremble = u > 0.46f ? 0.006f * (u - 0.46f) / 0.54f : 0;
                tile.position = Vector3.Lerp(start, top, k) + new Vector3(Random.Range(-tremble, tremble), Random.Range(-tremble, tremble), Random.Range(-tremble, tremble));
                tile.localScale = startScale * Mathf.Lerp(1.15f, 1.4f, u);
                tile.rotation = rotation * Quaternion.AngleAxis(u * 25, Vector3.right);
                if (u > 0.46f) for (emit += Time.deltaTime * 30; emit >= 1; emit--) SparkAt(tile.position + Random.insideUnitSphere * 0.03f, gold);
                yield return null;
            }
            t0 = Time.time; emit = 0; var from = tile.position;
            while (Time.time - t0 < 0.45f)
            {
                float u = (Time.time - t0) / 0.45f, k = u * u * u;
                tile.position = Vector3.Lerp(from, impact, k); tile.Rotate(Vector3.up, 900 * Time.deltaTime, Space.World);
                for (emit += Time.deltaTime * 60; emit >= 1; emit--) Smoke(tile.position);
                yield return null;
            }
            tile.position = impact; tile.localScale = startScale;
            onImpact?.Invoke();
            rig?.Shake(1.2f);
            var colors = new[] { Color.white, Hex("#FFD964"), Hex("#FF8A3D") };
            for (int i = 0; i < 3; i++) { Burst(impact + Vector3.up * 0.02f, colors[i], 95, 1.1f + i * 0.35f); Wave(impact, colors[i], i * 0.18f); }
            var lightGo = new GameObject("nukleer_isik"); lightGo.transform.position = impact + Vector3.up * 0.3f;
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Point; light.color = Hex("#FFB25A"); light.range = 4.5f; light.intensity = TableScene.ThreeLocalIntensity(90, 1.4f, 0.3f);
            var lying = new System.Collections.Generic.List<(Transform T, Vector3 P, float D)>();
            foreach (var view in FindObjectsByType<TileView>(FindObjectsSortMode.None))
                if (view != null && !view.Tweening && view.transform != tile && view.transform.position.y < TY + 0.05f) lying.Add((view.transform, view.transform.position, Vector3.Distance(view.transform.position, impact)));
            t0 = Time.time; int puffs = 0;
            while (Time.time - t0 < 2.2f)
            {
                float t = Time.time - t0;
                light.intensity = TableScene.ThreeLocalIntensity(90, 1.4f, 0.3f) * Mathf.Exp(-3 * t); // three: PointLight(#FFB25A, 90, 4.5, decay 1.4), 0.3 m up
                // The mushroom: a rising stem for 0.6 s, then the spreading cap.
                while (puffs < 34 && puffs < t / 1.1f * 34)
                {
                    bool cap = puffs >= 12; float a = Random.value * 6.283f, r = cap ? 0.06f + Random.value * 0.14f : Random.value * 0.03f;
                    var at = impact + new Vector3(Mathf.Cos(a) * r, cap ? 0.32f + Random.value * 0.08f : 0.03f + puffs * 0.025f, Mathf.Sin(a) * r);
                    var tone = Color.Lerp(Hex("#FF8A3D"), Hex("#8A8E9C"), puffs / 34f); tone.a = 0.45f;
                    haze.Emit(at, new Vector3(0, cap ? 0.03f : 0.12f, 0), tone, 0.06f, 2.4f - t * 0.6f, cap ? 0.24f : 0.12f, false);
                    puffs++;
                }
                foreach (var (tr, p, d) in lying)
                {
                    if (tr == null) continue;
                    float delay = d * 0.6f, u = Mathf.Clamp01((t - delay) / 0.45f), hop = u <= 0 || u >= 1 ? 0 : Mathf.Sin(u * Mathf.PI) * 0.035f / (1 + d * 3);
                    tr.position = p + Vector3.up * hop;
                }
                yield return null;
            }
            foreach (var (tr, p, _) in lying) if (tr != null) tr.position = p;
            Destroy(lightGo);
        }

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

        /// <summary>Per-frame pulse inputs from the game.</summary>
        public void SetState(bool discarding, bool drawing, bool previousPile, int deck) { canDiscard = discarding; canDraw = drawing; sidePulse = previousPile; deckCount = deck; }

        private void Update()
        {
            if (scene == null) return;
            float now = Time.time, t = now;
            var cam = scene.CameraRig != null ? scene.CameraRig.Camera : null;
            bursts.Step(Time.deltaTime, cam); sparks.Step(Time.deltaTime, cam); haze.Step(Time.deltaTime, cam);
            for (int i = scheduled.Count - 1; i >= 0; i--) if (scheduled[i].At <= now) { var run = scheduled[i].Run; scheduled.RemoveAt(i); run(); }
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i]; float u = (now - w.T0) / 0.7f;
                if (u >= 1) { Destroy(w.T.gameObject); Destroy(w.M); waves.RemoveAt(i); continue; }
                w.T.localScale = Vector3.one * (0.03f + EaseOutCubic(u) * 0.16f); SetAlpha(w.M, w.C, ThreeBlend.Additive(0.9f * (1 - u)));
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5);
            // Discard ring: hot while discarding (0.55 + 0.45 p, scale 1 + 0.08 p), quiet at 0.16.
            if (zone != null && zoneMaterial != null)
            {
                SetAlpha(zoneMaterial, Accent, ThreeBlend.Normal(canDiscard ? 0.55f + pulse * 0.45f : 0.16f));
                zone.localScale = Vector3.one * (canDiscard ? 1 + pulse * 0.08f : 1);
                SetAlpha(zoneFill, Accent, ThreeBlend.Additive(canDiscard ? 0.12f + pulse * 0.12f : 0));
            }
            SetAlpha(deckGlowMaterial, Accent, ThreeBlend.Additive(canDraw ? 0.25f + pulse * 0.35f : 0));
            if (deckGlow != null) { int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.CeilToInt(Mathf.Max(1, deckCount) / 5f) / (float)(deckCount > 60 ? 4 : deckCount > 25 ? 2 : 1))); deckGlow.localScale = Vector3.one * Mathf.Max(0.6f, cols * 0.18f) * 0.5f; }
            SetAlpha(sideMaterial, Accent, ThreeBlend.Normal(canDraw && sidePulse ? 0.5f + pulse * 0.5f : 0));
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
            private readonly float gravity; private readonly bool shrink;
            private const int Capacity = 2000;
            // Per frame only positions and colours change; the quads' uvs and indices are built once for the capacity.
            private readonly List<Vector3> v = new List<Vector3>(Capacity * 4); private readonly List<Color> c = new List<Color>(Capacity * 4);
            private static List<Vector2> uv; private static List<int> tri;
            private int shown = -1;

            public GlowPoints(MeshFilter filter, float gravity, bool shrink)
            {
                this.filter = filter; this.gravity = gravity; this.shrink = shrink;
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
                for (int i = points.Count - 1; i >= 0; i--)
                {
                    var p = points[i]; p.Age += dt;
                    if (p.Age >= p.Life) { points.RemoveAt(i); continue; }
                    p.Vel.y -= gravity * dt; p.Pos += p.Vel * dt; points[i] = p;
                    float u = p.Age / p.Life, size = p.SizeEnd >= 0 ? Mathf.Lerp(p.Size, p.SizeEnd, p.Sine ? u : 1 - (1 - u) * (1 - u) * (1 - u)) : p.Size;
                    float half = size * (shrink ? 1 - u * 0.6f : 1) * 0.5f;
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
            if (tableHalo != null) Destroy(tableHalo.gameObject);
            if (stackMarker != null) Destroy(stackMarker.gameObject);
            foreach (var w in waves) { if (w.M != null) Destroy(w.M); if (w.T != null) Destroy(w.T.gameObject); }
            if (glow != null) Destroy(glow);
            // Everything else this component made at runtime: meshes and materials.
            foreach (var m in new[] { ringMesh, discMesh, quadMesh }) if (m != null) Destroy(m);
            foreach (var p in new[] { bursts, sparks, haze }) p?.Release();
            foreach (var m in new[] { zoneFill, sideMaterial, deckGlowMaterial, tableHaloMaterial, stackMarkerMaterial }) if (m != null) Destroy(m);
        }
    }
}
