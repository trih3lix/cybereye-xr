Shader "CyberEye/FeedUnlit"
{
    // URP unlit for the Eye PreviewTexture (already RGB from XREALVideoCapture's blender). Cull Off so the
    // camera-parented quad shows regardless of facing. (Built-in "Unlit/Texture" renders magenta in URP.)
    Properties { _MainTex ("Feed", 2D) = "black" {} }
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
            V vert (A i) { V o; o.positionHCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o; }
            half4 frag (V i) : SV_Target { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv); }
            ENDHLSL
        }
    }
    Fallback Off
}
