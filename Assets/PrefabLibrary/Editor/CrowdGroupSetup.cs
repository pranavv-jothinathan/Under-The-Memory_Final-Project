using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// 生成 5 个单人 mixamo 预制体和 5 组人群预制体。
/// 组内的局部坐标 = 主场景世界坐标 - 该组锚点世界坐标，
/// 所以主工程那边只要把组根摆到锚点上、把父级 1.44 缩放抵消掉就位置正确。
/// </summary>
public static class CrowdGroupSetup
{
    private const string Root = "Assets/PrefabLibrary/Crowd";
    private const string ActorRoot = Root + "/Actors";
    private const string MaterialRoot = Root + "/Materials";
    private const string AnimControllerRoot = Root + "/Animators";
    private const string MixamoRoot = "Assets/PopulationSystem";
    private const string PeopleRoot = "Assets/PopulationSystem/Prefabs/People Prefabs";
    private const string LayoutScenePath = "Assets/Scenes/CrowdLayoutScene.unity";
    private const string LayoutRootName = "[CrowdLayouts]";
    private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

    // 5 个仅存的 mixamo 角色。
    private const string FbxSitFA = MixamoRoot + "/Sitting Talking.fbx";          // Ch22 女 坐姿交谈
    private const string FbxSitFB = MixamoRoot + "/Sitting Talking (1).fbx";      // Ch37 女 坐姿交谈
    private const string FbxSitMA = MixamoRoot + "/Sitting Talking (3).fbx";      // Ch08 男 坐姿交谈
    private const string FbxSitMB = MixamoRoot + "/Sitting.fbx";                  // Ch28 男 坐姿静止
    private const string FbxSoldier = MixamoRoot + "/Standing W_Briefcase Idle.fbx"; // Ch15 士兵 站姿

    private const string ActorSitFA = ActorRoot + "/PF_Sit_F_A.prefab";
    private const string ActorSitFB = ActorRoot + "/PF_Sit_F_B.prefab";
    private const string ActorSitMA = ActorRoot + "/PF_Sit_M_A.prefab";
    private const string ActorSitMB = ActorRoot + "/PF_Sit_M_B.prefab";
    private const string ActorSoldier = ActorRoot + "/PF_Soldier_Idle.prefab";

    private static readonly string[] MixamoFbxPaths =
    {
        FbxSitFA,
        FbxSitFB,
        FbxSitMA,
        FbxSitMB,
        FbxSoldier
    };

    private static readonly string[] GroupPaths =
    {
        Root + "/PF_Crowd_Group01_CafeOutdoor.prefab",
        Root + "/PF_Crowd_Group02_CafeDoorAndInterior.prefab",
        Root + "/PF_Crowd_Group03_CityHallSpeech.prefab",
        Root + "/PF_Crowd_Group04_CityHallProtest.prefab",
        Root + "/PF_Crowd_Group05_BusStop.prefab"
    };

    private static Dictionary<string, Material> materialCache;

    [MenuItem("Prefab Library/Create Crowd Actors And Groups")]
    public static void CreateAllMenu()
    {
        CreateAll(true);
    }

    [MenuItem("Prefab Library/Fix Mixamo Animation Imports")]
    public static void FixMixamoImportsMenu()
    {
        FixMixamoImports();
        AssetDatabase.SaveAssets();
        Debug.Log("Mixamo FBX animation imports reset. Check Animation tab: Length should not be 0.");
    }

    [MenuItem("Prefab Library/Add Crowd Groups To Open Scene")]
    public static void AddCrowdGroupsToOpenSceneMenu()
    {
        if (!File.Exists(GroupPaths[0]))
            CreateAll(false);

        AddGroupsToScene(SceneManager.GetActiveScene());
    }

    private static void CreateAll(bool logSuccess)
    {
        // 生成过程会换场景，先给用户机会保存当前场景。
        // batchmode 下弹窗被自动应答，返回值不可靠，直接跳过。
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        materialCache = new Dictionary<string, Material>();

        EnsureFolder(Root);
        EnsureFolder(ActorRoot);
        EnsureFolder(MaterialRoot);
        EnsureFolder(AnimControllerRoot);
        EnsureFolder(Root + "/Scripts");

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        FixMixamoImports();

        BuildActor(FbxSitFA, ActorSitFA);
        BuildActor(FbxSitFB, ActorSitFB);
        BuildActor(FbxSitMA, ActorSitMA);
        BuildActor(FbxSitMB, ActorSitMB);
        BuildActor(FbxSoldier, ActorSoldier);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        BuildGroup01CafeOutdoor();
        BuildGroup02CafeDoorAndInterior();
        BuildGroup03CityHallSpeech();
        BuildGroup04CityHallProtest();
        BuildGroup05BusStop();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        BuildLayoutScene();

        if (logSuccess)
        {
            Debug.Log(
                "人群资产已生成：\n" +
                "  单人预制体 -> " + ActorRoot + "\n" +
                "  人群组     -> " + Root + "\n" +
                "  预览场景   -> " + LayoutScenePath
            );
        }
    }

    // ------------------------------------------------------------------
    // 单人预制体
    // ------------------------------------------------------------------

    private static void BuildActor(string fbxPath, string prefabPath)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (source == null)
        {
            Debug.LogWarning("缺少 mixamo FBX: " + fbxPath);
            return;
        }

        string actorName = Path.GetFileNameWithoutExtension(prefabPath);
        var root = new GameObject(actorName);

        GameObject model = Object.Instantiate(source);
        model.name = "Model";
        model.transform.SetParent(root.transform, false);

        EnsureMetricScale(model);
        StripColliders(model);
        UpgradeMaterials(model);
        DisableShadows(model);

        AnimationClip clip = GetFirstClip(fbxPath);
        Animator animator = model.GetComponentInChildren<Animator>();
        if (animator == null)
            animator = model.AddComponent<Animator>();

        Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(fbxPath);
        if (avatar != null)
            animator.avatar = avatar;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullCompletely;

        if (clip != null && clip.length > 0.001f)
        {
            animator.runtimeAnimatorController = GetOrCreateLoopController(fbxPath, clip);
            clip.SampleAnimation(model, 0f);
        }

        // 坐姿的脚要落在 y = 0，靠采样后的实测 bounds 对齐，不信家具碰撞盒。
        AlignFeetToOrigin(root.transform, model.transform);

        AddMarkers(root.transform, model.transform);

        var actor = root.AddComponent<StaticCrowdActor>();
        var so = new SerializedObject(actor);
        so.FindProperty("populationState").stringValue = "mixamo_com";
        so.FindProperty("randomStartPhase").boolValue = true;
        so.FindProperty("freezePose").boolValue = false;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    /// <summary>
    /// 把模型往上/下挪，让实测包围盒底面贴到组根的 y = 0。
    /// 用 BakeMesh 读当前采样姿势的真实体积，SkinnedMeshRenderer.bounds 在编辑器里只反映绑定姿势，不可信。
    /// 偏移超过 0.6m 视为测量异常，只警告不修改。
    /// </summary>
    private static void AlignFeetToOrigin(Transform root, Transform model)
    {
        if (!TryMeasurePosedBounds(model, out Bounds bounds))
            return;

        float localMinY = root.InverseTransformPoint(bounds.min).y;
        if (Mathf.Abs(localMinY) < 0.005f)
            return;

        if (Mathf.Abs(localMinY) > 0.6f)
        {
            Debug.LogWarning(
                root.name + "：实测底面在 y = " + localMinY.ToString("F2") +
                "，偏差太大没有自动对齐，需要手动确认坐姿高度。"
            );

            return;
        }

        Vector3 local = model.localPosition;
        local.y -= localMinY;
        model.localPosition = local;
    }

    /// <summary>mixamo 导出偶尔差 100 倍，这里只在明显不对时纠正。</summary>
    private static void EnsureMetricScale(GameObject model)
    {
        if (!TryMeasurePosedBounds(model.transform, out Bounds bounds))
            return;

        float height = bounds.size.y;
        if (height > 10f)
            model.transform.localScale = Vector3.one * 0.01f;
        else if (height > 0f && height < 0.2f)
            model.transform.localScale = Vector3.one * 100f;
    }

    /// <summary>把当前姿势烘成网格再量世界包围盒。</summary>
    private static bool TryMeasurePosedBounds(Transform model, out Bounds bounds)
    {
        bounds = new Bounds();
        bool has = false;

        foreach (SkinnedMeshRenderer skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinned.sharedMesh == null)
                continue;

            var baked = new Mesh();
            skinned.BakeMesh(baked, true);
            Bounds local = baked.bounds;
            Object.DestroyImmediate(baked);

            AccumulateCorners(skinned.transform, local, ref bounds, ref has);
        }

        foreach (MeshRenderer mesh in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!has)
            {
                bounds = mesh.bounds;
                has = true;
                continue;
            }

            bounds.Encapsulate(mesh.bounds);
        }

        return has;
    }

    private static void AccumulateCorners(Transform space, Bounds local, ref Bounds bounds, ref bool has)
    {
        Vector3 c = local.center;
        Vector3 e = local.extents;

        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                c.x + ((i & 1) == 0 ? -e.x : e.x),
                c.y + ((i & 2) == 0 ? -e.y : e.y),
                c.z + ((i & 4) == 0 ? -e.z : e.z)
            );

            Vector3 world = space.TransformPoint(corner);
            if (!has)
            {
                bounds = new Bounds(world, Vector3.zero);
                has = true;
                continue;
            }

            bounds.Encapsulate(world);
        }
    }

    /// <summary>每个角色都带一个视野探针点和一个台词锚点，主工程按名字找。</summary>
    private static void AddMarkers(Transform root, Transform model)
    {
        float headHeight = MeasureHeight(model);

        var probe = new GameObject("VisProbe");
        probe.transform.SetParent(root, false);
        probe.transform.localPosition = new Vector3(0f, Mathf.Max(0.6f, headHeight * 0.62f), 0f);

        var voice = new GameObject("VoiceAnchor");
        voice.transform.SetParent(root, false);
        voice.transform.localPosition = new Vector3(0f, Mathf.Max(0.8f, headHeight * 0.92f), 0f);
    }

    private static float MeasureHeight(Transform model)
    {
        return TryMeasurePosedBounds(model, out Bounds bounds) ? bounds.size.y : 1.7f;
    }

    // ------------------------------------------------------------------
    // 第 1 组：咖啡厅外景（锚点 = 咖啡桌 (1)，地面 y = 1.85）
    // ------------------------------------------------------------------

    private static void BuildGroup01CafeOutdoor()
    {
        Transform root = new GameObject("PF_Crowd_Group01_CafeOutdoor").transform;

        // 咖啡椅 (4) 与 咖啡椅 (5) 相对咖啡桌 (1) 的偏移，两人面朝桌子。
        PlaceActor(root, ActorSitMA, "Outdoor_Man_Ch08", new Vector3(2.92f, 0f, 0.19f), Vector3.zero);
        PlaceActor(root, ActorSitFB, "Outdoor_Woman_Ch37", new Vector3(-2.91f, 0f, -0.26f), Vector3.zero);

        SaveGroupPrefab(root, GroupPaths[0]);
    }

    // ------------------------------------------------------------------
    // 第 2 组：咖啡厅走廊 + 内景（锚点 = 内景 Table，地板 y = 2.20）
    // ------------------------------------------------------------------

    private static void BuildGroup02CafeDoorAndInterior()
    {
        Transform root = new GameObject("PF_Crowd_Group02_CafeDoorAndInterior").transform;

        // 走廊：靠 Door 3 那侧，两人对着闲聊。位置是估的，主工程里可以整块拖。
        Transform corridor = CreateChild(root, "Corridor");
        PlacePopulation(corridor, "Man_33", "Corridor_Man", new Vector3(15.69f, 0f, -1.02f), new Vector3(17.19f, 0f, -1.02f), "talk2");
        PlacePopulation(corridor, "Girl_44", "Corridor_Woman", new Vector3(17.19f, 0f, -1.02f), new Vector3(15.69f, 0f, -1.02f), "listen");

        // 店员：站吧台内侧，面朝 -X。
        Transform staff = CreateChild(root, "Staff");
        PlacePopulation(staff, "Man_11", "Staff_Man", new Vector3(9.69f, 0f, -2.82f), 270f, "talk1");
        PlacePopulation(staff, "Girl_22", "Staff_Woman", new Vector3(9.49f, 0f, -5.82f), 270f, "idle1");

        // 顾客：坐前两组桌椅，各一男一女，面朝桌子。
        Transform customers = CreateChild(root, "Customers");
        PlaceActor(customers, ActorSitMA, "TableA_Man_Ch08", new Vector3(-1.59f, 0f, -0.02f), new Vector3(0f, 0f, -0.02f));
        PlaceActor(customers, ActorSitFA, "TableA_Woman_Ch22", new Vector3(1.71f, 0f, -0.02f), new Vector3(0f, 0f, -0.02f));
        PlaceActor(customers, ActorSitMB, "TableB_Man_Ch28", new Vector3(-1.59f, 0f, -4.36f), new Vector3(0f, 0f, -4.36f));
        PlaceActor(customers, ActorSitFB, "TableB_Woman_Ch37", new Vector3(1.71f, 0f, -4.36f), new Vector3(0f, 0f, -4.36f));

        SaveGroupPrefab(root, GroupPaths[1]);
    }

    // ------------------------------------------------------------------
    // 第 3 组：市政厅阶段一演讲（锚点 = 演讲台，地面 y = 3.15）
    // 听众少量、站散，留出往东穿出去的空隙。没有空气墙。
    // ------------------------------------------------------------------

    private static void BuildGroup03CityHallSpeech()
    {
        Transform root = new GameObject("PF_Crowd_Group03_CityHallSpeech").transform;

        Transform stage = CreateChild(root, "Speaker");
        PlacePopulation(stage, "Man_99", "Speaker", new Vector3(-1.2f, 0f, 0f), 90f, "talk2");

        Transform press = CreateChild(root, "Press");
        PlacePopulation(press, "Man_22", "Press_A", new Vector3(2.6f, 0f, 2.2f), 270f, "talk1");
        PlacePopulation(press, "Girl_33", "Press_B", new Vector3(3.2f, 0f, 0.6f), 268f, "idle1");
        PlacePopulation(press, "Man_44", "Press_C", new Vector3(2.8f, 0f, -1.4f), 274f, "listen");
        PlacePopulation(press, "Girl_55", "Press_D", new Vector3(3.6f, 0f, -2.8f), 280f, "talk1");

        Transform audience = CreateChild(root, "Audience");
        Vector3[] spots =
        {
            new Vector3(5.5f, 0f, 3.4f),
            new Vector3(6.2f, 0f, -2.6f),
            new Vector3(7.8f, 0f, 1.2f),
            new Vector3(8.6f, 0f, -4.2f),
            new Vector3(9.4f, 0f, 4.6f),
            new Vector3(10.2f, 0f, -1.0f),
            new Vector3(11.0f, 0f, 2.6f),
            new Vector3(11.5f, 0f, -3.4f)
        };

        string[] people = { "Man_11", "Girl_11", "Man_33", "Girl_66", "Man_55", "Girl_77", "Man_99", "Girl_44" };
        string[] states = { "idle1", "claphands", "idle2", "idle1", "claphands", "idle2", "idle1", "idle2" };

        for (int i = 0; i < spots.Length; i++)
        {
            PlacePopulation(
                audience,
                people[i],
                "Audience_" + (i + 1),
                spots[i],
                262f + (i * 5f % 26f),
                states[i]
            );
        }

        SaveGroupPrefab(root, GroupPaths[2]);
    }

    // ------------------------------------------------------------------
    // 第 4 组：市政厅阶段二抗议（锚点 = 演讲台）
    // 士兵站人群前面面朝人群，人多，带空气墙。
    // ------------------------------------------------------------------

    private static void BuildGroup04CityHallProtest()
    {
        Transform root = new GameObject("PF_Crowd_Group04_CityHallProtest").transform;

        Transform soldiers = CreateChild(root, "Soldiers");
        PlaceActor(soldiers, ActorSoldier, "Soldier_L", new Vector3(3.0f, 0f, 2.0f), 90f);
        PlaceActor(soldiers, ActorSoldier, "Soldier_R", new Vector3(3.0f, 0f, -2.0f), 90f);

        Transform crowd = CreateChild(root, "ProtestCrowd");
        float[] columns = { 6.2f, 7.9f, 9.6f, 11.3f };
        float[] rows = { 4.2f, 1.4f, -1.4f, -4.2f };

        string[] people =
        {
            "Man_11", "Girl_11", "Man_22", "Girl_22",
            "Man_33", "Girl_33", "Man_44", "Girl_44",
            "Man_55", "Girl_55", "Man_66", "Girl_66",
            "Man_77", "Girl_77", "Man_88", "Man_111"
        };

        string[] states =
        {
            "cheer", "talk1", "cheer", "listen",
            "talk2", "cheer", "talk1", "cheer",
            "cheer", "talk2", "listen", "cheer",
            "talk1", "cheer", "talk2", "cheer"
        };

        for (int i = 0; i < people.Length; i++)
        {
            int col = i % columns.Length;
            int row = i / columns.Length;
            var pos = new Vector3(
                columns[col] + (i % 3 - 1) * 0.22f,
                0f,
                rows[row] + (i % 2 == 0 ? 0.24f : -0.18f)
            );

            PlacePopulation(crowd, people[i], "Protester_" + (i + 1), pos, 258f + (i * 7f % 30f), states[i]);
        }

        // 空气墙：把人群三面围住，玩家只能从演讲台那侧看。
        Transform blockers = CreateChild(root, "Blockers");
        CreateBlocker(blockers, "Blocker_East", new Vector3(12.8f, 1.3f, 0f), new Vector3(0.8f, 2.6f, 12.5f));
        CreateBlocker(blockers, "Blocker_North", new Vector3(9.4f, 1.3f, 6.0f), new Vector3(7.6f, 2.6f, 0.8f));
        CreateBlocker(blockers, "Blocker_South", new Vector3(9.4f, 1.3f, -6.0f), new Vector3(7.6f, 2.6f, 0.8f));

        SaveGroupPrefab(root, GroupPaths[3]);
    }

    // ------------------------------------------------------------------
    // 第 5 组：公交站（锚点 = BusStop，地面 y = 3.16）
    // 候车亭是一整块 mesh，长椅没有独立物体，坐姿是估的位置。
    // ------------------------------------------------------------------

    private static void BuildGroup05BusStop()
    {
        Transform root = new GameObject("PF_Crowd_Group05_BusStop").transform;

        Transform standing = CreateChild(root, "Standing");
        PlacePopulation(standing, "Man_55", "Wait_Man_A", new Vector3(-2.04f, 0f, 1.12f), new Vector3(-1.04f, 0f, 1.52f), "talk1");
        PlacePopulation(standing, "Girl_66", "Wait_Woman_A", new Vector3(-1.04f, 0f, 1.52f), new Vector3(-2.04f, 0f, 1.12f), "listen");
        PlacePopulation(standing, "Man_88", "Wait_Man_B", new Vector3(1.76f, 0f, 1.32f), new Vector3(2.76f, 0f, 0.92f), "talk2");
        PlacePopulation(standing, "Girl_77", "Wait_Woman_B", new Vector3(2.76f, 0f, 0.92f), new Vector3(1.76f, 0f, 1.32f), "listen");

        Transform seated = CreateChild(root, "Seated");
        PlaceActor(seated, ActorSitMB, "Bench_Man_Ch28", new Vector3(-0.84f, 0f, -0.48f), 0f);
        PlaceActor(seated, ActorSitFA, "Bench_Woman_Ch22", new Vector3(0.76f, 0f, -0.48f), 0f);

        SaveGroupPrefab(root, GroupPaths[4]);
    }

    // ------------------------------------------------------------------
    // 放置帮助函数
    // ------------------------------------------------------------------

    private static void PlaceActor(Transform parent, string actorPrefabPath, string name, Vector3 localPos, Vector3 lookAtLocal)
    {
        float yaw = YawTowards(localPos, lookAtLocal);
        PlaceActor(parent, actorPrefabPath, name, localPos, yaw);
    }

    private static void PlaceActor(Transform parent, string actorPrefabPath, string name, Vector3 localPos, float yaw)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(actorPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("缺少单人预制体: " + actorPrefabPath);
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPos;
        instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        instance.name = name;
    }

    private static void PlacePopulation(Transform parent, string peopleName, string name, Vector3 localPos, Vector3 lookAtLocal, string state)
    {
        PlacePopulation(parent, peopleName, name, localPos, YawTowards(localPos, lookAtLocal), state);
    }

    private static void PlacePopulation(Transform parent, string peopleName, string name, Vector3 localPos, float yaw, string state)
    {
        string path = PeopleRoot + "/" + peopleName + ".prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("缺少 PopulationSystem 角色: " + path);
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPos;
        instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        instance.name = name;

        StripColliders(instance);
        UpgradeMaterials(instance);
        DisableShadows(instance);

        Animator animator = instance.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
        }

        var actor = instance.GetComponent<StaticCrowdActor>();
        if (actor == null)
            actor = instance.AddComponent<StaticCrowdActor>();

        var so = new SerializedObject(actor);
        so.FindProperty("populationState").stringValue = state;
        so.FindProperty("randomStartPhase").boolValue = true;
        so.FindProperty("freezePose").boolValue = false;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.ApplyModifiedPropertiesWithoutUndo();

        AddMarkers(instance.transform, instance.transform);
    }

    private static void CreateBlocker(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = false;
        box.center = Vector3.zero;
        box.size = size;

        go.AddComponent<CrowdBlocker>();
    }

    private static float YawTowards(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return 0f;

        return Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y;
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static void SaveGroupPrefab(Transform root, string prefabPath)
    {
        EnsureFolder(Path.GetDirectoryName(prefabPath)?.Replace("\\", "/"));
        PrefabUtility.SaveAsPrefabAsset(root.gameObject, prefabPath);
        Object.DestroyImmediate(root.gameObject);
    }

    // ------------------------------------------------------------------
    // 导入 / 材质 / 杂项
    // ------------------------------------------------------------------

    private static void FixMixamoImports()
    {
        foreach (string path in MixamoFbxPaths)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning("mixamo FBX 不存在，已跳过: " + path);
                continue;
            }

            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                continue;

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.clipAnimations = new ModelImporterClipAnimation[0];
            importer.SaveAndReimport();

            DownscaleEmbeddedTextures(path);
        }

        foreach (string path in MixamoFbxPaths)
        {
            if (!File.Exists(path))
                continue;

            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (sub is not AnimationClip clip || clip.name.StartsWith("__preview__"))
                    continue;

                if (clip.length <= 0.001f)
                {
                    Debug.LogWarning(
                        "Mixamo clip 长度还是 0：" + path +
                        "。需要重新从 Mixamo 下载（With Skin, FBX Binary, 30fps）。",
                        clip
                    );

                    continue;
                }

                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                settings.loopBlend = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
            }
        }

        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// mixamo 的 .fbm 贴图是 4K，一屏十几个角色顶不住。压到 1K，
    /// 导入设置写在 .meta 里，拷进主工程后同样生效。
    /// </summary>
    private static void DownscaleEmbeddedTextures(string fbxPath)
    {
        string fbmFolder = Path.ChangeExtension(fbxPath, null) + ".fbm";
        if (!AssetDatabase.IsValidFolder(fbmFolder))
            return;

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { fbmFolder }))
        {
            string texturePath = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(texturePath) is not TextureImporter importer)
                continue;

            if (importer.maxTextureSize <= 1024)
                continue;

            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
    }

    private static AnimationClip GetFirstClip(string fbxPath)
    {
        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
        {
            if (sub is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        }

        return null;
    }

    private static RuntimeAnimatorController GetOrCreateLoopController(string fbxPath, AnimationClip clip)
    {
        string safeName = MakeSafeName(Path.GetFileNameWithoutExtension(fbxPath));
        string controllerPath = AnimControllerRoot + "/AC_" + safeName + ".controller";
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (existing != null)
            return existing;

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = machine.AddState("mixamo_com");
        state.motion = clip;
        machine.defaultState = state;
        return controller;
    }

    /// <summary>
    /// legacy Standard 材质统一换成 URP Lit，并且落成真正的资产文件，
    /// 否则保存进预制体后引用会丢。
    /// </summary>
    private static void UpgradeMaterials(GameObject root)
    {
        Shader urpLit = Shader.Find(UrpLitShaderName);
        if (urpLit == null)
        {
            Debug.LogWarning("找不到 " + UrpLitShaderName + "，材质没有转换。");
            return;
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < mats.Length; i++)
            {
                Material replacement = GetOrCreateUrpMaterial(mats[i], urpLit);
                if (replacement == null || replacement == mats[i])
                    continue;

                mats[i] = replacement;
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = mats;
        }
    }

    private static Material GetOrCreateUrpMaterial(Material src, Shader urpLit)
    {
        if (src == null)
            return null;

        bool alreadyUrp = src.shader != null && src.shader.name.Contains("Universal Render Pipeline");
        if (alreadyUrp && AssetDatabase.Contains(src))
            return src;

        materialCache ??= new Dictionary<string, Material>();

        string key = src.name;
        if (materialCache.TryGetValue(key, out Material cached) && cached != null)
            return cached;

        string path = MaterialRoot + "/M_" + MakeSafeName(key) + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            materialCache[key] = existing;
            return existing;
        }

        var dst = new Material(urpLit) { name = "M_" + MakeSafeName(key) };

        Texture baseMap = null;
        if (src.HasProperty("_MainTex"))
            baseMap = src.GetTexture("_MainTex");
        if (baseMap == null && src.HasProperty("_BaseMap"))
            baseMap = src.GetTexture("_BaseMap");
        if (baseMap != null)
            dst.SetTexture("_BaseMap", baseMap);

        Color baseColor = Color.white;
        if (src.HasProperty("_Color"))
            baseColor = src.GetColor("_Color");
        else if (src.HasProperty("_BaseColor"))
            baseColor = src.GetColor("_BaseColor");
        dst.SetColor("_BaseColor", baseColor);

        if (src.HasProperty("_BumpMap"))
        {
            Texture bump = src.GetTexture("_BumpMap");
            if (bump != null)
            {
                dst.SetTexture("_BumpMap", bump);
                dst.EnableKeyword("_NORMALMAP");
            }
        }

        dst.SetFloat("_Smoothness", 0.15f);

        EnsureFolder(MaterialRoot);
        AssetDatabase.CreateAsset(dst, path);
        materialCache[key] = dst;
        return dst;
    }

    private static void StripColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            Object.DestroyImmediate(colliders[i]);
    }

    private static void DisableShadows(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static string MakeSafeName(string raw)
    {
        return raw
            .Replace(" ", "_")
            .Replace("(", "")
            .Replace(")", "")
            .Replace("/", "_")
            .Replace("\\", "_");
    }

    private static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        path = path.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folder = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folder);
    }

    // ------------------------------------------------------------------
    // 工厂预览场景
    // ------------------------------------------------------------------

    private static void BuildLayoutScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var layouts = new GameObject(LayoutRootName);
        SpawnGroupRow(scene, layouts.transform);
        EditorSceneManager.SaveScene(scene, LayoutScenePath);
    }

    private static void AddGroupsToScene(Scene scene)
    {
        GameObject layouts = GameObject.Find(LayoutRootName) ?? new GameObject(LayoutRootName);
        SpawnGroupRow(scene, layouts.transform);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void SpawnGroupRow(Scene scene, Transform parent)
    {
        const float spacing = 34f;

        for (int i = 0; i < GroupPaths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GroupPaths[i]);
            if (prefab == null)
                continue;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(parent, false);
            instance.transform.position = new Vector3(i * spacing, 0f, 0f);
        }
    }
}
