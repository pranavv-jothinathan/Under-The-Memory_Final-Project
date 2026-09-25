using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Places the same XRI Starter Assets XR Origin used by the main VR project.
/// Menu: Prefab Library / Add XR Origin To Open Scene
/// </summary>
public static class XROriginSetup
{
    private const string OriginPath =
        "Assets/Samples/XR Interaction Toolkit/3.0.11/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

    [InitializeOnLoadMethod]
    private static void AutoAddOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying)
                return;

            if (Object.FindFirstObjectByType<XROrigin>() != null)
                return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(OriginPath) == null)
                return;

            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.Contains("SampleScene"))
                return;

            AddToOpenScene(false);
        };
    }

    [MenuItem("Prefab Library/Add XR Origin To Open Scene")]
    public static void AddToOpenSceneMenu()
    {
        AddToOpenScene(true);
    }

    private static void AddToOpenScene(bool showDialog)
    {
        if (Object.FindFirstObjectByType<XROrigin>() != null)
        {
            if (showDialog)
                EditorUtility.DisplayDialog("XR Origin", "This scene already has an XR Origin.", "OK");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OriginPath);
        if (prefab == null)
        {
            Debug.LogError("Missing XR Origin prefab at " + OriginPath);
            return;
        }

        if (Object.FindFirstObjectByType<XRInteractionManager>() == null)
        {
            GameObject manager = new GameObject("XR Interaction Manager");
            manager.AddComponent<XRInteractionManager>();
            Undo.RegisterCreatedObjectUndo(manager, "Create XR Interaction Manager");
        }

        DisableDefaultMainCamera();

        GameObject origin = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        origin.name = "XR Origin (XR Rig)";
        origin.transform.position = Vector3.zero;
        origin.transform.rotation = Quaternion.identity;
        Undo.RegisterCreatedObjectUndo(origin, "Add XR Origin");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        if (!showDialog)
            return;

        EditorUtility.DisplayDialog(
            "XR Origin",
            "Added the same XR Origin (XR Rig) used by the main project.\n\n" +
            "Put on a headset (Quest Link / SteamVR OpenXR) and press Play.\n" +
            "The old Main Camera was disabled so it does not fight the headset camera.",
            "OK");
    }

    private static void DisableDefaultMainCamera()
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera cam = cameras[i];
            if (cam.GetComponentInParent<XROrigin>() != null)
                continue;

            if (cam.gameObject.name != "Main Camera" && !cam.CompareTag("MainCamera"))
                continue;

            Undo.RecordObject(cam.gameObject, "Disable Default Main Camera");
            cam.gameObject.SetActive(false);
            EditorUtility.SetDirty(cam.gameObject);
        }
    }
}
