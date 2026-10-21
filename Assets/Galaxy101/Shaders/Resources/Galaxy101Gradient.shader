Shader "Galaxy101/Gradient"
{
    // Defaults are the menu layout; GalaxyBackground.UseTableLayout overrides them for the game table.
    Properties { _Shape("Gradient centre, radii",Vector)=(640,252,1024,504) _HaloShape("Halo centre, radius, mode",Vector)=(640,300,231,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "UnityCG.cginc"
        #include "../CanvasColor.hlsl"
        sampler2D _CanvasBackdrop;
        float4 _Bg1,_Bg2,_Bg3,_Halo,_GlowAtmosphere;
        float4 _Shape,_HaloShape; // gradient centre+radii; halo centre, radius, mode (0 menu glow, 1 table radial)
        float _Flattening,_GroupAngle;
        struct Input {float4 vertex:POSITION;};
        struct Varyings {float4 position:SV_POSITION;float2 pixelPosition:TEXCOORD0;};
        Varyings Vert(Input i) {
            Varyings o;o.position=float4(i.vertex.x/640-1,1-i.vertex.y/360,0,1);
            #if UNITY_UV_STARTS_AT_TOP
            o.position.y=-o.position.y;
            #endif
            o.pixelPosition=i.vertex.xy;return o;
        }
        float4 Background(Varyings i):SV_Target {float r=length((i.pixelPosition-_Shape.xy)/_Shape.zw);float3 c=r<.55?lerp(_Bg1.rgb,_Bg2.rgb,r/.55):lerp(_Bg2.rgb,_Bg3.rgb,saturate((r-.55)/.45));return float4(CanvasToLinear(c),1);}
        float4 Halo(Varyings i):SV_Target {
            float3 color=CanvasToGamma(tex2D(_CanvasBackdrop,CanvasUv(i.pixelPosition)).rgb);
            // Table: radial-gradient(closest-side,var(--pg),transparent 70%) only, no planet glow or rim.
            if(_HaloShape.w>.5) return float4(CanvasToLinear(lerp(color,_Halo.rgb,_Halo.a*saturate(1-length(i.pixelPosition-_HaloShape.xy)/_HaloShape.z))),1);
            float2 p=i.pixelPosition-_HaloShape.xy;float s=sin(-_GroupAngle),c=cos(-_GroupAngle);p=float2(c*p.x-s*p.y,s*p.x+c*p.y);
            float halo=_Halo.a*saturate(1-length(p)/_HaloShape.z);
            float ellipse=length(p*float2(1,_Flattening));
            // CSS shadow Gaussian approximation: 80px broad glow plus shifted 14px rim.
            float broadDistance=max(0,ellipse-148)/35;
            float rimDistance=(length((p+3)*float2(1,_Flattening))-142)/7;
            float broad=_Halo.a*exp(-broadDistance*broadDistance)*.55;
            float rim=_GlowAtmosphere.a*exp(-rimDistance*rimDistance);
            color=lerp(color,_Halo.rgb,halo);
            color=lerp(color,_Halo.rgb,broad);
            color=lerp(color,_GlowAtmosphere.rgb,rim);
            return float4(CanvasToLinear(color),1);
        }
        float4 Overlay(Varyings i):SV_Target {float r=length((i.pixelPosition-float2(640,345.6))/float2(704,324));float a=.28*saturate(1-r/.7);float3 background=CanvasToGamma(tex2D(_CanvasBackdrop,CanvasUv(i.pixelPosition)).rgb);return float4(CanvasToLinear(background*(1-a)),1);}
        ENDHLSL
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Background
            ENDHLSL
        }
        Pass
        {
            Blend One Zero
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Halo
            ENDHLSL
        }
        Pass
        {
            Blend One Zero
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Overlay
            ENDHLSL
        }
    }
}
