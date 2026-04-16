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
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _BlurDirection;
            float _BlurSize;

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 direction = _BlurDirection.xy * _BlurSize;
                fixed4 color = tex2D(_MainTex, input.uv) * 0.22702703;
                color += tex2D(_MainTex, input.uv + direction * 1.38461538) * 0.31621622;
                color += tex2D(_MainTex, input.uv - direction * 1.38461538) * 0.31621622;
                color += tex2D(_MainTex, input.uv + direction * 3.23076923) * 0.07027027;
                color += tex2D(_MainTex, input.uv - direction * 3.23076923) * 0.07027027;
                return color;
            }
            ENDCG
        }
    }
}
