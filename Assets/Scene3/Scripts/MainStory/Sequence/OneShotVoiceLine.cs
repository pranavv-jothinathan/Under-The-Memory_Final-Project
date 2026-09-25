using UnityEngine;

/// <summary>
/// 挂在角色身上的 3D 台词源。整局只会响一次。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class OneShotVoiceLine : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip clip;
    [Tooltip("台词播完后额外留出的静默，供 VoiceLineSequence 排队使用。")]
    [SerializeField] private float tailPadding = 0.4f;

    public bool HasPlayed { get; private set; }

    public float Duration
    {
        get
        {
            AudioClip resolved = ResolveClip();
            float length = resolved != null ? resolved.length : 2.5f;
            return length + Mathf.Max(0f, tailPadding);
        }
    }

    private void Awake()
    {
        if (source == null)
            source = GetComponent<AudioSource>();
    }

    /// <summary>播放台词。已经播过、或对象没激活时什么都不做。</summary>
    public bool Play()
    {
        if (HasPlayed)
            return false;

        if (source == null)
            source = GetComponent<AudioSource>();

        AudioClip resolved = ResolveClip();
        if (source == null || resolved == null)
            return false;

        if (!source.isActiveAndEnabled)
            return false;

        HasPlayed = true;
        source.clip = resolved;
        source.loop = false;
        source.Play();
        return true;
    }

    public void Stop()
    {
        if (source != null && source.isPlaying)
            source.Stop();
    }

    private AudioClip ResolveClip()
    {
        if (clip != null)
            return clip;

        return source != null ? source.clip : null;
    }
}
