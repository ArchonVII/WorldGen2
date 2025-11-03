Shader "Unlit/PlateBoundariesAA_v2"
{
    Properties
    {
        _PlateIDTex ("Plate ID (R channel)", 2D) = "white" {}
        _Thickness  ("Line Thickness (UV)", Float) = 1.0
        _AA         ("Antialias Width", Float) = 1.0
        _LineColor  ("Line Color", Color) = (0,0,0,1)
        _Opacity    ("Opacity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
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
                float  _Thickness;
                float  _AA;
                float4 _LineColor;
                float  _Opacity;
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

            float idAt(float2 uv) { return SAMPLE_TEXTURE2D(_PlateIDTex, sampler_PlateIDTex, uv).r; }

            half4 frag(Varyings i) : SV_Target
            {
                float2 t = _PlateIDTex_TexelSize.xy;

                // center & 4-neighbors (enough to mark borders cleanly)
                float c = idAt(i.uv);
                float d1 = abs(c - idAt(i.uv + float2( t.x, 0)));
                float d2 = abs(c - idAt(i.uv + float2(-t.x, 0)));
                float d3 = abs(c - idAt(i.uv + float2(0,  t.y)));
                float d4 = abs(c - idAt(i.uv + float2(0, -t.y)));

                // any change means "on a border"
                float diff = max(max(d1,d2), max(d3,d4));

                // AA around a tiny threshold near zero
                float thr = 0.001; // ~1/1024 of 0..1 range
                float aaw = max(fwidth(diff) * _AA, 1e-5);
                float alpha = smoothstep(thr - aaw * _Thickness, thr + aaw * _Thickness, diff);

                return half4(_LineColor.rgb, alpha * _Opacity);
            }
            ENDHLSL
        }
    }
}
