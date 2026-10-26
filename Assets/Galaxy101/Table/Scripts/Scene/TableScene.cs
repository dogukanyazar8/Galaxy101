using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Galaxy101.Table.TableConstants;

namespace Galaxy101.Table
{
    /// <summary>
    /// Builds the 3D table scene (spec/14 §2) from the asset catalog: lighting, room, table, four seats with
    /// racks + 30 slots, opponent chairs, deck, indicator, discard zone and piles. Layout math is ported from
    /// table3d.js in three space and mirrored once through <see cref="ThreeSpace"/>.
    /// </summary>
    public sealed class TableScene : MonoBehaviour
    {
        [SerializeField] private string planet = "Satürn";
        [SerializeField] private string tableSkin = "station";
        [SerializeField] private string rackSkin = "walnut";
        [SerializeField] private string tileSkin = "classic";
        [SerializeField] private bool demoLayout = true;

        private TableAssetCatalog catalog;
        private readonly SkinService skins = new SkinService();
        private readonly ThreeFrame[] seatFrames = new ThreeFrame[4];
        private readonly ThreeFrame[] rackFrames = new ThreeFrame[4];
        private readonly Transform[] seats = new Transform[4];
        private readonly Transform[][] slots = new Transform[4][];
        private Transform tableRoot, tilesRoot;
        private GameObject tableInstance;
        private readonly GameObject[] rackInstances = new GameObject[4];
        private TileFactory tiles;

        public TableCameraRig CameraRig { get; private set; }
        public TileFactory Tiles => tiles;
        public Transform TilesRoot => tilesRoot;
        /// <summary>The Blender die prefab, or null when the catalog predates it.</summary>
        public GameObject DiePrefab => catalog != null ? catalog.Die : null;
        public Transform DiscardZone { get; private set; }

        /// <summary>Finish celebration points (table3d.js celebrate): the finisher's corner on the felt and above their avatar.</summary>
        public Vector3 CornerThree(int seat) => seat == 0 ? seatFrames[0].Pose(0, TY, 0.5f).Position : seatFrames[seat].Pose(0, TY, 0.6f).Position;
        public Vector3 Corner(int seat) => ThreeSpace.ToUnity(CornerThree(seat));
        public Vector3 AvatarPoint(int seat) => seat == 0 ? seatFrames[0].Pose(0, TY + 0.25f, 0.75f).UnityPosition : AnchorWorld("a" + seat);
        public Transform RackRoot(int seat) => rackInstances[seat].transform;
        /// <summary>World position of seat-local three.js coordinates (poseIn(seatF[seat], x, y, z)).</summary>
        public Vector3 SeatPoint(int seat, float x, float y, float z) => seatFrames[seat].Pose(x, y, z).UnityPosition;
        public string Planet => planet;
        public string TableSkin => tableSkin;
        public string RackSkin => rackSkin;
        public string TileSkin => tileSkin;
        public SkinService Skins => skins;
        public IReadOnlyList<Transform> Seats => seats;
        public Transform Slot(int seat, int row, int index) => slots[seat][row * SlotsPerRow + index];

        /// <summary>
        /// three.js (r155+, physically correct) lights a surface with BRDF_Lambert = albedo / π; URP's BRDF folds the π
        /// out (diffuse = albedo). The design's light intensities land π times too bright in URP unless scaled by this.
        /// </summary>
        public const float ThreeToUrp = 1f / Mathf.PI;

        /// <summary>
        /// URP intensity for a three.js point / spot light of <paramref name="intensity"/> with falloff 1/d^decay (three's
        /// getDistanceAttenuation; URP is always 1/d²), matched at <paramref name="distance"/> — where the light meets the
        /// table. The cut-off windows are the same in both: (1 − (d/range)⁴)².
        /// </summary>
        public static float ThreeLocalIntensity(float intensity, float decay, float distance) => intensity * ThreeToUrp * Mathf.Pow(distance, 2 - decay);

        /// <summary>Tests and tools: the planet to theme the table with instead of the player's saved menu planet.</summary>
        public static string PlanetOverride;

        private void Awake()
        {
            if (!Application.isPlaying) return;
            // G101-Masa wears the player's planet (theme tokens, felt, back light, sky): the menu's saved choice.
            planet = PlanetOverride ?? SavedPlanet() ?? planet;
            Build();
        }

        private static string SavedPlanet()
        {
            try
            {
                var path = GalaxyPreferencesBridge.ResolveStatePath();
                if (!System.IO.File.Exists(path)) return null;
                var model = MenuModel.LoadResources(path);
                return model.LastPersistenceError == null && PlanetIds.ContainsKey(model.Planet) ? model.Planet : null;
            }
            catch (System.Exception e) { Debug.LogWarning("Table planet: " + e.Message); return null; }
        }

        /// <summary>Builds the whole scene. Called from Awake in play mode, or by editor tooling (capture) directly.</summary>
        public void Build()
        {
            catalog = TableAssetCatalog.Load();
            skins.SetTheme(TableTheme.Load(planet));
            for (int k = 0; k < 4; k++)
            {
                seatFrames[k] = ThreeFrame.World.Child(Vector3.zero, ThreeSpace.Euler(0, SeatYaw[k], 0));
                rackFrames[k] = seatFrames[k].Child(RackLocal, ThreeSpace.Euler(TILT, 0, 0));
            }
            ApplyRenderSettings();
            BuildLighting();
            BuildCamera();
            BuildEnvironment();
            // A running game owns the tiles; the static T05 layout is only for the bare scene (captures, HUD test).
            if (demoLayout && FindAnyObjectByType<TableGame>() == null) BuildDemoLayout();
        }

        
        private readonly System.Collections.Generic.List<TileFactory> retiredTiles = new System.Collections.Generic.List<TileFactory>();
        /// <summary>Runtime assets this scene made (post profile and its overrides, the discard ring's mesh and material).</summary>
        private readonly System.Collections.Generic.List<Object> owned = new System.Collections.Generic.List<Object>();
        private float savedShadowDistance; private int savedCascades, savedMsaa; private UniversalRenderPipelineAsset urp;

        private void OnDestroy()
        {
            skins.Release();
            tiles?.Dispose(); tiles = null;
            foreach (var retired in retiredTiles) retired.Dispose();
            retiredTiles.Clear();
            foreach (var o in owned) Kill(o);
            owned.Clear();
            // Restore the shared URP asset (the menu and the classic table use it too).
            if (urp != null) { urp.shadowDistance = savedShadowDistance; urp.shadowCascadeCount = savedCascades; urp.msaaSampleCount = savedMsaa; }
        }

        /// <summary>
        /// spec/14: shadows only need the 3 m table (key light ortho ±1.5, 2048 map) and MSAA 4×. The project URP
        /// asset is tuned for 50 m with four cascades, which smears 2048 texels over the room and causes acne on the felt.
        /// </summary>
        private void ApplyRenderSettings()
        {
            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return;
            savedShadowDistance = urp.shadowDistance; savedCascades = urp.shadowCascadeCount; savedMsaa = urp.msaaSampleCount;
            urp.shadowDistance = 4.5f; urp.shadowCascadeCount = 1; urp.msaaSampleCount = 4;
        }

        /// <summary>
        /// Efekt kalitesi for the table (0 Düşük, 1 Orta, 2 Yüksek): Düşük draws no shadows — the shadow pass is the
        /// largest fixed GPU cost after the view's resolution and MSAA. The URP asset's values come back with the scene.
        /// </summary>
        public void SetEffectQuality(int level)
        {
            if (urp != null) urp.shadowDistance = level == 0 ? 0 : 4.5f;
        }

        private static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        // ---------- lighting (spec/09 §4) ----------
        private void BuildLighting()
        {
            var theme = skins.Theme;
            var lights = new GameObject("Lighting").transform; lights.SetParent(transform, false);
            // scene.environment (buildEnv, ×0.75) and the hemisphere light (sky / #0a0a14, 0.45): ambient probe + reflections.
            ThreeEnvironment.Apply(lights, ThreeEnvironment.TableTint, theme.Sky, new Color32(0x0a, 0x0a, 0x14, 255), 0.45f);

            Light Make(string name, LightType type, Color color, float intensity, Vector3 threePos, Vector3? threeTarget)
            {
                var go = new GameObject(name); go.transform.SetParent(lights, false);
                var light = go.AddComponent<Light>(); light.type = type; light.color = color; light.intensity = intensity * ThreeToUrp;
                go.transform.position = ThreeSpace.ToUnity(threePos);
                if (threeTarget.HasValue) go.transform.rotation = Quaternion.LookRotation(ThreeSpace.ToUnity(threeTarget.Value) - go.transform.position);
                return light;
            }
            var key = Make("Key", LightType.Directional, new Color32(0xff, 0xf4, 0xe2, 255), 1.7f, new Vector3(1.1f, 4.2f, 2.0f), new Vector3(0, TY, 0));
            key.shadows = LightShadows.Soft; key.shadowResolution = LightShadowResolution.VeryHigh;
            var keyData = key.GetComponent<UniversalAdditionalLightData>() ?? key.gameObject.AddComponent<UniversalAdditionalLightData>();
            keyData.usePipelineSettings = false; key.shadowBias = 1.0f; key.shadowNormalBias = 0.8f; // URP texel-scaled biases
            var spot = Make("Spot", LightType.Spot, Color.white, 9f, new Vector3(0, 3.2f, 0.3f), new Vector3(0, TY, 0));
            Decay(spot, 9f, 1.6f, new Vector3(0, 3.2f, 0.3f));
            spot.range = 7; spot.spotAngle = 0.62f * 2 * Mathf.Rad2Deg; spot.innerSpotAngle = spot.spotAngle * 0.3f; // penumbra 0.7
            var back = Make("Back", LightType.Point, theme.A2, 5f, new Vector3(0, 1.5f, -2.3f), null); back.range = 7; Decay(back, 5f, 1.4f, new Vector3(0, 1.5f, -2.3f));
            var fill = Make("Fill", LightType.Point, new Color32(0x9f, 0xb6, 0xff, 255), 1.6f, new Vector3(0, 1.6f, 2.2f), null); fill.range = 6; Decay(fill, 1.6f, 1.5f, new Vector3(0, 1.6f, 2.2f));
            // spot 9 / decay 1.6, back 5 / 1.4, fill 1.6 / 1.5 (table3d.js), matched at the felt's centre.
            void Decay(Light light, float threeIntensity, float decay, Vector3 threePos) =>
                light.intensity = ThreeLocalIntensity(threeIntensity, decay, Vector3.Distance(threePos, new Vector3(0, TY, 0)));

            // Post: none. table3d.js renders with three.js ACESFilmic at exposure 1.05 and no bloom or vignette; the
            // camera's ThreeToneMapping does exactly that, so this volume switches off what the pipeline's default
            // profile would add (Neutral tone mapping, bloom 0.25, vignette 0.2).
            var volumeGo = new GameObject("Post"); volumeGo.transform.SetParent(lights, false);
            var volume = volumeGo.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 100;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "Table post";
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.None);
            profile.Add<Bloom>(true).intensity.Override(0f);
            profile.Add<Vignette>(true).intensity.Override(0f);
            profile.Add<ColorAdjustments>(true).postExposure.Override(0f);
            profile.Add<MotionBlur>(true).intensity.Override(0f);
            volume.sharedProfile = profile;
            owned.Add(profile); owned.AddRange(profile.components);
        }

        private void BuildCamera()
        {
            var camGo = new GameObject("MainCamera"); camGo.tag = "MainCamera"; camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = skins.Theme.Bg3; cam.allowHDR = true; cam.allowMSAA = true;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.None;
            camGo.AddComponent<ThreeToneMapping>();
            CameraRig = camGo.AddComponent<TableCameraRig>(); CameraRig.Initialize(cam, TableCameraRig.View.Player);
        }

        /// <summary>
        /// Draw the 3D view into <paramref name="target"/> with a transparent clear, so the UI can composite the
        /// table over the galaxy background (the room's window shows it). null restores the opaque screen camera.
        /// Layer 31 belongs to the UI gamma presentation camera.
        /// </summary>
        public void RenderInto(RenderTexture target)
        {
            var cam = CameraRig.Camera;
            cam.targetTexture = target; cam.backgroundColor = target != null ? Color.clear : skins.Theme.Bg3;
            cam.cullingMask = target != null ? ~(1 << 31) : -1;
        }

        /// <summary>World point of a HUD anchor (table3d.js anchorWorld): a1–a3 opponent avatars, disc, ind, deck.</summary>
        public Vector3 AnchorWorld(string key)
        {
            bool top = CameraRig != null && CameraRig.Current == TableCameraRig.View.Top;
            switch (key)
            {
                case "a1": return (top ? seatFrames[1].Pose(0, 0.8f, 1.22f) : seatFrames[1].Pose(0, 0.86f, 0.98f)).UnityPosition;
                case "a2": return (top ? seatFrames[2].Pose(0.56f, 0.86f, 0.98f) : seatFrames[2].Pose(0, 1.16f, 1.14f)).UnityPosition;
                case "a3": return (top ? seatFrames[3].Pose(0, 0.8f, 1.22f) : seatFrames[3].Pose(0, 0.86f, 0.98f)).UnityPosition;
                case "disc": return seatFrames[0].Pose(0.6f, TY + 0.02f, 0.6f).UnityPosition;
                // table3d.js overrides anchorWorld0.ind with the indicator pile itself: (PX0 + deck x, TY + 0.002, 0.07).
                case "ind": return ThreeSpace.ToUnity(DeckShift + PileX0, TY + 0.002f, 0.07f);
                case "deck": return ThreeSpace.ToUnity(0, TY + 0.13f, 0);
                case "putback": return seatFrames[3].Pose(DiscardLocal.x, TY + 0.002f, DiscardLocal.z).UnityPosition;
                default: throw new System.ArgumentException("Unknown table anchor: " + key, nameof(key));
            }
        }

        // ---------- room, table, seats ----------
        private void BuildEnvironment()
        {
            tableRoot = new GameObject("Table").transform; tableRoot.SetParent(transform, false);
            var room = Instantiate(catalog.Room, transform, false); room.name = "Room"; skins.Adopt(room);
            SetTableSkin(tableSkin);
            for (int k = 0; k < 4; k++)
            {
                var seat = new GameObject("Seat" + k).transform; seat.SetParent(transform, false);
                seat.SetPositionAndRotation(ThreeSpace.ToUnity(seatFrames[k].Position), ThreeSpace.ToUnity(seatFrames[k].Rotation));
                seats[k] = seat;
                if (k > 0)
                {
                    var chair = Instantiate(catalog.Chair, seat, false); chair.name = "Chair";
                    chair.transform.localPosition = ThreeSpace.ToUnity(ChairLocal); skins.Adopt(chair);
                }
            }
            SetRackSkin(rackSkin);
            tilesRoot = new GameObject("Tiles").transform; tilesRoot.SetParent(transform, false);
            SetTileSkin(tileSkin);
            BuildDiscardZone();
        }

        private GameObject tileSource;

        /// <summary>
        /// Tile style (T21): a new factory from that skin's tile-set GLB and face atlas. Tiles already on the table keep
        /// their objects; <see cref="TileFactory.Restyle"/> swaps their body and face material.
        /// </summary>
        public void SetTileSkin(string skin)
        {
            Kill(tileSource);
            // The factory needs one tile (tas_R1's body) as its template: copy that, not the whole 53-tile set.
            var set = TableAssetCatalog.Find(catalog.TileSets, skin);
            var template = set.transform.Find("tas_R1") ?? throw new System.ArgumentException("Tile set lacks tas_R1: " + skin);
            tileSource = new GameObject("TileSource"); tileSource.transform.SetParent(transform, false);
            Instantiate(template.gameObject, tileSource.transform, false).name = "tas_R1";
            skins.Adopt(tileSource);
            var faces = Newtonsoft.Json.Linq.JObject.Parse(Resources.Load<TextAsset>("Galaxy101/Table/TileFaces/tile_faces").text)["skins"][skin];
            // Tiles made by the previous factory (a ceremony in flight) may still wear its materials: freed with the scene.
            if (tiles != null) retiredTiles.Add(tiles);
            tiles = new TileFactory(tileSource, skin, (float)faces["roughness"], (float)faces["emission"]);
            tileSkin = skin;
        }

        public void SetTableSkin(string skin)
        {
            var prefab = TableAssetCatalog.Find(catalog.Tables, skin);
            // Destroy is deferred in play mode; effect rescans must not pick the retired skin's holo/LED renderers.
            if (tableInstance != null) tableInstance.SetActive(false);
            Kill(tableInstance);
            skins.SetTableSkin(skin);
            tableInstance = Instantiate(prefab, tableRoot, false); tableInstance.name = "Table_" + skin;
            skins.Adopt(tableInstance); tableSkin = skin;
            ApplyFelt();
            GetComponent<TableFx>()?.RescanTable();
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> PlanetIds = new System.Collections.Generic.Dictionary<string, string>
            { ["Satürn"] = "saturn", ["Mars"] = "mars", ["Neptün"] = "neptune", ["Jüpiter"] = "jupiter", ["Venüs"] = "venus" };

        /// <summary>
        /// table3d.js applyLook(): the felt is drawn for the planet (f1/f2 gradient, accent ring and dots) and the skin
        /// (wood vignette and frame, holo grid, the Andromeda nebula); tools/felt_textures.py renders those canvases.
        /// Holo and Andromeda felts also glow with their map.
        /// </summary>
        private void ApplyFelt()
        {
            if (tableInstance == null) return;
            string name = tableSkin == "andromeda" ? "andromeda" : tableSkin + "_" + (PlanetIds.TryGetValue(planet, out var id) ? id : "saturn");
            var felt = Resources.Load<Texture2D>("Galaxy101/Table/Felt/" + name);
            if (felt == null) { Debug.LogWarning("Missing felt texture " + name); return; }
            felt.anisoLevel = 8;
            foreach (var r in tableInstance.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || m.name != "cuha") continue;
                    m.SetTexture("_BaseMap", felt);
                    if (m.IsKeywordEnabled("_EMISSION") && m.GetTexture("_EmissionMap") != null) m.SetTexture("_EmissionMap", felt);
                }
        }

        public void SetRackSkin(string skin)
        {
            for (int k = 0; k < 4; k++)
            {
                if (rackInstances[k] != null) rackInstances[k].SetActive(false);
                Kill(rackInstances[k]);
                var rack = Instantiate(TableAssetCatalog.Find(catalog.Racks, skin), seats[k], false); rack.name = "Rack";
                var prefabRotation = rack.transform.localRotation; // the GLB root already carries TILT
                rack.transform.localPosition = ThreeSpace.ToUnity(RackLocal); rack.transform.localRotation = prefabRotation;
                skins.Adopt(rack); rackInstances[k] = rack;
                var found = new Transform[Rows * SlotsPerRow];
                foreach (var t in rack.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Slot_")) { var parts = t.name.Split('_'); found[int.Parse(parts[1]) * SlotsPerRow + int.Parse(parts[2])] = t; }
                slots[k] = found;
            }
            rackSkin = skin;
            GetComponent<TableFx>()?.RescanTable();
        }

        private void BuildDiscardZone()
        {
            var pose = seatFrames[0].Pose(DiscardLocal.x, TY + 0.0012f, DiscardLocal.z);
            var zone = new GameObject("DiscardZone"); zone.transform.SetParent(transform, false); DiscardZone = zone.transform;
            zone.transform.position = pose.UnityPosition;
            var ring = RingMesh(0.058f, 0.066f, 48); zone.AddComponent<MeshFilter>().sharedMesh = ring;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "atma_alani" };
            owned.Add(ring); owned.Add(mat);
            mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0); mat.SetFloat("_ZWrite", 0);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = (int)RenderQueue.Transparent;
            var c = skins.Theme.A1; c.a = 0.4f; mat.SetColor("_BaseColor", c);
            zone.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static Mesh RingMesh(float inner, float outer, int segments)
        {
            var v = new Vector3[segments * 2]; var tri = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v[i * 2] = d * inner; v[i * 2 + 1] = d * outer;
                int n = (i + 1) % segments;
                tri[i * 6] = i * 2; tri[i * 6 + 1] = n * 2; tri[i * 6 + 2] = i * 2 + 1;
                tri[i * 6 + 3] = i * 2 + 1; tri[i * 6 + 4] = n * 2; tri[i * 6 + 5] = n * 2 + 1;
            }
            var mesh = new Mesh { name = "Ring", vertices = v, triangles = tri }; mesh.RecalculateNormals(); return mesh;
        }

        // ---------- tile placement (poseFor in table3d.js) ----------
        private static readonly Quaternion FlatUp = ThreeSpace.Euler(-Mathf.PI / 2, 0, 0);
        private static readonly Quaternion FaceDown = ThreeSpace.Euler(Mathf.PI / 2, 0, 0);

        public GameObject PlaceInRack(string code, int seat, int row, int index)
        {
            var tile = tiles.Create(code, Slot(seat, row, index));
            tile.transform.SetParent(tilesRoot, true); return tile;
        }

        public GameObject PlaceDiscard(string code, int seat, int level, string id) => Place(code, DiscardPose(seat, level, id));
        public GameObject PlaceMeld(string code, int seat, int column, int row) => Place(code, MeldPose(seat, column, row));
        public GameObject PlaceIndicator(string code) => Place(code, IndicatorPose());

        /// <summary>layoutDeck(n): 5-high stacks, 4/2/1 rows, ±1.7° deterministic twist, face down.</summary>
        public void LayoutDeck(int n) { for (int j = 0; j < n; j++) Place(null, DeckPose(j, n)).name = "deck_" + j; }

        // Unity-space poses (poseFor in table3d.js), shared by the static layout and the live board.
        public Pose SlotPose(int seat, int row, int index) { var slot = Slot(seat, row, index); return new Pose(slot.position, slot.rotation); }

        public Pose DiscardPose(int seat, int level, string key)
        {
            var q = ThreeSpace.AxisAngle(Vector3.up, (Hash(key) - 0.5f) * 0.5f) * FlatUp;
            return Unity(seatFrames[seat].Pose(DiscardLocal.x, TY + TD / 2 + Mathf.Min(level, 4) * TD, DiscardLocal.z, q));
        }

        /// <summary>A pose in a seat's frame (three coordinates): face up readable from that seat, or face down.</summary>
        public Pose SeatPose(int seat, float x, float y, float z, bool faceUp) => Unity(seatFrames[seat].Pose(x, y, z, faceUp ? FlatUp : FaceDown));
        /// <summary>Unit vector on the table plane from the centre towards a seat (Unity space).</summary>
        public Vector3 SeatDirection(int seat) { var p = seatFrames[seat].Pose(0, 0, 1).UnityPosition - seatFrames[seat].Pose(0, 0, 0).UnityPosition; p.y = 0; return p.normalized; }
        public Pose MeldPose(int seat, int column, int row) => Unity(seatFrames[seat].Pose(-0.245f + column * 0.049f, TY + TD / 2, 0.37f - row * 0.08f, FlatUp));
        /// <summary>
        /// table3d.js poseFor('i'): the indicator lies face up on the six-tile pile; with six tiles or fewer left it lies
        /// beside the piles (x +0.085, z 0.02 in the middle; z 0.115 once the deck has slid left), turned −0.22 rad.
        /// </summary>
        public Pose IndicatorPose()
        {
            float left = Mathf.Clamp01(DeckShift / DeckLeft);
            if (DeckCount <= 6)
                return Unity(new ThreePose(new Vector3(DeckShift + PileX0 + Mathf.Lerp(0.085f, 0, left), TY + TD / 2, Mathf.Lerp(0.02f, 0.115f, left)), ThreeSpace.AxisAngle(Vector3.up, -0.22f) * FlatUp));
            return Unity(new ThreePose(new Vector3(DeckShift + PileX0, TY + TD / 2 + Mathf.Min(6, DeckCount) * TD, 0), ThreeSpace.AxisAngle(Vector3.up, 0.04f) * FlatUp));
        }

        /// <summary>Tiles in the deck (the board keeps it current); the indicator's pile depends on it.</summary>
        public int DeckCount = 21;
        /// <summary>table3d.js deckHeap: during the deal the deck stays in 5-high stacks even below 22 tiles.</summary>
        public bool DeckHeap;
        /// <summary>table3d.js PX0: the two piles sit ±3.6 cm either side of the deck's x.</summary>
        public const float PileX0 = 0.036f;
        private static bool Piles(int n, bool heap) => n <= 21 && !heap;

        /// <summary>spec/23: once melds are on the felt the deck and the indicator slide left (three x, eased by the board).</summary>
        public float DeckShift;
        public const float DeckLeft = -0.62f;

        /// <summary>Centre board cell (spec/23): 8 rows × 20 columns in my frame, face up, readable by me.</summary>
        public Pose BoardPose(int column, int row) => Unity(seatFrames[0].Pose(-0.465f + column * 0.049f, TY + TD / 2, -0.30f + row * 0.0857f, FlatUp));
        /// <summary>Row-start marker spot (x −0.505) for a board row.</summary>
        public Vector3 BoardRowStart(int row) => seatFrames[0].Pose(-0.505f, TY + 0.0012f, -0.30f + row * 0.0857f).UnityPosition;

        /// <summary>
        /// table3d.js layoutDeck: up to 21 tiles (outside the deal) stand in two piles — the first six under the indicator at
        /// x +PX0, the rest at −PX0 — otherwise in 5-high stacks on a grid.
        /// </summary>
        public Pose DeckPose(int j, int n)
        {
            if (Piles(n, DeckHeap))
            {
                bool under = j < 6;
                var q = new Vector3(DeckShift + (under ? PileX0 : -PileX0), TY + TD / 2 + (under ? j : j - 6) * TD, 0);
                return Unity(new ThreePose(q, ThreeSpace.AxisAngle(Vector3.up, ((j * 0.37f) % 1 - 0.5f) * 0.05f) * FaceDown));
            }
            int stacks = Mathf.CeilToInt(n / 5f), rows = n > 60 ? 4 : n > 25 ? 2 : 1, cols = Mathf.Max(1, Mathf.CeilToInt(stacks / (float)rows));
            int s = j / 5, lv = j % 5, col = s / rows, row = s % rows;
            var p = new Vector3(DeckShift + (col - (cols - 1) / 2f) * (TW + 0.006f), TY + TD / 2 + lv * TD, (row - (rows - 1) / 2f) * (TH + 0.006f));
            return Unity(new ThreePose(p, ThreeSpace.AxisAngle(Vector3.up, ((j * 0.37f) % 1 - 0.5f) * 0.06f) * FaceDown));
        }

        /// <summary>Where a tile leaves (or rejoins) the deck: one tile height above the stack of <paramref name="n"/>.</summary>
        public Pose DeckTop(int n)
        {
            if (n <= 0) return Unity(new ThreePose(new Vector3(DeckShift + (DeckHeap ? 0 : PileX0), TY + TD, 0), FaceDown));
            var top = DeckPose(n - 1, n); top.position += Vector3.up * TD; return top;
        }

        /// <summary>The deck's pick box (spec/11: deckHit = stacks + 4 cm, 12 cm high).</summary>
        public Bounds DeckBounds(int n)
        {
            if (Piles(n, DeckHeap)) return new Bounds(new Vector3(-DeckShift, TY + 0.1f, 0), new Vector3(0.2f, 0.26f, 0.15f)); // deckHit (piles)
            int stacks = Mathf.CeilToInt(Mathf.Max(1, n) / 5f), rows = n > 60 ? 4 : n > 25 ? 2 : 1, cols = Mathf.Max(1, Mathf.CeilToInt(stacks / (float)rows));
            return new Bounds(new Vector3(-DeckShift, TY + 0.06f, 0), new Vector3(cols * (TW + 0.006f) + 0.04f, 0.12f, rows * (TH + 0.006f) + 0.04f));
        }

        public Vector3 DiscardCenter(int seat) => seatFrames[seat].Pose(DiscardLocal.x, TY + 0.0012f, DiscardLocal.z).UnityPosition;

        /// <summary>My rack plane (rack-local z = 0). Returns the hit in three rack-local coordinates (x right, y up).</summary>
        public bool RackHit(Ray ray, out Vector3 local)
        {
            var root = RackRoot(0); var plane = new Plane(root.TransformDirection(Vector3.forward), root.position);
            local = default;
            if (!plane.Raycast(ray, out float distance)) return false;
            local = ThreeSpace.ToUnity(root.InverseTransformPoint(ray.GetPoint(distance))); return true;
        }

        /// <summary>A pose in my rack's frame, from three rack-local coordinates.</summary>
        public Pose RackPose(Vector3 local) { var root = RackRoot(0); return new Pose(root.TransformPoint(ThreeSpace.ToUnity(local)), root.rotation); }

        public bool TableHit(Ray ray, out Vector3 point)
        {
            var plane = new Plane(Vector3.up, new Vector3(0, TY, 0)); point = default;
            if (!plane.Raycast(ray, out float distance)) return false;
            point = ray.GetPoint(distance); return true;
        }

        /// <summary>Face-up orientation in front of me (dragging over the felt).</summary>
        public Quaternion FlatForMe => seatFrames[0].Pose(0, 0, 0, FlatUp).UnityRotation;

        private static Pose Unity(ThreePose pose) => new Pose(pose.UnityPosition, pose.UnityRotation);

        private GameObject Place(string code, Pose pose)
        {
            var tile = tiles.Create(code, tilesRoot); tile.transform.SetPositionAndRotation(pose.position, pose.rotation); return tile;
        }

        // ---------- demo layout matching screenshots/table/T05 ----------
        private static readonly int[][] OpponentFill = BuildFillOrder();
        private static int[][] BuildFillOrder()
        {
            var order = new List<int[]>();
            for (int d = 0; d < 8; d++)
            {
                foreach (int i in d == 0 ? new[] { 7 } : new[] { 7 + d, 7 - d })
                    if (i >= 0 && i < SlotsPerRow) { order.Add(new[] { 1, i }); order.Add(new[] { 0, i }); }
            }
            return order.ToArray();
        }

        private void BuildDemoLayout()
        {
            var deal = Newtonsoft.Json.Linq.JObject.Parse(Resources.Load<TextAsset>("Galaxy101/Table/table_content").text)["demoHands"];
            var rows = deal["deal"];
            for (int r = 0; r < 2; r++)
                for (int i = 0; i < SlotsPerRow; i++)
                {
                    var code = (string)rows[r][i];
                    if (code != null) PlaceInRack(code, 0, r == 0 ? 0 : 1, i);
                }
            for (int k = 1; k < 4; k++)
                for (int n = 0; n < 21; n++) PlaceInRack(null, k, OpponentFill[n][0], OpponentFill[n][1]);
            var melds = deal["ayseMelds"];
            int column = 0, meldRow = 0;
            foreach (var meld in melds)
            {
                if (column + meld.Count() > 11) { column = 0; meldRow++; }
                foreach (var code in meld) PlaceMeld((string)code, 2, column++, meldRow);
                column++;
            }
            LayoutDeck(24);
            PlaceIndicator("B6");
            PlaceDiscard("K12", 1, 0, "d1"); PlaceDiscard("R7", 3, 0, "d3"); PlaceDiscard("Y3", 2, 0, "d2");
        }
    }
}
