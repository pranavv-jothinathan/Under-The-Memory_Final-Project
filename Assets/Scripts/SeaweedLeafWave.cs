using UnityEngine;

public class SeaweedLeafWave : MonoBehaviour
{
    [Header("整体摆动")]
    public float bendAmount = 0.08f;      // 整体弯曲幅度
    public float bendSpeed = 0.8f;        // 整体摆动速度

    [Header("叶片细节波动")]
    public float rippleAmount = 0.025f;   // 叶片小波纹幅度
    public float rippleSpeed = 1.4f;      // 小波纹速度
    public float rippleFrequency = 3.0f;  // 小波纹密度

    [Header("根部固定")]
    public float rootStillness = 0.25f;   // 底部固定范围

    [Header("摆动方向")]
    public bool swayOnX = true;           // true = 左右动；false = 前后动
    public bool invertDirection = false;  // 如果方向反了就勾上

    private Mesh mesh;
    private Vector3[] originalVertices;
    private Vector3[] displacedVertices;

    private float minY;
    private float maxY;
    private float randomOffset;

    void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("SeaweedLeafWave：请把脚本挂到有 Mesh Filter 的海草模型那一层。", this);
            enabled = false;
            return;
        }

        mesh = Instantiate(meshFilter.sharedMesh);
        meshFilter.mesh = mesh;

        originalVertices = mesh.vertices;
        displacedVertices = new Vector3[originalVertices.Length];

        minY = originalVertices[0].y;
        maxY = originalVertices[0].y;

        for (int i = 0; i < originalVertices.Length; i++)
        {
            minY = Mathf.Min(minY, originalVertices[i].y);
            maxY = Mathf.Max(maxY, originalVertices[i].y);
        }

        randomOffset = Random.Range(0f, 100f);
    }

    void Update()
    {
        if (mesh == null) return;

        float height = maxY - minY;
        if (height <= 0.001f) return;

        float mainWave = Mathf.Sin(Time.time * bendSpeed + randomOffset);

        if (invertDirection)
        {
            mainWave *= -1f;
        }

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 v = originalVertices[i];

            float heightPercent = Mathf.InverseLerp(minY, maxY, v.y);

            // 底部不动，顶部动得多
            float mask = Mathf.Clamp01((heightPercent - rootStillness) / (1f - rootStillness));
            mask = Mathf.SmoothStep(0f, 1f, mask);

            // 顶部更柔软，弯曲更多
            float softMask = mask * mask;

            // 整体慢摆：不会一直往一边倒
            float bend = mainWave * bendAmount * softMask;

            // 细小叶片波纹：让它不那么像硬板
            float ripple =
                Mathf.Sin(Time.time * rippleSpeed + randomOffset + v.y * rippleFrequency + v.x * 1.3f)
                * rippleAmount
                * mask;

            float finalOffset = bend + ripple;

            if (swayOnX)
            {
                v.x += finalOffset;
            }
            else
            {
                v.z += finalOffset;
            }

            displacedVertices[i] = v;
        }

        mesh.vertices = displacedVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}