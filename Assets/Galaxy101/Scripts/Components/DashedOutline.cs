using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>Native stand-in for a CSS "1px dashed" rounded border; fills its parent's border box.</summary>
    public sealed class DashedOutline : VisualElement
    {
        private readonly float radius,width,dash,gap;
        private readonly Color color;

        public DashedOutline(float radius,float width,Color color,float dash=3,float gap=3)
        {
            this.radius=radius;this.width=width;this.color=color;this.dash=dash;this.gap=gap;
            pickingMode=PickingMode.Ignore;style.position=Position.Absolute;style.left=style.top=style.right=style.bottom=0;
            generateVisualContent+=Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            float w=contentRect.width,h=contentRect.height,inset=width*.5f;if(w<=0||h<=0)return;
            float r=Mathf.Min(radius-inset,Mathf.Min(w,h)*.5f-inset);
            float x0=inset,y0=inset,x1=w-inset,y1=h-inset,straightX=x1-x0-2*r,straightY=y1-y0-2*r,corner=Mathf.PI*.5f*r;
            float perimeter=2*straightX+2*straightY+4*corner;
            Vector2 At(float s)
            {
                s=Mathf.Repeat(s,perimeter);
                if(s<straightX)return new Vector2(x0+r+s,y0);s-=straightX;
                if(s<corner){float a=-Mathf.PI*.5f+s/r;return new Vector2(x1-r+Mathf.Cos(a)*r,y0+r+Mathf.Sin(a)*r);}s-=corner;
                if(s<straightY)return new Vector2(x1,y0+r+s);s-=straightY;
                if(s<corner){float a=s/r;return new Vector2(x1-r+Mathf.Cos(a)*r,y1-r+Mathf.Sin(a)*r);}s-=corner;
                if(s<straightX)return new Vector2(x1-r-s,y1);s-=straightX;
                if(s<corner){float a=Mathf.PI*.5f+s/r;return new Vector2(x0+r+Mathf.Cos(a)*r,y1-r+Mathf.Sin(a)*r);}s-=corner;
                if(s<straightY)return new Vector2(x0,y1-r-s);s-=straightY;
                {float a=Mathf.PI+s/r;return new Vector2(x0+r+Mathf.Cos(a)*r,y0+r+Mathf.Sin(a)*r);}
            }
            // Spread the remainder evenly so the dash pattern closes cleanly, as browsers do.
            int count=Mathf.Max(1,Mathf.RoundToInt(perimeter/(dash+gap)));float step=perimeter/count,length=step*dash/(dash+gap);
            var painter=context.painter2D;painter.strokeColor=color;painter.lineWidth=width;painter.lineCap=LineCap.Butt;painter.lineJoin=LineJoin.Round;
            for(int i=0;i<count;i++)
            {
                float start=i*step;painter.BeginPath();painter.MoveTo(At(start));
                for(float s=start+1;s<start+length;s+=1)painter.LineTo(At(s));
                painter.LineTo(At(start+length));painter.Stroke();
            }
        }
    }
}
