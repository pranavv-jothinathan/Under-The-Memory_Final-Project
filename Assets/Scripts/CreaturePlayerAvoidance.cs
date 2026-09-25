using UnityEngine;

public class CreaturePlayerAvoidance : MonoBehaviour
{
    [Header("玩家")]
    [Tooltip("可以手动拖入玩家；不填写时会寻找带有 Player 标签的物体")]
    public Transform player;

    [Header("原来的路线移动脚本")]
    [Tooltip("把这个生物原来的路线移动脚本拖进来")]
    public MonoBehaviour normalMovementScript;

    [Header("躲避范围")]
    [Tooltip("玩家距离多近时开始逃跑")]
    public float detectionDistance = 1.5f;

    [Tooltip("玩家离开多远后，生物才恢复正常路线")]
    public float safeDistance = 2.2f;

    [Header("逃跑参数")]
    public float fleeSpeed = 0.3f;
    public float fleeTurnSpeed = 4f;

    [Tooltip("逃跑方向稍微向上，避免撞到地面")]
    public float upwardAmount = 0.15f;

    [Header("恢复路线")]
    [Tooltip("玩家离开后等待多久恢复原路线")]
    public float resumeDelay = 1f;

    [Header("上下倾斜限制")]
    [Range(0f, 1f)]
    [Tooltip("数值越小，生物越不会竖起来")]
    public float pitchStrength = 0.25f;

    private bool isAvoiding;
    private float resumeTimer;

    void Start()
    {
        // 如果没有手动拖入玩家，自动寻找 Player 标签
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                player = playerObject.transform;
            }
            else if (Camera.main != null)
            {
                // 找不到 Player 标签时，临时使用主摄影机
                player = Camera.main.transform;
            }
        }
    }

    void Update()
    {
        if (player == null)
        {
            return;
        }

        float distanceToPlayer = Vector3.Distance(
            transform.position,
            player.position
        );

        if (!isAvoiding)
        {
            if (distanceToPlayer <= detectionDistance)
            {
                BeginAvoidance();
            }
        }
        else
        {
            AvoidPlayer();

            if (distanceToPlayer >= safeDistance)
            {
                resumeTimer -= Time.deltaTime;

                if (resumeTimer <= 0f)
                {
                    EndAvoidance();
                }
            }
            else
            {
                // 玩家又靠近了，重新计算等待时间
                resumeTimer = resumeDelay;
            }
        }
    }

    void BeginAvoidance()
    {
        isAvoiding = true;
        resumeTimer = resumeDelay;

        // 暂时关闭原来的路线移动脚本，避免两个脚本抢控制权
        if (normalMovementScript != null)
        {
            normalMovementScript.enabled = false;
        }
    }

    void AvoidPlayer()
    {
        Vector3 awayDirection =
            transform.position - player.position;

        // 如果玩家和生物刚好重合，使用生物当前前方
        if (awayDirection.sqrMagnitude < 0.0001f)
        {
            awayDirection = transform.forward;
        }

        // 稍微向上逃，避免贴着地面
        awayDirection.y += upwardAmount;

        // 减弱上下倾斜，避免生物突然竖起来
        awayDirection.y *= pitchStrength;

        awayDirection.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(
            awayDirection,
            Vector3.up
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            fleeTurnSpeed * Time.deltaTime
        );

        transform.position +=
            transform.forward * fleeSpeed * Time.deltaTime;
    }

    void EndAvoidance()
    {
        isAvoiding = false;

        // 重新开启原来的路线移动脚本
        if (normalMovementScript != null)
        {
            normalMovementScript.enabled = true;
        }
    }

    void OnDisable()
    {
        // 避免物体关闭后，原来的路线脚本一直处于关闭状态
        if (normalMovementScript != null)
        {
            normalMovementScript.enabled = true;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            detectionDistance
        );

        Gizmos.DrawWireSphere(
            transform.position,
            safeDistance
        );
    }
}