Shader "Galaxy101/Finish"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Linear tint", Vector) = (1,1,1,1)
        _SrcBlend ("Source blend", Float) = 5
        _DstBlend ("Destination blend", Float) = 1
        _SrcBlendAlpha ("Source alpha blend", Float) = 0
        _DstBlendAlpha ("Destination alpha blend", Float) = 1
        _Cull ("Cull", Float) = 0
        _BypassToneMapping ("Source toneMapped false", Float) = 0
        _Galaxy101Exposure ("Three exposure", Float) = 1.05
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "Finish"
            Tags { "LightMode"="Galaxy101Finish" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite Off Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Galaxy101ThreeAces.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _BypassToneMapping;
                float _Galaxy101Exposure;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color * _BaseColor;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color;
                if (_BypassToneMapping < .5) color.rgb = G101_ThreeAces(color.rgb, _Galaxy101Exposure);
                color.rgb = LinearToSRGB(color.rgb);
                return color;
            }
            ENDHLSL
        }
    }
}
