// Full-screen three.js ACESFilmic tone mapping for the 3D table camera (ThreeToneMappingFeature). Alpha passes through:
// the HUD composites the table view's alpha over the galaxy background.
Shader "Hidden/Galaxy101/ThreeAces"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass
        {
            Name "ThreeAces"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Galaxy101ThreeAces.hlsl"

            float _Galaxy101Exposure;

            half4 Frag(Varyings input) : SV_Target
            {
                float4 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0);
                return half4(G101_ThreeAces(c.rgb, _Galaxy101Exposure), c.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Encode finish destination"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Encode
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 Encode(Varyings input) : SV_Target
            {
                float4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0);
                return float4(LinearToSRGB(color.rgb), color.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Decode finish composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Decode
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 Decode(Varyings input) : SV_Target
            {
                float4 color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0);
                return float4(SRGBToLinear(color.rgb), color.a);
            }
            ENDHLSL
        }
    }
}
