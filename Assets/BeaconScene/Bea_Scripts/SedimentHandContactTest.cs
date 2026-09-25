using UnityEngine;

public class SedimentHandContactTest : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Sediment touched by: " + other.name);
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Hand left sediment: " + other.name);
    }
}