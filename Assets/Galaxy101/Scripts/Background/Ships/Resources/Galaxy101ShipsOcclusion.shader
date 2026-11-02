Shader "Galaxy101/ShipsOcclusion"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-10" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ColorMask 0 ZWrite On Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vert(float4 positionOS:POSITION):SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half4 frag():SV_Target { return 0; }
            ENDHLSL
        }
    }
}
