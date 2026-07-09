Shader "CyberEye/TargetBox"
{
    // M5: neon targeting box for a detected object. Interior black (= transparent on the
    // additive optical display, so the real object shows inside).
    //
    // _Lock animates the acquire→lock transition (0 = acquiring: long loose brackets,
    // fast pulse; 1 = locked: tight corners + steady frame). _TOffset de-syncs the pulse
    // between boxes. _Mode 1 draws a small rotating diamond reticle instead of a box.
    // _Mode 2 draws a one-shot expanding ring burst: _Lock is reused as 0..1 progress
    // (radius grows, brightness fades) — driven per-frame by TargetOverlay on a new lock.
    Properties
    {
        _Color   ("Color", Color) = (0,1,0.9,1)
        _Lock    ("Lock", Range(0,1)) = 0
        _TOffset ("Time Offset", Float) = 0
        _Mode    ("Mode", Float) = 0
    }
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
            float _Lock;
            float _TOffset;
            float _Mode;

            V vert (A i) { V o; o.p = TransformObjectToHClip(i.p.xyz); o.uv = i.uv; return o; }

            half4 frag (V i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y + _TOffset;

                if (_Mode > 1.5)
                {
                    // Lock burst: thin circular ring expanding from the reticle center.
                    float2 cb = uv - 0.5;
                    float r = length(cb);
                    float rad = lerp(0.08, 0.46, _Lock);
                    float ring = smoothstep(0.030, 0.012, abs(r - rad));
                    half3 rcb = _Color.rgb * ring * (1.0 - _Lock * _Lock);
                    return half4(rcb, 1.0);
                }

                if (_Mode > 0.5)
                {
                    // Rotating diamond reticle: thin manhattan-distance ring.
                    float2 c = uv - 0.5;
                    float ang = t * 1.6;
                    float s = sin(ang), cs = cos(ang);
                    c = float2(c.x * cs - c.y * s, c.x * s + c.y * cs);
                    float m = abs(c.x) + abs(c.y);
                    float ring = smoothstep(0.30, 0.27, m) * smoothstep(0.20, 0.23, m);
                    float pip  = smoothstep(0.05, 0.02, m);   // center pip
                    half3 rc = _Color.rgb * (ring * 0.95 + pip * 0.8);
                    return half4(rc, 1.0);
                }

                float2 e = min(uv, 1.0 - uv);
                float ed = min(e.x, e.y);            // distance to nearest edge

                // Locked = tighter, thinner, steadier. Acquiring = loose, breathing.
                float th = lerp(0.045, 0.028, _Lock);          // border thickness
                float L  = lerp(0.34, 0.18, _Lock);            // corner-bracket length
                float pulse = lerp(0.45 + 0.55 * sin(t * 7.0), // acquiring: fast breathe
                                   0.85 + 0.15 * sin(t * 2.0), // locked: near-steady
                                   _Lock);

                float onBorder = step(ed, th);
                float nearCorner = max(step(uv.x, L) + step(1.0 - L, uv.x),
                                       step(uv.y, L) + step(1.0 - L, uv.y));
                float bracket = onBorder * saturate(nearCorner);

                half3 col = _Color.rgb * bracket * pulse;
                // faint full frame, stronger once locked
                col += _Color.rgb * onBorder * lerp(0.10, 0.30, _Lock) * pulse;
                // scan line sweeps only while acquiring; a locked target holds a thin
                // top-edge highlight instead (less visual noise on the real object)
                float scan = smoothstep(0.03, 0.0, abs(frac(uv.y - t * 0.6) - 0.5) - 0.47);
                col += _Color.rgb * scan * 0.35 * (1.0 - _Lock);
                float topline = step(1.0 - th * 1.6, uv.y);
                col += _Color.rgb * topline * 0.25 * _Lock;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
