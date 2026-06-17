Shader "Enviro/Skybox"
{
    Properties
    {
		_MoonTex("Moon Tex", 2D) = "black" {}
		_MoonGlowTex("Moon Glow Tex", 2D) = "black" {}
		_SunTex("Sun Tex", 2D) = "black" {}
		_StarsTex ("Stars Tex", Cube) = "black" {}
		_GalaxyTex ("Galaxy Tex", Cube) = "black" {}
		_EnviroSkyRotation ("Sky Rotation", Vector) = (1,0,0,0)
		_EnviroFloatingSkyFill ("Floating Sky Fill", Vector) = (0,-1.42,-0.82,0)
		_EnviroStageAuroraColor ("Stage Aurora Color", Color) = (0,0,0,0)
		_EnviroStageAuroraParams ("Stage Aurora Params", Vector) = (0,0.02,0.72,0.16)
	}
	
    SubShader
    {
		Lod 300
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "IgnoreProjector"="True" }
		
        Pass
        {
            Cull Back
            ZWrite Off
		 
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
			#include "../Includes/SkyInclude.cginc"
			#pragma target 3.0 
			#pragma multi_compile __ UNITY_COLORSPACE_GAMMA
			#pragma multi_compile __ ENVIRO_SIMPLESKY


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

			uniform samplerCUBE _StarsTwinklingTex;					
			uniform float4x4 _StarsMatrix;
			uniform float4 _EnviroSkyRotation;
			uniform float4 _EnviroFloatingSkyFill;
			uniform float4 _EnviroStageAuroraColor;
			uniform float4 _EnviroStageAuroraParams;
			uniform float4x4 _StarsTwinklingMatrix;
			uniform float _StarsTwinkling;
			
			//uniform samplerCUBE _CubeTex;
			//uniform float _CubeIntensity;
			//uniform float _CubeBlend;

 
			struct VertexInput 
             {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0;
				float3 worldPos : TEXCOORD1; 
				UNITY_VERTEX_INPUT_INSTANCE_ID
             };


            struct v2f {
                float4 position : SV_POSITION;
             	float4 sunAndMoonPos : TEXCOORD0;
				float3 starPos : TEXCOORD1; 
				float3 texcoord : TEXCOORD2;
				float3 cirrusCoords : TEXCOORD3;
				float3 flatCoords : TEXCOORD4;
				float3 worldPos : TEXCOORD5;
				float3 starsTwinklingPos : TEXCOORD6;
				UNITY_VERTEX_OUTPUT_STEREO
            };
 
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

            v2f vert(VertexInput v) {
                v2f o;
				UNITY_SETUP_INSTANCE_ID(v); 
				UNITY_INITIALIZE_OUTPUT(v2f, o); 
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); 
                o.position = UnityObjectToClipPos(v.vertex);

				float3 rSun = normalize(cross(_SunDir.xyz, float3(0, -1, 0)));
				float3 uSun = cross(_SunDir.xyz, rSun);

				float3 rMoon = normalize(cross(_MoonDir.xyz, float3(0, -1, 0)));
				float3 uMoon = cross(_MoonDir.xyz, rMoon);

				o.sunAndMoonPos.xy = float2(dot(rSun, v.vertex.xyz), dot(uSun, v.vertex.xyz)) * (21.0 - _SkySunParameters.x) + 0.5;
				o.sunAndMoonPos.zw = float2(dot(rMoon, v.vertex.xyz), dot(uMoon, v.vertex.xyz)) * (20.7 - _SkyMoonParameters.z) + 0.5;
				//o.moonGlowPos.xy = float2(dot(rMoon, v.vertex.xyz), dot(uMoon, v.vertex.xyz)) * (21.0 - (_SkyMoonParameters.y)) + 0.5;
				o.starPos = RotateEnviroSky(mul((float3x3)_StarsMatrix,v.vertex.xyz));
				o.starsTwinklingPos = mul((float3x3)_StarsTwinklingMatrix, v.vertex.xyz);

				o.texcoord = RotateEnviroSky(v.texcoord);

				o.worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;

				if(_CirrusClouds > 0.0)
				{
					o.cirrusCoords = RotateEnviroSky(normalize(v.vertex).xyz);
					float3 cirrusCoords = normalize(o.cirrusCoords + float3(0,1,0));
					o.cirrusCoords.y *= 1 - dot(cirrusCoords.y + 10, float3(0,-0.15,0));
				}

				if(_FlatClouds > 0.0)
				{
					o.flatCoords = RotateEnviroSky(normalize(v.vertex).xyz);
					float3 flatCoords = normalize(o.flatCoords + float3(0,1,0));
					o.flatCoords.y *= 1 - dot(flatCoords.y + 200 * _FlatCloudsParams.z, float3(0,-0.1,0));
				}

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
				float _DitheringIntensity = 0.05; 
				float d = dot(float2(131.0, 312.0), vScreenPos.xy + _Time.y);
				float3 vDither = float3(d, d, d);
				vDither.rgb = frac(vDither.rgb / float3(103.0, 71.0, 97.0)) - float3(0.5, 0.5, 0.5);
				return (vDither.rgb / 15.0) * _DitheringIntensity;
			}   
 
            float4 frag(v2f i) : COLOR 
            {			
				float4 skyColor = float4(0, 0, 0, 1);
				float3 viewDir = normalize(i.texcoord);
   

				#if ENVIRO_SIMPLESKY
					skyColor = GetSkyColorSimple(viewDir, 0.005f);  
				#else
					skyColor = GetSkyColor(viewDir, 0.005f);  
				#endif
				

				//Stars
				float skyMask = EnviroStarHorizonMask(viewDir.y);
				float4 starsTex = texCUBE(_StarsTex, i.starPos.xyz) * skyMask;
				float4 stars = starsTex * _StarIntensity * 10;
				float proceduralStars = EnviroProceduralStars(i.starPos.xyz, _TabsStarDensity) * skyMask;
				stars += float4(proceduralStars, proceduralStars, proceduralStars, 0.0);

				#ifndef ENVIRO_SIMPLESKY
				if (_StarsTwinkling > 0)
				{
					float4 starsTwinklingMap = texCUBE(_StarsTwinklingTex, i.starsTwinklingPos.xyz);
					stars = stars * starsTwinklingMap;
				}  
				
				//Galaxy
				float4 galaxyTex = texCUBE(_GalaxyTex, i.starPos.xyz) * skyMask;
				float4 galaxy = galaxyTex * _GalaxyIntensity;
				#endif

				//Sun
				float4 sun = float4(0,0,0,1);
				float hideBackSun = saturate(dot(_SunDir.xyz, viewDir));
				float4 sunDisk = tex2D(_SunTex, i.sunAndMoonPos.xy) * hideBackSun;
				sun = sunDisk * _SunColor * 10;
				skyColor += sun;
	  
				//Moon
				if(_SkyMoonParameters.w > 0.0) 
				{
					float hideBackMoon = saturate(dot(-_MoonDir.xyz, viewDir));
					float4 moon = tex2D(_MoonTex, i.sunAndMoonPos.zw) * hideBackMoon;
					float alpha = MoonPhaseFactor(i.sunAndMoonPos.zw, _SkyMoonParameters.x);
					float moonArea = clamp(moon.a * 10, 0, 1); 
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
					float4 aurora = Aurora(i.worldPos);
					skyColor.rgb += aurora.rgb;
				}

				skyColor.rgb += EnviroStageAurora(viewDir);

				//Cube
				//float4 cubeMap = texCUBE(_CubeTex, i.texcoord.xyz); 
				//skyColor.rgb = skyColor.rgb * (1 - cubeMap.a * _CubeBlend) + (cubeMap.rgb * _CubeIntensity) * cubeMap.a * _CubeBlend;

				//Dithering
				#ifndef ENVIRO_SIMPLESKY
				skyColor.rgb += ScreenSpaceDither(i.position.xy,skyColor.rgb);
				#endif 

				//Cirrus
				if(_CirrusClouds > 0.0)
				{
					float4 cirrus = CirrusClouds(i.cirrusCoords);
					skyColor.rgb = skyColor.rgb * (1 - cirrus.a) + cirrus.rgb * cirrus.a; 
				}
 
				//2D Clouds
				if(_FlatClouds > 0.0)
				{
					float4 clouds = Clouds2D(i.flatCoords, i.worldPos); 
					skyColor.rgb = skyColor.rgb * (1 - clouds.a) + clouds.rgb * clouds.a;
				}

			#if defined(UNITY_COLORSPACE_GAMMA)
				skyColor.rgb = LinearToGammaSpace(skyColor.rgb);
			#endif

                return skyColor;
            }
            ENDCG
        }
	}
    FallBack Off
} 
