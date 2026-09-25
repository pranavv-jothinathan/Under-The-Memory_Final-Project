using UnityEngine;

public class DebrisRemovalTest : MonoBehaviour
{
    [SerializeField]
    private BoxCollider recoveryBoundary;

    private bool hasBeenRemoved;

    private void OnTriggerExit(Collider other)
    {
        if (hasBeenRemoved)
            return;

        if (other != recoveryBoundary)
            return;

        hasBeenRemoved = true;

        Debug.Log(gameObject.name + " has been removed from the recovery area.");
    }
}