using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Okey101.Core.Model;

namespace Okey101.Core.Rules
{
    public static class MeldCandidateGenerator
    {
        /// <summary>Enumerates all physical choices and joker bindings for a distinct hand of at most 22 tiles.</summary>
        public static IReadOnlyList<MeldProposal> Generate(IReadOnlyList<TileId> hand, TileFace indicator, MeldKind kind)
            => Generate(hand, indicator, kind, CancellationToken.None);

        public static IReadOnlyList<MeldProposal> Generate(IReadOnlyList<TileId> hand, TileFace indicator,
            MeldKind kind, CancellationToken token)
            => Generate(hand, indicator, kind, token, false);

        /// <summary>
        /// The search pool for planners: each group once up to physically equivalent tiles — the faces it shows and where
        /// okeys stand in (an okey for a face the hand holds stays a choice). The two copies of a face, like the two okeys,
        /// are interchangeable: a planner binds a pattern to concrete tiles with <see cref="MeldBinder"/> when it picks it,
        /// so any copy mix a set of groups needs is found. A dense 22-tile hand drops from ~10⁵ runs to about a thousand.
        /// </summary>
        public static IReadOnlyList<MeldProposal> GenerateDistinct(IReadOnlyList<TileId> hand, TileFace indicator,
            MeldKind kind, CancellationToken token)
            => Generate(hand, indicator, kind, token, true);

        private static IReadOnlyList<MeldProposal> Generate(IReadOnlyList<TileId> hand, TileFace indicator,
            MeldKind kind, CancellationToken token, bool distinct)
        {
            token.ThrowIfCancellationRequested();
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            if (hand.Count > 22) throw new ArgumentException("A player's hand cannot contain more than 22 tiles.", nameof(hand));
            if (!indicator.IsValid) throw new ArgumentException("Invalid indicator face.", nameof(indicator));
            Immutable.EnumValue(kind, nameof(kind));

            var ids = Immutable.Copy(hand, nameof(hand)).OrderBy(id => id.Value).ToArray();
            if (ids.Distinct().Count() != ids.Length)
                throw new ArgumentException("A hand cannot contain a physical tile more than once.", nameof(hand));
            var normals = new Dictionary<TileFace, List<TileId>>();
            var jokers = new List<TileId>(2);
            foreach (var id in ids)
            {
                if (TileCatalog.IsRealJoker(id, indicator)) { jokers.Add(id); continue; }
                var face = TileCatalog.GetNonJokerFace(id, indicator);
                if (!normals.TryGetValue(face, out var matching))
                    normals.Add(face, matching = new List<TileId>(2));
                matching.Add(id);
            }

            var candidates = new List<(MeldProposal Group, int Score)>();
            var used = new bool[106];

            void AddPattern(TileFace[] pattern)
            {
                token.ThrowIfCancellationRequested();
                if (pattern.Length > ids.Length) return;
                var placed = new PlacedTile[pattern.Length];
                var score = pattern.Sum(face => face.Number);

                // Distinct: only the first unused tile — a copy of the face, or an okey — is tried (the binder picks copies).
                TileId? First(List<TileId> options)
                {
                    foreach (var id in options) if (!used[id.Value]) return id;
                    return null;
                }

                void Place(int position, TileId id, TileFace face)
                {
                    used[id.Value] = true;
                    placed[position] = new PlacedTile(id, face);
                    Fill(position + 1);
                    used[id.Value] = false;
                }

                void Fill(int position)
                {
                    token.ThrowIfCancellationRequested();
                    if (position == pattern.Length)
                    {
                        // A pair's equal faces make the two placement permutations equivalent.
                        if (kind == MeldKind.Pair && placed[0].Id.Value >= placed[1].Id.Value) return;
                        candidates.Add((new MeldProposal(kind, placed), score));
                        return;
                    }
                    var face = pattern[position];
                    if (distinct)
                    {
                        if (normals.TryGetValue(face, out var copies) && First(copies) is TileId natural) Place(position, natural, face);
                        if (First(jokers) is TileId okey) Place(position, okey, face);
                        return;
                    }
                    if (normals.TryGetValue(face, out var matching))
                        foreach (var id in matching)
                            if (!used[id.Value]) Place(position, id, face);

                    // A matching natural tile must not suppress valid joker alternatives.
                    foreach (var joker in jokers)
                        if (!used[joker.Value]) Place(position, joker, face);
                }

                Fill(0);
            }

            switch (kind)
            {
                case MeldKind.Run:
                    for (var color = 0; color < 4; color++)
                        for (var start = 1; start <= 11; start++)
                            for (var length = 3; length <= Math.Min(14 - start, ids.Length); length++)
                            {
                                var pattern = new TileFace[length];
                                for (var i = 0; i < length; i++) pattern[i] = new TileFace((TileColor)color, start + i);
                                AddPattern(pattern);
                            }
                    break;
                case MeldKind.Set:
                    for (var number = 1; number <= 13; number++)
                        for (var mask = 1; mask < 16; mask++)
                        {
                            var pattern = new List<TileFace>(4);
                            for (var color = 0; color < 4; color++)
                                if ((mask & (1 << color)) != 0) pattern.Add(new TileFace((TileColor)color, number));
                            if (pattern.Count >= 3) AddPattern(pattern.ToArray());
                        }
                    break;
                case MeldKind.Pair:
                    for (var color = 0; color < 4; color++)
                        for (var number = 1; number <= 13; number++)
                        {
                            var face = new TileFace((TileColor)color, number);
                            AddPattern(new[] { face, face });
                        }
                    break;
            }

            candidates.Sort(Compare);
            token.ThrowIfCancellationRequested();
            return Array.AsReadOnly(candidates.Select(candidate => candidate.Group).ToArray());
        }

        private static int Compare((MeldProposal Group, int Score) left, (MeldProposal Group, int Score) right)
        {
            var comparison = right.Score.CompareTo(left.Score);
            if (comparison != 0) return comparison;
            var a = left.Group.Tiles;
            var b = right.Group.Tiles;
            var common = Math.Min(a.Count, b.Count);
            for (var i = 0; i < common; i++)
            {
                comparison = ((int)a[i].RepresentedFace.Color).CompareTo((int)b[i].RepresentedFace.Color);
                if (comparison != 0) return comparison;
            }
            comparison = a.Count.CompareTo(b.Count);
            if (comparison != 0) return comparison;
            for (var i = 0; i < common; i++)
            {
                comparison = a[i].Id.CompareTo(b[i].Id);
                if (comparison != 0) return comparison;
            }
            for (var i = 0; i < common; i++)
            {
                comparison = a[i].RepresentedFace.Number.CompareTo(b[i].RepresentedFace.Number);
                if (comparison != 0) return comparison;
            }
            return 0;
        }
    }

    /// <summary>
    /// Binds group patterns to a hand's tiles. Copies of a face (and the two okeys) are interchangeable, so a pattern takes
    /// the lowest free copy of each face it shows — <paramref name="preferred"/> (a tile just taken from the pile) first
    /// among its copies. Whatever packing of patterns the hand's face counts allow then binds disjointly, with every copy
    /// mix it needs (two fixed copy orders cannot: shared faces may form an odd cycle). Positions fit a 32-bit mask.
    /// </summary>
    public sealed class MeldBinder
    {
        private const int JokerKey = 52;
        private readonly TileFace indicator;
        private readonly TileId[] tiles;
        private readonly int[] keys, first = new int[JokerKey + 1], count = new int[JokerKey + 1];

        public MeldBinder(IReadOnlyList<TileId> hand, TileFace indicator, TileId? preferred = null)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            if (hand.Count > 32) throw new ArgumentException("A hand of at most 32 tiles fits the binder's masks.", nameof(hand));
            this.indicator = indicator;
            // Sorted by face key, the preferred tile first among its copies, then id: one composite int per tile.
            tiles = new TileId[hand.Count]; keys = new int[hand.Count]; var order = new int[hand.Count];
            for (var i = 0; i < tiles.Length; i++)
            {
                tiles[i] = hand[i];
                order[i] = KeyOf(hand[i]) * 256 + (preferred.HasValue && hand[i] == preferred.Value ? 0 : 128) + hand[i].Value;
            }
            Array.Sort(order, tiles);
            for (var i = tiles.Length - 1; i >= 0; i--) { keys[i] = order[i] / 256; first[keys[i]] = i; count[keys[i]]++; }
        }

        /// <summary>The hand in binding order (by face, the preferred tile first among its copies, okeys last).</summary>
        public IReadOnlyList<TileId> Tiles => tiles;
        public int KeyAt(int position) => keys[position];
        private int KeyOf(TileId id) => TileCatalog.IsRealJoker(id, indicator) ? JokerKey : FaceKey(TileCatalog.GetNonJokerFace(id, indicator));
        private static int FaceKey(TileFace face) => (int)face.Color * 13 + face.Number - 1;
        /// <summary>What a group's tile needs: a copy of its face, or any okey for an okey.</summary>
        public int KeyOf(PlacedTile tile) => TileCatalog.IsRealJoker(tile.Id, indicator) ? JokerKey : FaceKey(tile.RepresentedFace);

        /// <summary>What each of a group's tiles needs. It depends on the indicator only: compute it once per pattern.</summary>
        public static int[] Needs(MeldProposal group, TileFace indicator)
        {
            var tiles = group.Tiles; var needs = new int[tiles.Count];
            for (var i = 0; i < needs.Length; i++) needs[i] = TileCatalog.IsRealJoker(tiles[i].Id, indicator) ? JokerKey : FaceKey(tiles[i].RepresentedFace);
            return needs;
        }

        public bool TryMask(MeldProposal group, uint taken, out uint mask) => TryMask(Needs(group, indicator), taken, out mask);

        /// <summary>Positions a pattern takes beside the <paramref name="taken"/> ones; false when a face has no free copy.</summary>
        public bool TryMask(int[] needs, uint taken, out uint mask)
        {
            mask = 0;
            for (var i = 0; i < needs.Length; i++)
            {
                int key = needs[i], p = first[key], end = p + count[key];
                while (p < end && ((taken | mask) & (1u << p)) != 0) p++;
                if (p == end) return false;
                mask |= 1u << p;
            }
            return true;
        }

        public MeldProposal Bind(MeldProposal group, uint taken) => Bind(group, Needs(group, indicator), taken);

        /// <summary>The group on the tiles <see cref="TryMask(int[], uint, out uint)"/> chose.</summary>
        public MeldProposal Bind(MeldProposal group, int[] needs, uint taken)
        {
            var placed = new PlacedTile[needs.Length]; uint mask = 0;
            for (var i = 0; i < placed.Length; i++)
            {
                int key = needs[i], p = first[key], end = p + count[key];
                while (p < end && ((taken | mask) & (1u << p)) != 0) p++;
                if (p == end) throw new InvalidOperationException("The hand cannot hold this group.");
                mask |= 1u << p; placed[i] = new PlacedTile(tiles[p], group.Tiles[i].RepresentedFace);
            }
            return new MeldProposal(group.Kind, placed);
        }
    }
}
