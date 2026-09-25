using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Builds a fixed dartboard with three pull-off / throwable darts.
/// Menu: Prefab Library / Create Dartboard
/// </summary>
public static class DartPrefabSetup
{
    private const string Root = "Assets/PrefabLibrary/Darts";
    private const string BoardPrefabPath = Root + "/PF_DartBoard.prefab";
    private const string SourceBoard = "Assets/飞镖模型/Prefabs/DartBoard_1.prefab";

    private const string PhysMatPath = Root + "/Materials/M_DartNoBounce.asset";
    private const float TargetBoardDiameter = 0.46f;
    private const float TargetDartLength = 0.16f;
    private const float InitialEmbed = 0.02f;

    private static readonly int[] DartStyles = { 1, 3, 5 };

    private static readonly Vector2[] DartFaceOffsets =
    {
        new Vector2(0.07f, 0.05f),
        new Vector2(-0.075f, 0.015f),
        new Vector2(0.01f, -0.07f)
    };

    private static readonly Vector3[] DartTilts =
    {
        new Vector3(6f, -4f, 18f),
        new Vector3(-5f, 7f, 52f),
        new Vector3(4f, 3f, 78f)
    };

    [InitializeOnLoadMethod]
    private static void AutoCreateOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying)
                return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(SourceBoard) == null)
                return;

            if (!File.Exists(BoardPrefabPath) || DartsAreFloatingOffTheBoard())
            {
                CreatePrefabsInternal(false);
                TryAddToSampleScene();
            }
            else
            {
                UpgradeThrowSettings();
            }
        };
    }

    [MenuItem("Prefab Library/Create Dartboard")]
    public static void CreatePrefabMenu()
    {
        CreatePrefabsInternal(true);
    }

    [MenuItem("Prefab Library/Add Dartboard To Open Scene")]
    public static void AddToOpenSceneMenu()
    {
        if (!File.Exists(BoardPrefabPath))
            CreatePrefabsInternal(false);

        GameObject instance = AddToOpenScene(true);
        if (instance != null)
            Selection.activeGameObject = instance;
    }

    private static void CreatePrefabsInternal(bool showDialog)
    {
        EnsureFolders();

        GameObject boardSource = AssetDatabase.LoadAssetAtPath<GameObject>(SourceBoard);
        if (boardSource == null)
        {
            Debug.LogError("Missing dartboard model at " + SourceBoard);
            return;
        }

        string[] dartPrefabPaths = new string[DartStyles.Length];
        for (int i = 0; i < DartStyles.Length; i++)
        {
            GameObject dartSource = LoadDartSource(DartStyles[i]);
            if (dartSource == null)
            {
                Debug.LogError("Missing dart model for style " + DartStyles[i]);
                return;
            }

            dartPrefabPaths[i] = Root + "/PF_ThrowableDart" + DartStyles[i] + ".prefab";
            CreateDartPrefab(dartSource, dartPrefabPaths[i], "PF_ThrowableDart" + DartStyles[i]);
        }

        CreateBoardPrefab(boardSource, dartPrefabPaths);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!showDialog)
            return;

        EditorUtility.DisplayDialog(
            "Dartboard",
            "Created " + BoardPrefabPath + "\n\n" +
            "The board stays fixed. Pull a dart off, throw it, and it sticks on impact.\n" +
            "Also available: Prefab Library / Add Dartboard To Open Scene",
            "OK");
    }

    private static void CreateDartPrefab(GameObject source, string path, string name)
    {
        GameObject root = new GameObject(name);

        GameObject visual = InstantiateSource(source);
        visual.name = source.name;
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        UnpackIfPrefab(visual);
        StripColliders(visual);
        ClearStatic(visual);

        ScaleAlongLongestAxis(root, TargetDartLength);

        Vector3 tipLocal = FindTipLocalOffset(root.transform);
        Bounds world = EncapsulateRenderers(root);
        LocalBounds(root.transform, world, out Vector3 localCenter, out Vector3 localSize);

        CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
        capsule.direction = 2;
        capsule.center = localCenter;
        capsule.height = Mathf.Max(localSize.z + 0.01f, 0.09f);
        capsule.radius = Mathf.Max(0.02f, Mathf.Max(localSize.x, localSize.y) * 0.55f);
        capsule.sharedMaterial = LoadOrCreatePhysicsMaterial();

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 0.05f;
        body.linearDamping = 0.1f;
        body.angularDamping = 1f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.isKinematic = true;
        body.useGravity = false;
        body.centerOfMass = Vector3.Lerp(localCenter, tipLocal, 0.35f);

        XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.retainTransformParent = false;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwVelocityScale = 0f;
        grab.throwAngularVelocityScale = 0f;
        grab.attachEaseInTime = 0f;
        grab.colliders.Clear();
        grab.colliders.Add(capsule);

        ThrowableDart dart = root.AddComponent<ThrowableDart>();
        SerializedObject so = new SerializedObject(dart);
        so.FindProperty("tipLocalOffset").vector3Value = tipLocal;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void CreateBoardPrefab(GameObject boardSource, string[] dartPrefabPaths)
    {
        GameObject root = new GameObject("PF_DartBoard");

        GameObject visual = InstantiateSource(boardSource);
        visual.name = "BoardVisual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        UnpackIfPrefab(visual);
        StripColliders(visual);
        ClearStatic(visual);
        FaceVisualAlongZ(visual);
        ScaleBoardDiameter(visual, TargetBoardDiameter);

        MeshFilter filter = visual.GetComponentInChildren<MeshFilter>();
        GameObject colliderHost = filter != null ? filter.gameObject : visual;
        MeshCollider meshCollider = colliderHost.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = filter != null ? filter.sharedMesh : null;
        meshCollider.convex = false;
        meshCollider.sharedMaterial = LoadOrCreatePhysicsMaterial();

        if (meshCollider.sharedMesh == null)
        {
            Object.DestroyImmediate(meshCollider);
            Bounds world = EncapsulateRenderers(visual);
            LocalBounds(colliderHost.transform, world, out Vector3 boxCenter, out Vector3 boxSize);
            BoxCollider box = colliderHost.AddComponent<BoxCollider>();
            box.center = boxCenter;
            box.size = Vector3.Max(boxSize, new Vector3(0.2f, 0.2f, 0.02f));
            box.sharedMaterial = LoadOrCreatePhysicsMaterial();
        }

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 8f;
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeAll;
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        float radius = BoardRadius(root.transform);
        DartBoardTarget target = root.AddComponent<DartBoardTarget>();
        SerializedObject boardSo = new SerializedObject(target);
        Collider boardCollider = colliderHost.GetComponent<Collider>();
        boardSo.FindProperty("boardCollider").objectReferenceValue = boardCollider;
        boardSo.FindProperty("faceRadius").floatValue = radius;
        boardSo.ApplyModifiedPropertiesWithoutUndo();

        for (int i = 0; i < dartPrefabPaths.Length; i++)
        {
            GameObject dartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(dartPrefabPaths[i]);
            if (dartPrefab == null)
                continue;

            GameObject dart = (GameObject)PrefabUtility.InstantiatePrefab(dartPrefab, root.transform);
            dart.name = dartPrefab.name;
            PlaceDartOnBoard(dart, root.transform, DartFaceOffsets[i], DartTilts[i]);
        }

        PrefabUtility.SaveAsPrefabAsset(root, BoardPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void PlaceDartOnBoard(GameObject dart, Transform board, Vector2 faceXY, Vector3 tiltEuler)
    {
        ThrowableDart component = dart.GetComponent<ThrowableDart>();
        Vector3 tipLocal = new Vector3(0f, 0f, -0.08f);
        if (component != null)
        {
            SerializedObject so = new SerializedObject(component);
            tipLocal = so.FindProperty("tipLocalOffset").vector3Value;
        }

        dart.transform.SetParent(board, true);
        dart.transform.localRotation = Quaternion.Euler(tiltEuler.x, tiltEuler.y, tiltEuler.z);

        Vector3 targetTipWorld;
        Collider boardCollider = board.GetComponentInChildren<Collider>();
        Vector3 rayStart = board.TransformPoint(new Vector3(faceXY.x, faceXY.y, 0.7f));
        Vector3 rayDir = -board.forward;
        if (boardCollider != null && boardCollider.Raycast(new Ray(rayStart, rayDir), out RaycastHit hit, 1.6f))
        {
            targetTipWorld = hit.point + rayDir.normalized * InitialEmbed;
        }
        else
        {
            float frontZ = FrontLocalZ(board);
            targetTipWorld = board.TransformPoint(new Vector3(faceXY.x, faceXY.y, frontZ - InitialEmbed));
        }

        Vector3 currentTipWorld = dart.transform.TransformPoint(tipLocal);
        dart.transform.position += targetTipWorld - currentTipWorld;
    }

    private static bool DartsAreFloatingOffTheBoard()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BoardPrefabPath);
        if (root == null)
            return true;

        ThrowableDart[] darts = root.GetComponentsInChildren<ThrowableDart>(true);
        bool bad = darts.Length < 3;
        if (!bad)
        {
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            for (int i = 0; i < darts.Length; i++)
            {
                float z = root.transform.InverseTransformPoint(darts[i].transform.position).z;
                minZ = Mathf.Min(minZ, z);
                maxZ = Mathf.Max(maxZ, z);
            }

            bad = maxZ - minZ > 0.08f || maxZ > 0.22f;
        }

        PrefabUtility.UnloadPrefabContents(root);
        return bad;
    }

    private static void UpgradeThrowSettings()
    {
        for (int i = 0; i < DartStyles.Length; i++)
            UpgradeDartPrefab(Root + "/PF_ThrowableDart" + DartStyles[i] + ".prefab");

        if (!File.Exists(BoardPrefabPath))
            return;

        GameObject board = PrefabUtility.LoadPrefabContents(BoardPrefabPath);
        bool dirty = false;
        XRGrabInteractable[] grabs = board.GetComponentsInChildren<XRGrabInteractable>(true);
        Rigidbody[] bodies = board.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < grabs.Length; i++)
            dirty |= ApplyThrowSettings(grabs[i]);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i].GetComponent<ThrowableDart>() == null)
                continue;
            dirty |= ApplyDartBodySettings(bodies[i]);
        }

        if (dirty)
            PrefabUtility.SaveAsPrefabAsset(board, BoardPrefabPath);
        PrefabUtility.UnloadPrefabContents(board);
    }

    private static void UpgradeDartPrefab(string path)
    {
        if (!File.Exists(path))
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        bool dirty = ApplyThrowSettings(root.GetComponent<XRGrabInteractable>());
        dirty |= ApplyDartBodySettings(root.GetComponent<Rigidbody>());
        if (dirty)
            PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static bool ApplyThrowSettings(XRGrabInteractable grab)
    {
        if (grab == null)
            return false;

        bool dirty = false;
        if (Mathf.Abs(grab.throwVelocityScale) > 0.01f)
        {
            grab.throwVelocityScale = 0f;
            dirty = true;
        }

        if (Mathf.Abs(grab.throwAngularVelocityScale) > 0.01f)
        {
            grab.throwAngularVelocityScale = 0f;
            dirty = true;
        }

        if (grab.throwOnDetach)
        {
            grab.throwOnDetach = false;
            dirty = true;
        }

        if (grab.retainTransformParent)
        {
            grab.retainTransformParent = false;
            dirty = true;
        }

        return dirty;
    }

    private static bool ApplyDartBodySettings(Rigidbody body)
    {
        if (body == null)
            return false;

        bool dirty = false;
        if (Mathf.Abs(body.mass - 0.05f) > 0.001f)
        {
            body.mass = 0.05f;
            dirty = true;
        }

        if (Mathf.Abs(body.linearDamping - 0.1f) > 0.01f)
        {
            body.linearDamping = 0.1f;
            dirty = true;
        }

        if (Mathf.Abs(body.angularDamping - 1f) > 0.01f)
        {
            body.angularDamping = 1f;
            dirty = true;
        }

        return dirty;
    }

    private static PhysicsMaterial LoadOrCreatePhysicsMaterial()
    {
        CreateFolder(Root, "Materials");
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysMatPath);
        if (material == null)
        {
            material = new PhysicsMaterial("M_DartNoBounce");
            AssetDatabase.CreateAsset(material, PhysMatPath);
        }

        material.bounciness = 0f;
        material.bounceCombine = PhysicsMaterialCombine.Minimum;
        material.staticFriction = 0.18f;
        material.dynamicFriction = 0.12f;
        material.frictionCombine = PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void TryAddToSampleScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.Contains("SampleScene"))
            return;

        if (Object.FindFirstObjectByType<DartBoardTarget>() != null)
            return;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(BoardPrefabPath) == null)
            return;

        AddToOpenScene(false);
    }

    private static GameObject AddToOpenScene(bool showDialog)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoardPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("Missing dartboard prefab at " + BoardPrefabPath);
            return null;
        }

        if (Object.FindFirstObjectByType<DartBoardTarget>() != null)
        {
            if (showDialog)
                EditorUtility.DisplayDialog("Dartboard", "This scene already has a dartboard.", "OK");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "PF_DartBoard";
        instance.transform.position = new Vector3(0f, 1.55f, 2.15f);
        instance.transform.rotation = Quaternion.LookRotation(Vector3.back);
        Undo.RegisterCreatedObjectUndo(instance, "Add Dartboard");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return instance;
    }

    private static GameObject LoadDartSource(int style)
    {
        string[] paths =
        {
            "Assets/飞镖模型/Prefabs/DartPin" + style + "B_MidPoly.prefab",
            "Assets/飞镖模型/Prefabs/DartPin" + style + "A_HighPoly.prefab",
            "Assets/飞镖模型/Prefabs/DartPin" + style + "C_LowPoly.prefab"
        };

        for (int i = 0; i < paths.Length; i++)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            if (source != null)
                return source;
        }

        return null;
    }

    private static void FaceVisualAlongZ(GameObject visual)
    {
        Quaternion[] candidates =
        {
            Quaternion.identity,
            Quaternion.Euler(90f, 0f, 0f),
            Quaternion.Euler(-90f, 0f, 0f),
            Quaternion.Euler(0f, 90f, 0f),
            Quaternion.Euler(0f, -90f, 0f)
        };

        Quaternion best = Quaternion.identity;
        float bestArea = -1f;
        for (int i = 0; i < candidates.Length; i++)
        {
            visual.transform.localRotation = candidates[i];
            Bounds bounds = EncapsulateRenderers(visual);
            float area = bounds.size.x * bounds.size.y;
            if (area > bestArea)
            {
                bestArea = area;
                best = candidates[i];
            }
        }

        visual.transform.localRotation = best;
    }

    private static void ScaleBoardDiameter(GameObject visual, float targetDiameter)
    {
        Bounds bounds = EncapsulateRenderers(visual);
        float current = Mathf.Max(bounds.size.x, bounds.size.y);
        if (current < 0.0001f)
            return;

        visual.transform.localScale *= targetDiameter / current;
    }

    private static void ScaleAlongLongestAxis(GameObject go, float targetLength)
    {
        Bounds bounds = EncapsulateRenderers(go);
        float current = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (current < 0.0001f)
            return;

        go.transform.localScale *= targetLength / current;
    }

    private static float BoardRadius(Transform board)
    {
        Bounds world = EncapsulateRenderers(board.gameObject);
        LocalBounds(board, world, out _, out Vector3 localSize);
        return Mathf.Max(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y)) * 0.5f;
    }

    private static float FrontLocalZ(Transform board)
    {
        Transform visual = board.Find("BoardVisual");
        GameObject source = visual != null ? visual.gameObject : board.gameObject;
        Bounds world = EncapsulateRenderers(source);
        Vector3[] corners = Corners(world);
        float maxZ = float.MinValue;
        for (int i = 0; i < corners.Length; i++)
        {
            float z = board.InverseTransformPoint(corners[i]).z;
            if (z > maxZ)
                maxZ = z;
        }

        return maxZ;
    }

    private static Vector3 FindTipLocalOffset(Transform root)
    {
        Transform tip = FindNamed(root, "Tip");
        Renderer[] renderers = tip != null
            ? tip.GetComponentsInChildren<Renderer>()
            : root.GetComponentsInChildren<Renderer>();

        float minZ = float.MaxValue;
        Vector3 best = new Vector3(0f, 0f, -0.06f);
        bool found = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Vector3[] corners = Corners(renderers[i].bounds);
            for (int c = 0; c < corners.Length; c++)
            {
                Vector3 local = root.InverseTransformPoint(corners[c]);
                if (!found || local.z < minZ)
                {
                    minZ = local.z;
                    best = local;
                    found = true;
                }
            }
        }

        return best;
    }

    private static Transform FindNamed(Transform root, string name)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == name)
                return children[i];
        }

        return null;
    }

    private static void LocalBounds(Transform root, Bounds world, out Vector3 localCenter, out Vector3 localSize)
    {
        Vector3[] corners = Corners(world);
        Vector3 min = root.InverseTransformPoint(corners[0]);
        Vector3 max = min;
        for (int i = 1; i < corners.Length; i++)
        {
            Vector3 local = root.InverseTransformPoint(corners[i]);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }

        localCenter = (min + max) * 0.5f;
        localSize = max - min;
    }

    private static Vector3[] Corners(Bounds bounds)
    {
        Vector3 e = bounds.extents;
        Vector3 c = bounds.center;
        return new[]
        {
            c + new Vector3(-e.x, -e.y, -e.z),
            c + new Vector3(-e.x, -e.y, e.z),
            c + new Vector3(-e.x, e.y, -e.z),
            c + new Vector3(-e.x, e.y, e.z),
            c + new Vector3(e.x, -e.y, -e.z),
            c + new Vector3(e.x, -e.y, e.z),
            c + new Vector3(e.x, e.y, -e.z),
            c + new Vector3(e.x, e.y, e.z)
        };
    }

    private static Bounds EncapsulateRenderers(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
        bool found = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !renderers[i].enabled)
                continue;
            if (renderers[i] is ParticleSystemRenderer)
                continue;

            if (!found)
            {
                bounds = renderers[i].bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        if (!found)
            return new Bounds(go.transform.position, Vector3.one * 0.1f);

        return bounds;
    }

    private static GameObject InstantiateSource(GameObject source)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
            instance = Object.Instantiate(source);
        return instance;
    }

    private static void UnpackIfPrefab(GameObject go)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(go))
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    private static void StripColliders(GameObject go)
    {
        Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            Object.DestroyImmediate(colliders[i]);
    }

    private static void ClearStatic(GameObject go)
    {
        Transform[] transforms = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].gameObject.isStatic = false;
            GameObjectUtility.SetStaticEditorFlags(transforms[i].gameObject, 0);
        }
    }

    private static void EnsureFolders()
    {
        CreateFolder("Assets", "PrefabLibrary");
        CreateFolder("Assets/PrefabLibrary", "Darts");
        CreateFolder(Root, "Scripts");
        CreateFolder(Root, "Materials");
        CreateFolder("Assets/PrefabLibrary", "Editor");
    }

    private static void CreateFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
