using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 生成多层字幕帘：若干张沿 Z 排开的 Quad，每层一个 Scene3/MatrixRain 材质。
/// 字形的世界尺寸和下落速度在各层之间保持一致，靠层距和亮度衰减拉出纵深。
/// </summary>
public class MatrixCurtainBuilder : EditorWindow
{
    private enum BlendPreset
    {
        加色发光,
        预乘透明,
    }

    private const string RainShaderName = "Scene3/MatrixRain";

    private const string DefaultAtlasPath =
        "Assets/Scene3/矩阵背景/Textures/T_MatrixGlyphAtlas.png";

    private const string DefaultMaterialFolder = "Assets/Scene3/矩阵背景/Materials";

    private const string DefaultPrefabPath =
        "Assets/Scene3/矩阵背景/PF_MatrixCurtain.prefab";

    private Texture2D atlas;
    private int atlasColumns = 16;
    private int atlasRows = 16;

    private string curtainName = "[FX] MatrixCurtain";
    private int layerCount = 4;
    private float panelWidth = 30f;
    private float panelHeight = 14f;
    private float depthSpacing = 5f;
    private float scaleStep = 1.15f;

    private float glyphWorldSize = 0.45f;
    private float fallWorldSpeed = 6f;
    private float trailWorldLength = 6f;
    private float columnDensity = 0.8f;
    private float glyphChangeRate = 6f;
    private float speedVariance = 0.6f;
    private float flicker = 0.2f;
    private float headSharpness = 10f;
    private float trailFalloff = 2f;

    private Color matrixColor = new Color(0f, 1f, 0.18f, 1f);
    private Color headColor = new Color(0.7f, 1f, 0.8f, 1f);
    private float brightness = 1.5f;
    private float farBrightnessScale = 0.35f;
    private float farAlphaScale = 0.5f;

    private float fogAmount = 0f;
    private float nearFadeStart = 0f;
    private float nearFadeEnd = 0f;

    private BlendPreset blendPreset = BlendPreset.加色发光;
    private int targetLayer;
    private string materialFolder = DefaultMaterialFolder;
    private bool savePrefab = true;
    private string prefabPath = DefaultPrefabPath;

    private Vector2 scroll;

    [MenuItem("Prefab Library/矩阵字幕帘/生成字幕帘")]
    public static void Open()
    {
        MatrixCurtainBuilder window = GetWindow<MatrixCurtainBuilder>(true, "矩阵字幕帘", true);
        window.minSize = new Vector2(430f, 640f);
        window.Show();
    }

    private void OnEnable()
    {
        if (atlas == null)
            atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultAtlasPath);

        int surfaceWorld = LayerMask.NameToLayer("SurfaceWorld");
        targetLayer = surfaceWorld >= 0 ? surfaceWorld : 0;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("字形图集", EditorStyles.boldLabel);
        atlas = (Texture2D)EditorGUILayout.ObjectField("图集", atlas, typeof(Texture2D), false);
        atlasColumns = EditorGUILayout.IntSlider("图集列数", atlasColumns, 4, 32);
        atlasRows = EditorGUILayout.IntSlider("图集行数", atlasRows, 4, 32);

        if (atlas == null)
        {
            EditorGUILayout.HelpBox(
                "还没有图集。先跑 Prefab Library / 矩阵字幕帘 / 烘焙字符图集。" +
                "留空也能生成，但字符会是纯白方块。",
                MessageType.Warning
            );
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("几何", EditorStyles.boldLabel);
        curtainName = EditorGUILayout.TextField("名字", curtainName);
        layerCount = EditorGUILayout.IntSlider("层数", layerCount, 1, 8);
        panelWidth = EditorGUILayout.FloatField("首层宽（米）", panelWidth);
        panelHeight = EditorGUILayout.FloatField("首层高（米）", panelHeight);
        depthSpacing = EditorGUILayout.FloatField("层间距（米）", depthSpacing);
        scaleStep = EditorGUILayout.Slider("逐层放大", scaleStep, 1f, 1.6f);
        targetLayer = EditorGUILayout.LayerField("World 层", targetLayer);

        if (layerCount > 4)
        {
            EditorGUILayout.HelpBox(
                "VR 里透明层数越多 overdraw 越贵，4 层以内比较安全。",
                MessageType.Info
            );
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("雨", EditorStyles.boldLabel);
        glyphWorldSize = EditorGUILayout.Slider("字形边长（米）", glyphWorldSize, 0.05f, 2f);
        fallWorldSpeed = EditorGUILayout.Slider("下落速度（米/秒）", fallWorldSpeed, 0.1f, 40f);
        trailWorldLength = EditorGUILayout.Slider("拖尾长度（米）", trailWorldLength, 0.2f, 40f);
        trailFalloff = EditorGUILayout.Slider("拖尾衰减", trailFalloff, 0.2f, 8f);
        columnDensity = EditorGUILayout.Slider("列密度", columnDensity, 0f, 1f);
        speedVariance = EditorGUILayout.Slider("速度差异", speedVariance, 0f, 1f);
        glyphChangeRate = EditorGUILayout.Slider("换字频率", glyphChangeRate, 0f, 30f);
        flicker = EditorGUILayout.Slider("闪烁", flicker, 0f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("颜色", EditorStyles.boldLabel);
        matrixColor = EditorGUILayout.ColorField(
            new GUIContent("主色"), matrixColor, true, true, true
        );
        headColor = EditorGUILayout.ColorField(
            new GUIContent("头字色"), headColor, true, true, true
        );
        headSharpness = EditorGUILayout.Slider("头字锐度", headSharpness, 1f, 32f);
        brightness = EditorGUILayout.Slider("亮度", brightness, 0f, 8f);
        farBrightnessScale = EditorGUILayout.Slider("末层亮度比", farBrightnessScale, 0f, 1f);
        farAlphaScale = EditorGUILayout.Slider("末层透明度比", farAlphaScale, 0f, 1f);
        blendPreset = (BlendPreset)EditorGUILayout.EnumPopup("混合", blendPreset);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("衰减", EditorStyles.boldLabel);
        fogAmount = EditorGUILayout.Slider("吃雾程度", fogAmount, 0f, 1f);
        nearFadeStart = EditorGUILayout.FloatField("近处淡出起点（米）", nearFadeStart);
        nearFadeEnd = EditorGUILayout.FloatField("近处淡出终点（米）", nearFadeEnd);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("输出", EditorStyles.boldLabel);
        materialFolder = EditorGUILayout.TextField("材质目录", materialFolder);
        savePrefab = EditorGUILayout.Toggle("同时存成 Prefab", savePrefab);

        using (new EditorGUI.DisabledScope(!savePrefab))
        {
            prefabPath = EditorGUILayout.TextField("Prefab 路径", prefabPath);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("生成", GUILayout.Height(32f)))
        {
            Build();
        }

        EditorGUILayout.EndScrollView();
    }

    private void Build()
    {
        Shader shader = Shader.Find(RainShaderName);

        if (shader == null)
        {
            Debug.LogError(
                $"MatrixCurtainBuilder: 找不到 {RainShaderName}，" +
                "确认 Assets/Scene3/矩阵背景/Shaders/MatrixRain.shader 已经导入。"
            );
            return;
        }

        EnsureFolder(materialFolder);

        var root = new GameObject(curtainName);
        root.layer = targetLayer;

        SceneView view = SceneView.lastActiveSceneView;
        root.transform.position = view != null ? view.pivot : Vector3.zero;

        MatrixCurtainController controller = root.AddComponent<MatrixCurtainController>();
        controller.matrixColor = matrixColor;
        controller.headColor = headColor;
        controller.brightness = brightness;
        controller.alpha = 1f;
        controller.fallSpeed = 1f;
        controller.layers.Clear();

        float srcBlend = (float)UnityEngine.Rendering.BlendMode.One;
        float dstBlend = blendPreset == BlendPreset.加色发光
            ? (float)UnityEngine.Rendering.BlendMode.One
            : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;

        var log = new StringBuilder();
        log.AppendLine($"MatrixCurtainBuilder: 生成「{curtainName}」，{layerCount} 层。");

        for (int i = 0; i < layerCount; i++)
        {
            float layerScale = Mathf.Pow(scaleStep, i);
            float layerWidth = panelWidth * layerScale;
            float layerHeight = panelHeight * layerScale;

            int gridColumns = Mathf.Max(1, Mathf.RoundToInt(layerWidth / glyphWorldSize));
            int gridRows = Mathf.Max(1, Mathf.RoundToInt(layerHeight / glyphWorldSize));

            // shader 里的速度和拖尾都以「面片高度」为单位，换算成世界单位后
            // 各层的雨速和尾长才看得出是同一场雨。一个周期里雨头要多走两个
            // 拖尾长度（进场前一段、出场后一段），换算速度时得算进去。
            float layerTrail = Mathf.Clamp(trailWorldLength / layerHeight, 0.02f, 2f);
            float cycleHeight = layerHeight * (1f + 2f * layerTrail);
            float layerFallSpeed = fallWorldSpeed / cycleHeight;

            float depthT = layerCount > 1 ? i / (float)(layerCount - 1) : 0f;
            float brightnessScale = Mathf.Lerp(1f, farBrightnessScale, depthT);
            float alphaScale = Mathf.Lerp(1f, farAlphaScale, depthT);

            Material material = CreateMaterial(shader, i);
            material.SetTexture("_GlyphAtlas", atlas);
            material.SetFloat("_AtlasColumns", atlasColumns);
            material.SetFloat("_AtlasRows", atlasRows);
            material.SetFloat("_GridColumns", gridColumns);
            material.SetFloat("_GridRows", gridRows);
            material.SetFloat("_GlyphChangeRate", glyphChangeRate);
            material.SetFloat("_FallSpeed", layerFallSpeed);
            material.SetFloat("_SpeedVariance", speedVariance);
            material.SetFloat("_TrailLength", layerTrail);
            material.SetFloat("_TrailFalloff", trailFalloff);
            material.SetFloat("_ColumnDensity", columnDensity);
            material.SetFloat("_Flicker", flicker);
            material.SetFloat("_Seed", i * 137f);
            material.SetColor("_MatrixColor", matrixColor);
            material.SetColor("_HeadColor", headColor);
            material.SetFloat("_HeadSharpness", headSharpness);
            material.SetFloat("_Brightness", brightness * brightnessScale);
            material.SetFloat("_Alpha", alphaScale);
            material.SetFloat("_NearFadeStart", nearFadeStart);
            material.SetFloat("_NearFadeEnd", nearFadeEnd);
            material.SetFloat("_FogAmount", fogAmount);
            material.SetFloat("_SrcBlend", srcBlend);
            material.SetFloat("_DstBlend", dstBlend);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            EditorUtility.SetDirty(material);

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = $"MatrixLayer_{i}";
            panel.layer = targetLayer;

            Collider collider = panel.GetComponent<Collider>();

            if (collider != null)
                DestroyImmediate(collider);

            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = new Vector3(0f, 0f, i * depthSpacing);
            panel.transform.localScale = new Vector3(layerWidth, layerHeight, 1f);

            // 透明自发光面片不该进光照烘焙。
            GameObjectUtility.SetStaticEditorFlags(panel, (StaticEditorFlags)0);

            var renderer = panel.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;

            controller.layers.Add(new MatrixCurtainController.Layer
            {
                renderer = renderer,
                brightnessScale = brightnessScale,
                alphaScale = alphaScale,
                speedScale = layerFallSpeed,
                seed = i * 137f,
            });

            log.AppendLine(
                $"  · 层 {i}：{layerWidth:0.##}×{layerHeight:0.##} 米，" +
                $"网格 {gridColumns}×{gridRows}，z={i * depthSpacing:0.##}"
            );
        }

        controller.Apply();

        Undo.RegisterCreatedObjectUndo(root, "Create Matrix Curtain");

        if (savePrefab)
        {
            EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
            PrefabUtility.SaveAsPrefabAssetAndConnect(
                root, prefabPath, InteractionMode.AutomatedAction
            );
            log.AppendLine($"  · Prefab：{prefabPath}");
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = root;

        log.AppendLine();
        log.AppendLine(
            $"层已设为 {LayerMask.LayerToName(targetLayer)}({targetLayer})。" +
            "挪动或改层之后跑一次 Prefab Library / 世界层级审计（只读）。"
        );

        Debug.Log(log.ToString(), root);
    }

    private Material CreateMaterial(Shader shader, int index)
    {
        string path = $"{materialFolder}/M_MatrixRain_L{index}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (existing != null)
        {
            existing.shader = shader;
            return existing;
        }

        var material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);

        return material;
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
