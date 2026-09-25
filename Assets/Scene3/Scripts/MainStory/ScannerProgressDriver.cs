using UnityEngine;

public class ScannerProgressDriver : MonoBehaviour
{
    [Header("Existing Scanner")]
    public ScannerBehavior scannerBehavior;

    [Header("Raycast")]
    public Transform scanOrigin;

    public float scanDistance = 15f;

    public LayerMask scannableLayers;

    [Header("Debug")]
    public bool drawDebugRay = true;

    private const float DefaultScanOriginZ = 0.95f;

    private void Awake()
    {
        ScannerBehavior localBehavior =
            GetComponentInChildren<ScannerBehavior>(true);

        if (localBehavior != null)
        {
            scannerBehavior = localBehavior;
            localBehavior.enabled = true;
        }

        if (scanOrigin == null || !scanOrigin.IsChildOf(transform))
            scanOrigin = FindNamedChild("ScanOrigin");

        if (scanOrigin != null &&
            Mathf.Abs(scanOrigin.localPosition.z) < 0.01f)
        {
            Vector3 local = scanOrigin.localPosition;
            local.z = DefaultScanOriginZ;
            scanOrigin.localPosition = local;
        }
    }

    private Transform FindNamedChild(string name)
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name)
                return all[i];
        }

        return null;
    }

    private void Update()
    {
        if (scannerBehavior == null)
            return;

        if (scanOrigin == null)
            return;

        if (!scannerBehavior.IsScanInputHeld)
            return;

        TryScanTarget();
    }

    private void TryScanTarget()
    {
        Ray ray =
            new Ray(
                scanOrigin.position,
                scanOrigin.forward
            );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            scanDistance,
            scannableLayers,
            QueryTriggerInteraction.Ignore))
        {
            PillarScanTrigger pillarTrigger =
                hit.collider
                   .GetComponentInParent<PillarScanTrigger>();

            if (pillarTrigger != null &&
                pillarTrigger.CanScan)
            {
                pillarTrigger.AddScanTime(
                    Time.deltaTime
                );
            }

            ScannableTarget target =
                hit.collider
                   .GetComponentInParent<ScannableTarget>();

            if (target != null &&
                target.CanScan)
            {
                target.AddScanTime(
                    Time.deltaTime
                );
            }

            if (drawDebugRay)
            {
                Debug.DrawLine(
                    scanOrigin.position,
                    hit.point,
                    Color.green
                );
            }
        }
        else
        {
            if (drawDebugRay)
            {
                Debug.DrawRay(
                    scanOrigin.position,
                    scanOrigin.forward *
                    scanDistance,
                    Color.red
                );
            }
        }
    }
}