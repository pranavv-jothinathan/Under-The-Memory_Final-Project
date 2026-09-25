using UnityEngine;
using System.Collections;

public class MusicFadeOut : MonoBehaviour
{
    public AudioSource music;
    public float fadeDuration = 2f;

    public void FadeOutMusic()
    {
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float startVolume = music.volume;

        while (music.volume > 0f)
        {
            music.volume -= startVolume * Time.deltaTime / fadeDuration;
            yield return null;
        }

        music.volume = 0f;
        music.Stop();
    }
}
