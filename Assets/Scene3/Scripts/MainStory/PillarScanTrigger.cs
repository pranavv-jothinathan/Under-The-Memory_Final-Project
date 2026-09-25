using UnityEngine;

/// <summary>
/// 用扫描仪对准柱子胶囊、扣住扳机约 1 秒后激活柱子。
/// 不要勾 CapsuleCollider 的 Is Trigger：ScannerProgressDriver 会忽略 trigger。
/// </summary>
public class PillarScanTrigger : MonoBehaviour
{
    [Header("Story")]
    public MemoryPillar pillar;

    [Header("Scan Settings")]
    public float requiredScanTime = 1f;

    [Header("Runtime Debug")]
    [SerializeField]
    [Range(0f, 1f)]
    private float scanProgress = 0f;

    private float currentScanTime = 0f;

    public bool CanScan =>
        pillar != null &&
        pillar.CanActivate;

    private void Awake()
    {
        if (pillar == null)
            pillar = GetComponentInParent<MemoryPillar>();

        int scanTargetLayer =
            LayerMask.NameToLayer("ScanTarget");

        if (scanTargetLayer >= 0)
            gameObject.layer = scanTargetLayer;

        Collider col = GetComponent<Collider>();

        if (col != null)
            col.isTrigger = false;
    }

    public void AddScanTime(float amount)
    {
        if (!CanScan)
            return;

        if (requiredScanTime <= 0f)
        {
            scanProgress = 1f;
            CompleteScan();
            return;
        }

        currentScanTime += amount;

        scanProgress = Mathf.Clamp01(
            currentScanTime / requiredScanTime
        );

        if (scanProgress >= 1f)
            CompleteScan();
    }

    private void CompleteScan()
    {
        if (pillar == null)
            return;

        pillar.TouchGraffiti();
        scanProgress = 1f;
    }
}
