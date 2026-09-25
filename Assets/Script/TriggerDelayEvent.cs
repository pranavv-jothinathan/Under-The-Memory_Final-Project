using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TriggerDelayEvent : MonoBehaviour
{
    [Header("Settings")]
    public string playerTag = "Player";
    public float delaySeconds = 2f;

    [Header("Event")]
    public UnityEvent onTriggered;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        if (other.CompareTag(playerTag))
        {
            hasTriggered = true;
            StartCoroutine(DelayInvoke());
        }
    }

    private IEnumerator DelayInvoke()
    {
        yield return new WaitForSeconds(delaySeconds);
        onTriggered?.Invoke();
    }
}
