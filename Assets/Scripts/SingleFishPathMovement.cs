using UnityEngine;

public class SingleFishPathMovement : MonoBehaviour
{
    [Header("路线点，按照顺序拖入")]
    public Transform[] pathPoints;

    [Header("鱼的模型父物体")]
    [Tooltip("拖入鱼本体，或者包着鱼模型的空物体")]
    public Transform fishVisual;

    [Header("移动参数")]
    public float moveSpeed = 0.08f;
    public float rotateSpeed = 1.8f;

    [Header("提前转向")]
    [Tooltip("距离路线点还有多远时，提前切换到下一个点")]
    public float lookAheadDistance = 0.18f;

    [Header("上下倾斜")]
    [Range(0f, 1f)]
    public float pitchStrength = 0.18f;

    [Header("转弯侧倾")]
    public float bankAmount = 6f;
    public float bankSmooth = 2f;

    [Header("身体轻微摆动")]
    public float bodySwayAmount = 1.5f;
    public float bodySwaySpeed = 1.2f;

    [Header("开始设置")]
    public bool startAtFirstPoint = true;

    private int currentPointIndex;
    private Quaternion visualStartRotation;
    private Vector3 previousForward;

    void Start()
    {
        if (pathPoints == null || pathPoints.Length < 2)
        {
            Debug.LogWarning("单独的鱼至少需要两个路线点。");
            enabled = false;
            return;
        }

        if (startAtFirstPoint && pathPoints[0] != null)
        {
            transform.position = pathPoints[0].position;
            currentPointIndex = 1;
        }
        else
        {
            currentPointIndex = FindNearestPointIndex();
        }

        previousForward = transform.forward;

        if (fishVisual != null)
        {
            visualStartRotation = fishVisual.localRotation;
        }
    }

    void Update()
    {
        if (pathPoints == null || pathPoints.Length < 2)
        {
            return;
        }

        Transform targetPoint = pathPoints[currentPointIndex];

        if (targetPoint == null)
        {
            GoToNextPoint();
            return;
        }

        Vector3 direction = targetPoint.position - transform.position;

        if (direction.magnitude <= lookAheadDistance)
        {
            GoToNextPoint();

            targetPoint = pathPoints[currentPointIndex];

            if (targetPoint == null)
            {
                return;
            }

            direction = targetPoint.position - transform.position;
        }

        RotateTowards(direction);

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPoint.position,
            moveSpeed * Time.deltaTime
        );

        ApplyBodyMovement();
    }

    void RotateTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 adjustedDirection = direction;

        // 减弱上下倾斜，避免鱼突然竖起来
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

    void ApplyBodyMovement()
    {
        if (fishVisual == null)
        {
            previousForward = transform.forward;
            return;
        }

        float turnAmount = Vector3.SignedAngle(
            previousForward,
            transform.forward,
            Vector3.up
        );

        float bank = Mathf.Clamp(
            -turnAmount * bankAmount,
            -bankAmount,
            bankAmount
        );

        float sway = Mathf.Sin(
            Time.time * bodySwaySpeed
        ) * bodySwayAmount;

        Quaternion targetLocalRotation =
            visualStartRotation *
            Quaternion.Euler(0f, sway, bank);

        fishVisual.localRotation = Quaternion.Slerp(
            fishVisual.localRotation,
            targetLocalRotation,
            bankSmooth * Time.deltaTime
        );

        previousForward = transform.forward;
    }

    void GoToNextPoint()
    {
        currentPointIndex++;

        if (currentPointIndex >= pathPoints.Length)
        {
            currentPointIndex = 0;
        }
    }

    int FindNearestPointIndex()
    {
        float nearestDistance = Mathf.Infinity;
        int nearestIndex = 0;

        for (int i = 0; i < pathPoints.Length; i++)
        {
            if (pathPoints[i] == null)
            {
                continue;
            }

            float distance = Vector3.Distance(
                transform.position,
                pathPoints[i].position
            );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    void OnDrawGizmosSelected()
    {
        if (pathPoints == null || pathPoints.Length < 2)
        {
            return;
        }

        for (int i = 0; i < pathPoints.Length; i++)
        {
            if (pathPoints[i] == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(pathPoints[i].position, 0.04f);

            int nextIndex = (i + 1) % pathPoints.Length;

            if (pathPoints[nextIndex] != null)
            {
                Gizmos.DrawLine(
                    pathPoints[i].position,
                    pathPoints[nextIndex].position
                );
            }
        }
    }
}