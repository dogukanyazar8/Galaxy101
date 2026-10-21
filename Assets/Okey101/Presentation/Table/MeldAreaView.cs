using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Model;
using UnityEngine;

namespace Okey101.Presentation
{
    public sealed class MeldAreaView : MonoBehaviour
    {
        private readonly List<TileView> tiles = new List<TileView>();
        private Func<Transform, TileView> factory;
        private Camera tableCamera;
        private IReadOnlyList<Meld> groups = Array.Empty<Meld>();
        private TileFace indicator;
        private bool focused;
        public IReadOnlyList<Meld> Groups => groups;
        public int? SelectedMeldId { get; private set; }
        public TileId? SelectedTile { get; private set; }
        public string TargetDescription
        {
            get
            {
                var group = groups.FirstOrDefault(g => g.Id == SelectedMeldId);
                return group == null ? "Masa henüz boş" : $"Grup {group.Id + 1} · Oyuncu {group.Owner + 1}\n{(group.Group.Kind == MeldKind.Pair ? "Çift" : "Seri / sayı")} · {groups.ToList().IndexOf(group) + 1}/{groups.Count}";
            }
        }
        public void Initialize(Func<Transform, TileView> create, Camera camera) { factory = create; tableCamera = camera; }
        public void Render(IReadOnlyList<Meld> table, TileFace indicatorFace)
        {
            groups = table; indicator = indicatorFace;
            if (!groups.Any(g => g.Id == SelectedMeldId)) { SelectedMeldId = groups.Count > 0 ? groups[0].Id : (int?)null; SelectedTile = null; }
            if (SelectedTile.HasValue && !groups.Any(g => g.Id == SelectedMeldId && g.Group.Tiles.Any(t => t.Id == SelectedTile.Value))) SelectedTile = null;
            Rebuild();
        }
        public void Step(int direction)
        {
            if (groups.Count == 0) return;
            var index = groups.ToList().FindIndex(g => g.Id == SelectedMeldId);
            SelectedMeldId = groups[(index + direction % groups.Count + groups.Count) % groups.Count].Id; SelectedTile = null; Rebuild();
        }
        public void FocusGroup(int meldId)
        {
            if (!groups.Any(g => g.Id == meldId)) return;
            focused = true; SelectedMeldId = meldId; SelectedTile = null; Rebuild();
        }
        public void ResetFocus() { focused = false; Rebuild(); }
        public void Select(int meldId, TileId id)
        {
            if (!groups.Any(g => g.Id == meldId && g.Group.Tiles.Any(t => t.Id == id))) return;
            SelectedMeldId = meldId; SelectedTile = id;
            foreach (var tile in tiles) tile.SetSelected(tile.MeldId == meldId && tile.Id == id);
        }
        private void Rebuild()
        {
            foreach (var tile in tiles) { tile.gameObject.SetActive(false); Destroy(tile.gameObject); } tiles.Clear();
            var selected = groups.FirstOrDefault(g => g.Id == SelectedMeldId);
            if (focused && selected != null) { Show(selected, TablePoint(.5f, .70f), 1f); return; }
            var series = selected != null && selected.Group.Kind != MeldKind.Pair ? selected : groups.FirstOrDefault(g => g.Group.Kind != MeldKind.Pair);
            var pair = selected != null && selected.Group.Kind == MeldKind.Pair ? selected : groups.FirstOrDefault(g => g.Group.Kind == MeldKind.Pair);
            if (series != null) Show(series, TablePoint(.5f, .72f));
            if (pair != null) Show(pair, TablePoint(.5f, .63f));
        }
        private Vector3 TablePoint(float x, float y)
        {
            var ray = tableCamera.ViewportPointToRay(new Vector3(x, y, 0));
            new Plane(Vector3.up, new Vector3(0, .15f, 0)).Raycast(ray, out var distance);
            return transform.InverseTransformPoint(ray.GetPoint(distance));
        }
        private void Show(Meld group, Vector3 center, float scale = .84f)
        {
            var ordered = group.Group.Tiles.OrderBy(t => t.RepresentedFace.Number).ThenBy(t => t.RepresentedFace.Color).ToArray();
            for (var i = 0; i < ordered.Length; i++)
            {
                var tile = factory(transform); tile.transform.localScale = Vector3.one * scale;
                tile.transform.localPosition = center + Vector3.right * ((i - (ordered.Length - 1) * .5f) * .79f * scale);
                tile.ShowPlaced(ordered[i], group.Id, indicator, tableCamera); tile.SetInteractive(true);
                tile.SetSelected(SelectedMeldId == group.Id && SelectedTile == ordered[i].Id); tiles.Add(tile);
            }
        }
    }
}
