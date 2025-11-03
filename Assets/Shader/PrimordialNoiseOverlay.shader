Shader "Unlit/PrimordialNoiseOverlay"
{
    Properties
    {
        _Heightmap ("Heightmap (R)", 2D) = "white" {}
        _Opacity ("Opacity", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha // Alpha blending
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Heightmap); SAMPLER(sampler_Heightmap);

            CBUFFER_START(UnityPerMaterial)
                float _Opacity;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings  { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Sample the heightmap (value is in the R channel)
                float height = SAMPLE_TEXTURE2D(_Heightmap, sampler_Heightmap, i.uv).r;
                
                // Show as grayscale
                half4 color = half4(height, height, height, 1.0);
                
                // Apply opacity
                color.a *= _Opacity;
                
                return color;
            }
            ENDHLSL
        }
    }
}
