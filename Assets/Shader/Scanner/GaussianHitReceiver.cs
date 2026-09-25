using UnityEngine;
using UnityEngine.Events;

public class GaussianHitReceiver : MonoBehaviour
{
    [Header("Target B")]
    public GameObject target;
    public GaussianSplatRevealController targetB;

    [Header("Events")]
    public UnityEvent onScannerHit;

    public void OnScannerHit()
    {
        if (target != null)
        {
            target.SetActive(true);
        }

        if (targetB != null)
        {
            targetB.PlayReveal();
        }

        if (onScannerHit != null)
        {
            onScannerHit.Invoke();
        }
    }
}
