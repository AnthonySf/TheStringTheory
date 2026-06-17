Shader "Enviro3/Standard/WeatherTexture"  
{
	Properties 
	{
		_Coverage ("Coverage", Range(0,1)) = 0.5
		_Tiling ("Tiling", Range(1,100)) = 10
	}
	SubShader 
	{  
		Tags { "RenderType"="Opaque" }
		LOD 200
		Pass { 
			CGPROGRAM 
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			#include "../Includes/NoiseInclude.cginc"
 
			#pragma target 3.0
			#pragma exclude_renderers gles 

			sampler2D _MainTex;

		    struct VertexInput 
		    {
  				half4 vertex : POSITION;
 				float2 uv : TEXCOORD0;	
        	};

            struct VertexOutput 
			{
           		float4 position : SV_POSITION;
 				float2 uv : TEXCOORD0;
            }; 
			          
            VertexOutput vert (appdata_img v) 
			{
 			 	VertexOutput o;
 				o.position = UnityObjectToClipPos(v.vertex);				
 				o.uv = v.texcoord;
 				return o; 
            }       
 		     
 			float4x4 world_view_proj;
 
 			float _CoverageLayer1;  
			float _CloudsTypeLayer1;
			float _WorleyFreq1Layer1; 
			float _WorleyFreq2Layer1; 
			float _DilateCoverageLayer1; 
			float _DilateTypeLayer1;
			float _CloudsTypeModifierLayer1; 
			float4 _LocationOffset;
			float3 _WindDirectionLayer1;
		
 			float4 frag(VertexOutput input) : SV_Target
 			{  
				float2 uv = input.uv; 
 
				float2 windOffsetLayer1 = _WindDirectionLayer1.xy;
 
				//float2 fillerUV = uv.xy + windOffsetLayer1 + _LocationOffset.xy;
				//float covFiller = WorleyFBM2D(fillerUV,2,0.75) * 0.8; 
		 
				int freq1 = _WorleyFreq1Layer1;
				int freq2 = _WorleyFreq2Layer1;

				//Worley Noise
				float worley1Layer1 = WorleyFBM2D((windOffsetLayer1 + _LocationOffset.xy + uv.xy), freq1, 1.4);
				float worley2Layer1 = WorleyFBM2D((windOffsetLayer1 + _LocationOffset.xy + uv.xy), freq2, 2.2);
 
				float dilateCoverageLayer1 = lerp(worley1Layer1,worley2Layer1,_DilateCoverageLayer1); 

				//Coverage Layer
				float coverageLayer1 = saturate(dilateCoverageLayer1 + ((1-dilateCoverageLayer1) * _CoverageLayer1));			
				float dilateTypeLayer1 = (pow(lerp(worley1Layer1,worley2Layer1,_DilateTypeLayer1),0.5) - 0.1) * 0.65;
				float typeLayer1 = saturate(dilateTypeLayer1 * _CloudsTypeModifierLayer1);
				

				float topClouds = saturate(pow((worley2Layer1) * worley1Layer1,1.5) + pow(worley1Layer1,_CoverageLayer1 * 2)); 


				return float4(coverageLayer1,typeLayer1,topClouds,pow(topClouds,0.5));
			}
		ENDCG
		}
	}
	FallBack "Diffuse"
}
