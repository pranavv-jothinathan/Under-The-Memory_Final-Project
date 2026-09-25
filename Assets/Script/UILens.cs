using System.Collections;
using UnityEngine;

public class ScaleThenDisable : MonoBehaviour
{
    [Header("缩放设置")]
    public Vector3 initialScale = Vector3.one;

    [Tooltip("缩放到的目标大小。")]
    public Vector3 targetScale = Vector3.one * 1.5f;

    [Tooltip("从初始缩放到目标缩放所需时间（秒）。")]
    [Min(0f)]
    public float scaleUpDuration = 0.3f;

    [Header("停留时间")]
    [Min(0f)]
    public float stayDuration = 1f;

    [Header("回归设置")]
    [Min(0f)]
    public float scaleDownDuration = 0.3f;


    public bool useCurrentScaleAsInitial = true;

    private Coroutine scaleRoutine;

    private void Awake()
    {
        if (useCurrentScaleAsInitial)
            initialScale = transform.localScale;
    }

    private void OnEnable()
    {

        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);

        scaleRoutine = StartCoroutine(ScaleSequence());
    }

    private void OnDisable()
    {
        if (scaleRoutine != null)
        {
            StopCoroutine(scaleRoutine);
            scaleRoutine = null;
        }
    }

    private IEnumerator ScaleSequence()
    {

        transform.localScale = initialScale;


        yield return ScaleLinearly(initialScale, targetScale, scaleUpDuration);


        if (stayDuration > 0f)
            yield return new WaitForSeconds(stayDuration);


        yield return ScaleLinearly(targetScale, initialScale, scaleDownDuration);

        scaleRoutine = null;


        gameObject.SetActive(false);
    }

    private IEnumerator ScaleLinearly(
        Vector3 fromScale,
        Vector3 toScale,
        float duration)
    {

        if (duration <= 0f)
        {
            transform.localScale = toScale;
            yield break;
        }

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsedTime / duration);


            transform.localScale = Vector3.Lerp(
                fromScale,
                toScale,
                progress
            );

            yield return null;
        }

        transform.localScale = toScale;
    }
}

