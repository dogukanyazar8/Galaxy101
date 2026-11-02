using System;
using System.Collections.Generic;
using Galaxy101.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Galaxy101.Table
{
    /// <summary>
    /// v4 frames on the existing socket (K-04, <c>{type, id, data}</c>): the data of a table message as its contracts type,
    /// with the engine-shaped parts (game.ev, tableView.view, legal.legal, snapshot.game, matchResult ranking/history) as
    /// plain trees (Dictionary / List / long / double / string / bool / null), the same shapes the offline runtime hands the
    /// client. Field names are camelCase on both sides.
    /// </summary>
    public static class V4Wire
    {
        public static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(), NullValueHandling = NullValueHandling.Include,
            DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double,
        };

        private static readonly JsonSerializer Serializer = JsonSerializer.Create(Json);

        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>
        {
            [V4Messages.Session] = typeof(V4Session), [V4Messages.Snapshot] = typeof(V4Snapshot), [V4Messages.Balance] = typeof(V4Balance),
            [V4Messages.Xp] = typeof(V4Xp), [V4Messages.TableState] = typeof(V4TableEvent), [V4Messages.TableClosed] = typeof(V4TableClosed),
            [V4Messages.Kicked] = typeof(V4Kicked), [V4Messages.Countdown] = typeof(V4Countdown), [V4Messages.SeatConnection] = typeof(V4SeatConnection),
            [V4Messages.LeftTable] = typeof(V4LeftTable), [V4Messages.RankChange] = typeof(V4RankChange), [V4Messages.MatchStart] = typeof(V4MatchStart),
            [V4Messages.DealStart] = typeof(V4DealStart), [V4Messages.DealStep] = typeof(V4DealStep), [V4Messages.DealDie] = typeof(V4DealDie),
            [V4Messages.DealSkip] = typeof(V4DealSkip), [V4Messages.DealSkipVote] = typeof(V4DealSkipVote),
            [V4Messages.HandDealt] = typeof(V4HandDealt), [V4Messages.Turn] = typeof(V4Turn), [V4Messages.Legal] = typeof(V4Legal),
            [V4Messages.Game] = typeof(V4GameEvent), [V4Messages.HandWait] = typeof(V4HandWait), [V4Messages.ReadyState] = typeof(V4ReadyState),
            [V4Messages.MatchResult] = typeof(V4MatchResult), [V4Messages.Chat] = typeof(V4Chat), [V4Messages.Gift] = typeof(V4Gift),
            [V4Messages.TableView] = typeof(V4TableView), [V4Messages.RankedReady] = typeof(V4RankedReady),
        };

        /// <summary>The table messages a match session listens to (protocol.json s2c, table and deal groups).</summary>
        public static readonly string[] TableMessages =
        {
            V4Messages.MatchStart, V4Messages.DealStart, V4Messages.DealStep, V4Messages.DealDie, V4Messages.DealSkip, V4Messages.DealSkipVote, V4Messages.HandDealt, V4Messages.Turn,
            V4Messages.Legal, V4Messages.Game, V4Messages.HandWait, V4Messages.ReadyState, V4Messages.MatchResult, V4Messages.TableView,
            V4Messages.SeatConnection, V4Messages.Chat, V4Messages.Gift, V4Messages.Snapshot, V4Messages.LeftTable, V4Messages.TableClosed,
            V4Messages.Xp, V4Messages.Balance, V4Messages.RankChange,
        };

        public static bool Knows(string type) => Types.ContainsKey(type);

        /// <summary>A message's data as its contracts type (engine parts as trees); the bare tree for an unknown type.</summary>
        public static object Payload(string type, JToken data)
        {
            if (!Types.TryGetValue(type, out var t)) return ToTree(data);
            var o = data == null || data.Type == JTokenType.Null ? Activator.CreateInstance(t) : data.ToObject(t, Serializer);
            switch (o)
            {
                case V4GameEvent g: g.Ev = ToTree(g.Ev); break;
                case V4TableView v: v.View = ToTree(v.View); break;
                case V4Legal l: l.Legal = ToTree(l.Legal); break;
                case V4Snapshot s: s.Game = ToTree(s.Game); break;
                case V4MatchResult m: m.Ranking = ToTree(m.Ranking); m.History = ToTree(m.History); break;
            }
            return o;
        }

        /// <summary>A JSON value as a plain tree; other values pass through.</summary>
        public static object ToTree(object value)
        {
            switch (value)
            {
                case null: return null;
                case JObject o:
                {
                    var d = new Dictionary<string, object>();
                    foreach (var p in o.Properties()) d[p.Name] = ToTree(p.Value);
                    return d;
                }
                case JArray a:
                {
                    var l = new List<object>(a.Count);
                    foreach (var x in a) l.Add(ToTree(x));
                    return l;
                }
                case JValue v:
                    switch (v.Type)
                    {
                        case JTokenType.Null: case JTokenType.Undefined: return null;
                        case JTokenType.Integer: return Convert.ToInt64(v.Value);
                        case JTokenType.Float: return Convert.ToDouble(v.Value);
                        case JTokenType.Boolean: return (bool)v.Value;
                        default: return v.Value?.ToString();
                    }
                default: return value;
            }
        }

        /// <summary>A payload as the server puts it on the wire (camelCase JSON).</summary>
        public static JToken ToJson(object payload) => payload == null ? JValue.CreateNull() : JToken.FromObject(payload, Serializer);
    }
}
