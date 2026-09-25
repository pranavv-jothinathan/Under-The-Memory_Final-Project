using UnityEngine;

public class FishAroundCoral : MonoBehaviour
{
    [Header("路线点")]
    public Transform frontStartPoint;
    public Transform touchPoint1;
    public Transform backOffPoint;
    public Transform touchPoint2;
    public Transform sideAroundPoint;
    public Transform backSwimPoint;
    public Transform mouthPoint;

    [Header("移动参数")]
    public float swimSpeed = 0.42f;
    public float rotateSpeed = 1.1f;
    public float acceleration = 1.8f;

    [Header("到达判断")]
    public float arriveDistance = 0.28f;
    public float touchDistance = 0.18f;

    [Header("自然游动")]
    public float speedWaveAmount = 0.15f;
    public float bobAmount = 0.035f;
    public float swayAmount = 0.05f;
    public float naturalMoveSpeed = 2.4f;

    [Header("触碰动作")]
    public float peckSlowDown = 0.65f;
    public float peckTurnSpeed = 1.4f;

    [Header("循环设置")]
    public bool loop = true;

    [Header("调试用，不用改")]
    public string currentStateName;

    private float currentSpeed = 0f;
    private float randomOffset;

    private enum FishState
    {
        SwimToFront,
        MoveToTouch1,
        MoveToBackOff,
        MoveToTouch2,
        MoveToSideAround,
        MoveToBack
    }

    private FishState state = FishState.SwimToFront;

    void Start()
    {
        randomOffset = Random.Range(0f, 100f);
    }

    void OnEnable()
    {
        state = FishState.SwimToFront;
        currentSpeed = 0f;
    }

    void Update()
    {
        currentStateName = state.ToString();

        if (frontStartPoint == null ||
            touchPoint1 == null ||
            backOffPoint == null ||
            touchPoint2 == null ||
            sideAroundPoint == null ||
            backSwimPoint == null ||
            mouthPoint == null)
        {
            return;
        }

        switch (state)
        {
            case FishState.SwimToFront:
                MoveBodyTo(frontStartPoint.position, swimSpeed);

                if (Vector3.Distance(transform.position, frontStartPoint.position) <= arriveDistance)
                {
                    state = FishState.MoveToTouch1;
                }
                break;

            case FishState.MoveToTouch1:
                MoveMouthTo(touchPoint1.position, swimSpeed * peckSlowDown);

                if (Vector3.Distance(mouthPoint.position, touchPoint1.position) <= touchDistance)
                {
                    state = FishState.MoveToBackOff;
                }
                break;

            case FishState.MoveToBackOff:
                MoveBodyTo(backOffPoint.position, swimSpeed);

                if (Vector3.Distance(transform.position, backOffPoint.position) <= arriveDistance)
                {
                    state = FishState.MoveToTouch2;
                }
                break;

            case FishState.MoveToTouch2:
                MoveMouthTo(touchPoint2.position, swimSpeed * peckSlowDown);

                if (Vector3.Distance(mouthPoint.position, touchPoint2.position) <= touchDistance)
                {
                    state = FishState.MoveToSideAround;
                }
                break;

            case FishState.MoveToSideAround:
                MoveBodyTo(sideAroundPoint.position, swimSpeed);

                if (Vector3.Distance(transform.position, sideAroundPoint.position) <= arriveDistance)
                {
                    state = FishState.MoveToBack;
                }
                break;

            case FishState.MoveToBack:
                MoveBodyTo(backSwimPoint.position, swimSpeed);

                if (Vector3.Distance(transform.position, backSwimPoint.position) <= arriveDistance)
                {
                    if (loop)
                    {
                        state = FishState.SwimToFront;
                    }
                    else
                    {
                        currentSpeed = 0f;
                    }
                }
                break;
        }
    }

    void MoveMouthTo(Vector3 targetMouthPosition, float targetSpeed)
    {
        Vector3 mouthOffset = mouthPoint.position - transform.position;
        Vector3 targetBodyPosition = targetMouthPosition - mouthOffset;

        MoveBodyTo(targetBodyPosition, targetSpeed, true);
    }

    void MoveBodyTo(Vector3 target, float targetSpeed, bool isTouching = false)
    {
        Vector3 direction = target - transform.position;

        if (direction.magnitude > 0.01f)
        {
            LookAt(target, isTouching ? peckTurnSpeed : rotateSpeed);

            float speedWave = 1f + Mathf.Sin((Time.time + randomOffset) * 3f) * speedWaveAmount;

            Vector3 bobbing = transform.up *
                Mathf.Sin((Time.time + randomOffset) * naturalMoveSpeed) *
                bobAmount;

            Vector3 swaying = transform.right *
                Mathf.Sin((Time.time + randomOffset) * naturalMoveSpeed * 1.35f) *
                swayAmount;

            Vector3 naturalTarget = target + bobbing + swaying;

            float finalTargetSpeed = targetSpeed * speedWave;

            currentSpeed = Mathf.Lerp(
                currentSpeed,
                finalTargetSpeed,
                acceleration * Time.deltaTime
            );

            transform.position = Vector3.MoveTowards(
                transform.position,
                naturalTarget,
                currentSpeed * Time.deltaTime
            );
        }
    }

    void LookAt(Vector3 target, float turnSpeed)
    {
        Vector3 direction = target - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }
    }
}