Shader "Hidden/EnviroVolumetricCloudsBlendURP"
{
    Properties 
    {
        _MainTex ("Texture", any) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
 
      	Pass
        {
            Cull Off ZWrite Off ZTest Always

            HLSLPROGRAM
            #pragma vertex vert 
            #pragma fragment frag
            #pragma multi_compile __ ENVIRO_DEPTH_BLENDING
            #pragma multi_compile __ ENVIROURP
            #pragma multi_compile __ UNITY_COLORSPACE_GAMMA

            #if defined (ENVIROURP)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include_with_pragmas "../Includes/FogIncludeHLSL.hlsl"

            TEXTURE2D_X(_MainTex);
            SAMPLER (sampler_MainTex);

            TEXTURE2D_X(_CloudTex);
            SAMPLER (sampler_CloudTex);

            TEXTURE2D_X_FLOAT(_DownsampledDepth);
            SAMPLER (sampler_DownsampledDepth);
            
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER (sampler_CameraDepthTexture);

            float4 _CloudTex_TexelSize;  
            float4 _MainTex_TexelSize; 

            SamplerState Point_Clamp_Sampler;

            float4 _ProjectionExtents;
            float4 _ProjectionExtentsRight;

            float4x4 _CamToWorld;
            

            struct appdata
            {
                uint vertex : SV_VertexID;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
 
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                //float4 screenPos : TEXCOORD1;         
                float2 vsray : TEXCOORD1;
                //half3 pos : TEXCOORD2;
//#ifdef ENVIRO_DEPTH_BLENDING
                float2 uv00 : TEXCOORD2; 
                float2 uv10 : TEXCOORD3;
                float2 uv01 : TEXCOORD4;
                float2 uv11 : TEXCOORD5;
//#endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
               
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                //o.pos = -v.vertex; 

                float4 pos = GetFullScreenTriangleVertexPosition(v.vertex);
                float2 uv  = DYNAMIC_SCALING_APPLY_SCALEBIAS(GetFullScreenTriangleTexCoord(v.vertex));
             
                o.vertex = pos;
                o.uv  = uv;


                if(unity_StereoEyeIndex == 0) 
                    o.vsray = (2.0 * o.uv - 1.0) * _ProjectionExtents.xy + _ProjectionExtents.zw;
                else
                    o.vsray = (2.0 * o.uv - 1.0) * _ProjectionExtentsRight.xy + _ProjectionExtentsRight.zw;

              //  o.screenPos = ComputeScreenPos(o.vertex);
     
//#ifdef ENVIRO_DEPTH_BLENDING
                o.uv00 = o.uv - 0.5 * _CloudTex_TexelSize.xy;
                o.uv10 = o.uv00 + float2(_CloudTex_TexelSize.x, 0.0);
                o.uv01 = o.uv00 + float2(0.0, _CloudTex_TexelSize.y);
                o.uv11 = o.uv00 + _CloudTex_TexelSize.xy;
//#endif
                return o; 
            }

//#ifdef ENVIRO_DEPTH_BLENDING
            float4 Upsample(v2f i) 
            { 
                float4 lowResDepth = 0.0f;
                float highResDepth = LinearEyeDepth(SAMPLE_TEXTURE2D_X(_CameraDepthTexture,sampler_CameraDepthTexture,i.uv).r, _ZBufferParams);

                lowResDepth.x = LinearEyeDepth(SAMPLE_TEXTURE2D_X(_DownsampledDepth,sampler_DownsampledDepth,i.uv00).r, _ZBufferParams );
                lowResDepth.y = LinearEyeDepth(SAMPLE_TEXTURE2D_X(_DownsampledDepth,sampler_DownsampledDepth,i.uv10).r, _ZBufferParams);
                lowResDepth.z = LinearEyeDepth(SAMPLE_TEXTURE2D_X(_DownsampledDepth,sampler_DownsampledDepth,i.uv01).r, _ZBufferParams);
                lowResDepth.w = LinearEyeDepth(SAMPLE_TEXTURE2D_X(_DownsampledDepth,sampler_DownsampledDepth,i.uv11).r, _ZBufferParams); 

                float4 depthDiff = abs(lowResDepth - highResDepth);
                float accumDiff = dot(depthDiff, float4(1, 1, 1, 1));

                [branch]
                if (accumDiff < 1.5f)
                {
                    
                    return SAMPLE_TEXTURE2D_X(_CloudTex,sampler_CloudTex,i.uv);

#ifdef STEREO_INSTANCING_ON
                //float3 uv = float3(i.uv,unity_StereoEyeIndex);
                //return _CloudTex.Sample(sampler_CloudTex, uv);
#else
                //return _CloudTex.Sample(sampler_CloudTex, i.uv); 
#endif 
                }
                else
                {
                    float minDepthDiff = depthDiff[0];
                    float2 nearestUv = i.uv00;

                    if (depthDiff[1] < minDepthDiff)
                    { 
                        nearestUv = i.uv10;
                        minDepthDiff = depthDiff[1];
                    }

                    if (depthDiff[2] < minDepthDiff)
                    {
                        nearestUv = i.uv01;
                        minDepthDiff = depthDiff[2];
                    }

                    if (depthDiff[3] < minDepthDiff)
                    {
                        nearestUv = i.uv11; 
                        minDepthDiff = depthDiff[3];
                    }

                    return SAMPLE_TEXTURE2D_X(_CloudTex,Point_Clamp_Sampler,nearestUv);

#ifdef STEREO_INSTANCING_ON
  //              float3 uv = float3(UnityStereoTransformScreenSpaceTex(nearestUv),unity_StereoEyeIndex);
   //             return _CloudTex.Sample(Point_Clamp_Sampler, uv);
#else
      //          return _CloudTex.Sample(Point_Clamp_Sampler, UnityStereoTransformScreenSpaceTex(nearestUv)); 
#endif
                }
            }
//#endif


            half3 LinearToGammaSpace (half3 linRGB)
            {
                linRGB = max(linRGB, half3(0.h, 0.h, 0.h));
                return max(1.055h * pow(linRGB, 0.416666667h) - 0.055h, 0.h);
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                float4 vspos = float4(i.vsray, 1.0, 1.0);

                float4 worldPos = mul(_CamToWorld,vspos);

                float3 viewDir = normalize(worldPos.xyz - _WorldSpaceCameraPos);

                float4 sourceColor = SAMPLE_TEXTURE2D_X(_MainTex,sampler_MainTex,i.uv);
                 
                float4 cloudsColor = Upsample(i);     
                     
                float4 finalColor = GetCloudColor(cloudsColor,worldPos); 


                #if defined(UNITY_COLORSPACE_GAMMA)
				finalColor.rgb = LinearToGammaSpace(finalColor.rgb);
			    #endif

#if ENVIRO_DEPTH_BLENDING
                float4 final = float4(sourceColor.rgb * (1 - finalColor.a) + lerp(sourceColor.rgb, finalColor.rgb, finalColor.a) * finalColor.a, 1);
                return final;
#else
                float4 final = sourceColor; 

                float sceneDepth = Linear01Depth(SAMPLE_TEXTURE2D_X(_CameraDepthTexture,sampler_CameraDepthTexture, i.uv).r, _ZBufferParams);
                
                if (sceneDepth >= 0.99f) 
                    final = float4(sourceColor.rgb * saturate(1 - finalColor.a) + lerp(sourceColor.rgb, finalColor.rgb, finalColor.a) * finalColor.a, 1);
 
                return final;  
#endif
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
