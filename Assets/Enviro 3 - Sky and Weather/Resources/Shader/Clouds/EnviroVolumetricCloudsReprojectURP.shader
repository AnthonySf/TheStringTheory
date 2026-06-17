Shader "Hidden/EnviroVolumetricCloudsReprojectURP"
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
            #pragma multi_compile _ ENVIRO_DEPTH_BLENDING
            #pragma multi_compile _ ENVIROURP

            #if defined (ENVIROURP)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            float4 _MainTex_TexelSize;
            float4 _UndersampleCloudTex_TexelSize;

            TEXTURE2D_X(_MainTex);
            SAMPLER (sampler_MainTex);

            TEXTURE2D_X(_UndersampleCloudTex);
            SAMPLER (sampler_UndersampleCloudTex);
            
			TEXTURE2D_X_FLOAT(_DownsampledDepth);
            SAMPLER (sampler_DownsampledDepth);

            float4x4 _PrevVP;
            float4x4 _PrevVPRight;
            float4x4 _CamToWorld;

            float4 _ProjectionExtents;
            float4 _ProjectionExtentsRight;

            float2 _TexelSize;
            float _BlendTime;
            float _EnviroCloudHistoryReset;
            float3 _WorldOffset;
  

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
                float2 ray : TEXCOORD1;
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

                if(unity_StereoEyeIndex == 0) 
                    o.ray = (2.0 * o.uv - 1.0) * _ProjectionExtents.xy + _ProjectionExtents.zw;
                else
                    o.ray = (2.0 * o.uv - 1.0) * _ProjectionExtentsRight.xy + _ProjectionExtentsRight.zw;

                return o;
            } 


            float2 PrevUV(float4 wspos, out half outOfBound) 
            {
                float4x4 _prev = _PrevVP;

                if(unity_StereoEyeIndex == 1) 
                _prev = _PrevVPRight;

                float4 prevClip = mul(_prev, wspos);
                prevClip.xy /= prevClip.w;
                prevClip.xy = prevClip.xy * 0.5 + 0.5;

                half oobmax = max(0.0 - prevClip.x, 0.0 - prevClip.y);
                half oobmin = max(prevClip.x - 1.0, prevClip.y - 1.0);

                outOfBound = step(0, max(oobmin, oobmax));

                return prevClip.xy;
            }

            float4 ClipAABB(float4 aabbMin, float4 aabbMax, float4 prevSample)
            {
                float4 p_clip = 0.5 * (aabbMax + aabbMin);
                float4 e_clip = 0.5 * (aabbMax - aabbMin);

                float4 v_clip = prevSample - p_clip;
                float4 v_unit = v_clip / e_clip;
                float4 a_unit = abs(v_unit);
                float ma_unit = max(max(a_unit.x, max(a_unit.y, a_unit.z)), a_unit.w);

                if (ma_unit > 1.0)
                    return p_clip + v_clip / ma_unit;
                else
                    return prevSample;
            } 

        
            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 vspos = float3(i.ray, 1.0);
       
                float4 raymarchResult = SAMPLE_TEXTURE2D_X(_UndersampleCloudTex, sampler_UndersampleCloudTex, i.uv);
                if (_EnviroCloudHistoryReset > 0.5)
                    return raymarchResult;

                float distance = raymarchResult.y;		
               // float intensity = raymarchResult.x;
                half outOfBound;
 
                float4 worldPos = mul(_CamToWorld, float4(normalize(vspos) * distance, 1.0));       
                worldPos /= worldPos.w;
                //worldPos.xyz -= _WorldOffset;
                
                float2 prevUV = PrevUV(worldPos, outOfBound);          
                {	 

                    float4 prevSample = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, prevUV);
 
                    float4 m1 = float4(0.0f,0.0f,0.0f,0.0f); 
                    float4 m2 = float4(0.0f,0.0f,0.0f,0.0f);
                    
                    float sampleCount = 1.0f;
                    float originalPointDepth = LinearEyeDepth(SAMPLE_TEXTURE2D_X(_DownsampledDepth, sampler_DownsampledDepth,i.uv).r, _ZBufferParams);
                    [unroll]
                    for (int x = -1; x <= 1; x ++) 
                    { 
                        [unroll]
                        for (int y = -1; y <= 1; y ++ ) 
                        {
                            float4 val;
                            if (x == 0 && y == 0) 
                            { 
                                val = raymarchResult;
                                m1 += val;
                                m2 += val * val;
                            }
                            else 
                            {
                                float2 uv = i.uv + float2(x * _UndersampleCloudTex_TexelSize.x, y * _UndersampleCloudTex_TexelSize.y);
                                val = SAMPLE_TEXTURE2D_X(_UndersampleCloudTex,sampler_UndersampleCloudTex, uv);      
                                float depth = LinearEyeDepth(SAMPLE_TEXTURE2D_X(_DownsampledDepth,sampler_DownsampledDepth,uv).r, _ZBufferParams);
 
                                if (abs(originalPointDepth - depth) < (pow(max(1, depth), 1.5f) / 500.0f))
                                {
                                    m1 += val;
                                    m2 += val * val;
                                    sampleCount += 1.0f;
                                }
                            }
                        }
                    }

                    float gamma = _BlendTime;
                    float4 mu = m1 / sampleCount;
                    float4 sigma = sqrt(abs(m2 / sampleCount - mu * mu));
                    float4 minc = mu - gamma * sigma;
                    float4 maxc = mu + gamma * sigma;
                    prevSample = ClipAABB(minc, maxc, prevSample);	

                    //Blend.
                    raymarchResult = lerp(prevSample, raymarchResult, max(0.001f, outOfBound));
                   
                }
                return raymarchResult;
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
