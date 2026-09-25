using UnityEngine;

public class SquidPathMovement2 : MonoBehaviour
{
    [Header("路线点，按照顺序拖入")]
    public Transform[] pathPoints;

    [Header("乌贼模型父物体")]
    [Tooltip("拖入乌贼本体，或者专门包住乌贼模型的空物体")]
    public Transform squidVisual;

    [Header("移动参数")]
    public float moveSpeed = 0.06f;
    public float rotateSpeed = 1.2f;

    [Header("提前转向")]
    [Tooltip("距离路线点还有多远时，提前切换到下一个点")]
    public float lookAheadDistance = 0.15f;

    [Header("上下倾斜")]
    [Range(0f, 1f)]
    [Tooltip("越大，乌贼向上或向下游时倾斜越明显")]
    public float pitchStrength = 0.35f;

    [Header("转弯时轻微侧倾")]
    public float bankAmount = 4f;
    public float bankSmooth = 2f;

    [Header("身体轻微摆动")]
    public float bodySwayAmount = 1f;
    public float bodySwaySpeed = 0.8f;

    [Header("开始设置")]
    [Tooltip("勾选后，Play时乌贼会出现在第一个路线点")]
    public bool startAtFirstPoint = true;

    private int currentPointIndex;
    private Quaternion visualStartRotation;
    private Vector3 previousForward;

    void Start()
    {
        if (pathPoints == null || pathPoints.Length < 2)
        {
            Debug.LogWarning("乌贼至少需要两个路线点。");
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

        if (squidVisual != null)
        {
            visualStartRotation = squidVisual.localRotation;
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

        Vector3 normalizedDirection = direction.normalized;

        float yaw = Mathf.Atan2(
            normalizedDirection.x,
            normalizedDirection.z
        ) * Mathf.Rad2Deg;

        float horizontalDistance = new Vector2(
            normalizedDirection.x,
            normalizedDirection.z
        ).magnitude;

        float pitch = -Mathf.Atan2(
            normalizedDirection.y * pitchStrength,
            horizontalDistance
        ) * Mathf.Rad2Deg;

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

    void ApplyBodyMovement()
    {
        if (squidVisual == null)
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

        squidVisual.localRotation = Quaternion.Slerp(
            squidVisual.localRotation,
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

            Gizmos.DrawWireSphere(
                pathPoints[i].position,
                0.04f
            );

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