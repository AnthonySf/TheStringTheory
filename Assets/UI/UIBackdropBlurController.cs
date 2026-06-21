using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

public sealed class UIBackdropBlurController : MonoBehaviour
{
    private const string BlurShaderName = "Hidden/StringTheory/UIBackdropBlur";
    private const string BlurShaderResourcePath = "Shaders/UIBackdropBlur";
    private static readonly int BlurDirectionId = Shader.PropertyToID("_BlurDirection");
    private static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");
    private const int Downsample = 4;
    private const int GameplayBackgroundLayer = 2;
    private const float BlurSize = 4.5f;
    private const float DefaultBlurBrightness = 0.48f;
    private const int BlurPassPairs = 3;

    public VisualElement TargetElement { get; set; }
    public Camera SourceCamera { get; set; }
    public float Brightness { get; set; } = DefaultBlurBrightness;

    private Camera captureCamera;
    private Material blurMaterial;
    private RenderTexture sceneTexture;
    private RenderTexture blurTextureA;
    private RenderTexture blurTextureB;
    private RenderTexture targetTexture;
    private int textureWidth = -1;
    private int textureHeight = -1;
    private int targetTextureWidth = -1;
    private int targetTextureHeight = -1;

    private void LateUpdate()
    {
        if (!ShouldRender())
            return;

        EnsureBlurMaterial();
        if (blurMaterial == null || blurMaterial.shader == null || !blurMaterial.shader.isSupported)
        {
            if (TargetElement != null)
                TargetElement.style.backgroundImage = StyleKeyword.None;
            return;
        }

        int width = Mathf.Max(256, Mathf.CeilToInt(Screen.width / (float)Downsample));
        int height = Mathf.Max(144, Mathf.CeilToInt(Screen.height / (float)Downsample));
        Rect targetScreenBounds = GetTargetScreenBounds();
        int targetWidth = Mathf.Max(16, Mathf.CeilToInt(targetScreenBounds.width / (float)Downsample));
        int targetHeight = Mathf.Max(16, Mathf.CeilToInt(targetScreenBounds.height / (float)Downsample));

        EnsureRenderTextures(width, height);
        if (!RenderLiveCameraToSceneTexture())
            return;

        RenderTexture source = sceneTexture;
        for (int pass = 0; pass < BlurPassPairs; pass++)
        {
            blurMaterial.SetVector(BlurDirectionId, new Vector2(1f / textureWidth, 0f));
            blurMaterial.SetFloat(BlurSizeId, BlurSize);
            Graphics.Blit(source, blurTextureA, blurMaterial, 0);

            blurMaterial.SetVector(BlurDirectionId, new Vector2(0f, 1f / textureHeight));
            Graphics.Blit(blurTextureA, blurTextureB, blurMaterial, 0);
            source = blurTextureB;
        }

        if (TargetElement == null)
            return;

        EnsureTargetTexture(targetWidth, targetHeight);
        int sourceX = Mathf.Clamp(Mathf.FloorToInt(targetScreenBounds.xMin / Downsample), 0, Mathf.Max(0, textureWidth - targetWidth));
        int sourceY = Mathf.Clamp(textureHeight - Mathf.CeilToInt(targetScreenBounds.yMax / Downsample), 0, Mathf.Max(0, textureHeight - targetHeight));
        Vector2 scale = new Vector2(targetWidth / (float)textureWidth, targetHeight / (float)textureHeight);
        Vector2 offset = new Vector2(sourceX / (float)textureWidth, sourceY / (float)textureHeight);
        Graphics.Blit(blurTextureB, targetTexture, scale, offset);

        TargetElement.style.backgroundImage = StyleKeyword.None;
        TargetElement.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(targetTexture));
        TargetElement.style.backgroundSize = new BackgroundSize(Length.Percent(100f), Length.Percent(100f));
        TargetElement.style.unityBackgroundImageTintColor = new Color(Brightness, Brightness, Brightness, 1f);
    }

    private bool ShouldRender()
    {
        return TargetElement != null
            && TargetElement.style.display.value != DisplayStyle.None
            && TargetElement.worldBound.width > 16f
            && TargetElement.worldBound.height > 16f;
    }

    private Rect GetTargetScreenBounds()
    {
        if (TargetElement == null)
            return default;

        Rect targetBounds = TargetElement.worldBound;
        VisualElement panelRoot = TargetElement.panel?.visualTree;
        if (panelRoot == null)
            return targetBounds;

        Rect panelBounds = panelRoot.worldBound;
        if (panelBounds.width <= 1f || panelBounds.height <= 1f)
            return targetBounds;

        float scaleX = Screen.width / panelBounds.width;
        float scaleY = Screen.height / panelBounds.height;
        if (!float.IsFinite(scaleX) || !float.IsFinite(scaleY) || scaleX <= 0f || scaleY <= 0f)
            return targetBounds;

        return new Rect(
            (targetBounds.xMin - panelBounds.xMin) * scaleX,
            (targetBounds.yMin - panelBounds.yMin) * scaleY,
            targetBounds.width * scaleX,
            targetBounds.height * scaleY);
    }

    private Camera ResolveSourceCamera()
    {
        if (SourceCamera != null)
            return SourceCamera;

        SourceCamera = Camera.main;
        return SourceCamera;
    }

    private void EnsureBlurMaterial()
    {
        if (blurMaterial != null)
            return;

        Shader shader = Resources.Load<Shader>(BlurShaderResourcePath);
        if (shader == null)
            shader = Shader.Find(BlurShaderName);
        if (shader == null)
            return;

        blurMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private void EnsureCaptureCamera()
    {
        if (captureCamera != null)
            return;

        GameObject cameraObject = new GameObject("UIBackdropBlurCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        cameraObject.transform.SetParent(transform, false);
        captureCamera = cameraObject.AddComponent<Camera>();
        captureCamera.enabled = false;
        cameraObject.AddComponent<UniversalAdditionalCameraData>();
    }

    private bool RenderLiveCameraToSceneTexture()
    {
        Camera sourceCamera = ResolveSourceCamera();
        if (sourceCamera == null)
            return false;

        EnsureCaptureCamera();

        captureCamera.CopyFrom(sourceCamera);
        captureCamera.enabled = false;
        captureCamera.targetTexture = sceneTexture;
        captureCamera.forceIntoRenderTexture = true;
        captureCamera.rect = new Rect(0f, 0f, 1f, 1f);
        if (sourceCamera.clearFlags == CameraClearFlags.Depth || sourceCamera.clearFlags == CameraClearFlags.Nothing)
        {
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = sourceCamera.backgroundColor;
            captureCamera.cullingMask = sourceCamera.cullingMask | (1 << GameplayBackgroundLayer);
        }
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
            captureCamera.stereoTargetEye = StereoTargetEyeMask.None;
        SyncUniversalCameraSettings(sourceCamera, captureCamera);
        captureCamera.Render();
        return true;
    }

    private void EnsureRenderTextures(int width, int height)
    {
        if (width == textureWidth && height == textureHeight && sceneTexture != null && blurTextureA != null && blurTextureB != null)
            return;

        ReleaseRenderTextures();
        textureWidth = width;
        textureHeight = height;
        sceneTexture = CreateBlurTexture("UIBackdropScene", width, height);
        blurTextureA = CreateBlurTexture("UIBackdropBlurA", width, height);
        blurTextureB = CreateBlurTexture("UIBackdropBlurB", width, height);
    }

    private void EnsureTargetTexture(int width, int height)
    {
        if (width == targetTextureWidth && height == targetTextureHeight && targetTexture != null)
            return;

        ReleaseTexture(ref targetTexture);
        targetTextureWidth = width;
        targetTextureHeight = height;
        targetTexture = CreateBlurTexture("UIBackdropTarget", width, height);
    }

    private static RenderTexture CreateBlurTexture(string textureName, int width, int height)
    {
        RenderTexture texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.Create();
        return texture;
    }

    private void ReleaseRenderTextures()
    {
        ReleaseTexture(ref sceneTexture);
        ReleaseTexture(ref blurTextureA);
        ReleaseTexture(ref blurTextureB);
        ReleaseTexture(ref targetTexture);
        textureWidth = -1;
        textureHeight = -1;
        targetTextureWidth = -1;
        targetTextureHeight = -1;
    }

    private static void ReleaseTexture(ref RenderTexture texture)
    {
        if (texture == null)
            return;

        texture.Release();
        Destroy(texture);
        texture = null;
    }

    private static void SyncUniversalCameraSettings(Camera source, Camera destination)
    {
        if (source == null || destination == null)
            return;

        if (!source.TryGetComponent(out UniversalAdditionalCameraData sourceData))
            return;

        UniversalAdditionalCameraData destinationData = destination.GetComponent<UniversalAdditionalCameraData>();
        if (destinationData == null)
            return;

        destinationData.renderType = CameraRenderType.Base;
        destinationData.renderPostProcessing = sourceData.renderPostProcessing;
        destinationData.antialiasing = sourceData.antialiasing;
        destinationData.antialiasingQuality = sourceData.antialiasingQuality;
        destinationData.stopNaN = sourceData.stopNaN;
        destinationData.dithering = sourceData.dithering;
        destinationData.renderShadows = sourceData.renderShadows;
        destinationData.requiresColorOption = sourceData.requiresColorOption;
        destinationData.requiresDepthOption = sourceData.requiresDepthOption;
        destinationData.volumeLayerMask = sourceData.volumeLayerMask;
        destinationData.volumeTrigger = sourceData.volumeTrigger;
        destinationData.allowXRRendering = false;
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        ReleaseRenderTextures();
        if (blurMaterial != null)
        {
            Destroy(blurMaterial);
            blurMaterial = null;
        }

        if (captureCamera != null)
        {
            Destroy(captureCamera.gameObject);
            captureCamera = null;
        }
    }
}
