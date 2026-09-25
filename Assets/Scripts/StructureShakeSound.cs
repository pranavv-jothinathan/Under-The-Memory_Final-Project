using UnityEngine;

public class StructureShakeSound : MonoBehaviour
{
    [SerializeField] private AudioSource shakeAudio;

    private void Start()
    {
        StartShakeSound();
    }

    public void StartShakeSound()
    {
        if (shakeAudio == null)
            return;

        if (!shakeAudio.isPlaying)
            shakeAudio.Play();
    }

    public void StopShakeSound()
    {
        if (shakeAudio == null)
            return;

        shakeAudio.Stop();
    }
}