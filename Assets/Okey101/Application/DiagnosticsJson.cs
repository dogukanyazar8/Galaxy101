using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using Okey101.Core.Model;

namespace Okey101.Application
{
    // Closed DTO graph. No CLR type-name resolution, polymorphic deserialization or Unity dependency.
    internal static class DiagnosticsJson
    {
        [DataContract] internal sealed class DocumentDto
        {
            [DataMember(IsRequired=true, Order=0)] public int SchemaVersion;
            [DataMember(IsRequired=true, Order=1)] public string RuleVersion;
            [DataMember(IsRequired=true, Order=2)] public string ShuffleVersion;
            [DataMember(IsRequired=true, Order=3)] public uint Seed;
            [DataMember(IsRequired=true, Order=4)] public StateDto InitialState;
            [DataMember(IsRequired=true, Order=5)] public CommandDto[] Commands;
            [DataMember(IsRequired=true, Order=6)] public string ExpectedFinalHash;
        }
        [DataContract] internal sealed class StateDto
        {
            [DataMember(IsRequired=true, Order=0)] public string RoundId;
            [DataMember(IsRequired=true, Order=1)] public long Version;
            [DataMember(IsRequired=true, Order=2)] public uint Seed;
            [DataMember(IsRequired=true, Order=3)] public int StartingPlayer;
            [DataMember(IsRequired=true, Order=4)] public int ActivePlayer;
            [DataMember(IsRequired=true, Order=5)] public int TurnNumber;
            [DataMember(IsRequired=true, Order=6)] public int Phase;
            [DataMember(IsRequired=true, Order=7)] public int Indicator;
            [DataMember(IsRequired=true, Order=8)] public int[] Stock;
            [DataMember(IsRequired=true, Order=9)] public PlayerDto[] Players;
            [DataMember(IsRequired=true, Order=10)] public MeldDto[] Table;
            [DataMember(IsRequired=true, Order=11)] public int[][] Discards;
            [DataMember(IsRequired=true, Order=12)] public DrawDto PendingDraw;
            [DataMember(IsRequired=true, Order=13)] public int OpeningAtTurnStart;
            [DataMember(IsRequired=true, Order=14)] public int SeriesTilesUsed;
            [DataMember(IsRequired=true, Order=15)] public bool IsInitialTurn;
            [DataMember(IsRequired=true, Order=16)] public long[] LastAcceptedSequences;
            [DataMember(IsRequired=true, Order=17)] public ResultDto Result;
        }
        [DataContract] internal sealed class PlayerDto
        {
            [DataMember(IsRequired=true)] public int[] Hand;
            [DataMember(IsRequired=true)] public int Opening;
            [DataMember(IsRequired=true)] public int Penalties;
        }
        [DataContract] internal sealed class DrawDto
        {
            [DataMember(IsRequired=true)] public int Source;
            [DataMember(IsRequired=true)] public int Tile;
            [DataMember(IsRequired=true)] public bool WasLastStock;
        }
        [DataContract] internal sealed class PlacedDto
        {
            [DataMember(IsRequired=true)] public int Id;
            [DataMember(IsRequired=true)] public int Color;
            [DataMember(IsRequired=true)] public int Number;
        }
        [DataContract] internal sealed class GroupDto
        {
            [DataMember(IsRequired=true)] public int Kind;
            [DataMember(IsRequired=true)] public PlacedDto[] Tiles;
        }
        [DataContract] internal sealed class MeldDto
        {
            [DataMember(IsRequired=true)] public int Id;
            [DataMember(IsRequired=true)] public int Owner;
            [DataMember(IsRequired=true)] public GroupDto Group;
        }
        [DataContract] internal sealed class ResultDto
        {
            [DataMember(IsRequired=true)] public int Reason;
            [DataMember(IsRequired=true)] public int? Winner;
            [DataMember(IsRequired=true)] public int WinnerOpening;
            [DataMember(IsRequired=true)] public bool WinnerOpenedThisTurn;
            [DataMember(IsRequired=true)] public bool DiscardedRealJoker;
            [DataMember(IsRequired=true)] public ScoreDto[] Scores;
        }
        [DataContract] internal sealed class ScoreDto
        {
            [DataMember(IsRequired=true)] public bool HasScore;
            [DataMember(IsRequired=true)] public int N;
            [DataMember(IsRequired=true)] public int J;
            [DataMember(IsRequired=true)] public int P;
            [DataMember(IsRequired=true)] public int B;
            [DataMember(IsRequired=true)] public int M;
            [DataMember(IsRequired=true)] public int? Total;
        }
        [DataContract] internal sealed class CommandDto
        {
            [DataMember(IsRequired=true)] public string Kind;
            [DataMember(IsRequired=true)] public string RoundId;
            [DataMember(IsRequired=true)] public int Actor;
            [DataMember(IsRequired=true)] public long Sequence;
            [DataMember(IsRequired=true)] public long ExpectedVersion;
            [DataMember(IsRequired=true)] public int Source;
            [DataMember(IsRequired=true)] public ActionDto[] Actions;
            [DataMember(IsRequired=true)] public int Discard;
            [DataMember(IsRequired=true)] public bool AcceptPenalty;
        }
        [DataContract] internal sealed class ActionDto
        {
            [DataMember(IsRequired=true)] public string Kind;
            [DataMember(IsRequired=true)] public int Opening;
            [DataMember(IsRequired=true)] public GroupDto[] Groups;
            [DataMember(IsRequired=true)] public int MeldId;
            [DataMember(IsRequired=true)] public bool Prepend;
            [DataMember(IsRequired=true)] public PlacedDto[] Tiles;
            [DataMember(IsRequired=true)] public int JokerId;
            [DataMember(IsRequired=true)] public int ReplacementId;
        }
        private static DataContractJsonSerializer Serializer(Type type) => new DataContractJsonSerializer(type,
            new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 500000 });
        internal static byte[] Encode<T>(T value)
        { using (var stream = new MemoryStream()) { Serializer(typeof(T)).WriteObject(stream, value); return stream.ToArray(); } }
        internal static DocumentDto Decode(byte[] bytes)
        {
            CheckSingleObject(bytes);
            var quotas = new XmlDictionaryReaderQuotas { MaxDepth = 32, MaxArrayLength = 200000, MaxStringContentLength = 4096, MaxBytesPerRead = 4096, MaxNameTableCharCount = 8192 };
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, 0, bytes.Length, Encoding.UTF8, quotas, null))
            {
                var document = (DocumentDto)Serializer(typeof(DocumentDto)).ReadObject(reader, true);
                if (reader.Read()) throw new SerializationException("Trailing JSON is not allowed.");
                return document ?? throw new SerializationException("A document is required.");
            }
        }
        private static void CheckSingleObject(byte[] bytes)
        {
            // Unity's Mono JSON reader stops after the first root and ignores a second
            // root. Enforce framing here; the standard reader still validates JSON syntax.
            bool text=false,escaped=false,started=false,ended=false; var depth=0;
            foreach(var value in bytes)
            {
                if(ended || !started)
                {
                    if(value==32 || value==9 || value==10 || value==13) continue;
                    if(ended || value!='{') throw new SerializationException("Exactly one JSON object is required.");
                    started=true;
                }
                if(text)
                { if(escaped) escaped=false;else if(value=='\\') escaped=true;else if(value=='"') text=false;continue; }
                if(value=='"') text=true;
                else if(value=='{' || value=='[') { if(++depth>32) throw new SerializationException("JSON nesting limit exceeded."); }
                else if(value=='}' || value==']') { if(--depth==0) ended=true; }
            }
            if(!ended || text) throw new SerializationException("Incomplete JSON object.");
        }
        private static T[] Items<T>(T[] values, int maximum)
        {
            if (values == null || values.Length > maximum || values.Any(value => value == null)) throw new SerializationException("Invalid collection.");
            return values;
        }
        internal static DocumentDto Document(ReplayRecord record) => new DocumentDto {
            SchemaVersion=ReplayRecord.SchemaVersion, RuleVersion=ReplayRecord.RuleVersion, ShuffleVersion=ReplayRecord.ShuffleVersion,
            Seed=record.Seed, InitialState=Snapshot(record.InitialState), Commands=record.Commands.Select(Command).ToArray(), ExpectedFinalHash=record.ExpectedFinalHash };
        internal static ReplayRecord Record(DocumentDto dto)
        {
            if (dto.SchemaVersion != ReplayRecord.SchemaVersion || dto.RuleVersion != ReplayRecord.RuleVersion || dto.ShuffleVersion != ReplayRecord.ShuffleVersion
                || dto.InitialState == null || dto.Seed != dto.InitialState.Seed || dto.ExpectedFinalHash == null || dto.ExpectedFinalHash.Length != 64)
                throw new SerializationException("Unsupported or incomplete replay.");
            return new ReplayRecord(State(dto.InitialState), Items(dto.Commands, ReplayCodec.MaxCommands).Select(Command).ToArray(), dto.ExpectedFinalHash);
        }
        internal static StateDto Snapshot(RoundState s) => new StateDto {
            RoundId=s.RoundId.ToString("D"), Version=s.Version, Seed=s.Seed, StartingPlayer=s.StartingPlayer, ActivePlayer=s.ActivePlayer, TurnNumber=s.TurnNumber,
            Phase=(int)s.Phase, Indicator=s.Indicator.Value, Stock=s.Stock.Select(id=>id.Value).ToArray(),
            Players=s.Players.Select(p=>new PlayerDto { Hand=p.Hand.Select(id=>id.Value).ToArray(), Opening=(int)p.Opening, Penalties=p.Penalties }).ToArray(),
            Table=s.Table.Select(m=>new MeldDto { Id=m.Id, Owner=m.Owner, Group=Group(m.Group) }).ToArray(),
            Discards=s.Discards.Select(p=>p.Select(id=>id.Value).ToArray()).ToArray(),
            PendingDraw=s.PendingDraw==null ? null : new DrawDto { Source=(int)s.PendingDraw.Source, Tile=s.PendingDraw.Tile.Value, WasLastStock=s.PendingDraw.WasLastStock },
            OpeningAtTurnStart=(int)s.OpeningAtTurnStart, SeriesTilesUsed=s.SeriesTilesUsed, IsInitialTurn=s.IsInitialTurn, LastAcceptedSequences=s.LastAcceptedSequences.ToArray(),
            Result=s.Result==null ? null : new ResultDto { Reason=(int)s.Result.FinishInfo.Reason, Winner=s.Result.FinishInfo.Winner, WinnerOpening=(int)s.Result.FinishInfo.WinnerOpening,
                WinnerOpenedThisTurn=s.Result.FinishInfo.WinnerOpenedThisTurn, DiscardedRealJoker=s.Result.FinishInfo.DiscardedRealJoker,
                Scores=s.Result.Scores.Select(x=>new ScoreDto { HasScore=x.HasScore,N=x.N,J=x.J,P=x.P,B=x.B,M=x.M,Total=x.Total }).ToArray() } };
        private static RoundState State(StateDto s)
        {
            var players=Items(s.Players,4).Select(p=>new PlayerState(Items(p.Hand,22).Select(id=>new TileId(id)).ToArray(),(OpeningKind)p.Opening,p.Penalties)).ToArray();
            var table=Items(s.Table,53).Select(m=>new Meld(m.Id,m.Owner,Group(m.Group))).ToArray();
            var result=s.Result;
            return new RoundState(Guid.Parse(s.RoundId),s.Version,s.Seed,s.StartingPlayer,s.ActivePlayer,s.TurnNumber,(RoundPhase)s.Phase,new TileId(s.Indicator),
                Items(s.Stock,106).Select(id=>new TileId(id)).ToArray(),players,table,
                Items(s.Discards,4).Select(p=>(System.Collections.Generic.IReadOnlyList<TileId>)Items(p,106).Select(id=>new TileId(id)).ToArray()).ToArray(),
                s.PendingDraw==null?null:new DrawReceipt((DrawSource)s.PendingDraw.Source,new TileId(s.PendingDraw.Tile),s.PendingDraw.WasLastStock),
                (OpeningKind)s.OpeningAtTurnStart,s.SeriesTilesUsed,s.IsInitialTurn,Items(s.LastAcceptedSequences,4),
                result==null?null:new RoundResult(new FinishInfo((FinishReason)result.Reason,result.Winner,(OpeningKind)result.WinnerOpening,result.WinnerOpenedThisTurn,result.DiscardedRealJoker),
                    Items(result.Scores,4).Select(x=>new ScoreBreakdown(x.HasScore,x.N,x.J,x.P,x.B,x.M,x.Total)).ToArray()));
        }
        private static PlacedDto Placed(PlacedTile t)=>new PlacedDto { Id=t.Id.Value,Color=(int)t.RepresentedFace.Color,Number=t.RepresentedFace.Number };
        private static PlacedTile Placed(PlacedDto t)=>new PlacedTile(new TileId(t.Id),new TileFace((TileColor)t.Color,t.Number));
        private static GroupDto Group(MeldProposal g)=>new GroupDto { Kind=(int)g.Kind,Tiles=g.Tiles.Select(Placed).ToArray() };
        private static MeldProposal Group(GroupDto g)
        { if(g==null) throw new SerializationException("Group is required."); return new MeldProposal((MeldKind)g.Kind,Items(g.Tiles,13).Select(Placed).ToArray()); }
        private static CommandDto Command(GameCommand c)
        {
            var dto=new CommandDto { Kind=c is DrawCommand?"Draw":"Commit",RoundId=c.Meta.RoundId.ToString("D"),Actor=c.Meta.Actor,Sequence=c.Meta.Sequence,ExpectedVersion=c.Meta.ExpectedVersion,Actions=Array.Empty<ActionDto>() };
            if(c is DrawCommand draw) dto.Source=(int)draw.Source;
            else if(c is CommitTurnCommand commit) { dto.Actions=commit.Plan.Actions.Select(Action).ToArray(); dto.Discard=commit.Plan.Discard.Value; dto.AcceptPenalty=commit.Plan.AcceptPenalty; }
            else throw new SerializationException("Unknown command.");
            return dto;
        }
        private static GameCommand Command(CommandDto c)
        {
            var meta=new CommandMeta(Guid.Parse(c.RoundId),c.Actor,c.Sequence,c.ExpectedVersion);
            var actions=Items(c.Actions,106);
            switch(c.Kind)
            {
                case "Draw": if(actions.Length!=0) throw new SerializationException("Draw cannot carry actions."); return new DrawCommand(meta,(DrawSource)c.Source);
                case "Commit": return new CommitTurnCommand(meta,new TurnPlan(actions.Select(Action).ToArray(),new TileId(c.Discard),c.AcceptPenalty));
                default: throw new SerializationException("Unknown command discriminant.");
            }
        }
        private static ActionDto Action(TableAction a)
        {
            var dto=new ActionDto { Groups=Array.Empty<GroupDto>(),Tiles=Array.Empty<PlacedDto>() };
            switch(a)
            {
                case OpenGroups open: dto.Kind="Open";dto.Opening=(int)open.Opening;dto.Groups=open.Groups.Select(Group).ToArray();break;
                case AddGroup add: dto.Kind="Add";dto.Groups=new[]{Group(add.Group)};break;
                case ExtendMeld extend: dto.Kind="Extend";dto.MeldId=extend.MeldId;dto.Prepend=extend.Prepend;dto.Tiles=extend.Tiles.Select(Placed).ToArray();break;
                case SwapJoker swap: dto.Kind="Swap";dto.MeldId=swap.MeldId;dto.JokerId=swap.JokerId.Value;dto.ReplacementId=swap.ReplacementId.Value;break;
                default: throw new SerializationException("Unknown action.");
            }
            return dto;
        }
        private static TableAction Action(ActionDto a)
        {
            var groups=Items(a.Groups,53);var tiles=Items(a.Tiles,13);
            switch(a.Kind)
            {
                case "Open": return new OpenGroups((OpeningKind)a.Opening,groups.Select(Group).ToArray());
                case "Add": if(groups.Length!=1) throw new SerializationException("Add requires one group.");return new AddGroup(Group(groups[0]));
                case "Extend": return new ExtendMeld(a.MeldId,a.Prepend,tiles.Select(Placed).ToArray());
                case "Swap": return new SwapJoker(a.MeldId,new TileId(a.JokerId),new TileId(a.ReplacementId));
                default: throw new SerializationException("Unknown action discriminant.");
            }
        }
    }
}
