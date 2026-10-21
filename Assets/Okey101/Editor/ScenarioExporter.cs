using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Okey101.Application;
using Okey101.Core.Model;
using Okey101.Core.Turns;
using UnityEditor;
using UnityEngine;

namespace Okey101.Editor
{
    public static class ScenarioExporter
    {
        public const string Root = "Assets/Okey101/Tests/Fixtures/Scenarios";
        public static readonly string[] Names = { "series-opening", "pair-opening", "joker-swap", "finish-x8", "stock-end", "four-pairs" };
        [MenuItem("Okey 101/Export test scenarios")]
        public static void Export()
        {
            Directory.CreateDirectory(Root); var codec = new ReplayCodec(Root);
            for(var i=0;i<Names.Length;i++)
            {
                var state = Create(i); var record = new ReplayRecord(state, Array.Empty<GameCommand>(), ReplayCodec.Hash(state));
                var temporary = codec.NewFileName(); codec.Write(record, temporary);
                var source = Path.Combine(Root, temporary); var target = Path.Combine(Root, Names[i] + ".json");
                try
                {
                    if (File.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new IOException("Scenario asset cannot be a link.");
                    if (!File.Exists(target) || !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(target))) File.Copy(source, target, true);
                }
                finally { File.Delete(source); }
            }
            AssetDatabase.Refresh(); Debug.Log("T10_SCENARIOS_READY count=6");
        }
        private static RoundState Create(int index)
        {
            RoundState original;
            if(index<3) original=PracticeDeals.Create(new[]{"Series","Pairs","Swap"}[index]);
            else
            {
                var indicator=Id(TileColor.Black,13); var table=new List<Meld>();
                var openings=new OpeningKind[4]; TileId[] hand;
                if(index==4) hand=new[]{Id(TileColor.Red,1),Id(TileColor.Black,12)};
                else
                {
                    hand=Enumerable.Range(1,5).SelectMany(n=>new[]{Id(TileColor.Red,n),Id(TileColor.Red,n,1)})
                        .Concat(index==3?new[]{Id(TileColor.Black,1)}:new[]{Id(TileColor.Black,12),Id(TileColor.Blue,8)}).ToArray();
                    if(index==5)
                        for(var p=1;p<4;p++)
                        {
                            openings[p]=OpeningKind.Pairs;
                            for(var n=1;n<=5;n++)
                            {
                                var number=p==3?n+1:n; var face=new TileFace((TileColor)p,number);
                                table.Add(new Meld(table.Count,p,new MeldProposal(MeldKind.Pair,new[]{new PlacedTile(Id((TileColor)p,number),face),new PlacedTile(Id((TileColor)p,number,1),face)})));
                            }
                        }
                }
                var used=new HashSet<TileId>(hand.Concat(new[]{indicator}).Concat(table.SelectMany(m=>m.Group.Tiles.Select(t=>t.Id))));
                var remaining=TileCatalog.Create().Select(t=>t.Id).Where(id=>!used.Contains(id)).ToArray();
                var players=new PlayerState[4]; players[0]=new PlayerState(hand,OpeningKind.None,0);
                for(var p=1;p<4;p++) players[p]=new PlayerState(new[]{remaining[p-1]},openings[p],0);
                var discards=Enumerable.Range(0,4).Select(_=>(IReadOnlyList<TileId>)Array.Empty<TileId>()).ToArray();
                if(index==4) discards[0]=remaining.Skip(3).ToArray();
                original=new RoundState(Guid.NewGuid(),1,71,0,0,1,RoundPhase.PlanningTurn,indicator,index==4?Array.Empty<TileId>():remaining.Skip(3).ToArray(),players,table,discards,
                    new DrawReceipt(DrawSource.Stock,hand[0],index==4),OpeningKind.None,0,false,new long[4]);
            }
            var stableId=Guid.Parse("10100000-0000-0000-0000-"+(index+1).ToString("D12",System.Globalization.CultureInfo.InvariantCulture));
            var state=new RoundState(stableId,original.Version,original.Seed,original.StartingPlayer,original.ActivePlayer,original.TurnNumber,original.Phase,original.Indicator,original.Stock,original.Players,
                original.Table,original.Discards,original.PendingDraw,original.OpeningAtTurnStart,original.SeriesTilesUsed,original.IsInitialTurn,original.LastAcceptedSequences,original.Result);
            if(!StateInvariant.ValidateAuthority(state).Success) throw new InvalidOperationException("Generated scenario violates authority: "+Names[index]);
            return state;
        }
        private static TileId Id(TileColor color,int number,int copy=0)=>new TileId(2*(13*(int)color+number-1)+copy);
    }
}
