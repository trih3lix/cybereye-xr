Shader "CyberEye/CyberpunkFeed"
{
    // M3: cyberpunk filter applied directly to the Eye feed quad — chromatic aberration, neon cyan/magenta
    // grade, scanlines, moving scan-sweep, glitch bands, vignette, grain. URP unlit, Cull Off.
    Properties
    {
        _MainTex ("Feed", 2D) = "black" {}
        _Intensity ("Intensity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off ZWrite On ZTest LEqual
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float _Intensity;

            V vert (A i) { V o; o.positionHCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o; }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            half4 frag (V i) : SV_Target
            {
                float k = _Intensity;
                float t = _Time.y;
                float2 uv = i.uv;
                float2 c = uv - 0.5;
                float dist = length(c);

                // glitch: occasional horizontal band x-offset
                float band = floor(uv.y * 44.0);
                float gsel = hash(float2(band, floor(t * 9.0)));
                float glitch = (gsel > 0.93) ? (hash(float2(band, floor(t * 23.0))) - 0.5) * 0.05 : 0.0;
                uv.x = saturate(uv.x + glitch * k);

                // chromatic aberration (grows toward edges)
                float2 ca = c * (0.005 + dist * 0.012) * k;
                half r  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + ca).r;
                half gg = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).g;
                half b  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - ca).b;
                half3 col = half3(r, gg, b);

                // neon grade: contrast + cyan(dark)->magenta(bright) tint + highlight bloom
                col = pow(saturate(col), 1.15);
                half luma = dot(col, half3(0.299, 0.587, 0.114));
                half3 tint = lerp(half3(0.05, 0.35, 0.50), half3(0.90, 0.15, 0.70), luma);
                col = lerp(col, col * 1.15 + tint * 0.35, 0.5 * k);
                col += saturate(luma - 0.6) * half3(0.10, 0.55, 0.75) * k;

                // scanlines + moving scan-sweep
                float sl = 0.88 + 0.12 * sin(i.uv.y * 700.0 + t * 3.0);
                col *= lerp(1.0, sl, k);
                float sweep = 1.0 - smoothstep(0.0, 0.05, abs(frac(uv.y - t * 0.12) - 0.5) - 0.45);
                col += sweep * half3(0.0, 0.45, 0.55) * 0.12 * k;

                // vignette + grain
                col *= lerp(1.0, smoothstep(0.90, 0.30, dist), 0.7 * k);
                col += (hash(uv * 1000.0 + t) - 0.5) * 0.03 * k;

                return half4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
