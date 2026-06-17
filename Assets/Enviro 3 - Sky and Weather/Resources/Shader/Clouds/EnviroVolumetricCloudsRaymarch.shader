Shader "Hidden/EnviroCloudsRaymarch"
{
    Properties
    { 
        //_MainTex ("Texture", any) = "white" {}
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
            #pragma multi_compile_local _ ENVIRO_DEPTH_BLENDING
            #pragma multi_compile_local _ ENVIRO_CLOUD_SHADOWS
            #pragma multi_compile_local _ ENVIRO_LIGHTNING
            #pragma multi_compile_local _ ENVIRO_VARIABLE_BOTTOM
            #pragma multi_compile _ ENVIROURP
            #include "UnityCG.cginc"
            #include_with_pragmas "../Includes/VolumetricCloudsInclude.cginc"
            #include_with_pragmas "../Includes/VolumetricCloudsTexInclude.cginc"
 
            int _Frame;
            uniform float _BlueNoiseIntensity;
            uniform float4x4 _EnviroStageCloudPitchMatrix;
            float4 _CameraDepthTexture_TexelSize;
     
            struct v2f
            {
                float4 position : SV_POSITION;
		        float2 uv : TEXCOORD0;
                float2 uv00 : TEXCOORD1;
                float2 uv10 : TEXCOORD2;
                float2 uv01 : TEXCOORD3;
                float2 uv11 : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct appdata 
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert (appdata_img v)
            {
                v2f o; 
                 
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);   
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                #if defined(ENVIROURP)
		        o.position = float4(v.vertex.xyz,1.0);
		        #if UNITY_UV_STARTS_AT_TOP
                o.position.y *= -1;
                #endif
                #else
		        o.position = UnityObjectToClipPos(v.vertex);
                #endif   

                o.uv = v.texcoord;
                o.uv00 = v.texcoord - 0.5 * _CameraDepthTexture_TexelSize.xy;
                o.uv10 = o.uv00 + float2(_CameraDepthTexture_TexelSize.x, 0.0);
                o.uv01 = o.uv00 + float2(0.0, _CameraDepthTexture_TexelSize.y);
                o.uv11 = o.uv00 + _CameraDepthTexture_TexelSize.xy;
                return o;
            } 

             
            float4 frag (v2f i) : SV_Target
            { 
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float4 cameraRay =  float4(i.uv * 2.0 - 1.0, 1.0, 1.0);
                float3 EyePosition = _CameraPosition;
                float3 ray = 0; 
 
               	if (unity_StereoEyeIndex == 0)
	            {
                    cameraRay = mul(_InverseProjection, cameraRay);
                    cameraRay = cameraRay / cameraRay.w;
                    float3 pitchedCameraRay = mul((float3x3)_EnviroStageCloudPitchMatrix, cameraRay.xyz);
                    ray = normalize(mul((float3x3)_InverseRotation, pitchedCameraRay));
                }
                else  
                {
                    cameraRay = mul(_InverseProjectionRight, cameraRay);
                    cameraRay = cameraRay / cameraRay.w; 
                    float3 pitchedCameraRayRight = mul((float3x3)_EnviroStageCloudPitchMatrix, cameraRay.xyz);
                    ray = normalize(mul((float3x3)_InverseRotationRight, pitchedCameraRayRight));
                }
  
                float rayLength = length(ray);
      
                float sceneDepth = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_DownsampledDepth, UnityStereoTransformScreenSpaceTex(i.uv));
                float3 cameraDirection = -1 * transpose(_InverseRotation)[2].xyz;
                float fwdFactor = dot(ray, cameraDirection); 

                float raymarchEnd = GetRaymarchEndFromSceneDepth(Linear01Depth(sceneDepth) / fwdFactor, 1000000);
 
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
            ENDCG
        }
    }
}
