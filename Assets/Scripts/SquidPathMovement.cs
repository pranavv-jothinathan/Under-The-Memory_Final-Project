using UnityEngine;

public class SquidPathMovement : MonoBehaviour
{
    [Header("路线点")]
    public Transform[] pathPoints;

    [Header("移动参数")]
    public float moveSpeed = 0.15f;
    public float rotateSpeed = 1.5f;
    public float arriveDistance = 0.05f;

    [Header("到达路线点后停留")]
    public float waitTime = 0.2f;

    private int currentPointIndex;
    private float waitTimer;
    private bool isWaiting;

    void Start()
    {
        if (pathPoints == null || pathPoints.Length == 0)
        {
            Debug.LogWarning("SquidMover 没有设置路线点。");
            enabled = false;
        }
    }

    void Update()
    {
        if (pathPoints == null || pathPoints.Length == 0)
        {
            return;
        }

        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0f)
            {
                isWaiting = false;
                currentPointIndex++;

                if (currentPointIndex >= pathPoints.Length)
                {
                    currentPointIndex = 0;
                }
            }

            return;
        }

        Transform targetPoint = pathPoints[currentPointIndex];
        Vector3 direction = targetPoint.position - transform.position;

        if (direction.magnitude <= arriveDistance)
        {
            isWaiting = true;
            waitTimer = waitTime;
            return;
        }

        RotateTowardsDirection(direction);

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint.position,
            moveSpeed * Time.deltaTime
        );
    }

    void RotateTowardsDirection(Vector3 direction)
    {
        Vector3 normalizedDirection = direction.normalized;

        // 计算左右方向
        float yaw = Mathf.Atan2(
            normalizedDirection.x,
            normalizedDirection.z
        ) * Mathf.Rad2Deg;

        // 计算向上或向下的角度
        float horizontalDistance = new Vector2(
            normalizedDirection.x,
            normalizedDirection.z
        ).magnitude;

        float pitch = -Mathf.Atan2(
            normalizedDirection.y,
            horizontalDistance
        ) * Mathf.Rad2Deg;

        // Z 永远保持为 0，防止乌贼侧翻
        Quaternion targetRotation = Quaternion.Euler(
            pitch,
            yaw,
            0f
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );
    }
}