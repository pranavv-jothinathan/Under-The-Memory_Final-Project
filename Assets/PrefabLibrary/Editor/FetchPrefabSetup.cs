using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Builds fetch dog / ball prefabs after XR packages have imported.
/// Menu: Prefab Library / Create Fetch Dog Prefabs
/// </summary>
public static class FetchPrefabSetup
{
    private const string Root = "Assets/PrefabLibrary/Dog";
    private const string DogPrefabPath = Root + "/Dog/PF_FetchDog.prefab";
    private const string BallPrefabPath = Root + "/Ball/PF_FetchBall.prefab";
    private const string ControllerPath = Root + "/Animations/AC_FetchDog.controller";
    private const string BallMaterialPath = Root + "/Ball/M_FetchBall.mat";
    private const string SourceDogPrefab = "Assets/RSG_DogsPack/HDRP/Prefabs/P_GermanShepherd.prefab";
    private const string IdleFbx = "Assets/RSG_DogsPack/HDRP/Animations/A_Idle_Playing.fbx";
    private const string RunFbx = "Assets/RSG_DogsPack/HDRP/Animations/A_Run.fbx";

    [InitializeOnLoadMethod]
    private static void AutoCreateOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(DogPrefabPath) && File.Exists(BallPrefabPath))
                return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(SourceDogPrefab) == null)
                return;

            CreatePrefabsInternal(false);
        };
    }

    [MenuItem("Prefab Library/Create Fetch Dog Prefabs")]
    public static void CreatePrefabs()
    {
        CreatePrefabsInternal(true);
    }

    private static void CreatePrefabsInternal(bool showDialog)
    {
        EnsureFolders();

        AnimatorController controller = CreateAnimator();
        Material ballMaterial = CreateBallMaterial();
        CreateBallPrefab(ballMaterial);
        CreateDogPrefab(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!showDialog)
            return;

        EditorUtility.DisplayDialog(
            "Fetch Dog Prefabs",
            "Created:\n" +
            "- " + DogPrefabPath + "\n" +
            "- " + BallPrefabPath + "\n\n" +
            "Drag both into a scene with a ground collider.\n" +
            "Add XR Origin (XR Rig) from XRI Starter Assets to test with a headset.\n" +
            "Without a headset: Play, then click-drag the ball in the Game view and throw it.",
            "OK"
        );
    }

    private static void EnsureFolders()
    {
        CreateFolder("Assets", "PrefabLibrary");
        CreateFolder("Assets/PrefabLibrary", "Dog");
        CreateFolder(Root, "Dog");
        CreateFolder(Root, "Ball");
        CreateFolder(Root, "Animations");
        CreateFolder(Root, "Scripts");
        CreateFolder("Assets/PrefabLibrary", "Editor");
    }

    private static void CreateFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static AnimatorController CreateAnimator()
    {
        if (File.Exists(ControllerPath))
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimationClip idle = LoadFirstClip(IdleFbx);
        AnimationClip run = LoadFirstClip(RunFbx);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle", new Vector3(300f, 80f, 0f));
        AnimatorState runState = machine.AddState("Run", new Vector3(300f, 200f, 0f));

        idleState.motion = idle;
        runState.motion = run;
        machine.defaultState = idleState;

        AnimatorStateTransition toRun = idleState.AddTransition(runState);
        toRun.hasExitTime = false;
        toRun.duration = 0.15f;
        toRun.AddCondition(AnimatorConditionMode.If, 0f, "IsRunning");

        AnimatorStateTransition toIdle = runState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.15f;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsRunning");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimationClip LoadFirstClip(string fbxPath)
    {
        return AssetDatabase
            .LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));
    }

    private static Material CreateBallMaterial()
    {
        if (File.Exists(BallMaterialPath))
            AssetDatabase.DeleteAsset(BallMaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = new Color(0.85f, 0.18f, 0.12f);
        AssetDatabase.CreateAsset(material, BallMaterialPath);
        return material;
    }

    private static void CreateBallPrefab(Material material)
    {
        if (File.Exists(BallPrefabPath))
            AssetDatabase.DeleteAsset(BallPrefabPath);
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "PF_FetchBall";
        ball.transform.localScale = Vector3.one * 0.12f;

        SphereCollider collider = ball.GetComponent<SphereCollider>();
        collider.radius = 0.5f;

        Rigidbody body = ball.AddComponent<Rigidbody>();
        body.mass = 0.25f;
        body.linearDamping = 0.4f;
        body.angularDamping = 0.4f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.isKinematic = true;
        body.useGravity = false;

        MeshRenderer renderer = ball.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        XRGrabInteractable grab = ball.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = true;
        grab.useDynamicAttach = true;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;

        ball.AddComponent<FetchBall>();

        PrefabUtility.SaveAsPrefabAsset(ball, BallPrefabPath);
        Object.DestroyImmediate(ball);
    }

    private static void CreateDogPrefab(AnimatorController controller)
    {
        if (File.Exists(DogPrefabPath))
            AssetDatabase.DeleteAsset(DogPrefabPath);
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceDogPrefab);
        if (source == null)
        {
            Debug.LogError("Could not find " + SourceDogPrefab);
            return;
        }

        GameObject root = new GameObject("PF_FetchDog");

        CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 0.4f, 0.1f);
        capsule.radius = 0.28f;
        capsule.height = 0.9f;
        capsule.direction = 1;

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
        visual.name = "P_GermanShepherd";
        visual.transform.SetParent(root.transform, false);

        Animator animator = visual.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = visual.AddComponent<Animator>();

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        FetchDog fetch = root.AddComponent<FetchDog>();

        Transform mouth = CreateMouthAnchor(root.transform);
        SerializedObject so = new SerializedObject(fetch);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("fetchController").objectReferenceValue = controller;
        so.FindProperty("mouthAnchor").objectReferenceValue = mouth;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, DogPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static Transform CreateMouthAnchor(Transform root)
    {
        Transform head = null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            string n = child.name.ToLowerInvariant();
            if (n.Contains("jaw") || n.Contains("mouth") || n.Contains("head"))
            {
                head = child;
                if (n.Contains("jaw") || n.Contains("mouth"))
                    break;
            }
        }

        Transform parent = head != null ? head : root;
        GameObject anchor = new GameObject("MouthAnchor");
        anchor.transform.SetParent(parent, false);
        anchor.transform.localPosition = head != null
            ? new Vector3(0f, 0.02f, 0.18f)
            : new Vector3(0f, 0.55f, 0.55f);
        return anchor.transform;
    }

    [MenuItem("Prefab Library/Add Fetch Test To Open Scene")]
    public static void AddFetchTestToOpenScene()
    {
        if (!File.Exists(DogPrefabPath) || !File.Exists(BallPrefabPath))
            CreatePrefabsInternal(false);

        GameObject dogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DogPrefabPath);
        GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);

        if (dogPrefab == null || ballPrefab == null)
        {
            Debug.LogError("Fetch prefabs were not created. Wait for XR packages to import, then use Prefab Library/Create Fetch Dog Prefabs.");
            return;
        }

        if (Object.FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>() == null)
        {
            GameObject manager = new GameObject("XR Interaction Manager");
            manager.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            Undo.RegisterCreatedObjectUndo(manager, "Create XR Interaction Manager");
        }

        GameObject dog = (GameObject)PrefabUtility.InstantiatePrefab(dogPrefab);
        dog.transform.position = new Vector3(0f, 0f, 2f);
        Undo.RegisterCreatedObjectUndo(dog, "Create Fetch Dog");

        GameObject ball = (GameObject)PrefabUtility.InstantiatePrefab(ballPrefab);
        ball.transform.position = new Vector3(0.4f, 0.2f, 1.2f);
        Undo.RegisterCreatedObjectUndo(ball, "Create Fetch Ball");

        FetchDog fetch = dog.GetComponent<FetchDog>();
        if (fetch != null)
        {
            SerializedObject so = new SerializedObject(fetch);
            Camera cam = Camera.main;
            if (cam != null)
                so.FindProperty("returnTarget").objectReferenceValue = cam.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Selection.objects = new Object[] { dog, ball };
    }
}
