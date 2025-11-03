Shader "Unlit/PlateBoundariesAA"
{
    Properties
    {
        _PlateIDTex("Plate ID (R8)", 2D) = "white" {}
        _Thickness("Line Thickness (UV)", Float) = 1.5
        _AA("Antialias Width", Float) = 1.0
        _LineColor("Line Color", Color) = (0,0,0,1)
        _Opacity("Opacity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_PlateIDTex); SAMPLER(sampler_PlateIDTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _PlateIDTex_TexelSize; // x=1/w, y=1/h
                float _Thickness;
                float _AA;
                float4 _LineColor;
                float _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float sampleID(float2 uv)
            {
                // IDs are stored in R channel 0..1; we compare as floats
                return SAMPLE_TEXTURE2D(_PlateIDTex, sampler_PlateIDTex, uv).r;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 texel = _PlateIDTex_TexelSize.xy;

                // Center & 8-neighbor samples
                float c  = sampleID(i.uv);
                float n  = sampleID(i.uv + float2(0, -texel.y));
                float s  = sampleID(i.uv + float2(0,  texel.y));
                float e  = sampleID(i.uv + float2( texel.x, 0));
                float w  = sampleID(i.uv + float2(-texel.x, 0));
                float ne = sampleID(i.uv + texel * float2( 1,-1));
                float nw = sampleID(i.uv + texel * float2(-1,-1));
                float se = sampleID(i.uv + texel * float2( 1, 1));
                float sw = sampleID(i.uv + texel * float2(-1, 1));

                // Any difference signals a boundary presence
                float maxDiff = max(
                    max(abs(c-n), abs(c-s)),
                    max(abs(c-e), abs(c-w))
                );
                maxDiff = max(maxDiff, max(abs(c-ne), abs(c-nw)));
                maxDiff = max(maxDiff, max(abs(c-se), abs(c-sw)));

                // Convert to a distance-like signal using screen-space derivatives
                // fwidth gives us a good AA scale factor
                float edge = saturate(maxDiff * _Thickness);
                float aaw = fwidth(edge) * _AA;
                float alpha = smoothstep(1.0 - aaw, 1.0 + aaw, edge);

                // Output thin, crisp lines
                return half4(_LineColor.rgb, alpha * _Opacity);
            }
            ENDHLSL
        }
    }
}
