using UnityEngine;

public class Shuimu2PathMovement : MonoBehaviour
{
    [Header("路线点")]
    public Transform[] pathPoints;

    [Header("移动参数")]
    public float moveSpeed = 0.03f;
    public float rotateSpeed = 0.5f;
    public float arriveDistance = 0.03f;

    [Header("到达路线点后停留")]
    public float waitTime = 0.4f;

    [Header("上下倾斜程度")]
    [Range(0f, 1f)]
    public float pitchStrength = 0.15f;

    [Header("轻微上下漂浮")]
    public float bobAmount = 0.012f;
    public float bobSpeed = 0.7f;

    private int currentPointIndex = 0;
    private float waitTimer = 0f;
    private bool isWaiting = false;

    private float randomOffset;
    private float previousBobValue;

    void Start()
    {
        randomOffset = Random.Range(0f, 10f);

        if (pathPoints == null || pathPoints.Length == 0)
        {
            Debug.LogWarning("Shuimu2Mover 没有设置路线点。");
            enabled = false;
        }
    }

    void Update()
    {
        if (pathPoints == null || pathPoints.Length == 0)
        {
            return;
        }

        ApplyFloating();

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0f)
            {
                isWaiting = false;
                GoToNextPoint();
            }

            return;
        }

        Transform targetPoint = pathPoints[currentPointIndex];

        if (targetPoint == null)
        {
            GoToNextPoint();
            return;
        }

        Vector3 direction = targetPoint.position - transform.position;

        if (direction.magnitude <= arriveDistance)
        {
            isWaiting = true;
            waitTimer = waitTime;
            return;
        }

        RotateTowards(direction);

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint.position,
            moveSpeed * Time.deltaTime
        );
    }

    void RotateTowards(Vector3 direction)
    {
        Vector3 adjustedDirection = direction;

        // 减弱上下倾斜，避免水母突然横躺
        adjustedDirection.y *= pitchStrength;

        if (adjustedDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(
            adjustedDirection.normalized,
            Vector3.up
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );
    }

    void ApplyFloating()
    {
        float currentBobValue =
            Mathf.Sin((Time.time + randomOffset) * bobSpeed) * bobAmount;

        float bobDifference = currentBobValue - previousBobValue;

        transform.position += Vector3.up * bobDifference;

        previousBobValue = currentBobValue;
    }

    void GoToNextPoint()
    {
        currentPointIndex++;

        if (currentPointIndex >= pathPoints.Length)
        {
            currentPointIndex = 0;
        }
    }
}