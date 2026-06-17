Shader "Hidden/EnviroCloudsRaymarchHDRP"
{
    Properties
    { 

    } 
    SubShader 
    {
        //Tags { "RenderType"="Opaque" }

       	Pass 
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ ENVIRO_DEPTH_BLENDING
            #pragma multi_compile_local _ ENVIRO_CLOUD_SHADOWS
            #pragma multi_compile_local _ ENVIRO_LIGHTNING
            #pragma multi_compile_local _ ENVIRO_VARIABLE_BOTTOM
            #pragma multi_compile __ ENVIROHDRP 
               
            #if defined (ENVIROHDRP)
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/FXAA.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/PostProcessing/Shaders/RTUpscale.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/AtmosphericScattering/AtmosphericScattering.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"
            #include_with_pragmas "../Includes/VolumetricCloudsInclude.cginc"
            #include_with_pragmas "../Includes/VolumetricCloudsTexHDRPInclude.cginc"

		
			int _Frame;
            uniform float _BlueNoiseIntensity;
            uniform float4x4 _EnviroStageCloudPitchMatrix;
            uniform float4 _DepthHandleScale;

            struct appdata
            {
                uint vertexID : SV_VertexID;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.position = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv = GetFullScreenTriangleTexCoord(v.vertexID);

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);  

               // uint2 positionSS = i.uv * _ScreenSize.xy;

                float4 cameraRay =  float4(i.uv * 2.0 - 1.0, 1.0, 1.0);
                float3 EyePosition = _CameraPosition;
                float3 ray = 0; 
 
               	//if (unity_StereoEyeIndex == 0)
	            //{
                    cameraRay = mul(_InverseProjection, cameraRay);
                    cameraRay = cameraRay / cameraRay.w;
                    float3 pitchedCameraRay = mul((float3x3)_EnviroStageCloudPitchMatrix, cameraRay.xyz);
                    ray = normalize(mul((float3x3)_InverseRotation, pitchedCameraRay));
                //}
                //else  
                // {
                //     cameraRay = mul(_InverseProjectionRight, cameraRay);
                //    cameraRay = cameraRay / cameraRay.w; 
                //    float3 pitchedCameraRayRight = mul((float3x3)_EnviroStageCloudPitchMatrix, cameraRay.xyz);
                //    ray = normalize(mul((float3x3)_InverseRotationRight, pitchedCameraRayRight));
                //}
  
                float rayLength = length(ray);
                
                float sceneDepth = LOAD_TEXTURE2D_X(_DownsampledDepth, i.uv * _DepthHandleScale.zw);  
                //float sceneDepth = LOAD_TEXTURE2D_X_LOD(_CameraDepthTexture, i.uv * _ScreenSize.xy, 0);
                float raymarchEnd = GetRaymarchEndFromSceneDepth(Linear01Depth(sceneDepth, _ZBufferParams), 1000000); //* rayLenght

                float offset = tex2D(_BlueNoise, squareUV(i.uv + _Randomness.xy)).x * _BlueNoiseIntensity;  

                float3 pCent = float3(EyePosition.x, -_CloudsParameter.w, EyePosition.z);

                float intensity, distance, alpha, shadow = 0.0f;
               
                RaymarchParameters parameters;
                InitRaymarchParameters(parameters);
                float2 hitDistance = ResolveRay(EyePosition,ray,pCent,raymarchEnd,parameters);
                float3 result = Raymarch(EyePosition,ray,hitDistance,pCent,parameters,offset);
#if ENVIRO_CLOUD_SHADOWS
                float3 wpos = CalculateWorldPosition(i.uv,sceneDepth) - _WorldOffset;
                shadow = RaymarchShadows(EyePosition,wpos,ray,pCent,parameters,offset,sceneDepth);
#endif
                intensity = result.r;
                distance = result.g;
                alpha = result.b;

                return float4(max(intensity,0.0),max(distance,1.0f),clamp(shadow,0.0,0.25),saturate(alpha)); 
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
