using UnityEngine;

public class PlayVoice : MonoBehaviour
{
    public AudioSource voiceSource;

    [Header("Test Only")]
    public KeyCode testKey = KeyCode.P;

    public void PlayVoiceClip()
    {
        if (voiceSource != null)
        {
            voiceSource.Play();
        }
    }

    void Update()
    {
        // 方便在编辑器里测试
        if (Input.GetKeyDown(testKey))
        {
            PlayVoiceClip();
        }
    }
}