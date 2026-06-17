Shader "Enviro3/Lightning" {
Properties {
	_MainTex ("Texture", 2D) = "white" {}
	_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	_BrightnessMod("Brightness Mod ", float) = 250
	_Brightness("Brightness ", float) = 250
	
} 

Category {
	Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
	Blend One One
	ColorMask RGBA
	Cull Off Lighting Off ZWrite Off

	SubShader {
		Pass {
		
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_particles
			#pragma multi_compile_fog
			#pragma exclude_renderers gles 
			#include "UnityCG.cginc"
			#include "../Includes/FogInclude.cginc"

			sampler2D _MainTex; 
			float4 _TintColor;
			
			float _Brightness = 1;
			float _BrightnessMod = 1;
			
			struct appdata_t 
			{
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float2 texcoord : TEXCOORD0;
				float4 wpos : TEXCOORD1;
				float4 screenPos : TEXCOORD2;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			float4 _MainTex_ST;
			
			v2f vert (appdata_t v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
				float3 viewVector = mul(unity_CameraInvProjection, float4(v.texcoord.xy * 5 - 1, 0, 1));
                o.wpos = mul(unity_CameraToWorld, float4(viewVector, 0));
				o.screenPos = ComputeScreenPos(v.vertex);
				return o;
			}

			
			float4 frag (v2f i) : SV_Target
			{
				float3 worldPos = i.wpos.xyz;
				float2 screenPos = (i.screenPos.xy/i.screenPos.w);
				float depth = i.screenPos.w;

		
				float4 col = tex2D(_MainTex, i.texcoord) * _TintColor;	
				col.rgb *= _Brightness * _BrightnessMod;
					
				//col.rgb = ApplyFogAndVolumetricLights(col.rgb,screenPos,worldPos,depth);			
				col.rgb *= col.a;	
				return col;
			}
			ENDCG 
		}
	} 
}
Fallback "Legacy Shaders/Transparent/Diffuse"
}
