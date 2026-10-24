using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>Retains the supplied SVG geometry as native vector UI, without a web runtime.</summary>
    public sealed class GalaxyIcon : VisualElement
    {
        private readonly XElement svg;
        private readonly Color tint;
        private static readonly Dictionary<string,XElement> cache=new Dictionary<string,XElement>();
        public GalaxyIcon(string name,Color color)
        {
            tint=color;pickingMode=PickingMode.Ignore;
            if(!cache.TryGetValue(name,out svg))
            {
                var source=Resources.Load<TextAsset>("Galaxy101/Icons/"+name);
                if(source==null)throw new InvalidOperationException("Missing supplied icon: "+name);
                using(var reader=XmlReader.Create(new StringReader(source.text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))svg=XElement.Load(reader);
                cache.Add(name,svg);
            }
            generateVisualContent+=Draw;
        }
        private void Draw(MeshGenerationContext context)
        {
            var painter=context.painter2D;var box=((string)svg.Attribute("viewBox")??"0 0 24 24").Split(' ');
            float scale=Mathf.Min(contentRect.width/float.Parse(box[2],CultureInfo.InvariantCulture),contentRect.height/float.Parse(box[3],CultureInfo.InvariantCulture));
            var offset=contentRect.position+(contentRect.size-new Vector2(float.Parse(box[2],CultureInfo.InvariantCulture),float.Parse(box[3],CultureInfo.InvariantCulture))*scale)*.5f-new Vector2(float.Parse(box[0],CultureInfo.InvariantCulture),float.Parse(box[1],CultureInfo.InvariantCulture))*scale;
            foreach(var shape in svg.Elements())
            {
                string cap=Property(shape,"stroke-linecap",Property(svg,"stroke-linecap","butt")),join=Property(shape,"stroke-linejoin",Property(svg,"stroke-linejoin","miter"));
                painter.lineCap=cap=="round"?LineCap.Round:LineCap.Butt;painter.lineJoin=join=="round"?LineJoin.Round:join=="bevel"?LineJoin.Bevel:LineJoin.Miter;
                painter.miterLimit=F(shape,"stroke-miterlimit",F(svg,"stroke-miterlimit",4));
                painter.BeginPath();string type=shape.Name.LocalName;
                if(type=="path")Path(painter,(string)shape.Attribute("d")??"",scale,offset);
                else if(type=="circle")painter.Arc(new Vector2(F(shape,"cx"),F(shape,"cy"))*scale+offset,F(shape,"r")*scale,Angle.Degrees(0),Angle.Degrees(360));
                else if(type=="rect")
                {
                    float x=F(shape,"x"),y=F(shape,"y"),w=F(shape,"width"),h=F(shape,"height"),r=Mathf.Min(F(shape,"rx"),Mathf.Min(w,h)*.5f),k=.5523f*r;
                    // Quarter circles as cubic Béziers: Painter2D.Arc strays at small radii (a padlock grew "legs").
                    Vector2 P(float px,float py)=>new Vector2(px,py)*scale+offset;
                    painter.MoveTo(P(x+r,y));painter.LineTo(P(x+w-r,y));painter.BezierCurveTo(P(x+w-r+k,y),P(x+w,y+r-k),P(x+w,y+r));
                    painter.LineTo(P(x+w,y+h-r));painter.BezierCurveTo(P(x+w,y+h-r+k),P(x+w-r+k,y+h),P(x+w-r,y+h));
                    painter.LineTo(P(x+r,y+h));painter.BezierCurveTo(P(x+r-k,y+h),P(x,y+h-r+k),P(x,y+h-r));
                    painter.LineTo(P(x,y+r));painter.BezierCurveTo(P(x,y+r-k),P(x+r-k,y),P(x+r,y));painter.ClosePath();
                }
                else continue;
                string fill=Property(shape,"fill",Property(svg,"fill","black"));
                float opacity=F(svg,"opacity",1)*F(shape,"opacity",1);
                if(fill!="none"){var color=Paint(fill);color.a*=opacity*F(shape,"fill-opacity",F(svg,"fill-opacity",1));painter.fillColor=color;painter.Fill(Property(shape,"fill-rule",Property(svg,"fill-rule","nonzero"))=="evenodd"?FillRule.OddEven:FillRule.NonZero);}
                string stroke=Property(shape,"stroke",Property(svg,"stroke","none"));
                if(stroke!="none")
                {
                    // Root stroke is the icon's contextual color. Per-shape paints remain literal SVG colors.
                    var color=Property(shape,"stroke",null)==null?tint:Paint(stroke);color.a*=opacity*F(shape,"stroke-opacity",F(svg,"stroke-opacity",1));
                    painter.strokeColor=color;painter.lineWidth=F(shape,"stroke-width",F(svg,"stroke-width",1))*scale;painter.Stroke();
                }
            }
        }
        private Color Paint(string value)
        {
            if(value.StartsWith("rgba",StringComparison.Ordinal))
            {var parts=value.Substring(value.IndexOf('(')+1).TrimEnd(')').Split(',');return new Color(float.Parse(parts[0],CultureInfo.InvariantCulture)/255,float.Parse(parts[1],CultureInfo.InvariantCulture)/255,float.Parse(parts[2],CultureInfo.InvariantCulture)/255,parts.Length==4?float.Parse(parts[3],CultureInfo.InvariantCulture):1);}
            if(value=="currentColor"||value.StartsWith("var(",StringComparison.Ordinal))return tint;
            if(value=="transparent")return Color.clear;
            if(ColorUtility.TryParseHtmlString(value,out var literal))return literal;
            throw new InvalidOperationException("Unsupported supplied SVG paint: "+value);
        }
        private static string Property(XElement element,string property,string fallback)
        {
            string value=(string)element.Attribute(property);if(value!=null)return value;
            var style=Regex.Match((string)element.Attribute("style")??"",@"(?:^|;)\s*"+Regex.Escape(property)+@"\s*:\s*([^;]+)");return style.Success?style.Groups[1].Value.Trim():fallback;
        }
        private static float F(XElement element,string property,float fallback=0)=>float.TryParse((string)element.Attribute(property),NumberStyles.Float,CultureInfo.InvariantCulture,out float value)?value:fallback;
        private static void Path(Painter2D p,string source,float scale,Vector2 offset)
        {
            var tokens=Regex.Matches(source,@"[a-zA-Z]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?").Cast<Match>().Select(x=>x.Value).ToArray();
            int i=0;char command='M',previous=' ';Vector2 point=Vector2.zero,start=Vector2.zero,control=Vector2.zero;
            Func<float> read=()=>float.Parse(tokens[i++],CultureInfo.InvariantCulture);
            while(i<tokens.Length)
            {
                if(char.IsLetter(tokens[i][0]))command=tokens[i++][0];bool relative=char.IsLower(command);char kind=char.ToUpperInvariant(command);Vector2 origin=relative?point:Vector2.zero;
                if(kind=='Z'){p.ClosePath();point=start;previous=command;continue;}
                switch(kind)
                {
                    case 'M':point=origin+new Vector2(read(),read());p.MoveTo(point*scale+offset);start=point;command=relative?'l':'L';break;
                    case 'L':point=origin+new Vector2(read(),read());p.LineTo(point*scale+offset);break;
                    case 'H':point=new Vector2(read()+(relative?point.x:0),point.y);p.LineTo(point*scale+offset);break;
                    case 'V':point=new Vector2(point.x,read()+(relative?point.y:0));p.LineTo(point*scale+offset);break;
                    case 'C':{var a=origin+new Vector2(read(),read());var b=origin+new Vector2(read(),read());var end=origin+new Vector2(read(),read());p.BezierCurveTo(a*scale+offset,b*scale+offset,end*scale+offset);point=end;control=b;break;}
                    case 'S':{var a=char.ToUpperInvariant(previous)=='C'||char.ToUpperInvariant(previous)=='S'?2*point-control:point;var b=origin+new Vector2(read(),read());var end=origin+new Vector2(read(),read());p.BezierCurveTo(a*scale+offset,b*scale+offset,end*scale+offset);point=end;control=b;break;}
                    case 'Q':{var a=origin+new Vector2(read(),read());var end=origin+new Vector2(read(),read());p.QuadraticCurveTo(a*scale+offset,end*scale+offset);point=end;control=a;break;}
                    case 'A':{float rx=read(),ry=read(),angle=read();bool large=read()!=0,sweep=read()!=0;var end=origin+new Vector2(read(),read());Arc(p,point,end,rx,ry,angle,large,sweep,scale,offset);point=end;break;}
                    default:throw new InvalidOperationException("Unsupported supplied SVG path command: "+command);
                }
                previous=command;
            }
        }
        private static void Arc(Painter2D painter,Vector2 from,Vector2 to,float rx,float ry,float angle,bool large,bool sweep,float scale,Vector2 offset)
        {
            if(from==to)return;
            rx=Mathf.Abs(rx);ry=Mathf.Abs(ry);if(rx==0||ry==0){painter.LineTo(to*scale+offset);return;}
            if(!Mathf.Approximately(rx,ry))throw new InvalidOperationException("The supplied icon renderer requires circular SVG arcs; an elliptical arc needs native SVG import.");
            var arc=CalculateCircularArc(from,to,rx,angle,large,sweep);
            // Painter2D.Arc mis-sweeps counterclockwise SVG arcs (stray loops at small corner radii), so emit
            // the exact span as cubic segments of at most 90°, ending precisely on the SVG endpoint.
            float span=arc.EndRadians-arc.StartRadians;int pieces=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(span)/(Mathf.PI*.5f)-1e-4f));
            float step=span/pieces,handle=4f/3f*Mathf.Tan(step/4);
            Vector2 At(float a)=>arc.Center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*arc.Radius;
            Vector2 Tangent(float a)=>new Vector2(-Mathf.Sin(a),Mathf.Cos(a))*arc.Radius;
            for(int i=0;i<pieces;i++)
            {
                float a0=arc.StartRadians+step*i,a1=a0+step;var p0=At(a0);var p1=i==pieces-1?to:At(a1);
                painter.BezierCurveTo((p0+Tangent(a0)*handle)*scale+offset,(p1-Tangent(a1)*handle)*scale+offset,p1*scale+offset);
            }
        }
        private readonly struct CircularArc
        {
            public readonly Vector2 Center;
            public readonly float Radius,StartRadians,EndRadians;
            public CircularArc(Vector2 center,float radius,float start,float end){Center=center;Radius=radius;StartRadians=start;EndRadians=end;}
        }
        private static CircularArc CalculateCircularArc(Vector2 from,Vector2 to,float radius,float angle,bool large,bool sweep)
        {
            float rx=radius,ry=radius;
            float phi=angle*Mathf.Deg2Rad,c=Mathf.Cos(phi),s=Mathf.Sin(phi);var delta=(from-to)*.5f;
            float x=c*delta.x+s*delta.y,y=-s*delta.x+c*delta.y,ratio=x*x/(rx*rx)+y*y/(ry*ry);
            if(ratio>1){float factor=Mathf.Sqrt(ratio);rx*=factor;ry*=factor;}
            float coefficient=Mathf.Sqrt(Mathf.Max(0,(rx*rx*ry*ry-rx*rx*y*y-ry*ry*x*x)/(rx*rx*y*y+ry*ry*x*x)))*(large==sweep?-1:1);
            float cx=coefficient*rx*y/ry,cy=-coefficient*ry*x/rx;
            var center=new Vector2(c*cx-s*cy,s*cx+c*cy)+(from+to)*.5f;
            float first=Mathf.Atan2((y-cy)/ry,(x-cx)/rx),last=Mathf.Atan2((-y-cy)/ry,(-x-cx)/rx),span=last-first;
            if(!sweep&&span>0)span-=2*Mathf.PI;if(sweep&&span<0)span+=2*Mathf.PI;
            return new CircularArc(center,rx,first+phi,first+phi+span);
        }
    }
}
