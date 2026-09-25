using UnityEngine;

/// <summary>
/// Light paper sag. Vertices droop with gravity; no Cloth.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class SoftPaper : MonoBehaviour
{
    [SerializeField] private float sag = 0.012f;
    [SerializeField] private float flutter = 0.0035f;
    [SerializeField] private float flutterSpeed = 3.2f;

    private MeshFilter filter;
    private Mesh mesh;
    private Vector3[] baseVerts;
    private Vector3[] work;
    private float maxRadius = 0.1f;
    private Vector3 supportLocal;
    private float flutterScale = 1f;

    public void SetSupportLocal(Vector3 localPoint)
    {
        supportLocal = localPoint;
    }

    public void SetFlutterScale(float scale)
    {
        flutterScale = Mathf.Clamp01(scale);
    }

    private void Awake()
    {
        filter = GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return;

        mesh = Instantiate(filter.sharedMesh);
        mesh.name = filter.sharedMesh.name + "_Soft";
        filter.mesh = mesh;
        baseVerts = mesh.vertices;
        work = new Vector3[baseVerts.Length];

        float maxSq = 0f;
        for (int i = 0; i < baseVerts.Length; i++)
        {
            float sq = baseVerts[i].x * baseVerts[i].x + baseVerts[i].y * baseVerts[i].y;
            if (sq > maxSq)
                maxSq = sq;
        }

        maxRadius = Mathf.Max(0.04f, Mathf.Sqrt(maxSq));
    }

    private void LateUpdate()
    {
        if (mesh == null || baseVerts == null)
            return;

        Vector3 downLocal = transform.InverseTransformDirection(Vector3.down);
        float time = Time.time * flutterSpeed;
        float sagAmt = sag;
        float flutterAmt = flutter * flutterScale;

        for (int i = 0; i < baseVerts.Length; i++)
        {
            Vector3 v = baseVerts[i];
            float dx = v.x - supportLocal.x;
            float dy = v.y - supportLocal.y;
            float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / maxRadius);
            float drop = sagAmt * t * t;
            float wave = flutterAmt * t * Mathf.Sin(time + v.x * 18f + v.y * 14f);
            work[i] = v + downLocal * (drop + wave);
        }

        mesh.vertices = work;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
    }

    public static Mesh CreatePaperMesh(float width, float height, int segsX, int segsY, string meshName)
    {
        int vertsX = Mathf.Max(2, segsX + 1);
        int vertsY = Mathf.Max(2, segsY + 1);
        segsX = vertsX - 1;
        segsY = vertsY - 1;

        Vector3[] vertices = new Vector3[vertsX * vertsY];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[segsX * segsY * 6];

        for (int y = 0; y < vertsY; y++)
        {
            float v = y / (float)segsY;
            for (int x = 0; x < vertsX; x++)
            {
                float u = x / (float)segsX;
                int i = y * vertsX + x;
                vertices[i] = new Vector3((u - 0.5f) * width, (v - 0.5f) * height, 0f);
                uv[i] = new Vector2(u, v);
            }
        }

        int t = 0;
        for (int y = 0; y < segsY; y++)
        {
            for (int x = 0; x < segsX; x++)
            {
                int i = y * vertsX + x;
                triangles[t++] = i;
                triangles[t++] = i + vertsX;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + vertsX;
                triangles[t++] = i + 1 + vertsX;
            }
        }

        Mesh created = new Mesh { name = meshName };
        created.vertices = vertices;
        created.uv = uv;
        created.triangles = triangles;
        created.RecalculateNormals();
        created.RecalculateBounds();
        created.RecalculateTangents();
        return created;
    }
}
