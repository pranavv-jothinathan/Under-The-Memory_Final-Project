using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Builds the eco airship flyover, two flyers, and the folding pamphlet.
/// Menu: Prefab Library / Create Eco Airship
/// </summary>
public static class AirshipPrefabSetup
{
    private const string Root = "Assets/PrefabLibrary/Airship";
    private const string ShaderName = "PrefabLibrary/PaperUnlitDoubleSided";
    private const string SourceAirship = "Assets/飞艇模型/Models/Prefabs/V1_Airship_1_Standard.prefab";
    private const string SourceAudio = "Assets/飞艇模型/飞艇螺旋桨发动机_爱给网_aigei_com.mp3";
    private const string DestAudio = Root + "/Audio/AirshipEngine.mp3";

    private const string AirshipPrefabPath = Root + "/PF_EcoAirship.prefab";
    private const string ProtestPrefabPath = Root + "/PF_FlyerProtest.prefab";
    private const string BristolPrefabPath = Root + "/PF_FlyerBristol.prefab";
    private const string PamphletPrefabPath = Root + "/PF_HarborPamphlet.prefab";

    private const string FlyerMeshPath = Root + "/Meshes/PaperFlyer.asset";
    private const string PamphletMeshPath = Root + "/Meshes/PaperPamphlet.asset";
    private const string ParticleMeshPath = Root + "/Meshes/PaperParticle.asset";
    private const string PhysMatPath = Root + "/Materials/M_Paper.asset";

    private static readonly string[] TextureNames =
    {
        "T_FlyerProtest.png",
        "T_FlyerBristol.png",
        "T_Pamphlet1_Cover.png",
        "T_Pamphlet2_Why.png",
        "T_Pamphlet3_Change.png",
        "T_Pamphlet4_Everyday.png",
        "T_Pamphlet5_Action.png",
        "T_Pamphlet6_Scan.png"
    };

    [InitializeOnLoadMethod]
    private static void AutoCreateOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying)
                return;

            string protestPath = Root + "/Textures/T_FlyerProtest.png";
            if (!File.Exists(protestPath))
                return;

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(protestPath) == null)
                AssetDatabase.ImportAsset(protestPath);

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(protestPath) == null)
                return;

            if (!File.Exists(AirshipPrefabPath) || !File.Exists(PamphletPrefabPath) || NeedsTwoSidedPaperUpgrade())
            {
                CreatePrefabsInternal(false);
                TryAddToSampleScene();
            }
        };
    }

    [MenuItem("Prefab Library/Create Eco Airship")]
    public static void CreatePrefabMenu()
    {
        CreatePrefabsInternal(true);
    }

    [MenuItem("Prefab Library/Add Eco Airship To Open Scene")]
    public static void AddToOpenSceneMenu()
    {
        if (!File.Exists(AirshipPrefabPath))
            CreatePrefabsInternal(false);

        GameObject instance = AddToOpenScene(true);
        if (instance != null)
            Selection.activeGameObject = instance;
    }

    private static void CreatePrefabsInternal(bool showDialog)
    {
        EnsureFolders();
        CopyAudio();
        ImportTextures();

        Texture2D protestTex = LoadTex("T_FlyerProtest.png");
        Texture2D bristolTex = LoadTex("T_FlyerBristol.png");
        Texture2D[] pamphletTex =
        {
            LoadTex("T_Pamphlet1_Cover.png"),
            LoadTex("T_Pamphlet2_Why.png"),
            LoadTex("T_Pamphlet3_Change.png"),
            LoadTex("T_Pamphlet4_Everyday.png"),
            LoadTex("T_Pamphlet5_Action.png"),
            LoadTex("T_Pamphlet6_Scan.png")
        };

        if (protestTex == null || bristolTex == null)
        {
            Debug.LogError("Airship paper textures are missing in " + Root + "/Textures");
            return;
        }

        for (int i = 0; i < pamphletTex.Length; i++)
        {
            if (pamphletTex[i] == null)
            {
                Debug.LogError("Missing pamphlet texture index " + i);
                return;
            }
        }

        try
        {
            PunchBorderAlpha(Root + "/Textures/T_FlyerProtest.png");
            protestTex = LoadTex("T_FlyerProtest.png");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("Protest flyer alpha punch skipped: " + ex.Message);
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        Material protestMat = CreatePaperMaterial(Root + "/Materials/M_FlyerProtest.mat", shader, protestTex, protestTex, 0.12f);
        Material bristolMat = CreatePaperMaterial(Root + "/Materials/M_FlyerBristol.mat", shader, bristolTex, bristolTex, 0.02f);
        Material leftMat = CreatePaperMaterial(Root + "/Materials/M_PamphletLeft.mat", shader, pamphletTex[1], pamphletTex[0], 0.02f);
        Material centerMat = CreatePaperMaterial(Root + "/Materials/M_PamphletCenter.mat", shader, pamphletTex[2], pamphletTex[5], 0.02f);
        Material rightMat = CreatePaperMaterial(Root + "/Materials/M_PamphletRight.mat", shader, pamphletTex[3], pamphletTex[4], 0.02f);
        Material coverParticleMat = CreatePaperMaterial(Root + "/Materials/M_Pamphlet1.mat", shader, pamphletTex[0], pamphletTex[0], 0.02f);

        PhysicsMaterial phys = CreatePhysicsMaterial();
        Mesh flyerMesh = SaveMesh(FlyerMeshPath, SoftPaper.CreatePaperMesh(FlyerSheet.Width, FlyerSheet.Height, 6, 8, "PaperFlyer"));
        Mesh pamphletMesh = SaveMesh(PamphletMeshPath, SoftPaper.CreatePaperMesh(FoldingPamphlet.PanelWidth, FoldingPamphlet.PanelHeight, 4, 6, "PaperPamphlet"));
        Mesh particleMesh = SaveMesh(ParticleMeshPath, SoftPaper.CreatePaperMesh(0.16f, 0.22f, 1, 1, "PaperParticle"));

        CreateFlyerPrefab(ProtestPrefabPath, "PF_FlyerProtest", flyerMesh, protestMat, phys);
        CreateFlyerPrefab(BristolPrefabPath, "PF_FlyerBristol", flyerMesh, bristolMat, phys);
        CreatePamphletPrefab(pamphletMesh, leftMat, centerMat, rightMat, phys);
        CreateAirshipPrefab(particleMesh, protestMat, bristolMat, coverParticleMat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!showDialog)
            return;

        EditorUtility.DisplayDialog(
            "Eco Airship",
            "Created airship, two flyers, and the folding pamphlet.\n\n" +
            "Play: the airship enters after 5 seconds, rains particle papers, " +
            "and drops 5 grabbable sheets near you.\n" +
            "Hold the pamphlet and pull the cover with the other Grip. " +
            "Press O in Play Mode to cycle fold poses.\n\n" +
            "Also available: Prefab Library / Add Eco Airship To Open Scene",
            "OK");
    }

    private static void CreateFlyerPrefab(
        string path,
        string name,
        Mesh mesh,
        Material paperMat,
        PhysicsMaterial phys)
    {
        GameObject root = new GameObject(name);
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(FlyerSheet.Width, FlyerSheet.Height, 0.004f);
        box.material = phys;

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 0.018f;
        body.linearDamping = 2.1f;
        body.angularDamping = 3.2f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.isKinematic = true;
        body.useGravity = false;

        XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = true;
        grab.useDynamicAttach = true;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwVelocityScale = 0.55f;
        grab.throwAngularVelocityScale = 0.7f;
        grab.attachEaseInTime = 0f;
        grab.colliders.Clear();
        grab.colliders.Add(box);

        root.AddComponent<FlyerSheet>();
        CreatePaperSheet(root.transform, "Paper", mesh, paperMat);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void CreatePamphletPrefab(
        Mesh mesh,
        Material leftMat,
        Material centerMat,
        Material rightMat,
        PhysicsMaterial phys)
    {
        GameObject root = new GameObject("PF_HarborPamphlet");
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(FoldingPamphlet.PanelWidth + 0.01f, FoldingPamphlet.PanelHeight + 0.01f, 0.008f);
        box.material = phys;

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 0.03f;
        body.linearDamping = 1.8f;
        body.angularDamping = 2.8f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.isKinematic = true;
        body.useGravity = false;

        XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = true;
        grab.useDynamicAttach = true;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwVelocityScale = 0.5f;
        grab.throwAngularVelocityScale = 0.55f;
        grab.attachEaseInTime = 0f;
        grab.colliders.Clear();
        grab.colliders.Add(box);

        float w = FoldingPamphlet.PanelWidth;
        GameObject center = new GameObject("Center");
        center.transform.SetParent(root.transform, false);
        CreatePaperSheet(center.transform, "Paper", mesh, centerMat);

        GameObject leftHinge = new GameObject("LeftHinge");
        leftHinge.transform.SetParent(root.transform, false);
        leftHinge.transform.localPosition = new Vector3(-w * 0.5f, 0f, 0.0012f);
        GameObject left = new GameObject("Left");
        left.transform.SetParent(leftHinge.transform, false);
        left.transform.localPosition = new Vector3(-w * 0.5f, 0f, 0f);
        CreatePaperSheet(left.transform, "Paper", mesh, leftMat);

        GameObject rightHinge = new GameObject("RightHinge");
        rightHinge.transform.SetParent(root.transform, false);
        rightHinge.transform.localPosition = new Vector3(w * 0.5f, 0f, 0.0006f);
        GameObject right = new GameObject("Right");
        right.transform.SetParent(rightHinge.transform, false);
        right.transform.localPosition = new Vector3(w * 0.5f, 0f, 0f);
        CreatePaperSheet(right.transform, "Paper", mesh, rightMat);

        FoldingPamphlet pamphlet = root.AddComponent<FoldingPamphlet>();
        SerializedObject so = new SerializedObject(pamphlet);
        so.FindProperty("leftHinge").objectReferenceValue = leftHinge.transform;
        so.FindProperty("rightHinge").objectReferenceValue = rightHinge.transform;
        so.FindProperty("leftPanel").objectReferenceValue = left.transform;
        so.FindProperty("rightPanel").objectReferenceValue = right.transform;
        so.FindProperty("bodyCollider").objectReferenceValue = box;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PamphletPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateAirshipPrefab(
        Mesh particleMesh,
        Material protestMat,
        Material bristolMat,
        Material pamphletMat)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceAirship);
        if (source == null)
        {
            Debug.LogError("Missing airship model at " + SourceAirship);
            return;
        }

        GameObject root = new GameObject("PF_EcoAirship");
        GameObject path = new GameObject("Path");
        path.transform.SetParent(root.transform, false);

        Vector3[] points =
        {
            new Vector3(-16f, 8.4f, 5.2f),
            new Vector3(-4f, 8.0f, 3.2f),
            new Vector3(4.5f, 8.1f, 2.4f),
            new Vector3(17f, 8.6f, 4.6f)
        };

        Transform[] waypoints = new Transform[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            GameObject p = new GameObject("P" + i);
            p.transform.SetParent(path.transform, false);
            p.transform.localPosition = points[i];
            waypoints[i] = p.transform;
        }

        GameObject ship = new GameObject("Ship");
        ship.transform.SetParent(root.transform, false);
        ship.transform.position = points[0];

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
        visual.name = "V1_Airship_1_Standard";
        visual.transform.SetParent(ship.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * 0.15f;
        StripColliders(visual);
        ClearStatic(visual);

        GameObject drop = new GameObject("DropPoint");
        drop.transform.SetParent(ship.transform, false);
        drop.transform.localPosition = new Vector3(0f, -0.32f, 0.08f);

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(DestAudio);
        if (clip == null)
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SourceAudio);

        AudioSource audio = ship.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.loop = true;
        audio.spatialBlend = 1f;
        audio.clip = clip;
        audio.volume = 0.55f;
        audio.minDistance = 4f;
        audio.maxDistance = 38f;
        audio.rolloffMode = AudioRolloffMode.Linear;
        audio.dopplerLevel = 0.15f;

        ParticleSystem rainProtest = CreateRain(drop.transform, "RainProtest", particleMesh, protestMat, 0.16f, 7f, 40);
        ParticleSystem rainBristol = CreateRain(drop.transform, "RainBristol", particleMesh, bristolMat, 0.16f, 6f, 40);
        ParticleSystem rainPamphlet = CreateRain(drop.transform, "RainPamphlet", particleMesh, pamphletMat, 0.09f, 2f, 16);

        List<Transform> propellers = new List<Transform>();
        Transform[] children = visual.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == "Propeller_L" || children[i].name == "Propeller_R")
                propellers.Add(children[i]);
        }

        GameObject protestGo = AssetDatabase.LoadAssetAtPath<GameObject>(ProtestPrefabPath);
        GameObject bristolGo = AssetDatabase.LoadAssetAtPath<GameObject>(BristolPrefabPath);
        GameObject pamphletGo = AssetDatabase.LoadAssetAtPath<GameObject>(PamphletPrefabPath);
        FlyerSheet protestPrefab = protestGo != null ? protestGo.GetComponent<FlyerSheet>() : null;
        FlyerSheet bristolPrefab = bristolGo != null ? bristolGo.GetComponent<FlyerSheet>() : null;
        FoldingPamphlet pamphletPrefab = pamphletGo != null ? pamphletGo.GetComponent<FoldingPamphlet>() : null;

        AirshipFlyover flyover = root.AddComponent<AirshipFlyover>();
        SerializedObject so = new SerializedObject(flyover);
        so.FindProperty("playOnStart").boolValue = true;
        so.FindProperty("delayBeforeEnter").floatValue = 5f;
        so.FindProperty("flightDuration").floatValue = 16f;
        so.FindProperty("visual").objectReferenceValue = ship.transform;
        SerializedProperty wp = so.FindProperty("waypoints");
        wp.arraySize = waypoints.Length;
        for (int i = 0; i < waypoints.Length; i++)
            wp.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
        so.FindProperty("engineAudio").objectReferenceValue = audio;
        SerializedProperty props = so.FindProperty("propellers");
        props.arraySize = propellers.Count;
        for (int i = 0; i < propellers.Count; i++)
            props.GetArrayElementAtIndex(i).objectReferenceValue = propellers[i];
        SerializedProperty rain = so.FindProperty("rain");
        rain.arraySize = 3;
        rain.GetArrayElementAtIndex(0).objectReferenceValue = rainProtest;
        rain.GetArrayElementAtIndex(1).objectReferenceValue = rainBristol;
        rain.GetArrayElementAtIndex(2).objectReferenceValue = rainPamphlet;
        so.FindProperty("dropPoint").objectReferenceValue = drop.transform;
        so.FindProperty("protestPrefab").objectReferenceValue = protestPrefab;
        so.FindProperty("bristolPrefab").objectReferenceValue = bristolPrefab;
        so.FindProperty("pamphletPrefab").objectReferenceValue = pamphletPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, AirshipPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static ParticleSystem CreateRain(
        Transform parent,
        string name,
        Mesh mesh,
        Material material,
        float size,
        float rate,
        int max)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5.5f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.85f, size * 1.12f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.3f;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.7f, 0.1f, 0.45f);

        ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-90f, 90f);
        rot.y = new ParticleSystem.MinMaxCurve(-140f, 140f);
        rot.z = new ParticleSystem.MinMaxCurve(-90f, 90f);

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.32f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.18f;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = mesh;
        renderer.sharedMaterial = material;
        renderer.alignment = ParticleSystemRenderSpace.Local;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    private static GameObject CreatePaperSheet(Transform parent, string name, Mesh mesh, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        go.AddComponent<SoftPaper>();
        return go;
    }

    private static bool NeedsTwoSidedPaperUpgrade()
    {
        GameObject flyer = AssetDatabase.LoadAssetAtPath<GameObject>(ProtestPrefabPath);
        if (flyer == null)
            return true;

        return flyer.transform.Find("Back") != null || flyer.transform.Find("Paper") == null;
    }

    private static void TryAddToSampleScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.Contains("SampleScene"))
            return;

        if (Object.FindFirstObjectByType<AirshipFlyover>() != null)
            return;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(AirshipPrefabPath) == null)
            return;

        AddToOpenScene(false);
    }

    private static GameObject AddToOpenScene(bool showDialog)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AirshipPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("Missing eco airship prefab at " + AirshipPrefabPath);
            return null;
        }

        if (Object.FindFirstObjectByType<AirshipFlyover>() != null)
        {
            if (showDialog)
                EditorUtility.DisplayDialog("Eco Airship", "This scene already has an eco airship.", "OK");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "PF_EcoAirship";
        instance.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(instance, "Add Eco Airship");
        AddPaperSamples();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        return instance;
    }

    private static void AddPaperSamples()
    {
        if (GameObject.Find("AirshipPaperSamples") != null)
            return;

        GameObject group = new GameObject("AirshipPaperSamples");
        Undo.RegisterCreatedObjectUndo(group, "Add paper samples");
        PlaceSample(ProtestPrefabPath, group.transform, new Vector3(0.30f, 1.05f, 0.55f), new Vector3(0f, 180f, 8f));
        PlaceSample(BristolPrefabPath, group.transform, new Vector3(0.50f, 1.05f, 0.55f), new Vector3(0f, 180f, -4f));
        PlaceSample(PamphletPrefabPath, group.transform, new Vector3(0.68f, 1.05f, 0.55f), new Vector3(0f, 175f, 0f));
    }

    private static void PlaceSample(string path, Transform parent, Vector3 pos, Vector3 euler)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            return;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(parent, true);
        instance.transform.position = pos;
        instance.transform.rotation = Quaternion.Euler(euler);
    }

    private static void EnsureFolders()
    {
        CreateFolder("Assets", "PrefabLibrary");
        CreateFolder("Assets/PrefabLibrary", "Airship");
        CreateFolder(Root, "Textures");
        CreateFolder(Root, "Materials");
        CreateFolder(Root, "Meshes");
        CreateFolder(Root, "Scripts");
        CreateFolder(Root, "Shaders");
        CreateFolder(Root, "Audio");
        CreateFolder("Assets/PrefabLibrary", "Editor");
    }

    private static void CreateFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static void CopyAudio()
    {
        if (File.Exists(DestAudio) || !File.Exists(SourceAudio))
            return;

        AssetDatabase.CopyAsset(SourceAudio, DestAudio);
    }

    private static Texture2D LoadTex(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + fileName);
    }

    private static void ImportTextures()
    {
        for (int i = 0; i < TextureNames.Length; i++)
        {
            string path = Root + "/Textures/" + TextureNames[i];
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = TextureNames[i].Contains("Bristol") ? 2048 : 1024;
            importer.SaveAndReimport();
        }
    }

    private static void PunchBorderAlpha(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.isReadable = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (tex == null)
            return;

        Color32[] pixels = tex.GetPixels32();
        int w = tex.width;
        int h = tex.height;
        if (pixels.Length != w * h)
            return;

        bool[] visited = new bool[pixels.Length];
        Queue<int> queue = new Queue<int>();
        TryEnqueueCorner(queue, visited, pixels, 0);
        TryEnqueueCorner(queue, visited, pixels, w - 1);
        TryEnqueueCorner(queue, visited, pixels, (h - 1) * w);
        TryEnqueueCorner(queue, visited, pixels, (h - 1) * w + w - 1);

        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            Color32 c = pixels[i];
            c.a = 0;
            pixels[i] = c;

            int x = i % w;
            int y = i / w;
            for (int k = 0; k < 4; k++)
            {
                int nx = x;
                int ny = y;
                if (k == 0) nx++;
                else if (k == 1) nx--;
                else if (k == 2) ny++;
                else ny--;
                if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                    continue;

                int ni = ny * w + nx;
                if (visited[ni] || !IsBorderInk(pixels[ni]))
                    continue;

                visited[ni] = true;
                queue.Enqueue(ni);
            }
        }

        tex.SetPixels32(pixels);
        File.WriteAllBytes(Path.Combine(Application.dataPath, "PrefabLibrary/Airship/Textures/T_FlyerProtest.png"), tex.EncodeToPNG());
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    private static void TryEnqueueCorner(Queue<int> queue, bool[] visited, Color32[] pixels, int i)
    {
        if (i < 0 || i >= pixels.Length || visited[i] || !IsBorderInk(pixels[i]))
            return;

        visited[i] = true;
        queue.Enqueue(i);
    }

    private static bool IsBorderInk(Color32 c)
    {
        float lum = (c.r * 0.22f + c.g * 0.72f + c.b * 0.06f) / 255f;
        return lum < 0.14f && c.a > 8;
    }

    private static Material CreatePaperMaterial(
        string path,
        Shader shader,
        Texture2D front,
        Texture2D back,
        float cutoff)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        material.SetTexture("_FrontMap", front);
        material.SetTexture("_BackMap", back);
        material.SetTexture("_BaseMap", front);
        material.SetTexture("_MainTex", front);
        material.mainTexture = front;
        material.SetColor("_BaseColor", Color.white);
        material.color = Color.white;
        material.SetFloat("_Cutoff", cutoff);
        material.SetFloat("_Cull", 0f);
        material.doubleSidedGI = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static PhysicsMaterial CreatePhysicsMaterial()
    {
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysMatPath);
        if (material == null)
        {
            material = new PhysicsMaterial("M_Paper");
            AssetDatabase.CreateAsset(material, PhysMatPath);
        }

        material.bounciness = 0.02f;
        material.bounceCombine = PhysicsMaterialCombine.Minimum;
        material.staticFriction = 0.62f;
        material.dynamicFriction = 0.5f;
        material.frictionCombine = PhysicsMaterialCombine.Average;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh SaveMesh(string path, Mesh mesh)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
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
