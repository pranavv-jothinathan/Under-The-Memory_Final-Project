using System.Collections;
using UnityEngine;

public class BridgeSequence : MonoBehaviour
{
    [Header("Bridge")]
    [SerializeField] private Animator bridgeAnimator;

    [Header("Structure Shake")]
    [SerializeField] private Animator structureShakeAnimator;
    [SerializeField] private StructureShakeSound structureShakeSound;

    [Header("Falling structure")]
    [SerializeField] private Rigidbody fallingStructure;

    [Tooltip("애니메이션 시작 후 구조물이 떨어질 때까지 기다리는 시간")]
    [SerializeField] private float releaseDelay = 2.8f;

    [Tooltip("구조물이 떨어지기 시작할 방향과 힘")]
    [SerializeField] private Vector3 releaseForce = new Vector3(3f, -0.5f, 0f);

    [Tooltip("구조물이 회전하며 떨어지는 힘")]
    [SerializeField] private Vector3 releaseTorque = new Vector3(0f, 0f, 3f);

    private bool activated;

    private void Awake()
    {
        if (fallingStructure == null)
        {
            Debug.LogError("Falling Structure Rigidbody가 연결되지 않았습니다.");
            enabled = false;
            return;
        }

        fallingStructure.isKinematic = true;
        fallingStructure.useGravity = false;
    }

    private void Update()
    {
        // 임시 테스트: 스페이스바를 누르면 실행
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ActivateBridge();
        }
    }

    public void ActivateBridge()
    {
    if (activated)
        return;

    activated = true;

    // 흔들림 애니메이션 멈추기
    if (structureShakeAnimator != null)
    {
        structureShakeAnimator.SetTrigger("StopShake");
    }

    // 흔들림 사운드 멈추기
    if (structureShakeSound != null)
    {
        structureShakeSound.StopShakeSound();
    }

    // 다리 올리기
    bridgeAnimator.SetTrigger("RaiseBridge");

    // 일정 시간 후 돌 떨어뜨리기
    StartCoroutine(ReleaseAfterDelay());
    }
    
    private IEnumerator ReleaseAfterDelay()
    {
        yield return new WaitForSeconds(releaseDelay);

        Transform structureTransform = fallingStructure.transform;

        // 다리의 자식에서 분리하되 현재 월드 위치는 유지
        structureTransform.SetParent(null, true);

        fallingStructure.isKinematic = false;
        fallingStructure.useGravity = true;

        // 현재 구조물 방향을 기준으로 힘 적용
        Vector3 worldForce =
            structureTransform.TransformDirection(releaseForce);

        Vector3 worldTorque =
            structureTransform.TransformDirection(releaseTorque);

        fallingStructure.AddForce(worldForce, ForceMode.Impulse);
        fallingStructure.AddTorque(worldTorque, ForceMode.Impulse);
    }
}