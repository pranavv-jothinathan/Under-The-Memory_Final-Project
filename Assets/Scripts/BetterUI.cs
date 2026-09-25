using System.Collections;
using UnityEngine;

/// <summary>
/// 挂在展示 UI 的 Quad 本体上。
/// Quad 本身保持 Active，以便接收 UnityEvent / Event Wrapper 的 Show 调用。
/// 隐藏时只关闭 MeshRenderer，不禁用整个 GameObject。
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class UIBetter : MonoBehaviour
{
    [Header("Head Binding")]
    [Tooltip("玩家头部 / XR Camera 的 Transform。建议直接拖入。")]
    [SerializeField] private Transform head;

    [Tooltip("未手动指定 Head 时，自动使用 PlayerHead.Transform。")]
    [SerializeField] private bool usePlayerHeadFallback = true;

    [Header("Placement")]
    [Tooltip("UI 位于玩家头部前方的距离（米）。")]
    [SerializeField, Min(0.01f)] private float distanceFromHead = 1.2f;

    [Tooltip("相对视野中心的世界 Y 轴偏移；负数向下。")]
    [SerializeField] private float verticalOffset = -0.1f;

    [Tooltip("面板偏离视线超过此角度时，重新放回视野前方。")]
    [SerializeField, Range(0f, 180f)] private float reorientAngle = 35f;

    [Tooltip("玩家离上次锚点超过此距离时，重新放回视野前方。")]
    [SerializeField, Min(0f)] private float maxDrift = 1.5f;

    [Tooltip("UI 追回新锚点时的位置与旋转平滑速度。")]
    [SerializeField, Min(0f)] private float followSpeed = 4f;

    [Tooltip("若 Quad 正反面显示错误，勾选此项翻转 180 度。")]
    [SerializeField] private bool flipFacing;

    [Header("Display")]
    [Tooltip("弹出动画完成后，UI 保持显示的时长（秒）。")]
    [SerializeField, Min(0f)] private float duration = 3f;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float popInSeconds = 0.25f;
    [SerializeField, Min(0f)] private float popOutSeconds = 0.2f;

    [Header("Behaviour")]
    [Tooltip("勾选后，本次运行期间只能显示一次。")]
    [SerializeField] private bool showOnce = true;

    private MeshRenderer meshRenderer;
    private Vector3 originalScale;

    private Vector3 anchorPosition;
    private Vector3 anchorHeadPosition;

    private bool isVisible;
    private bool isPlaying;
    private bool hasShown;
    private Coroutine playRoutine;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        originalScale = transform.localScale;

        // 不要 gameObject.SetActive(false)：
        // 本脚本挂在 Quad 自己身上，禁用自己后外部事件无法正常启动协程。
        transform.localScale = Vector3.zero;
        SetVisualVisible(false);
    }

    private void Update()
    {
        if (!isVisible)
            return;

        Transform currentHead = GetHead();
        if (currentHead == null)
            return;

        // 保留原 StoryTipsPresenter 的逻辑：
        // 只有转头过大或移动过远时，才重新设置面板锚点。
        Vector3 toPanel = anchorPosition - currentHead.position;

        float offAxis = toPanel.sqrMagnitude > 0.0001f
            ? Vector3.Angle(currentHead.forward, toPanel)
            : 0f;

        bool drifted = Vector3.Distance(
            currentHead.position,
            anchorHeadPosition) > maxDrift;

        if (offAxis > reorientAngle || drifted)
        {
            anchorHeadPosition = currentHead.position;
            anchorPosition = DesiredPosition(currentHead);
        }

        float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);

        transform.position = Vector3.Lerp(
            transform.position,
            anchorPosition,
            k);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            FaceRotation(currentHead),
            k);
    }

    /// <summary>
    /// 由 UnityEvent、Event Wrapper 或其他脚本调用。
    /// </summary>
    public void Show()
    {
        if (isPlaying)
            return;

        if (showOnce && hasShown)
            return;

        playRoutine = StartCoroutine(PlayRoutine());
    }

    /// <summary>
    /// 忽略 Show Once 限制，强制再播放一次。
    /// </summary>
    public void ShowForce()
    {
        if (isPlaying)
            return;

        playRoutine = StartCoroutine(PlayRoutine());
    }

    /// <summary>
    /// 允许 Show Once 类型的 UI 在本局再次被 Show 调用。
    /// </summary>
    public void ResetShowOnce()
    {
        hasShown = false;
    }

    /// <summary>
    /// 立刻停止并隐藏 UI，不播放缩小动画。
    /// </summary>
    public void HideImmediately()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        isVisible = false;
        isPlaying = false;

        transform.localScale = Vector3.zero;
        SetVisualVisible(false);
    }

    private IEnumerator PlayRoutine()
    {
        isPlaying = true;
        hasShown = true;

        Transform currentHead = GetHead();

        if (currentHead == null)
        {
            Debug.LogWarning(
                "UIBetter：未绑定 Head，且无法找到 PlayerHead.Transform。",
                this);

            isPlaying = false;
            playRoutine = null;
            yield break;
        }

        // 与旧 Presenter 一样：出现的第一帧直接放到当前视野，
        // 不从上一次隐藏的位置飞过来。
        SnapToView(currentHead);

        transform.localScale = Vector3.zero;
        SetVisualVisible(true);
        isVisible = true;

        yield return Pop(Vector3.zero, originalScale, popInSeconds);

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        yield return Pop(originalScale, Vector3.zero, popOutSeconds);

        isVisible = false;
        isPlaying = false;
        playRoutine = null;

        SetVisualVisible(false);
    }

    private void SnapToView(Transform currentHead)
    {
        anchorHeadPosition = currentHead.position;
        anchorPosition = DesiredPosition(currentHead);

        transform.position = anchorPosition;
        transform.rotation = FaceRotation(currentHead);
    }

    private Vector3 DesiredPosition(Transform currentHead)
    {
        return currentHead.position
             + currentHead.forward * distanceFromHead
             + Vector3.up * verticalOffset;
    }

    private Quaternion FaceRotation(Transform currentHead)
    {
        Vector3 look = transform.position - currentHead.position;

        if (look.sqrMagnitude < 0.0001f)
            look = currentHead.forward;

        Quaternion rotation = Quaternion.LookRotation(
            look.normalized,
            Vector3.up);

        return flipFacing
            ? rotation * Quaternion.Euler(0f, 180f, 0f)
            : rotation;
    }

    private IEnumerator Pop(Vector3 from, Vector3 to, float seconds)
    {
        transform.localScale = from;

        if (seconds <= 0f)
        {
            transform.localScale = to;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(elapsed / seconds));

            transform.localScale = Vector3.Lerp(from, to, t);

            yield return null;
        }

        transform.localScale = to;
    }

    private void SetVisualVisible(bool visible)
    {
        if (meshRenderer != null)
            meshRenderer.enabled = visible;
    }

    private Transform GetHead()
    {
        if (head != null)
            return head;

        if (usePlayerHeadFallback)
            return PlayerHead.Transform;

        return null;
    }
}
