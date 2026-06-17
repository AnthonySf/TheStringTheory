Shader "Hidden/EnviroBlitThrough"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off
        Pass
        { 
            Name "EnviroBlitThrough"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag    
            #pragma multi_compile __ ENVIROURP 
            
            #if defined (ENVIROURP)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionHCS   : POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4  positionCS  : SV_POSITION;
                float2  uv          : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Note: The pass is setup with a mesh already in CS
                // Therefore, we can just output vertex position
                // See RenderingUtils.fullscreenMesh
                output.positionCS = float4(input.positionHCS.xyz, 1.0);

                #if UNITY_UV_STARTS_AT_TOP
                output.positionCS.y *= -1;
                #endif

                output.uv = input.uv;
                return output;
            }

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);

            half4 frag (Varyings input) : SV_Target
            {
			    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv);
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
