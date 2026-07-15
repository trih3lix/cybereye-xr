Shader "CyberEye/HudOverlay"
{
    // Option A (optical see-through): cyberpunk grade as a NEON overlay on black. On the One Pro's additive
    // display, black emits no light (= transparent, real world shows through) and neon pixels overlay. Draw
    // faint scanlines + a moving scan-sweep line + a neon edge frame. Backmost layer (HUD/glow render on top).
    Properties { _Intensity ("Intensity", Range(0,1)) = 1 }
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
            float _Intensity;
            V vert (A i) { V o; o.positionHCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            half4 frag (V i) : SV_Target
            {
                float k = _Intensity;
                float t = _Time.y;
                float2 uv = i.uv;
                float d = length(uv - 0.5);
                half3 col = 0;

                // faint cyan scanlines
                float sl = 0.5 + 0.5 * sin(uv.y * 620.0);
                col += half3(0.0, 0.45, 0.65) * (sl * sl) * 0.09 * k;

                // bright cyan scan-sweep moving down
                float sweepY = frac(t * 0.15);
                // 'line' is a RESERVED word in GLSL ES — naming a variable `line` compiled on the
                // editor platform but failed the GLES3 variant at build time, so the whole overlay
                // fell back to the magenta error shader on-device (the app forces GLES3).
                float sweepLine = smoothstep(0.012, 0.0, abs(uv.y - sweepY));
                col += half3(0.15, 0.9, 1.0) * sweepLine * 0.55 * k;

                // neon edge frame (vignette-glow)
                float edge = smoothstep(0.40, 0.80, d);
                col += half3(0.0, 0.35, 0.55) * edge * 0.22 * k;

                // faint magenta cross-hair ticks at center
                float cross = smoothstep(0.004, 0.0, abs(uv.x - 0.5)) + smoothstep(0.004, 0.0, abs(uv.y - 0.5));
                col += half3(0.8, 0.15, 0.6) * cross * 0.15 * k;

                // subtle chromatic grain
                col += (hash(uv * 900.0 + t) - 0.5) * 0.02 * k * half3(0.0, 1.0, 1.0);

                return half4(col, 1.0);   // black -> transparent on additive display
            }
            ENDHLSL
        }
    }
    Fallback Off
}
