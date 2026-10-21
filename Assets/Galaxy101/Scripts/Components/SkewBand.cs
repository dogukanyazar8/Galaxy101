using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>Native equivalent of a CSS shine strip: a box with skewX applied around its own center.</summary>
    public sealed class SkewBand : VisualElement
    {
        private float left,width,top,bottom,skew,alpha;
        private readonly Color color;
        private readonly bool feathered;

        /// <param name="feathered">Uses linear-gradient(90deg,transparent,color,transparent) instead of a flat fill.</param>
        public SkewBand(float width,float top,float bottom,float skewDegrees,Color color,bool feathered)
        {
            this.width=width;this.top=top;this.bottom=bottom;skew=skewDegrees;this.color=color;this.feathered=feathered;alpha=1;
            pickingMode=PickingMode.Ignore;style.position=Position.Absolute;style.left=style.top=style.right=style.bottom=0;
            generateVisualContent+=Draw;
        }
        /// <summary>Left edge of the unskewed box, relative to the parent's padding box.</summary>
        public float Left{get=>left;set{if(left==value)return;left=value;MarkDirtyRepaint();}}
        public float Opacity{get=>alpha;set{if(alpha==value)return;alpha=value;MarkDirtyRepaint();}}

        private void Draw(MeshGenerationContext context)
        {
            if(alpha<=0)return;
            float y0=top,y1=contentRect.height-bottom,centerY=(y0+y1)*.5f,shift=Mathf.Tan(skew*Mathf.Deg2Rad);
            float TopX(float x)=>x+shift*(y0-centerY);
            float BottomX(float x)=>x+shift*(y1-centerY);
            var solid=color;solid.a*=alpha;var clear=solid;clear.a=0;
            float[] columns=feathered?new[]{left,left+width*.5f,left+width}:new[]{left,left+width};
            Color[] tints=feathered?new[]{clear,solid,clear}:new[]{solid,solid};
            var mesh=context.Allocate(columns.Length*2,(columns.Length-1)*6);
            for(int i=0;i<columns.Length;i++)
            {
                mesh.SetNextVertex(new Vertex{position=new Vector3(TopX(columns[i]),y0,Vertex.nearZ),tint=tints[i]});
                mesh.SetNextVertex(new Vertex{position=new Vector3(BottomX(columns[i]),y1,Vertex.nearZ),tint=tints[i]});
            }
            for(int i=0;i<columns.Length-1;i++)
            {
                ushort a=(ushort)(i*2),b=(ushort)(i*2+1),c=(ushort)(i*2+2),d=(ushort)(i*2+3);
                mesh.SetNextIndex(a);mesh.SetNextIndex(c);mesh.SetNextIndex(d);
                mesh.SetNextIndex(a);mesh.SetNextIndex(d);mesh.SetNextIndex(b);
            }
        }
    }
}
