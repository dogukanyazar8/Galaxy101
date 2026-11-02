Shader "Galaxy101/PlanetTransition"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend One Zero
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            #include "../CanvasColor.hlsl"
            sampler2D _CanvasBackdrop;
            float4 _Pulse; // centre in canvas pixels, scale, opacity
            float4 _Accent,_Glow;
            float _PixelScale;
            struct Input {float4 vertex:POSITION;};
            struct Varyings {float4 position:SV_POSITION;float2 pixelPosition:TEXCOORD0;};
            Varyings Vert(Input i)
            {
                Varyings o;o.position=float4(i.vertex.x/640-1,1-i.vertex.y/360,0,1);
                #if UNITY_UV_STARTS_AT_TOP
                o.position.y=-o.position.y;
                #endif
                o.pixelPosition=i.vertex.xy;return o;
            }
            float4 Frag(Varyings i):SV_Target
            {
                float radius=length(i.pixelPosition-_Pulse.xy)/max(.001,_Pulse.z);
                float aa=1/max(.001,_PixelScale*_Pulse.z);
                // The 340px empty circle has an outer 3px solid shadow and a 40px blur, 8px spread glow.
                float outside=saturate((radius-170)/aa+.5);
                float border=outside*saturate((173-radius)/aa+.5)*_Accent.a;
                float distance=max(0,radius-178)/20;
                float glow=outside*exp(-.5*distance*distance)*_Glow.a;
                float alpha=(border+(1-border)*glow)*_Pulse.w;
                float3 color=(_Accent.rgb*border+_Glow.rgb*glow*(1-border))*_Pulse.w;
                float3 background=CanvasToGamma(tex2D(_CanvasBackdrop,CanvasUv(i.pixelPosition)).rgb);
                return float4(CanvasToLinear(background*(1-alpha)+color),1);
            }
            ENDHLSL
        }
    }
}
