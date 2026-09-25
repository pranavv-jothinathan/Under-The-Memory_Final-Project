using UnityEngine;

public class GaussianScanFX2 : MonoBehaviour
{
    private enum ScanState
    {
        Normal = 0,
        ScanningToSparse = 1,
        Sparse = 2,
        RestoringToNormal = 3
    }

    [Header("Scan Source")]
    [Tooltip("Usually player / XR Origin / Main Camera. If empty, this object's transform is used.")]
    public Transform scanCenter;

    [Header("Input")]
    [Tooltip("Press once: normal -> sparse. Press again: sparse -> normal.")]
    public KeyCode toggleKey = KeyCode.F;

    [Header("Wave")]
    [Min(0.01f)]
    public float speed = 10f;

    [Min(0.1f)]
    [Tooltip("Must be large enough to cover the whole scene you want affected.")]
    public float maxRadius = 30f;

    [Header("Sparse Point Cloud Look")]
    [Range(0.01f, 1f)]
    [Tooltip("Lower = more sparse.")]
    public float sparseDensity = 0.12f;

    [Min(0.25f)]
    [Tooltip("Small point dot size in pixels.")]
    public float dotPixelSize = 1.3f;

    [Range(0f, 1f)]
    public float dotAlpha = 0.95f;

    [Min(0.1f)]
    public float colorIntensity = 1.0f;

    [Header("Height Color Mapping")]
    [Tooltip("This world Y height will appear green.")]
    public float baseHeight = 0f;

    [Min(0.001f)]
    [Tooltip("How far below baseHeight the color transitions towards cyan/blue.")]
    public float belowRange = 3f;

    [Min(0.001f)]
    [Tooltip("How far above baseHeight the color transitions towards yellow/orange/red.")]
    public float aboveRange = 3f;

    [Header("Auto")]
    public bool playScanOnStart = false;

    [Header("Debug")]
    public bool printStateChanges = false;

    private ScanState state = ScanState.Normal;

    private Vector3 sparseScanCenter;
    private float sparseScanRadius;

    private Vector3 restoreScanCenter;
    private float restoreScanRadius;

    private static readonly int HasSparseFieldID = Shader.PropertyToID("_GScan2_HasSparseField");
    private static readonly int ScanCenterID = Shader.PropertyToID("_GScan2_ScanCenter");
    private static readonly int ScanRadiusID = Shader.PropertyToID("_GScan2_ScanRadius");
    private static readonly int RestoreCenterID = Shader.PropertyToID("_GScan2_RestoreCenter");
    private static readonly int RestoreRadiusID = Shader.PropertyToID("_GScan2_RestoreRadius");
    private static readonly int RestoreActiveID = Shader.PropertyToID("_GScan2_RestoreActive");
    private static readonly int SparseDensityID = Shader.PropertyToID("_GScan2_SparseDensity");
    private static readonly int DotPixelSizeID = Shader.PropertyToID("_GScan2_DotPixelSize");
    private static readonly int DotAlphaID = Shader.PropertyToID("_GScan2_DotAlpha");
    private static readonly int BaseHeightID = Shader.PropertyToID("_GScan2_BaseHeight");
    private static readonly int BelowRangeID = Shader.PropertyToID("_GScan2_BelowRange");
    private static readonly int AboveRangeID = Shader.PropertyToID("_GScan2_AboveRange");
    private static readonly int ColorIntensityID = Shader.PropertyToID("_GScan2_ColorIntensity");

    private void OnEnable()
    {
        ResetShaderGlobals();

        if (playScanOnStart)
        {
            BeginSparseScan();
        }
        else
        {
            PushShaderGlobals();
        }
    }

    private void OnDisable()
    {
        ResetShaderGlobals();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleScan();
        }

        float dt = Time.deltaTime;

        switch (state)
        {
            case ScanState.ScanningToSparse:
                {
                    sparseScanRadius += speed * dt;
                    if (sparseScanRadius >= maxRadius)
                    {
                        sparseScanRadius = maxRadius;
                        state = ScanState.Sparse;
                        LogState("Reached Sparse state.");
                    }
                    break;
                }

            case ScanState.RestoringToNormal:
                {
                    restoreScanRadius += speed * dt;
                    if (restoreScanRadius >= maxRadius)
                    {
                        restoreScanRadius = maxRadius;

                        state = ScanState.Normal;

                        sparseScanRadius = 0f;
                        restoreScanRadius = 0f;

                        LogState("Restored to Normal state.");
                    }
                    break;
                }
        }

        PushShaderGlobals();
    }

    [ContextMenu("Toggle Scan")]
    public void ToggleScan()
    {
        if (state == ScanState.ScanningToSparse || state == ScanState.RestoringToNormal)
        {
            // Ignore while a scan is still propagating
            return;
        }

        if (state == ScanState.Normal)
        {
            BeginSparseScan();
        }
        else if (state == ScanState.Sparse)
        {
            BeginRestoreScan();
        }
    }

    [ContextMenu("Begin Sparse Scan")]
    public void BeginSparseScan()
    {
        Transform src = scanCenter != null ? scanCenter : transform;

        sparseScanCenter = src.position;
        sparseScanRadius = 0f;

        restoreScanCenter = Vector3.zero;
        restoreScanRadius = 0f;

        state = ScanState.ScanningToSparse;
        LogState("Begin first scan: Normal -> Sparse.");
        PushShaderGlobals();
    }

    [ContextMenu("Begin Restore Scan")]
    public void BeginRestoreScan()
    {
        Transform src = scanCenter != null ? scanCenter : transform;

        restoreScanCenter = src.position;
        restoreScanRadius = 0f;

        state = ScanState.RestoringToNormal;
        LogState("Begin second scan: Sparse -> Normal.");
        PushShaderGlobals();
    }

    [ContextMenu("Force Normal")]
    public void ForceNormal()
    {
        state = ScanState.Normal;
        sparseScanRadius = 0f;
        restoreScanRadius = 0f;
        PushShaderGlobals();
    }

    [ContextMenu("Force Sparse")]
    public void ForceSparse()
    {
        Transform src = scanCenter != null ? scanCenter : transform;

        sparseScanCenter = src.position;
        sparseScanRadius = maxRadius;
        restoreScanRadius = 0f;
        state = ScanState.Sparse;
        PushShaderGlobals();
    }

    private void PushShaderGlobals()
    {
        bool hasSparseField =
            state == ScanState.ScanningToSparse ||
            state == ScanState.Sparse ||
            state == ScanState.RestoringToNormal;

        bool restoreActive = state == ScanState.RestoringToNormal;

        Shader.SetGlobalFloat(HasSparseFieldID, hasSparseField ? 1f : 0f);

        Shader.SetGlobalVector(ScanCenterID, new Vector4(
            sparseScanCenter.x, sparseScanCenter.y, sparseScanCenter.z, 1f));
        Shader.SetGlobalFloat(ScanRadiusID, Mathf.Max(0f, sparseScanRadius));

        Shader.SetGlobalVector(RestoreCenterID, new Vector4(
            restoreScanCenter.x, restoreScanCenter.y, restoreScanCenter.z, 1f));
        Shader.SetGlobalFloat(RestoreRadiusID, Mathf.Max(0f, restoreScanRadius));
        Shader.SetGlobalFloat(RestoreActiveID, restoreActive ? 1f : 0f);

        Shader.SetGlobalFloat(SparseDensityID, Mathf.Clamp01(sparseDensity));
        Shader.SetGlobalFloat(DotPixelSizeID, Mathf.Max(0.25f, dotPixelSize));
        Shader.SetGlobalFloat(DotAlphaID, Mathf.Clamp01(dotAlpha));
        Shader.SetGlobalFloat(ColorIntensityID, Mathf.Max(0.1f, colorIntensity));

        Shader.SetGlobalFloat(BaseHeightID, baseHeight);
        Shader.SetGlobalFloat(BelowRangeID, Mathf.Max(0.001f, belowRange));
        Shader.SetGlobalFloat(AboveRangeID, Mathf.Max(0.001f, aboveRange));
    }

    private void ResetShaderGlobals()
    {
        Shader.SetGlobalFloat(HasSparseFieldID, 0f);
        Shader.SetGlobalFloat(ScanRadiusID, 0f);
        Shader.SetGlobalFloat(RestoreRadiusID, 0f);
        Shader.SetGlobalFloat(RestoreActiveID, 0f);
    }

    private void LogState(string msg)
    {
        if (printStateChanges)
        {
            Debug.Log("[GaussianScanFX2] " + msg);
        }
    }
}