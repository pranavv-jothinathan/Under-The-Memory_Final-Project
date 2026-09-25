#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using Unity.XR.CoreUtils;

public static class TitleTutorialSetup
{
    const string ScenePath = "Assets/Scenes/TitleScene.unity";
    const string UiFolder = "Assets/Scenes/TitleScene/UI";
    const string AudioFolder = "Assets/Scenes/TitleScene/Audio";
    const string TutorialTexturePath = UiFolder + "/移动教学UI.png";
    const string TutorialMaterialPath = UiFolder + "/Materials/移动教学UI.mat";

    [MenuItem("Title Tutorial/搭建移动教学")]
    public static void Setup()
    {
        AssetDatabase.Refresh();
        ConfigureTutorialTexture();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        XROrigin xrOrigin = FindComponent<XROrigin>(scene, "XR Origin (XR Rig)");
        if (xrOrigin == null || xrOrigin.Camera == null)
            throw new InvalidOperationException("TitleScene 缺少可用的 XR Origin (XR Rig) 或 XR Camera。");

        DisableDuplicateRootCamera(scene, xrOrigin.Camera);

        GameObject anyButtonPanel = FindObject(scene, "按任意键开始");
        GameObject anyTriggerPanel = FindObject(scene, "按任意扳机开始");
        if (anyButtonPanel == null || anyTriggerPanel == null)
            throw new InvalidOperationException("TitleScene 中找不到现有的按键/扳机提示对象。");

        Material tutorialMaterial = CreateOrUpdateTutorialMaterial();
        GameObject tutorialPanel = FindObject(scene, "移动教学UI") ?? GameObject.CreatePrimitive(PrimitiveType.Quad);
        tutorialPanel.name = "移动教学UI";
        Renderer tutorialRenderer = tutorialPanel.GetComponent<Renderer>();
        tutorialRenderer.sharedMaterial = tutorialMaterial;
        tutorialRenderer.shadowCastingMode = ShadowCastingMode.Off;
        tutorialRenderer.receiveShadows = false;

        RemoveCollider(anyButtonPanel);
        RemoveCollider(anyTriggerPanel);
        RemoveCollider(tutorialPanel);

        AlignTutorialPanel(anyButtonPanel, tutorialPanel);

        SetActive(anyButtonPanel, false);
        SetActive(anyTriggerPanel, false);
        SetActive(tutorialPanel, false);

        GameObject starterControls = FindObject(scene, "Interactive Controls");
        if (starterControls != null)
            SetActive(starterControls, false);

        CharacterController characterController = xrOrigin.GetComponent<CharacterController>();
        ContinuousMoveProvider moveProvider = FindPreferredComponent<ContinuousMoveProvider>(xrOrigin.gameObject, "Move");
        SnapTurnProvider snapTurnProvider = FindPreferredComponent<SnapTurnProvider>(xrOrigin.gameObject, "Turn");
        ContinuousTurnProvider continuousTurnProvider =
            FindPreferredComponent<ContinuousTurnProvider>(xrOrigin.gameObject, "Turn");
        if (characterController == null || moveProvider == null ||
            snapTurnProvider == null || continuousTurnProvider == null)
            throw new InvalidOperationException("XR Origin 缺少 CharacterController、Move 或 Turn Provider。");

        GameObject locomotionHost = FindObject(scene, "Locomotion");
        if (locomotionHost == null || !locomotionHost.transform.IsChildOf(xrOrigin.transform))
            throw new InvalidOperationException("XR Origin 下找不到 Locomotion 对象。");
        locomotionHost.SetActive(true);
        locomotionHost.transform.localPosition = Vector3.zero;

        CharacterJump jump = GetOrAdd<CharacterJump>(locomotionHost);
        SwimVerticalInput swimVertical = GetOrAdd<SwimVerticalInput>(locomotionHost);
        FreeAimTeleport teleport = GetOrAdd<FreeAimTeleport>(locomotionHost);
        LocomotionModeSwitcher switcher = GetOrAdd<LocomotionModeSwitcher>(locomotionHost);

        SetObjectReference(jump, "m_CharacterController", characterController);
        SetObjectReference(swimVertical, "m_CharacterController", characterController);
        SetObjectReference(swimVertical, "m_Origin", xrOrigin.Origin.transform);

        SetObjectReference(teleport, "m_RayOrigin", FindTransform(xrOrigin.gameObject, "Right Controller"));
        SetObjectReference(teleport, "m_BodyCenter", xrOrigin.Camera.transform);
        SetObjectReference(teleport, "m_Origin", xrOrigin.Origin.transform);
        SetObjectReference(teleport, "m_CharacterController", characterController);

        SetObjectReference(switcher, "m_MoveProvider", moveProvider);
        SetObjectReference(switcher, "m_SnapTurnProvider", snapTurnProvider);
        SetObjectReference(switcher, "m_ContinuousTurnProvider", continuousTurnProvider);
        SetObjectReference(switcher, "m_Jump", jump);
        SetObjectReference(switcher, "m_SwimVertical", swimVertical);
        SetObjectReference(switcher, "m_Teleport", teleport);

        GameObject system = FindObject(scene, "[SYS] TitleTutorial") ?? new GameObject("[SYS] TitleTutorial");
        AudioSource voiceSource = GetOrAdd<AudioSource>(system);
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        voiceSource.volume = 1f;

        TitleTutorialDirector director = GetOrAdd<TitleTutorialDirector>(system);
        AudioSource backgroundMusic = FindObject(scene, "Audio")?.GetComponent<AudioSource>();

        SerializedObject directorSerialized = new SerializedObject(director);
        SetProperty(directorSerialized, "m_AnyButtonPanel", anyButtonPanel);
        SetProperty(directorSerialized, "m_TutorialPanel", tutorialPanel);
        SetProperty(directorSerialized, "m_AnyTriggerPanel", anyTriggerPanel);
        SetProperty(directorSerialized, "m_ModeSwitcher", switcher);
        SetProperty(directorSerialized, "m_MoveProvider", moveProvider);
        SetProperty(directorSerialized, "m_SnapTurnProvider", snapTurnProvider);
        SetProperty(directorSerialized, "m_Jump", jump);
        SetProperty(directorSerialized, "m_Teleport", teleport);
        SetProperty(directorSerialized, "m_VoiceSource", voiceSource);
        SetProperty(directorSerialized, "m_BackgroundMusic", backgroundMusic);
        SetProperty(directorSerialized, "m_ThreeModesVoice", LoadClip("There are three movement modes_"));
        SetProperty(directorSerialized, "m_SwitchModeVoice", LoadClip("Press the B button"));
        SetProperty(directorSerialized, "m_CommonControlsVoice", LoadClip("In all modes"));
        SetProperty(directorSerialized, "m_WalkingVoice", LoadClip("In Walking Mode"));
        SetProperty(directorSerialized, "m_SwimmingVoice", LoadClip("In Swimming Mode"));
        SetProperty(directorSerialized, "m_TeleportVoice", LoadClip("In Teleport Mode"));
        directorSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(jump);
        EditorUtility.SetDirty(teleport);
        EditorUtility.SetDirty(switcher);
        EditorUtility.SetDirty(voiceSource);
        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        AddTitleSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("TitleScene 移动教学已搭建完成。");
    }

    public static void SetupFromCommandLine()
    {
        try
        {
            Setup();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    static void ConfigureTutorialTexture()
    {
        TextureImporter importer = AssetImporter.GetAtPath(TutorialTexturePath) as TextureImporter;
        if (importer == null)
            throw new FileNotFoundException("找不到移动教学 UI 贴图。", TutorialTexturePath);

        bool changed = importer.npotScale != TextureImporterNPOTScale.None ||
                       importer.mipmapEnabled ||
                       !importer.alphaIsTransparency;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        if (changed)
            importer.SaveAndReimport();
    }

    static Material CreateOrUpdateTutorialMaterial()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/UI/title shader.shadergraph");
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TutorialTexturePath);
        if (shader == null || texture == null)
            throw new InvalidOperationException("无法加载 Title UI Shader 或移动教学贴图。");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(TutorialMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "移动教学UI" };
            AssetDatabase.CreateAsset(material, TutorialMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetTexture("_Texture2D", texture);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void AlignTutorialPanel(GameObject prompt, GameObject tutorialPanel)
    {
        if (prompt == null || tutorialPanel == null)
            return;

        tutorialPanel.transform.SetParent(prompt.transform.parent, true);
        tutorialPanel.transform.SetPositionAndRotation(prompt.transform.position, prompt.transform.rotation);
    }

    static void DisableDuplicateRootCamera(Scene scene, Camera xrCamera)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera camera = root.GetComponent<Camera>();
            if (camera != null && camera != xrCamera)
                root.SetActive(false);
        }
    }

    static void AddTitleSceneToBuildSettings()
    {
        EditorBuildSettingsScene title = new EditorBuildSettingsScene(ScenePath, true);
        EditorBuildSettingsScene[] remaining = EditorBuildSettings.scenes
            .Where(scene => !string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        EditorBuildSettings.scenes = new[] { title }.Concat(remaining).ToArray();
    }

    static AudioClip LoadClip(string fileNamePrefix)
    {
        string guid = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder })
            .FirstOrDefault(candidate =>
            {
                string path = AssetDatabase.GUIDToAssetPath(candidate);
                return Path.GetFileName(path).StartsWith(fileNamePrefix, StringComparison.OrdinalIgnoreCase);
            });

        if (string.IsNullOrEmpty(guid))
            throw new FileNotFoundException($"找不到教学语音：{fileNamePrefix}");
        return AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
    }

    static T FindComponent<T>(Scene scene, string objectName) where T : Component
    {
        return FindObject(scene, objectName)?.GetComponent<T>();
    }

    static GameObject FindObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == objectName);
            if (match != null)
                return match.gameObject;
        }
        return null;
    }

    static Transform FindTransform(GameObject root, string objectName)
    {
        return root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(transform => transform.name == objectName);
    }

    static T FindPreferredComponent<T>(GameObject root, string preferredObjectName) where T : Component
    {
        T[] components = root.GetComponentsInChildren<T>(true);
        return components.FirstOrDefault(component => component.gameObject.name == preferredObjectName) ??
               components.FirstOrDefault(component => component.gameObject.activeInHierarchy) ??
               components.FirstOrDefault();
    }

    static T GetOrAdd<T>(GameObject target) where T : Component
    {
        return target.GetComponent<T>() ?? target.AddComponent<T>();
    }

    static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SetProperty(serialized, propertyName, value);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetProperty(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new MissingFieldException(serialized.targetObject.GetType().Name, propertyName);
        property.objectReferenceValue = value;
    }

    static void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);
    }

    static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}
#endif
