using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 把水上 / 水下内容归位到 SurfaceWorld / UnderwaterWorld 层。
///
/// WorldSwitchManager 只翻转这两层的 cullingMask 位，Default 永远留在 mask 里，
/// 所以任何留在 Default 的水下物体在水上也会被画出来。
///
/// 先跑「世界层级审计（只读）」看 Console 清单，确认后再跑「应用世界层级修复」。
/// 应用是一步 Undo，按 Ctrl+Z 可整体回滚。
/// </summary>
public static class WorldLayerFixer
{
    private const string SurfaceLayer = "SurfaceWorld";
    private const string UnderwaterLayer = "UnderwaterWorld";

    private const string SurfaceRootName = "[WORLD] Surface";
    private const string UnderwaterRootName = "[WORLD] Underwater";

    /// <summary>
    /// 扫描系统 / UI / 交互系统故意设的层，批量刷层时必须原样放过，
    /// 否则扫描仪打不到目标、提示 UI 会消失。
    /// </summary>
    private static readonly string[] ProtectedLayers =
    {
        "ScanSurface",
        "ScanTarget",
        "3dgs",
        "Scannable",
        "HoloTransparent",
        "UI",
        "Overlay UI",
        "FilterLens",
        "PlayerHand",
        "Ignore Raycast",
        "VFX",
        "Portal",
    };

    /// <summary>
    /// 不在两个 WORLD 根下、但内容属于某个世界的物体。
    /// 路径以场景顶层根开头，用 / 指定子物体。
    /// </summary>
    private static readonly (string path, string layer)[] ExplicitTargets =
    {
        ("Eco_Event_root", UnderwaterLayer),
        ("DustEffects", UnderwaterLayer),
        ("3DGS_Models", UnderwaterLayer),
        ("GroundPlane", UnderwaterLayer),
        ("Underwater Ambience", UnderwaterLayer),
        ("UIgroup/EcoTips", UnderwaterLayer),
    };

    /// <summary>
    /// 有意留在 Default 的顶层根，只用于在审计报告里说明，不参与修改。
    /// </summary>
    private static readonly string[] IntentionalDefaults =
    {
        "EventsTrigger",
        "NextSceneTrigger",
        "cityhalll围墙",
        "UIgroup/LensTips",
        "UIgroup/ScannerTips",
        "[SYS] StoryTips",
        "[SEQ] Story Directors",
        "[SYS]XR",
        "[SYS] FilterSystem",
        "Scanner",
        "TestRig",
        "MainStoryManager",
        "Directional Light",
    };

    private struct Change
    {
        public GameObject go;
        public string group;
        public int fromLayer;
        public int toLayer;
    }

    [MenuItem("Prefab Library/世界层级审计（只读）")]
    public static void Audit()
    {
        List<Change> changes = Collect(out List<string> problems);
        Debug.Log(BuildReport(changes, problems, false));
    }

    [MenuItem("Prefab Library/应用世界层级修复")]
    public static void Apply()
    {
        List<Change> changes = Collect(out List<string> problems);

        if (changes.Count == 0)
        {
            Debug.Log("WorldLayerFixer: 没有需要修改的物体。\n" + BuildReport(changes, problems, false));
            return;
        }

        bool go = Application.isBatchMode || EditorUtility.DisplayDialog(
            "应用世界层级修复",
            $"将修改 {changes.Count} 个物体的 Layer。\n\n" +
            "受保护层（ScanSurface / ScanTarget / 3dgs / UI 等）会原样放过。\n" +
            "这是一步 Undo，按 Ctrl+Z 可整体回滚。\n\n" +
            "详细清单请先看「世界层级审计（只读）」。",
            "应用",
            "取消");

        if (!go)
            return;

        Undo.SetCurrentGroupName("World Layer Fix");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < changes.Count; i++)
        {
            Undo.RecordObject(changes[i].go, "World Layer Fix");
            changes[i].go.layer = changes[i].toLayer;
            EditorUtility.SetDirty(changes[i].go);
        }

        Undo.CollapseUndoOperations(undoGroup);

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(BuildReport(changes, problems, true));
    }

    private static List<Change> Collect(out List<string> problems)
    {
        problems = new List<string>();
        var changes = new List<Change>();

        int surfaceLayer = RequireLayer(SurfaceLayer, problems);
        int underwaterLayer = RequireLayer(UnderwaterLayer, problems);
        if (surfaceLayer < 0 || underwaterLayer < 0)
            return changes;

        int protectedMask = BuildProtectedMask(problems);
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        GameObject surfaceRoot = FindRoot(roots, SurfaceRootName);
        GameObject underwaterRoot = FindRoot(roots, UnderwaterRootName);

        if (surfaceRoot == null)
            problems.Add($"找不到顶层根 '{SurfaceRootName}'");
        else
            CollectSubtree(surfaceRoot.transform, surfaceLayer, protectedMask, SurfaceRootName, changes);

        if (underwaterRoot == null)
            problems.Add($"找不到顶层根 '{UnderwaterRootName}'");
        else
            CollectSubtree(underwaterRoot.transform, underwaterLayer, protectedMask, UnderwaterRootName, changes);

        foreach ((string path, string layerName) in ExplicitTargets)
        {
            Transform target = ResolvePath(roots, path);
            if (target == null)
            {
                problems.Add($"找不到 '{path}'（可能已被删除，可从 ExplicitTargets 移除）");
                continue;
            }

            int layer = LayerMask.NameToLayer(layerName);
            CollectSubtree(target, layer, protectedMask, path, changes);
        }

        return changes;
    }

    private static void CollectSubtree(
        Transform root,
        int targetLayer,
        int protectedMask,
        string group,
        List<Change> changes)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i].gameObject;

            if (go.layer == targetLayer)
                continue;

            if ((protectedMask & (1 << go.layer)) != 0)
                continue;

            changes.Add(new Change
            {
                go = go,
                group = group,
                fromLayer = go.layer,
                toLayer = targetLayer,
            });
        }
    }

    private static int RequireLayer(string name, List<string> problems)
    {
        int layer = LayerMask.NameToLayer(name);
        if (layer < 0)
            problems.Add($"TagManager 里没有层 '{name}'");

        return layer;
    }

    private static int BuildProtectedMask(List<string> problems)
    {
        int mask = 0;

        for (int i = 0; i < ProtectedLayers.Length; i++)
        {
            int layer = LayerMask.NameToLayer(ProtectedLayers[i]);
            if (layer < 0)
                continue;

            mask |= 1 << layer;
        }

        if (mask == 0)
            problems.Add("受保护层一个都没解析出来，先别执行应用");

        return mask;
    }

    private static GameObject FindRoot(GameObject[] roots, string name)
    {
        // ' [WORLD] Surface' 场景里带前导空格，统一 Trim 后比较
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name.Trim() == name)
                return roots[i];
        }

        return null;
    }

    private static Transform ResolvePath(GameObject[] roots, string path)
    {
        string[] parts = path.Split('/');
        GameObject root = FindRoot(roots, parts[0]);
        if (root == null)
            return null;

        Transform current = root.transform;
        for (int i = 1; i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
            if (current == null)
                return null;
        }

        return current;
    }

    private static string BuildReport(List<Change> changes, List<string> problems, bool applied)
    {
        var sb = new StringBuilder();
        sb.AppendLine(applied
            ? $"WorldLayerFixer: 已修改 {changes.Count} 个物体的 Layer（Ctrl+Z 可整体回滚）"
            : $"WorldLayerFixer 审计：共 {changes.Count} 个物体需要改层（本次未修改任何东西）");

        if (problems.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("── 需要注意 ──");
            for (int i = 0; i < problems.Count; i++)
                sb.AppendLine("  ! " + problems[i]);
        }

        // 按 来源根 + 原层→目标层 分组，避免刷屏
        var buckets = new Dictionary<string, List<Change>>();
        var order = new List<string>();

        for (int i = 0; i < changes.Count; i++)
        {
            string key = $"{changes[i].group}  [{LayerName(changes[i].fromLayer)} → {LayerName(changes[i].toLayer)}]";
            if (!buckets.TryGetValue(key, out List<Change> list))
            {
                list = new List<Change>();
                buckets[key] = list;
                order.Add(key);
            }

            list.Add(changes[i]);
        }

        sb.AppendLine();
        sb.AppendLine("── 分组 ──");
        for (int i = 0; i < order.Count; i++)
        {
            List<Change> list = buckets[order[i]];
            sb.AppendLine($"  {order[i]}  ×{list.Count}");

            int sample = Mathf.Min(6, list.Count);
            for (int j = 0; j < sample; j++)
                sb.AppendLine("      · " + list[j].go.name);

            if (list.Count > sample)
                sb.AppendLine($"      … 另有 {list.Count - sample} 个");
        }

        sb.AppendLine();
        sb.AppendLine("── 有意保留 Default，未处理 ──");
        for (int i = 0; i < IntentionalDefaults.Length; i++)
            sb.AppendLine("  · " + IntentionalDefaults[i]);

        sb.AppendLine();
        sb.AppendLine("── 受保护层（原样放过）──");
        sb.AppendLine("  " + string.Join(" / ", ProtectedLayers));

        return sb.ToString();
    }

    private static string LayerName(int layer)
    {
        string name = LayerMask.LayerToName(layer);
        return string.IsNullOrEmpty(name) ? $"Layer{layer}" : name;
    }
}
