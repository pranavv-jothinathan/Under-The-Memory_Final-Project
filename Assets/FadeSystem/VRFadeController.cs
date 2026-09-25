using System.Collections;
using UnityEngine;


public class VRFadeController : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeOutDuration = 1f;
    [SerializeField] private float fadeInDuration = 1f;

    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>淡出至黑屏</summary>
    public void FadeOut()
    {
        StartFade(1f, fadeOutDuration);
    }

    /// <summary>从黑屏淡入</summary>
    public void FadeIn()
    {
        StartFade(0f, fadeInDuration);
    }

    private void StartFade(float targetAlpha, float duration)
    {
        if (fadeCanvasGroup == null)
        {
            Debug.LogWarning("[VRFadeController] 未绑定 CanvasGroup。");
            return;
        }

        // 避免重复调用时多个淡入淡出协程互相抢 alpha
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, duration));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        float startAlpha = fadeCanvasGroup.alpha;

        if (duration <= 0f)
        {
            fadeCanvasGroup.alpha = targetAlpha;
            fadeCanvasGroup.blocksRaycasts = targetAlpha > 0f;
            fadeCoroutine = null;
            yield break;
        }

        fadeCanvasGroup.blocksRaycasts = true;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            fadeCanvasGroup.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                Mathf.Clamp01(elapsedTime / duration)
            );

            yield return null;
        }

        fadeCanvasGroup.alpha = targetAlpha;
        fadeCanvasGroup.blocksRaycasts = targetAlpha > 0f;
        fadeCoroutine = null;
    }
}
