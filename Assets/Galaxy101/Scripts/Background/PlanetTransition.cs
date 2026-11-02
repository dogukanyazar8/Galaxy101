using System;

namespace Galaxy101
{
    /// <summary>The menu's Web Animations keyframes, sampled against the pausable background clock.</summary>
    public sealed class PlanetTransition
    {
        public readonly struct Pose
        {
            public readonly float X,Y,Scale,Rotation,Opacity,Blur,Brightness;
            public Pose(float x,float y,float scale,float rotation,float opacity,float blur,float brightness=1)
            {X=x;Y=y;Scale=scale;Rotation=rotation;Opacity=opacity;Blur=blur;Brightness=brightness;}
        }
        public readonly struct Frame
        {
            public readonly bool Active;
            public readonly Pose Incoming,Outgoing;
            public readonly float RingScale,RingOpacity,HaloOpacity;
            public Frame(bool active,Pose incoming,Pose outgoing,float ringScale,float ringOpacity,float haloOpacity)
            {Active=active;Incoming=incoming;Outgoing=outgoing;RingScale=ringScale;RingOpacity=ringOpacity;HaloOpacity=haloOpacity;}
        }

        private static readonly Pose Rest=new Pose(0,0,1,0,1,0);
        private static readonly Pose Gone=new Pose(-560,300,.16f,-75,0,7,.6f);
        private static readonly Pose Recoil=new Pose(14,-8,1.07f,3,1,0,1.15f);
        private static readonly Pose Arriving=new Pose(480,-320,.1f,0,0,10);
        private static readonly Pose Overshoot=new Pose(-10,6,1.05f,0,1,0);
        public string Current {get;private set;}
        public string Previous {get;private set;}
        public double StartedAt {get;private set;}

        public void Configure(string name,bool animate,double now)
        {
            ValidateTime(now);
            if(string.IsNullOrEmpty(name))throw new ArgumentException("A planet resource name is required.",nameof(name));
            if(!animate) {Current=name;Cancel();return;}
            if(Current==name)return;
            // Replacing a running swap drops its old snapshot, just as planetSwap removes _swapG.
            Previous=Current;Current=name;StartedAt=now;
        }

        public void Cancel()=>Previous=null;

        public Frame Sample(double now)
        {
            ValidateTime(now);
            double elapsed=Math.Max(0,now-StartedAt);
            if(Previous==null||elapsed>=1.8)
            {
                Cancel();return new Frame(false,Rest,Gone,1.9f,0,1);
            }
            // WAAPI's effect easing warps total progress before keyframe offset interpolation.
            float leave=Ease(elapsed/.95,.55,0,.75,.35);
            var outgoing=leave<=.28f?Mix(Rest,Recoil,leave/.28f):Mix(Recoil,Gone,(leave-.28f)/.72f);
            float arrive=Ease((elapsed-.32)/1.15,.2,.85,.25,1);
            var incoming=arrive<=.72f?Mix(Arriving,Overshoot,arrive/.72f):Mix(Overshoot,Rest,(arrive-.72f)/.28f);
            float pulse=Ease((elapsed-.9)/.9,.2,.7,.3,1);
            float pulseOpacity=pulse<=.25f?.85f*pulse/.25f:.85f*(1-(pulse-.25f)/.75f);
            float halo=Ease((elapsed-.5)/.9,0,0,.58,1);
            return new Frame(true,incoming,outgoing,.55f+1.35f*pulse,pulseOpacity,halo);
        }

        private static Pose Mix(Pose from,Pose to,float t)=>new Pose(
            Lerp(from.X,to.X,t),Lerp(from.Y,to.Y,t),Lerp(from.Scale,to.Scale,t),Lerp(from.Rotation,to.Rotation,t),
            Lerp(from.Opacity,to.Opacity,t),Lerp(from.Blur,to.Blur,t),Lerp(from.Brightness,to.Brightness,t));
        private static float Lerp(float from,float to,float t)=>from+(to-from)*t;
        private static float Ease(double x,double x1,double y1,double x2,double y2)
        {
            if(x<=0)return 0;if(x>=1)return 1;
            double lo=0,hi=1,t=0;
            for(int i=0;i<24;i++)
            {
                t=(lo+hi)*.5;double q=1-t;
                if(3*q*q*t*x1+3*q*t*t*x2+t*t*t<x)lo=t;else hi=t;
            }
            double v=1-t;return (float)(3*v*v*t*y1+3*v*t*t*y2+t*t*t);
        }
        private static void ValidateTime(double now)
        {
            if(double.IsNaN(now)||double.IsInfinity(now)||now<0)throw new ArgumentOutOfRangeException(nameof(now));
        }
    }
}
