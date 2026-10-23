Shader "Galaxy101/Planet"
{
    Properties { _Surface("Surface",2D)="white"{} _Placement("Centre, scale",Vector)=(640,300,1,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend One Zero
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../PlanetRing.hlsl"
            #include "../CanvasColor.hlsl"
            sampler2D _Surface;
            sampler2D _CanvasBackdrop;
            float4 _Planet; // tilt radians, flattening, limb darkening, ambient
            float4 _Atmosphere; // RGB in gamma space and strength
            float4 _Placement; // centre in canvas px, scale, fade (0 = fully drawn, 1 = gone)
            float _Rings, _Rotation, _GroupAngle, _PixelScale, _SurfaceLod;
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 position:SV_POSITION;float2 local:TEXCOORD0;float2 pixelPosition:TEXCOORD1;};
            Varyings Vert(Input i)
            {
                Varyings o;float s=sin(_GroupAngle),c=cos(_GroupAngle);
                float2 v=i.vertex.xy*_Placement.z;
                float2 p=float2(c*v.x-s*v.y,s*v.x+c*v.y)+_Placement.xy;
                o.position=float4(p.x/640-1,1-p.y/360,0,1);
                #if UNITY_UV_STARTS_AT_TOP
                o.position.y=-o.position.y;
                #endif
                o.local=i.vertex.xy;o.pixelPosition=p;return o;
            }
            float4 Frag(Varyings i):SV_Target
            {
                float dx=i.local.x/149,dy=-i.local.y/149*_Planet.y;
                float r2=dx*dx+dy*dy, nz=sqrt(1-min(r2,.9999));
                float pa=saturate((1-sqrt(r2))*149*_Placement.z*_PixelScale/_Planet.y+.5);
                float ca=cos(_Planet.x),sa=sin(_Planet.x);
                float3 L=normalize(float3(-.5,.45,.74));
                float3 Lp=float3(L.x,L.y*ca+L.z*sa,-L.y*sa+L.z*ca);
                float4 ring=0;
                if(_Rings>.5)
                {
                    float z=-dy*ca/sa,qz=-dy*sa+z*ca,rad=length(float2(dx,qz));
                    if(rad>1.2 && rad<2.4 && (pa<=0 || z>nz))
                    {
                        ring=RingAt(rad);float b=dx*Lp.x+qz*Lp.z;
                        float shadow=(b<0 && b*b-(rad*rad-1)>0) ? .1 : 1;
                        ring.rgb*=(.62+.38*Lp.y)*shadow;
                    }
                }
                float3 color=ring.rgb;
                if(pa>0)
                {
                    float diff=dot(float3(dx,dy,nz),L),lam=saturate((diff+.06)/1.06);
                    float shade=_Planet.w+(1-_Planet.w)*pow(lam,1.15)*((1-_Planet.z)+_Planet.z*pow(nz,.55));
                    float3 q=float3(dx,dy*ca+nz*sa,-dy*sa+nz*ca);
                    if(_Rings>.5 && Lp.y>0 && q.y<0)
                    {
                        float t=-q.y/Lp.y;shade*=1-.85*RingAt(length(q.xz+t*Lp.xz)).a;
                    }
                    float lat=asin(clamp(q.y,-1,1)),lon=atan2(q.x,q.z);
                    float2 uv=float2(frac((lon+UNITY_PI)/(2*UNITY_PI)-_Rotation),.5+lat/UNITY_PI);
                    float3 surface=tex2Dlod(_Surface,float4(uv,0,_SurfaceLod)).rgb;
                    // Surface importer uses linear data: lighting must match the JS byte-space math.
                    float rim=pow(1-nz,2.4)*saturate(diff+.35)*_Atmosphere.w;
                    color=(surface*shade+_Atmosphere.rgb*rim)*(1-ring.a)+ring.rgb*ring.a;
                }
                float alpha=max(pa,ring.a)*saturate(1-_Placement.w);
                float3 background=CanvasToGamma(tex2D(_CanvasBackdrop,CanvasUv(i.pixelPosition)).rgb);
                return float4(CanvasToLinear(lerp(background,saturate(color),alpha)),1);
            }
            ENDHLSL
        }
    }
}
