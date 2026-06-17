Shader "Hidden/EnviroHeightFogURP"
{ 
    Properties
    {
        _MainTex ("Texture", any) = "white"  {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ UNITY_COLORSPACE_GAMMA     
            #pragma multi_compile __ ENVIROURP

    #if defined (ENVIROURP)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include_with_pragmas "../Includes/FogIncludeHLSL.hlsl"

            struct appdata
            {
                uint vertex : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 position : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o; 
                UNITY_SETUP_INSTANCE_ID(v); 
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 
                float4 pos = GetFullScreenTriangleVertexPosition(v.vertex);
                float2 uv  = GetFullScreenTriangleTexCoord(v.vertex);
            
                o.position = pos;
                o.uv  = DYNAMIC_SCALING_APPLY_SCALEBIAS(uv);

                return o;
            }

            float4x4 _LeftWorldFromView;
            float4x4 _RightWorldFromView;
            float4x4 _LeftViewFromScreen;
            float4x4 _RightViewFromScreen;

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER (sampler_CameraDepthTexture);

            void InverseProjectDepth (float depth, float2 texcoord, out float3 worldPos)
            {
                float4x4 proj, eyeToWorld;

                if (unity_StereoEyeIndex == 0)
                {
                    proj 		= _LeftViewFromScreen;
                    eyeToWorld 	= _LeftWorldFromView;
                }
                else
                {
                    proj 		= _RightViewFromScreen;
                    eyeToWorld 	= _RightWorldFromView;
                }

                #if !UNITY_UV_STARTS_AT_TOP
                    //texcoord.y = 1 - texcoord.y;
                #endif
                
                float2 uvClip = texcoord * 2.0 - 1.0;
                float clipDepth = depth; // Fix for OpenGl Core thanks to Lars Bertram
				clipDepth = (UNITY_NEAR_CLIP_VALUE < 0) ? clipDepth * 2 - 1 : clipDepth;
                float4 clipPos = float4(uvClip, clipDepth, 1.0);
                float4 viewPos = mul(proj, clipPos); // inverse projection by clip position
                viewPos /= viewPos.w; // perspective division
                worldPos = mul(eyeToWorld, viewPos).xyz;
            }   
  

            float4 frag (v2f i) : SV_Target
            { 
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture,sampler_CameraDepthTexture, i.uv);
                float linearDepth = Linear01Depth(depth, _ZBufferParams);

                float3 worldPos;
                InverseProjectDepth(depth, i.uv.xy, worldPos);
 
                float4 col = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, i.uv); 
                float4 fog = GetExponentialHeightFog(worldPos,linearDepth);
                //this is not correct but LinearToGamma does produce even worse results.. 
                #if defined(UNITY_COLORSPACE_GAMMA) 
                fog.rgb *= 1.5;
                #endif

                float3 final = ApplyVolumetricLights(fog,col.rgb, i.uv);
                
                return float4(final.rgb,col.a);
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
