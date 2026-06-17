Shader "Enviro/HDRP/Sky"
{
    //Properties
    //{
	//	_MoonTex("Moon Tex", 2D) = "black" {}
	//	_MoonGlowTex("Moon Glow Tex", 2D) = "black" {}
	//	_SunTex("Sun Tex", 2D) = "black" {}
	//	_StarsTex ("Stars Tex", Cube) = "black" {}
	//	_GalaxyTex ("Galaxy Tex", Cube) = "black" {}
	//}

	HLSLINCLUDE
	#pragma editor_sync_compilation
	#pragma multi_compile __ ENVIROHDRP 
	#pragma multi_compile __ ENVIRO_SIMPLESKY
              
	#if defined (ENVIROHDRP)
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
	#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
	#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"
	#include "../Includes/SkyIncludeHLSL.hlsl"

	uniform float4 _SkyMoonParameters;
	uniform float4 _SkySunParameters;
	uniform sampler2D _MoonTex;
	//uniform sampler2D _MoonGlowTex;
	uniform sampler2D _SunTex;
	uniform float4 _MoonColor;
	uniform float _MoonGlowIntensity;
	uniform float _StarIntensity;
	uniform float _GalaxyIntensity;
	uniform float _TabsStarDensity;
	uniform float _CirrusClouds;
	uniform float _FlatClouds;
	uniform float _Aurora;
	uniform samplerCUBE _StarsTex;
	uniform samplerCUBE _GalaxyTex;					
	uniform float4x4 _StarsMatrix;
	uniform float4 _EnviroSkyRotation;
	uniform float4 _EnviroFloatingSkyFill;
	uniform float4 _EnviroStageAuroraColor;
	uniform float4 _EnviroStageAuroraParams;
	uniform float4 _AmbientColorTintHDRP;
	uniform float _EnviroSkyIntensity;

	uniform samplerCUBE _StarsTwinklingTex;					
	uniform float4x4 _StarsTwinklingMatrix;
	uniform float _StarsTwinkling;


	struct VertexInput 
	{
		uint vertexID : SV_VertexID;
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};


	struct v2f 
	{
		float4 position : SV_POSITION;
		UNITY_VERTEX_OUTPUT_STEREO
	};

	v2f vert(VertexInput v) 
	{
		v2f o;
		UNITY_SETUP_INSTANCE_ID(v);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

		o.position = GetFullScreenTriangleVertexPosition(v.vertexID, UNITY_RAW_FAR_CLIP_VALUE);
		return o;
	}

	float MoonPhaseFactor(float2 uv, float phase)
	{
		float alpha = 1.0;


		float srefx = uv.x - 0.5;
		float refx = abs(uv.x - 0.5);

		if (phase > 0)
		{
			srefx = (1 - uv.x) - 0.5;
			refx = abs((1 - uv.x) - 0.5);
		}

		phase = abs(_SkyMoonParameters.x);
		float refy = abs(uv.y - 0.5);
		float refxfory = sqrt(0.25 - refy * refy);
		float xmin = -refxfory;
		float xmax = refxfory;
		float xmin1 = (xmax - xmin) * (phase / 2) + xmin;
		float xmin2 = (xmax - xmin) * phase + xmin;

		if (srefx < xmin1)
		{
			alpha = 0;
		}
		else if (srefx < xmin2 && xmin1 != xmin2)
		{
			alpha = (srefx - xmin1) / (xmin2 - xmin1);
		}

		return alpha;
	}

	float3 ScreenSpaceDither(float2 vScreenPos, float3 clr)
	{
		float _DitheringIntensity = 0.25;
		float d = dot(float2(131.0, 312.0), vScreenPos.xy + _Time.y);
		float3 vDither = float3(d, d, d);
		vDither.rgb = frac(vDither.rgb / float3(103.0, 71.0, 97.0)) - float3(0.5, 0.5, 0.5);
		return (vDither.rgb / 15.0) * _DitheringIntensity;
	}   

	float3 RotateEnviroSky(float3 dir)
	{
		float2 yaw = dot(_EnviroSkyRotation.xy, _EnviroSkyRotation.xy) < 0.0001 ? float2(1.0, 0.0) : _EnviroSkyRotation.xy;
		float2 pitch = dot(_EnviroSkyRotation.zw, _EnviroSkyRotation.zw) < 0.0001 ? float2(1.0, 0.0) : _EnviroSkyRotation.zw;
		float3 yawed = float3(
			dir.x * yaw.x - dir.z * yaw.y,
			dir.y,
			dir.x * yaw.y + dir.z * yaw.x);

		return float3(
			yawed.x,
			yawed.y * pitch.x + yawed.z * pitch.y,
			yawed.z * pitch.x - yawed.y * pitch.y);
	}

	float EnviroStarHorizonMask(float y)
	{
		float originalMask = saturate(y);
		float lower = _EnviroFloatingSkyFill.y;
		float upper = max(_EnviroFloatingSkyFill.z, lower + 0.001);
		float floatingMask = smoothstep(lower, upper, y);
		return lerp(originalMask, max(originalMask, floatingMask), saturate(_EnviroFloatingSkyFill.x));
	}

	float EnviroHash21(float2 p)
	{
		p = frac(p * float2(123.34, 345.45));
		p += dot(p, p + 34.345);
		return frac(p.x * p.y);
	}

	float EnviroProceduralStars(float3 dir, float density)
	{
		density = saturate(density);
		if (density <= 0.0001)
			return 0.0;

		float3 n = normalize(dir);
		float2 uv = float2(
			atan2(n.x, n.z) * 0.159154943 + 0.5,
			asin(clamp(n.y, -1.0, 1.0)) * 0.318309886 + 0.5);
		float2 grid = uv * float2(960.0, 420.0);
		float2 cell = floor(grid);
		float2 f = frac(grid);
		float spawn = step(lerp(0.9975, 0.9875, density), EnviroHash21(cell));
		float2 center = float2(EnviroHash21(cell + 19.19), EnviroHash21(cell + 71.71));
		float dist = length((f - center) * float2(1.0, 1.8));
		float size = lerp(0.025, 0.065, EnviroHash21(cell + 131.13));
		float star = spawn * smoothstep(size, 0.0, dist);
		float brightness = lerp(0.45, 1.35, EnviroHash21(cell + 211.21));
		return star * brightness * density;
	}

	float3 EnviroStageAurora(float3 viewDir)
	{
		float intensity = _EnviroStageAuroraParams.x;
		if (intensity <= 0.001)
			return float3(0, 0, 0);

		float lower = _EnviroStageAuroraParams.y;
		float upper = max(_EnviroStageAuroraParams.z, lower + 0.001);
		float y01 = saturate((viewDir.y - lower) / max(0.001, upper - lower));
		float yMask = smoothstep(lower, lower + 0.10, viewDir.y);
		float bodyFade = smoothstep(0.18, 0.34, y01) * (1.0 - smoothstep(0.66, 0.92, y01));
		float ribbonFade = smoothstep(0.03, 0.18, y01) * (1.0 - smoothstep(0.78, 1.00, y01));
		float x = atan2(viewDir.x, max(0.001, abs(viewDir.z)));
		float time = _Time.y * _EnviroStageAuroraParams.w;
		float arcOffset = sin(y01 * 2.7 + time * 0.18) * 0.10 + sin(y01 * 5.1 - time * 0.08) * 0.05;
		float wave = sin((x + arcOffset) * 8.0 + time) * 0.42 + sin((x - arcOffset * 0.6) * 15.0 - time * 1.35) * 0.24 + sin(x * 27.0 + time * 0.75) * 0.12;
		float fineWave = sin((x + arcOffset) * 18.0 - time * 1.15 + y01 * 5.4) * 0.28 + sin(x * 34.0 + time * 0.8) * 0.16;
		float curtain = pow(saturate(0.53 + wave), 5.2);
		float strand = pow(saturate(0.58 + fineWave), 7.5);
		float ribbon = pow(1.0 - abs(frac((x * 0.48) + time * 0.026) * 2.0 - 1.0), 4.0);
		float horizonRibbon = smoothstep(-0.24, 0.08, viewDir.y) * (1.0 - smoothstep(0.28, 0.54, viewDir.y));
		float aurora = yMask * ((bodyFade * (curtain * 0.48 + strand * 0.30)) + (ribbonFade * ribbon * 0.12) + (horizonRibbon * ribbon * 0.08));
		float colorShift = saturate(0.5 + 0.5 * sin(x * 2.2 + time * 0.35 + y01 * 6.0));
		float3 tealBase = lerp(_EnviroStageAuroraColor.rgb, float3(0.10, 0.74, 0.92), 0.36);
		float3 auroraColor = lerp(tealBase, float3(0.20, 0.42, 1.00), colorShift * 0.38);
		float pinkShift = saturate(0.5 + 0.5 * sin(x * 3.7 - time * 0.42 + y01 * 8.0));
		float blushWave = pow(saturate(0.55 + 0.45 * sin((x + y01 * 0.55) * 4.4 - time * 0.58)), 1.45);
		float violetWave = pow(saturate(0.54 + 0.46 * sin((x - y01 * 0.35) * 6.2 + time * 0.30)), 1.8);
		float violetCurtain = saturate(curtain * 0.38 + strand * 0.26) * colorShift;
		float magentaCurtain = saturate(strand * 0.72 + ribbon * 0.34) * smoothstep(0.04, 0.74, y01);
		float roseEdge = ribbon * horizonRibbon * (0.30 + pinkShift * 0.45);
		float violetVeil = saturate(bodyFade * violetWave * 0.58 + violetCurtain * 0.34);
		float magentaVeil = saturate(bodyFade * blushWave * (0.46 + pinkShift * 0.34) + magentaCurtain * 0.62);
		auroraColor = lerp(auroraColor, float3(0.56, 0.18, 1.00), violetVeil);
		auroraColor = lerp(auroraColor, float3(1.00, 0.16, 0.62), magentaVeil);
		auroraColor = lerp(auroraColor, float3(1.00, 0.42, 0.92), saturate(roseEdge + ribbonFade * blushWave * 0.36));
		return auroraColor * aurora * intensity * 1.18;
	}

	float4 frag(v2f i) : SV_Target 
	{			
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

		float3 viewDirWS = GetSkyViewDirWS(i.position.xy);
		float3 dir = -viewDirWS;
		float3 wpos = normalize(mul(UNITY_MATRIX_M, float4(dir, 0.0f))).xyz;
		float4 skyColor = float4(0, 0, 0, 1);


		float3 viewDir = normalize(dir);

		#if ENVIRO_SIMPLESKY
			skyColor = GetSkyColorSimple(viewDir, 0.005f);  
		#else
			skyColor = GetSkyColor(viewDir, 0.005f);  
		#endif 

		//Stars
		float3 starsUV = RotateEnviroSky(mul((float3x3)_StarsMatrix, dir));
		float skyMask = EnviroStarHorizonMask(viewDir.y);
		float4 starsTex = texCUBE(_StarsTex, starsUV) * skyMask;
		float4 stars = starsTex * _StarIntensity * 10;
		float proceduralStars = EnviroProceduralStars(starsUV, _TabsStarDensity) * skyMask;
		stars += float4(proceduralStars, proceduralStars, proceduralStars, 0.0);

		#ifndef ENVIRO_SIMPLESKY
		if (_StarsTwinkling > 0)
			{
				float3 starsTwinklingUV = mul((float3x3)_StarsTwinklingMatrix, dir);
				float4 starsTwinklingMap = texCUBE(_StarsTwinklingTex, starsTwinklingUV);
				stars = stars * starsTwinklingMap;
			} 
		 

		//Galaxy
		float4 galaxyTex = texCUBE(_GalaxyTex, starsUV) * skyMask;
		float4 galaxy = galaxyTex * _GalaxyIntensity;
		#endif


		//Sun and Moon UV
		float3 rSun = normalize(cross(_SunDir.xyz, float3(0, -1, 0)));
		float3 uSun = cross(_SunDir.xyz, rSun);
		float2 sunUV = float2(dot(rSun, dir), dot(uSun, dir)) * (21.0 - _SkySunParameters.y) + 0.5;
		float3 rMoon = normalize(cross(_MoonDir.xyz, float3(0, -1, 0)));
		float3 uMoon = cross(_MoonDir.xyz, rMoon);
		float2 moonUV  = float2(dot(rMoon, dir), dot(uMoon, dir)) * (20.7 - _SkyMoonParameters.z) + 0.5;
		
		//Sun
		float4 sun = float4(0,0,0,1);
		float hideBackSun = saturate(dot(_SunDir.xyz, viewDir));
		float4 sunDisk = tex2D(_SunTex, sunUV) * hideBackSun;
		sun = sunDisk * _SunColor * 10;
		skyColor += sun;

		//Moon
		if(_SkyMoonParameters.w > 0.0) 
		{
			float hideBackMoon = saturate(dot(-_MoonDir.xyz, viewDir));
			float4 moon = tex2D(_MoonTex, moonUV) * hideBackMoon;
			float alpha = MoonPhaseFactor(moonUV, _SkyMoonParameters.x);
			float3 moonArea = clamp(moon * 10, 0, 1);
			float starsBehindMoon = 1 - clamp((moonArea * 5), 0, 1);
			moon = lerp(float4(0, 0, 0, 0), moon, alpha);
			moon = moon * _MoonColor;
			//float4 moonGlow = tex2D(_MoonGlowTex, i.moonGlowPos.xy) * hideBackMoon;
			//moonGlow = moonGlow * _MoonColor * _MoonGlowIntensity;
			skyColor += stars * starsBehindMoon;
			#ifndef ENVIRO_SIMPLESKY
			skyColor += galaxy * starsBehindMoon;
			#endif
			skyColor += moon;
		}
		else
		{
			skyColor += stars;
			#ifndef ENVIRO_SIMPLESKY
			skyColor += galaxy;
			#endif
		}
		
		//Aurora 
		if(_Aurora > 0.0)
		{
			float4 aurora = Aurora(wpos); 
			skyColor.rgb += aurora.rgb;
		}

		skyColor.rgb += EnviroStageAurora(viewDir);

		//Dithering
		//skyColor.rgb += ScreenSpaceDither(i.position.xy,skyColor.rgb);

		float3 cloudsDir = normalize(wpos + float3(0,1,0));

		//Cirrus
		if(_CirrusClouds > 0.0)
		{	
			float3 cirrusUV = wpos;
			cirrusUV.y *= 1 - dot(cloudsDir.y + 10, float3(0,-0.15,0));

			float4 cirrus = CirrusClouds(cirrusUV);
			skyColor.rgb = skyColor.rgb * (1 - cirrus.a) + cirrus.rgb * cirrus.a;
		}

		//2D Clouds
		if(_FlatClouds > 0.0)
		{
			float3 flatCloudsUV = wpos;
			flatCloudsUV.y *= 1 - dot(cloudsDir.y + 200 * _FlatCloudsParams.z, float3(0,-0.1,0));
			float4 clouds = Clouds2D(flatCloudsUV, wpos); 
			skyColor.rgb = skyColor.rgb * (1 - clouds.a) + clouds.rgb * clouds.a;
		}
	
		return float4(skyColor.rgb * _EnviroSkyIntensity * GetCurrentExposureMultiplier(), 1);
	}

	float4 fragBaking(v2f i) : SV_Target  
	{			
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

		float3 viewDirWS = GetSkyViewDirWS(i.position.xy);
		float3 dir = -viewDirWS;
		float3 wpos = normalize(mul(UNITY_MATRIX_M, float4(dir, 0.0f))).xyz;

		float4 skyColor = float4(0, 0, 0, 1);


		float3 viewDir = normalize(dir);
		#if ENVIRO_SIMPLESKY
		skyColor = GetSkyColorSimple(viewDir, 0.005f);  
		#else
		skyColor = GetSkyColor(viewDir, 0.005f);  
		#endif 

		//Stars
		float3 starsUV = RotateEnviroSky(mul((float3x3)_StarsMatrix, dir));
		float skyMask = EnviroStarHorizonMask(viewDir.y);
		float4 starsTex = texCUBE(_StarsTex, starsUV) * skyMask;
		float4 stars = starsTex * _StarIntensity;
		float proceduralStars = EnviroProceduralStars(starsUV, _TabsStarDensity) * skyMask;
		stars += float4(proceduralStars, proceduralStars, proceduralStars, 0.0);
		//skyColor += stars;

		//Galaxy
		#ifndef ENVIRO_SIMPLESKY
		float4 galaxyTex = texCUBE(_GalaxyTex, starsUV) * skyMask;
		float4 galaxy = galaxyTex * _GalaxyIntensity;
		#endif

		//Sun and Moon UV
		float3 rSun = normalize(cross(_SunDir.xyz, float3(0, -1, 0)));
		float3 uSun = cross(_SunDir.xyz, rSun);
		float2 sunUV = float2(dot(rSun, dir), dot(uSun, dir)) * (21.0 - _SkySunParameters.y) + 0.5;
		float3 rMoon = normalize(cross(_MoonDir.xyz, float3(0, -1, 0)));
		float3 uMoon = cross(_MoonDir.xyz, rMoon);
		float2 moonUV  = float2(dot(rMoon, dir), dot(uMoon, dir)) * (20.7 - _SkyMoonParameters.z) + 0.5;
		
		//Sun
		float4 sun = float4(0,0,0,1);
		float hideBackSun = saturate(dot(_SunDir.xyz, viewDir));
		float4 sunDisk = tex2D(_SunTex, sunUV) * hideBackSun;
		sun = sunDisk * _SunColor * 10;
		skyColor += sun;

		//Moon
		if(_SkyMoonParameters.w > 0.0) 
		{
			float hideBackMoon = saturate(dot(-_MoonDir.xyz, viewDir));
			float4 moon = tex2D(_MoonTex, moonUV) * hideBackMoon;
			float alpha = MoonPhaseFactor(moonUV, _SkyMoonParameters.x);
			float3 moonArea = clamp(moon * 10, 0, 1);
			float starsBehindMoon = 1 - clamp((moonArea * 5), 0, 1);
			moon = lerp(float4(0, 0, 0, 0), moon, alpha);
			moon = moon * _MoonColor;
			//float4 moonGlow = tex2D(_MoonGlowTex, i.moonGlowPos.xy) * hideBackMoon;
			//moonGlow = moonGlow * _MoonColor * _MoonGlowIntensity;
			skyColor += stars * starsBehindMoon;
			#ifndef ENVIRO_SIMPLESKY
			skyColor += galaxy * starsBehindMoon;
			#endif
			skyColor += moon;
		}
		else
		{
			skyColor += stars;
			#ifndef ENVIRO_SIMPLESKY
			skyColor += galaxy;
			#endif
		}
		
		//Aurora
		if(_Aurora > 0.0)
		{
			float4 aurora = Aurora(wpos);
			skyColor.rgb += aurora.rgb;
		}

		skyColor.rgb += EnviroStageAurora(viewDir);

		//Dithering
		//skyColor.rgb += ScreenSpaceDither(i.position.xy,skyColor.rgb);

		float3 cloudsDir = normalize(wpos + float3(0,1,0));

		//Cirrus
		if(_CirrusClouds > 0.0)
		{	
			
			float3 cirrusUV = wpos;
			cirrusUV.y *= 1 - dot(cloudsDir.y + 10, float3(0,-0.15,0));

			float4 cirrus = CirrusClouds(cirrusUV);
			skyColor.rgb = skyColor.rgb * (1 - cirrus.a) + cirrus.rgb * cirrus.a;
		}

		//2D Clouds
		if(_FlatClouds > 0.0)
		{
			float3 flatCloudsUV = wpos;
			flatCloudsUV.y *= 1 - dot(cloudsDir.y + 200, float3(0,-0.1,0));
			float4 clouds = Clouds2D(flatCloudsUV, wpos); 
			skyColor.rgb = skyColor.rgb * (1 - clouds.a) + clouds.rgb * clouds.a;
		} 
		return float4(skyColor.rgb * _EnviroSkyIntensity, 1);
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

		float4 fragBaking (v2f i) : SV_Target
		{
			float4 col = tex2D(_MainTex, i.uv);
			// just invert the colors
			col.rgb = 1 - col.rgb;
			return col;
		}
		#endif
	ENDHLSL

	SubShader
	{
		Tags{ "RenderPipeline" = "HDRenderPipeline" }
		Pass
		{
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment fragBaking
			ENDHLSL
		}

		// For fullscreen Sky
		Pass
		{
			ZWrite Off
			ZTest LEqual
			Blend Off
			Cull Off

			HLSLPROGRAM	
			#pragma vertex vert
			#pragma fragment frag
			ENDHLSL
		}
	}
}
