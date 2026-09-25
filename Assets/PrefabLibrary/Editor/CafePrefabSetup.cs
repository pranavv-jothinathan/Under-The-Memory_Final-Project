using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Builds drinkable cups and the cake-on-plate prefab.
/// Menu: Prefab Library / Create Cafe Prefabs
/// </summary>
public static class CafePrefabSetup
{
    private const string Root = "Assets/PrefabLibrary/Cafe";
    private const string CoffeeDir = Root + "/Coffee";
    private const string CakeDir = Root + "/Cake";
    private const string MatDir = Root + "/Materials";

    private const string CoffeePrefabPath = CoffeeDir + "/PF_DrinkableCoffee.prefab";
    private const string GlassShortPath = CoffeeDir + "/PF_DrinkableGlassShort.prefab";
    private const string GlassTallPath = CoffeeDir + "/PF_DrinkableGlassTall.prefab";
    private const string CakePrefabPath = CakeDir + "/PF_CakeOnPlate.prefab";
    private const string CoffeeMatPath = MatDir + "/M_CoffeeLiquid.mat";
    private const string CupOpaqueMatPath = MatDir + "/M_CoffeeCupOpaque.mat";
    private const string CrumbMatPath = MatDir + "/M_CakeCrumbs.mat";

    private const string SourceCoffeeCup = "Assets/音效/咖啡杯.prefab";
    private const string SourceGlassShort = "Assets/音效/玻璃杯（矮）.prefab";
    private const string SourceGlassTall = "Assets/音效/玻璃杯（高）.prefab";
    private const string SourcePlate = "Assets/Great Cakes/Cake Prefabs/Props/Plate.prefab";
    private const string SourceCake = "Assets/Great Cakes/Cake Prefabs/Regular Cakes/Chocolate_Cut.prefab";
    private const string DrinkClipPath = "Assets/音效/喝水声_爱给网_aigei_com.mp3";
    private const string ChewClipPath = "Assets/音效/烘焙糕点的咀嚼声-环境音-饮食_爱给网_aigei_com.mp3";

    [InitializeOnLoadMethod]
    private static void AutoCreateOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SourceCoffeeCup) == null)
                return;

            if (!File.Exists(CoffeePrefabPath) || !File.Exists(CakePrefabPath))
            {
                CreatePrefabsInternal(false);
                return;
            }

            if (!PlateHasGrab(CakePrefabPath))
                EnsurePlateIsGrabbable();
        };
    }

    [MenuItem("Prefab Library/Create Cafe Prefabs")]
    public static void CreatePrefabs()
    {
        CreatePrefabsInternal(true);
    }

    private static void CreatePrefabsInternal(bool showDialog)
    {
        EnsureFolders();

        Material coffeeLiquid = CreateLiquidMaterial(CoffeeMatPath, new Color(0.23f, 0.11f, 0.04f, 0.92f));
        Material cupOpaque = CreateOpaqueMaterial(CupOpaqueMatPath, new Color(0.93f, 0.93f, 0.91f, 1f));
        Material crumbs = CreateOpaqueMaterial(CrumbMatPath, new Color(0.45f, 0.28f, 0.14f, 1f));
        AudioClip drink = AssetDatabase.LoadAssetAtPath<AudioClip>(DrinkClipPath);
        AudioClip chew = AssetDatabase.LoadAssetAtPath<AudioClip>(ChewClipPath);

        CreateDrinkPrefab(
            SourceCoffeeCup,
            CoffeePrefabPath,
            "PF_DrinkableCoffee",
            coffeeLiquid,
            cupOpaque,
            drink,
            0.86f,
            0.08f);

        CreateDrinkPrefab(
            SourceGlassShort,
            GlassShortPath,
            "PF_DrinkableGlassShort",
            coffeeLiquid,
            null,
            drink,
            0.90f,
            0.07f);

        CreateDrinkPrefab(
            SourceGlassTall,
            GlassTallPath,
            "PF_DrinkableGlassTall",
            coffeeLiquid,
            null,
            drink,
            0.90f,
            0.12f);

        CreateCakePrefab(crumbs, chew);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!showDialog)
            return;

        EditorUtility.DisplayDialog(
            "Cafe Prefabs",
            "Created:\n" +
            "- " + CoffeePrefabPath + "\n" +
            "- " + GlassShortPath + "\n" +
            "- " + GlassTallPath + "\n" +
            "- " + CakePrefabPath + "\n\n" +
            "Drag into a scene with XR Origin.\n" +
            "Without a headset: Play, click-drag a cup/slice to the camera. Right-click while dragging a cup to tilt and sip.",
            "OK");
    }

    private static void EnsureFolders()
    {
        CreateFolder("Assets", "PrefabLibrary");
        CreateFolder("Assets/PrefabLibrary", "Cafe");
        CreateFolder(Root, "Coffee");
        CreateFolder(Root, "Cake");
        CreateFolder(Root, "Materials");
        CreateFolder(Root, "Scripts");
        CreateFolder("Assets/PrefabLibrary", "Editor");
    }

    private static void CreateFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static Material CreateLiquidMaterial(string path, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

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

        ApplyTransparent(material, color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOpaqueMaterial(string path, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

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

        material.SetColor("_BaseColor", color);
        material.color = color;
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_ZWrite", 1f);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 2000;
        material.SetFloat("_Smoothness", 0.35f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ApplyTransparent(Material material, Color color)
    {
        material.SetColor("_BaseColor", color);
        material.color = color;
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = 3000;
        material.SetFloat("_Smoothness", 0.85f);
        material.SetFloat("_Metallic", 0.05f);
    }

    private static void CreateDrinkPrefab(
        string sourcePath,
        string savePath,
        string name,
        Material liquidMat,
        Material cupOverride,
        AudioClip drinkClip,
        float inset,
        float rimHeight)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null)
        {
            Debug.LogError("Missing cup source: " + sourcePath);
            return;
        }

        if (File.Exists(savePath))
            AssetDatabase.DeleteAsset(savePath);

        GameObject root = new GameObject(name);
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        visual.name = source.name;
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        ClearStatic(visual);

        foreach (MeshCollider meshCollider in visual.GetComponentsInChildren<MeshCollider>(true))
            Object.DestroyImmediate(meshCollider);

        if (cupOverride != null)
        {
            MeshRenderer[] renderers = visual.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = cupOverride;
        }

        Bounds bounds = EncapsulateRenderers(root);
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.center = root.transform.InverseTransformPoint(bounds.center);
        box.size = Vector3.Max(bounds.size, new Vector3(0.04f, 0.04f, 0.04f));

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 0.2f;
        body.linearDamping = 0.8f;
        body.angularDamping = 0.9f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.isKinematic = true;
        body.useGravity = false;

        XRGrabInteractable grab = root.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = true;
        grab.useDynamicAttach = true;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;

        AudioSource audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.clip = drinkClip;
        audio.minDistance = 0.15f;
        audio.maxDistance = 4f;

        GameObject fillGo = new GameObject("CoffeeFill");
        fillGo.transform.SetParent(visual.transform, false);
        fillGo.AddComponent<MeshFilter>();
        MeshRenderer fillRenderer = fillGo.AddComponent<MeshRenderer>();
        fillRenderer.sharedMaterial = liquidMat;
        fillRenderer.shadowCastingMode = ShadowCastingMode.Off;

        CupLiquidFill fill = fillGo.AddComponent<CupLiquidFill>();
        Mesh cupMesh = visual.GetComponentInChildren<MeshFilter>()?.sharedMesh;
        bool fitted = cupMesh != null && fill.TryFitFromMesh(cupMesh, inset);
        if (!fitted)
        {
            Bounds meshBounds = cupMesh != null
                ? cupMesh.bounds
                : new Bounds(new Vector3(0f, 0.04f, 0f), new Vector3(0.08f, 0.08f, 0.08f));
            float outer = Mathf.Min(meshBounds.extents.x, meshBounds.extents.z);
            fill.ApplyFit(new CupInteriorFitter.FitResult
            {
                bottomY = meshBounds.min.y + meshBounds.size.y * 0.08f,
                height = meshBounds.size.y * 0.75f,
                axisXZ = new Vector2(meshBounds.center.x, meshBounds.center.z),
                profile = new[]
                {
                    new CupInteriorFitter.RadiusKey(0f, outer * 0.58f * inset),
                    new CupInteriorFitter.RadiusKey(1f, outer * 0.78f * inset)
                }
            });
            Debug.LogWarning("Used fallback liquid profile for " + name + ". Tweak CoffeeFill radiusScale if it clips.");
        }

        fill.FillAmount = 1f;
        fill.Rebuild(true);

        DrinkableCup drinkable = root.AddComponent<DrinkableCup>();
        SerializedObject so = new SerializedObject(drinkable);
        so.FindProperty("liquid").objectReferenceValue = fill;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.FindProperty("drinkClip").objectReferenceValue = drinkClip;
        so.FindProperty("rimHeight").floatValue = rimHeight;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);
    }

    private static void CreateCakePrefab(Material crumbMat, AudioClip chewClip)
    {
        GameObject plateSource = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePlate);
        GameObject cakeSource = AssetDatabase.LoadAssetAtPath<GameObject>(SourceCake);
        if (plateSource == null || cakeSource == null)
        {
            Debug.LogError("Missing plate or chocolate cut cake prefab.");
            return;
        }

        if (File.Exists(CakePrefabPath))
            AssetDatabase.DeleteAsset(CakePrefabPath);

        GameObject root = new GameObject("PF_CakeOnPlate");

        GameObject plate = (GameObject)PrefabUtility.InstantiatePrefab(plateSource);
        PrefabUtility.UnpackPrefabInstance(plate, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        plate.name = "Plate";
        plate.transform.SetParent(root.transform, false);
        plate.transform.localPosition = Vector3.zero;
        plate.transform.localRotation = Quaternion.identity;
        ScaleToDiameter(plate, 0.22f);

        GameObject cake = (GameObject)PrefabUtility.InstantiatePrefab(cakeSource);
        PrefabUtility.UnpackPrefabInstance(cake, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        cake.name = "Chocolate_Cut";
        cake.transform.SetParent(root.transform, false);
        cake.transform.localRotation = Quaternion.identity;
        cake.transform.localPosition = Vector3.zero;
        ScaleToDiameter(cake, 0.18f);

        PromoteRootMeshToSlice(cake);
        SeatOnPlate(plate, cake);
        cake.transform.SetParent(plate.transform, true);
        SetupGrabbablePlate(plate);

        CakePlateAligner aligner = root.AddComponent<CakePlateAligner>();
        SerializedObject alignerSo = new SerializedObject(aligner);
        alignerSo.FindProperty("plate").objectReferenceValue = plate.transform;
        alignerSo.FindProperty("cake").objectReferenceValue = cake.transform;
        alignerSo.ApplyModifiedPropertiesWithoutUndo();

        foreach (MeshFilter filter in cake.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            if (filter.gameObject == cake)
                continue;

            SetupSlice(filter.gameObject, chewClip, crumbMat);
        }

        PrefabUtility.SaveAsPrefabAsset(root, CakePrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void PromoteRootMeshToSlice(GameObject cake)
    {
        MeshFilter meshFilter = cake.GetComponent<MeshFilter>();
        MeshRenderer meshRenderer = cake.GetComponent<MeshRenderer>();
        if (meshFilter == null || meshFilter.sharedMesh == null || cake.transform.childCount == 0)
            return;

        GameObject slice = new GameObject("Slice_0");
        slice.transform.SetParent(cake.transform, false);
        slice.transform.SetAsFirstSibling();
        slice.AddComponent<MeshFilter>().sharedMesh = meshFilter.sharedMesh;
        slice.AddComponent<MeshRenderer>().sharedMaterials = meshRenderer.sharedMaterials;
        Object.DestroyImmediate(meshFilter);
        Object.DestroyImmediate(meshRenderer);

        MeshCollider rootCollider = cake.GetComponent<MeshCollider>();
        if (rootCollider != null)
            Object.DestroyImmediate(rootCollider);
    }

    private static void SetupSlice(GameObject slice, AudioClip chewClip, Material crumbMat)
    {
        foreach (MeshCollider old in slice.GetComponents<MeshCollider>())
            Object.DestroyImmediate(old);

        MeshFilter filter = slice.GetComponent<MeshFilter>();
        MeshCollider meshCollider = slice.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = filter.sharedMesh;
        meshCollider.convex = true;

        Rigidbody body = slice.AddComponent<Rigidbody>();
        body.mass = 0.12f;
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        XRGrabInteractable grab = slice.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = true;
        grab.useDynamicAttach = true;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;

        AudioSource audio = slice.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 1f;
        audio.minDistance = 0.1f;
        audio.maxDistance = 3f;

        ParticleSystem crumbs = CreateCrumbBurst(slice.transform, crumbMat);

        EatableSlice eatable = slice.AddComponent<EatableSlice>();
        SerializedObject so = new SerializedObject(eatable);
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.FindProperty("chewClip").objectReferenceValue = chewClip;
        so.FindProperty("crumbs").objectReferenceValue = crumbs;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ParticleSystem CreateCrumbBurst(Transform parent, Material crumbMat)
    {
        GameObject go = new GameObject("Crumbs");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * 0.04f;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = 0.7f;
        main.startSpeed = 0.35f;
        main.startSize = 0.012f;
        main.startColor = new Color(0.48f, 0.3f, 0.16f, 1f);
        main.gravityModifier = 1.4f;
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.035f;

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
            particleShader = Shader.Find("Sprites/Default");
        if (particleShader != null)
        {
            Material particleMat = new Material(particleShader);
            particleMat.color = new Color(0.48f, 0.3f, 0.16f, 1f);
            renderer.sharedMaterial = particleMat;
        }
        else
        {
            renderer.sharedMaterial = crumbMat;
        }

        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        return ps;
    }

    private static bool PlateHasGrab(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return false;

        Transform plate = prefab.transform.Find("Plate");
        return plate != null && plate.GetComponent<XRGrabInteractable>() != null;
    }

    private static void EnsurePlateIsGrabbable()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CakePrefabPath);
        try
        {
            Transform plate = root.transform.Find("Plate");
            Transform cake = root.transform.Find("Chocolate_Cut");
            if (cake == null && plate != null)
                cake = plate.Find("Chocolate_Cut");

            if (plate == null)
                return;

            if (cake != null && cake.parent != plate)
                cake.SetParent(plate, true);

            SetupGrabbablePlate(plate.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, CakePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetupGrabbablePlate(GameObject plate)
    {
        ClearStatic(plate);

        foreach (MeshCollider meshCollider in plate.GetComponents<MeshCollider>())
            Object.DestroyImmediate(meshCollider);

        FitBoxCollider(plate, 0.03f);

        Rigidbody body = plate.GetComponent<Rigidbody>();
        if (body == null)
            body = plate.AddComponent<Rigidbody>();
        body.mass = 0.35f;
        body.linearDamping = 0.8f;
        body.angularDamping = 0.95f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.isKinematic = true;
        body.useGravity = false;

        BoxCollider box = plate.GetComponent<BoxCollider>();
        XRGrabInteractable grab = plate.GetComponent<XRGrabInteractable>();
        if (grab == null)
            grab = plate.AddComponent<XRGrabInteractable>();
        grab.throwOnDetach = true;
        grab.useDynamicAttach = true;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwVelocityScale = 0.8f;
        grab.colliders.Clear();
        if (box != null)
            grab.colliders.Add(box);

        if (plate.GetComponent<GrabbableProp>() == null)
            plate.AddComponent<GrabbableProp>();
    }

    private static void FitBoxCollider(GameObject go, float minWorldHeight)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        Bounds world = renderer != null
            ? renderer.bounds
            : new Bounds(go.transform.position, new Vector3(0.22f, minWorldHeight, 0.22f));

        Vector3 worldSize = world.size;
        worldSize.y = Mathf.Max(worldSize.y, minWorldHeight);

        Vector3 lossy = go.transform.lossyScale;
        BoxCollider box = go.GetComponent<BoxCollider>();
        if (box == null)
            box = go.AddComponent<BoxCollider>();

        box.size = new Vector3(
            worldSize.x / Mathf.Max(Mathf.Abs(lossy.x), 0.0001f),
            worldSize.y / Mathf.Max(Mathf.Abs(lossy.y), 0.0001f),
            worldSize.z / Mathf.Max(Mathf.Abs(lossy.z), 0.0001f));
        box.center = go.transform.InverseTransformPoint(world.center);
        box.isTrigger = false;
    }

    private static void ScaleToDiameter(GameObject go, float targetDiameter)
    {
        Bounds bounds = EncapsulateRenderers(go);
        float current = Mathf.Max(bounds.size.x, bounds.size.z);
        if (current < 0.0001f)
            return;

        go.transform.localScale *= targetDiameter / current;
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

    private static void SeatOnPlate(GameObject plate, GameObject cake)
    {
        Bounds plateBounds = EncapsulateRenderers(plate);
        Bounds cakeBounds = EncapsulateRenderers(cake);
        float delta = plateBounds.max.y - cakeBounds.min.y;
        cake.transform.position += Vector3.up * (delta + 0.001f);
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

    [MenuItem("Prefab Library/Add Cafe Test To Open Scene")]
    public static void AddCafeTestToOpenScene()
    {
        if (!File.Exists(CoffeePrefabPath) || !File.Exists(CakePrefabPath))
            CreatePrefabsInternal(false);

        if (Object.FindFirstObjectByType<XRInteractionManager>() == null)
        {
            GameObject manager = new GameObject("XR Interaction Manager");
            manager.AddComponent<XRInteractionManager>();
            Undo.RegisterCreatedObjectUndo(manager, "Create XR Interaction Manager");
        }

        Place(CoffeePrefabPath, new Vector3(-0.25f, 0.95f, 0.6f));
        Place(GlassShortPath, new Vector3(0f, 0.95f, 0.6f));
        Place(GlassTallPath, new Vector3(0.25f, 0.95f, 0.6f));
        Place(CakePrefabPath, new Vector3(0f, 0.9f, 0.95f));
    }

    private static void Place(string path, Vector3 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            return;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = position;
        Undo.RegisterCreatedObjectUndo(instance, "Create Cafe Prefab");
    }
}
