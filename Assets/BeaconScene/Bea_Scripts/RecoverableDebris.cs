using UnityEngine;

public class RecoverableDebris : MonoBehaviour
{
    [SerializeField]
    private Collider recoveryBoundary;

    [SerializeField]
    private RecoveryManager recoveryManager;

    [Header("Sequence")]
    [SerializeField]
    private bool isPlasticBottle;

    private bool hasBeenRemoved;

    private void OnTriggerExit(Collider other)
    {
        if (hasBeenRemoved)
            return;

        if (other != recoveryBoundary)
            return;

        hasBeenRemoved = true;
        recoveryManager.RegisterRemoved(gameObject, isPlasticBottle);
    }
}