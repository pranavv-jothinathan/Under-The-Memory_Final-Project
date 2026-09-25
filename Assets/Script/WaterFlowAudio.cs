using UnityEngine;

public class WaterFlowAudio : MonoBehaviour
{
    public AudioSource waterAudio;

    // 正式功能：开始水流
    public void StartWaterFlow()
    {
        if (waterAudio != null && !waterAudio.isPlaying)
        {
            waterAudio.Play();
        }
    }

    // 正式功能：停止水流
    public void StopWaterFlow()
    {
        if (waterAudio != null && waterAudio.isPlaying)
        {
            waterAudio.Stop();
        }
    }

    // ===== 临时测试 =====
void Update()
{
    // 数字1：开始水流
    if (Input.GetKeyDown(KeyCode.Alpha1))
    {
        StartWaterFlow();
    }

    // 数字2：停止水流
    if (Input.GetKeyDown(KeyCode.Alpha2))
    {
        StopWaterFlow();
    }

    }
}