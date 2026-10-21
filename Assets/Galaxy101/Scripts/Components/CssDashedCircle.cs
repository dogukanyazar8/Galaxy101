using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>Centered, closed-loop CSS dashed border; shared geometry with the shop coin artwork.</summary>
    internal sealed class CssDashedCircle : VisualElement
    {
        internal readonly struct Layout
        {
            public readonly float Radius,StepRadians;
            public readonly int Count;
            public Layout(float diameter,float stroke)
            {
                if(stroke<=0||diameter<=stroke)throw new ArgumentOutOfRangeException(nameof(stroke));
                Radius=(diameter-stroke)*.5f;
                // A nominal dash and its gap each occupy three stroke widths. Fit whole pairs
                // around the closed border, matching ShopArtwork.CoinPaint's existing rule.
                Count=Mathf.Max(1,Mathf.RoundToInt(2*Mathf.PI*Radius/(6*stroke)));
                StepRadians=2*Mathf.PI/Count;
            }
        }
        internal CssDashedCircle(float diameter,float stroke,Color color)
        {
            name="css-dashed-circle";pickingMode=PickingMode.Ignore;
            style.position=Position.Absolute;style.left=style.top=0;style.width=style.height=diameter;
            var layout=new Layout(diameter,stroke);
            generateVisualContent+=context=>
            {
                var painter=context.painter2D;painter.strokeColor=color;painter.lineWidth=stroke;painter.lineCap=LineCap.Butt;
                var center=new Vector2(diameter*.5f,diameter*.5f);
                for(int i=0;i<layout.Count;i++)
                {
                    painter.BeginPath();painter.Arc(center,layout.Radius,Angle.Radians(i*layout.StepRadians),Angle.Radians((i+.5f)*layout.StepRadians));painter.Stroke();
                }
            };
        }
    }
}
