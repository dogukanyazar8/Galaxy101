using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>Rasterizes supplied CSS gradients for native rounded UI Toolkit backgrounds.</summary>
    public sealed class GalaxySurface
    {
        private readonly MenuView view;
        private static readonly Regex ColorToken=new Regex(@"var\(--[A-Za-z0-9]+\)|#[0-9a-fA-F]{3,8}\b|rgba?\([^)]*\)|transparent|white|black",RegexOptions.Compiled);
        private static readonly Regex RadialPosition=new Regex(@"\bat\s+([-\d.]+)%\s+([-\d.]+)%",RegexOptions.Compiled);
        private static readonly Regex EllipseRadii=new Regex(@"\bellipse\s+([\d.]+)%\s+([\d.]+)%",RegexOptions.Compiled);
        public GalaxySurface(MenuView view){this.view=view;}
        public Texture2D CreateGradient(string css)=>CreateGradient(css,1,1);

        /// <summary>CSS positioning-box dimensions are needed for circular gradients on nonsquare elements.</summary>
        public Texture2D CreateGradient(string css,float width,float height)
        {
            if(width<=0||float.IsNaN(width)||float.IsInfinity(width))throw new ArgumentOutOfRangeException(nameof(width));
            if(height<=0||float.IsNaN(height)||float.IsInfinity(height))throw new ArgumentOutOfRangeException(nameof(height));
            const int size=128;
            var matches=ColorToken.Matches(css);var colors=new List<Color>();var positions=new List<float>();
            bool conic=css.StartsWith("conic",StringComparison.Ordinal),radial=css.StartsWith("radial",StringComparison.Ordinal);
            foreach(Match match in matches)
            {
                Color color=view.C(match.Value);string tail=css.Substring(match.Index+match.Length);int comma=tail.IndexOf(',');if(comma>=0)tail=tail.Substring(0,comma);
                var stops=Regex.Matches(tail,@"([\d.]+)(%|deg)?");
                if(stops.Count==0){colors.Add(color);positions.Add(-1);}
                else foreach(Match stop in stops)
                {colors.Add(color);positions.Add(float.Parse(stop.Groups[1].Value,CultureInfo.InvariantCulture)/(stop.Groups[2].Value=="deg"?360:stop.Groups[2].Value=="%"?100:1));}
            }
            if(colors.Count==0)throw new InvalidOperationException("No colors in reference gradient: "+css);
            if(positions[0]<0)positions[0]=0;if(positions[positions.Count-1]<0)positions[positions.Count-1]=1;
            for(int i=1;i<positions.Count-1;i++)
            {
                if(positions[i]>=0)continue;int next=i+1;while(next<positions.Count&&positions[next]<0)next++;
                float first=positions[i-1],last=positions[next];int span=next-i+1;
                for(int j=i;j<next;j++)positions[j]=Mathf.Lerp(first,last,(float)(j-i+1)/span);
                i=next-1;
            }
            var angle=Regex.Match(css,@"^linear-gradient\(\s*([-\d.]+)deg");float degrees=angle.Success?float.Parse(angle.Groups[1].Value,CultureInfo.InvariantCulture):180;
            Vector2 direction=new Vector2(Mathf.Sin(degrees*Mathf.Deg2Rad),-Mathf.Cos(degrees*Mathf.Deg2Rad));
            var center=new Vector2(.5f,.5f);var radii=Vector2.one;
            if(radial)
            {
                int opening=css.IndexOf('(');string geometry=css.Substring(opening+1,matches[0].Index-opening-1);
                var position=RadialPosition.Match(geometry);
                if(position.Success)center=new Vector2(Parse(position.Groups[1].Value)/100,Parse(position.Groups[2].Value)/100);
                float farX=Mathf.Max(Mathf.Abs(center.x),Mathf.Abs(1-center.x));
                float farY=Mathf.Max(Mathf.Abs(center.y),Mathf.Abs(1-center.y));
                if(Regex.IsMatch(geometry,@"\bcircle\b"))
                {
                    float radius=new Vector2(farX*width,farY*height).magnitude;
                    radii=new Vector2(radius/width,radius/height);
                }
                else
                {
                    var explicitRadii=EllipseRadii.Match(geometry);
                    // The default farthest-corner ellipse keeps the farthest-side aspect ratio.
                    // Its two normalized radii are sqrt(2) times their respective farthest sides.
                    radii=explicitRadii.Success
                        ?new Vector2(Parse(explicitRadii.Groups[1].Value)/100,Parse(explicitRadii.Groups[2].Value)/100)
                        :new Vector2(farX,farY)*Mathf.Sqrt(2);
                }
            }
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var point=new Vector2((float)x/(size-1),1f-(float)y/(size-1));float t;
                if(conic){t=Mathf.Repeat(Mathf.Atan2(point.x-.5f,.5f-point.y)/(2*Mathf.PI)+1,1);if(css.Contains("210deg"))t=Mathf.Repeat(t-210f/360,1);}
                else if(radial)t=new Vector2((point.x-center.x)/radii.x,(point.y-center.y)/radii.y).magnitude;
                else
                {
                    var offset=Vector2.Scale(point-new Vector2(.5f,.5f),new Vector2(width,height));
                    t=.5f+Vector2.Dot(offset,direction)/(Mathf.Abs(direction.x)*width+Mathf.Abs(direction.y)*height);
                }
                Color color=colors[colors.Count-1];
                if(t<=positions[0])color=colors[0];else for(int i=1;i<colors.Count;i++)if(t<=positions[i]){color=Color.Lerp(colors[i-1],colors[i],Mathf.InverseLerp(positions[i-1],positions[i],t));break;}
                pixels[y*size+x]=color;
            }
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Galaxy reference gradient",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            tex.SetPixels32(pixels);tex.Apply(false,true);return tex;
        }

        private static float Parse(string value)=>float.Parse(value,CultureInfo.InvariantCulture);
    }
}
