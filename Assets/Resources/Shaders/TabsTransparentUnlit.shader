Shader "Custom/TabsTransparentUnlit"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [HDR]_EmissionColor ("Emission Color", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

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
            Cull Off
            ZWrite Off
            ZTest LEqual

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
