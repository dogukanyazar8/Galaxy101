Shader "Galaxy101/Stars"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            #include "../CanvasColor.hlsl"
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float2 mode:TEXCOORD1;};
            struct Varyings {float4 position:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float2 mode:TEXCOORD1;};
            Varyings Vert(Input i) {
                Varyings o;o.position=float4(i.vertex.x/640-1,1-i.vertex.y/360,0,1);
                #if UNITY_UV_STARTS_AT_TOP
                o.position.y=-o.position.y;
                #endif
                o.uv=i.uv;o.color=i.color;o.mode=i.mode;return o;
            }
            float4 Frag(Varyings i):SV_Target
            {
                float a;
                if(i.mode.x>.5) {float t=i.uv.x;a=t<.25?lerp(1,.5,t*4):lerp(.5,0,(t-.25)/.75);a*=saturate((1-abs(i.uv.y*2-1))*3);}
                else {float r=length(i.uv*2-1);a=r<.18?lerp(1,.85,r/.18):r<.45?lerp(.85,.18,(r-.18)/.27):lerp(.18,0,saturate((r-.45)/.55));}
                float3 c=i.color.rgb;
                if(i.mode.x>.5) c*=i.uv.x<.25?lerp(float3(1,1,1),float3(1,.94117647,.82352941),i.uv.x*4):lerp(float3(1,.94117647,.82352941),float3(1,1,1),(i.uv.x-.25)/.75);
                // Alpha belongs to the gamma-space canvas contribution, before conversion.
                return float4(CanvasToLinear(c*a*i.color.a),0);
            }
            ENDHLSL
        }
    }
}
