using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

/// <summary>
/// 把 SurfaceWorld / UnderwaterWorld 两层写进所有 XR 交互器的物理查询遮罩。
///
/// 起因：世界层级修复把水下道具从 Default 挪到了 UnderwaterWorld，而 XRI 的
/// Near-Far 交互器默认只查 Default，于是塑料瓶 / 砖块 / 钢板都抓不起来了。
///
/// WorldSwitchManager.ApplyGrabPhysicsMask() 本来会在运行时补这两位，但那依赖
/// 执行顺序，而且 Inspector 里看到的还是旧值，排查起来很误导。这里直接写进场景。
/// </summary>
public static class GrabLayerMaskFix
{
    [MenuItem("Prefab Library/修复手部抓取层遮罩")]
    public static void Run()
    {
        int worldBits = BuildWorldBits(out string layerNote);
        if (worldBits == 0)
        {
            Debug.LogError($"GrabLayerMaskFix: {layerNote}");
            return;
        }

        var log = new StringBuilder();
        log.AppendLine($"GrabLayerMaskFix: 给交互器遮罩补上 {layerNote}");
        log.AppendLine();

        Undo.SetCurrentGroupName("Grab Layer Mask Fix");
        int undoGroup = Undo.GetCurrentGroup();

        int changed = 0;
        changed += Patch<SphereInteractionCaster>("m_PhysicsLayerMask", worldBits, log);
        changed += Patch<CurveInteractionCaster>("m_RaycastMask", worldBits, log);
        changed += Patch<XRDirectInteractor>("m_PhysicsLayerMask", worldBits, log);
        changed += Patch<XRPokeInteractor>("m_PhysicsLayerMask", worldBits, log);
        changed += Patch<XRRayInteractor>("m_RaycastMask", worldBits, log);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        log.AppendLine();
        log.AppendLine($"共修改 {changed} 个交互器组件。Ctrl+Z 可整体回滚。");
        Debug.Log(log.ToString());
    }

    private static int BuildWorldBits(out string note)
    {
        int surface = LayerMask.NameToLayer("SurfaceWorld");
        int underwater = LayerMask.NameToLayer("UnderwaterWorld");

        if (surface < 0 || underwater < 0)
        {
            note = "TagManager 里找不到 SurfaceWorld / UnderwaterWorld 层";
            return 0;
        }

        note = $"SurfaceWorld({surface}) + UnderwaterWorld({underwater})";
        return (1 << surface) | (1 << underwater);
    }

    private static int Patch<T>(string field, int worldBits, StringBuilder log)
        where T : Component
    {
        T[] all = Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (all.Length == 0)
        {
            log.AppendLine($"  · 场景里没有 {typeof(T).Name}");
            return 0;
        }

        int changed = 0;

        for (int i = 0; i < all.Length; i++)
        {
            var so = new SerializedObject(all[i]);
            SerializedProperty prop = so.FindProperty(field);

            if (prop == null)
            {
                log.AppendLine($"  ! {typeof(T).Name} 上没有字段 {field}，XRI 版本可能变了");
                so.Dispose();
                continue;
            }

            int before = prop.intValue;
            int after = before | worldBits;

            if (before == after)
            {
                log.AppendLine($"  · {Path(all[i].transform)} 已包含两层");
                so.Dispose();
                continue;
            }

            Undo.RecordObject(all[i], "Grab Layer Mask Fix");
            prop.intValue = after;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(all[i]);
            so.Dispose();

            log.AppendLine($"  ✓ {Path(all[i].transform)}  {field}: {before} → {after}");
            changed++;
        }

        return changed;
    }

    private static string Path(Transform t)
    {
        var sb = new StringBuilder(t.name);
        Transform p = t.parent;
        while (p != null)
        {
            sb.Insert(0, p.name + "/");
            p = p.parent;
        }

        return sb.ToString();
    }
}
