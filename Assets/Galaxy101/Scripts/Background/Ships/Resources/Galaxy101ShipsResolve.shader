Shader "Galaxy101/ShipsResolve"
{
    Properties { _MainTex("Source",2D)="black"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Assets/Galaxy101/Shaders/Resources/Galaxy101ThreeAces.hlsl"
            sampler2D _MainTex;
            half4 frag(v2f_img i):SV_Target
            {
                float4 c=tex2D(_MainTex,i.uv);
                return half4(G101_ThreeAces(c.rgb/max(c.a,.0001),1.1),c.a);
            }
            ENDHLSL
        }
    }
}
