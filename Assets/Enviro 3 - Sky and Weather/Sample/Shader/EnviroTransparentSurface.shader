Shader "Enviro 3/Sample/TransparentSurface"
{
	Properties
	{
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
	} 
	SubShader 
	{
		Tags {"Queue" = "Transparent" "RenderType"="Transparent" }
		LOD 200
   
		CGPROGRAM
		#include "../../Resources/Shader/Includes/FogInclude.cginc"
		#pragma surface surf Standard fullforwardshadows alpha finalcolor:ApplyFog
		#pragma target 3.0

		 
		sampler2D _MainTex;
		sampler2D _CameraDepthTexture;
 
		struct Input {
			float2 uv_MainTex;
			float4 screenPos;
			float3 worldPos;
		}; 

		void ApplyFog(Input IN, SurfaceOutputStandard o, inout fixed4 color)
		{ 
				//Get ScreenPosition
				float3 uvscreen = IN.screenPos.xyz / max(0.00001,IN.screenPos.w);
				// Calculate Linear Depth
				half linear01Depth = Linear01Depth(uvscreen.z);
				//get World Position
				float3 wPos = IN.worldPos.xyz;
				// Calculate Fog and apply volume lighting tex
				float3 fogClr = ApplyFogAndVolumetricLights(color.rgb,uvscreen.xy, wPos, linear01Depth);

				#if _ALPHAPREMULTIPLY_ON
					fogClr.rgb *= o.Alpha;
				#endif

				#ifndef UNITY_PASS_FORWARDADD
					color.rgb = fogClr.rgb;
				#endif
		}

		half _Glossiness;
		half _Metallic;
		fixed4 _Color;
 
		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
		}
		ENDCG
	}
	FallBack "Standard"
}