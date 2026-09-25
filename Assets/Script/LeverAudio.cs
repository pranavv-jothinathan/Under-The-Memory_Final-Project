using UnityEngine;

public class LeverAudio : MonoBehaviour
{
    public AudioSource scrapeAudio;

    public void StartScrape()
    {
        if (scrapeAudio != null && !scrapeAudio.isPlaying)
        {
            scrapeAudio.Play();
        }
    }

    public void StopScrape()
    {
        if (scrapeAudio != null && scrapeAudio.isPlaying)
        {
            scrapeAudio.Stop();
        }
    }

    // 临时测试
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Y))
        {
            StartScrape();
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            StopScrape();
        }
    }
}