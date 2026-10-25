using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using Okey101.Core.Model;
using Okey101.Core.V4;

namespace Okey101.Application
{
    /// <summary>Which engine opens a record (K-02).</summary>
    public enum RecordEngine { Unknown, Legacy, V4 }

    /// <summary>
    /// K-02 record versions. Matches dealt and played by the v4 step engine (<see cref="G101Rules"/>, mulberry32) are written as
    /// <c>101-v4</c> / <c>mulberry32-v4</c>; <c>101-v1</c> / <c>xorshift32-fy-v1</c> records keep opening with the old engine
    /// (<see cref="Okey101.Core.Turns.GameEngine"/>); any other combination is rejected.
    /// </summary>
    public static class RecordVersions
    {
        public const int LegacySchema = ReplayRecord.SchemaVersion;
        public const string LegacyRule = ReplayRecord.RuleVersion, LegacyShuffle = ReplayRecord.ShuffleVersion;
        public const int V4Schema = V4ReplayRecord.SchemaVersion;
        public const string V4Rule = G101Rules.RuleVersion, V4Shuffle = G101Rules.ShuffleVersion;

        public static RecordEngine Classify(int schemaVersion, string ruleVersion, string shuffleVersion)
        {
            if (schemaVersion == LegacySchema && ruleVersion == LegacyRule && shuffleVersion == LegacyShuffle) return RecordEngine.Legacy;
            if (schemaVersion == V4Schema && ruleVersion == V4Rule && shuffleVersion == V4Shuffle) return RecordEngine.V4;
            return RecordEngine.Unknown;
        }
    }

    /// <summary>
    /// A v4 match record: options and seed of <see cref="G101Rules.NewMatch"/>, then every accepted action in order, and the
    /// SHA-256 of the final state (canonical tree JSON). Replaying the actions on the same seed rebuilds the match exactly.
    /// </summary>
    public sealed class V4ReplayRecord
    {
        public const int SchemaVersion = 1;
        public const string RuleVersion = G101Rules.RuleVersion, ShuffleVersion = G101Rules.ShuffleVersion;
        public uint Seed { get; }
        public MatchOptions Options { get; }
        public IReadOnlyList<GameAction> Actions { get; }
        public string ExpectedFinalHash { get; }
        public V4ReplayRecord(MatchOptions options, uint seed, IEnumerable<GameAction> actions, string expectedFinalHash)
        {
            Options = (options ?? throw new ArgumentNullException(nameof(options))).Clone();
            Seed = seed;
            Actions = Array.AsReadOnly((actions ?? throw new ArgumentNullException(nameof(actions))).ToArray());
            ExpectedFinalHash = expectedFinalHash;
        }
        public static V4ReplayRecord Capture(MatchOptions options, uint seed, IEnumerable<GameAction> actions, MatchState final)
            => new V4ReplayRecord(options, seed, actions, V4Replay.Hash(final));
    }

    /// <summary>Hashing, restoring and the closed JSON shape of <see cref="V4ReplayRecord"/>.</summary>
    public static class V4Replay
    {
        public const int MaxActions = 20000;
        private static readonly HashSet<string> ActionKinds = new HashSet<string>
            { "startHand", "nextHand", "draw", "returnSide", "open", "lay", "add", "swap", "undo", "discard", "timeout", "leave", "takeSeat" };

        public static string Hash(MatchState state)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(V4Tree.Canonical(state.ToTree())))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Replays the record from a new match. Every action must be accepted and the final hash must match.</summary>
        public static RuleResult<MatchState> Restore(V4ReplayRecord record)
        {
            if (record == null || record.Actions.Count > MaxActions) return RuleResult<MatchState>.Fail(ErrorCode.InvalidState);
            var state = G101Rules.NewMatch(record.Options, record.Seed);
            try
            {
                foreach (var action in record.Actions)
                {
                    var r = G101Rules.Apply(state, action);
                    if (!r.Ok) return RuleResult<MatchState>.Fail(ErrorCode.InvalidState);
                    state = r.State;
                }
            }
            catch (ArgumentException) { return RuleResult<MatchState>.Fail(ErrorCode.InvalidState); }
            catch (IndexOutOfRangeException) { return RuleResult<MatchState>.Fail(ErrorCode.InvalidState); }
            catch (NullReferenceException) { return RuleResult<MatchState>.Fail(ErrorCode.InvalidState); }
            return string.Equals(Hash(state), record.ExpectedFinalHash, StringComparison.Ordinal)
                ? RuleResult<MatchState>.Ok(state) : RuleResult<MatchState>.Fail(ErrorCode.InvalidState);
        }

        [DataContract] internal sealed class DocumentDto
        {
            [DataMember(IsRequired=true, Order=0)] public int SchemaVersion;
            [DataMember(IsRequired=true, Order=1)] public string RuleVersion;
            [DataMember(IsRequired=true, Order=2)] public string ShuffleVersion;
            [DataMember(IsRequired=true, Order=3)] public uint Seed;
            [DataMember(IsRequired=true, Order=4)] public OptionsDto Options;
            [DataMember(IsRequired=true, Order=5)] public ActionDto[] Actions;
            [DataMember(IsRequired=true, Order=6)] public string ExpectedFinalHash;
        }
        [DataContract] internal sealed class OptionsDto
        {
            [DataMember(IsRequired=true, Order=0)] public string Mode;
            [DataMember(IsRequired=true, Order=1)] public int Hands;
            [DataMember(IsRequired=true, Order=2)] public bool Penalties;
            [DataMember(IsRequired=true, Order=3)] public bool Isle;
            [DataMember(IsRequired=true, Order=4)] public bool Katlama;
            [DataMember(IsRequired=true, Order=5)] public bool KatlamaPartner;
        }
        [DataContract] internal sealed class ActionDto
        {
            [DataMember(IsRequired=true, Order=0)] public string T;
            [DataMember(IsRequired=false, EmitDefaultValue=false, Order=1)] public int? Seat;
            [DataMember(IsRequired=false, EmitDefaultValue=false, Order=2)] public string Src;
            [DataMember(IsRequired=false, EmitDefaultValue=false, Order=3)] public MeldDto[] Melds;
            [DataMember(IsRequired=false, EmitDefaultValue=false, Order=4)] public string Tile;
            [DataMember(IsRequired=false, EmitDefaultValue=false, Order=5)] public string Meld;
            [DataMember(IsRequired=false, EmitDefaultValue=false, Order=6)] public string Side;
            [DataMember(IsRequired=false, EmitDefaultValue=false, Order=7)] public int? Index;
            [DataMember(IsRequired=false, EmitDefaultValue=false, Order=8)] public RigDto Rig;
        }
        [DataContract] internal sealed class MeldDto
        {
            [DataMember(IsRequired=true, Order=0)] public string Kind;
            [DataMember(IsRequired=true, Order=1)] public string[] Tiles;
        }
        [DataContract] internal sealed class RigDto
        {
            [DataMember(IsRequired=false, EmitDefaultValue=false, Order=0)] public int? Dealer;
            [DataMember(IsRequired=false, EmitDefaultValue=false, Order=1)] public int[] Dice;
            [DataMember(IsRequired=false, EmitDefaultValue=false, Order=2)] public string[] Order;
        }

        internal static byte[] Encode(V4ReplayRecord record) => DiagnosticsJson.Encode(new DocumentDto
        {
            SchemaVersion = V4ReplayRecord.SchemaVersion, RuleVersion = V4ReplayRecord.RuleVersion, ShuffleVersion = V4ReplayRecord.ShuffleVersion, Seed = record.Seed,
            Options = new OptionsDto { Mode = record.Options.Mode, Hands = record.Options.Hands, Penalties = record.Options.Penalties, Isle = record.Options.Isle, Katlama = record.Options.Katlama, KatlamaPartner = record.Options.KatlamaPartner },
            Actions = record.Actions.Select(Action).ToArray(), ExpectedFinalHash = record.ExpectedFinalHash
        });

        /// <summary>Decodes and validates the shape (not the play: <see cref="Restore"/> does that). Throws SerializationException.</summary>
        internal static V4ReplayRecord Decode(byte[] bytes)
        {
            var dto = DiagnosticsJson.DecodeAs<DocumentDto>(bytes);
            if (RecordVersions.Classify(dto.SchemaVersion, dto.RuleVersion, dto.ShuffleVersion) != RecordEngine.V4
                || dto.Options == null || dto.ExpectedFinalHash == null || dto.ExpectedFinalHash.Length != 64
                || dto.Actions == null || dto.Actions.Length > MaxActions || dto.Actions.Any(a => a == null))
                throw new SerializationException("Unsupported or incomplete v4 replay.");
            var o = dto.Options;
            if ((o.Mode != "solo" && o.Mode != "pairs") || o.Hands < 1 || o.Hands > 99) throw new SerializationException("Invalid options.");
            var options = new MatchOptions { Mode = o.Mode, Hands = o.Hands, Penalties = o.Penalties, Isle = o.Isle, Katlama = o.Katlama, KatlamaPartner = o.KatlamaPartner };
            return new V4ReplayRecord(options, dto.Seed, dto.Actions.Select(Action), dto.ExpectedFinalHash);
        }

        private static ActionDto Action(GameAction a) => new ActionDto
        {
            T = a.T, Seat = a.Seat >= 0 ? (int?)a.Seat : null, Src = a.Src, Tile = a.Tile, Meld = a.Meld, Side = a.Side, Index = a.Index >= 0 ? (int?)a.Index : null,
            Melds = a.Melds?.Select(m => new MeldDto { Kind = m.Kind, Tiles = m.Tiles.ToArray() }).ToArray(),
            Rig = a.Rig == null ? null : new RigDto { Dealer = a.Rig.Dealer, Dice = a.Rig.Dice, Order = a.Rig.Order?.ToArray() }
        };

        private static GameAction Action(ActionDto d)
        {
            if (d.T == null || !ActionKinds.Contains(d.T)) throw new SerializationException("Unknown action.");
            if (d.Seat.HasValue && (d.Seat.Value < 0 || d.Seat.Value > 3)) throw new SerializationException("Invalid seat.");
            if (d.Index.HasValue && (d.Index.Value < 0 || d.Index.Value > 12)) throw new SerializationException("Invalid index.");
            var a = new GameAction { T = d.T, Seat = d.Seat ?? -1, Src = d.Src, Tile = Tile(d.Tile), Meld = d.Meld, Side = d.Side, Index = d.Index ?? -1 };
            if (d.Melds != null)
            {
                if (d.Melds.Length > 53 || d.Melds.Any(m => m == null || m.Tiles == null || m.Tiles.Length > 13)) throw new SerializationException("Invalid melds.");
                a.Melds = d.Melds.Select(m => new MeldSpec(m.Kind, m.Tiles.Select(Tile).ToList())).ToList();
            }
            if (d.Rig != null)
            {
                var rig = d.Rig;
                if (rig.Dealer.HasValue && (rig.Dealer.Value < 0 || rig.Dealer.Value > 3)) throw new SerializationException("Invalid dealer.");
                if (rig.Dice != null && (rig.Dice.Length != 2 || rig.Dice.Any(x => x < 1 || x > 6))) throw new SerializationException("Invalid dice.");
                if (rig.Order != null && (rig.Order.Length != 106 || rig.Order.Distinct().Count() != 106 || rig.Order.Any(x => !G101Rules.IsTile(x))))
                    throw new SerializationException("Invalid deal order.");
                a.Rig = new DealRig { Dealer = rig.Dealer, Dice = rig.Dice, Order = rig.Order?.ToList() };
            }
            return a;
        }

        private static string Tile(string id)
        {
            if (id != null && !G101Rules.IsTile(id)) throw new SerializationException("Unknown tile.");
            return id;
        }
    }
}
