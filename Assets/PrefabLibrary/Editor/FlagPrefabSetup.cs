using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Builds the handheld protest flag prefab (pole + Cloth sheet).
/// Menu: Prefab Library / Create Protest Flag
/// </summary>
public static class FlagPrefabSetup
{
    private const string Root = "Assets/PrefabLibrary/Flag";
    private const string PrefabPath = Root + "/PF_ProtestFlag.prefab";
    private const string MeshPath = Root + "/Meshes/FlagCloth.asset";
    private const string FlagMatPath = Root + "/Materials/M_ProtestFlag.mat";
    private const string PoleMatPath = Root + "/Materials/M_FlagPole.mat";
    private const string TexturePath = Root + "/Textures/T_ProtestFlag.png";
    private const string ShaderName = "PrefabLibrary/FlagUnlitDoubleSided";

    [InitializeOnLoadMethod]
    private static void AutoCreateOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying)
                return;

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath) == null)
                return;

            bool createdNow = false;
            if (!File.Exists(PrefabPath))
            {
                CreatePrefabInternal(false);
                createdNow = true;
            }

            if (createdNow)
                TryAddToSampleScene();
        };
    }

    [MenuItem("Prefab Library/Create Protest Flag")]
    public static void CreatePrefabMenu()
    {
        CreatePrefabInternal(true);
    }

    [MenuItem("Prefab Library/Add Protest Flag To Open Scene")]
    public static void AddToOpenSceneMenu()
    {
        if (!File.Exists(PrefabPath))
            CreatePrefabInternal(false);

        GameObject instance = AddToOpenScene(true);
        if (instance != null)
            Selection.activeGameObject = instance;
    }

    private static void CreatePrefabInternal(bool showDialog)
    {
        EnsureFolders();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null)
        {
            Debug.LogError("Missing flag texture at " + TexturePath);
            return;
        }

        Material flagMat = CreateFlagMaterial(texture);
        Material poleMat = CreatePoleMaterial();
        Mesh clothMesh = CreateMeshAsset();

        if (File.Exists(PrefabPath))
            AssetDatabase.DeleteAsset(PrefabPath);

        GameObject root = new GameObject("PF_ProtestFlag");

        CapsuleCollider poleCollider = root.AddComponent<CapsuleCollider>();
        poleCollider.direction = 1;
        poleCollider.radius = 0.03f;
        poleCollider.height = ProtestFlag.PoleHeight;
        poleCollider.center = new Vector3(0f, ProtestFlag.PoleHeight * 0.5f, 0f);

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 0.7f;
        body.linearDamping = 0.9f;
        body.angularDamping = 1.15f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.isKinematic = true;
        body.useGravity = false;

        XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = true;
        grab.useDynamicAttach = true;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwVelocityScale = 0.7f;
        grab.throwAngularVelocityScale = 0.6f;
        grab.attachEaseInTime = 0f;

        CreatePoleVisual(root.transform, poleMat);

        GameObject clothGo = new GameObject("FlagCloth");
        clothGo.transform.SetParent(root.transform, false);
        clothGo.transform.localPosition = Vector3.zero;
        clothGo.transform.localRotation = Quaternion.identity;
        clothGo.transform.localScale = Vector3.one;

        SkinnedMeshRenderer smr = clothGo.AddComponent<SkinnedMeshRenderer>();
        ProtestFlag.PrepareSkinnedRenderer(smr, clothMesh, clothGo.transform, flagMat);

        ProtestFlag flag = root.AddComponent<ProtestFlag>();
        SerializedObject so = new SerializedObject(flag);
        so.FindProperty("flagRenderer").objectReferenceValue = smr;
        so.FindProperty("poleCollider").objectReferenceValue = poleCollider;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!showDialog)
            return;

        EditorUtility.DisplayDialog(
            "Protest Flag",
            "Created " + PrefabPath + "\n\n" +
            "Grab the pole in VR and wave it. The cloth is pinned to the pole.\n" +
            "Also available: Prefab Library / Add Protest Flag To Open Scene",
            "OK");
    }

    private static void TryAddToSampleScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.Contains("SampleScene"))
            return;

        if (Object.FindFirstObjectByType<ProtestFlag>() != null)
            return;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            return;

        AddToOpenScene(false);
    }

    private static GameObject AddToOpenScene(bool showDialog)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("Missing protest flag prefab at " + PrefabPath);
            return null;
        }

        if (Object.FindFirstObjectByType<ProtestFlag>() != null)
        {
            if (showDialog)
                EditorUtility.DisplayDialog("Protest Flag", "This scene already has a protest flag.", "OK");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "PF_ProtestFlag";
        instance.transform.position = new Vector3(1.05f, 0f, 0.35f);
        instance.transform.rotation = Quaternion.identity;
        Undo.RegisterCreatedObjectUndo(instance, "Add Protest Flag");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return instance;
    }

    private static void EnsureFolders()
    {
        CreateFolder("Assets", "PrefabLibrary");
        CreateFolder("Assets/PrefabLibrary", "Flag");
        CreateFolder(Root, "Textures");
        CreateFolder(Root, "Materials");
        CreateFolder(Root, "Meshes");
        CreateFolder(Root, "Scripts");
        CreateFolder(Root, "Shaders");
        CreateFolder("Assets/PrefabLibrary", "Editor");
    }

    private static void CreateFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static Mesh CreateMeshAsset()
    {
        Mesh mesh = ProtestFlag.CreateClothMesh(
            ProtestFlag.FlagWidth,
            ProtestFlag.FlagHeight,
            ProtestFlag.SegsX,
            ProtestFlag.SegsY,
            ProtestFlag.PoleRadius,
            ProtestFlag.PoleHeight - ProtestFlag.FlagHeight - 0.04f);

        if (File.Exists(MeshPath))
            AssetDatabase.DeleteAsset(MeshPath);

        AssetDatabase.CreateAsset(mesh, MeshPath);
        return mesh;
    }

    private static Material CreateFlagMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Texture");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(FlagMatPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, FlagMatPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.mainTexture = texture;
        material.SetColor("_BaseColor", Color.white);
        material.color = Color.white;
        material.SetFloat("_Cull", 0f);
        material.doubleSidedGI = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreatePoleMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(PoleMatPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, PoleMatPath);
        }
        else
        {
            material.shader = shader;
        }

        Color color = new Color(0.13f, 0.09f, 0.07f, 1f);
        material.SetColor("_BaseColor", color);
        material.color = color;
        material.SetFloat("_Smoothness", 0.28f);
        material.SetFloat("_Metallic", 0.05f);
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_Cull", (float)CullMode.Back);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void CreatePoleVisual(Transform parent, Material poleMat)
    {
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(pole.GetComponent<CapsuleCollider>());
        pole.name = "Pole";
        pole.transform.SetParent(parent, false);
        pole.transform.localPosition = new Vector3(0f, ProtestFlag.PoleHeight * 0.5f, 0f);
        pole.transform.localRotation = Quaternion.identity;
        pole.transform.localScale = new Vector3(ProtestFlag.PoleRadius * 2f, ProtestFlag.PoleHeight * 0.5f, ProtestFlag.PoleRadius * 2f);
        pole.GetComponent<MeshRenderer>().sharedMaterial = poleMat;

        GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(grip.GetComponent<CapsuleCollider>());
        grip.name = "Grip";
        grip.transform.SetParent(parent, false);
        grip.transform.localPosition = new Vector3(0f, 1.08f, 0f);
        grip.transform.localRotation = Quaternion.identity;
        grip.transform.localScale = new Vector3(0.046f, 0.07f, 0.046f);
        grip.GetComponent<MeshRenderer>().sharedMaterial = poleMat;

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.DestroyImmediate(cap.GetComponent<SphereCollider>());
        cap.name = "Finial";
        cap.transform.SetParent(parent, false);
        cap.transform.localPosition = new Vector3(0f, ProtestFlag.PoleHeight, 0f);
        cap.transform.localRotation = Quaternion.identity;
        cap.transform.localScale = Vector3.one * 0.05f;
        cap.GetComponent<MeshRenderer>().sharedMaterial = poleMat;
    }
}
