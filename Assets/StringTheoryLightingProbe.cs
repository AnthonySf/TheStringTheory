#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public sealed class StringTheoryLightingProbe : MonoBehaviour
{
    private const KeyCode ProbeKey = KeyCode.F8;
    private const KeyCode TraceToggleKey = KeyCode.L;
    private const KeyCode StepToggleKey = KeyCode.K;
    private const KeyCode EnviroFeatureToggleKey = KeyCode.E;
    private static bool traceEnabled;
    private static bool stepBreaksEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindObjectOfType<StringTheoryLightingProbe>() != null)
            return;

        GameObject probe = new GameObject("StringTheoryLightingProbe");
        probe.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(probe);
        probe.AddComponent<StringTheoryLightingProbe>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(ProbeKey))
            Log("manual F8");

        if (IsDebugChordDown(TraceToggleKey))
        {
            traceEnabled = !traceEnabled;
            Debug.Log($"[StringTheoryLightingProbe] lighting trace {(traceEnabled ? "enabled" : "disabled")} (Ctrl+Shift+L). stepBreaks={stepBreaksEnabled}");
            LogTraceSnapshot("trace-toggle");
        }

        if (IsDebugChordDown(StepToggleKey))
        {
            stepBreaksEnabled = !stepBreaksEnabled;
            traceEnabled = true;
            Debug.Log($"[StringTheoryLightingProbe] lighting trace step breaks {(stepBreaksEnabled ? "enabled" : "disabled")} (Ctrl+Shift+K). It now pauses only on shared renderer/camera transition points.");
            LogTraceSnapshot("step-toggle");
        }

        if (IsDebugChordDown(EnviroFeatureToggleKey))
            ToggleEnviroRenderFeature();
    }

    private static bool IsDebugChordDown(KeyCode key)
    {
        bool controlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        return controlHeld && shiftHeld && Input.GetKeyDown(key);
    }

    public static void TraceStep(string reason)
    {
        if (!traceEnabled && !stepBreaksEnabled)
            return;

        bool shouldBreak = stepBreaksEnabled && ShouldBreakForReason(reason);
        bool shouldLog = !stepBreaksEnabled || shouldBreak;
        if (!shouldLog && !shouldBreak)
            return;

        LogTraceSnapshot(reason);
        if (shouldBreak)
            Debug.Break();
    }

    private static void ToggleEnviroRenderFeature()
    {
        int changed = 0;
        bool? activeState = null;
        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
        FieldInfo rendererDataListField = pipeline?.GetType().GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        if (rendererDataListField?.GetValue(pipeline) is Array rendererDataList)
        {
            for (int rendererIndex = 0; rendererIndex < rendererDataList.Length; rendererIndex++)
            {
                object rendererData = rendererDataList.GetValue(rendererIndex);
                FieldInfo featuresField = rendererData?.GetType().GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
                if (!(featuresField?.GetValue(rendererData) is System.Collections.IList features))
                    continue;

                for (int featureIndex = 0; featureIndex < features.Count; featureIndex++)
                {
                    if (!(features[featureIndex] is ScriptableRendererFeature feature))
                        continue;
                    Type featureType = feature.GetType();
                    if (featureType == null || !featureType.Name.Contains("Enviro", StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool isActive = feature.isActive;
                    bool nextActive = !isActive;
                    feature.SetActive(nextActive);
                    activeState = nextActive;
                    changed++;
                }
            }
        }

        Debug.Log($"[StringTheoryLightingProbe] Enviro render feature toggle changed={changed} active={activeState?.ToString() ?? "n/a"} (Ctrl+Shift+E)");
        LogTraceSnapshot("enviro-feature-toggle");
    }

    private static bool ShouldBreakForReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return true;

        return reason.StartsWith("GuitarBridgeServer.StartFromMainMenuFromUi", StringComparison.Ordinal) ||
               reason.StartsWith("GuitarBridgeServer.ContinueFromMainMenuFromUi", StringComparison.Ordinal) ||
               reason.StartsWith("GuitarBridgeServer.OpenMiniGamesFromUi", StringComparison.Ordinal) ||
               reason.StartsWith("GuitarBridgeServer.OpenFightClub", StringComparison.Ordinal) ||
               reason.StartsWith("GuitarBridgeServer.StartFightClub", StringComparison.Ordinal) ||
               reason.StartsWith("GuitarBridgeServer.EnsureRendererImpl", StringComparison.Ordinal) ||
               reason.StartsWith("GuitarBridgeServer.Update", StringComparison.Ordinal) ||
               reason.StartsWith("GuitarHighway3DRenderer.Render", StringComparison.Ordinal) ||
               reason.StartsWith("GuitarHighway3DRenderer.EnsureBackgroundMode", StringComparison.Ordinal) ||
               reason.StartsWith("GuitarHighway3DRenderer.InitializeBackgroundEffect", StringComparison.Ordinal);
    }

    public static void Log(string reason)
    {
        StringBuilder builder = new StringBuilder(8192);
        builder.AppendLine($"[StringTheoryLightingProbe] {reason}");
        builder.AppendLine($"pipeline='{Describe(GraphicsSettings.currentRenderPipeline)}' qualityPipeline='{Describe(QualitySettings.renderPipeline)}' qualityLevel={QualitySettings.GetQualityLevel()} pixelLightCount={QualitySettings.pixelLightCount} graphicsDevice={SystemInfo.graphicsDeviceType}");
        builder.AppendLine($"renderSettings ambientMode={RenderSettings.ambientMode} ambientLight={FormatColor(RenderSettings.ambientLight)} ambientIntensity={RenderSettings.ambientIntensity:0.###} sun='{Describe(RenderSettings.sun)}' fog={RenderSettings.fog}");
        AppendPipeline(builder, "currentPipeline", GraphicsSettings.currentRenderPipeline);
        AppendPipeline(builder, "qualityPipeline", QualitySettings.renderPipeline);
        AppendShaderKeyword(builder, "_ADDITIONAL_LIGHTS");
        AppendShaderKeyword(builder, "_ADDITIONAL_LIGHTS_VERTEX");
        AppendShaderKeyword(builder, "_CLUSTER_LIGHT_LOOP");
        AppendShaderKeyword(builder, "_FORWARD_PLUS");
        AppendShaderKeyword(builder, "_LIGHT_LAYERS");
        AppendShaderKeyword(builder, "_MAIN_LIGHT_SHADOWS");
        AppendShaderKeyword(builder, "_ADDITIONAL_LIGHT_SHADOWS");
        AppendLightingGlobalSummary(builder);
        AppendEnviroTraceSummary(builder);

        Camera[] cameras = Camera.allCameras;
        builder.AppendLine($"cameras count={cameras.Length} main='{Describe(Camera.main)}'");
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
                continue;

            builder.AppendLine($"camera[{i}] name='{camera.name}' enabled={camera.enabled} active={camera.gameObject.activeInHierarchy} tag='{camera.tag}' type={camera.cameraType} depth={camera.depth:0.###} clearFlags={camera.clearFlags} cullingMask={FormatMask(camera.cullingMask)} layer={FormatLayer(camera.gameObject.layer)} pos={FormatVector(camera.transform.position)} rot={FormatVector(camera.transform.rotation.eulerAngles)} {DescribeCameraData(camera)}");
        }

        Light[] lights = Resources.FindObjectsOfTypeAll<Light>();
        int logged = 0;
        int enabledLoaded = 0;
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null || light.gameObject == null)
                continue;

            if (!light.gameObject.scene.IsValid() || !light.gameObject.scene.isLoaded)
                continue;

            if (light.enabled)
                enabledLoaded++;

            if (logged >= 40)
                continue;

            UniversalAdditionalLightData lightData = light.GetComponent<UniversalAdditionalLightData>();
            string urpLayers = lightData != null ? FormatMask((int)lightData.renderingLayers) : "none";
            string shadowLayers = lightData != null && lightData.customShadowLayers ? "custom" : "default";
            builder.AppendLine($"light[{logged}] name='{light.name}' enabled={light.enabled} active={light.gameObject.activeInHierarchy} layer={FormatLayer(light.gameObject.layer)} type={light.type} intensity={light.intensity:0.###} range={light.range:0.###} shadows={light.shadows} cullingMask={FormatMask(light.cullingMask)} renderingLayerMask={FormatMask(light.renderingLayerMask)} urpRenderingLayers={urpLayers} urpShadowLayers={shadowLayers} pos={FormatVector(light.transform.position)} rot={FormatVector(light.transform.rotation.eulerAngles)}");
            logged++;
        }

        builder.AppendLine($"lights enabledLoaded={enabledLoaded} logged={logged}");
        AppendLoadedCameraTraceSummary(builder);
        AppendSceneViews(builder);
        AppendSelection(builder);
        AppendNamedObject(builder, "Cube");
        AppendNamedObject(builder, "Spot Light");
        Debug.Log(builder.ToString());
    }

    private static void LogTraceSnapshot(string reason)
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine($"[StringTheoryLightingTrace] {reason} frame={Time.frameCount} time={Time.realtimeSinceStartup:0.###} trace={traceEnabled} stepBreaks={stepBreaksEnabled}");
        builder.AppendLine($"pipeline='{Describe(GraphicsSettings.currentRenderPipeline)}' quality='{Describe(QualitySettings.renderPipeline)}' qualityLevel={QualitySettings.GetQualityLevel()} pixelLightCount={QualitySettings.pixelLightCount}");
        AppendPipelineTraceSummary(builder, "currentPipeline", GraphicsSettings.currentRenderPipeline);
        AppendPipelineTraceSummary(builder, "qualityPipeline", QualitySettings.renderPipeline);
        AppendRendererModeSummary(builder, GraphicsSettings.currentRenderPipeline);
        builder.AppendLine($"renderSettings ambientMode={RenderSettings.ambientMode} ambientIntensity={RenderSettings.ambientIntensity:0.###} sun='{Describe(RenderSettings.sun)}' fog={RenderSettings.fog}");
        AppendKeywordSummary(builder);
        AppendLightingGlobalSummary(builder);
        AppendEnviroTraceSummary(builder);
        AppendCameraTraceSummary(builder);
        AppendLoadedCameraTraceSummary(builder);
        AppendEnabledLightTraceSummary(builder);
        AppendNamedObject(builder, "Cube");
        AppendNamedObject(builder, "Spot Light");
        Debug.Log(builder.ToString());
    }

    private static void AppendPipelineTraceSummary(StringBuilder builder, string label, RenderPipelineAsset pipeline)
    {
        if (pipeline == null)
        {
            builder.AppendLine($"{label}=null");
            return;
        }

        builder.Append($"{label} name='{pipeline.name}'");
        AppendProperty(builder, pipeline, "additionalLightsRenderingMode");
        AppendProperty(builder, pipeline, "maxAdditionalLightsCount");
        AppendProperty(builder, pipeline, "supportsAdditionalLightShadows");
        AppendProperty(builder, pipeline, "supportsMainLightShadows");
        AppendProperty(builder, pipeline, "supportsSoftShadows");
        AppendProperty(builder, pipeline, "supportsLightLayers");
        AppendProperty(builder, pipeline, "shadowDistance");
        AppendProperty(builder, pipeline, "renderScale");
        AppendField(builder, pipeline, "m_PrefilteringModeMainLightShadows");
        AppendField(builder, pipeline, "m_PrefilteringModeAdditionalLight");
        AppendField(builder, pipeline, "m_PrefilteringModeAdditionalLightShadows");
        AppendField(builder, pipeline, "m_PrefilteringModeForwardPlus");
        AppendField(builder, pipeline, "m_PrefilterWriteRenderingLayers");
        builder.AppendLine();
    }

    private static void AppendRendererModeSummary(StringBuilder builder, RenderPipelineAsset pipeline)
    {
        if (pipeline == null)
        {
            builder.AppendLine("rendererMode pipeline=null");
            return;
        }

        FieldInfo rendererDataListField = pipeline.GetType().GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        if (!(rendererDataListField?.GetValue(pipeline) is Array rendererDataList) || rendererDataList.Length == 0)
        {
            builder.AppendLine("rendererMode rendererDataList=empty");
            return;
        }

        object rendererData = rendererDataList.GetValue(0);
        builder.Append("rendererMode");
        AppendField(builder, rendererData, "m_RenderingMode");
        AppendField(builder, rendererData, "m_OpaqueLayerMask");
        AppendField(builder, rendererData, "m_TransparentLayerMask");
        builder.AppendLine();
        AppendRendererFeatures(builder, "rendererMode", 0, rendererData);
    }

    private static void AppendEnviroTraceSummary(StringBuilder builder)
    {
        Enviro.EnviroManager manager = Enviro.EnviroManager.instance;
        if (manager == null)
        {
            builder.AppendLine("enviro manager=null");
            return;
        }

        int cameraCount = manager.Cameras != null ? manager.Cameras.Count : -1;
        builder.AppendLine($"enviro activeSelf={manager.gameObject.activeSelf} active={manager.gameObject.activeInHierarchy} camera='{Describe(manager.Camera)}' optionalFollow='{Describe(manager.optionalFollowTransform)}' cameras={cameraCount} sky={manager.Sky != null} volumetricClouds={manager.VolumetricClouds != null} fog={manager.Fog != null} lighting={manager.Lighting != null}");
        if (manager.Cameras != null)
        {
            for (int i = 0; i < manager.Cameras.Count && i < 4; i++)
            {
                Camera camera = manager.Cameras[i].camera;
                builder.AppendLine($"enviro.camera[{i}] camera='{Describe(camera)}' resetMatrix={manager.Cameras[i].resetMatrix} quality='{Describe(manager.Cameras[i].quality)}'");
            }
        }
    }

    private static void AppendKeywordSummary(StringBuilder builder)
    {
        builder.Append("keywords");
        AppendKeywordValue(builder, "_ADDITIONAL_LIGHTS");
        AppendKeywordValue(builder, "_ADDITIONAL_LIGHTS_VERTEX");
        AppendKeywordValue(builder, "_CLUSTER_LIGHT_LOOP");
        AppendKeywordValue(builder, "_FORWARD_PLUS");
        AppendKeywordValue(builder, "_LIGHT_LAYERS");
        AppendKeywordValue(builder, "_MAIN_LIGHT_SHADOWS");
        AppendKeywordValue(builder, "_ADDITIONAL_LIGHT_SHADOWS");
        builder.AppendLine();
    }

    private static void AppendLightingGlobalSummary(StringBuilder builder)
    {
        builder.AppendLine(
            $"lightingGlobals _AdditionalLightsCount={FormatVector4(Shader.GetGlobalVector("_AdditionalLightsCount"))} " +
            $"_AdditionalLightsDirectionalCount={Shader.GetGlobalInt("_AdditionalLightsDirectionalCount")} " +
            $"_AdditionalLightsPosition0={FormatFirstVector("_AdditionalLightsPosition")} " +
            $"_AdditionalLightsColor0={FormatFirstVector("_AdditionalLightsColor")} " +
            $"_AdditionalLightsAttenuation0={FormatFirstVector("_AdditionalLightsAttenuation")} " +
            $"_AdditionalLightsSpotDir0={FormatFirstVector("_AdditionalLightsSpotDir")} " +
            $"_AdditionalLightsLayerMasks0={FormatFirstVector("_AdditionalLightsLayerMasks")}");
    }

    private static string FormatFirstVector(string propertyName)
    {
        try
        {
            Vector4[] values = Shader.GetGlobalVectorArray(propertyName);
            if (values == null || values.Length == 0)
                return "empty";
            return FormatVector4(values[0]);
        }
        catch (Exception ex)
        {
            return $"error:{ex.GetType().Name}";
        }
    }

    private static void AppendKeywordValue(StringBuilder builder, string keyword)
    {
        try
        {
            builder.Append($" {keyword}={Shader.IsKeywordEnabled(keyword)}");
        }
        catch
        {
            builder.Append($" {keyword}=error");
        }
    }

    private static void AppendCameraTraceSummary(StringBuilder builder)
    {
        Camera[] cameras = Camera.allCameras;
        builder.AppendLine($"cameras count={cameras.Length} main='{Describe(Camera.main)}'");
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
                continue;

            builder.AppendLine($"camera[{i}] '{camera.name}' enabled={camera.enabled} active={camera.gameObject.activeInHierarchy} depth={camera.depth:0.###} clear={camera.clearFlags} culling={FormatMask(camera.cullingMask)} pos={FormatVector(camera.transform.position)} rot={FormatVector(camera.transform.rotation.eulerAngles)} {DescribeCameraData(camera)}");
        }
    }

    private static void AppendLoadedCameraTraceSummary(StringBuilder builder)
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        int logged = 0;
        int loaded = 0;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera.gameObject == null)
                continue;
            if (!camera.gameObject.scene.IsValid() || !camera.gameObject.scene.isLoaded)
                continue;

            loaded++;
            if (logged >= 20)
                continue;

            builder.AppendLine($"loadedCamera[{logged}] '{camera.name}' enabled={camera.enabled} activeSelf={camera.gameObject.activeSelf} active={camera.gameObject.activeInHierarchy} tag='{camera.tag}' type={camera.cameraType} depth={camera.depth:0.###} clear={camera.clearFlags} culling={FormatMask(camera.cullingMask)} target={Describe(camera.targetTexture)} pos={FormatVector(camera.transform.position)} rot={FormatVector(camera.transform.rotation.eulerAngles)} {DescribeCameraData(camera)}");
            logged++;
        }

        builder.AppendLine($"loadedCameras count={loaded} logged={logged}");
    }

    private static void AppendEnabledLightTraceSummary(StringBuilder builder)
    {
        Light[] lights = Resources.FindObjectsOfTypeAll<Light>();
        int logged = 0;
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null || light.gameObject == null)
                continue;
            if (!light.gameObject.scene.IsValid() || !light.gameObject.scene.isLoaded)
                continue;
            if (!light.enabled)
                continue;

            UniversalAdditionalLightData lightData = light.GetComponent<UniversalAdditionalLightData>();
            string urpLayers = lightData != null ? FormatMask((uint)lightData.renderingLayers) : "none";
            builder.AppendLine($"light[{logged}] '{light.name}' active={light.gameObject.activeInHierarchy} layer={FormatLayer(light.gameObject.layer)} type={light.type} intensity={light.intensity:0.###} range={light.range:0.###} shadows={light.shadows} culling={FormatMask(light.cullingMask)} renderLayer={FormatMask(light.renderingLayerMask)} urpLayers={urpLayers} pos={FormatVector(light.transform.position)} rot={FormatVector(light.transform.rotation.eulerAngles)}");
            logged++;
        }

        builder.AppendLine($"enabledLights logged={logged}");
    }

    private static void AppendPipeline(StringBuilder builder, string label, RenderPipelineAsset pipeline)
    {
        if (pipeline == null)
        {
            builder.AppendLine($"{label}=null");
            return;
        }

        builder.Append($"{label} name='{pipeline.name}' type='{pipeline.GetType().FullName}'");
        AppendProperty(builder, pipeline, "additionalLightsRenderingMode");
        AppendProperty(builder, pipeline, "maxAdditionalLightsCount");
        AppendProperty(builder, pipeline, "supportsAdditionalLightShadows");
        AppendProperty(builder, pipeline, "supportsMainLightShadows");
        AppendProperty(builder, pipeline, "supportsSoftShadows");
        AppendProperty(builder, pipeline, "supportsLightLayers");
        AppendProperty(builder, pipeline, "shadowDistance");
        AppendProperty(builder, pipeline, "renderScale");
        builder.AppendLine();
        AppendRendererDataList(builder, label, pipeline);
    }

    private static void AppendRendererDataList(StringBuilder builder, string label, RenderPipelineAsset pipeline)
    {
        FieldInfo rendererDataListField = pipeline.GetType().GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        object value = rendererDataListField?.GetValue(pipeline);
        if (!(value is Array rendererDataList))
        {
            builder.AppendLine($"{label}.rendererDataList=<not found>");
            return;
        }

        builder.AppendLine($"{label}.rendererDataList count={rendererDataList.Length}");
        for (int i = 0; i < rendererDataList.Length; i++)
        {
            object rendererData = rendererDataList.GetValue(i);
            if (rendererData == null)
            {
                builder.AppendLine($"{label}.renderer[{i}]=null");
                continue;
            }

            Object rendererObject = rendererData as Object;
            builder.Append($"{label}.renderer[{i}] name='{Describe(rendererObject)}' type='{rendererData.GetType().FullName}'");
            AppendField(builder, rendererData, "m_RenderingMode");
            AppendField(builder, rendererData, "m_UseNativeRenderPass");
            AppendField(builder, rendererData, "m_OpaqueLayerMask");
            AppendField(builder, rendererData, "m_TransparentLayerMask");
            AppendField(builder, rendererData, "m_IntermediateTextureMode");
            builder.AppendLine();
            AppendRendererFeatures(builder, label, i, rendererData);
        }
    }

    private static void AppendRendererFeatures(StringBuilder builder, string label, int rendererIndex, object rendererData)
    {
        FieldInfo featuresField = rendererData.GetType().GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
        object value = featuresField?.GetValue(rendererData);
        if (!(value is System.Collections.IList features))
            return;

        builder.AppendLine($"{label}.renderer[{rendererIndex}].features count={features.Count}");
        for (int i = 0; i < features.Count; i++)
        {
            Object feature = features[i] as Object;
            builder.AppendLine($"{label}.renderer[{rendererIndex}].feature[{i}]='{Describe(feature)}' active={GetReflectedValue(feature, "isActive")}");
        }
    }

    private static void AppendShaderKeyword(StringBuilder builder, string keyword)
    {
        try
        {
            builder.AppendLine($"shaderKeyword {keyword}={Shader.IsKeywordEnabled(keyword)}");
        }
        catch (Exception ex)
        {
            builder.AppendLine($"shaderKeyword {keyword}=<error:{ex.GetType().Name}>");
        }
    }

    private static void AppendSceneViews(StringBuilder builder)
    {
        builder.AppendLine($"sceneViews count={SceneView.sceneViews.Count}");
        for (int i = 0; i < SceneView.sceneViews.Count; i++)
        {
            if (!(SceneView.sceneViews[i] is SceneView sceneView))
                continue;

            Camera camera = sceneView.camera;
            builder.AppendLine($"sceneView[{i}] title='{sceneView.titleContent?.text}' sceneLighting={sceneView.sceneLighting} cameraMode='{sceneView.cameraMode.name}/{sceneView.cameraMode.drawMode}' camera='{Describe(camera)}'");
            if (camera != null)
                builder.AppendLine($"sceneView[{i}].camera enabled={camera.enabled} active={camera.gameObject.activeInHierarchy} clearFlags={camera.clearFlags} cullingMask={FormatMask(camera.cullingMask)} pos={FormatVector(camera.transform.position)} rot={FormatVector(camera.transform.rotation.eulerAngles)}");
        }
    }

    private static void AppendSelection(StringBuilder builder)
    {
        Object[] selected = Selection.objects;
        builder.AppendLine($"selection count={selected.Length}");
        for (int i = 0; i < selected.Length && i < 8; i++)
            AppendObjectRenderers(builder, $"selection[{i}]", selected[i]);
    }

    private static void AppendNamedObject(StringBuilder builder, string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
        {
            builder.AppendLine($"namedObject '{objectName}'=null");
            return;
        }

        AppendObjectRenderers(builder, $"namedObject '{objectName}'", target);
    }

    private static void AppendObjectRenderers(StringBuilder builder, string label, Object unityObject)
    {
        GameObject gameObject = null;
        if (unityObject is GameObject selectedGameObject)
            gameObject = selectedGameObject;
        else if (unityObject is Component component)
            gameObject = component.gameObject;

        if (gameObject == null)
        {
            builder.AppendLine($"{label} object='{Describe(unityObject)}' gameObject=null");
            return;
        }

        builder.AppendLine($"{label} object='{Describe(gameObject)}' active={gameObject.activeInHierarchy} layer={FormatLayer(gameObject.layer)} pos={FormatVector(gameObject.transform.position)} rot={FormatVector(gameObject.transform.rotation.eulerAngles)}");
        Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length && i < 8; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material material = renderer.sharedMaterial;
            builder.Append($"{label}.renderer[{i}] name='{renderer.name}' layer={FormatLayer(renderer.gameObject.layer)} enabled={renderer.enabled} visible={renderer.isVisible} shadowCasting={renderer.shadowCastingMode} receiveShadows={renderer.receiveShadows} renderingLayerMask={FormatMask(renderer.renderingLayerMask)} material='{Describe(material)}'");
            AppendMaterial(builder, material);
            builder.AppendLine();
        }
    }

    private static void AppendMaterial(StringBuilder builder, Material material)
    {
        if (material == null)
            return;

        builder.Append($" shader='{(material.shader != null ? material.shader.name : "null")}' queue={material.renderQueue}");
        AppendMaterialColor(builder, material, "_BaseColor");
        AppendMaterialColor(builder, material, "_Color");
        AppendMaterialFloat(builder, material, "_Surface");
        AppendMaterialFloat(builder, material, "_AlphaClip");
        AppendMaterialFloat(builder, material, "_ZWrite");
        AppendMaterialFloat(builder, material, "_Cull");
        AppendMaterialFloat(builder, material, "_Smoothness");
        AppendMaterialFloat(builder, material, "_Metallic");
    }

    private static string DescribeCameraData(Camera camera)
    {
        if (camera == null)
            return "cameraData=null";

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
            return "cameraData=none";

        return $"cameraData renderType={cameraData.renderType} renderPostProcessing={cameraData.renderPostProcessing} requiresDepth={cameraData.requiresDepthTexture} requiresColor={cameraData.requiresColorTexture} renderShadows={cameraData.renderShadows} renderer={(cameraData.scriptableRenderer != null ? cameraData.scriptableRenderer.GetType().Name : "null")}";
    }

    private static void AppendProperty(StringBuilder builder, object instance, string name)
    {
        builder.Append($" {name}={GetReflectedValue(instance, name)}");
    }

    private static void AppendField(StringBuilder builder, object instance, string name)
    {
        FieldInfo field = instance?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        builder.Append($" {name}={(field != null ? FormatObject(field.GetValue(instance)) : "<not found>")}");
    }

    private static string GetReflectedValue(object instance, string name)
    {
        if (instance == null)
            return "null";

        PropertyInfo property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null || property.GetIndexParameters().Length > 0)
            return "<not found>";

        try
        {
            return FormatObject(property.GetValue(instance, null));
        }
        catch (Exception ex)
        {
            return $"<error:{ex.GetType().Name}>";
        }
    }

    private static void AppendMaterialColor(StringBuilder builder, Material material, string propertyName)
    {
        if (material != null && material.HasProperty(propertyName))
            builder.Append($" {propertyName}={FormatColor(material.GetColor(propertyName))}");
    }

    private static void AppendMaterialFloat(StringBuilder builder, Material material, string propertyName)
    {
        if (material != null && material.HasProperty(propertyName))
            builder.Append($" {propertyName}={material.GetFloat(propertyName):0.###}");
    }

    private static string Describe(Object value)
    {
        return value == null ? "null" : $"{value.name} ({value.GetType().Name})";
    }

    private static string FormatMask(int value)
    {
        return $"0x{value:X8}";
    }

    private static string FormatMask(uint value)
    {
        return $"0x{value:X8}";
    }

    private static string FormatLayer(int layer)
    {
        string name = LayerMask.LayerToName(layer);
        return string.IsNullOrWhiteSpace(name) ? layer.ToString() : $"{layer}:{name}";
    }

    private static string FormatColor(Color color)
    {
        return $"RGBA({color.r:0.###}, {color.g:0.###}, {color.b:0.###}, {color.a:0.###})";
    }

    private static string FormatObject(object value)
    {
        if (value == null)
            return "null";
        if (value is Object unityObject)
            return Describe(unityObject);
        if (value is LayerMask layerMask)
            return FormatMask(layerMask.value);
        if (value is int intValue)
            return intValue.ToString();
        if (value is uint uintValue)
            return FormatMask(unchecked((int)uintValue));
        if (value is float floatValue)
            return floatValue.ToString("0.###");
        if (value is bool boolValue)
            return boolValue ? "true" : "false";
        return value.ToString();
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
    }

    private static string FormatVector4(Vector4 value)
    {
        return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###}, {value.w:0.###})";
    }
}
#endif
