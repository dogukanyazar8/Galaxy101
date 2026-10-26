using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>
    /// The ranked league art of g101-rank.js: league table (Res/Rank/ranks.json "leagues"), G101Emblem (the league emblem
    /// PNGs of the package, Res/Rank) and G101Ring (the league avatar frame, rasterised from ring() by
    /// docs/v4/menu/tools/league_rings.py into Res/UI/Rank). Avatar frames follow G101-Menu frameOf()/frameOpts().
    /// </summary>
    public sealed partial class MenuView
    {
        private static JArray leagues;
        /// <summary>g101-rank.js LEAGUES: bronze, silver, gold, diamond, master, galaxy (key, tr, en, metal, text, bar, glow).</summary>
        public static JArray Leagues
        {
            get
            {
                if(leagues!=null)return leagues;var asset=Resources.Load<TextAsset>("Galaxy101/Rank/ranks");
                if(asset==null)throw new InvalidOperationException("Missing league table: Galaxy101/Rank/ranks");
                return leagues=(JArray)JObject.Parse(asset.text)["leagues"];
            }
        }
        /// <summary>g101-rank.js ROMAN.</summary>
        public static readonly string[] Roman={"","I","II","III"};
        /// <summary>The prototype's ranked sample (G101-Menu renderVals: props.ranked ?? {idx 7, lp 72, peak 7}) — Altın II.</summary>
        public const int SampleRankIndex=7,SampleRankLp=72,SampleRankPeak=7;
        /// <summary>Step index 0–15 (G101Rank.at) of a league code b3…m1, gx (content.json leaderboard.leagues).</summary>
        public static int RankIndex(string code)
        {
            if(string.IsNullOrEmpty(code))return SampleRankIndex;if(code=="gx")return 15;
            int league="bsgdm".IndexOf(code[0]);int tier=code.Length>1&&char.IsDigit(code[1])?code[1]-'0':3;
            return league<0?SampleRankIndex:league*3+Mathf.Clamp(3-tier,0,2);
        }
        /// <summary>G101Rank.at(idx): the league and tier (3 = III … 1 = I; Galaksi 0).</summary>
        public static (JObject League,int Tier) RankAt(int idx)=>idx>=15?((JObject)Leagues[5],0):((JObject)Leagues[Mathf.Clamp(idx,0,14)/3],3-Mathf.Clamp(idx,0,14)%3);
        /// <summary>G101Rank.label(idx, lang): "Altın II", "Galaksi".</summary>
        public string RankLabel(int idx){var (league,tier)=RankAt(idx);return (string)league[Loc.Language=="en"?"en":"tr"]+(tier>0?" "+Roman[tier]:"");}
        /// <summary>The league's name colour (ranks.json leagues[].text = g101-rank.js txt).</summary>
        public static string RankInk(int idx)=>(string)RankAt(idx).League["text"];
        /// <summary>
        /// G101Emblem league="key" tier size: the package PNG (sizes 24 / 52 / 160, the 24 one is the simplified small
        /// art the SVG draws under 40 px) at 2×, scaled to the box.
        /// </summary>
        public VisualElement Emblem(string key,int tier,int size)
        {
            int art=size<40?24:size<=80?52:160;string name="Galaxy101/Rank/"+key+(tier>0&&key!="galaxy"?"_"+Roman[tier]:"")+"_"+art+"@2x";
            var texture=Resources.Load<Texture2D>(name);if(texture==null)throw new InvalidOperationException("Missing league emblem: "+name);
            var emblem=new VisualElement{name="league-emblem",pickingMode=PickingMode.Ignore};emblem.style.width=emblem.style.height=size;emblem.style.flexShrink=0;
            emblem.style.backgroundImage=new StyleBackground(texture);return emblem;
        }
        /// <summary>Emblem of a step index (G101Emblem league="{{ rk.key }}" tier="{{ rk.tier }}").</summary>
        public VisualElement Emblem(int idx,int size){var (league,tier)=RankAt(idx);return Emblem((string)league["key"],tier,size);}
        /// <summary>
        /// G101Ring league size: the notched metal ring (position:absolute; inset:0; overflow visible — its halo reaches
        /// past the box, so the image is padded by ceil(size · 10.88 / 200) px on every side).
        /// </summary>
        public VisualElement LeagueRing(string key,int size)
        {
            string name="Galaxy101/UI/Rank/ring_"+key+"_"+size+"@2x";var texture=Resources.Load<Texture2D>(name);
            if(texture==null)throw new InvalidOperationException("Missing league ring: "+name);
            int pad=Mathf.CeilToInt(size*(99*1.12f-100)/200);
            var ring=new VisualElement{name="league-ring",pickingMode=PickingMode.Ignore};Place(ring,-pad,-pad,size+2*pad,size+2*pad);ring.style.backgroundImage=new StyleBackground(texture);return ring;
        }
        /// <summary>The v4 frame ids (frameOpts ids): 0 Klasik, 10–15 the league frames; a saved 1 is the Altın league frame (frameOf: i === 1 → 12).</summary>
        public static readonly int[] FrameIds={0,10,11,12,13,14,15};
        public static int FrameId(int saved)=>saved==1?12:saved;
        /// <summary>The league key of a frame id, or null for Klasik.</summary>
        public static string FrameLeague(int frame){frame=FrameId(frame);return frame>=10&&frame<=15?(string)Leagues[frame-10]["key"]:null;}
        /// <summary>
        /// frameOpts(): a league frame is locked above the peak league reached in ranked (peakL = floor(min(peak, 15) / 3)).
        /// </summary>
        public static bool FrameLocked(int frame,int peakIndex){frame=FrameId(frame);return frame>=10&&frame-10>Mathf.Min(peakIndex,15)/3;}
        /// <summary>The highest step reached this season: the account's best league, else the prototype's sample (peak 7).</summary>
        public int PeakRankIndex(){var stats=Services.Profile.GetStats();var best=(string)stats?["bestLeague"]??(string)stats?["league"];return best==null?SampleRankPeak:RankIndex(best);}
        /// <summary>The account's step index: its league, else the prototype's sample (idx 7, Altın II).</summary>
        public int CurrentRankIndex(){var league=(string)Services.Profile.GetStats()?["league"];return league==null?SampleRankIndex:RankIndex(league);}
        /// <summary>
        /// frameOf(i) for one avatar: the face under the ring (league: transparent with "0 0 16px glow"; Klasik: the conic
        /// a1/dp face) and the frame's gap colour (league: metal[3]; Klasik: --sd, or --pn for gapS on the top bar).
        /// </summary>
        public (string Face,Color Glow,string Gap,string League) FrameLook(int frame,bool topBar=false)
        {
            string league=FrameLeague(frame);
            if(league==null)return ("conic-gradient(var(--a1) 0 245deg,var(--dp) 245deg)",Color.clear,topBar?"var(--pn)":"var(--sd)",null);
            var data=(JObject)Leagues[FrameId(frame)-10];return ("transparent",C((string)data["glow"]),(string)data["metal"][3],league);
        }
    }
}
