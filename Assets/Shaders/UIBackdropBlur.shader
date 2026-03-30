Shader "Hidden/StringTheory/UIBackdropBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurDirection ("Blur Direction", Vector) = (0.001, 0, 0, 0)
        _BlurSize ("Blur Size", Float) = 2.6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend One Zero

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float4 _BlurDirection;
            float _BlurSize;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 direction = _BlurDirection.xy * _BlurSize;
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * 0.22702703h;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + direction * 1.38461538f) * 0.31621622h;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - direction * 1.38461538f) * 0.31621622h;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + direction * 3.23076923f) * 0.07027027h;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - direction * 3.23076923f) * 0.07027027h;
                return color;
            }
            ENDHLSL
        }
    }
}
