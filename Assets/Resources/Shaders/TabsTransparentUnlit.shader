Shader "Custom/TabsTransparentUnlit"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [HDR]_EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4
        [Enum(Off,0,On,1)] _ZWrite ("Z Write", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull [_Cull]
        ZWrite [_ZWrite]
        ZTest [_ZTest]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _BaseColor;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseColor = _Color.a > 0.0001 ? _Color : _BaseColor;
                return fixed4(baseColor.rgb, max(baseColor.a, _BaseColor.a));
            }
            ENDCG
        }

        Pass
        {
            Blend SrcAlpha One
            Cull [_Cull]
            ZWrite Off
            ZTest [_ZTest]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragGlow
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _BaseColor;
            fixed4 _EmissionColor;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed3 CompressEmission(fixed3 emission)
            {
                return 1.0 - exp(-emission * 0.09);
            }

            fixed4 fragGlow(v2f i) : SV_Target
            {
                fixed4 baseColor = _Color.a > 0.0001 ? _Color : _BaseColor;
                fixed alpha = max(baseColor.a, _BaseColor.a);
                fixed3 glow = CompressEmission(max(_EmissionColor.rgb, 0));
                return fixed4(glow, alpha);
            }
            ENDCG
        }
    }
}
