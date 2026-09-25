using UnityEngine;

public class PlayEcoSink : MonoBehaviour
{
    [SerializeField] private Animator ecoAnimator;

    public void StartEcoSink()
    {
        if (ecoAnimator == null)
        {
            return;
        }

        ecoAnimator.enabled = true;
        ecoAnimator.Play("EcoSink", 0, 0f);
    }
}