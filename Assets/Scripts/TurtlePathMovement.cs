using UnityEngine;

public class TurtlePathMovement : MonoBehaviour
{
    [Header("路线点")]
    public Transform[] pathPoints;

    [Header("移动参数")]
    public float moveSpeed = 0.05f;
    public float rotateSpeed = 1.5f;
    public float lookAheadDistance = 0.4f;

    [Header("上下倾斜")]
    [Range(0f, 1f)]
    public float pitchStrength = 0.15f;

    [Header("海龟身体")]
    public Transform turtleVisual;

    [Header("转弯侧倾")]
    public float bankAmount = 12f;
    public float bankSmooth = 2f;

    [Header("轻微摆动")]
    public float bodySwayAmount = 2f;
    public float bodySwaySpeed = 1.2f;

    private int currentPointIndex = 0;
    private Quaternion visualStartRotation;
    private Vector3 previousForward;

    void Start()
    {
        if (pathPoints == null || pathPoints.Length < 2)
        {
            Debug.LogWarning("海龟至少需要两个路线点。");
            enabled = false;
            return;
        }

        previousForward = transform.forward;

        if (turtleVisual != null)
        {
            visualStartRotation = turtleVisual.localRotation;
        }
    }

    void Update()
    {
        Transform currentPoint = pathPoints[currentPointIndex];
        Vector3 direction = currentPoint.position - transform.position;

        if (direction.magnitude <= lookAheadDistance)
        {
            currentPointIndex++;

            if (currentPointIndex >= pathPoints.Length)
            {
                currentPointIndex = 0;
            }

            currentPoint = pathPoints[currentPointIndex];
            direction = currentPoint.position - transform.position;
        }

        RotateMover(direction);

        transform.position = Vector3.MoveTowards(
            transform.position,
            currentPoint.position,
            moveSpeed * Time.deltaTime
        );

        ApplyBodyBank();
    }

    void RotateMover(Vector3 direction)
    {
        Vector3 adjustedDirection = direction;
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

    void ApplyBodyBank()
    {
        if (turtleVisual == null)
        {
            return;
        }

        float turnDirection = Vector3.SignedAngle(
            previousForward,
            transform.forward,
            Vector3.up
        );

        float bank = Mathf.Clamp(
            -turnDirection * bankAmount,
            -bankAmount,
            bankAmount
        );

        float sway = Mathf.Sin(
            Time.time * bodySwaySpeed
        ) * bodySwayAmount;

        Quaternion targetLocalRotation =
            visualStartRotation *
            Quaternion.Euler(0f, sway, bank);

        turtleVisual.localRotation = Quaternion.Slerp(
            turtleVisual.localRotation,
            targetLocalRotation,
            bankSmooth * Time.deltaTime
        );

        previousForward = transform.forward;
    }
}