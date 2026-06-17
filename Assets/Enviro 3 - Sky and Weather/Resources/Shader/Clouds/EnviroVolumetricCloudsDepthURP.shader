Shader "Hidden/EnviroVolumetricCloudsDepthURP"
{
    Properties
    {
        _MainTex ("Texture", any) = "white" {}
    }
    SubShader 
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        //Pass 1 downsample
       	Pass 
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ ENVIROURP

        #if defined (ENVIROURP)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"


			TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER (sampler_CameraDepthTexture);
            float4 _CameraDepthTexture_TexelSize;

            struct appdata
            {
                uint vertex : SV_VertexID;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv00 : TEXCOORD1;
                float2 uv10 : TEXCOORD2;
                float2 uv01 : TEXCOORD3;
                float2 uv11 : TEXCOORD4;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v); 
		        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 pos = GetFullScreenTriangleVertexPosition(v.vertex);
                float2 uv  = GetFullScreenTriangleTexCoord(v.vertex);
             
                o.vertex = pos;
                o.uv  = DYNAMIC_SCALING_APPLY_SCALEBIAS(uv);

                o.uv00 = DYNAMIC_SCALING_APPLY_SCALEBIAS(uv) - 0.5 * _CameraDepthTexture_TexelSize.xy;
                o.uv10 = o.uv00 + float2(_CameraDepthTexture_TexelSize.x, 0.0);
                o.uv01 = o.uv00 + float2(0.0, _CameraDepthTexture_TexelSize.y);
                o.uv11 = o.uv00 + _CameraDepthTexture_TexelSize.xy;

                return o;
            }

            float frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 
                float4 depth;
         
                depth[0] = SAMPLE_TEXTURE2D_X(_CameraDepthTexture,sampler_CameraDepthTexture,i.uv00).r;
                depth[1] = SAMPLE_TEXTURE2D_X(_CameraDepthTexture,sampler_CameraDepthTexture,i.uv10).r;
                depth[2] = SAMPLE_TEXTURE2D_X(_CameraDepthTexture,sampler_CameraDepthTexture,i.uv01).r;
                depth[3] = SAMPLE_TEXTURE2D_X(_CameraDepthTexture,sampler_CameraDepthTexture,i.uv11).r;
                return min(depth[0], min(depth[1], min(depth[2], depth[3])));
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

        //Pass 2 Copy
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ ENVIROURP

        #if defined (ENVIROURP)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"


			TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER (sampler_CameraDepthTexture);

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

            v2f vert(appdata v)
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

            float frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                return SAMPLE_TEXTURE2D_X(_CameraDepthTexture,sampler_CameraDepthTexture,i.uv).r;
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
