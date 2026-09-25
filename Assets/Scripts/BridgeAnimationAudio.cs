using UnityEngine;

public class BridgeAnimationAudio : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip bridgeSound;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [Tooltip("从音频的第几秒开始播放")]
    [Min(0f)]
    [SerializeField] private float audioStartTime = 0f;

    private bool hasPlayed;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            Debug.LogError("BridgeAnimationAudio：没有找到 Audio Source。");
            enabled = false;
            return;
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    public void PlayBridgeSound()
    {
        if (hasPlayed || bridgeSound == null)
        {
            return;
        }

        hasPlayed = true;

        audioSource.clip = bridgeSound;
        audioSource.volume = volume;

        float safeStartTime = Mathf.Clamp(
            audioStartTime,
            0f,
            Mathf.Max(0f, bridgeSound.length - 0.05f)
        );

        audioSource.time = safeStartTime;
        audioSource.Play();
    }

    public void ResetSound()
    {
        hasPlayed = false;
        audioSource.Stop();
    }
}