using UnityEngine;

/// <summary>
/// Builds a closed liquid mesh that follows a cup interior profile.
/// Fill amount truncates the lathe so the surface radius matches that height
/// (wide at the rim of a tapered mug, narrow in the stem of a tall glass).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CupLiquidFill : MonoBehaviour
{
    [SerializeField] private float bottomY;
    [SerializeField] private float height = 0.08f;
    [SerializeField] private Vector2 axisXZ;
    [SerializeField] private CupInteriorFitter.RadiusKey[] profile;
    [SerializeField] [Range(0f, 1f)] private float fillAmount = 1f;
    [SerializeField] [Range(0.5f, 1.1f)] private float radiusScale = 1f;
    [SerializeField] private int radialSegments = 24;
    [SerializeField] private int heightSegments = 18;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh liquidMesh;
    private float builtFill = -1f;
    private float displayedFill = 1f;

    public float FillAmount
    {
        get => fillAmount;
        set
        {
            fillAmount = Mathf.Clamp01(value);
            if (!Application.isPlaying)
                displayedFill = fillAmount;
        }
    }

    public bool HasProfile => profile != null && profile.Length >= 2 && height > 0.0001f;

    private void OnEnable()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        displayedFill = fillAmount;
        Rebuild(true);
    }

    private void OnDisable()
    {
        if (liquidMesh != null)
            DestroyMesh(liquidMesh);
        liquidMesh = null;
        builtFill = -1f;
    }

    private void OnValidate()
    {
        fillAmount = Mathf.Clamp01(fillAmount);
        height = Mathf.Max(0.001f, height);
        radialSegments = Mathf.Clamp(radialSegments, 8, 48);
        heightSegments = Mathf.Clamp(heightSegments, 4, 32);
        if (isActiveAndEnabled)
            Rebuild(true);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        displayedFill = Mathf.MoveTowards(displayedFill, fillAmount, Time.deltaTime * 2.2f);
        Rebuild(false);
    }

    public void ApplyFit(CupInteriorFitter.FitResult fit)
    {
        bottomY = fit.bottomY;
        height = fit.height;
        axisXZ = fit.axisXZ;
        profile = fit.profile;
        displayedFill = fillAmount;
        Rebuild(true);
    }

    public bool TryFitFromMesh(Mesh mesh, float inset)
    {
        if (!CupInteriorFitter.TryFit(mesh, 16, inset, out CupInteriorFitter.FitResult fit))
            return false;

        ApplyFit(fit);
        return true;
    }

    [ContextMenu("Fit From Sibling Cup Mesh")]
    private void FitFromSiblingCupMesh()
    {
        if (transform.parent == null)
            return;

        MeshFilter[] filters = transform.parent.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].gameObject == gameObject || filters[i].sharedMesh == null)
                continue;

            TryFitFromMesh(filters[i].sharedMesh, 0.88f);
            return;
        }
    }

    public void Rebuild(bool force)
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        float fill = Application.isPlaying ? displayedFill : fillAmount;
        if (!force && Mathf.Abs(fill - builtFill) < 0.0005f)
            return;

        builtFill = fill;

        if (!HasProfile || fill <= 0.001f)
        {
            if (meshRenderer != null)
                meshRenderer.enabled = false;
            return;
        }

        if (meshRenderer != null)
            meshRenderer.enabled = true;

        if (liquidMesh == null)
        {
            liquidMesh = new Mesh { name = "CupLiquidMesh" };
            liquidMesh.MarkDynamic();
        }

        BuildLathe(liquidMesh, fill);
        meshFilter.sharedMesh = liquidMesh;
    }

    private void BuildLathe(Mesh mesh, float fill)
    {
        int rings = Mathf.Max(2, Mathf.RoundToInt(heightSegments * Mathf.Max(fill, 0.05f)) + 1);
        int segs = radialSegments;
        int sideVerts = rings * (segs + 1);
        int bottomStart = sideVerts;
        int topStart = bottomStart + segs + 1;
        int bottomCenter = topStart + segs + 1;
        int topCenter = bottomCenter + 1;
        int vertCount = topCenter + 1;

        Vector3[] vertices = new Vector3[vertCount];
        Vector3[] normals = new Vector3[vertCount];
        Vector2[] uv = new Vector2[vertCount];

        for (int y = 0; y < rings; y++)
        {
            float t = fill * (y / (float)(rings - 1));
            float radius = SampleRadius(t) * radiusScale;
            float worldY = bottomY + height * t;

            for (int x = 0; x <= segs; x++)
            {
                float u = x / (float)segs;
                float angle = u * Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                int index = y * (segs + 1) + x;
                vertices[index] = new Vector3(axisXZ.x, worldY, axisXZ.y) + radial * radius;
                normals[index] = radial;
                uv[index] = new Vector2(u, t);
            }
        }

        float bottomRadius = SampleRadius(0f) * radiusScale;
        float topRadius = SampleRadius(fill) * radiusScale;
        float topY = bottomY + height * fill;
        Vector3 center = new Vector3(axisXZ.x, 0f, axisXZ.y);

        for (int x = 0; x <= segs; x++)
        {
            float u = x / (float)segs;
            float angle = u * Mathf.PI * 2f;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            vertices[bottomStart + x] = center + Vector3.up * bottomY + radial * bottomRadius;
            normals[bottomStart + x] = Vector3.down;
            uv[bottomStart + x] = new Vector2(u, 0f);

            vertices[topStart + x] = center + Vector3.up * topY + radial * topRadius;
            normals[topStart + x] = Vector3.up;
            uv[topStart + x] = new Vector2(u, 1f);
        }

        vertices[bottomCenter] = center + Vector3.up * bottomY;
        normals[bottomCenter] = Vector3.down;
        uv[bottomCenter] = new Vector2(0.5f, 0.5f);

        vertices[topCenter] = center + Vector3.up * topY;
        normals[topCenter] = Vector3.up;
        uv[topCenter] = new Vector2(0.5f, 0.5f);

        int sideTriCount = (rings - 1) * segs * 6;
        int capTriCount = segs * 3 * 2;
        int[] triangles = new int[sideTriCount + capTriCount];
        int tIndex = 0;

        for (int y = 0; y < rings - 1; y++)
        {
            for (int x = 0; x < segs; x++)
            {
                int a = y * (segs + 1) + x;
                int b = a + segs + 1;
                triangles[tIndex++] = a;
                triangles[tIndex++] = b;
                triangles[tIndex++] = a + 1;
                triangles[tIndex++] = a + 1;
                triangles[tIndex++] = b;
                triangles[tIndex++] = b + 1;
            }
        }

        for (int x = 0; x < segs; x++)
        {
            triangles[tIndex++] = bottomCenter;
            triangles[tIndex++] = bottomStart + x + 1;
            triangles[tIndex++] = bottomStart + x;

            triangles[tIndex++] = topCenter;
            triangles[tIndex++] = topStart + x;
            triangles[tIndex++] = topStart + x + 1;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private float SampleRadius(float height01)
    {
        height01 = Mathf.Clamp01(height01);
        if (profile == null || profile.Length == 0)
            return 0.03f;

        if (profile.Length == 1)
            return profile[0].radius;

        if (height01 <= profile[0].height01)
            return profile[0].radius;

        for (int i = 1; i < profile.Length; i++)
        {
            if (height01 > profile[i].height01)
                continue;

            float a = profile[i - 1].height01;
            float b = profile[i].height01;
            float u = Mathf.Approximately(a, b) ? 1f : Mathf.InverseLerp(a, b, height01);
            return Mathf.Lerp(profile[i - 1].radius, profile[i].radius, u);
        }

        return profile[profile.Length - 1].radius;
    }

    private static void DestroyMesh(Mesh mesh)
    {
        if (Application.isPlaying)
            Destroy(mesh);
        else
            DestroyImmediate(mesh);
    }
}
