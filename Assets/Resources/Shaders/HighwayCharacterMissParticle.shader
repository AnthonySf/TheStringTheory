Shader "Custom/HighwayCharacterMissParticle"
{
    Properties
    {
        _Color ("Core Color", Color) = (1, 0.44, 0.16, 1)
        _EdgeColor ("Edge Color", Color) = (1, 0.84, 0.42, 1)
        _Glow ("Glow", Range(0, 4)) = 1.55
        _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        ZTest [_ZTest]
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _EdgeColor;
            float _Glow;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = (i.uv * 2.0) - 1.0;
                float2 sparkUv = float2(uv.x * 0.78, uv.y * 1.5);
                float radial = saturate(1.0 - length(sparkUv));
                float softBody = radial * radial;
                float core = pow(saturate(1.0 - length(sparkUv * 1.65)), 4.0);
                float halo = pow(saturate(1.0 - length(sparkUv * 0.92)), 1.8);

                fixed4 tint = lerp(_Color, _EdgeColor, core);
                tint *= i.color;
                tint.rgb *= 0.65 + (_Glow * (0.45 + (core * 0.55)));
                tint.a *= softBody * (0.35 + (halo * 0.65));
                return tint;
            }
            ENDCG
        }
    }
}
