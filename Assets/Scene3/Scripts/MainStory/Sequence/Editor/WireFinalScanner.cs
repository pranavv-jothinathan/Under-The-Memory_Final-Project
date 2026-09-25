using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 把柱子 / 建筑扫描从 TestRig 接到场景根上那个最终 Scanner，然后关掉 TestRig。
///
/// 最终 Scanner 根节点保持关闭：残骸箱子打开时会 SetActive(true)。
/// ScannerBehavior 组件必须勾上，否则箱子打开后 Update 仍不跑。
///
/// MainScene.unity 不能用文本工具改，跑完在 Unity 里手动保存场景。
/// </summary>
public static class WireFinalScanner
{
    private const float ScanOriginZ = 0.95f;

    [MenuItem("Prefab Library/把扫描接到最终 Scanner 并关掉 TestRig")]
    public static void Run()
    {
        var log = new StringBuilder();
        log.AppendLine("WireFinalScanner:");
        log.AppendLine();

        GameObject testRig = FindSceneRoot("TestRig");
        GameObject scanner = FindSceneRoot("Scanner");

        if (scanner == null)
        {
            Debug.LogError("WireFinalScanner: 找不到场景根上的 Scanner。");
            return;
        }

        Undo.SetCurrentGroupName("Wire Final Scanner");
        int undoGroup = Undo.GetCurrentGroup();

        ScannerBehavior behavior =
            scanner.GetComponentInChildren<ScannerBehavior>(true);

        if (behavior == null)
        {
            Debug.LogError("WireFinalScanner: Scanner 底下找不到 ScannerBehavior。");
            return;
        }

        Transform scanOrigin = FindNamedChild(scanner.transform, "ScanOrigin");
        ScannerProgressDriver driver =
            scanner.GetComponent<ScannerProgressDriver>();

        Undo.RecordObject(behavior, "Wire Final Scanner");
        behavior.enabled = true;
        EditorUtility.SetDirty(behavior);
        log.AppendLine("  · 勾上 ScannerBehavior");

        if (scanOrigin != null)
        {
            Undo.RecordObject(scanOrigin, "Wire Final Scanner");
            Vector3 local = scanOrigin.localPosition;
            local.z = ScanOriginZ;
            scanOrigin.localPosition = local;
            EditorUtility.SetDirty(scanOrigin);
            log.AppendLine($"  · ScanOrigin.local Z = {ScanOriginZ}");
        }
        else
        {
            log.AppendLine("  · 找不到 ScanOrigin");
        }

        if (driver != null)
        {
            Undo.RecordObject(driver, "Wire Final Scanner");
            driver.scannerBehavior = behavior;
            driver.scanOrigin = scanOrigin;
            EditorUtility.SetDirty(driver);
            log.AppendLine("  · ScannerProgressDriver 改指自己的 Behavior / ScanOrigin");
        }
        else
        {
            log.AppendLine("  · 找不到 ScannerProgressDriver");
        }

        if (scanner.activeSelf)
        {
            log.AppendLine("  · Scanner 根节点已经是开的（残骸箱子那条 SetActive 仍可重复调用）");
        }
        else
        {
            log.AppendLine("  · Scanner 根节点保持关闭，等残骸箱子打开再出现");
        }

        if (testRig != null)
        {
            Undo.RecordObject(testRig, "Wire Final Scanner");
            testRig.SetActive(false);
            EditorUtility.SetDirty(testRig);
            log.AppendLine("  · TestRig 已取消激活");
        }
        else
        {
            log.AppendLine("  · 场景里没有 TestRig（可能已经关掉或删了）");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(
            SceneManager.GetActiveScene()
        );

        log.AppendLine();
        log.AppendLine("Ctrl+Z 可整体回滚。请手动保存场景。");
        Debug.Log(log.ToString());
    }

    private static GameObject FindSceneRoot(string name)
    {
        GameObject[] all = Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null &&
                all[i].name == name &&
                all[i].transform.parent == null)
            {
                return all[i];
            }
        }

        return null;
    }

    private static Transform FindNamedChild(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name)
                return all[i];
        }

        return null;
    }
}
