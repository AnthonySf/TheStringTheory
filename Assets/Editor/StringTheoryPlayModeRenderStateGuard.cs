#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class StringTheoryPlayModeRenderStateGuard
{
    private static readonly List<LightState> LightStates = new List<LightState>();
    private static bool captured;
    private static Material skybox;
    private static Light sun;
    private static bool fog;
    private static Color fogColor;
    private static FogMode fogMode;
    private static float fogDensity;
    private static float fogStartDistance;
    private static float fogEndDistance;
    private static AmbientMode ambientMode;
    private static Color ambientLight;
    private static Color ambientSkyColor;
    private static Color ambientEquatorColor;
    private static Color ambientGroundColor;
    private static float ambientIntensity;
    private static Color subtractiveShadowColor;
    private static DefaultReflectionMode defaultReflectionMode;
    private static int defaultReflectionResolution;
    private static int reflectionBounces;
    private static float reflectionIntensity;

    static StringTheoryPlayModeRenderStateGuard()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            Capture();
        else if (state == PlayModeStateChange.EnteredPlayMode && !captured)
            Capture();
        else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            Restore();
    }

    private static void Capture()
    {
        skybox = RenderSettings.skybox;
        sun = RenderSettings.sun;
        fog = RenderSettings.fog;
        fogColor = RenderSettings.fogColor;
        fogMode = RenderSettings.fogMode;
        fogDensity = RenderSettings.fogDensity;
        fogStartDistance = RenderSettings.fogStartDistance;
        fogEndDistance = RenderSettings.fogEndDistance;
        ambientMode = RenderSettings.ambientMode;
        ambientLight = RenderSettings.ambientLight;
        ambientSkyColor = RenderSettings.ambientSkyColor;
        ambientEquatorColor = RenderSettings.ambientEquatorColor;
        ambientGroundColor = RenderSettings.ambientGroundColor;
        ambientIntensity = RenderSettings.ambientIntensity;
        subtractiveShadowColor = RenderSettings.subtractiveShadowColor;
        defaultReflectionMode = RenderSettings.defaultReflectionMode;
        defaultReflectionResolution = RenderSettings.defaultReflectionResolution;
        reflectionBounces = RenderSettings.reflectionBounces;
        reflectionIntensity = RenderSettings.reflectionIntensity;

        LightStates.Clear();
        Light[] lights = Resources.FindObjectsOfTypeAll<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null || EditorUtility.IsPersistent(light))
                continue;

            GameObject lightObject = light.gameObject;
            if (lightObject == null || !lightObject.scene.IsValid() || !lightObject.scene.isLoaded)
                continue;

            LightStates.Add(new LightState(light));
        }

        captured = true;
    }

    private static void Restore()
    {
        if (!captured)
            return;

        RenderSettings.skybox = skybox;
        RenderSettings.sun = sun;
        RenderSettings.fog = fog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = fogMode;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fogStartDistance = fogStartDistance;
        RenderSettings.fogEndDistance = fogEndDistance;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientLight = ambientLight;
        RenderSettings.ambientSkyColor = ambientSkyColor;
        RenderSettings.ambientEquatorColor = ambientEquatorColor;
        RenderSettings.ambientGroundColor = ambientGroundColor;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.subtractiveShadowColor = subtractiveShadowColor;
        RenderSettings.defaultReflectionMode = defaultReflectionMode;
        RenderSettings.defaultReflectionResolution = defaultReflectionResolution;
        RenderSettings.reflectionBounces = reflectionBounces;
        RenderSettings.reflectionIntensity = reflectionIntensity;

        for (int i = 0; i < LightStates.Count; i++)
            LightStates[i].Restore();

        SceneView.RepaintAll();
        LightStates.Clear();
        captured = false;
    }

    private readonly struct LightState
    {
        private readonly Light light;
        private readonly bool enabled;
        private readonly int cullingMask;
        private readonly int renderingLayerMask;
        private readonly float intensity;
        private readonly Color color;
        private readonly LightShadows shadows;
        private readonly float shadowStrength;
        private readonly float shadowBias;
        private readonly float shadowNormalBias;
        private readonly LightRenderMode renderMode;
        private readonly bool hasAdditionalLightData;
        private readonly bool customShadowLayers;
        private readonly uint additionalRenderingLayers;

        public LightState(Light light)
        {
            this.light = light;
            enabled = light.enabled;
            cullingMask = light.cullingMask;
            renderingLayerMask = light.renderingLayerMask;
            intensity = light.intensity;
            color = light.color;
            shadows = light.shadows;
            shadowStrength = light.shadowStrength;
            shadowBias = light.shadowBias;
            shadowNormalBias = light.shadowNormalBias;
            renderMode = light.renderMode;
            if (light.TryGetComponent(out UniversalAdditionalLightData lightData))
            {
                hasAdditionalLightData = true;
                customShadowLayers = lightData.customShadowLayers;
                additionalRenderingLayers = lightData.renderingLayers;
            }
            else
            {
                hasAdditionalLightData = false;
                customShadowLayers = false;
                additionalRenderingLayers = 0u;
            }
        }

        public void Restore()
        {
            if (light == null)
                return;

            light.enabled = enabled;
            light.cullingMask = cullingMask;
            light.renderingLayerMask = renderingLayerMask;
            light.intensity = intensity;
            light.color = color;
            light.shadows = shadows;
            light.shadowStrength = shadowStrength;
            light.shadowBias = shadowBias;
            light.shadowNormalBias = shadowNormalBias;
            light.renderMode = renderMode;
            if (hasAdditionalLightData && light.TryGetComponent(out UniversalAdditionalLightData lightData))
            {
                lightData.customShadowLayers = customShadowLayers;
                lightData.renderingLayers = additionalRenderingLayers;
            }
        }
    }
}
#endif
