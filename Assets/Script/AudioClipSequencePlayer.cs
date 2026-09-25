using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioClipSequencePlayer : MonoBehaviour
{
    [System.Serializable]
    public class AudioItem
    {
        public AudioClip clip;
        [Min(0f)]
        public float intervalAfter = 0.5f;
    }

    [Header("Audio Source")]
    public AudioSource audioSource;

    [Header("Sequence Clips")]
    public List<AudioItem> audioClips = new List<AudioItem>();

    [Header("Options")]
    public bool playOnStart = false;
    public bool loopSequence = false;

    private Coroutine playCoroutine;

    private void Start()
    {
        if (playOnStart)
        {
            PlaySequence();
        }
    }


    public void PlaySequence()
    {

        StopSequence();
        playCoroutine = StartCoroutine(PlaySequenceRoutine());
    }

    public void StopSequence()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private IEnumerator PlaySequenceRoutine()
    {
        do
        {
            for (int i = 0; i < audioClips.Count; i++)
            {
                AudioItem item = audioClips[i];

                if (item == null || item.clip == null)
                    continue;

                audioSource.clip = item.clip;
                audioSource.Play();


                yield return new WaitForSeconds(item.clip.length);


                if (item.intervalAfter > 0f)
                {
                    yield return new WaitForSeconds(item.intervalAfter);
                }
            }
        }
        while (loopSequence);

        playCoroutine = null;
    }
}
