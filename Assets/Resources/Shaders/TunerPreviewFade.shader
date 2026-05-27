Shader "Hidden/StringTheory/TunerPreviewFade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _FadeStart ("Fade Start", Range(0, 1)) = 0.72
        _FadeEnd ("Fade End", Range(0, 1)) = 0.98
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
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
            float _FadeStart;
            float _FadeEnd;

            fixed4 frag(v2f_img input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.uv);
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, input.uv.y);
                color.rgb *= fade;
                color.a *= fade;
                return color;
            }
            ENDCG
        }
    }
}
