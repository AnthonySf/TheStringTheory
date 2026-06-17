Shader "Hidden/EnviroApplyShadowsURP"
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
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ ENVIROURP

            #if defined (ENVIROURP)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"


            struct appdata
            {
                uint vertex : SV_VertexID;
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
		        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 pos = GetFullScreenTriangleVertexPosition(v.vertex);
                float2 uv  = GetFullScreenTriangleTexCoord(v.vertex);
             
                o.vertex = pos;
                o.uv  = DYNAMIC_SCALING_APPLY_SCALEBIAS(uv);
                return o;
            }

            float _Intensity;
            TEXTURE2D_X(_MainTex);
            SAMPLER (sampler_MainTex);
            TEXTURE2D_X(_CloudsTex);
            SAMPLER (sampler_CloudsTex);

           // UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
           // UNITY_DECLARE_SCREENSPACE_TEXTURE(_CloudsTex);
         

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float4 sceneColor = SAMPLE_TEXTURE2D_X(_MainTex,sampler_MainTex,i.uv);
                float4 cloudTex = SAMPLE_TEXTURE2D_X(_CloudsTex,sampler_CloudsTex,i.uv);
                float shadowsClouds = saturate(1-(cloudTex.b * _Intensity));
              // shadowsClouds = shadowsClouds * ;
               
                float4 final = float4(sceneColor.rgb * shadowsClouds, sceneColor.a);
                return final;
            }
            #else

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;

            float4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                // just invert the colors
                col.rgb = 1 - col.rgb;
                return col;
            }
            #endif
            ENDHLSL
        }
    }
}
