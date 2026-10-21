using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>Double-precision port of the reference gen(), including Float32 noise table quantization.</summary>
    public static class PlanetTextureBaker
    {
        private const string Folder="Assets/Galaxy101/Art/Planets/Resources/Galaxy101/Planets";
        private static readonly double[][] Jup={new[]{-1d,118,100,86},new[]{-.82,150,126,102},new[]{-.62,202,178,148},new[]{-.48,176,132,96},new[]{-.34,228,208,184},new[]{-.22,190,140,100},new[]{-.12,238,224,204},new[]{-.04,214,184,150},new[]{.05,242,230,212},new[]{.14,176,120,82},new[]{.24,204,154,114},new[]{.33,234,218,194},new[]{.45,186,146,112},new[]{.6,216,196,172},new[]{.8,160,136,112},new[]{1d,122,104,90}};
        private static readonly double[][] Sat={new[]{-1d,96,112,126},new[]{-.9,116,128,136},new[]{-.8,150,146,130},new[]{-.7,182,164,124},new[]{-.6,212,190,142},new[]{-.52,186,158,110},new[]{-.44,224,204,154},new[]{-.34,192,160,104},new[]{-.26,230,210,160},new[]{-.14,246,232,188},new[]{0d,250,238,196},new[]{.1,240,222,172},new[]{.17,196,162,104},new[]{.24,230,206,152},new[]{.32,206,176,120},new[]{.42,228,208,160},new[]{.52,194,170,124},new[]{.62,214,196,154},new[]{.72,184,172,138},new[]{.82,156,156,140},new[]{.91,128,136,136},new[]{1d,108,118,124}};
        private static readonly double[][] Mars={new[]{0d,70,32,22},new[]{.34,96,46,30},new[]{.45,146,70,40},new[]{.56,184,96,56},new[]{.72,206,124,78},new[]{1d,224,156,112}};
        private static readonly double[][] Nep={new[]{-1d,36,56,142},new[]{-.6,46,76,176},new[]{-.3,56,96,206},new[]{-.1,62,108,220},new[]{0d,66,114,226},new[]{.15,58,98,206},new[]{.4,50,84,190},new[]{.7,44,70,168},new[]{1d,38,58,148}};
        private static readonly double[][] Ven={new[]{0d,206,168,110},new[]{.38,226,196,140},new[]{.62,240,221,174},new[]{1d,250,241,212}};

        [MenuItem("Galaxy 101/Bake Planet Textures (2048)")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(Folder);
            foreach(var name in new[]{"Satürn","Mars","Neptün","Jüpiter","Venüs"})
            {
                const int width=2048,height=1024;
                var pixels=GeneratePixels(name,width);var flipped=new Color32[pixels.Length];
                for(int y=0;y<height;y++) Array.Copy(pixels,y*width,flipped,(height-1-y)*width,width);
                var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,true);
                try {texture.SetPixels32(flipped);texture.Apply();File.WriteAllBytes(Folder+"/"+PlanetDefinition.For(name).Resource+".png",texture.EncodeToPNG());}
                finally {UnityEngine.Object.DestroyImmediate(texture);}
            }
            AssetDatabase.Refresh();
        }

        /// <returns>Top-to-bottom pixels, same indexing and rounding as JS Uint8ClampedArray.</returns>
        public static Color32[] GeneratePixels(string name,int width)
        {
            if(width<2||width>4096||(width&1)!=0) throw new ArgumentOutOfRangeException(nameof(width));
            var planet=PlanetDefinition.For(name);var a=new Noise(planet.Seed);var b=new Noise(planet.Seed+77);
            int height=width/2;var pixels=new Color32[width*height];var c=new double[3];
            for(int j=0;j<height;j++)
            {
                double lat=(.5-(j+.5)/height)*Math.PI,cl=Math.Cos(lat),y=Math.Sin(lat);
                for(int i=0;i<width;i++)
                {
                    double lon=(i+.5)/width*2*Math.PI-Math.PI,x=cl*Math.Sin(lon),z=cl*Math.Cos(lon);
                    Generate(planet.Resource,a,b,x,y,z,lat,lon,c);
                    pixels[j*width+i]=new Color32(Byte(c[0]),Byte(c[1]),Byte(c[2]),255);
                }
            }
            return pixels;
        }
        private static byte Byte(double value) => (byte)Math.Round(Math.Max(0,Math.Min(255,value)),MidpointRounding.ToEven);
        private static double Clamp(double v) => Math.Max(0,Math.Min(1,v));
        private static double Smooth(double a,double b,double v) {double t=Clamp((v-a)/(b-a));return t*t*(3-2*t);}
        private static double Contrast(double f,double k) => Clamp((f-.5)*k+.5);
        private static double Wrap(double angle) {angle=(angle+Math.PI)%(2*Math.PI);if(angle<0) angle+=2*Math.PI;return angle-Math.PI;}
        private static void Mix(double[] c,double r,double g,double b,double t) {c[0]+=(r-c[0])*t;c[1]+=(g-c[1])*t;c[2]+=(b-c[2])*t;}
        private static void Multiply(double[] c,double d) {for(int i=0;i<3;i++) c[i]*=d;}
        private static void Ramp(double[][] table,double t,double[] color)
        {
            if(t<=table[0][0]) {Array.Copy(table[0],1,color,0,3);return;}
            for(int i=1;i<table.Length;i++) if(t<=table[i][0])
            {
                var a=table[i-1];var b=table[i];double f=(t-a[0])/(b[0]-a[0]);
                for(int j=0;j<3;j++) color[j]=a[j+1]+(b[j+1]-a[j+1])*f;return;
            }
            Array.Copy(table[table.Length-1],1,color,0,3);
        }
        private static void Generate(string name,Noise a,Noise b,double x,double y,double z,double lat,double lon,double[] c)
        {
            switch(name)
            {
                case "jupiter":
                    Ramp(Jup,y+.055*(a.F(x*2.5,y*7,z*2.5,3)-.5)+.02*(b.F(x*12,y*36,z*12,2)-.5),c);
                    Multiply(c,.84+.32*b.F(x*5,y*24,z*5,3));
                    double dx=Wrap(lon-1.2)*Math.Cos(lat)/.2,dy=(lat+.38)/.1,dd=dx*dx+dy*dy;
                    if(dd<2.4) {double g=.8+.4*a.F(x*18,y*18,z*18,2);Mix(c,200*g,102*g,66*g,Smooth(1.15,.3,dd));Mix(c,240,224,204,Smooth(.6,.95,dd)*Smooth(1.7,1.05,dd)*.55);}
                    break;
                case "saturn":
                    Ramp(Sat,y+.022*(a.F(x*3,y*9,z*3,3)-.5),c);Multiply(c,.92+.16*b.F(x*4,y*60,z*4,2));
                    double sp=Contrast(a.F(x*9+3,y*30,z*9,2),2.4);
                    if(y< -.3&&y> -.42) Mix(c,250,240,214,Smooth(.74,.9,sp)*.5);
                    double co=Math.PI/2-lat;
                    if(co<.5)
                    {
                        double seg=Math.PI/3,ang=((lon%seg)+seg)%seg-seg/2,rh=.3/Math.Cos(ang),e=co/rh;
                        Mix(c,96,112,124,Smooth(1.08,.9,e)*.45);Mix(c,200,192,168,Smooth(.09,0,Math.Abs(e-1))*.18);Mix(c,78,92,110,Smooth(.3,0,co)*.4);
                    }
                    break;
                case "mars":
                    Ramp(Mars,Contrast(a.F(x*1.6,y*1.6,z*1.6,5),2.6),c);Multiply(c,.84+.32*b.F(x*8,y*8,z*8,3));
                    double q=Math.Abs(y)+.06*(b.F(x*5,y*5,z*5,2)-.5);Mix(c,240,236,230,(y>0?Smooth(.92,.955,q):Smooth(.95,.975,q))*.92);
                    break;
                case "neptune":
                    Ramp(Nep,y+.03*(a.F(x*3,y*8,z*3,3)-.5),c);Multiply(c,.92+.16*b.F(x*4,y*20,z*4,2));
                    double ci=Contrast(b.F(x*2.5+7,y*28,z*2.5,4),2.2);Mix(c,226,236,255,Smooth(.7,.9,ci)*(.2+.8*Math.Abs(Math.Sin(y*7)))*.55);
                    double dl=Wrap(lon-2.6),cosLat=Math.Cos(lat),nx=dl*cosLat/.16,ny=(lat+.35)/.085,nd=nx*nx+ny*ny;
                    if(nd<2) Mix(c,22,36,104,Smooth(1.1,.3,nd)*.9);
                    double ex=dl*cosLat/.13,ey=(lat+.46)/.028,ee=ex*ex+ey*ey;if(ee<1.2) Mix(c,236,244,255,Smooth(1,0,ee)*.85);
                    break;
                case "venus":
                    double wx=a.F(x*1.4,y*1.4,z*1.4,3)-.5;Ramp(Ven,Contrast(b.F(x*2+wx*1.6,y*4.5+wx*1.2,z*2+wx*1.6,4),2.1),c);
                    break;
                default:throw new ArgumentException("Unknown planet texture: "+name,nameof(name));
            }
        }
        private sealed class Noise
        {
            private uint state;
            private readonly int[] p=new int[512];private readonly float[] values=new float[256];
            private double Random() {state=unchecked(state*1664525u+1013904223u);return state/4294967296.0;}
            public Noise(int seed)
            {
                state=(uint)seed;if(state==0) state=1;var a=new int[256];for(int i=0;i<256;i++) a[i]=i;
                for(int i=255;i>0;i--) {int j=(int)Math.Floor(Random()*(i+1));int t=a[i];a[i]=a[j];a[j]=t;}
                for(int i=0;i<512;i++) p[i]=a[i&255];for(int i=0;i<256;i++) values[i]=(float)Random();
            }
            public double F(double x,double y,double z,int octaves)
            {
                double sum=0,amp=.5,norm=0;for(int i=0;i<octaves;i++) {sum+=amp*N(x,y,z);norm+=amp;amp*=.5;x*=2.03;y*=2.03;z*=2.03;}return sum/norm;
            }
            private double N(double x,double y,double z)
            {
                int xi=(int)Math.Floor(x),yi=(int)Math.Floor(y),zi=(int)Math.Floor(z),xx=xi&255,yy=yi&255,zz=zi&255,x1=(xx+1)&255,y1=(yy+1)&255,z1=(zz+1)&255;
                double xf=x-xi,yf=y-yi,zf=z-zi,u=xf*xf*(3-2*xf),w=yf*yf*(3-2*yf),t=zf*zf*(3-2*zf);
                int a00=p[p[xx]+yy],a01=p[p[xx]+y1],a10=p[p[x1]+yy],a11=p[p[x1]+y1];
                // Promote each Float32 value before subtraction to match JavaScript arithmetic.
                double c000=values[p[a00+zz]],c001=values[p[a00+z1]],c010=values[p[a01+zz]],c011=values[p[a01+z1]],c100=values[p[a10+zz]],c101=values[p[a10+z1]],c110=values[p[a11+zz]],c111=values[p[a11+z1]];
                double x00=c000+(c100-c000)*u,x01=c001+(c101-c001)*u,x10=c010+(c110-c010)*u,x11=c011+(c111-c011)*u,y0=x00+(x10-x00)*w,yOut=x01+(x11-x01)*w;
                return y0+(yOut-y0)*t;
            }
        }
    }

    internal sealed class PlanetTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Galaxy101/Art/Planets/",StringComparison.Ordinal)) return;
            var importer=(TextureImporter)assetImporter;importer.textureType=TextureImporterType.Default;
            importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.wrapModeU=TextureWrapMode.Repeat;importer.wrapModeV=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.isReadable=false;
        }
        [MenuItem("Galaxy 101/Refresh Planet Texture Imports")]
        public static void RefreshImports()
        {
            foreach(var path in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Galaxy101/Art/Planets"})) AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(path),ImportAssetOptions.ForceUpdate);
        }
    }
}
