using UnityEngine;

public class PlaySFX : MonoBehaviour
{
    public AudioSource audioSource;

    [Header("Random SFX")]
    public AudioClip[] audioClips;

    [Header("Test Only")]
    public KeyCode testKey = KeyCode.P;

    public void PlaySound()
    {
        if (audioSource == null || audioClips.Length == 0)
            return;

        int randomIndex = Random.Range(0, audioClips.Length);

        audioSource.clip = audioClips[randomIndex];
        audioSource.Play();

        Debug.Log("Playing SFX: " + audioClips[randomIndex].name);
    }

    void Update()
    {
        if (Input.GetKeyDown(testKey))
        {
            PlaySound();
        }
    }
}