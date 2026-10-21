using System;
using System.Collections.Generic;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>Three fixed-size parallax layers; mesh allocation stays bounded.</summary>
    internal sealed class Starfield : IDisposable
    {
        private struct Star { public float X,Y,Alpha,Size,Speed,Twinkle,Phase; public bool Tint; public int Layer; }
        private struct Shot { public Vector2 Position,Velocity; public float Age,Life,Length; }
        private readonly Star[] stars=new Star[242];
        private readonly List<Shot> shots=new List<Shot>(4);
        private readonly System.Random random=new System.Random(101);
        private readonly List<Vector3> positions=new List<Vector3>(1100);
        private readonly List<Vector2> uv=new List<Vector2>(1100),modes=new List<Vector2>(1100);
        private readonly List<Color> colors=new List<Color>(1100);
        private readonly List<int> triangles=new List<int>(1650);
        private double nextShot=1.5;
        public Mesh Mesh { get; }
        public Starfield()
        {
            Mesh=new Mesh {name="Galaxy101 animated starfield",hideFlags=HideFlags.DontSave};Mesh.MarkDynamic();
            int index=0;int[] counts={150,70,22};float[] alpha={.35f,.7f,1.1f},size={.9f,1.5f,2.4f},speed={3,8,16};
            for(int layer=0;layer<3;layer++) for(int j=0;j<counts[layer];j++)
                stars[index++]=new Star {X=Range(0,1280),Y=Range(0,720),Alpha=alpha[layer]*Range(.6f,1),Size=size[layer]*Range(.6f,1.3f),Speed=speed[layer],Twinkle=Range(.4f,2.6f),Phase=Range(0,6.28f),Tint=Range(0,1)<.35f,Layer=layer};
        }
        private float Range(float low,float high) => low+(high-low)*(float)random.NextDouble();
        public void Update(float delta,double seconds,Color tint,bool lowQuality)
        {
            positions.Clear();uv.Clear();modes.Clear();colors.Clear();triangles.Clear();
            delta=Mathf.Min(.05f,delta);
            for(int i=0;i<stars.Length;i++)
            {
                var s=stars[i];s.X-=s.Speed*delta;if(s.X< -4) {s.X=1284;s.Y=Range(0,720);}stars[i]=s;
                if(lowQuality && (i&1)!=0) continue;
                float a=s.Alpha*(.55f+.45f*Mathf.Sin((float)seconds*s.Twinkle+s.Phase));
                var c=s.Tint?Color.Lerp(Color.white,tint,.55f):Color.white;c.a=Mathf.Clamp01(a);
                Quad(new Vector2(s.X,s.Y),Vector2.right*s.Size*2.6f,Vector2.up*s.Size*2.6f,c,false);
            }
            if(seconds>nextShot)
            {
                bool left=Range(0,1)<.5f;float angle=((left?200:-20)+Range(-5,20))*Mathf.Deg2Rad;
                shots.Add(new Shot {Position=new Vector2(left?1280*Range(.45f,1):1280*Range(0,.55f),Range(0,324)),Velocity=new Vector2(Mathf.Cos(angle)*820,Mathf.Abs(Mathf.Sin(angle))*820+120),Life=Range(.75f,1.25f),Length=Range(120,210)});
                nextShot=seconds+Range(2.2f,6.4f);
            }
            for(int i=shots.Count-1;i>=0;i--)
            {
                var shot=shots[i];shot.Age+=delta;if(shot.Age>shot.Life) {shots.RemoveAt(i);continue;}
                shot.Position+=shot.Velocity*delta;shots[i]=shot;
                float fade=Mathf.Sin(shot.Age/shot.Life*Mathf.PI);var direction=shot.Velocity.normalized;
                var color=new Color(1,1,1,fade);
                Quad(shot.Position-direction*shot.Length/2,-direction*shot.Length/2,new Vector2(-direction.y,direction.x)*.9f,color,true);
                Quad(shot.Position,Vector2.right*7,Vector2.up*7,color,false);
            }
            Mesh.Clear();Mesh.SetVertices(positions);Mesh.SetUVs(0,uv);Mesh.SetUVs(1,modes);Mesh.SetColors(colors);Mesh.SetTriangles(triangles,0,false);
            Mesh.bounds=new Bounds(new Vector3(640,360),new Vector3(1600,1000,1));
        }
        private void Quad(Vector2 center,Vector2 across,Vector2 down,Color color,bool tail)
        {
            int n=positions.Count;positions.Add(center-across-down);positions.Add(center+across-down);positions.Add(center+across+down);positions.Add(center-across+down);
            // Tail coordinate zero begins at its bright head.
            uv.Add(new Vector2(0,0));uv.Add(new Vector2(1,0));uv.Add(new Vector2(1,1));uv.Add(new Vector2(0,1));
            for(int j=0;j<4;j++) {colors.Add(color);modes.Add(new Vector2(tail?1:0,0));}
            triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
        }
        public void Dispose() {if(Mesh!=null) {if(Application.isPlaying) UnityEngine.Object.Destroy(Mesh);else UnityEngine.Object.DestroyImmediate(Mesh);}}
    }
}
