using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 一键搭建 Scene3 剧情提示 UI。可重复运行：已经存在的东西会复用，不会建重复的。
///
/// MainScene.unity 有 13 MB / 48 万行，agent 的文本编辑工具会把它截断，所以场景改动
/// 只能走这里。跑完在 Unity 里手动保存场景。
/// </summary>
public static class StoryTipsSetup
{
    private const string RootName = "[SYS] StoryTips";
    private const string PanelName = "TipPanel";

    private const string ImageFolder = "Assets/Scene3/UI";
    private const string VoiceFolder = "Assets/Scene3/音效/UI音效";
    private const string MaterialFolder = "Assets/Scene3/UI/Materials";
    private const string MaterialPath = MaterialFolder + "/M_StoryTip.mat";
    private const string ShaderPath = "Assets/UI/UIMaterials/UISHADER.shadergraph";

    private const string RecoveryRootName = "Scanner Recovery Interaction (2)";
    private const string BoxName = "ScannerBox_Box";
    private const string LensName = "SurfaceLens";

    private const string UiLayerName = "UI";

    private struct TipSpec
    {
        public StoryTipId id;
        public string image;
        public string voice;
        public float silentDuration;
        public float width;
        public bool showOnce;
        public StoryTipId followUp;
        public float followUpDelay;
    }

    /// <summary>
    /// 七条提示的内容表。图号是策划稿里的编号。
    /// 宽屏条（约 3:1）用 0.75 米，方一点的（约 1.9:1）用 0.55 米。
    /// </summary>
    private static readonly TipSpec[] Specs =
    {
        new TipSpec
        {
            // 图2 靠近箱子
            id = StoryTipId.ArchiveLensDetected,
            image = "archive lens Detected",
            voice = null,
            silentDuration = 4f,
            width = 0.55f,
            showOnce = true,
        },
        new TipSpec
        {
            // 图4 拿起箱子里的透镜
            id = StoryTipId.LensReadyPressY,
            image = "archive lens ready_grab",
            voice = "You can press D",
            width = 0.55f,
            showOnce = true,
        },
        new TipSpec
        {
            // 图5 第一次按 Y 呼出滤镜仓
            id = StoryTipId.GrabLeftLens,
            image = "grab",
            voice = "Grab lens D",
            width = 0.75f,
            showOnce = true,
        },
        new TipSpec
        {
            // 图6 第一次上到水面
            id = StoryTipId.NoArchiveData,
            image = "missing data",
            voice = "Missing data.",
            width = 0.75f,
            showOnce = true,
        },
        new TipSpec
        {
            // 图7 第一次回到水下
            id = StoryTipId.FollowThePillars,
            image = "follow the pillars",
            voice = "Scan the glowing pillars",
            width = 0.75f,
            showOnce = true,
        },
        new TipSpec
        {
            // 图8 每个阶段扫描完成，第一次之后衔接图9
            id = StoryTipId.ScanComplete,
            image = "scan complete",
            voice = "Scan complete",
            width = 0.75f,
            showOnce = false,
            followUp = StoryTipId.ArchiveDataAvailable,
            followUpDelay = 2f,
        },
        new TipSpec
        {
            // 图9 只在第一次扫描完成后出现一次
            id = StoryTipId.ArchiveDataAvailable,
            image = "archive data available",
            voice = null,
            silentDuration = 4f,
            width = 0.75f,
            showOnce = true,
        },
    };

    [MenuItem("Prefab Library/搭建剧情提示 UI")]
    public static void Build()
    {
        var report = new StringBuilder();
        var problems = new List<string>();

        report.AppendLine("StoryTipsSetup:");

        Undo.SetCurrentGroupName("Setup Story Tips UI");
        int undoGroup = Undo.GetCurrentGroup();

        Material material = EnsureMaterial(problems);
        GameObject root = EnsureRoot(report);
        AudioSource voiceSource = EnsureVoiceSource(root, report);
        MeshRenderer panelRenderer = EnsurePanel(root, material, problems, report);

        StoryTipsPresenter presenter = EnsureComponent<StoryTipsPresenter>(root, report);
        StoryTipsStoryBinder binder = EnsureComponent<StoryTipsStoryBinder>(root, report);

        ConfigurePresenter(presenter, panelRenderer, voiceSource, problems, report);
        ConfigureBinder(binder, presenter);

        WireSceneTriggers(problems, report);

        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        if (problems.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("── 需要注意 ──");
            for (int i = 0; i < problems.Count; i++)
                report.AppendLine("  ! " + problems[i]);
        }

        report.AppendLine();
        report.AppendLine("场景已标脏，记得手动保存（Ctrl+S）。整个操作是一步 Undo。");

        if (problems.Count > 0)
            Debug.LogWarning(report.ToString());
        else
            Debug.Log(report.ToString());
    }

    private static Material EnsureMaterial(List<string> problems)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null)
            return existing;

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            problems.Add($"找不到 shader '{ShaderPath}'，面板材质没建出来。");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder(ImageFolder, "Materials");

        var material = new Material(shader) { name = "M_StoryTip" };

        // 只是给个初始贴图，免得材质预览是空白。运行时会被 Presenter 换掉。
        Texture2D first = LoadImage(Specs[0].image);
        if (first != null && material.HasProperty("_Texture2D"))
            material.SetTexture("_Texture2D", first);

        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();

        return material;
    }

    private static GameObject EnsureRoot(StringBuilder report)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == RootName)
            {
                report.AppendLine($"  · 复用已有根节点 '{RootName}'");
                return roots[i];
            }
        }

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Story Tips Root");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        report.AppendLine($"  + 新建根节点 '{RootName}'（场景根，Default 层）");
        return root;
    }

    private static AudioSource EnsureVoiceSource(GameObject root, StringBuilder report)
    {
        AudioSource source = root.GetComponent<AudioSource>();

        if (source == null)
        {
            source = Undo.AddComponent<AudioSource>(root);
            report.AppendLine("  + 加了 AudioSource（2D 旁白）");
        }

        Undo.RecordObject(source, "Configure Story Tips Audio");

        source.playOnAwake = false;
        source.loop = false;

        // 教学旁白，不要有方位和距离衰减
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.volume = 1f;

        return source;
    }

    private static MeshRenderer EnsurePanel(
        GameObject root,
        Material material,
        List<string> problems,
        StringBuilder report)
    {
        Transform existing = root.transform.Find(PanelName);
        GameObject panel;

        if (existing != null)
        {
            panel = existing.gameObject;
            report.AppendLine($"  · 复用已有面板 '{PanelName}'");
        }
        else
        {
            panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = PanelName;
            Undo.RegisterCreatedObjectUndo(panel, "Create Story Tip Panel");
            panel.transform.SetParent(root.transform, false);

            // CreatePrimitive 会带一个 MeshCollider，提示面板不需要
            Collider collider = panel.GetComponent<Collider>();
            if (collider != null)
                Undo.DestroyObjectImmediate(collider);

            report.AppendLine($"  + 新建面板 '{PanelName}'（Quad，无 collider）");
        }

        Undo.RecordObject(panel, "Configure Story Tip Panel");

        int uiLayer = LayerMask.NameToLayer(UiLayerName);
        if (uiLayer >= 0)
        {
            panel.layer = uiLayer;
        }
        else
        {
            problems.Add(
                $"TagManager 里没有层 '{UiLayerName}'，面板留在了 Default —— " +
                "它会出现在扫描仪的小屏幕上。");
        }

        // 全静态会被烘进光照贴图，屏幕变死图（电视那次踩过）
        GameObjectUtility.SetStaticEditorFlags(panel, 0);

        var renderer = panel.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            problems.Add($"'{PanelName}' 上没有 MeshRenderer。");
            return null;
        }

        Undo.RecordObject(renderer, "Configure Story Tip Renderer");

        if (material != null)
            renderer.sharedMaterial = material;

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        // 遮挡剔除有可能把贴在脸前的面板剔掉
        renderer.allowOcclusionWhenDynamic = false;

        panel.transform.localScale = Vector3.zero;
        panel.SetActive(false);

        return renderer;
    }

    private static T EnsureComponent<T>(GameObject go, StringBuilder report)
        where T : Component
    {
        T component = go.GetComponent<T>();

        if (component != null)
        {
            report.AppendLine($"  · 复用已有 {typeof(T).Name}");
            return component;
        }

        component = Undo.AddComponent<T>(go);
        report.AppendLine($"  + 加了 {typeof(T).Name}");

        return component;
    }

    private static void ConfigurePresenter(
        StoryTipsPresenter presenter,
        MeshRenderer panelRenderer,
        AudioSource voiceSource,
        List<string> problems,
        StringBuilder report)
    {
        if (presenter == null)
            return;

        var so = new SerializedObject(presenter);

        if (panelRenderer != null)
        {
            so.FindProperty("panel").objectReferenceValue = panelRenderer.transform;
            so.FindProperty("panelRenderer").objectReferenceValue = panelRenderer;
        }

        so.FindProperty("voiceSource").objectReferenceValue = voiceSource;

        SerializedProperty list = so.FindProperty("tips");
        list.arraySize = Specs.Length;

        int images = 0;
        int voices = 0;

        for (int i = 0; i < Specs.Length; i++)
        {
            TipSpec spec = Specs[i];
            SerializedProperty item = list.GetArrayElementAtIndex(i);

            item.FindPropertyRelative("id").intValue = (int)spec.id;

            Texture2D image = LoadImage(spec.image);
            if (image == null)
                problems.Add($"找不到贴图 '{ImageFolder}/{spec.image}.png'（提示 {spec.id}）");
            else
                images++;

            item.FindPropertyRelative("image").objectReferenceValue = image;

            AudioClip voice = null;
            if (!string.IsNullOrEmpty(spec.voice))
            {
                voice = LoadVoice(spec.voice);
                if (voice == null)
                    problems.Add($"找不到语音 '{VoiceFolder}/{spec.voice}.wav'（提示 {spec.id}）");
                else
                    voices++;
            }

            item.FindPropertyRelative("voice").objectReferenceValue = voice;

            item.FindPropertyRelative("silentDuration").floatValue =
                spec.silentDuration > 0f ? spec.silentDuration : 4f;

            item.FindPropertyRelative("voiceTailSeconds").floatValue = 0.5f;
            item.FindPropertyRelative("widthMeters").floatValue = spec.width;
            item.FindPropertyRelative("showOnce").boolValue = spec.showOnce;
            item.FindPropertyRelative("followUp").intValue = (int)spec.followUp;

            item.FindPropertyRelative("followUpDelay").floatValue =
                spec.followUpDelay > 0f ? spec.followUpDelay : 2f;

            item.FindPropertyRelative("followUpOnce").boolValue = true;
        }

        so.ApplyModifiedProperties();

        report.AppendLine(
            $"  · 填好 {Specs.Length} 条提示：贴图 {images}/{Specs.Length}，语音 {voices}/5");
    }

    private static void ConfigureBinder(
        StoryTipsStoryBinder binder,
        StoryTipsPresenter presenter)
    {
        if (binder == null)
            return;

        var so = new SerializedObject(binder);
        so.FindProperty("presenter").objectReferenceValue = presenter;
        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// 把靠近箱子和抓起透镜两个触发点挂到场景物体上。
    ///
    /// 只认 'Scanner Recovery Interaction (2)' —— 场景里有两份，(2) 才是放透镜的那个箱子，
    /// 没带后缀那份是放扫描仪的。而且 [SYS] FilterSystem 底下还有个同名的 SurfaceLens，
    /// 所以必须在 (2) 的子层级里找，不能全场按名字搜。
    /// </summary>
    private static void WireSceneTriggers(List<string> problems, StringBuilder report)
    {
        Transform recovery = FindInScene(RecoveryRootName);

        if (recovery == null)
        {
            problems.Add($"场景里找不到 '{RecoveryRootName}'，箱子和透镜的触发没挂上。");
            return;
        }

        Transform box = recovery.Find(BoxName);
        if (box == null)
        {
            problems.Add($"'{RecoveryRootName}' 下找不到 '{BoxName}'。");
        }
        else
        {
            StoryTipProximity proximity = box.GetComponent<StoryTipProximity>();

            if (proximity == null)
            {
                proximity = Undo.AddComponent<StoryTipProximity>(box.gameObject);
                report.AppendLine($"  + '{BoxName}' 上加了 StoryTipProximity（半径 2.5 m）");
            }
            else
            {
                report.AppendLine($"  · '{BoxName}' 上已有 StoryTipProximity");
            }

            var so = new SerializedObject(proximity);
            so.FindProperty("tip").intValue = (int)StoryTipId.ArchiveLensDetected;
            so.FindProperty("radius").floatValue = 2.5f;
            so.FindProperty("once").boolValue = true;
            so.ApplyModifiedProperties();
        }

        Transform lens = recovery.Find(LensName);
        if (lens == null)
        {
            problems.Add($"'{RecoveryRootName}' 下找不到 '{LensName}'。");
            return;
        }

        StoryTipOnGrab onGrab = lens.GetComponent<StoryTipOnGrab>();

        if (onGrab == null)
        {
            onGrab = Undo.AddComponent<StoryTipOnGrab>(lens.gameObject);
            report.AppendLine($"  + '{LensName}' 上加了 StoryTipOnGrab");
        }
        else
        {
            report.AppendLine($"  · '{LensName}' 上已有 StoryTipOnGrab");
        }

        var interactable = lens.GetComponent<XRBaseInteractable>();
        if (interactable == null)
            problems.Add($"'{LensName}' 上没有 XRBaseInteractable，抓取提示不会触发。");

        var lensSo = new SerializedObject(onGrab);
        lensSo.FindProperty("tip").intValue = (int)StoryTipId.LensReadyPressY;
        lensSo.FindProperty("interactable").objectReferenceValue = interactable;
        lensSo.FindProperty("once").boolValue = true;
        lensSo.ApplyModifiedProperties();
    }

    private static Texture2D LoadImage(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(
            $"{ImageFolder}/{fileName}.png");
    }

    private static AudioClip LoadVoice(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(
            $"{VoiceFolder}/{fileName}.wav");
    }

    private static Transform FindInScene(string name)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);

            for (int j = 0; j < all.Length; j++)
            {
                if (all[j].name == name)
                    return all[j];
            }
        }

        return null;
    }
}
