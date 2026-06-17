Shader "Custom/TabsMountainSilhouette"
{
    Properties
    {
        _Color ("Color", Color) = (0.02, 0.04, 0.10, 0.25)
        _BaseColor ("Base Color", Color) = (0.02, 0.04, 0.10, 0.25)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 8
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = _Color.a > 0.0001 ? _Color : _BaseColor;
                return color;
            }
            ENDCG
        }
    }
}
