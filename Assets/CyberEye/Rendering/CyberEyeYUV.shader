Shader "CyberEye/YUVtoRGB"
{
    // URP-native YUV_420_888 -> RGB for the XREAL Eye feed (the sample's Unlit/YUVTransRGB is Built-in-RP
    // and renders magenta under URP). Y/U/V arrive as three Alpha8 textures (data in the .a channel).
    Properties
    {
        _MainTex ("Y plane", 2D) = "black" {}
        _UTex ("U plane", 2D) = "black" {}
        _VTex ("V plane", 2D) = "black" {}
        // C-2: detector-input channel order. 1 = legacy BGR placement (pre-review default,
        // kept to avoid a blind regression); 0 = true RGB. Settle on-device via feed_dump.png.
        _SwapRB ("Swap R/B (1=legacy BGR, 0=RGB)", Float) = 1
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

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_UTex);    SAMPLER(sampler_UTex);
            TEXTURE2D(_VTex);    SAMPLER(sampler_VTex);
            float _SwapRB;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half y = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                half u = SAMPLE_TEXTURE2D(_UTex,    sampler_UTex,    IN.uv).a;
                half v = SAMPLE_TEXTURE2D(_VTex,    sampler_VTex,    IN.uv).a;
                // BT.601 with the XREAL sample's coefficients + channel placement.
                half r = y + 1.4022h * v - 0.7011h;
                half g = y - 0.3456h * u - 0.7145h * v + 0.53005h;
                half b = y + 1.771h  * u - 0.8855h;
                // Detector-input feed (never displayed): YOLOv8 wants sRGB-encoded RGB in [0,1].
                // C-2 fix (a): removed the pow(2.2) gamma->linear that crushed the mid-tones the
                // network keys on (0.5 -> 0.22). The RT is now created Linear (EyeCameraFeed), so
                // these sRGB-encoded values reach the Sentis tensor unchanged.
                // C-2 fix (b): channel order is A/B-testable via _SwapRB — legacy BGR stays the
                // default until feed_dump.png confirms the Eye's plane order on device.
                half3 rgb = saturate(lerp(half3(r, g, b), half3(b, g, r), _SwapRB));
                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
