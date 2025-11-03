Shader "Unlit/PlateBoundariesAA_v2"
{
    Properties
    {
        // --- MODIFIED: We no longer use PlateIDTex for boundary logic ---
        _BoundaryDeltaTex ("Boundary Delta (RHalf)", 2D) = "white" {}
        
        // --- MODIFIED: These properties now control the new logic ---
        // [Tooltip(...)] attributes are C# only and not valid in ShaderLab.
        _Threshold ("Threshold", Range(0.001, 0.1)) = 0.02
        
        _AA ("Antialias Width", Range(0.001, 0.05)) = 0.01

        _LineColor ("Line Color", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0,1)) = 1.0
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

            // --- MODIFIED: New texture and sampler ---
            TEXTURE2D(_BoundaryDeltaTex);
            SAMPLER(sampler_BoundaryDeltaTex);

            CBUFFER_START(UnityPerMaterial)
                float _Threshold;
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
            
            half4 frag (Varyings i) : SV_Target
            {
                // --- MODIFIED: New analytic boundary logic ---
                // [cite: GeminiUpload/Planet_Generator_Architecture_Review.md]
                
                // 1. Sample the pre-calculated boundary delta
                float delta = SAMPLE_TEXTURE2D(_BoundaryDeltaTex, sampler_BoundaryDeltaTex, i.uv).r;

                // 2. Calculate alpha
                // We want a line where delta is *less than* the threshold.
                // smoothstep(edge0, edge1, x) = 0 if x < edge0, 1 if x > edge1
                // We invert the logic to get an alpha *at* the edge.
                float alpha = 1.0 - smoothstep(_Threshold - _AA, _Threshold + _AA, delta);
                
                // 3. Output final color
                return half4(_LineColor.rgb, alpha * _Opacity);
            }
            ENDHLSL
        }
    }
}

