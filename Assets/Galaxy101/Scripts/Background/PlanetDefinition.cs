using System;
using UnityEngine;

namespace Galaxy101
{
    public sealed class PlanetDefinition
    {
        public readonly string Name, Resource;
        public readonly int Seed;
        public readonly bool Rings;
        public readonly float Tilt, Flattening, Limb, Ambient, AtmosphereStrength;
        public readonly Color Atmosphere, Bg1, Bg2, Bg3, Halo, GlowAtmosphere, Stars;

        private PlanetDefinition(string name, string resource, int seed, bool rings, float tilt, float flat, float limb,
            float ambient, Color atmosphere, float strength, string bg1, string bg2, string bg3, Color halo, Color glow, string stars)
        {
            Name=name;Resource=resource;Seed=seed;Rings=rings;Tilt=tilt;Flattening=flat;Limb=limb;Ambient=ambient;
            Atmosphere=atmosphere;AtmosphereStrength=strength;Bg1=Hex(bg1);Bg2=Hex(bg2);Bg3=Hex(bg3);Halo=halo;GlowAtmosphere=glow;Stars=Hex(stars);
        }
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString(value,out var c);return c; }
        private static Color Rgba(int r,int g,int b,float a=1) => new Color(r/255f,g/255f,b/255f,a);
        private static readonly PlanetDefinition[] All = {
            new PlanetDefinition("Satürn","saturn",5,true,22,1.1f,.45f,.02f,Rgba(255,222,175),.2f,"#2A2F6E","#161A45","#0A0C28",Rgba(255,214,140,.35f),Rgba(255,226,175,.55f),"#FFE7A3"),
            new PlanetDefinition("Mars","mars",23,false,14,1,.12f,.02f,Rgba(255,165,125),.42f,"#5A1F17","#2C0E0B","#140605",Rgba(255,110,60,.35f),Rgba(255,150,110,.45f),"#FFC9B0"),
            new PlanetDefinition("Neptün","neptune",37,false,18,1.02f,.32f,.02f,Rgba(120,185,255),.75f,"#0F3C80","#081F52","#03102E",Rgba(80,170,255,.4f),Rgba(150,210,255,.8f),"#CDEBFF"),
            new PlanetDefinition("Jüpiter","jupiter",11,false,6,1.07f,.4f,.02f,Rgba(255,226,190),.22f,"#4E3522","#26180E","#120A05",Rgba(255,190,130,.3f),Rgba(255,220,180,.5f),"#FFE2C2"),
            new PlanetDefinition("Venüs","venus",51,false,4,1,.28f,.03f,Rgba(255,240,212),.85f,"#5A2A4E","#2C1428","#140812",Rgba(255,200,170,.35f),Rgba(255,238,205,.9f),"#FFE0C8")
        };
        public static PlanetDefinition For(string name)
        {
            foreach(var p in All)
                if(string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase)||string.Equals(p.Resource,name,StringComparison.OrdinalIgnoreCase)) return p;
            return All[0];
        }
    }
}
