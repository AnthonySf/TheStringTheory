using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

public sealed class GuitarTunerModelPreview
{
    private static readonly bool UseDiagnosticPartColors = false;
    private static readonly bool DebugColorMappedPegsAlways = false;
    private static readonly bool DebugShowHeadstockIslandLabels = false;
    private const string FadeShaderName = "Hidden/StringTheory/TunerPreviewFade";
    private const string FadeShaderResourcePath = "Shaders/TunerPreviewFade";
    private const string DiagnosticShaderName = "Hidden/StringTheory/TunerDiagnosticPartColors";
    private const string DiagnosticShaderResourcePath = "Shaders/TunerDiagnosticPartColors";
    private const string GuitarResourcePath = "3d/IbanezJem/GuitarSplit";
    private const string GuitarDiffuseTexturePath = "3d/IbanezJem/IbanezJem_Diffuse";
    private const string GuitarNormalTexturePath = "3d/IbanezJem/IbanezJem_Normal";
    private const string GuitarSpecularTexturePath = "3d/IbanezJem/IbanezJem_Specular";
    private const string GuitarGlossinessTexturePath = "3d/IbanezJem/IbanezJem_Glossiness";
    private const string GuitarOcclusionTexturePath = "3d/IbanezJem/IbanezJem_Occlusion";
    private const string BassResourcePath = "3d/IbanezJem/Bass/BassSplit";
    private const string BassDiffuseTexturePath = "3d/IbanezJem/Bass/Guitar_Guitar_mat_BaseColor";
    private const string BassNormalTexturePath = "3d/IbanezJem/Bass/Guitar_Guitar_mat_Normal";
    private const string BassMetallicTexturePath = "3d/IbanezJem/Bass/Guitar_Guitar_mat_Metallic";
    private const string BassRoughnessTexturePath = "3d/IbanezJem/Bass/Guitar_Guitar_mat_Roughness";
    private const string BassOcclusionTexturePath = "3d/IbanezJem/Bass/Guitar_Guitar_mat_Ambient_Occlusion";
    private const int PreviewLayer = 30;
    private const int TextureWidth = 768;
    private const int TextureHeight = 1024;
    private const float FadeStart = 0.70f;
    private const float FadeEnd = 0.98f;
    private static readonly int FadeStartId = Shader.PropertyToID("_FadeStart");
    private static readonly int FadeEndId = Shader.PropertyToID("_FadeEnd");
    private static readonly int DiagnosticTintId = Shader.PropertyToID("_Tint");
    private static readonly Vector3 PreviewOrigin = new Vector3(10000f, 10000f, 10000f);
    // These indices use the same sorted mesh-island order as the diagnostic color view.
    // Each row is one string, top to bottom on the six-in-line headstock.
    private static readonly int[][] TuningPegDiagnosticIslandGroups =
    {
        new[] { 111, 117, 103, 135, 138 },
        new[] { 112, 118, 104, 136, 140 },
        new[] { 114, 120, 105, 133, 142 },
        new[] { 109, 115, 106, 132, 141 },
        new[] { 113, 119, 107, 134, 143 },
        new[] { 110, 116, 108, 137, 139 }
    };
    private static readonly Color[] DiagnosticIslandColors =
    {
        new Color(1.00f, 0.06f, 0.04f, 1f),
        new Color(1.00f, 0.86f, 0.02f, 1f),
        new Color(0.00f, 0.92f, 1.00f, 1f),
        new Color(1.00f, 0.38f, 0.00f, 1f),
        new Color(0.08f, 1.00f, 0.12f, 1f),
        new Color(1.00f, 0.00f, 1.00f, 1f),
        new Color(0.34f, 0.44f, 1.00f, 1f),
        new Color(0.00f, 1.00f, 0.68f, 1f),
        new Color(1.00f, 0.22f, 0.48f, 1f),
        new Color(0.78f, 0.40f, 1.00f, 1f),
        new Color(0.92f, 1.00f, 0.22f, 1f),
        new Color(0.16f, 0.74f, 1.00f, 1f)
    };
    private static readonly Color[] DebugPegColors =
    {
        new Color(1.00f, 0.04f, 0.02f, 1f),
        new Color(1.00f, 0.92f, 0.02f, 1f),
        new Color(0.00f, 0.92f, 1.00f, 1f),
        new Color(1.00f, 0.42f, 0.00f, 1f),
        new Color(0.04f, 1.00f, 0.12f, 1f),
        new Color(1.00f, 0.00f, 1.00f, 1f)
    };

    private GameObject sceneRoot;
    private Transform modelPivot;
    private Camera previewCamera;
    private RenderTexture cameraRenderTexture;
    private RenderTexture renderTexture;
    private Material fadeMaterial;
    private Shader diagnosticShader;
    private readonly List<Material> ownedMaterials = new List<Material>();
    private readonly List<Mesh> ownedMeshes = new List<Mesh>();
    private readonly List<PegHighlightRenderer> pegHighlightRenderers = new List<PegHighlightRenderer>();
    private readonly List<DebugIslandMarkerState> debugIslandMarkerStates = new List<DebugIslandMarkerState>();
    private readonly List<DebugIslandMarker> debugIslandMarkers = new List<DebugIslandMarker>();
    private Texture2D diffuseTexture;
    private Texture2D normalTexture;
    private Texture2D specularTexture;
    private Texture2D glossinessTexture;
    private Texture2D occlusionTexture;
    private Bounds localBounds;
    private GuitarTunerInstrument currentInstrument = GuitarTunerInstrument.Guitar;
    private int tuningPartCount = 6;
    private bool initialized;
    private bool modelLoaded;
    private bool loggedPegSplit;
    private bool loggedNamedPegSetup;
    private bool warnedMissingPegCandidates;
    private bool warnedNoPegHighlightRenderers;
    private int activePegIndex = 0;
    private Color activePegColor = Color.white;

    public RenderTexture Texture => renderTexture;
    public bool IsReady => initialized && modelLoaded && renderTexture != null;
    public bool ShowDebugIslandLabels => DebugShowHeadstockIslandLabels;
    public IReadOnlyList<DebugIslandMarker> DebugIslandMarkers => debugIslandMarkers;

    public void Initialize(Transform parent)
    {
        if (initialized)
            return;

        sceneRoot = new GameObject("GuitarTunerModelPreviewScene");
        sceneRoot.hideFlags = HideFlags.DontSave;
        sceneRoot.transform.SetParent(parent, false);
        sceneRoot.transform.position = PreviewOrigin;
        sceneRoot.SetActive(false);

        modelPivot = new GameObject("ModelPivot").transform;
        modelPivot.SetParent(sceneRoot.transform, false);

        EnsureRenderTexture();
        CreateCamera();
        CreateLights();
        LoadModel(GetModelDefinition(currentInstrument));

        initialized = true;
    }

    public void SetInstrument(GuitarTunerInstrument instrument)
    {
        GuitarTunerInstrument normalized = instrument == GuitarTunerInstrument.Bass
            ? GuitarTunerInstrument.Bass
            : GuitarTunerInstrument.Guitar;
        if (currentInstrument == normalized && modelLoaded)
            return;

        currentInstrument = normalized;
        if (!initialized)
            return;

        bool wasVisible = sceneRoot != null && sceneRoot.activeSelf;
        ClearLoadedModelResources();
        LoadModel(GetModelDefinition(currentInstrument));
        SetVisible(wasVisible);
    }

    public void SetVisible(bool visible)
    {
        if (sceneRoot != null)
            sceneRoot.SetActive(visible && modelLoaded);
    }

    public void SetActiveTuningPeg(int pegIndex, Color stringColor)
    {
        activePegIndex = pegIndex >= 0 && pegIndex < tuningPartCount ? pegIndex : -1;
        activePegColor = stringColor;
        UpdatePegHighlightMaterials();
        if (activePegIndex >= 0 && pegHighlightRenderers.Count == 0 && !warnedNoPegHighlightRenderers)
        {
            warnedNoPegHighlightRenderers = true;
            Debug.LogWarning("[GuitarTuner] No isolated tuner peg/string renderers are available. The active string highlight cannot be shown.");
        }
    }

    public void Render(float introProgress)
    {
        if (!IsReady)
            return;

        sceneRoot.SetActive(true);
        ConfigureModelAndCamera(Mathf.Clamp01(introProgress));
        UpdateDebugIslandMarkers();
        UpdatePegHighlightMaterials();
        previewCamera.Render();
        ApplyOutputFade();
    }

    public void Dispose()
    {
        if (cameraRenderTexture != null)
        {
            cameraRenderTexture.Release();
            UnityEngine.Object.Destroy(cameraRenderTexture);
            cameraRenderTexture = null;
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
            UnityEngine.Object.Destroy(renderTexture);
            renderTexture = null;
        }

        if (fadeMaterial != null)
        {
            UnityEngine.Object.Destroy(fadeMaterial);
            fadeMaterial = null;
        }

        ClearLoadedModelResources();

        if (sceneRoot != null)
        {
            UnityEngine.Object.Destroy(sceneRoot);
            sceneRoot = null;
            modelPivot = null;
            previewCamera = null;
        }
    }

    private void ClearLoadedModelResources()
    {
        modelLoaded = false;
        localBounds = default;
        pegHighlightRenderers.Clear();
        debugIslandMarkerStates.Clear();
        debugIslandMarkers.Clear();
        loggedPegSplit = false;
        loggedNamedPegSetup = false;
        warnedMissingPegCandidates = false;
        warnedNoPegHighlightRenderers = false;

        if (modelPivot != null)
        {
            for (int i = modelPivot.childCount - 1; i >= 0; i--)
            {
                Transform child = modelPivot.GetChild(i);
                if (child != null)
                {
                    child.gameObject.SetActive(false);
                    child.SetParent(null, false);
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }
        }

        for (int i = 0; i < ownedMaterials.Count; i++)
        {
            if (ownedMaterials[i] != null)
                UnityEngine.Object.Destroy(ownedMaterials[i]);
        }
        ownedMaterials.Clear();

        for (int i = 0; i < ownedMeshes.Count; i++)
        {
            if (ownedMeshes[i] != null)
                UnityEngine.Object.Destroy(ownedMeshes[i]);
        }
        ownedMeshes.Clear();
    }

    private void EnsureRenderTexture()
    {
        if (cameraRenderTexture == null)
        {
            cameraRenderTexture = CreatePreviewTexture("GuitarTunerHeadPreviewRaw", 4);
        }

        if (renderTexture == null)
            renderTexture = CreatePreviewTexture("GuitarTunerHeadPreview", 1);
    }

    private static RenderTexture CreatePreviewTexture(string textureName, int antiAliasing)
    {
        RenderTexture texture = new RenderTexture(TextureWidth, TextureHeight, 24, RenderTextureFormat.ARGB32)
        {
            name = textureName,
            antiAliasing = Mathf.Max(1, antiAliasing),
            useMipMap = false,
            autoGenerateMips = false,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        texture.Create();
        return texture;
    }

    private void CreateCamera()
    {
        GameObject cameraObject = new GameObject("GuitarTunerModelPreviewCamera");
        cameraObject.hideFlags = HideFlags.DontSave;
        cameraObject.transform.SetParent(sceneRoot.transform, false);

        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.enabled = false;
        previewCamera.orthographic = true;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 25f;
        previewCamera.allowHDR = true;
        previewCamera.allowMSAA = true;
        previewCamera.targetTexture = cameraRenderTexture != null ? cameraRenderTexture : renderTexture;
        previewCamera.stereoTargetEye = StereoTargetEyeMask.None;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
        UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderType = CameraRenderType.Base;
        cameraData.renderPostProcessing = false;
        cameraData.requiresDepthTexture = false;
        cameraData.requiresColorTexture = false;
#endif
    }

    private void ApplyOutputFade()
    {
        if (cameraRenderTexture == null || renderTexture == null)
            return;

        EnsureFadeMaterial();
        if (fadeMaterial == null || fadeMaterial.shader == null || !fadeMaterial.shader.isSupported)
        {
            Graphics.Blit(cameraRenderTexture, renderTexture);
            return;
        }

        fadeMaterial.SetFloat(FadeStartId, FadeStart);
        fadeMaterial.SetFloat(FadeEndId, FadeEnd);
        Graphics.Blit(cameraRenderTexture, renderTexture, fadeMaterial);
    }

    private void EnsureFadeMaterial()
    {
        if (fadeMaterial != null)
            return;

        Shader shader = Resources.Load<Shader>(FadeShaderResourcePath);
        if (shader == null)
            shader = Shader.Find(FadeShaderName);
        if (shader == null)
            return;

        fadeMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private void CreateLights()
    {
        CreateDirectionalLight(
            "GuitarTunerModelKeyLight",
            new Vector3(48f, -34f, 18f),
            new Color(0.80f, 0.94f, 1f, 1f),
            2.35f);

        CreateDirectionalLight(
            "GuitarTunerModelRimLight",
            new Vector3(-24f, 38f, -52f),
            new Color(0.28f, 0.96f, 0.82f, 1f),
            1.25f);
    }

    private Light CreateDirectionalLight(string name, Vector3 eulerAngles, Color color, float intensity)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.hideFlags = HideFlags.DontSave;
        lightObject.transform.SetParent(sceneRoot.transform, false);
        lightObject.transform.rotation = Quaternion.Euler(eulerAngles);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = color;
        light.intensity = intensity;
        light.cullingMask = 1 << PreviewLayer;
        return light;
    }

    private void LoadModel(ModelDefinition definition)
    {
        if (definition == null)
            definition = GetModelDefinition(GuitarTunerInstrument.Guitar);

        tuningPartCount = Mathf.Clamp(definition.StringCount, 1, 6);
        activePegIndex = Mathf.Clamp(activePegIndex, 0, tuningPartCount - 1);
        LoadTextures(definition);

        GameObject guitarAsset = Resources.Load<GameObject>(definition.ResourcePath);
        string loadedResourcePath = definition.ResourcePath;
        if (guitarAsset == null && !string.IsNullOrWhiteSpace(definition.FallbackResourcePath))
        {
            guitarAsset = Resources.Load<GameObject>(definition.FallbackResourcePath);
            loadedResourcePath = definition.FallbackResourcePath;
        }

        if (guitarAsset == null)
        {
            Debug.LogWarning($"[GuitarTuner] Could not load model resource '{definition.ResourcePath}'. The tuner will continue without the 3D preview.");
            return;
        }

        GameObject modelInstance = UnityEngine.Object.Instantiate(guitarAsset, modelPivot);
        modelInstance.name = $"{definition.Instrument}HeadModel";
        modelInstance.hideFlags = HideFlags.DontSave;
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;

        AssignLayerRecursively(modelInstance, PreviewLayer);
        bool hasNamedParts = HasAnyNamedTuningPart(modelInstance.transform);
        PreparePreviewRenderers(modelInstance, !hasNamedParts);
        if (hasNamedParts)
            RegisterNamedTuningPartRenderers(modelInstance);

        CenterModel(modelInstance.transform);
        localBounds = CalculateLocalBounds(modelPivot);
        modelLoaded = localBounds.size.sqrMagnitude > 0.0001f;
        Debug.Log($"[GuitarTuner] Loaded tuner {definition.Instrument.ToString().ToLowerInvariant()} model '{loadedResourcePath}' with namedParts={hasNamedParts}.");
    }

    private void LoadTextures(ModelDefinition definition)
    {
        if (definition == null)
            definition = GetModelDefinition(GuitarTunerInstrument.Guitar);

        diffuseTexture = Resources.Load<Texture2D>(definition.DiffuseTexturePath);
        normalTexture = Resources.Load<Texture2D>(definition.NormalTexturePath);
        specularTexture = Resources.Load<Texture2D>(definition.SpecularTexturePath);
        glossinessTexture = Resources.Load<Texture2D>(definition.GlossinessTexturePath);
        occlusionTexture = Resources.Load<Texture2D>(definition.OcclusionTexturePath);
    }

    private static ModelDefinition GetModelDefinition(GuitarTunerInstrument instrument)
    {
        if (instrument == GuitarTunerInstrument.Bass)
        {
            return new ModelDefinition
            {
                Instrument = GuitarTunerInstrument.Bass,
                StringCount = 4,
                ResourcePath = BassResourcePath,
                FallbackResourcePath = string.Empty,
                DiffuseTexturePath = BassDiffuseTexturePath,
                NormalTexturePath = BassNormalTexturePath,
                SpecularTexturePath = BassMetallicTexturePath,
                GlossinessTexturePath = BassRoughnessTexturePath,
                OcclusionTexturePath = BassOcclusionTexturePath
            };
        }

        return new ModelDefinition
        {
            Instrument = GuitarTunerInstrument.Guitar,
            StringCount = 6,
            ResourcePath = GuitarResourcePath,
            FallbackResourcePath = string.Empty,
            DiffuseTexturePath = GuitarDiffuseTexturePath,
            NormalTexturePath = GuitarNormalTexturePath,
            SpecularTexturePath = GuitarSpecularTexturePath,
            GlossinessTexturePath = GuitarGlossinessTexturePath,
            OcclusionTexturePath = GuitarOcclusionTexturePath
        };
    }

    private void PreparePreviewRenderers(GameObject modelInstance, bool allowMeshIslandFallback)
    {
        debugIslandMarkerStates.Clear();
        debugIslandMarkers.Clear();

        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (UseDiagnosticPartColors && BuildDiagnosticIslandRenderers(renderer))
                continue;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = CreatePreviewMaterial(null, "Fallback");
            }
            else
            {
                for (int m = 0; m < materials.Length; m++)
                    materials[m] = CreatePreviewMaterial(materials[m], $"Material{m}");
                renderer.sharedMaterials = materials;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (allowMeshIslandFallback)
                BuildPegHighlightRenderers(renderer);
        }
    }

    private bool BuildDiagnosticIslandRenderers(Renderer renderer)
    {
        if (!TryGetRendererMesh(renderer, out Mesh mesh))
            return false;

        if (!TryBuildMeshIslands(mesh, "diagnostic", out Vector3[] vertices, out Vector3[] normals, out List<MeshIslandData> islands))
            return false;

        SortIslandsForDiagnosticOrder(islands);

        for (int i = 0; i < islands.Count; i++)
            CreateDiagnosticIslandRenderer(renderer.transform, renderer.gameObject.layer, mesh.name, i, vertices, normals, islands[i].triangles);

        renderer.enabled = false;
        return true;
    }

    private void BuildPegHighlightRenderers(Renderer renderer)
    {
        if (!TryGetRendererMesh(renderer, out Mesh mesh))
            return;
        if (!TryBuildMeshIslands(mesh, "peg highlight", out Vector3[] vertices, out Vector3[] normals, out List<MeshIslandData> islands))
            return;

        SortIslandsForDiagnosticOrder(islands);
        Dictionary<int, int> pegIndexByIslandIndex = BuildTuningPegIndexMap(islands.Count);
        RegisterDebugIslandMarkers(renderer.transform, islands, pegIndexByIslandIndex);
        Debug.Log($"[GuitarTuner] Preparing tuner peg split for renderer '{renderer.name}', mesh '{mesh.name}', islands={islands.Count}, mapped={pegIndexByIslandIndex.Count}.");
        if (pegIndexByIslandIndex.Count == 0)
        {
            if (!warnedMissingPegCandidates)
            {
                warnedMissingPegCandidates = true;
                Debug.LogWarning($"[GuitarTuner] No mapped tuner peg mesh islands were found in '{mesh.name}'. The peg material highlight will stay disabled.");
            }
            return;
        }

        for (int i = 0; i < islands.Count; i++)
        {
            Material baseMaterial = SelectMaterialForIsland(renderer.sharedMaterials, mesh, islands[i].triangles);
            if (pegIndexByIslandIndex.TryGetValue(i, out int pegIndex))
                CreatePegHighlightRenderer(renderer.transform, renderer.gameObject.layer, mesh, pegIndex, i, vertices, normals, islands[i].triangles, baseMaterial);
            else
                CreateStandardIslandRenderer(renderer.transform, renderer.gameObject.layer, mesh, i, vertices, normals, islands[i].triangles, baseMaterial);
        }

        renderer.enabled = false;

        if (!loggedPegSplit && pegHighlightRenderers.Count > 0)
        {
            loggedPegSplit = true;
            Debug.Log($"[GuitarTuner] Split '{mesh.name}' into isolated renderers and mapped {pegHighlightRenderers.Count} tuner peg mesh islands for material highlighting.");
        }

        UpdatePegHighlightMaterials();
    }

    private bool HasAnyNamedTuningPart(Transform root)
    {
        if (root == null)
            return false;

        for (int targetIndex = 0; targetIndex < tuningPartCount; targetIndex++)
        {
            int objectNumber = GetTuningObjectNumber(targetIndex);
            if (FindChildByName(root, $"Peg{objectNumber}") != null)
                return true;
            if (FindChildByName(root, $"String{objectNumber}") != null)
                return true;
        }

        return false;
    }

    private void RegisterNamedTuningPartRenderers(GameObject modelInstance)
    {
        if (modelInstance == null)
            return;

        int registeredRendererCount = 0;
        for (int targetIndex = 0; targetIndex < tuningPartCount; targetIndex++)
        {
            int objectNumber = GetTuningObjectNumber(targetIndex);
            registeredRendererCount += RegisterNamedTuningObjectRenderers(
                modelInstance.transform,
                targetIndex,
                $"Peg{objectNumber}");
            registeredRendererCount += RegisterNamedTuningObjectRenderers(
                modelInstance.transform,
                targetIndex,
                $"String{objectNumber}");
        }

        if (!loggedNamedPegSetup)
        {
            loggedNamedPegSetup = true;
            Debug.Log($"[GuitarTuner] Registered {registeredRendererCount} named tuner peg/string renderers from GuitarSplit.");
        }

        UpdatePegHighlightMaterials();
    }

    private int GetTuningObjectNumber(int targetIndex)
    {
        return Mathf.Clamp(tuningPartCount - targetIndex, 1, tuningPartCount);
    }

    private int RegisterNamedTuningObjectRenderers(Transform modelRoot, int targetIndex, string objectName)
    {
        Transform partRoot = FindChildByName(modelRoot, objectName);
        if (partRoot == null)
        {
            Debug.LogWarning($"[GuitarTuner] Named tuning object '{objectName}' was not found in the tuner guitar model.");
            return 0;
        }

        Renderer[] renderers = partRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning($"[GuitarTuner] Named tuning object '{objectName}' has no renderers.");
            return 0;
        }

        int registeredCount = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] baseMaterials = renderer.sharedMaterials;
            if (baseMaterials == null || baseMaterials.Length == 0)
            {
                baseMaterials = new[] { CreatePreviewMaterial(null, objectName) };
                renderer.sharedMaterials = baseMaterials;
            }

            Material[] activeMaterials = new Material[baseMaterials.Length];
            Color[] baseColors = new Color[baseMaterials.Length];
            for (int materialIndex = 0; materialIndex < baseMaterials.Length; materialIndex++)
            {
                activeMaterials[materialIndex] = CreateActivePegMaterial(targetIndex);
                baseColors[materialIndex] = ReadMaterialColor(baseMaterials[materialIndex]);
            }

            pegHighlightRenderers.Add(new PegHighlightRenderer
            {
                pegIndex = targetIndex,
                renderer = renderer,
                materials = baseMaterials,
                activeMaterials = activeMaterials,
                baseColors = baseColors
            });
            registeredCount++;
        }

        return registeredCount;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        if (string.Equals(root.name, childName, StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildByName(root.GetChild(i), childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private static Dictionary<int, int> BuildTuningPegIndexMap(int islandCount)
    {
        Dictionary<int, int> pegIndexByIslandIndex = new Dictionary<int, int>();
        for (int pegIndex = 0; pegIndex < TuningPegDiagnosticIslandGroups.Length; pegIndex++)
        {
            int[] islandIndices = TuningPegDiagnosticIslandGroups[pegIndex];
            if (islandIndices == null)
                continue;

            for (int i = 0; i < islandIndices.Length; i++)
            {
                int islandIndex = islandIndices[i];
                if (islandIndex >= 0 && islandIndex < islandCount && !pegIndexByIslandIndex.ContainsKey(islandIndex))
                    pegIndexByIslandIndex.Add(islandIndex, pegIndex);
            }
        }

        return pegIndexByIslandIndex;
    }

    private void RegisterDebugIslandMarkers(Transform sourceTransform, List<MeshIslandData> islands, Dictionary<int, int> pegIndexByIslandIndex)
    {
        if (!DebugShowHeadstockIslandLabels || sourceTransform == null || islands == null)
            return;

        for (int i = 0; i < islands.Count; i++)
        {
            int pegIndex = -1;
            bool mapped = pegIndexByIslandIndex != null && pegIndexByIslandIndex.TryGetValue(i, out pegIndex);
            if (!mapped && !LooksUsefulForHeadstockIslandDebug(islands[i].stats))
                continue;

            debugIslandMarkerStates.Add(new DebugIslandMarkerState
            {
                sourceTransform = sourceTransform,
                localCenter = islands[i].stats.Center,
                islandIndex = i,
                pegIndex = mapped ? pegIndex : -1,
                mapped = mapped
            });
        }
    }

    private static bool LooksUsefulForHeadstockIslandDebug(IslandStats stats)
    {
        Vector3 center = stats.Center;
        Vector3 size = stats.Size;
        return center.x >= 2.7f
            && center.x <= 5.85f
            && stats.count >= 20
            && stats.count <= 900
            && size.x <= 1.05f
            && size.y <= 0.48f
            && size.z <= 1.35f;
    }

    private void UpdateDebugIslandMarkers()
    {
        debugIslandMarkers.Clear();
        if (!DebugShowHeadstockIslandLabels || previewCamera == null)
            return;

        for (int i = 0; i < debugIslandMarkerStates.Count; i++)
        {
            DebugIslandMarkerState state = debugIslandMarkerStates[i];
            if (state.sourceTransform == null)
                continue;

            Vector3 viewport = previewCamera.WorldToViewportPoint(state.sourceTransform.TransformPoint(state.localCenter));
            if (viewport.z <= 0f || viewport.x < -0.18f || viewport.x > 1.18f || viewport.y < -0.18f || viewport.y > 1.18f)
                continue;

            debugIslandMarkers.Add(new DebugIslandMarker(
                state.islandIndex,
                state.pegIndex,
                state.mapped,
                new Vector2(viewport.x, viewport.y)));
        }
    }

    private static void SortIslandsForDiagnosticOrder(List<MeshIslandData> islands)
    {
        if (islands == null)
            return;

        islands.Sort((left, right) =>
        {
            Vector3 centerA = left.stats.Center;
            Vector3 centerB = right.stats.Center;
            int compare = centerA.x.CompareTo(centerB.x);
            if (compare != 0)
                return compare;
            compare = centerA.y.CompareTo(centerB.y);
            if (compare != 0)
                return compare;
            return centerA.z.CompareTo(centerB.z);
        });
    }

    private static bool TryGetRendererMesh(Renderer renderer, out Mesh mesh)
    {
        mesh = null;
        if (renderer == null)
            return false;

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        SkinnedMeshRenderer skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
        if (meshFilter != null)
            mesh = meshFilter.sharedMesh;
        else if (skinnedMeshRenderer != null)
            mesh = skinnedMeshRenderer.sharedMesh;

        return mesh != null && mesh.vertexCount > 0;
    }

    private static Material SelectMaterialForIsland(Material[] materials, Mesh mesh, List<int> islandTriangles)
    {
        if (materials == null || materials.Length == 0)
            return null;
        if (mesh == null || islandTriangles == null || islandTriangles.Count < 3)
            return materials[0];

        int islandA = islandTriangles[0];
        int islandB = islandTriangles[1];
        int islandC = islandTriangles[2];
        int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
        for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
        {
            int[] triangles = mesh.GetTriangles(subMesh);
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                if (triangles[t] != islandA || triangles[t + 1] != islandB || triangles[t + 2] != islandC)
                    continue;

                int materialIndex = Mathf.Clamp(subMesh, 0, materials.Length - 1);
                return materials[materialIndex];
            }
        }

        return materials[0];
    }

    private bool TryBuildMeshIslands(
        Mesh mesh,
        string purpose,
        out Vector3[] vertices,
        out Vector3[] normals,
        out List<MeshIslandData> meshIslands)
    {
        vertices = null;
        normals = null;
        meshIslands = null;

        if (mesh == null || mesh.vertexCount <= 0)
            return false;

        try
        {
            vertices = mesh.vertices;
            normals = mesh.normals;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GuitarTuner] Mesh island split skipped for {purpose} on unreadable mesh '{mesh.name}': {ex.Message}");
            return false;
        }

        int vertexCount = vertices != null ? vertices.Length : 0;
        if (vertexCount <= 0)
            return false;

        DisjointSet islands = new DisjointSet(vertexCount);
        Dictionary<VertexPositionKey, int> firstVertexAtPosition = new Dictionary<VertexPositionKey, int>(vertexCount);
        for (int i = 0; i < vertexCount; i++)
        {
            VertexPositionKey key = VertexPositionKey.From(vertices[i]);
            if (firstVertexAtPosition.TryGetValue(key, out int previousIndex))
                islands.Union(previousIndex, i);
            else
                firstVertexAtPosition[key] = i;
        }

        int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
        Dictionary<int, List<int>> trianglesByRoot = new Dictionary<int, List<int>>();
        for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
        {
            int[] triangles = mesh.GetTriangles(subMesh);
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = triangles[t];
                int b = triangles[t + 1];
                int c = triangles[t + 2];
                if (IsValidVertexIndex(a, vertexCount) && IsValidVertexIndex(b, vertexCount))
                    islands.Union(a, b);
                if (IsValidVertexIndex(a, vertexCount) && IsValidVertexIndex(c, vertexCount))
                    islands.Union(a, c);
            }
        }

        for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
        {
            int[] triangles = mesh.GetTriangles(subMesh);
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = triangles[t];
                int b = triangles[t + 1];
                int c = triangles[t + 2];
                if (!IsValidVertexIndex(a, vertexCount) || !IsValidVertexIndex(b, vertexCount) || !IsValidVertexIndex(c, vertexCount))
                    continue;

                int root = islands.Find(a);
                if (!trianglesByRoot.TryGetValue(root, out List<int> islandTriangles))
                {
                    islandTriangles = new List<int>();
                    trianglesByRoot[root] = islandTriangles;
                }

                islandTriangles.Add(a);
                islandTriangles.Add(b);
                islandTriangles.Add(c);
            }
        }

        if (trianglesByRoot.Count == 0)
            return false;

        Dictionary<int, MeshIslandData> dataByRoot = new Dictionary<int, MeshIslandData>();
        foreach (KeyValuePair<int, List<int>> entry in trianglesByRoot)
        {
            dataByRoot[entry.Key] = new MeshIslandData
            {
                root = entry.Key,
                triangles = entry.Value
            };
        }

        for (int i = 0; i < vertexCount; i++)
        {
            int root = islands.Find(i);
            if (!dataByRoot.TryGetValue(root, out MeshIslandData data))
                continue;

            data.stats.Add(vertices[i]);
        }

        meshIslands = new List<MeshIslandData>(dataByRoot.Values);
        return meshIslands.Count > 0;
    }

    private static bool IsValidVertexIndex(int index, int vertexCount)
    {
        return index >= 0 && index < vertexCount;
    }

    private void CreateDiagnosticIslandRenderer(
        Transform parent,
        int layer,
        string sourceMeshName,
        int islandIndex,
        Vector3[] sourceVertices,
        Vector3[] sourceNormals,
        List<int> sourceTriangles)
    {
        if (sourceTriangles == null || sourceTriangles.Count < 3)
            return;

        Dictionary<int, int> remap = new Dictionary<int, int>();
        List<Vector3> islandVertices = new List<Vector3>();
        List<Vector3> islandNormals = sourceNormals != null && sourceNormals.Length == sourceVertices.Length
            ? new List<Vector3>()
            : null;
        List<int> islandTriangles = new List<int>(sourceTriangles.Count);

        for (int i = 0; i < sourceTriangles.Count; i++)
        {
            int sourceIndex = sourceTriangles[i];
            if (!remap.TryGetValue(sourceIndex, out int islandVertexIndex))
            {
                islandVertexIndex = islandVertices.Count;
                remap[sourceIndex] = islandVertexIndex;
                islandVertices.Add(sourceVertices[sourceIndex]);
                islandNormals?.Add(sourceNormals[sourceIndex]);
            }

            islandTriangles.Add(islandVertexIndex);
        }

        Mesh islandMesh = new Mesh
        {
            name = $"{sourceMeshName}_TunerDiagnosticIsland_{islandIndex:00}",
            indexFormat = islandVertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };
        islandMesh.SetVertices(islandVertices);
        if (islandNormals != null && islandNormals.Count == islandVertices.Count)
            islandMesh.SetNormals(islandNormals);
        islandMesh.SetTriangles(islandTriangles, 0);
        islandMesh.RecalculateBounds();
        if (islandNormals == null)
            islandMesh.RecalculateNormals();
        ownedMeshes.Add(islandMesh);

        GameObject islandObject = new GameObject($"TunerDiagnosticIsland_{islandIndex:00}");
        islandObject.hideFlags = HideFlags.DontSave;
        islandObject.layer = layer;
        islandObject.transform.SetParent(parent, false);
        islandObject.transform.localPosition = Vector3.zero;
        islandObject.transform.localRotation = Quaternion.identity;
        islandObject.transform.localScale = Vector3.one;

        MeshFilter islandFilter = islandObject.AddComponent<MeshFilter>();
        islandFilter.sharedMesh = islandMesh;

        MeshRenderer islandRenderer = islandObject.AddComponent<MeshRenderer>();
        islandRenderer.sharedMaterial = CreateDiagnosticMaterial(islandIndex);
        islandRenderer.shadowCastingMode = ShadowCastingMode.Off;
        islandRenderer.receiveShadows = false;
    }

    private void CreatePegHighlightRenderer(
        Transform parent,
        int layer,
        Mesh sourceMesh,
        int pegIndex,
        int islandIndex,
        Vector3[] sourceVertices,
        Vector3[] sourceNormals,
        List<int> sourceTriangles,
        Material baseMaterial)
    {
        Mesh islandMesh = CreateIslandMesh(
            $"{sourceMesh.name}_TunerPegHighlight_{pegIndex:00}_{islandIndex:00}",
            sourceMesh,
            sourceVertices,
            sourceNormals,
            sourceTriangles);
        if (islandMesh == null)
            return;

        GameObject islandObject = new GameObject($"TunerPegHighlight_{pegIndex:00}_{islandIndex:00}");
        islandObject.hideFlags = HideFlags.DontSave;
        islandObject.layer = layer;
        islandObject.transform.SetParent(parent, false);
        islandObject.transform.localPosition = Vector3.zero;
        islandObject.transform.localRotation = Quaternion.identity;
        islandObject.transform.localScale = Vector3.one;

        MeshFilter islandFilter = islandObject.AddComponent<MeshFilter>();
        islandFilter.sharedMesh = islandMesh;

        MeshRenderer islandRenderer = islandObject.AddComponent<MeshRenderer>();
        Material material = CreatePegMaterial(pegIndex, baseMaterial);
        islandRenderer.sharedMaterial = material;
        islandRenderer.shadowCastingMode = ShadowCastingMode.Off;
        islandRenderer.receiveShadows = false;
        islandRenderer.enabled = true;

        pegHighlightRenderers.Add(new PegHighlightRenderer
        {
            pegIndex = pegIndex,
            renderer = islandRenderer,
            materials = new[] { material },
            activeMaterials = new[] { CreateActivePegMaterial(pegIndex) },
            baseColors = new[] { ReadMaterialColor(material) }
        });
    }

    private void CreateStandardIslandRenderer(
        Transform parent,
        int layer,
        Mesh sourceMesh,
        int islandIndex,
        Vector3[] sourceVertices,
        Vector3[] sourceNormals,
        List<int> sourceTriangles,
        Material material)
    {
        Mesh islandMesh = CreateIslandMesh(
            $"{sourceMesh.name}_TunerIsland_{islandIndex:00}",
            sourceMesh,
            sourceVertices,
            sourceNormals,
            sourceTriangles);
        if (islandMesh == null)
            return;

        GameObject islandObject = new GameObject($"TunerIsland_{islandIndex:00}");
        islandObject.hideFlags = HideFlags.DontSave;
        islandObject.layer = layer;
        islandObject.transform.SetParent(parent, false);
        islandObject.transform.localPosition = Vector3.zero;
        islandObject.transform.localRotation = Quaternion.identity;
        islandObject.transform.localScale = Vector3.one;

        MeshFilter islandFilter = islandObject.AddComponent<MeshFilter>();
        islandFilter.sharedMesh = islandMesh;

        MeshRenderer islandRenderer = islandObject.AddComponent<MeshRenderer>();
        islandRenderer.sharedMaterial = material != null ? material : CreatePreviewMaterial(null, $"Island{islandIndex}");
        islandRenderer.shadowCastingMode = ShadowCastingMode.Off;
        islandRenderer.receiveShadows = false;
        islandRenderer.enabled = true;
    }

    private Mesh CreateIslandMesh(
        string meshName,
        Mesh sourceMesh,
        Vector3[] sourceVertices,
        Vector3[] sourceNormals,
        List<int> sourceTriangles,
        float normalOffset = 0f)
    {
        if (sourceVertices == null || sourceTriangles == null || sourceTriangles.Count < 3)
            return null;

        Dictionary<int, int> remap = new Dictionary<int, int>();
        List<Vector3> islandVertices = new List<Vector3>();
        List<Vector3> islandNormals = sourceNormals != null && sourceNormals.Length == sourceVertices.Length
            ? new List<Vector3>()
            : null;
        Vector2[] sourceUv = sourceMesh != null ? sourceMesh.uv : null;
        Vector2[] sourceUv2 = sourceMesh != null ? sourceMesh.uv2 : null;
        Vector4[] sourceTangents = sourceMesh != null ? sourceMesh.tangents : null;
        Color[] sourceColors = sourceMesh != null ? sourceMesh.colors : null;
        List<Vector2> islandUv = sourceUv != null && sourceUv.Length == sourceVertices.Length ? new List<Vector2>() : null;
        List<Vector2> islandUv2 = sourceUv2 != null && sourceUv2.Length == sourceVertices.Length ? new List<Vector2>() : null;
        List<Vector4> islandTangents = sourceTangents != null && sourceTangents.Length == sourceVertices.Length ? new List<Vector4>() : null;
        List<Color> islandColors = sourceColors != null && sourceColors.Length == sourceVertices.Length ? new List<Color>() : null;
        List<int> islandTriangles = new List<int>(sourceTriangles.Count);

        for (int i = 0; i < sourceTriangles.Count; i++)
        {
            int sourceIndex = sourceTriangles[i];
            if (!IsValidVertexIndex(sourceIndex, sourceVertices.Length))
                continue;

            if (!remap.TryGetValue(sourceIndex, out int islandVertexIndex))
            {
                islandVertexIndex = islandVertices.Count;
                remap[sourceIndex] = islandVertexIndex;
                Vector3 vertex = sourceVertices[sourceIndex];
                if (normalOffset > 0f && sourceNormals != null && sourceNormals.Length == sourceVertices.Length)
                    vertex += sourceNormals[sourceIndex].normalized * normalOffset;

                islandVertices.Add(vertex);
                islandNormals?.Add(sourceNormals[sourceIndex]);
                islandUv?.Add(sourceUv[sourceIndex]);
                islandUv2?.Add(sourceUv2[sourceIndex]);
                islandTangents?.Add(sourceTangents[sourceIndex]);
                islandColors?.Add(sourceColors[sourceIndex]);
            }

            islandTriangles.Add(islandVertexIndex);
        }

        if (islandVertices.Count == 0 || islandTriangles.Count < 3)
            return null;

        Mesh islandMesh = new Mesh
        {
            name = meshName,
            indexFormat = islandVertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };
        islandMesh.SetVertices(islandVertices);
        if (islandNormals != null && islandNormals.Count == islandVertices.Count)
            islandMesh.SetNormals(islandNormals);
        if (islandUv != null && islandUv.Count == islandVertices.Count)
            islandMesh.SetUVs(0, islandUv);
        if (islandUv2 != null && islandUv2.Count == islandVertices.Count)
            islandMesh.SetUVs(1, islandUv2);
        if (islandTangents != null && islandTangents.Count == islandVertices.Count)
            islandMesh.SetTangents(islandTangents);
        if (islandColors != null && islandColors.Count == islandVertices.Count)
            islandMesh.SetColors(islandColors);
        islandMesh.SetTriangles(islandTriangles, 0);
        islandMesh.RecalculateBounds();
        if (islandNormals == null)
            islandMesh.RecalculateNormals();
        if (islandTangents == null && islandUv != null && islandNormals != null)
            islandMesh.RecalculateTangents();
        ownedMeshes.Add(islandMesh);
        return islandMesh;
    }

    private Material CreatePegMaterial(int pegIndex, Material baseMaterial)
    {
        Material material;
        if (baseMaterial != null)
        {
            material = new Material(baseMaterial);
        }
        else
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            material = new Material(shader);
            SetMaterialColor(material, new Color(0.70f, 0.70f, 0.66f, 1f));
        }

        material.name = $"TunerPegMaterial_{pegIndex:00}";
        material.hideFlags = HideFlags.DontSave;

        SetMaterialFloat(material, "_Metallic", 0.04f);
        SetMaterialFloat(material, "_Smoothness", 0.82f);
        SetMaterialFloat(material, "_Glossiness", 0.82f);
        if (material.HasProperty("_EmissionColor"))
            material.EnableKeyword("_EMISSION");

        ownedMaterials.Add(material);
        return material;
    }

    private Material CreateActivePegMaterial(int pegIndex)
    {
        EnsureDiagnosticShader();
        Shader shader = diagnosticShader;
        if (shader == null || !shader.isSupported)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            name = $"TunerPegActiveMaterial_{pegIndex:00}",
            hideFlags = HideFlags.DontSave
        };

        SetMaterialColor(material, Color.white);
        if (material.HasProperty(DiagnosticTintId))
            material.SetColor(DiagnosticTintId, Color.white);
        if (material.HasProperty("_EmissionColor"))
            material.EnableKeyword("_EMISSION");
        material.renderQueue = 5000;

        ownedMaterials.Add(material);
        return material;
    }

    private void UpdatePegHighlightMaterials()
    {
        for (int i = 0; i < pegHighlightRenderers.Count; i++)
        {
            PegHighlightRenderer highlight = pegHighlightRenderers[i];
            bool active = DebugColorMappedPegsAlways || (highlight.pegIndex == activePegIndex && highlight.renderer != null && highlight.materials != null && highlight.activeMaterials != null);
            if (highlight.renderer != null)
                highlight.renderer.enabled = true;
            if (highlight.materials == null || highlight.materials.Length == 0)
                continue;

            if (active)
            {
                float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 7.0f) * 0.5f;
                Color baseColor = DebugColorMappedPegsAlways
                    ? DebugPegColors[Mathf.Abs(highlight.pegIndex) % DebugPegColors.Length]
                    : activePegColor;
                Color color = Color.Lerp(baseColor, Color.white, DebugColorMappedPegsAlways ? 0.0f : 0.12f);
                color.a = 1f;
                Color emission = baseColor * Mathf.Lerp(0.45f, 0.95f, pulse);
                for (int materialIndex = 0; materialIndex < highlight.activeMaterials.Length; materialIndex++)
                {
                    Material activeMaterial = highlight.activeMaterials[materialIndex];
                    if (activeMaterial == null)
                        continue;

                    SetMaterialColor(activeMaterial, color);
                    if (activeMaterial.HasProperty(DiagnosticTintId))
                        activeMaterial.SetColor(DiagnosticTintId, color);
                    if (activeMaterial.HasProperty("_EmissionColor"))
                        activeMaterial.SetColor("_EmissionColor", emission);
                }
                if (highlight.renderer != null)
                    highlight.renderer.sharedMaterials = highlight.activeMaterials;
                continue;
            }

            if (highlight.renderer != null)
                highlight.renderer.sharedMaterials = highlight.materials;
            for (int materialIndex = 0; materialIndex < highlight.materials.Length; materialIndex++)
            {
                Material material = highlight.materials[materialIndex];
                if (material == null)
                    continue;

                Color baseColor = highlight.baseColors != null && materialIndex < highlight.baseColors.Length
                    ? highlight.baseColors[materialIndex]
                    : ReadMaterialColor(material);
                SetMaterialColor(material, baseColor);
                if (material.HasProperty("_EmissionColor"))
                    material.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    private Material CreatePreviewMaterial(Material source, string fallbackName)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        Material material = new Material(shader)
        {
            name = source != null ? $"GuitarTuner_{source.name}" : $"GuitarTuner_{fallbackName}",
            hideFlags = HideFlags.DontSave
        };

        if (UseDiagnosticPartColors)
        {
            ConfigureFallbackDiagnosticMaterial(material, fallbackName);
            ownedMaterials.Add(material);
            return material;
        }

        Color color = source != null ? ReadMaterialColor(source) : new Color(0.12f, 0.13f, 0.14f, 1f);
        if (source != null && TryResolveKnownMaterialColor(source.name, out Color knownColor))
            color = knownColor;

        color = EnhancePreviewColor(color);
        SetMaterialColor(material, color);
        SetMaterialFloat(material, "_Metallic", source != null ? ReadMaterialFloat(source, "_Metallic", 0.04f) : 0.08f);
        SetMaterialFloat(material, "_Smoothness", source != null ? ReadMaterialFloat(source, "_Smoothness", 0.46f) : 0.42f);
        SetMaterialFloat(material, "_Glossiness", source != null ? ReadMaterialFloat(source, "_Glossiness", 0.46f) : 0.42f);
        ApplyPreviewTextures(material);

        ownedMaterials.Add(material);
        return material;
    }

    private void ConfigureFallbackDiagnosticMaterial(Material material, string key)
    {
        if (material == null)
            return;

        EnsureDiagnosticShader();
        if (diagnosticShader != null && diagnosticShader.isSupported)
            material.shader = diagnosticShader;

        Color color = DiagnosticIslandColors[Mathf.Abs(StableColorIndex(key)) % DiagnosticIslandColors.Length];
        SetMaterialColor(material, color);
        if (material.HasProperty(DiagnosticTintId))
            material.SetColor(DiagnosticTintId, color);
    }

    private static int StableColorIndex(string key)
    {
        if (string.IsNullOrEmpty(key))
            return 0;

        unchecked
        {
            int hash = 17;
            for (int i = 0; i < key.Length; i++)
                hash = hash * 31 + key[i];
            return hash;
        }
    }

    private Material CreateDiagnosticMaterial(int islandIndex)
    {
        EnsureDiagnosticShader();
        Shader shader = diagnosticShader;
        if (shader == null || !shader.isSupported)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            name = $"TunerDiagnosticIsland_{islandIndex:00}",
            hideFlags = HideFlags.DontSave
        };

        Color color = DiagnosticIslandColors[islandIndex % DiagnosticIslandColors.Length];
        SetMaterialColor(material, color);
        if (material.HasProperty(DiagnosticTintId))
            material.SetColor(DiagnosticTintId, color);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", color * 0.12f);

        ownedMaterials.Add(material);
        return material;
    }

    private void EnsureDiagnosticShader()
    {
        if (diagnosticShader != null)
            return;

        diagnosticShader = Resources.Load<Shader>(DiagnosticShaderResourcePath);
        if (diagnosticShader == null)
            diagnosticShader = Shader.Find(DiagnosticShaderName);
    }

    private void ApplyPreviewTextures(Material material)
    {
        if (material == null)
            return;

        SetMaterialTexture(material, "_BaseMap", diffuseTexture);
        SetMaterialTexture(material, "_MainTex", diffuseTexture);
        SetMaterialTexture(material, "_BumpMap", normalTexture);
        SetMaterialTexture(material, "_SpecGlossMap", specularTexture);
        SetMaterialTexture(material, "_MetallicGlossMap", glossinessTexture);
        SetMaterialTexture(material, "_OcclusionMap", occlusionTexture);

        if (normalTexture != null)
            material.EnableKeyword("_NORMALMAP");
        if (specularTexture != null)
            material.EnableKeyword("_SPECGLOSSMAP");
        if (glossinessTexture != null)
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        if (occlusionTexture != null)
            material.EnableKeyword("_OCCLUSIONMAP");

        SetMaterialFloat(material, "_BumpScale", 0.70f);
        SetMaterialFloat(material, "_OcclusionStrength", 0.72f);
        SetMaterialFloat(material, "_GlossMapScale", 0.42f);
        SetMaterialFloat(material, "_WorkflowMode", 1f);
    }

    private static Color ReadMaterialColor(Material material)
    {
        if (material == null)
            return new Color(0.12f, 0.13f, 0.14f, 1f);
        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");
        return material.color;
    }

    private static float ReadMaterialFloat(Material material, string propertyName, float fallback)
    {
        return material != null && material.HasProperty(propertyName)
            ? material.GetFloat(propertyName)
            : fallback;
    }

    private static Color EnhancePreviewColor(Color color)
    {
        color.a = 1f;
        if (color.maxColorComponent < 0.035f)
            return new Color(0.025f, 0.026f, 0.028f, 1f);

        return Color.Lerp(color, Color.white, 0.04f);
    }

    private static bool TryResolveKnownMaterialColor(string materialName, out Color color)
    {
        string normalized = NormalizeMaterialName(materialName);
        switch (normalized)
        {
            case "blinn1":
            case "blinn1sg":
            case "lambert4":
            case "lambert4sg":
                color = new Color(0.18f, 0.0f, 0.0f, 1f);
                return true;

            case "lambert2":
            case "lambert2sg":
                color = new Color(0.006f, 0.006f, 0.007f, 1f);
                return true;

            case "lambert5":
            case "lambert5sg":
                color = new Color(0.24f, 0.21f, 0.21f, 1f);
                return true;

            case "lambert1":
            case "lambert3":
            case "lambert6":
            case "phong1":
            case "lambert1sg":
            case "lambert3sg":
            case "lambert6sg":
            case "phong1sg":
                color = new Color(0.86f, 0.84f, 0.80f, 1f);
                return true;

            case "mia_material_x1":
            case "mia_material_x2":
            case "mia_material_x3":
            case "mia_material_x4":
            case "mia_material_x5":
            case "mia_material_x6":
            case "mia_material_x1sg":
            case "mia_material_x2sg":
            case "mia_material_x3sg":
            case "mia_material_x4sg":
            case "mia_material_x5sg":
            case "mia_material_x6sg":
                color = new Color(0.70f, 0.70f, 0.66f, 1f);
                return true;

            default:
                color = Color.white;
                return false;
        }
    }

    private static string NormalizeMaterialName(string materialName)
    {
        if (string.IsNullOrWhiteSpace(materialName))
            return string.Empty;

        string normalized = materialName.Trim().ToLowerInvariant();
        int instanceIndex = normalized.IndexOf(" (instance)", StringComparison.Ordinal);
        if (instanceIndex >= 0)
            normalized = normalized.Substring(0, instanceIndex);

        return normalized.Replace(" ", string.Empty);
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    private static void SetMaterialTexture(Material material, string propertyName, Texture texture)
    {
        if (texture != null && material.HasProperty(propertyName))
            material.SetTexture(propertyName, texture);
    }

    private static void CenterModel(Transform model)
    {
        Bounds bounds = CalculateLocalBounds(model);
        model.localPosition -= bounds.center;
    }

    private static Bounds CalculateLocalBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            for (int c = 0; c < corners.Length; c++)
            {
                Vector3 localPoint = root.InverseTransformPoint(corners[c]);
                if (!hasBounds)
                {
                    bounds = new Bounds(localPoint, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(localPoint);
                }
            }
        }

        return bounds;
    }

    private void ConfigureModelAndCamera(float introProgress)
    {
        float eased = SmoothStep(introProgress);
        float turnDegrees = Mathf.Lerp(-15f, -7f, eased);
        float longAxisTurnDegrees = Mathf.Sin(Time.unscaledTime * 0.82f) * 6.0f * eased;
        modelPivot.localRotation = Quaternion.Euler(longAxisTurnDegrees, turnDegrees, 0f);

        float length = Mathf.Max(localBounds.size.x, 0.01f);
        float headLength = Mathf.Clamp(length * 0.24f, length * 0.16f, length * 0.30f);
        float headCenterX = localBounds.max.x - headLength * 0.43f;
        Vector3 localTarget = new Vector3(headCenterX, localBounds.center.y, localBounds.center.z);
        Vector3 worldTarget = modelPivot.TransformPoint(localTarget);

        previewCamera.transform.position = worldTarget + Vector3.up * 6.0f;
        previewCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.left);
        previewCamera.orthographicSize = Mathf.Lerp(headLength * 0.68f, headLength * 0.52f, eased);
        previewCamera.aspect = TextureWidth / (float)TextureHeight;
    }

    private static float SmoothStep(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static void AssignLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;
        Transform transform = root.transform;
        for (int i = 0; i < transform.childCount; i++)
            AssignLayerRecursively(transform.GetChild(i).gameObject, layer);
    }

    private struct IslandStats
    {
        public int count;
        public Vector3 center;
        public Vector3 min;
        public Vector3 max;

        public Vector3 Center => count > 0 ? center / count : Vector3.zero;
        public Vector3 Size => count > 0 ? max - min : Vector3.zero;

        public void Add(Vector3 vertex)
        {
            if (count == 0)
            {
                min = vertex;
                max = vertex;
            }
            else
            {
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            center += vertex;
            count++;
        }
    }

    private sealed class MeshIslandData
    {
        public int root;
        public List<int> triangles;
        public IslandStats stats;
    }

    private sealed class PegHighlightRenderer
    {
        public int pegIndex;
        public Renderer renderer;
        public Material[] materials;
        public Material[] activeMaterials;
        public Color[] baseColors;
    }

    private sealed class ModelDefinition
    {
        public GuitarTunerInstrument Instrument;
        public int StringCount;
        public string ResourcePath;
        public string FallbackResourcePath;
        public string DiffuseTexturePath;
        public string NormalTexturePath;
        public string SpecularTexturePath;
        public string GlossinessTexturePath;
        public string OcclusionTexturePath;
    }

    public readonly struct DebugIslandMarker
    {
        public readonly int islandIndex;
        public readonly int pegIndex;
        public readonly bool mapped;
        public readonly Vector2 viewport;

        public DebugIslandMarker(int islandIndex, int pegIndex, bool mapped, Vector2 viewport)
        {
            this.islandIndex = islandIndex;
            this.pegIndex = pegIndex;
            this.mapped = mapped;
            this.viewport = viewport;
        }
    }

    private sealed class DebugIslandMarkerState
    {
        public Transform sourceTransform;
        public Vector3 localCenter;
        public int islandIndex;
        public int pegIndex;
        public bool mapped;
    }

    private readonly struct VertexPositionKey : IEquatable<VertexPositionKey>
    {
        private const float Quantization = 10000f;
        private readonly int x;
        private readonly int y;
        private readonly int z;

        private VertexPositionKey(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static VertexPositionKey From(Vector3 value)
        {
            return new VertexPositionKey(
                Mathf.RoundToInt(value.x * Quantization),
                Mathf.RoundToInt(value.y * Quantization),
                Mathf.RoundToInt(value.z * Quantization));
        }

        public bool Equals(VertexPositionKey other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is VertexPositionKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + x;
                hash = hash * 31 + y;
                hash = hash * 31 + z;
                return hash;
            }
        }
    }

    private sealed class DisjointSet
    {
        private readonly int[] parent;
        private readonly byte[] rank;

        public DisjointSet(int count)
        {
            parent = new int[count];
            rank = new byte[count];
            for (int i = 0; i < count; i++)
                parent[i] = i;
        }

        public int Find(int value)
        {
            int root = value;
            while (parent[root] != root)
                root = parent[root];

            while (parent[value] != value)
            {
                int next = parent[value];
                parent[value] = root;
                value = next;
            }

            return root;
        }

        public void Union(int left, int right)
        {
            int leftRoot = Find(left);
            int rightRoot = Find(right);
            if (leftRoot == rightRoot)
                return;

            if (rank[leftRoot] < rank[rightRoot])
            {
                parent[leftRoot] = rightRoot;
            }
            else if (rank[leftRoot] > rank[rightRoot])
            {
                parent[rightRoot] = leftRoot;
            }
            else
            {
                parent[rightRoot] = leftRoot;
                rank[leftRoot]++;
            }
        }
    }
}
