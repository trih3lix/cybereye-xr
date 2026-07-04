Shader "CyberEye/YUVtoRGB"
{
    // URP-native YUV_420_888 -> RGB for the XREAL Eye feed (the sample's Unlit/YUVTransRGB is Built-in-RP
    // and renders magenta under URP). Y/U/V arrive as three Alpha8 textures (data in the .a channel).
    Properties
    {
        _MainTex ("Y plane", 2D) = "black" {}
        _UTex ("U plane", 2D) = "black" {}
        _VTex ("V plane", 2D) = "black" {}
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
                half3 rgb = half3(b, g, r);
                rgb = pow(saturate(rgb), 2.2h);   // gamma -> linear (project is Linear color space)
                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
