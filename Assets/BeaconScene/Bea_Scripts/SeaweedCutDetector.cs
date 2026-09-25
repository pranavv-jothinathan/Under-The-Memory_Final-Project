using UnityEngine;

public class SeaweedCutDetector : MonoBehaviour
{
    [SerializeField] private Collider bladeCutTrigger;
    [SerializeField] private SeaweedCutAnimation seaweedCutAnimation;

    [SerializeField] private RecoveryManager recoveryManager;
    [SerializeField] private GameObject seaweedRecoveryItem;

    private bool cutDetected;

    private void OnTriggerEnter(Collider other)
    {
        if (cutDetected || other != bladeCutTrigger)
            return;

        cutDetected = true;

        Debug.Log("SEAWEED CUT DETECTED");

        seaweedCutAnimation.StartCutAnimation();

        if (recoveryManager != null)
        {
            recoveryManager.RegisterRemoved(
                seaweedRecoveryItem != null
                    ? seaweedRecoveryItem
                    : gameObject
            );
        }
        else
        {
            Debug.LogWarning(
                "Recovery Manager is not assigned for the seaweed."
            );
        }
    }
}