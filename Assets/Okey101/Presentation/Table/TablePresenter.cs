using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Okey101.Core.Model;
using Okey101.Application;
using UnityEngine;

namespace Okey101.Presentation
{
    public sealed class TablePresenter : MonoBehaviour
    {
        [SerializeField] private Camera tableCamera;
        [SerializeField] private TileView tilePrefab;
        [SerializeField] private GameObject tablePrefab;
        [SerializeField] private GameObject rackPrefab;
        private readonly Dictionary<TileId, TileView> ownTiles = new Dictionary<TileId, TileView>();
        private readonly List<Material> temporaryMaterials = new List<Material>();
        private TileView indicatorTile;
        private TileView stockTile;
        private readonly TileView[] discards = new TileView[4];
        private RackLayout rack;
        private PlayerView current;
        private readonly HashSet<TileId> selected = new HashSet<TileId>();
        public MeldAreaView Melds { get; private set; }
        public TileMotion Motion { get; private set; }
        public TableViewport Viewport { get; private set; }
        public TableAudio Audio { get; private set; }
        public Camera TableCamera => tableCamera;
        public RackLayout Rack => rack;
        public IReadOnlyList<TileId> VisibleOwnTileIds => Array.AsReadOnly(ownTiles.Keys.OrderBy(id => id.Value).ToArray());

        public void Configure(Camera camera, TileView tile, GameObject table, GameObject rackModel)
        { tableCamera = camera; tilePrefab = tile; tablePrefab = table; rackPrefab = rackModel; }

        public void Initialize()
        {
            if (rack != null) return;
            if (tableCamera == null)
            {
                var cameraObject = new GameObject("Table Camera"); cameraObject.transform.SetParent(transform, false);
                tableCamera = cameraObject.AddComponent<Camera>();
            }
            tableCamera.transform.position = new Vector3(0, 14, -6);
            tableCamera.transform.LookAt(Vector3.zero); tableCamera.orthographic = true; tableCamera.depth = 10;
            tableCamera.clearFlags = CameraClearFlags.SolidColor; tableCamera.backgroundColor = new Color(.025f, .06f, .055f);
            var lightObject = new GameObject("Soft key light"); lightObject.transform.SetParent(transform, false);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 1.2f; light.color = new Color(1, .93f, .81f); light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48, -25, 0);
            RenderSettings.ambientLight = new Color(.48f, .51f, .50f);
            if (tablePrefab != null) Instantiate(tablePrefab, transform);
            else RackLayout.CreateTable(transform, Surface(new Color(.20f, .13f, .09f)),
                Surface(new Color(.035f, .22f, .17f)), Surface(new Color(.57f, .39f, .15f)));
            GameObject rackObject = rackPrefab != null ? Instantiate(rackPrefab, transform)
                : RackLayout.CreateRack(transform, Surface(new Color(.20f, .13f, .09f)));
            rackObject.transform.localPosition = new Vector3(0, .25f, -3.65f);
            rack = rackObject.GetComponent<RackLayout>(); rack.Initialize();
            if (tilePrefab == null)
            {
                tilePrefab = TileView.CreatePrimitive(transform, Surface(Color.white), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
                tilePrefab.gameObject.SetActive(false);
            }
            indicatorTile = MakeTile(transform); indicatorTile.transform.localPosition = new Vector3(-1.6f, .12f, .7f);
            indicatorTile.SetInteractive(false);
            stockTile = MakeTile(transform); stockTile.transform.localPosition = new Vector3(0, .23f, .7f); stockTile.ShowBack(tableCamera);
            for (var p = 0; p < 4; p++)
            {
                discards[p] = MakeTile(transform); discards[p].SetInteractive(false);
                discards[p].transform.localPosition = p == 0 ? new Vector3(0, .12f, -1.35f)
                    : p == 1 ? new Vector3(6.3f, .12f, 0) : p == 2 ? new Vector3(0, .12f, 3.15f) : new Vector3(-6.3f, .12f, 0);
            }
            FitCamera();
            var meldObject = new GameObject("Public meld areas"); meldObject.transform.SetParent(transform, false);
            Melds = meldObject.AddComponent<MeldAreaView>(); Melds.Initialize(MakeTile, tableCamera);
            Motion = gameObject.AddComponent<TileMotion>();
            Audio = GetComponent<TableAudio>() ?? gameObject.AddComponent<TableAudio>();
            Viewport = gameObject.AddComponent<TableViewport>();
            var controller = GetComponent<GameBootstrap>();
            Viewport.Initialize(Melds, () => controller != null ? controller.CanControl : !Motion.IsPlaying);
            PositionPublicTiles();
        }

        public async Task PresentAcceptedAsync(PlayerView before, PlayerView after, IReadOnlyList<VisibleEvent> events, CancellationToken token)
        {
            Render(before);
            var starts = GetComponentsInChildren<TileView>().Where(t => t.Role == TileRole.Hand || t.Role == TileRole.Meld || t.Role == TileRole.Discard)
                .GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First().transform.position);
            Render(after); SelectMany(Array.Empty<TileId>());
            var actor = events.Count > 0 ? events[0].Actor : after.Actor;
            var fallback = actor == after.Actor ? stockTile.transform.position : discards[actor].transform.position;
            var placements = new List<TilePlacement>();
            foreach (var tile in GetComponentsInChildren<TileView>())
            {
                if (tile.Role == TileRole.Stock || tile.Role == TileRole.Indicator) continue;
                var end = tile.transform.position;
                var start = starts.TryGetValue(tile.Id, out var position) ? position : fallback;
                if ((start - end).sqrMagnitude > .0001f) placements.Add(new TilePlacement(tile.transform, start, end));
            }
            Motion.Prepare(placements);
            foreach (var visibleEvent in events) Audio.Play(visibleEvent);
            await Motion.PlayAsync(events, Motion.ReducedMotion, token);
        }
        private Vector3 TablePoint(float x, float y, float height = .12f)
        {
            var ray = tableCamera.ViewportPointToRay(new Vector3(x, y, 0));
            new Plane(Vector3.up, new Vector3(0, height, 0)).Raycast(ray, out var distance);
            return transform.InverseTransformPoint(ray.GetPoint(distance));
        }
        private void PositionPublicTiles()
        {
            indicatorTile.transform.localPosition = TablePoint(.43f, .55f);
            stockTile.transform.localPosition = TablePoint(.51f, .55f, .23f);
            discards[0].transform.localPosition = TablePoint(.67f, .54f);
            discards[1].transform.localPosition = TablePoint(.75f, .60f);
            discards[2].transform.localPosition = TablePoint(.68f, .83f);
            discards[3].transform.localPosition = TablePoint(.25f, .60f);
        }

        private Material Surface(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is required.");
            var material = new Material(shader); material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .20f);
            temporaryMaterials.Add(material); return material;
        }
        private TileView MakeTile(Transform parent)
        {
            var tile = Instantiate(tilePrefab, parent); tile.gameObject.SetActive(true); return tile;
        }
        public void Render(PlayerView view)
        {
            Initialize();
            if (current == null || current.Actor != view.Actor || current.RoundId != view.RoundId) { rack.Clear(); selected.Clear(); }
            current = view; rack.UpdateHand(view.OwnHand);
            var held = new HashSet<TileId>(view.OwnHand);
            foreach (var id in ownTiles.Keys.Where(id => !held.Contains(id)).ToArray())
            { ownTiles[id].gameObject.SetActive(false); Destroy(ownTiles[id].gameObject); ownTiles.Remove(id); }
            var indicator = TileCatalog.Create()[view.Indicator.Value].PrintedFace.Value;
            foreach (var id in view.OwnHand)
            {
                if (!ownTiles.TryGetValue(id, out var tile)) { tile = MakeTile(rack.transform); ownTiles.Add(id, tile); }
                tile.Show(id, indicator, TileRole.Hand, tableCamera);
                tile.SetReserved(false);
            }
            indicatorTile.Show(view.Indicator, indicator, TileRole.Indicator, tableCamera);
            stockTile.gameObject.SetActive(view.StockCount > 0);
            for (var p = 0; p < 4; p++)
            {
                discards[p].gameObject.SetActive(view.Discards[p].Count > 0);
                if (view.Discards[p].Count > 0) discards[p].Show(view.Discards[p][view.Discards[p].Count - 1], indicator, TileRole.Discard, tableCamera);
                discards[p].SetInteractive(p == (view.Actor + 3) % 4);
            }
            Melds.Render(view.Table, indicator);
            RefreshLayout();
        }
        public void RenderPreview(PlayerView source, TurnPreview preview)
        {
            if (!preview.ActionsValid)
            {
                Render(source);
                foreach (var pair in ownTiles) pair.Value.SetReserved(preview.ReservedTiles.Contains(pair.Key));
                return;
            }
            var publicPlayers = source.Players.ToArray();
            publicPlayers[source.Actor] = new PublicPlayerInfo(preview.ProjectedOwnHand.Count, preview.Opening, source.OwnPenalties);
            Render(new PlayerView(source.RoundId, source.Version, source.Actor, source.ActivePlayer, source.Phase,
                preview.ProjectedOwnHand, preview.Opening, source.OwnPenalties, source.OwnLastSequence, source.Indicator,
                source.StockCount, publicPlayers, preview.ProjectedTable, source.Discards, source.PendingDraw, preview.SeriesTilesUsed, source.Result));
        }
        public void Select(TileId? tile) { SelectMany(tile.HasValue ? new[] { tile.Value } : Array.Empty<TileId>()); }
        public void SelectMany(IEnumerable<TileId> tiles) { selected.Clear(); selected.UnionWith(tiles); RefreshLayout(); }
        public void Move(TileId tile, int slot) { rack.Move(tile, slot); RefreshLayout(); }
        public void Sort()
        {
            rack.Sort(TileCatalog.Create()[current.Indicator.Value].PrintedFace.Value); RefreshLayout();
        }
        private void RefreshLayout()
        {
            foreach (var pair in ownTiles)
            {
                var isSelected = selected.Contains(pair.Key);
                pair.Value.transform.localPosition = RackLayout.Position(rack.IndexOf(pair.Key)) + (isSelected ? Vector3.up * .13f : Vector3.zero);
                pair.Value.SetSelected(isSelected);
            }
        }
        private void FitCamera() { tableCamera.orthographicSize = Mathf.Max(5.4f, 9.6f / Mathf.Max(.5f, tableCamera.aspect)); }
        private void LateUpdate()
        {
            if (tableCamera == null) return;
            FitCamera();
            if (stockTile != null && !Motion.IsPlaying) PositionPublicTiles();
        }
        private void OnDestroy() { foreach (var material in temporaryMaterials) if (material != null) Destroy(material); }
    }
}
