using System;
using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>Prefabs built from the polished GLBs by Galaxy 101 › Build Table Assets.</summary>
    [CreateAssetMenu(menuName = "Galaxy 101/Table Asset Catalog")]
    public sealed class TableAssetCatalog : ScriptableObject
    {
        [Serializable] public struct Entry { public string Key; public GameObject Prefab; }

        public static readonly string[] TableSkins = { "station", "wood", "holo", "andromeda" };
        public static readonly string[] RackSkins = { "walnut", "sedef", "chrome", "carbon", "neon", "andromeda" };
        public static readonly string[] TileSkins = { "classic", "galaxy", "gold", "neon", "marble" };
        public static readonly string[] Gifts = { "tea", "coffee", "cola", "simit", "baklava", "seeds", "flower", "cake", "trophy", "rocket" };

        public Entry[] Tables = Array.Empty<Entry>();
        public Entry[] Racks = Array.Empty<Entry>();
        public Entry[] TileSets = Array.Empty<Entry>();
        public Entry[] GiftModels = Array.Empty<Entry>();
        public GameObject Chair, Room;
        /// <summary>The 30 mm die with dimpled pips (Blender, spec/24); the deal ceremony rolls it.</summary>
        public GameObject Die;

        public static GameObject Find(Entry[] entries, string key)
        {
            foreach (var e in entries) if (e.Key == key) return e.Prefab;
            throw new ArgumentException("Table asset not found: " + key, nameof(key));
        }

        public static TableAssetCatalog Load()
        {
            var catalog = Resources.Load<TableAssetCatalog>("Galaxy101/Table/TableAssetCatalog");
            if (catalog == null) throw new InvalidOperationException("Table asset catalog missing; run Galaxy 101 › Build Table Assets.");
            return catalog;
        }
    }
}
