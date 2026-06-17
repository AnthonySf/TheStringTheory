Shader "Custom/TabsDomeStars"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.78, 0.90, 1.0, 1.0)
        _Brightness ("Brightness", Float) = 1.0
        _TwinkleStrength ("Twinkle Strength", Range(0, 1)) = 0.35
        _TwinkleSpeed ("Twinkle Speed", Float) = 0.65
        _StageTime ("Stage Time", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Tint;
            float _Brightness;
            float _TwinkleStrength;
            float _TwinkleSpeed;
            float _StageTime;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 twinkle : TEXCOORD1;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.twinkle = v.uv2;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centered = i.uv * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(centered, centered));
                float core = pow(radial, 5.0);
                float glow = pow(radial, 1.45) * 0.33;
                float twinkle = 1.0 + sin(_StageTime * _TwinkleSpeed + i.twinkle.x) * _TwinkleStrength * lerp(0.35, 1.0, i.twinkle.y);

                fixed4 color = i.color * _Tint;
                float alpha = saturate((core + glow) * i.color.a * _Brightness * twinkle);
                color.rgb *= (core * 1.35 + glow) * _Brightness * twinkle;
                color.a = alpha;
                return color;
            }
            ENDCG
        }
    }
}
