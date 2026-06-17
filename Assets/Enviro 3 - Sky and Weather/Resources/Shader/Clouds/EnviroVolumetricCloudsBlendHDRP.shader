Shader "Hidden/EnviroVolumetricCloudsBlendHDRP"
{
    Properties 
    {
        //_MainTex ("Texture", any) = "white" {}
    }
    SubShader
    { 
        Tags { "RenderType"="Opaque"}

        LOD 100

      	Pass
        {
            Cull Off ZWrite Off ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert 
            #pragma fragment frag
            #pragma multi_compile __ ENVIRO_DEPTH_BLENDING
            #pragma multi_compile __ ENVIROHDRP  
              
            #if defined (ENVIROHDRP)
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/FXAA.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/AtmosphericScattering/AtmosphericScattering.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"
           #include_with_pragmas "../Includes/FogIncludeHLSL.hlsl"

            TEXTURE2D_X(_MainTex);
            TEXTURE2D_X(_DownsampledDepth);

            TEXTURE2D_X (_CloudTex);
            SAMPLER(sampler_CloudTex);
      
            SamplerState Point_Clamp_Sampler;

            float4 _CloudTex_TexelSize;  
            float4 _MainTex_TexelSize; 
            float4 _HandleScales; 
            float4 _DepthHandleScale;
            float4 _ProjectionExtents;
            float4 _ProjectionExtentsRight;
            float4x4 _CamToWorld;
            float3 color;
	        float3 opacity;
            float _EnviroSkyIntensity;
            
            struct appdata
            {
                uint vertexID : SV_VertexID;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;        
                float2 vsray : TEXCOORD1;
#ifdef ENVIRO_DEPTH_BLENDING
                float2 uv00 : TEXCOORD2; 
                float2 uv10 : TEXCOORD3;
                float2 uv01 : TEXCOORD4;
                float2 uv11 : TEXCOORD5;
#endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;          
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.vertex = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv = GetFullScreenTriangleTexCoord(v.vertexID);

                if(unity_StereoEyeIndex == 0) 
                    o.vsray = (2.0 * o.uv * (1/_RTHandleScale.xy) - 1.0) * _ProjectionExtents.xy + _ProjectionExtents.zw;
                else
                    o.vsray = (2.0 * o.uv * (1/_RTHandleScale.xy) - 1.0) * _ProjectionExtentsRight.xy + _ProjectionExtentsRight.zw;

     
#ifdef ENVIRO_DEPTH_BLENDING
                o.uv00 = o.uv - 0.5 * _CloudTex_TexelSize.xy;
                o.uv10 = o.uv00 + float2(_CloudTex_TexelSize.x, 0.0);
                o.uv01 = o.uv00 + float2(0.0, _CloudTex_TexelSize.y);
                o.uv11 = o.uv00 + _CloudTex_TexelSize.xy;
#endif
                return o; 
            } 

#ifdef ENVIRO_DEPTH_BLENDING
            float4 Upsample(v2f i)  
            { 
                float4 lowResDepth = 0.0f;
                float highResDepth = Linear01Depth(LOAD_TEXTURE2D_X_LOD(_CameraDepthTexture, i.uv * _ScreenSize.xy * (1/_RTHandleScale.xy), 0), _ZBufferParams);
  
                lowResDepth.x = Linear01Depth(LOAD_TEXTURE2D_X(_DownsampledDepth,i.uv00 * _DepthHandleScale.zw * _DepthHandleScale.xy).r, _ZBufferParams);
                lowResDepth.y = Linear01Depth(LOAD_TEXTURE2D_X(_DownsampledDepth,i.uv10 * _DepthHandleScale.zw * _DepthHandleScale.xy).r, _ZBufferParams);
                lowResDepth.z = Linear01Depth(LOAD_TEXTURE2D_X(_DownsampledDepth,i.uv01 * _DepthHandleScale.zw * _DepthHandleScale.xy).r, _ZBufferParams);
                lowResDepth.w = Linear01Depth(LOAD_TEXTURE2D_X(_DownsampledDepth,i.uv11 * _DepthHandleScale.zw * _DepthHandleScale.xy).r, _ZBufferParams); 

                float4 depthDiff = abs(lowResDepth - highResDepth);
                float accumDiff = dot(depthDiff, float4(1, 1, 1, 1));

                [branch]
                if (accumDiff < 1.5f)
                {
                    //float3 uv = float3(i.uv * _HandleScales.xy,unity_StereoEyeIndex);
                    //return _CloudTex.Sample(sampler_CloudTex, uv);
                    return SAMPLE_TEXTURE2D_X_LOD(_CloudTex,sampler_CloudTex, i.uv * _HandleScales.xy, 0);
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

                //float3 uv = float3(nearestUv * _HandleScales.xy,unity_StereoEyeIndex);
                //return _CloudTex.Sample(Point_Clamp_Sampler, uv );
                return SAMPLE_TEXTURE2D_X_LOD(_CloudTex,Point_Clamp_Sampler, nearestUv * _HandleScales.xy, 0);
                }
            }
#endif
  

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                float4 vspos = float4(i.vsray, 1.0, 1.0);

                float4 worldPos = mul(_CamToWorld,vspos); 
                float3 viewDir = normalize(worldPos.xyz - _WorldSpaceCameraPos);
                float4 sourceColor = SAMPLE_TEXTURE2D_X_LOD(_MainTex,s_trilinear_clamp_sampler, i.uv, 0);
                float4 cloudsColor = float4(0,0,0,0);
#ifdef ENVIRO_DEPTH_BLENDING
                cloudsColor = Upsample(i);
#else
                cloudsColor = SAMPLE_TEXTURE2D_X_LOD(_CloudTex,sampler_CloudTex, i.uv * _HandleScales.xy, 0);
#endif              
                

                float4 finalColor = GetCloudColor(cloudsColor,worldPos); 
                    finalColor.rgb *= _EnviroSkyIntensity * GetCurrentExposureMultiplier();
                float rawDepth = LOAD_TEXTURE2D_X_LOD(_CameraDepthTexture, i.uv * _ScreenSize.xy * (1/_RTHandleScale.xy), 0);
                float sceneDepth = Linear01Depth(rawDepth, _ZBufferParams);

#if ENVIRO_DEPTH_BLENDING
                float4 final = float4(sourceColor.rgb * (1 - finalColor.a) + lerp(sourceColor.rgb, finalColor.rgb, finalColor.a) * finalColor.a, 1);
#else 
                float4 final = sourceColor;

                if (sceneDepth == 1.0f) 
                    final = half4(sourceColor.rgb * saturate(1 - finalColor.a) + lerp(sourceColor.rgb, finalColor.rgb, finalColor.a) * finalColor.a, 1); 
#endif       
                // HDRP Fog 
                /*if (sceneDepth == 1.0f)
                {             
                    PositionInputs posInput = GetPositionInput(i.vertex.xy, _ScreenSize.zw, rawDepth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
                    float3 V = GetSkyViewDirWS(i.uv * _ScreenSize.xy * (1/_RTHandleScale.xy)); 
                    posInput.positionWS = GetCurrentViewPosition() - V * _MaxFogDistance;
                    EvaluateAtmosphericScattering(posInput, V, color, opacity);
                    final.rgb = color + (1 - opacity) * final.rgb;
                }*/
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
