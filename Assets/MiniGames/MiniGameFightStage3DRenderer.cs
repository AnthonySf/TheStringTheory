using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public sealed class MiniGameFightStage3DRenderer
{
    private const string RootName = "MiniGameFightStage3D";
    private const string FightClubAssetDirectory = "MiniGames/Assets_FightClub";
    private const string FightClubIdleSheetFileName = "Elize_Idle_Spritesheet.png";
    private const string FightClubActionJumpFileName = "NEW_Elize_Jumping.png";
    private const string FightClubActionHeadbangFileName = "NEW_Elize_Headbanging.png";
    private const string FightClubIdlePoseFileName = "NEW_Elize_Idle.png";
    private const int IdleFrameCount = 6;
    private const float IdleFps = 7.5f;
    private const float ActionHoldSeconds = 0.58f;
    private const float MissHoldSeconds = 0.82f;
    private const float StageDistance = 11.45f;
    private const float StageYOffset = -1.18f;
    private const float FloorY = -3.05f;
    private const float CharacterHeight = 6.55f;
    private const float CharacterWidth = 4.55f;
    private const float IdleFramePixelWidth = 120f;
    private const float IdleFramePixelHeight = 160f;
    private const float ReferenceVisiblePixelHeight = 124f;
    private const float CharacterFootSink = -0.02f;
    private const float LeftCharacterBaseX = -5.05f;
    private const float RightCharacterBaseX = 5.05f;
    private const uint StageRenderingLayerMask = 1u;
    public const int StageUnityLayer = 29;
    public const int StageUnityLayerMask = 1 << StageUnityLayer;

    private static readonly Color FloorColor = new Color(0.008f, 0.011f, 0.026f, 1f);
    private static readonly Color FloorSpecularColor = new Color(0.20f, 0.24f, 0.42f, 1f);
    private static readonly Color HitTint = new Color(0.94f, 0.95f, 1f, 1f);
    private static readonly Color MissTint = new Color(0.88f, 0.42f, 0.56f, 1f);
    private static readonly Color MissPulseTint = new Color(1.0f, 0.58f, 0.72f, 1f);
    private static readonly Color LeftSpotColor = new Color(0.54f, 0.74f, 1.00f, 1f);
    private static readonly Color RightSpotColor = new Color(0.92f, 0.56f, 1.00f, 1f);
    private static readonly Color CenterSpotColor = new Color(0.30f, 0.40f, 0.86f, 1f);
    private const float CharacterAlphaCutoff = 0.08f;

    private readonly GuitarBridgeServer owner;
    private readonly int[] lastChordStatuses = { -1, -1, -1, -1 };
    private GameObject root;
    private Transform leftCharacter;
    private Transform rightCharacter;
    private Transform leftShadowCaster;
    private Transform rightShadowCaster;
    private Renderer floorRenderer;
    private Renderer leftCharacterRenderer;
    private Renderer rightCharacterRenderer;
    private Renderer leftShadowCasterRenderer;
    private Renderer rightShadowCasterRenderer;
    private Material leftCharacterMaterial;
    private Material rightCharacterMaterial;
    private Material leftShadowCasterMaterial;
    private Material rightShadowCasterMaterial;
    private Material floorMaterial;
    private Texture2D idleSheetTexture;
    private Texture2D actionJumpTexture;
    private Texture2D actionHeadbangTexture;
    private Texture2D idlePoseTexture;
    private bool staleRootScanDone;
    private int lastRound = -1;
    private int lastActiveChordIndex = -2;
    private int lastOpponentActiveChordIndex = -2;
    private int lastActionChordIndex;
    private int lastOpponentActionChordIndex;
    private float actionStartedAt = -999f;
    private float actionUntil = -999f;
    private float opponentActionStartedAt = -999f;
    private float opponentActionUntil = -999f;
    private float missStartedAt = -999f;
    private float missUntil = -999f;

    public MiniGameFightStage3DRenderer(GuitarBridgeServer owner)
    {
        this.owner = owner;
    }

    public void Update(FightClubMiniGameSnapshot snapshot, bool visible)
    {
        bool shouldShow = visible && snapshot != null && snapshot.active && !snapshot.ended;
        if (!shouldShow)
        {
            Hide();
            return;
        }

        EnsureRoot();
        EnsureTextures();
        SetStageLightsEnabled(true);
        PositionRoot();
        SetVisible(true);
        UpdateTriggers(snapshot);
        UpdateCharacterPose(snapshot);
    }

    public void Hide()
    {
        if (root == null)
        {
            DestroyExistingRootOnce();
            return;
        }

        SetVisible(false);
        ResetState();
    }

    private void EnsureRoot()
    {
        if (root != null)
            return;

        DestroyExistingRoot();
        staleRootScanDone = true;

        root = new GameObject(RootName);
        root.hideFlags = HideFlags.DontSave;
        root.layer = StageUnityLayer;
        if (owner != null)
            root.transform.SetParent(owner.transform, false);

        CreateStageLighting(root.transform);
        CreateFloor(root.transform);
        CreateCharacters(root.transform);
        root.SetActive(false);
    }

    private void SetStageLightsEnabled(bool enabled)
    {
        if (root == null)
            return;

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                lights[i].enabled = enabled;
        }
    }

    private void CreateFloor(Transform parent)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "FightClubFlatFloor";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, FloorY, 0.15f);
        floor.transform.localRotation = Quaternion.identity;
        floor.transform.localScale = new Vector3(1.82f, 1f, 1.92f);
        floorRenderer = floor.GetComponent<Renderer>();
        floorMaterial = CreateFloorMaterial();
        floorRenderer.sharedMaterial = floorMaterial;
        ConfigureRenderer(floorRenderer, ShadowCastingMode.Off, receiveShadows: true);
        Object.Destroy(floor.GetComponent<Collider>());
    }

    private void CreateCharacters(Transform parent)
    {
        leftCharacter = CreateCharacterQuad(parent, "FightClubLeftCharacter", LeftCharacterBaseX, out leftCharacterRenderer, out leftCharacterMaterial);
        rightCharacter = CreateCharacterQuad(parent, "FightClubRightCharacter", RightCharacterBaseX, out rightCharacterRenderer, out rightCharacterMaterial);
        leftShadowCaster = CreateCharacterShadowCaster(parent, "FightClubLeftShadowCaster", LeftCharacterBaseX, out leftShadowCasterRenderer, out leftShadowCasterMaterial);
        rightShadowCaster = CreateCharacterShadowCaster(parent, "FightClubRightShadowCaster", RightCharacterBaseX, out rightShadowCasterRenderer, out rightShadowCasterMaterial);
        leftCharacter.localScale = new Vector3(CharacterWidth, CharacterHeight, 1f);
        rightCharacter.localScale = new Vector3(-CharacterWidth, CharacterHeight, 1f);
        leftShadowCaster.localScale = leftCharacter.localScale;
        rightShadowCaster.localScale = rightCharacter.localScale;
    }

    private void CreateStageLighting(Transform parent)
    {
        CreateStageSpotLight(parent, "FightClubLeftSpotlight", new Vector3(-6.85f, 5.45f, -3.65f), new Vector3(LeftCharacterBaseX - 0.10f, FloorY + 0.04f, 0.92f), LeftSpotColor, 2160.0f, 15.4f, 35f, castShadows: true);
        CreateStageSpotLight(parent, "FightClubRightSpotlight", new Vector3(6.85f, 5.45f, -3.65f), new Vector3(RightCharacterBaseX + 0.10f, FloorY + 0.04f, 0.92f), RightSpotColor, 2080.0f, 15.4f, 35f, castShadows: true);
        CreateStageSpotLight(parent, "FightClubCenterFloorWash", new Vector3(0f, 4.65f, -5.35f), new Vector3(0f, FloorY + 0.02f, 1.75f), CenterSpotColor, 420.0f, 18.0f, 68f, castShadows: false);
    }

    private static void CreateStageSpotLight(Transform parent, string name, Vector3 localPosition, Vector3 target, Color color, float intensity, float range, float spotAngle, bool castShadows)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.hideFlags = HideFlags.DontSave;
        lightObject.layer = StageUnityLayer;
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = localPosition;
        Vector3 direction = target - localPosition;
        if (direction.sqrMagnitude > 0.0001f)
            lightObject.transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = spotAngle;
        light.innerSpotAngle = Mathf.Max(8f, spotAngle * 0.56f);
        light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        light.shadowStrength = castShadows ? 0.84f : 0f;
        light.shadowBias = 0.003f;
        light.shadowNormalBias = 0.006f;
        light.renderMode = LightRenderMode.ForcePixel;
        light.cullingMask = StageUnityLayerMask;
        SetRenderingLayerMask(light, StageRenderingLayerMask);
    }

    private Transform CreateCharacterQuad(Transform parent, string name, float localX, out Renderer renderer, out Material material)
    {
        GameObject character = GameObject.CreatePrimitive(PrimitiveType.Quad);
        character.name = name;
        character.transform.SetParent(parent, false);
        character.transform.localPosition = new Vector3(localX, FloorY + (CharacterHeight * 0.50f) - 0.14f, -0.03f);
        character.transform.localRotation = Quaternion.identity;
        character.transform.localScale = new Vector3(CharacterWidth, CharacterHeight, 1f);
        renderer = character.GetComponent<Renderer>();
        material = CreateCharacterMaterial();
        renderer.sharedMaterial = material;
        ConfigureRenderer(renderer, ShadowCastingMode.Off, receiveShadows: false);
        Object.Destroy(character.GetComponent<Collider>());
        return character.transform;
    }

    private Transform CreateCharacterShadowCaster(Transform parent, string name, float localX, out Renderer renderer, out Material material)
    {
        GameObject character = GameObject.CreatePrimitive(PrimitiveType.Quad);
        character.name = name;
        character.transform.SetParent(parent, false);
        character.transform.localPosition = new Vector3(localX, FloorY + (CharacterHeight * 0.50f) - 0.14f, -0.032f);
        character.transform.localRotation = Quaternion.identity;
        character.transform.localScale = new Vector3(CharacterWidth, CharacterHeight, 1f);
        renderer = character.GetComponent<Renderer>();
        material = CreateCharacterShadowCasterMaterial();
        renderer.sharedMaterial = material;
        ConfigureRenderer(renderer, ShadowCastingMode.ShadowsOnly, receiveShadows: false);
        Object.Destroy(character.GetComponent<Collider>());
        return character.transform;
    }

    private void PositionRoot()
    {
        Camera camera = Camera.main;
        if (camera == null || root == null)
            return;

        Transform cameraTransform = camera.transform;
        root.transform.position = cameraTransform.position + (cameraTransform.forward * StageDistance) + (cameraTransform.up * StageYOffset);
        root.transform.rotation = cameraTransform.rotation;
        root.transform.localScale = Vector3.one;
    }

    private void UpdateTriggers(FightClubMiniGameSnapshot snapshot)
    {
        if (snapshot.round != lastRound)
        {
            lastRound = snapshot.round;
            for (int i = 0; i < lastChordStatuses.Length; i++)
                lastChordStatuses[i] = 0;
            lastActiveChordIndex = -2;
            lastOpponentActiveChordIndex = -2;
        }

        float now = Time.unscaledTime;
        lastActiveChordIndex = snapshot.activeChordIndex;

        if (snapshot.opponentActiveChordIndex >= 0 && snapshot.opponentActiveChordIndex != lastOpponentActiveChordIndex)
            TriggerOpponentAction(snapshot.opponentActiveChordIndex, now);
        lastOpponentActiveChordIndex = snapshot.opponentActiveChordIndex;

        if (snapshot.opponentPreviewActive)
            return;

        List<FightClubChordSnapshot> chords = snapshot.chords ?? new List<FightClubChordSnapshot>();
        for (int i = 0; i < lastChordStatuses.Length; i++)
        {
            int status = i < chords.Count ? Mathf.Clamp(chords[i]?.status ?? 0, 0, 2) : 0;
            if (status == lastChordStatuses[i])
                continue;

            if (status == 1)
                TriggerAction(i, now);
            else if (status == 2)
                TriggerMiss(i, now);
            lastChordStatuses[i] = status;
        }
    }

    private void UpdateCharacterPose(FightClubMiniGameSnapshot snapshot)
    {
        float now = Time.unscaledTime;
        bool missActive = now < missUntil;
        bool actionActive = !missActive && now < actionUntil;
        bool opponentActionActive = now < opponentActionUntil;
        float idleBreath = (Mathf.Sin(now * 2.0f) + 1f) * 0.5f;
        float idleSway = Mathf.Sin(now * 1.35f);
        Texture2D leftTexture = idleSheetTexture;
        Texture2D rightTexture = idleSheetTexture;
        Vector2 leftScale = GetIdleUvScale();
        Vector2 rightScale = leftScale;
        Vector2 leftOffset = GetIdleUvOffset(now, 0f);
        Vector2 rightOffset = GetIdleUvOffset(now, 0.18f);
        Color leftTint = HitTint;
        Color rightTint = HitTint;
        float poseHeight = GetPoseHeight(leftTexture);
        float poseWidth = GetPoseWidth(leftTexture, leftScale, poseHeight);
        float scaledPoseHeight = poseHeight * (1f + (idleBreath * 0.018f));
        float leftX = LeftCharacterBaseX;
        float rightX = RightCharacterBaseX;
        float leftY = GetPoseCenterY(leftTexture, scaledPoseHeight);
        float rightY = leftY;
        float leftRot = idleSway * 1.1f;
        float rightRot = -leftRot;
        float leftScaleX = poseWidth * (1f - (idleBreath * 0.012f));
        float rightScaleX = -leftScaleX;
        float leftScaleY = scaledPoseHeight;
        float rightScaleY = scaledPoseHeight;

        if (missActive)
        {
            float t = Mathf.Clamp01((now - missStartedAt) / MissHoldSeconds);
            float hitPulse = Mathf.Sin(Mathf.Clamp01(t / 0.38f) * Mathf.PI);
            float settle = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / 0.58f));
            float recoil = -0.24f * hitPulse;
            float shake = Mathf.Sin(t * 28f) * 0.035f * settle;
            leftTexture = idlePoseTexture != null ? idlePoseTexture : idleSheetTexture;
            bool useIdlePose = idlePoseTexture != null;
            leftScale = useIdlePose ? Vector2.one : GetIdleUvScale();
            leftOffset = useIdlePose ? Vector2.zero : GetIdleUvOffset(now, 0f);
            leftTint = Color.Lerp(MissTint, MissPulseTint, hitPulse * 0.28f);
            rightTint = HitTint;
            poseHeight = GetPoseHeight(leftTexture);
            poseWidth = GetPoseWidth(leftTexture, leftScale, poseHeight);
            leftScaleX = poseWidth * (1f + (hitPulse * 0.075f));
            leftScaleY = poseHeight * (1f - (hitPulse * 0.028f));
            leftX = LeftCharacterBaseX + recoil + shake;
            leftY = GetPoseCenterY(leftTexture, leftScaleY);
            leftRot = 0f;
        }
        else if (actionActive)
        {
            float t = Mathf.Clamp01((now - actionStartedAt) / ActionHoldSeconds);
            float power = Mathf.Sin(t * Mathf.PI);
            bool useJump = (lastActionChordIndex & 1) == 0;
            Texture2D actionTexture = useJump ? actionJumpTexture : actionHeadbangTexture;
            if (actionTexture != null)
            {
                leftTexture = actionTexture;
                leftScale = Vector2.one;
                leftOffset = Vector2.zero;
            }

            poseHeight = GetPoseHeight(leftTexture);
            poseWidth = GetPoseWidth(leftTexture, leftScale, poseHeight);
            leftScaleY = poseHeight * (1f + (power * 0.055f));
            leftX = LeftCharacterBaseX + (0.64f * power);
            leftY = GetPoseCenterY(leftTexture, leftScaleY) + (0.34f * power);
            leftRot = useJump ? -5.0f * power : 4.0f * power;
            leftScaleX = poseWidth * (1f + (power * 0.10f));
        }

        if (opponentActionActive)
        {
            float t = Mathf.Clamp01((now - opponentActionStartedAt) / ActionHoldSeconds);
            float power = Mathf.Sin(t * Mathf.PI);
            bool useJump = (lastOpponentActionChordIndex & 1) == 0;
            Texture2D actionTexture = useJump ? actionJumpTexture : actionHeadbangTexture;
            if (actionTexture != null)
            {
                rightTexture = actionTexture;
                rightScale = Vector2.one;
                rightOffset = Vector2.zero;
            }

            float rightPoseHeight = GetPoseHeight(rightTexture);
            float rightPoseWidth = GetPoseWidth(rightTexture, rightScale, rightPoseHeight);
            rightScaleY = rightPoseHeight * (1f + (power * 0.055f));
            rightX = RightCharacterBaseX - (0.64f * power);
            rightY = GetPoseCenterY(rightTexture, rightScaleY) + (0.34f * power);
            rightRot = useJump ? 5.0f * power : -4.0f * power;
            rightScaleX = -rightPoseWidth * (1f + (power * 0.10f));
        }

        ApplyCharacter(leftCharacter, leftCharacterMaterial, leftTexture, leftScale, leftOffset, leftTint, leftX, leftY, leftScaleX, leftScaleY, leftRot);
        ApplyCharacter(rightCharacter, rightCharacterMaterial, rightTexture, rightScale, rightOffset, rightTint, rightX, rightY, rightScaleX, rightScaleY, rightRot);
        ApplyCharacter(leftShadowCaster, leftShadowCasterMaterial, leftTexture, leftScale, leftOffset, Color.white, leftX, leftY, leftScaleX, leftScaleY, leftRot);
        ApplyCharacter(rightShadowCaster, rightShadowCasterMaterial, rightTexture, rightScale, rightOffset, Color.white, rightX, rightY, rightScaleX, rightScaleY, rightRot);
    }

    private void ApplyCharacter(
        Transform character,
        Material material,
        Texture2D texture,
        Vector2 textureScale,
        Vector2 textureOffset,
        Color tint,
        float x,
        float y,
        float scaleX,
        float scaleY,
        float rotationZ)
    {
        if (character == null || material == null)
            return;

        if (texture != null)
        {
            SetMaterialTexture(material, texture);
            SetMaterialTextureScale(material, textureScale);
            SetMaterialTextureOffset(material, textureOffset);
        }

        SetMaterialColor(material, tint);
        character.localPosition = new Vector3(x, y, -0.03f);
        character.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        character.localScale = new Vector3(scaleX, scaleY, 1f);
    }

    private void TriggerAction(int chordIndex, float now)
    {
        lastActionChordIndex = Mathf.Max(0, chordIndex);
        actionStartedAt = now;
        actionUntil = now + ActionHoldSeconds;
    }

    private void TriggerOpponentAction(int chordIndex, float now)
    {
        lastOpponentActionChordIndex = Mathf.Max(0, chordIndex);
        opponentActionStartedAt = now;
        opponentActionUntil = now + ActionHoldSeconds;
    }

    private void TriggerMiss(int chordIndex, float now)
    {
        lastActionChordIndex = Mathf.Max(0, chordIndex);
        missStartedAt = now;
        missUntil = now + MissHoldSeconds;
    }

    private void ResetState()
    {
        lastRound = -1;
        lastActiveChordIndex = -2;
        lastOpponentActiveChordIndex = -2;
        for (int i = 0; i < lastChordStatuses.Length; i++)
            lastChordStatuses[i] = -1;
        actionUntil = -999f;
        opponentActionUntil = -999f;
        missUntil = -999f;
    }

    private void EnsureTextures()
    {
        if (idleSheetTexture != null)
            return;

        idleSheetTexture = LoadFightClubTexture(FightClubIdleSheetFileName);
        actionJumpTexture = LoadFightClubTexture(FightClubActionJumpFileName);
        actionHeadbangTexture = LoadFightClubTexture(FightClubActionHeadbangFileName);
        idlePoseTexture = LoadFightClubTexture(FightClubIdlePoseFileName);
    }

    private static Vector2 GetIdleUvScale()
    {
        return new Vector2(1f / IdleFrameCount, 1f);
    }

    private static Vector2 GetIdleUvOffset(float now, float phaseOffset)
    {
        int frame = Mathf.FloorToInt((now + phaseOffset) * IdleFps) % IdleFrameCount;
        if (frame < 0)
            frame = 0;
        return new Vector2(frame / (float)IdleFrameCount, 0f);
    }

    private static float GetPoseHeight(Texture2D texture)
    {
        float referenceVisibleHeight = CharacterHeight * (ReferenceVisiblePixelHeight / IdleFramePixelHeight);
        float frameHeight = GetFramePixelHeight(texture);
        float visibleHeight = Mathf.Max(1f, GetVisiblePixelHeight(texture));
        return referenceVisibleHeight * (frameHeight / visibleHeight);
    }

    private static float GetPoseWidth(Texture2D texture, Vector2 textureScale, float poseHeight)
    {
        if (texture == null || texture.height <= 0)
            return CharacterWidth;

        float frameWidth = texture.width * Mathf.Max(0.0001f, Mathf.Abs(textureScale.x));
        float frameHeight = texture.height * Mathf.Max(0.0001f, Mathf.Abs(textureScale.y));
        return poseHeight * (frameWidth / frameHeight);
    }

    private static float GetPoseCenterY(Texture2D texture, float poseHeight)
    {
        float frameHeight = Mathf.Max(1f, GetFramePixelHeight(texture));
        float bottomPadding = GetBottomPaddingPixels(texture);
        return FloorY + (poseHeight * 0.5f) - ((bottomPadding / frameHeight) * poseHeight) + CharacterFootSink;
    }

    private static float GetFramePixelHeight(Texture2D texture)
    {
        if (texture == null)
            return IdleFramePixelHeight;

        string name = texture.name ?? string.Empty;
        if (name.IndexOf("Headbanging", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("NEW_Elize_Idle", StringComparison.OrdinalIgnoreCase) >= 0)
            return 128f;
        if (name.IndexOf("Jumping", StringComparison.OrdinalIgnoreCase) >= 0)
            return 144f;
        return IdleFramePixelHeight;
    }

    private static float GetVisiblePixelHeight(Texture2D texture)
    {
        if (texture == null)
            return ReferenceVisiblePixelHeight;

        string name = texture.name ?? string.Empty;
        if (name.IndexOf("Headbanging", StringComparison.OrdinalIgnoreCase) >= 0)
            return 126f;
        return ReferenceVisiblePixelHeight;
    }

    private static float GetBottomPaddingPixels(Texture2D texture)
    {
        if (texture == null)
            return 16f;

        string name = texture.name ?? string.Empty;
        if (name.IndexOf("Headbanging", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("NEW_Elize_Idle", StringComparison.OrdinalIgnoreCase) >= 0)
            return 0f;
        return 16f;
    }

    private static Texture2D LoadFightClubTexture(string fileName)
    {
        string resourcePath = $"{FightClubAssetDirectory}/{System.IO.Path.GetFileNameWithoutExtension(fileName)}";
        Texture2D resourceTexture = Resources.Load<Texture2D>(resourcePath);
        if (resourceTexture != null)
        {
            HighwayCharacterVisualUtility.ApplyRuntimeTextureSettings(resourceTexture);
            return resourceTexture;
        }

        string path = ResolveFightClubTextureFilePath(fileName);
        if (string.IsNullOrEmpty(path))
            return null;

        try
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(fileName),
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            if (!texture.LoadImage(bytes, false))
                return null;
            HighwayCharacterVisualUtility.ApplyRuntimeTextureSettings(texture);
            return texture;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MiniGameFightStage3DRenderer] Failed to load Fight Club texture '{path}': {ex.Message}");
            return null;
        }
    }

    private static string ResolveFightClubTextureFilePath(string fileName)
    {
        string[] paths =
        {
            System.IO.Path.Combine(Application.dataPath, "MiniGames", "Resources", FightClubAssetDirectory, fileName),
            System.IO.Path.Combine(Application.dataPath, FightClubAssetDirectory, fileName)
        };

        for (int i = 0; i < paths.Length; i++)
        {
            if (System.IO.File.Exists(paths[i]))
                return paths[i];
        }

        return null;
    }

    private static Material CreateFloorMaterial()
    {
        Material material = CreateLitMaterial();
        SetMaterialColor(material, FloorColor);
        SetMaterialSmoothness(material, 0.40f);
        SetMaterialSpecular(material, FloorSpecularColor);
        ConfigureOpaqueMaterial(material, (int)RenderQueue.Geometry + 10);
        return material;
    }

    private static Material CreateCharacterMaterial()
    {
        Material material = CreateSpriteTransparentMaterial((int)RenderQueue.Transparent + 64);
        SetMaterialColor(material, HitTint);
        return material;
    }

    private static Material CreateCharacterShadowCasterMaterial()
    {
        Material material = CreateCharacterAlphaShadowCasterMaterial();
        if (material == null)
            material = CreateCharacterCutoutLitMaterial();
        SetMaterialColor(material, Color.white);
        SetMaterialSmoothness(material, 0.12f);
        DisableExtraLitResponse(material);
        return material;
    }

    private static Material CreateMaterial(bool transparent)
    {
        Shader shader = transparent ? Shader.Find("Unlit/Transparent") : Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = transparent ? Shader.Find("Sprites/Default") : Shader.Find("Unlit/Color");
        if (shader == null)
            shader = transparent ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };

        if (transparent)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_AlphaClip"))
                material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHATEST_ON");
        }

        return material;
    }

    private static Material CreateSpriteTransparentMaterial(int renderQueue)
    {
        Shader shader = Resources.Load<Shader>("Shaders/TabsTexturedTransparent");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };

        material.SetOverrideTag("RenderType", "Transparent");
        ConfigureMaterial(material, renderQueue);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        return material;
    }

    private static Material CreateCharacterAlphaShadowCasterMaterial()
    {
        Shader shader = Resources.Load<Shader>("Shaders/FightClubSpriteShadowCaster");
        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };

        material.renderQueue = (int)RenderQueue.AlphaTest + 20;
        material.SetOverrideTag("RenderType", "TransparentCutout");
        if (material.HasProperty("_Cutoff"))
            material.SetFloat("_Cutoff", CharacterAlphaCutoff);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_ZTest"))
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        return material;
    }

    private static Material CreateCharacterCutoutLitMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return CreateSpriteTransparentMaterial((int)RenderQueue.AlphaTest + 20);

        Material material = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };

        material.renderQueue = (int)RenderQueue.AlphaTest + 20;
        material.SetOverrideTag("RenderType", "TransparentCutout");
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 1f);
        if (material.HasProperty("_Cutoff"))
            material.SetFloat("_Cutoff", CharacterAlphaCutoff);
        if (material.HasProperty("_AlphaCutoff"))
            material.SetFloat("_AlphaCutoff", CharacterAlphaCutoff);
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)BlendMode.One);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_CullMode"))
            material.SetFloat("_CullMode", (float)CullMode.Off);
        if (material.HasProperty("_ZTest"))
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        if (material.HasProperty("_ZTestMode"))
            material.SetFloat("_ZTestMode", (float)CompareFunction.LessEqual);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);

        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_ALPHATEST_ON");
        return material;
    }

    private static Material CreateLitMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return CreateMaterial(transparent: false);

        Material material = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };

        return material;
    }

    private static void ConfigureMaterial(Material material, int renderQueue)
    {
        if (material == null)
            return;

        material.renderQueue = renderQueue;
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_CullMode"))
            material.SetFloat("_CullMode", (float)CullMode.Off);
        if (material.HasProperty("_ZTest"))
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        if (material.HasProperty("_ZTestMode"))
            material.SetFloat("_ZTestMode", (float)CompareFunction.LessEqual);
    }

    private static void ConfigureOpaqueMaterial(Material material, int renderQueue)
    {
        if (material == null)
            return;

        material.renderQueue = renderQueue;
        material.SetOverrideTag("RenderType", "Opaque");
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)BlendMode.One);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_CullMode"))
            material.SetFloat("_CullMode", (float)CullMode.Off);
        if (material.HasProperty("_ZTest"))
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        if (material.HasProperty("_ZTestMode"))
            material.SetFloat("_ZTestMode", (float)CompareFunction.LessEqual);
    }

    private static void ConfigureRenderer(Renderer renderer, ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off, bool receiveShadows = false)
    {
        if (renderer == null)
            return;

        renderer.gameObject.layer = StageUnityLayer;
        renderer.shadowCastingMode = shadowCastingMode;
        renderer.receiveShadows = receiveShadows;
        renderer.renderingLayerMask = StageRenderingLayerMask;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private static void SetRenderingLayerMask(Light light, uint mask)
    {
        if (light == null)
            return;

        UniversalAdditionalLightData lightData = light.GetUniversalAdditionalLightData();
        lightData.customShadowLayers = false;
        lightData.renderingLayers = mask;
        light.renderingLayerMask = (int)mask;
    }

    private void SetVisible(bool visible)
    {
        if (root == null)
            return;

        if (root.activeSelf != visible)
            root.SetActive(visible);
    }

    private void DestroyExistingRootOnce()
    {
        if (staleRootScanDone)
            return;

        DestroyExistingRoot();
        staleRootScanDone = true;
    }

    private static void DestroyExistingRoot()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            Object.Destroy(existing);
    }

    private static void SetMaterialTexture(Material material, Texture texture)
    {
        if (material == null || texture == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        material.mainTexture = texture;
    }

    private static void SetMaterialTextureScale(Material material, Vector2 scale)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTextureScale("_BaseMap", scale);
        if (material.HasProperty("_MainTex"))
            material.SetTextureScale("_MainTex", scale);
        material.mainTextureScale = scale;
    }

    private static void SetMaterialTextureOffset(Material material, Vector2 offset)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTextureOffset("_BaseMap", offset);
        if (material.HasProperty("_MainTex"))
            material.SetTextureOffset("_MainTex", offset);
        material.mainTextureOffset = offset;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", color);
        material.color = color;
    }

    private static void SetMaterialSmoothness(Material material, float smoothness)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
    }

    private static void DisableExtraLitResponse(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_SpecularHighlights"))
            material.SetFloat("_SpecularHighlights", 0f);
        if (material.HasProperty("_EnvironmentReflections"))
            material.SetFloat("_EnvironmentReflections", 0f);
        if (material.HasProperty("_SpecColor"))
            material.SetColor("_SpecColor", Color.black);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
    }

    private static void SetMaterialSpecular(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_SpecularHighlights"))
            material.SetFloat("_SpecularHighlights", 1f);
        if (material.HasProperty("_EnvironmentReflections"))
            material.SetFloat("_EnvironmentReflections", 0f);
        if (material.HasProperty("_SpecColor"))
            material.SetColor("_SpecColor", color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
    }

}
