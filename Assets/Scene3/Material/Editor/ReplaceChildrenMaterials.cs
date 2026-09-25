using UnityEngine;
using UnityEditor;

public class ReplaceChildrenMaterials : EditorWindow
{
    private GameObject targetRoot;
    private Material newMaterial;

    [MenuItem("Tools/Replace Children Materials")]
    public static void ShowWindow()
    {
        GetWindow<ReplaceChildrenMaterials>("Replace Materials");
    }

    private void OnGUI()
    {
        GUILayout.Label("Replace All Child Renderer Materials", EditorStyles.boldLabel);

        targetRoot = (GameObject)EditorGUILayout.ObjectField(
            "Target Root",
            targetRoot,
            typeof(GameObject),
            true
        );

        newMaterial = (Material)EditorGUILayout.ObjectField(
            "New Material",
            newMaterial,
            typeof(Material),
            false
        );

        GUILayout.Space(10);

        if (GUILayout.Button("Replace All Materials"))
        {
            ReplaceMaterials();
        }
    }

    private void ReplaceMaterials()
    {
        if (targetRoot == null)
        {
            Debug.LogError("Target Root 没有设置！");
            return;
        }

        if (newMaterial == null)
        {
            Debug.LogError("New Material 没有设置！");
            return;
        }

        Renderer[] renderers = targetRoot.GetComponentsInChildren<Renderer>(true);

        Undo.RecordObjects(renderers, "Replace Child Materials");

        int rendererCount = 0;
        int materialCount = 0;

        foreach (Renderer renderer in renderers)
        {
            Material[] oldMaterials = renderer.sharedMaterials;

            Material[] newMaterials = new Material[oldMaterials.Length];

            for (int i = 0; i < newMaterials.Length; i++)
            {
                newMaterials[i] = newMaterial;
                materialCount++;
            }

            renderer.sharedMaterials = newMaterials;

            EditorUtility.SetDirty(renderer);

            rendererCount++;
        }

        Debug.Log(
            $"完成！修改了 {rendererCount} 个 Renderer，" +
            $"共替换 {materialCount} 个材质槽。"
        );
    }
}