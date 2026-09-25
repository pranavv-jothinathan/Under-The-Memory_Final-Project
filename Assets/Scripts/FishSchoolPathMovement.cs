using UnityEngine;

public class FishSchoolPathMovement : MonoBehaviour
{
    [Header("路线点，按照顺序拖入")]
    public Transform[] pathPoints;

    [Header("鱼群身体模型")]
    [Tooltip("拖入装着五只鱼的子物体。不填写时，轻微侧倾会直接作用在 FishSchool 上。")]
    public Transform fishVisual;

    [Header("移动参数")]
    public float moveSpeed = 0.05f;
    public float rotateSpeed = 1.4f;

    [Header("提前转向")]
    [Tooltip("距离路线点还有多远时，提前切换到下一个点")]
    public float lookAheadDistance = 0.18f;

    [Header("上下倾斜")]
    [Range(0f, 1f)]
    [Tooltip("数值越大，鱼群向上或向下游时倾斜越明显")]
    public float pitchStrength = 0.2f;

    [Header("转弯时身体轻微侧倾")]
    public float bankAmount = 7f;
    public float bankSmooth = 2f;

    [Header("自然轻微摆动")]
    public float bodySwayAmount = 1.5f;
    public float bodySwaySpeed = 1f;

    [Header("开始设置")]
    [Tooltip("勾选后，Play 时鱼群会直接出现在第一个路线点")]
    public bool startAtFirstPoint = true;

    private int currentPointIndex = 0;
    private Quaternion visualStartRotation;
    private Vector3 previousForward;

    void Start()
    {
        if (pathPoints == null || pathPoints.Length < 2)
        {
            Debug.LogWarning("鱼群至少需要两个路线点。");
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
        else
        {
            visualStartRotation = transform.localRotation;
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
        float distance = direction.magnitude;

        // 提前切换目标点，让转弯更圆滑
        if (distance <= lookAheadDistance)
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

        // 减弱上下倾斜，避免鱼群突然竖起来
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
        Transform visual = fishVisual != null ? fishVisual : transform;

        float turnAmount = Vector3.SignedAngle(
            previousForward,
            transform.forward,
            Vector3.up
        );

        // 左右转弯时轻微侧倾
        float bank = Mathf.Clamp(
            -turnAmount * bankAmount,
            -bankAmount,
            bankAmount
        );

        // 轻微左右摆动
        float sway = Mathf.Sin(
            Time.time * bodySwaySpeed
        ) * bodySwayAmount;

        Quaternion targetLocalRotation =
            visualStartRotation *
            Quaternion.Euler(0f, sway, bank);

        visual.localRotation = Quaternion.Slerp(
            visual.localRotation,
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

            int nextIndex = i + 1;

            if (nextIndex >= pathPoints.Length)
            {
                nextIndex = 0;
            }

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