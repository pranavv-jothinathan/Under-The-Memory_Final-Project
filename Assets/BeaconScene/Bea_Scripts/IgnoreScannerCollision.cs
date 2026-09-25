using UnityEngine;

public class IgnoreScannerCollision : MonoBehaviour
{
    [SerializeField] private Collider[] scannerColliders;

    private Collider[] seaweedColliders;

    private void Awake()
    {
        seaweedColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Start()
    {
        SetScannerCollisionIgnored(true);
    }

    private void OnDisable()
    {
        SetScannerCollisionIgnored(false);
    }

    private void SetScannerCollisionIgnored(bool ignored)
    {
        if (scannerColliders == null)
            return;

        foreach (Collider seaweedCollider in seaweedColliders)
        {
            foreach (Collider scannerCollider in scannerColliders)
            {
                if (seaweedCollider != null && scannerCollider != null)
                {
                    Physics.IgnoreCollision(
                        seaweedCollider,
                        scannerCollider,
                        ignored
                    );
                }
            }
        }
    }
}