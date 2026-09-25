using UnityEngine;

public class SeaweedSway : MonoBehaviour
{
    [Header("水波摆动设置")]
    public float swayAmount = 8f;      // 摆动角度，越大晃得越厉害
    public float swaySpeed = 1.2f;     // 摆动速度，越大越快
    public float noiseOffset = 0f;     // 不同海草错开摆动

    [Header("细微上下浮动")]
    public float verticalAmount = 0.02f;
    public float verticalSpeed = 0.8f;

    private Quaternion startRotation;
    private Vector3 startPosition;

    void Start()
    {
        startRotation = transform.localRotation;
        startPosition = transform.localPosition;

        // 如果没有手动设置，就自动给每根海草一个不同的摆动时间
        if (noiseOffset == 0f)
        {
            noiseOffset = Random.Range(0f, 100f);
        }
    }

    void Update()
    {
        float time = Time.time + noiseOffset;

        // 左右慢慢摆动
        float sway = Mathf.Sin(time * swaySpeed) * swayAmount;

        // 加一点不规则感，避免太机械
        float secondarySway = Mathf.Sin(time * swaySpeed * 0.47f) * swayAmount * 0.35f;

        transform.localRotation = startRotation * Quaternion.Euler(
            0f,
            0f,
            sway + secondarySway
        );

        // 非常轻微上下漂浮
        float verticalMove = Mathf.Sin(time * verticalSpeed) * verticalAmount;
        transform.localPosition = startPosition + new Vector3(0f, verticalMove, 0f);
    }
}