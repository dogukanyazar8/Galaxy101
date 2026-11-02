// Unlit, alpha-blended tile face for tiles lying on the felt (table3d.js faceMatFlat: MeshBasicMaterial, toneMapped:false).
// The table camera is tone-mapped as a whole (ThreeToneMappingFeature), so the face writes the colour that the tone
// mapper turns back into its texture colour.
Shader "Galaxy101/FlatFace"
{
    Properties
    {
        _BaseMap ("Atlas", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Galaxy101Exposure ("Tone mapper exposure", Float) = 1.05
        _Galaxy101ToneMapped ("Drawn under the three.js tone mapper", Float) = 1
        _Galaxy101SrgbBlend ("Blend as the browser does (display space) over the backdrop", Float) = 0
        _Galaxy101Backdrop ("Displayed colour under the face (sRGB)", Color) = (0.953, 0.914, 0.894, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            // Alpha accumulates "over" so the HUD's composite of this camera's alpha has no holes.
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Galaxy101ThreeAces.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Galaxy101Exposure;
                float _Galaxy101ToneMapped;
                float _Galaxy101SrgbBlend;
                half4 _Galaxy101Backdrop;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings Vert(Attributes input)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                // Only under the three.js tone mapper (the table camera); elsewhere (0) the colour is used as is.
                if (_Galaxy101ToneMapped > 0.5)
                {
                    half3 ink = G101_ThreeAcesInverse(c.rgb, _Galaxy101Exposure);
                    if (_Galaxy101SrgbBlend > 0.5 && c.a > 0.001 && c.a < 0.999)
                    {
                        // three.js blends this face in the 8-bit display buffer: shown = a·ink + (1 − a)·backdrop (sRGB).
                        // Here the blend runs before the tone mapper, where the white tile is far brighter than the ink, so
                        // soft edges would wash out. Choose the alpha that lands on the browser's colour over the backdrop.
                        const half3 w = half3(0.2126, 0.7152, 0.0722);
                        half3 shown = c.a * LinearToSRGB(c.rgb) + (1 - c.a) * _Galaxy101Backdrop.rgb;
                        half target = dot(G101_ThreeAcesInverse(SRGBToLinear(shown), _Galaxy101Exposure), w);
                        half back = dot(G101_ThreeAcesInverse(SRGBToLinear(_Galaxy101Backdrop.rgb), _Galaxy101Exposure), w), front = dot(ink, w);
                        if (abs(back - front) > 1e-3) c.a = saturate((back - target) / (back - front));
                    }
                    c.rgb = ink;
                }
                return c;
            }
            ENDHLSL
        }
    }
}
