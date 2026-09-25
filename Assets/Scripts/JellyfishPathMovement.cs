using UnityEngine;

public class JellyfishPathMovement : MonoBehaviour
{
    [Header("路线点")]
    public Transform[] pathPoints;

    [Header("移动参数")]
    public float moveSpeed = 0.05f;
    public float rotateSpeed = 0.6f;
    public float arriveDistance = 0.04f;

    [Header("到达点后停留")]
    public float waitTime = 0.5f;

    [Header("自然漂浮")]
    public float bobAmount = 0.03f;
    public float bobSpeed = 0.8f;

    private int currentPointIndex;
    private float waitTimer;
    private bool isWaiting;
    private float randomOffset;

    void Start()
    {
        randomOffset = Random.Range(0f, 10f);

        if (pathPoints == null || pathPoints.Length == 0)
        {
            Debug.LogWarning("水母没有设置路线点。");
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

            ApplyFloating();
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

        RotateTowards(direction);

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint.position,
            moveSpeed * Time.deltaTime
        );

        ApplyFloating();
    }

    void RotateTowards(Vector3 direction)
    {
        Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);

        if (flatDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(flatDirection);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime
        );
    }

    void ApplyFloating()
    {
        float floating = Mathf.Sin(
            (Time.time + randomOffset) * bobSpeed
        ) * bobAmount;

        transform.position += Vector3.up * floating * Time.deltaTime;
    }
}