using UnityEngine;

/// <summary>
/// 传单落点的引导标识：一个尖端朝下的箭头 + 一圈贴地光圈，一起明暗脉冲。
/// 被捡起后淡出并销毁。网格在运行时程序化生成，不依赖外部模型资源。
///
/// 用暖橙色而不是青色，是因为海港的天空 / 海面 / 远景本身就是蓝青色系，
/// 同色系的标识不管多不透明都会糊在背景里。
/// 材质默认 ZTest Always，被喷泉或人群挡住时也看得见。
/// </summary>
public class FlyerPickupBeacon : MonoBehaviour
{
    private const string BeaconShaderName = "Scene3/FlyerBeacon";

    [Header("Placement")]
    [Tooltip("箭头悬停在地面上方的高度。")]
    [SerializeField] private float hoverHeight = 2.774f;
    [SerializeField] private float bobAmplitude = 0.12f;
    [SerializeField] private float bobSpeed = 1.6f;
    [SerializeField] private float spinSpeed = 35f;

    [Header("Ground Snap")]
    [Tooltip("传单的投放点比真实地面高，落地后还可能陷进地面，所以锚点要重新找一次地面。")]
    [SerializeField] private bool snapToGround = true;
    [Tooltip("从锚点往上抬多少开始往下打射线。要够高，能覆盖陷进地面的传单。")]
    [SerializeField] private float groundProbeUp = 2.5f;
    [SerializeField] private float groundProbeDown = 8f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Look")]
    [SerializeField] private Material beaconMaterial;
    [Tooltip("暖橙色，跟海港的蓝青背景形成对比。")]
    [SerializeField] private Color tint = new Color(1f, 0.55f, 0.1f, 0.95f);
    [SerializeField] private float arrowHeight = 0.7f;
    [SerializeField] private float arrowRadius = 0.22f;

    [Header("Ground Ring")]
    [SerializeField] private bool showGroundRing = true;
    [SerializeField] private float ringRadius = 0.9f;
    [SerializeField] private float ringWidth = 0.12f;
    [Tooltip("光圈离地高度，防止和地面 z-fighting。")]
    [SerializeField] private float ringLift = 0.02f;

    [Header("Pulse")]
    [Tooltip("每秒脉冲次数。运动比单纯的亮度更能抓住余光注意力。")]
    [SerializeField] private float pulseSpeed = 2f;
    [Range(0f, 1f)]
    [SerializeField] private float pulseMin = 0.55f;
    [Range(0f, 1f)]
    [SerializeField] private float pulseMax = 1f;

    [Header("Fade")]
    [SerializeField] private float fadeOutDuration = 0.6f;

    private Transform arrow;
    private Transform ring;
    private Material runtimeMaterial;
    private Vector3 anchor;
    private float phase;
    private float pulsePhase;
    private bool hiding;
    private float hideTimer;

    private void Awake()
    {
        anchor = transform.position;
        EnsureMaterial();
        BuildArrow();
        BuildRing();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    /// <summary>把标识挂到某个世界位置上并显示。X/Z 用传入值，Y 重新贴到地面。</summary>
    public void ShowAt(Vector3 groundPoint)
    {
        anchor = SnapToGround(groundPoint);
        hiding = false;
        hideTimer = 0f;
        pulsePhase = 0f;
        SetVisible(true);
        ApplyLook(1f);
    }

    public void Hide()
    {
        if (hiding || arrow == null || !arrow.gameObject.activeSelf)
            return;

        hiding = true;
        hideTimer = 0f;
    }

    private void Update()
    {
        if (arrow == null || !arrow.gameObject.activeSelf)
            return;

        phase += Time.deltaTime * bobSpeed;
        float bob = Mathf.Sin(phase) * bobAmplitude;
        arrow.position = anchor + new Vector3(0f, hoverHeight + bob, 0f);
        arrow.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        if (ring != null)
            ring.position = anchor + new Vector3(0f, ringLift, 0f);

        pulsePhase += Time.deltaTime * pulseSpeed * Mathf.PI * 2f;
        float pulse01 = (Mathf.Sin(pulsePhase) + 1f) * 0.5f;
        float pulse = Mathf.Lerp(pulseMin, pulseMax, pulse01);

        if (!hiding)
        {
            ApplyLook(pulse);
            return;
        }

        hideTimer += Time.deltaTime;
        float t = Mathf.Clamp01(hideTimer / Mathf.Max(0.05f, fadeOutDuration));
        ApplyLook(Mathf.Lerp(pulse, 0f, t));

        if (t >= 1f)
        {
            SetVisible(false);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 从锚点上方往下打射线找真正的地面。
    /// 落地的传单自己带刚体会挡住射线，所以跳过所有带刚体的命中，只认静态地面。
    /// </summary>
    private Vector3 SnapToGround(Vector3 point)
    {
        if (!snapToGround)
            return point;

        Vector3 origin = point + Vector3.up * groundProbeUp;
        float distance = groundProbeUp + groundProbeDown;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            distance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        bool found = false;
        float highest = 0f;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].rigidbody != null)
                continue;

            // 往下打，y 最大的就是最先碰到的那个面。
            if (!found || hits[i].point.y > highest)
            {
                highest = hits[i].point.y;
                found = true;
            }
        }

        if (!found)
            return point;

        return new Vector3(point.x, highest, point.z);
    }

    private void SetVisible(bool visible)
    {
        if (arrow != null)
            arrow.gameObject.SetActive(visible);

        if (ring != null)
            ring.gameObject.SetActive(visible && showGroundRing);
    }

    /// <summary>
    /// intensity 同时压 alpha 和亮度，这样脉冲在亮背景和暗背景下都看得出来。
    /// </summary>
    private void ApplyLook(float intensity)
    {
        if (runtimeMaterial == null)
            return;

        Color color = tint * intensity;
        color.a = tint.a * intensity;

        if (runtimeMaterial.HasProperty("_BaseColor"))
            runtimeMaterial.SetColor("_BaseColor", color);

        if (runtimeMaterial.HasProperty("_Color"))
            runtimeMaterial.SetColor("_Color", color);
    }

    private void EnsureMaterial()
    {
        if (beaconMaterial != null)
        {
            runtimeMaterial = new Material(beaconMaterial);
            return;
        }

        // Inspector 没配也要能用，否则渲染器拿不到材质会整个看不见
        Shader shader = Shader.Find(BeaconShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"FlyerPickupBeacon: 找不到 shader {BeaconShaderName}，标识不会显示。", this);
            return;
        }

        runtimeMaterial = new Material(shader);
    }

    private void BuildArrow()
    {
        arrow = CreatePiece("Arrow", CreateDownArrowMesh(arrowHeight, arrowRadius));
    }

    private void BuildRing()
    {
        if (!showGroundRing)
            return;

        ring = CreatePiece("GroundRing", CreateRingMesh(ringRadius, ringWidth));
    }

    private Transform CreatePiece(string pieceName, Mesh mesh)
    {
        var go = new GameObject(pieceName);
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        renderer.sharedMaterial = runtimeMaterial;

        return go.transform;
    }

    /// <summary>生成一个尖端朝下的锥体。</summary>
    private static Mesh CreateDownArrowMesh(float height, float radius)
    {
        const int segments = 12;

        var vertices = new Vector3[segments + 2];
        vertices[0] = new Vector3(0f, -height * 0.5f, 0f);
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, height * 0.5f, Mathf.Sin(angle) * radius);
        }

        vertices[segments + 1] = new Vector3(0f, height * 0.5f, 0f);

        var triangles = new int[segments * 6];
        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            int a = i + 1;
            int b = i + 1 < segments + 1 ? i + 2 : 1;

            triangles[t++] = 0;
            triangles[t++] = b;
            triangles[t++] = a;

            triangles[t++] = segments + 1;
            triangles[t++] = a;
            triangles[t++] = b;
        }

        var mesh = new Mesh { name = "FlyerBeaconArrow" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>生成一个躺在 XZ 平面上的环带。</summary>
    private static Mesh CreateRingMesh(float radius, float width)
    {
        const int segments = 48;

        float inner = Mathf.Max(0.01f, radius - width * 0.5f);
        float outer = radius + width * 0.5f;

        var vertices = new Vector3[segments * 2];
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices[i * 2] = new Vector3(cos * inner, 0f, sin * inner);
            vertices[i * 2 + 1] = new Vector3(cos * outer, 0f, sin * outer);
        }

        var triangles = new int[segments * 6];
        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            int i0 = i * 2;
            int i1 = i * 2 + 1;
            int n0 = next * 2;
            int n1 = next * 2 + 1;

            triangles[t++] = i0;
            triangles[t++] = i1;
            triangles[t++] = n1;

            triangles[t++] = i0;
            triangles[t++] = n1;
            triangles[t++] = n0;
        }

        var mesh = new Mesh { name = "FlyerBeaconRing" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
