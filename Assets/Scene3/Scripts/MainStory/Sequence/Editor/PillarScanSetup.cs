using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 把三个 ScanTrigger 从旧的手部 TouchTrigger 接到扫描仪链路：
/// ScanTarget 层、非 trigger 胶囊、PillarScanTrigger、各自的 MemoryPillar。
/// </summary>
public static class PillarScanSetup
{
    [MenuItem("Prefab Library/接上柱子扫描触发")]
    public static void Run()
    {
        int scanTargetLayer = LayerMask.NameToLayer("ScanTarget");
        if (scanTargetLayer < 0)
        {
            Debug.LogError(
                "PillarScanSetup: TagManager 里找不到 ScanTarget 层。"
            );
            return;
        }

        var bindings = new (string triggerName, string pillarName)[]
        {
            ("ScanTrigger (1)", "Pillar_Cafe"),
            ("ScanTrigger (2)", "Pillar_CityHall"),
            ("ScanTrigger (3)", "Pillar_BusStop"),
        };

        var log = new StringBuilder();
        log.AppendLine("PillarScanSetup: 把柱子触发改成扫描仪扫描。");
        log.AppendLine();

        Undo.SetCurrentGroupName("Pillar Scan Setup");
        int undoGroup = Undo.GetCurrentGroup();

        int changed = 0;

        for (int i = 0; i < bindings.Length; i++)
        {
            string triggerName = bindings[i].triggerName;
            string pillarName = bindings[i].pillarName;

            GameObject trigger = FindByName(triggerName);
            MemoryPillar pillar = FindPillar(pillarName);

            if (trigger == null)
            {
                log.AppendLine($"  · 找不到 {triggerName}");
                continue;
            }

            if (pillar == null)
            {
                log.AppendLine($"  · 找不到 {pillarName}");
                continue;
            }

            Undo.RecordObject(trigger, "Pillar Scan Setup");
            trigger.layer = scanTargetLayer;

            Collider col = trigger.GetComponent<Collider>();
            if (col != null)
            {
                Undo.RecordObject(col, "Pillar Scan Setup");
                col.isTrigger = false;
                col.enabled = true;
            }

            PillarTouchTrigger oldTouch =
                trigger.GetComponent<PillarTouchTrigger>();

            if (oldTouch != null)
                Undo.DestroyObjectImmediate(oldTouch);

            PillarScanTrigger scan =
                trigger.GetComponent<PillarScanTrigger>();

            if (scan == null)
                scan = Undo.AddComponent<PillarScanTrigger>(trigger);

            Undo.RecordObject(scan, "Pillar Scan Setup");
            scan.pillar = pillar;
            scan.requiredScanTime = 1f;

            EditorUtility.SetDirty(trigger);
            changed++;

            log.AppendLine(
                $"  · {triggerName} → {pillarName}  " +
                $"layer=ScanTarget({scanTargetLayer})  " +
                $"scanTime=1s"
            );
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(
            SceneManager.GetActiveScene()
        );

        log.AppendLine();
        log.AppendLine(
            $"共配置 {changed} 个 ScanTrigger。Ctrl+Z 可整体回滚。"
        );
        Debug.Log(log.ToString());
    }

    private static GameObject FindByName(string name)
    {
        GameObject[] all = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name)
                return all[i];
        }

        return null;
    }

    private static MemoryPillar FindPillar(string name)
    {
        MemoryPillar[] all = Object.FindObjectsByType<MemoryPillar>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name)
                return all[i];
        }

        return null;
    }
}
