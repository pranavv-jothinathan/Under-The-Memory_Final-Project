using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class UIScript : MonoBehaviour
{
    [Serializable]
    public class TriggerEvent : UnityEvent<Collider> { }

    [Header("Trigger Events")]
    public TriggerEvent onTriggerEnterEvent;
    public TriggerEvent onTriggerExitEvent;

    [Header("Target Object")]
    [SerializeField] private GameObject targetObject;

    [Header("Scale Settings")]
    [SerializeField] private Vector3 targetScale = Vector3.one;
    [SerializeField] private float transitionDuration = 1f;

    [Tooltip("进入 Trigger 后，等待多少秒恢复原始大小")]
    [SerializeField] private float restoreDelay = 3f;

    [Header("Material Settings")]
    [SerializeField] private Material enterMaterial;
    [SerializeField] private Material exitMaterial;

    private Animator animator;
    private AudioSource audioSource;
    public AudioClip audioClip;

    private Vector3 initialScale;
    private Coroutine scaleCoroutine;
    private Coroutine restoreCoroutine;
    private Renderer targetRenderer;

    private void Start()
    {
        animator = GetComponent<Animator>();

        if (animator != null)
            animator.SetBool("TipTrigger", false);

        audioSource = GetComponent<AudioSource>();

        if (targetObject != null)
        {
            initialScale = targetObject.transform.localScale;
            targetRenderer = targetObject.GetComponent<Renderer>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        onTriggerEnterEvent?.Invoke(other);

        if (animator != null)
            animator.SetBool("TipTrigger", true);

        if (audioSource != null && audioClip != null)
            audioSource.PlayOneShot(audioClip);

        if (targetObject != null)
        {
            // 先变为目标大小
            StartScaleTransition(targetScale);

            // 应用进入时材质
            ChangeMaterial(enterMaterial);

            // 若之前已有恢复倒计时，先取消，重新开始计时
            if (restoreCoroutine != null)
                StopCoroutine(restoreCoroutine);

            restoreCoroutine = StartCoroutine(RestoreScaleAfterDelay());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        onTriggerExitEvent?.Invoke(other);

        if (animator != null)
            animator.SetBool("TipTrigger", false);

        if (audioSource != null && audioClip != null)
            audioSource.PlayOneShot(audioClip);

        // 离开 Trigger 时不恢复 Scale
        // Scale 会在进入后经过 restoreDelay 秒自动恢复。

        if (targetObject != null)
        {
            ChangeMaterial(exitMaterial);
        }
    }

    private IEnumerator RestoreScaleAfterDelay()
    {
        yield return new WaitForSeconds(restoreDelay);

        StartScaleTransition(initialScale);
        restoreCoroutine = null;
    }

    private void StartScaleTransition(Vector3 endScale)
    {
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        scaleCoroutine = StartCoroutine(LerpScale(endScale, transitionDuration));
    }

    private IEnumerator LerpScale(Vector3 endScale, float duration)
    {
        Vector3 startScale = targetObject.transform.localScale;

        if (duration <= 0f)
        {
            targetObject.transform.localScale = endScale;
            yield break;
        }

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            targetObject.transform.localScale =
                Vector3.Lerp(startScale, endScale, t);

            yield return null;
        }

        targetObject.transform.localScale = endScale;
        scaleCoroutine = null;
    }

    private void ChangeMaterial(Material newMaterial)
    {
        if (targetRenderer == null)
        {
            Debug.LogWarning("Target Object none");
            return;
        }

        if (newMaterial == null)
        {
            Debug.LogWarning("Material none");
            return;
        }

        targetRenderer.material = newMaterial;
    }
}

