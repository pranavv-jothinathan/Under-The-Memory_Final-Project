using System.Collections;
using UnityEngine;

/// <summary>
/// 一组循环背景音的统一淡入淡出。各音源保留自己在 Inspector 上设定的音量作为目标值。
/// </summary>
public class FadeAudioGroup : MonoBehaviour
{
    [SerializeField] private AudioSource[] sources;
    [Tooltip("勾选后场景开始时立即静音并停止播放。")]
    [SerializeField] private bool silentOnAwake = true;

    private float[] baseVolumes;
    private Coroutine fadeRoutine;

    public bool IsFadedIn { get; private set; }

    private void Awake()
    {
        CacheBaseVolumes();

        if (silentOnAwake)
            SetSilent();
    }

    public void SetSilent()
    {
        StopFade();
        if (sources == null)
            return;

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null)
                continue;

            sources[i].volume = 0f;
            sources[i].Stop();
        }

        IsFadedIn = false;
    }

    public void FadeIn(float duration)
    {
        if (sources == null || sources.Length == 0)
            return;

        CacheBaseVolumes();
        StopFade();

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null)
                continue;

            source.loop = true;
            if (!source.isPlaying)
            {
                source.volume = 0f;
                source.Play();
            }
        }

        IsFadedIn = true;
        fadeRoutine = StartCoroutine(FadeRoutine(duration, true));
    }

    public void FadeOut(float duration)
    {
        if (sources == null || sources.Length == 0)
            return;

        StopFade();
        IsFadedIn = false;
        fadeRoutine = StartCoroutine(FadeRoutine(duration, false));
    }

    private IEnumerator FadeRoutine(float duration, bool fadeIn)
    {
        float[] start = new float[sources.Length];
        for (int i = 0; i < sources.Length; i++)
            start[i] = sources[i] != null ? sources[i].volume : 0f;

        float time = 0f;
        float total = Mathf.Max(0.01f, duration);

        while (time < total)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / total);

            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null)
                    continue;

                float target = fadeIn ? baseVolumes[i] : 0f;
                sources[i].volume = Mathf.Lerp(start[i], target, t);
            }

            yield return null;
        }

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null)
                continue;

            sources[i].volume = fadeIn ? baseVolumes[i] : 0f;
            if (!fadeIn)
                sources[i].Stop();
        }

        fadeRoutine = null;
    }

    private void StopFade()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
    }

    private void CacheBaseVolumes()
    {
        if (sources == null)
        {
            baseVolumes = new float[0];
            return;
        }

        if (baseVolumes != null && baseVolumes.Length == sources.Length)
            return;

        baseVolumes = new float[sources.Length];
        for (int i = 0; i < sources.Length; i++)
            baseVolumes[i] = sources[i] != null ? Mathf.Max(0.0001f, sources[i].volume) : 0f;
    }
}
