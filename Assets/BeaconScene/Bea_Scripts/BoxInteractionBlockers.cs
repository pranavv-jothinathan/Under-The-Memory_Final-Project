using UnityEngine;

public class BoxInteractionBlockers : MonoBehaviour
{
    [SerializeField] private GameObject interactionBlockers;

    public void DisableBlockers()
    {
        if (interactionBlockers != null)
        {
            interactionBlockers.SetActive(false);
            Debug.Log("Scanner box interaction blockers disabled.");
        }
    }
}