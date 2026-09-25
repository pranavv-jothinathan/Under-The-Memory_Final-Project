using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Builds the wearable playable guitar prefab.
/// Menu: Prefab Library / Create Playable Guitar
/// </summary>
public static class GuitarPrefabSetup
{
    private const string Root = "Assets/PrefabLibrary/Guitar";
    private const string PrefabPath = Root + "/PF_PlayableGuitar.prefab";
    private const string AudioDir = Root + "/Audio";
    private const string SourceGuitar = "Assets/吉他模型/Prefabs/Guitar_01.prefab";
    private const string SourceGuitarLegacy = "Assets/Classic Acoustic Guitar/Prefabs/Guitar_01.prefab";

    private static readonly string[] SourceClips =
    {
        "Assets/音效/吉他/吉他爵士1.wav",
        "Assets/音效/吉他/吉他爵士2.wav",
        "Assets/音效/吉他/爵士吉他3.mp3"
    };

    private static readonly string[] DestClips =
    {
        AudioDir + "/吉他爵士1.wav",
        AudioDir + "/吉他爵士2.wav",
        AudioDir + "/爵士吉他3.mp3"
    };

    [InitializeOnLoadMethod]
    private static void AutoCreateOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying)
                return;

            if (LoadSourceGuitar() == null)
                return;

            if (!File.Exists(PrefabPath))
            {
                CreatePrefabInternal(false);
                TryAddToSampleScene();
            }
            else
            {
                UpgradeExistingPrefab();
            }
        };
    }

    [MenuItem("Prefab Library/Create Playable Guitar")]
    public static void CreatePrefabMenu()
    {
        CreatePrefabInternal(true);
    }

    [MenuItem("Prefab Library/Add Playable Guitar To Open Scene")]
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
        CopyAudioClips();

        GameObject source = LoadSourceGuitar();
        if (source == null)
        {
            Debug.LogError(
                "Missing guitar model. Expected:\n- " + SourceGuitar + "\n- " + SourceGuitarLegacy);
            return;
        }

        AudioClip[] clips = LoadClips();

        if (File.Exists(PrefabPath))
            AssetDatabase.DeleteAsset(PrefabPath);

        GameObject root = new GameObject("PF_PlayableGuitar");

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
        visual.name = "Guitar_01";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        StripColliders(visual);
        ClearStatic(visual);

        BoxCollider body = CreateBox(
            root.transform,
            "BodyCollider",
            new Vector3(0f, 0.26f, 0.05f),
            new Vector3(0.34f, 0.50f, 0.10f),
            false);

        BoxCollider neck = CreateBox(
            root.transform,
            "NeckZone",
            new Vector3(0f, 0.70f, 0.05f),
            new Vector3(0.055f, 0.10f, 0.055f),
            true);

        BoxCollider strum = CreateBox(
            root.transform,
            "StrumZone",
            new Vector3(0f, 0.34f, 0.14f),
            new Vector3(0.18f, 0.10f, 0.08f),
            true);

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.mass = 1.4f;
        rb.linearDamping = 0.85f;
        rb.angularDamping = 1.1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.centerOfMass = new Vector3(0f, 0.32f, 0.05f);

        XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = true;
        grab.useDynamicAttach = true;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwVelocityScale = 0.65f;
        grab.throwAngularVelocityScale = 0.5f;
        grab.attachEaseInTime = 0f;
        grab.colliders.Clear();
        grab.colliders.Add(body);
        grab.colliders.Add(neck);

        AudioSource audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.loop = true;
        audio.spatialBlend = 1f;
        audio.volume = 0f;
        audio.minDistance = 0.2f;
        audio.maxDistance = 8f;
        audio.rolloffMode = AudioRolloffMode.Linear;

        PlayableGuitar playable = root.AddComponent<PlayableGuitar>();
        GuitarStrumAudio strumAudio = root.AddComponent<GuitarStrumAudio>();

        SerializedObject playableSo = new SerializedObject(playable);
        playableSo.FindProperty("bodyCollider").objectReferenceValue = body;
        playableSo.FindProperty("neckZone").objectReferenceValue = neck;
        playableSo.FindProperty("strumZone").objectReferenceValue = strum;
        playableSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject audioSo = new SerializedObject(strumAudio);
        audioSo.FindProperty("guitar").objectReferenceValue = playable;
        audioSo.FindProperty("audioSource").objectReferenceValue = audio;
        SerializedProperty clipProp = audioSo.FindProperty("clips");
        clipProp.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
            clipProp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        audioSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!showDialog)
            return;

        EditorUtility.DisplayDialog(
            "Playable Guitar",
            "Created " + PrefabPath + "\n\n" +
            "Grab the body or neck, bring it to your chest to wear it.\n" +
            "Right hand swipes across the soundhole to play.\n" +
            "Press B on the right controller to take it off.\n\n" +
            "Also available: Prefab Library / Add Playable Guitar To Open Scene",
            "OK");
    }

    private static void TryAddToSampleScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.Contains("SampleScene"))
            return;

        if (Object.FindFirstObjectByType<PlayableGuitar>() != null)
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
            Debug.LogError("Missing playable guitar prefab at " + PrefabPath);
            return null;
        }

        if (Object.FindFirstObjectByType<PlayableGuitar>() != null)
        {
            if (showDialog)
                EditorUtility.DisplayDialog("Playable Guitar", "This scene already has a playable guitar.", "OK");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "PF_PlayableGuitar";
        instance.transform.position = new Vector3(-0.85f, 0f, 0.45f);
        instance.transform.rotation = Quaternion.identity;
        Undo.RegisterCreatedObjectUndo(instance, "Add Playable Guitar");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return instance;
    }

    private static GameObject LoadSourceGuitar()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceGuitar);
        if (source != null)
            return source;

        source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceGuitarLegacy);
        if (source != null)
            return source;

        string[] guids = AssetDatabase.FindAssets("Guitar_01 t:Prefab");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path) || path.Contains("PF_PlayableGuitar"))
                continue;

            source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source != null)
                return source;
        }

        return null;
    }

    private static void EnsureFolders()
    {
        CreateFolder("Assets", "PrefabLibrary");
        CreateFolder("Assets/PrefabLibrary", "Guitar");
        CreateFolder(Root, "Scripts");
        CreateFolder(Root, "Materials");
        CreateFolder(Root, "Audio");
        CreateFolder(Root, "Meshes");
        CreateFolder("Assets/PrefabLibrary", "Editor");
    }

    private static void CreateFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static void CopyAudioClips()
    {
        for (int i = 0; i < SourceClips.Length; i++)
        {
            if (File.Exists(DestClips[i]))
                continue;
            if (!File.Exists(SourceClips[i]))
            {
                Debug.LogWarning("Missing guitar clip at " + SourceClips[i]);
                continue;
            }

            AssetDatabase.CopyAsset(SourceClips[i], DestClips[i]);
        }
    }

    private static AudioClip[] LoadClips()
    {
        AudioClip[] clips = new AudioClip[DestClips.Length];
        for (int i = 0; i < DestClips.Length; i++)
        {
            clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(DestClips[i]);
            if (clips[i] == null)
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(SourceClips[i]);
        }

        return clips;
    }

    private static void UpgradeExistingPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
            return;

        Transform neckTransform = root.transform.Find("NeckZone");
        BoxCollider neckBox = neckTransform != null ? neckTransform.GetComponent<BoxCollider>() : null;
        Transform strumTransform = root.transform.Find("StrumZone");
        BoxCollider strumBox = strumTransform != null ? strumTransform.GetComponent<BoxCollider>() : null;
        Transform strap = root.transform.Find("Strap");
        bool needsUpgrade = strap != null
            || (neckBox != null && neckBox.size.y > 0.15f)
            || (strumBox != null && strumBox.size.x < 0.15f);

        FitBox(root.transform, "BodyCollider", new Vector3(0f, 0.26f, 0.05f), new Vector3(0.34f, 0.50f, 0.10f), false);
        FitBox(root.transform, "NeckZone", new Vector3(0f, 0.70f, 0.05f), new Vector3(0.055f, 0.10f, 0.055f), true);
        FitBox(root.transform, "StrumZone", new Vector3(0f, 0.34f, 0.14f), new Vector3(0.18f, 0.10f, 0.08f), true);

        if (strap != null)
            Object.DestroyImmediate(strap.gameObject);

        Rigidbody rb = root.GetComponent<Rigidbody>();
        if (rb != null)
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

        Transform bodyTransform = root.transform.Find("BodyCollider");
        XRGrabInteractable grab = root.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.enabled = true;
            grab.colliders.Clear();
            if (bodyTransform != null)
            {
                BoxCollider body = bodyTransform.GetComponent<BoxCollider>();
                if (body != null)
                    grab.colliders.Add(body);
            }

            if (neckTransform != null && neckBox != null)
                grab.colliders.Add(neckBox);
        }

        if (!needsUpgrade)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void FitBox(Transform root, string name, Vector3 center, Vector3 size, bool isTrigger)
    {
        Transform child = root.Find(name);
        if (child == null)
            return;

        BoxCollider box = child.GetComponent<BoxCollider>();
        if (box == null)
            return;

        box.center = center;
        box.size = size;
        box.isTrigger = isTrigger;
    }

    private static BoxCollider CreateBox(Transform parent, string name, Vector3 center, Vector3 size, bool isTrigger)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
        box.isTrigger = isTrigger;
        return box;
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
            transforms[i].gameObject.isStatic = false;
    }
}
