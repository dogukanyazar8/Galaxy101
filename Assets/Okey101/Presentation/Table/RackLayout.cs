using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Model;
using UnityEngine;

namespace Okey101.Presentation
{
    public sealed class RackLayout : MonoBehaviour
    {
        private readonly TileId?[] slots = new TileId?[24];
        private readonly Dictionary<Collider, int> targets = new Dictionary<Collider, int>();
        public int IndexOf(TileId tile) => Array.IndexOf(slots, (TileId?)tile);
        public static Vector3 Position(int slot) => new Vector3((slot % 12 - 5.5f) * .85f, .28f, slot < 12 ? -.53f : .53f);
        public bool TryGetSlot(Collider collider, out int slot) => targets.TryGetValue(collider, out slot);

        public void Initialize()
        {
            if (targets.Count != 0) return;
            var material = GetComponentInChildren<MeshRenderer>().sharedMaterial;
            for (var i = 0; i < slots.Length; i++)
            {
                var pad = Cube(transform, "Slot " + (i + 1), Position(i) - Vector3.up * .10f,
                    new Vector3(.80f, .035f, .99f), material);
                targets.Add(pad.GetComponent<Collider>(), i);
                pad.GetComponent<MeshRenderer>().enabled = false;
            }
        }

        public void Clear() { Array.Clear(slots, 0, slots.Length); }
        public void UpdateHand(IReadOnlyList<TileId> hand)
        {
            var remaining = new HashSet<TileId>(hand);
            if (remaining.Count != hand.Count || hand.Count > 22) throw new ArgumentException("Invalid rack hand.", nameof(hand));
            for (var i = 0; i < slots.Length; i++)
                if (slots[i].HasValue && !remaining.Remove(slots[i].Value)) slots[i] = null;
            foreach (var id in hand)
                if (remaining.Remove(id)) slots[Array.IndexOf(slots, null)] = id;
        }
        public void Move(TileId id, int target)
        {
            if (target < 0 || target >= slots.Length) throw new ArgumentOutOfRangeException(nameof(target));
            var source = IndexOf(id);
            if (source < 0) throw new ArgumentException("Tile is not on this rack.", nameof(id));
            var other = slots[target]; slots[target] = id; slots[source] = other;
        }
        public void Sort(TileFace indicator)
        {
            var ordered = slots.Where(t => t.HasValue).Select(t => t.Value)
                .OrderBy(id => TileCatalog.IsRealJoker(id, indicator) ? 4 : (int)TileCatalog.GetNonJokerFace(id, indicator).Color)
                .ThenBy(id => TileCatalog.IsRealJoker(id, indicator) ? 0 : TileCatalog.GetNonJokerFace(id, indicator).Number)
                .ThenBy(id => id.Value).ToArray();
            Clear(); for (var i = 0; i < ordered.Length; i++) slots[i] = ordered[i];
        }

        public static GameObject CreateTable(Transform parent, Material wood, Material felt, Material brass)
        {
            var root = new GameObject("Table"); root.transform.SetParent(parent, false);
            Cube(root.transform, "Wood rim", new Vector3(0, -.40f, 0), new Vector3(18, .45f, 10.4f), wood);
            Cube(root.transform, "Felt", new Vector3(0, -.12f, 0), new Vector3(17.45f, .10f, 9.85f), felt);
            Cube(root.transform, "Near inlay", new Vector3(0, -.05f, -4.97f), new Vector3(17.5f, .018f, .025f), brass);
            Cube(root.transform, "Far inlay", new Vector3(0, -.05f, 4.97f), new Vector3(17.5f, .018f, .025f), brass);
            return root;
        }
        public static GameObject CreateRack(Transform parent, Material wood)
        {
            var root = new GameObject("Rack"); root.transform.SetParent(parent, false);
            Cube(root.transform, "Rack plinth", Vector3.zero, new Vector3(10.5f, .30f, 2.18f), wood);
            Cube(root.transform, "Front lip", new Vector3(0, .22f, -1.05f), new Vector3(10.6f, .20f, .10f), wood);
            Cube(root.transform, "Back lip", new Vector3(0, .22f, 1.05f), new Vector3(10.6f, .20f, .10f), wood);
            root.AddComponent<RackLayout>(); return root;
        }
        public static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material; return go;
        }
    }
}
