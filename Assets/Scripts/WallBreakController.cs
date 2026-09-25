using System.Collections;
using UnityEngine;

public class WallBreakController : MonoBehaviour
{
    [Header("完整墙体")]
    public GameObject wallIntact;

    [Tooltip("墙体碎块父物体，例如 WallChunks")]
    public GameObject wallBroken;

    [Header("参与轻微抖动的建筑物")]
    [Tooltip("拖入完整假墙、左墙、右墙、天花板等")]
    public Transform[] shakeObjects;

    [Tooltip("不同物体抖动节奏的错开程度")]
    [Range(0f, 1f)]
    public float shakeVariation = 0.1f;

    [Header("抖动结束后保留歪斜的物体")]
    [Tooltip("通常只拖入左墙和右墙")]
    public Transform[] damagedShakeObjects;

    [Tooltip("最终倾斜最小角度")]
    public float finalTiltMin = 4f;

    [Tooltip("最终倾斜最大角度")]
    public float finalTiltMax = 8f;

    [Tooltip("最终轻微位置偏移")]
    public float finalPositionOffset = 0.025f;

    [Header("第一批小烟尘")]
    public ParticleSystem[] firstSmallDusts;

    [Header("主体大烟尘")]
    [Tooltip("可放一个长条烟尘，也可以放多个主体烟尘")]
    public ParticleSystem[] mainDusts;

    [Tooltip("多个主体烟尘之间的播放间隔")]
    public float mainDustInterval = 0.12f;

    [Header("第二批小烟尘")]
    public ParticleSystem[] secondSmallDusts;

    [Header("持续掉落的小碎石")]
    public Rigidbody[] smallFallingRocks;

    [Header("最后掉落的大碎石")]
    public Rigidbody[] bigFallingRocks;

    [Header("墙体碎块")]
    [Tooltip("按照希望的倒塌顺序拖入墙块")]
    public Rigidbody[] wallChunks;

    [Tooltip("每块墙体开始倒塌的间隔")]
    public float wallChunkInterval = 0.3f;

    [Tooltip("墙块向外倒塌的力量")]
    public float wallChunkOutwardForce = 0.08f;

    [Tooltip("墙块向下坠落的初始力量")]
    public float wallChunkDownwardForce = 0.3f;

    [Tooltip("墙块旋转力度")]
    public float wallChunkTorque = 0.25f;

    [Header("动画时间")]
    [Tooltip("第一批小烟尘出现后，多久开始主体烟尘")]
    public float mainDustDelay = 1.2f;

    [Tooltip("主体烟尘开始后，第一批小烟尘继续保持多久")]
    public float firstSmallDustOverlapDuration = 1.5f;

    [Tooltip("第二批小烟尘出现后，多久隐藏完整假墙")]
    public float wallDisappearDelay = 3f;

    [Tooltip("完整假墙隐藏后，多久开始倒塌墙块")]
    public float wallChunkStartDelay = 0.2f;

    [Header("碎石掉落时间")]
    [Tooltip("第一批小烟尘出现后，多久开始掉小碎石")]
    public float smallRockStartDelay = 0.7f;

    [Tooltip("每块小碎石之间的掉落间隔")]
    public float smallRockInterval = 0.65f;

    [Header("建筑轻微抖动")]
    [Tooltip("位置抖动幅度。建议 0.0015 到 0.0035")]
    public float shakeStrength = 0.0025f;

    [Tooltip("抖动速度。较高数值更像颤动而不是摇摆")]
    public float shakeSpeed = 55f;

    [Tooltip("抖动强度是否逐渐增加")]
    public bool increaseShakeOverTime = true;

    [Header("普通碎石运动")]
    public float downwardSpeed = 1.5f;
    public float outwardForce = 0.1f;
    public float randomForce = 0.08f;
    public float rockTorque = 0.3f;

    [Header("临时测试")]
    public KeyCode testKey = KeyCode.K;

    private bool hasBroken;
    private bool isShaking;

    private float shakeTimer;
    private float totalSequenceDuration;

    private Vector3 wallStartPosition;

    private Vector3[] shakeStartPositions;
    private Quaternion[] shakeStartRotations;

    private void Start()
    {
        if (wallIntact != null)
        {
            wallIntact.SetActive(true);
            wallStartPosition = wallIntact.transform.localPosition;
        }

        if (wallBroken != null)
        {
            wallBroken.SetActive(false);
        }

        PrepareShakeObjects();

        StopAndClearAllParticles();

        PrepareRockGroup(smallFallingRocks);
        PrepareRockGroup(bigFallingRocks);
        PrepareRockGroup(wallChunks);

        totalSequenceDuration =
            mainDustDelay +
            firstSmallDustOverlapDuration +
            wallDisappearDelay;
    }

    private void Update()
    {
        if (Input.GetKeyDown(testKey))
        {
            StartWallBreak();
        }

        UpdateWallShake();
    }

    public void StartWallBreak()
    {
        if (hasBroken)
            return;

        if (wallIntact == null)
            return;

        hasBroken = true;
        StartCoroutine(WallBreakSequence());
    }

    private IEnumerator WallBreakSequence()
    {
        // 1. 第一批小烟尘出现
        PlayParticleGroup(firstSmallDusts);

        isShaking = true;
        shakeTimer = 0f;

        // 小碎石陆续掉落
        StartCoroutine(ReleaseSmallRocksSequence());

        // 2. 等待主体烟尘出现
        yield return new WaitForSeconds(mainDustDelay);

        yield return StartCoroutine(PlayMainDustSequence());

        // 3. 第一批小烟尘和主体烟尘重叠
        yield return new WaitForSeconds(
            firstSmallDustOverlapDuration
        );

        PlayParticleGroup(secondSmallDusts);

        StopParticleGroup(firstSmallDusts);

        // 4. 等主体烟尘达到较浓状态
        yield return new WaitForSeconds(
            wallDisappearDelay
        );

        isShaking = false;

        // 假墙和天花板恢复原位，左右墙保留歪斜
        FinishShakeObjects();

        // 隐藏完整假墙
        if (wallIntact != null)
        {
            wallIntact.transform.localPosition =
                wallStartPosition;

            wallIntact.SetActive(false);
        }

        // 显示墙体碎块
        if (wallBroken != null)
        {
            wallBroken.SetActive(true);
        }

        yield return null;

        yield return new WaitForSeconds(
            wallChunkStartDelay
        );

        // 5. 墙块依次坍塌
        StartCoroutine(ReleaseWallChunksSequence());

        // 最后的大碎石一起掉落
        ReleaseRockGroup(bigFallingRocks);

        // 第二批小烟尘停止继续发射
        StopParticleGroup(secondSmallDusts);
    }

    private void PrepareShakeObjects()
    {
        if (shakeObjects == null)
            return;

        shakeStartPositions =
            new Vector3[shakeObjects.Length];

        shakeStartRotations =
            new Quaternion[shakeObjects.Length];

        for (int i = 0; i < shakeObjects.Length; i++)
        {
            Transform target = shakeObjects[i];

            if (target == null)
                continue;

            shakeStartPositions[i] =
                target.localPosition;

            shakeStartRotations[i] =
                target.localRotation;
        }
    }

    private void UpdateWallShake()
    {
        if (!isShaking)
            return;

        shakeTimer += Time.deltaTime;

        float currentStrength = shakeStrength;

        if (
            increaseShakeOverTime &&
            totalSequenceDuration > 0f
        )
        {
            float progress =
                Mathf.Clamp01(
                    shakeTimer /
                    totalSequenceDuration
                );

            currentStrength =
                Mathf.Lerp(
                    shakeStrength * 0.35f,
                    shakeStrength,
                    progress
                );
        }

        if (
            shakeObjects == null ||
            shakeStartPositions == null ||
            shakeStartRotations == null
        )
            return;

        for (int i = 0; i < shakeObjects.Length; i++)
        {
            Transform target = shakeObjects[i];

            if (target == null)
                continue;

            float phase =
                i * shakeVariation * 2.5f;

            // 三个方向使用不同频率，形成细微、不规则的颤动
            float offsetX =
                Mathf.Sin(
                    shakeTimer * shakeSpeed +
                    phase
                ) * currentStrength;

            float offsetY =
                Mathf.Sin(
                    shakeTimer *
                    shakeSpeed *
                    1.37f +
                    phase
                ) *
                currentStrength *
                0.65f;

            float offsetZ =
                Mathf.Sin(
                    shakeTimer *
                    shakeSpeed *
                    1.81f +
                    phase
                ) *
                currentStrength *
                0.3f;

            target.localPosition =
                shakeStartPositions[i] +
                new Vector3(
                    offsetX,
                    offsetY,
                    offsetZ
                );

            // 抖动过程中完全不旋转，避免出现左右摇摆
            target.localRotation =
                shakeStartRotations[i];
        }
    }

    private void FinishShakeObjects()
    {
        if (
            shakeObjects == null ||
            shakeStartPositions == null ||
            shakeStartRotations == null
        )
            return;

        for (int i = 0; i < shakeObjects.Length; i++)
        {
            Transform target = shakeObjects[i];

            if (target == null)
                continue;

            bool shouldStayDamaged =
                IsDamagedShakeObject(target);

            if (!shouldStayDamaged)
            {
                target.localPosition =
                    shakeStartPositions[i];

                target.localRotation =
                    shakeStartRotations[i];

                continue;
            }

            Vector3 randomOffset =
                new Vector3(
                    Random.Range(
                        -finalPositionOffset,
                        finalPositionOffset
                    ),
                    Random.Range(
                        -finalPositionOffset * 0.4f,
                        finalPositionOffset * 0.4f
                    ),
                    Random.Range(
                        -finalPositionOffset,
                        finalPositionOffset
                    )
                );

            target.localPosition =
                shakeStartPositions[i] +
                randomOffset;

            float direction =
                Random.value < 0.5f
                    ? -1f
                    : 1f;

            float zTilt =
                Random.Range(
                    finalTiltMin,
                    finalTiltMax
                ) * direction;

            float xTilt =
                Random.Range(
                    -finalTiltMax * 0.2f,
                    finalTiltMax * 0.2f
                );

            float yTilt =
                Random.Range(
                    -finalTiltMax * 0.1f,
                    finalTiltMax * 0.1f
                );

            target.localRotation =
                shakeStartRotations[i] *
                Quaternion.Euler(
                    xTilt,
                    yTilt,
                    zTilt
                );
        }
    }

    private bool IsDamagedShakeObject(
        Transform target
    )
    {
        if (damagedShakeObjects == null)
            return false;

        foreach (
            Transform damagedObject in damagedShakeObjects
        )
        {
            if (damagedObject == target)
                return true;
        }

        return false;
    }

    private IEnumerator PlayMainDustSequence()
    {
        if (mainDusts == null)
            yield break;

        foreach (ParticleSystem dust in mainDusts)
        {
            if (dust != null)
            {
                dust.Play();
            }

            if (mainDustInterval > 0f)
            {
                yield return new WaitForSeconds(
                    mainDustInterval
                );
            }
        }
    }

    private IEnumerator ReleaseSmallRocksSequence()
    {
        yield return new WaitForSeconds(
            smallRockStartDelay
        );

        if (smallFallingRocks == null)
            yield break;

        foreach (Rigidbody rock in smallFallingRocks)
        {
            ReleaseOneRock(rock);

            yield return new WaitForSeconds(
                smallRockInterval
            );
        }
    }

    private IEnumerator ReleaseWallChunksSequence()
    {
        if (wallChunks == null)
            yield break;

        foreach (Rigidbody chunk in wallChunks)
        {
            ReleaseWallChunk(chunk);

            yield return new WaitForSeconds(
                wallChunkInterval
            );
        }
    }

    private void ReleaseWallChunk(
        Rigidbody chunk
    )
    {
        if (chunk == null)
            return;

        chunk.isKinematic = false;
        chunk.useGravity = true;
        chunk.WakeUp();

        Vector3 outwardDirection =
            wallIntact != null
                ? wallIntact.transform.forward
                : transform.forward;

        Vector3 force =
            Vector3.down *
            wallChunkDownwardForce +
            outwardDirection *
            wallChunkOutwardForce +
            Random.insideUnitSphere *
            randomForce;

        if (force.y > 0f)
        {
            force.y =
                -Mathf.Abs(force.y);
        }

        chunk.AddForce(
            force,
            ForceMode.Impulse
        );

        chunk.AddTorque(
            Random.insideUnitSphere *
            wallChunkTorque,
            ForceMode.Impulse
        );
    }

    private void ReleaseRockGroup(
        Rigidbody[] rocks
    )
    {
        if (rocks == null)
            return;

        foreach (Rigidbody rock in rocks)
        {
            ReleaseOneRock(rock);
        }
    }

    private void ReleaseOneRock(
        Rigidbody rock
    )
    {
        if (rock == null)
            return;

        rock.isKinematic = false;
        rock.useGravity = true;
        rock.WakeUp();

        rock.linearVelocity =
            Vector3.down *
            downwardSpeed;

        Vector3 outwardDirection =
            wallIntact != null
                ? wallIntact.transform.forward
                : transform.forward;

        Vector3 force =
            outwardDirection *
            outwardForce +
            Random.insideUnitSphere *
            randomForce;

        if (force.y > 0f)
        {
            force.y *= 0.2f;
        }

        rock.AddForce(
            force,
            ForceMode.Impulse
        );

        rock.AddTorque(
            Random.insideUnitSphere *
            rockTorque,
            ForceMode.Impulse
        );
    }

    private void PrepareRockGroup(
        Rigidbody[] rocks
    )
    {
        if (rocks == null)
            return;

        foreach (Rigidbody rock in rocks)
        {
            if (rock == null)
                continue;

            rock.isKinematic = true;
            rock.useGravity = true;

            rock.linearVelocity =
                Vector3.zero;

            rock.angularVelocity =
                Vector3.zero;
        }
    }

    private void PlayParticleGroup(
        ParticleSystem[] particles
    )
    {
        if (particles == null)
            return;

        foreach (ParticleSystem particle in particles)
        {
            if (particle != null)
            {
                particle.Play();
            }
        }
    }

    private void StopParticleGroup(
        ParticleSystem[] particles
    )
    {
        if (particles == null)
            return;

        foreach (ParticleSystem particle in particles)
        {
            if (particle == null)
                continue;

            particle.Stop(
                true,
                ParticleSystemStopBehavior
                    .StopEmitting
            );
        }
    }

    private void StopAndClearAllParticles()
    {
        StopAndClearParticleGroup(
            firstSmallDusts
        );

        StopAndClearParticleGroup(
            mainDusts
        );

        StopAndClearParticleGroup(
            secondSmallDusts
        );
    }

    private void StopAndClearParticleGroup(
        ParticleSystem[] particles
    )
    {
        if (particles == null)
            return;

        foreach (ParticleSystem particle in particles)
        {
            if (particle == null)
                continue;

            particle.Stop(
                true,
                ParticleSystemStopBehavior
                    .StopEmittingAndClear
            );
        }
    }
}