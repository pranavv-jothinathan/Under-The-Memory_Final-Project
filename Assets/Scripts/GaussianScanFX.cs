using UnityEngine;

public class GaussianScanFX : MonoBehaviour
{
    [Header("Scan Source")]
    [Tooltip("Usually the player or the VR camera rig. If empty, this object's transform is used.")]
    public Transform scanCenter;

    [Tooltip("Press this key to trigger one scan.")]
    public KeyCode triggerKey = KeyCode.F;

    [Header("Wave")]
    [Min(0.01f)]
    public float speed = 8f;

    [Min(0.01f)]
    [Tooltip("How long each scanned area stays sparse before recovering.")]
    public float scannedDuration = 1.2f;

    [Min(0.001f)]
    [Tooltip("Width of the bright expanding ring.")]
    public float ringWidth = 0.45f;

    [Min(0.1f)]
    [Tooltip("After this many seconds, the scan turns itself off.")]
    public float maxActiveTime = 8f;

    [Header("Scanned Particle Look")]
    [Range(0.01f, 1f)]
    [Tooltip("Lower = more sparse after being scanned.")]
    public float scannedDensity = 0.18f;

    [Range(0.001f, 1f)]
    [Tooltip("Lower = smaller point-cloud particles.")]
    public float scannedPointScale = 0.08f;

    [Range(0f, 2f)]
    public float scannedAlpha = 0.75f;

    [ColorUsage(true, true)]
    public Color scanTint = new Color(0.25f, 0.85f, 1f, 1f);

    [Range(0f, 5f)]
    public float ringGlow = 1.5f;

    private bool active;
    private float timer;
    private Vector3 centerAtTrigger;

    private static readonly int EnableID = Shader.PropertyToID("_GScan_Enable");
    private static readonly int CenterID = Shader.PropertyToID("_GScan_Center");
    private static readonly int TimeID = Shader.PropertyToID("_GScan_Time");
    private static readonly int SpeedID = Shader.PropertyToID("_GScan_Speed");
    private static readonly int DurationID = Shader.PropertyToID("_GScan_Duration");
    private static readonly int RingWidthID = Shader.PropertyToID("_GScan_RingWidth");
    private static readonly int DensityID = Shader.PropertyToID("_GScan_Density");
    private static readonly int PointScaleID = Shader.PropertyToID("_GScan_PointScale");
    private static readonly int AlphaID = Shader.PropertyToID("_GScan_Alpha");
    private static readonly int TintID = Shader.PropertyToID("_GScan_Tint");
    private static readonly int GlowID = Shader.PropertyToID("_GScan_Glow");

    private void OnEnable()
    {
        DisableScanGlobals();
    }

    private void OnDisable()
    {
        DisableScanGlobals();
    }

    private void Update()
    {
        if (Input.GetKeyDown(triggerKey))
        {
            TriggerScan();
        }

        if (!active)
        {
            return;
        }

        timer += UnityEngine.Time.deltaTime;

        if (timer > maxActiveTime)
        {
            active = false;
            DisableScanGlobals();
            return;
        }

        PushShaderGlobals();
    }

    [ContextMenu("Trigger Scan")]
    public void TriggerScan()
    {
        Transform source = scanCenter != null ? scanCenter : transform;

        centerAtTrigger = source.position;
        timer = 0f;
        active = true;

        PushShaderGlobals();
    }

    private void PushShaderGlobals()
    {
        Shader.SetGlobalFloat(EnableID, active ? 1f : 0f);
        Shader.SetGlobalVector(CenterID, new Vector4(centerAtTrigger.x, centerAtTrigger.y, centerAtTrigger.z, 1f));
        Shader.SetGlobalFloat(TimeID, timer);
        Shader.SetGlobalFloat(SpeedID, Mathf.Max(speed, 0.01f));
        Shader.SetGlobalFloat(DurationID, Mathf.Max(scannedDuration, 0.01f));
        Shader.SetGlobalFloat(RingWidthID, Mathf.Max(ringWidth, 0.001f));
        Shader.SetGlobalFloat(DensityID, Mathf.Clamp01(scannedDensity));
        Shader.SetGlobalFloat(PointScaleID, Mathf.Max(scannedPointScale, 0.001f));
        Shader.SetGlobalFloat(AlphaID, Mathf.Max(scannedAlpha, 0f));
        Shader.SetGlobalVector(TintID, scanTint);
        Shader.SetGlobalFloat(GlowID, Mathf.Max(ringGlow, 0f));
    }

    private void DisableScanGlobals()
    {
        Shader.SetGlobalFloat(EnableID, 0f);
        Shader.SetGlobalFloat(TimeID, 0f);
    }
}