using System.Collections;
using UnityEngine;

/// <summary>
/// 按间隔依次触发一组台词。挂在常驻的 director 上，这样人物节点关掉也不会打断排队。
/// </summary>
public class VoiceLineSequence : MonoBehaviour
{
    [SerializeField] private OneShotVoiceLine[] lines;
    [SerializeField] private float startDelay = 1.2f;
    [SerializeField] private float gapBetweenLines = 0.6f;

    public bool HasStarted { get; private set; }
    public bool IsFinished { get; private set; }

    private Coroutine routine;

    public void PlaySequence()
    {
        if (HasStarted)
            return;

        HasStarted = true;
        routine = StartCoroutine(Run());
    }

    public void Abort()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (lines != null)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null)
                    lines[i].Stop();
            }
        }

        IsFinished = true;
    }

    private IEnumerator Run()
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        if (lines != null)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                OneShotVoiceLine line = lines[i];
                if (line == null || line.HasPlayed)
                    continue;

                float wait = line.Duration;
                if (!line.Play())
                    continue;

                yield return new WaitForSeconds(wait + Mathf.Max(0f, gapBetweenLines));
            }
        }

        IsFinished = true;
        routine = null;
    }
}
