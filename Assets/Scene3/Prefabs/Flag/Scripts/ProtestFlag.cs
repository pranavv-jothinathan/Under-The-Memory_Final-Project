using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Handheld protest flag. Grab the pole; Cloth on the attached sheet reacts to waving.
/// Stays kinematic until first grab so Scene dragging is not fighting gravity.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ProtestFlag : MonoBehaviour
{
    public const float PoleHeight = 1.8f;
    public const float PoleRadius = 0.018f;
    public const float FlagWidth = 1.2f;
    public const float FlagHeight = 0.8f;
    public const int SegsX = 12;
    public const int SegsY = 8;

    [SerializeField] private SkinnedMeshRenderer flagRenderer;
    [SerializeField] private Cloth flagCloth;
    [SerializeField] private CapsuleCollider poleCollider;
    [SerializeField] private Vector3 idleWind = new Vector3(1.8f, 0.05f, 0.25f);

    private XRGrabInteractable grab;
    private Rigidbody body;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        if (flagRenderer == null)
            flagRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (flagCloth == null && flagRenderer != null)
            flagCloth = flagRenderer.GetComponent<Cloth>();
        if (poleCollider == null)
            poleCollider = GetComponent<CapsuleCollider>();

        GrabPhysics.SleepUntilGrabbed(body);
        body.detectCollisions = true;
        body.centerOfMass = new Vector3(0.08f, PoleHeight * 0.45f, 0f);

        if (grab != null)
        {
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = true;
            grab.attachEaseInTime = 0f;
            if (poleCollider != null)
            {
                poleCollider.radius = 0.04f;
                grab.colliders.Clear();
                grab.colliders.Add(poleCollider);
            }

            grab.enabled = true;
        }

        ConfigureCloth();
    }

    private IEnumerator Start()
    {
        for (int i = 0; i < 8; i++)
        {
            ConfigureCloth();
            if (HasPinnedCloth())
                yield break;
            yield return null;
        }
    }

    private bool HasPinnedCloth()
    {
        if (flagCloth == null || flagRenderer == null || flagRenderer.sharedMesh == null)
            return false;

        ClothSkinningCoefficient[] coefficients = flagCloth.coefficients;
        return coefficients != null && coefficients.Length == flagRenderer.sharedMesh.vertexCount;
    }

    private void OnEnable()
    {
        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();
        if (poleCollider == null)
            poleCollider = GetComponent<CapsuleCollider>();
        if (grab != null && poleCollider != null)
        {
            grab.colliders.Clear();
            grab.colliders.Add(poleCollider);
            grab.enabled = true;
        }

        if (grab == null)
            return;

        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        if (grab == null)
            return;

        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (body != null)
            body.detectCollisions = false;

        if (flagCloth != null)
        {
            flagCloth.enabled = true;
            flagCloth.capsuleColliders = System.Array.Empty<CapsuleCollider>();
            flagCloth.externalAcceleration = idleWind;
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        StartCoroutine(EnableGravityAfterDetach());
    }

    private IEnumerator EnableGravityAfterDetach()
    {
        yield return null;
        if (body != null)
            body.detectCollisions = true;
        GrabPhysics.ActivateAfterDrop(body);
    }

    public void ConfigureCloth()
    {
        if (flagRenderer == null)
            return;

        if (flagRenderer.sharedMesh == null)
            flagRenderer.sharedMesh = CreateClothMesh(FlagWidth, FlagHeight, SegsX, SegsY, PoleRadius, PoleHeight - FlagHeight - 0.04f);

        if (flagCloth == null)
            flagCloth = flagRenderer.GetComponent<Cloth>();
        if (flagCloth == null)
            flagCloth = flagRenderer.gameObject.AddComponent<Cloth>();

        ApplyClothSettings(flagCloth, flagRenderer.sharedMesh, poleCollider, idleWind);
    }

    public static Mesh CreateClothMesh(float width, float height, int segsX, int segsY, float attachX, float bottomY)
    {
        int vertsX = segsX + 1;
        int vertsY = segsY + 1;
        Vector3[] vertices = new Vector3[vertsX * vertsY];
        Vector2[] uv = new Vector2[vertices.Length];
        BoneWeight[] weights = new BoneWeight[vertices.Length];
        int[] triangles = new int[segsX * segsY * 6];

        for (int y = 0; y < vertsY; y++)
        {
            float v = y / (float)segsY;
            for (int x = 0; x < vertsX; x++)
            {
                float u = x / (float)segsX;
                int i = y * vertsX + x;
                vertices[i] = new Vector3(attachX + u * width, bottomY + v * height, 0f);
                uv[i] = new Vector2(u, v);
                weights[i].boneIndex0 = 0;
                weights[i].weight0 = 1f;
            }
        }

        int t = 0;
        for (int y = 0; y < segsY; y++)
        {
            for (int x = 0; x < segsX; x++)
            {
                int i = y * vertsX + x;
                triangles[t++] = i;
                triangles[t++] = i + 1;
                triangles[t++] = i + vertsX;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1 + vertsX;
                triangles[t++] = i + vertsX;
            }
        }

        Mesh mesh = new Mesh { name = "FlagCloth" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.boneWeights = weights;
        mesh.bindposes = new[] { Matrix4x4.identity };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    public static void ApplyClothSettings(Cloth cloth, Mesh mesh, CapsuleCollider pole, Vector3 wind)
    {
        if (cloth == null || mesh == null)
            return;

        cloth.useGravity = true;
        cloth.useTethers = true;
        cloth.stretchingStiffness = 0.92f;
        cloth.bendingStiffness = 0.5f;
        cloth.damping = 0.35f;
        cloth.friction = 0.2f;
        cloth.worldVelocityScale = 0.35f;
        cloth.worldAccelerationScale = 0.4f;
        cloth.enableContinuousCollision = false;
        cloth.externalAcceleration = wind;
        cloth.randomAcceleration = new Vector3(0.12f, 0.03f, 0.12f);
        cloth.capsuleColliders = System.Array.Empty<CapsuleCollider>();
        cloth.sphereColliders = System.Array.Empty<ClothSphereColliderPair>();

        ClothSkinningCoefficient[] coefficients = cloth.coefficients;
        Vector3[] vertices = mesh.vertices;
        if (coefficients == null || coefficients.Length != vertices.Length)
            return;

        float pinX = vertices[0].x;
        for (int i = 0; i < coefficients.Length; i++)
        {
            bool pinned = Mathf.Abs(vertices[i].x - pinX) < 0.001f;
            coefficients[i].maxDistance = pinned ? 0f : float.MaxValue;
            coefficients[i].collisionSphereDistance = 0f;
        }

        cloth.coefficients = coefficients;
    }

    public static void PrepareSkinnedRenderer(SkinnedMeshRenderer renderer, Mesh mesh, Transform bone, Material material)
    {
        renderer.sharedMesh = mesh;
        renderer.rootBone = bone;
        renderer.bones = new[] { bone };
        renderer.sharedMaterial = material;
        renderer.updateWhenOffscreen = true;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.localBounds = new Bounds(mesh.bounds.center, mesh.bounds.size * 2.2f);
        renderer.quality = SkinQuality.Bone1;
    }
}
