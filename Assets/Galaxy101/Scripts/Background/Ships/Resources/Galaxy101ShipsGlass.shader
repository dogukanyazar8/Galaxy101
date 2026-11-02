// The lounge domes' glass as the source draws it: MeshPhysicalMaterial #CFE8FF, roughness .03, clearcoat 1,
// envMapIntensity 2.4, transparent opacity .2, normal blending, double-sided, no depth write. three.js scales the whole
// lit colour (diffuse, specular and clearcoat reflections) by the opacity, so the okey lounge stays visible through it.
Shader "Galaxy101/ShipsGlass"
{
    Properties { _BaseColor("Base", Color) = (0.812, 0.91, 1, 0.2) _EnvCube("Environment", Cube) = "black" {} _EnvIntensity("Environment intensity", Float) = 2.4 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            TEXTURECUBE(_EnvCube); SAMPLER(sampler_EnvCube);
            CBUFFER_START(UnityPerMaterial) half4 _BaseColor; float _EnvIntensity; CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS); return o;
            }
            half4 frag(Varyings i, bool front:SV_IsFrontFace):SV_Target
            {
                float3 n = normalize(i.normalWS) * (front ? 1 : -1), v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float nv = saturate(dot(n, v)), fresnel = .04 + .96 * pow(1 - nv, 5);
                Light key = GetMainLight();
                float3 h = normalize(key.direction + v);
                float nl = saturate(dot(n, key.direction)), nh = saturate(dot(n, h));
                // Lambert under the key light and the studio ambient; a near-mirror (roughness .03) highlight.
                float3 diffuse = _BaseColor.rgb * (key.color * nl + SampleSH(n)); // key.color is already the source intensity / π
                float3 highlight = key.color * nl * pow(nh, 1400) * 60 * PI * fresnel;
                // Base specular and clearcoat both mirror the studio environment.
                float3 reflection = SAMPLE_TEXTURECUBE_LOD(_EnvCube, sampler_EnvCube, reflect(-v, n), 0).rgb * _EnvIntensity * fresnel * 2;
                return half4(diffuse * (1 - fresnel) + reflection + highlight, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
