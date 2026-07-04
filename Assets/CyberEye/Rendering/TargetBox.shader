Shader "CyberEye/TargetBox"
{
    // M5: neon targeting box for a detected object. Pulsing border + moving scan line in a class color;
    // interior black (= transparent on the additive optical display, so the real object shows inside).
    Properties { _Color ("Color", Color) = (0,1,0.9,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 p : SV_POSITION; float2 uv : TEXCOORD0; };
            float4 _Color;
            V vert (A i) { V o; o.p = TransformObjectToHClip(i.p.xyz); o.uv = i.uv; return o; }
            half4 frag (V i) : SV_Target
            {
                float2 uv = i.uv;
                float2 e = min(uv, 1.0 - uv);
                float ed = min(e.x, e.y);            // distance to nearest edge
                float th = 0.035;                    // border thickness
                float L  = 0.24;                     // corner-bracket length
                // corner brackets: border pixels that are also near a corner
                float onBorder = step(ed, th);
                float nearCorner = max(step(uv.x, L) + step(1.0 - L, uv.x), step(uv.y, L) + step(1.0 - L, uv.y));
                float bracket = onBorder * saturate(nearCorner);
                float pulse = 0.55 + 0.45 * sin(_Time.y * 5.0);
                half3 col = _Color.rgb * bracket * pulse;
                col += _Color.rgb * onBorder * 0.18 * pulse;   // faint full frame
                // scan line sweeping down inside the box
                float scan = smoothstep(0.03, 0.0, abs(frac(uv.y - _Time.y * 0.6) - 0.5) - 0.47);
                col += _Color.rgb * scan * 0.30;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
