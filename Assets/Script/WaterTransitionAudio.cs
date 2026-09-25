using UnityEngine;

public class WaterTransitionAudio : MonoBehaviour
{
    public AudioSource exitWaterAudio;
    public AudioSource enterWaterAudio;

    // 玩家出水时调用
    public void PlayExitWater()
    {
        if (exitWaterAudio != null)
        {
            exitWaterAudio.Play();
        }
    }

    // 玩家入水时调用
    public void PlayEnterWater()
    {
        if (enterWaterAudio != null)
        {
            enterWaterAudio.Play();
        }
    }

    // 临时测试
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            PlayExitWater();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            PlayEnterWater();
        }
    }
}