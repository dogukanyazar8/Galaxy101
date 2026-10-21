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

            void AddPattern(TileFace[] pattern)
            {
                token.ThrowIfCancellationRequested();
                if (pattern.Length > ids.Length) return;
                var placed = new PlacedTile[pattern.Length];
                var used = new bool[106];
                var score = pattern.Sum(face => face.Number);

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
                    if (normals.TryGetValue(face, out var matching))
                        foreach (var id in matching)
                        {
                            if (used[id.Value]) continue;
                            used[id.Value] = true;
                            placed[position] = new PlacedTile(id, face);
                            Fill(position + 1);
                            used[id.Value] = false;
                        }

                    // A matching natural tile must not suppress valid joker alternatives.
                    foreach (var joker in jokers)
                    {
                        if (used[joker.Value]) continue;
                        used[joker.Value] = true;
                        placed[position] = new PlacedTile(joker, face);
                        Fill(position + 1);
                        used[joker.Value] = false;
                    }
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
}
