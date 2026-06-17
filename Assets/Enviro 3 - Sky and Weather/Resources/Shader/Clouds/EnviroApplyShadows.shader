Shader "Hidden/EnviroApplyShadows"
{
    Properties
    {
        //_MainTex ("Texture", any) = "white" {}
        //_CloudsTex ("Texture", any) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass 
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ ENVIROURP

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                #if defined(ENVIROURP)
		        o.vertex = float4(v.vertex.xyz,1.0);
		        #if UNITY_UV_STARTS_AT_TOP
                o.vertex.y *= -1;
                #endif
                #else
		        o.vertex = UnityObjectToClipPos(v.vertex);
                #endif  
                o.uv = v.uv;
                return o;
            }

            float _Intensity;

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_CloudsTex);
         

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float4 sceneColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex,i.uv);
                float4 cloudTex = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CloudsTex,i.uv);
                float shadowsClouds = saturate(1-(cloudTex.b * _Intensity));
              // shadowsClouds = shadowsClouds * ;
               
                float4 final = float4(sceneColor.rgb * shadowsClouds, sceneColor.a);
                return final;
            }
            ENDCG
        }
    }
}
