using UnityEngine;

public class JellyfishSoftOrbit : MonoBehaviour
{
    [Header("Orbit Points")]
    public Transform[] points;

    [Header("Soft Jellyfish Movement")]
    public float baseSpeed = 0.08f;
    public float pulseBoost = 0.18f;
    public float pulseFrequency = 0.75f;
    public float turnSoftness = 0.8f;
    public float reachDistance = 0.5f;

    [Header("Floating")]
    public float verticalFloat = 0.12f;
    public float verticalFloatSpeed = 0.45f;
    public float horizontalDrift = 0.04f;
    public float horizontalDriftSpeed = 0.35f;

    [Header("Keep Jellyfish Upright")]
    public bool keepUpright = true;

    private int currentPointIndex = 0;
    private float randomOffset;
    private Vector3 velocity;

    void Start()
    {
        randomOffset = Random.Range(0f, 100f);

        if (points != null && points.Length > 0)
        {
            currentPointIndex = GetClosestPointIndex();
        }
    }

    void Update()
    {
        if (points == null || points.Length == 0)
            return;

        Transform targetPoint = points[currentPointIndex];

        Vector3 targetPos = targetPoint.position;

        // 很慢的上下漂浮，不要太夸张
        targetPos.y += Mathf.Sin(Time.time * verticalFloatSpeed + randomOffset) * verticalFloat;

        // 轻微水流漂移
        Vector3 drift = new Vector3(
            Mathf.Sin(Time.time * horizontalDriftSpeed + randomOffset),
            0f,
            Mathf.Cos(Time.time * horizontalDriftSpeed * 0.8f + randomOffset)
        ) * horizontalDrift;

        targetPos += drift;

        Vector3 toTarget = targetPos - transform.position;

        // 水母不是匀速游，而是一下一下推进
        float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f + randomOffset)), 3f);
        float currentSpeed = baseSpeed + pulse * pulseBoost;

        Vector3 desiredVelocity = toTarget.normalized * currentSpeed;

        // 让速度变化变软，不要突然被拖走
        velocity = Vector3.Lerp(velocity, desiredVelocity, Time.deltaTime * 1.2f);

        transform.position += velocity * Time.deltaTime;

        // 保持水母竖直，只做很轻微的旋转
        if (keepUpright && velocity.sqrMagnitude > 0.0001f)
        {
            Quaternion softRotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);

            // 只保留 Y 轴旋转，避免水母歪倒
            Vector3 euler = softRotation.eulerAngles;
            Quaternion targetRot = Quaternion.Euler(0f, euler.y, 0f);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * turnSoftness
            );
        }

        if (Vector3.Distance(transform.position, targetPoint.position) < reachDistance)
        {
            currentPointIndex++;

            if (currentPointIndex >= points.Length)
                currentPointIndex = 0;
        }
    }

    int GetClosestPointIndex()
    {
        int closestIndex = 0;
        float closestDistance = Vector3.Distance(transform.position, points[0].position);

        for (int i = 1; i < points.Length; i++)
        {
            float distance = Vector3.Distance(transform.position, points[i].position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }
}