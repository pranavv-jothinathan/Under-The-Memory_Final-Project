using UnityEngine;

/// <summary>
/// Plays a random jazz clip while the right hand is strumming, with fade in/out.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class GuitarStrumAudio : MonoBehaviour
{
    [SerializeField] private PlayableGuitar guitar;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] clips;
    [SerializeField] private float fadeInTime = 0.25f;
    [SerializeField] private float fadeOutTime = 0.4f;
    [SerializeField] private float stopDelay = 0.4f;
    [SerializeField] private float playVolume = 0.85f;

    private float targetVolume;
    private float currentVolume;
    private float lastStrumTime = -10f;
    private int lastClipIndex = -1;

    private void Awake()
    {
        if (guitar == null)
            guitar = GetComponent<PlayableGuitar>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 1f;
            audioSource.volume = 0f;
        }
    }

    private void Update()
    {
        bool strumming = CanPlay() && guitar.IsRightHandStrumming();
        if (strumming)
            lastStrumTime = Time.time;

        if (strumming)
        {
            if (targetVolume <= 0.01f || !audioSource.isPlaying)
                StartRandomClip();
            targetVolume = playVolume;
        }
        else if (Time.time - lastStrumTime >= stopDelay)
        {
            targetVolume = 0f;
        }

        FadeVolume();
    }

    public void StopPlaying()
    {
        lastStrumTime = -10f;
        targetVolume = 0f;
    }

    private bool CanPlay()
    {
        if (guitar == null || audioSource == null || clips == null || clips.Length == 0)
            return false;

        if (!guitar.IsWorn)
            return false;

        return true;
    }

    private void StartRandomClip()
    {
        AudioClip clip = PickClip();
        if (clip == null)
            return;

        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.time = 0f;
        audioSource.volume = currentVolume;
        audioSource.Play();
    }

    private AudioClip PickClip()
    {
        int count = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                count++;
        }

        if (count == 0)
            return null;

        int index = lastClipIndex;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            index = Random.Range(0, clips.Length);
            if (clips[index] == null)
                continue;
            if (count == 1 || index != lastClipIndex)
                break;
        }

        lastClipIndex = index;
        return clips[index];
    }

    private void FadeVolume()
    {
        if (audioSource == null)
            return;

        float duration = targetVolume > currentVolume ? fadeInTime : fadeOutTime;
        float step = duration > 0.001f ? Time.deltaTime / duration : 1f;
        currentVolume = Mathf.MoveTowards(currentVolume, targetVolume, step * playVolume);
        audioSource.volume = currentVolume;

        if (currentVolume <= 0.01f && targetVolume <= 0f && audioSource.isPlaying)
            audioSource.Stop();
    }
}
