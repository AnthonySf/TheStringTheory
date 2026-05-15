Shader "Custom/HighwayCloudEdgeGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EdgeWidth ("Edge Width", Range(0.5, 4.0)) = 1.4
        _LeftBias ("Left Bias", Range(0.5, 6.0)) = 2.2
        _VerticalFocus ("Vertical Focus", Range(0.2, 2.5)) = 1.1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _EdgeWidth;
            float _LeftBias;
            float _VerticalFocus;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sample = tex2D(_MainTex, i.texcoord);
                float alpha = sample.a;
                if (alpha <= 0.001f)
                    return 0;

                float2 stepUV = _MainTex_TexelSize.xy * _EdgeWidth;
                float alphaLeft = tex2D(_MainTex, i.texcoord + float2(-stepUV.x, 0)).a;
                float alphaRight = tex2D(_MainTex, i.texcoord + float2(stepUV.x, 0)).a;
                float alphaUp = tex2D(_MainTex, i.texcoord + float2(0, stepUV.y)).a;
                float alphaDown = tex2D(_MainTex, i.texcoord + float2(0, -stepUV.y)).a;

                float neighborMin = min(min(alphaLeft, alphaRight), min(alphaUp, alphaDown));
                float edge = saturate((alpha - neighborMin) * 5.0f);
                edge = smoothstep(0.04f, 0.40f, edge);

                float leftWeight = pow(saturate(1.0f - i.texcoord.x), _LeftBias);
                float vertical = saturate(1.0f - abs(i.texcoord.y - 0.42f) / 0.70f);
                vertical = pow(vertical, _VerticalFocus);

                float glow = edge * leftWeight * vertical * i.color.a;
                return fixed4(i.color.rgb * glow, glow);
            }
            ENDCG
        }
    }
}
