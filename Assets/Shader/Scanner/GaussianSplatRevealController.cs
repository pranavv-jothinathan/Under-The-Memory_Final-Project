using System.Collections;
using UnityEngine;
using GaussianSplatting.Runtime;

public class GaussianSplatRevealController : MonoBehaviour
{
    [Header("Renderer")]
    public GaussianSplatRenderer splatRenderer;

    [Header("Timings")]
    public float revealDuration = 1f;
    public float holdDuration = 5f;

    [Header("Opacity")]
    public float hiddenOpacity = 0f;
    public float visibleOpacity = 1f;

    private Coroutine playCoroutine;

    private void Awake()
    {
        if (splatRenderer == null)
            splatRenderer = GetComponent<GaussianSplatRenderer>();

        SetOpacity(hiddenOpacity);
        gameObject.SetActive(false);
    }

    public void PlayReveal()
    {
        if (playCoroutine != null)
            StopCoroutine(playCoroutine);

        playCoroutine = StartCoroutine(RevealRoutine());
    }

    private IEnumerator RevealRoutine()
    {
        gameObject.SetActive(true);

        float t = 0f;
        while (t < revealDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / revealDuration);

            SetOpacity(Mathf.Lerp(hiddenOpacity, visibleOpacity, k));

            yield return null;
        }

        SetOpacity(visibleOpacity);

        yield return new WaitForSeconds(holdDuration);

        gameObject.SetActive(false);
    }

    private void SetOpacity(float value)
    {
        if (splatRenderer != null)
        {
            splatRenderer.m_OpacityScale = value;
        }
    }
}
