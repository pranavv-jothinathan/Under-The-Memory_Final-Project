using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BridgeDustSequence : MonoBehaviour
{
    [System.Serializable]
    public class DustStep
    {
        [Tooltip("这一阶段要播放的灰尘粒子")]
        public ParticleSystem dust;

        [Tooltip("播放这个灰尘之前等待多久")]
        [Min(0f)]
        public float delayBeforePlay = 0.5f;
    }

    [Header("Dust Sequence")]
    [SerializeField] private List<DustStep> dustSteps =
        new List<DustStep>();

    [Header("Settings")]
    [Tooltip("防止同一次动画重复触发")]
    [SerializeField] private bool playOnlyOnce = true;

    private bool hasPlayed;
    private Coroutine sequenceCoroutine;

    private void Awake()
    {
        StopAndClearAllDust();
    }

    // 给 Animation Event 调用
    public void PlayDustSequence()
    {
        if (playOnlyOnce && hasPlayed)
        {
            return;
        }

        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
        }

        hasPlayed = true;
        sequenceCoroutine = StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        for (int i = 0; i < dustSteps.Count; i++)
        {
            DustStep step = dustSteps[i];

            if (step == null)
            {
                continue;
            }

            if (step.delayBeforePlay > 0f)
            {
                yield return new WaitForSeconds(step.delayBeforePlay);
            }

            PlayDust(step.dust);
        }

        sequenceCoroutine = null;
    }

    private void PlayDust(ParticleSystem dust)
    {
        if (dust == null)
        {
            return;
        }

        dust.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        dust.Play(true);
    }

    private void StopAndClearAllDust()
    {
        foreach (DustStep step in dustSteps)
        {
            if (step == null || step.dust == null)
            {
                continue;
            }

            step.dust.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    public void ResetDustSequence()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        StopAndClearAllDust();
        hasPlayed = false;
    }
}