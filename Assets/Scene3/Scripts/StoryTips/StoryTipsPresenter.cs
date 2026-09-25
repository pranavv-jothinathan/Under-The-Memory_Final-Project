using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一条剧情提示的内容和时长。
///
/// 有语音时面板时长跟着语音走（语音长度 + 尾巴），换配音不用回来改秒数。
///
/// 放在 Presenter 这个文件里而不是单独一个 .cs：单独放的时候 Unity 的资源库反复漏掉
/// 那个文件，没把它算进 Assembly-CSharp（Library/Bee 的源清单里没有它），Ctrl+R 和
/// Reimport 都没用。跟着唯一的使用者走，省掉这个坑。
/// </summary>
[System.Serializable]
public class StoryTipDefinition
{
    public StoryTipId id = StoryTipId.None;

    [Header("Content")]
    [Tooltip("提示贴图。面板高宽比按贴图尺寸自动算。")]
    public Texture2D image;

    [Tooltip("同时播放的语音，可以留空。")]
    public AudioClip voice;

    [Header("Timing")]
    [Tooltip("没有语音时的显示秒数。")]
    [Min(0f)]
    public float silentDuration = 4f;

    [Tooltip("有语音时，语音播完之后面板还留多久。")]
    [Min(0f)]
    public float voiceTailSeconds = 0.5f;

    [Header("Size")]
    [Tooltip("面板宽度（米），高度按贴图比例算。")]
    [Min(0.05f)]
    public float widthMeters = 0.7f;

    [Header("Flow")]
    [Tooltip("勾上表示整局只弹一次。")]
    public bool showOnce = true;

    [Tooltip("这条播完后自动衔接的下一条，None 表示不衔接。")]
    public StoryTipId followUp = StoryTipId.None;

    [Tooltip("衔接延迟。锚点是本条语音结束（没语音就是面板消失）。")]
    [Min(0f)]
    public float followUpDelay = 2f;

    [Tooltip("勾上表示只衔接一次。扫描完成要用这个：只有第一次扫完才提示换滤镜。")]
    public bool followUpOnce = true;
}

/// <summary>
/// Scene3 剧情提示面板。
///
/// 全场只有一块复用的 Quad（UI 层，UISHADER 的 ZTest Always 材质，能穿墙看见），
/// 换提示只换材质实例上的贴图，不动材质资产。
///
/// 面板以 PlayerHead 为基准出现在视野中央偏下，之后世界固定；只有玩家转头超过
/// reorientAngle 或走离锚点超过 maxDrift 才平滑追回来。不做贴脸跟随 —— 那个在
/// 头显里很难受。绝不能用 Camera.main：场景里有多个相机打了 MainCamera tag，
/// 包括手持扫描仪上的那两个。
///
/// 语音默认排队不抢断：这几条是连贯的教学口播，中途切断会听起来断掉。只有队首
/// 等待超过 maxQueueWait 才会抢断当前这条。
/// </summary>
public class StoryTipsPresenter : MonoBehaviour
{
    public static StoryTipsPresenter Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private Transform panel;
    [SerializeField] private MeshRenderer panelRenderer;
    [SerializeField] private AudioSource voiceSource;

    [Tooltip("UISHADER 上的贴图属性名。换 shader 时在这里改，不用动代码。")]
    [SerializeField] private string textureProperty = "_Texture2D";

    [Header("Placement")]
    [Tooltip("面板出现在玩家眼前多远（米）。")]
    [SerializeField] private float distanceFromHead = 1.2f;

    [Tooltip("相对视线中心的上下偏移（米），负数往下。")]
    [SerializeField] private float verticalOffset = -0.1f;

    [Tooltip("面板偏离视线中心超过这个角度才重新贴回视野。")]
    [SerializeField] private float reorientAngle = 35f;

    [Tooltip("玩家走离锚点超过这个距离（米）才重新贴回视野。")]
    [SerializeField] private float maxDrift = 1.5f;

    [Tooltip("追回视野的平滑速度，越大越快。")]
    [SerializeField] private float followSpeed = 4f;

    [Tooltip("Unity 内置 Quad 的可见面是 -Z。要是面板看不见或者是镜像的，勾这个翻 180°。")]
    [SerializeField] private bool flipFacing = false;

    [Header("Animation")]
    [SerializeField] private float popInSeconds = 0.25f;
    [SerializeField] private float popOutSeconds = 0.2f;

    [Header("Queue")]
    [Tooltip("队首等待超过这么久就抢断当前提示，避免玩家操作快时排出一长条。")]
    [SerializeField] private float maxQueueWait = 9f;

    [Tooltip("两条提示之间的最小间隔。")]
    [SerializeField] private float gapBetweenTips = 0.3f;

    [Header("Tips")]
    [SerializeField] private List<StoryTipDefinition> tips = new List<StoryTipDefinition>();

    private readonly List<StoryTipId> queue = new List<StoryTipId>();
    private readonly List<float> queuedAt = new List<float>();
    private readonly HashSet<StoryTipId> alreadyShown = new HashSet<StoryTipId>();
    private readonly HashSet<StoryTipId> followUpDone = new HashSet<StoryTipId>();

    private Material runtimeMaterial;
    private Vector3 anchorPosition;
    private Vector3 anchorHeadPosition;
    private Vector3 currentTargetScale = Vector3.one;
    private bool panelVisible;
    private bool lastHoldCutShort;

    /// <summary>不需要连线的调用入口，给场景里的触发挂件用。</summary>
    public static void Request(StoryTipId id)
    {
        if (Instance != null)
        {
            Instance.Show(id);
            return;
        }

        Debug.LogWarning(
            $"StoryTipsPresenter: 场景里没有 Presenter，提示 {id} 被丢掉了。" +
            "跑一次 Prefab Library / 搭建剧情提示 UI。");
    }

    private void Awake()
    {
        Instance = this;

        if (panel != null)
        {
            panel.localScale = Vector3.zero;
            panel.gameObject.SetActive(false);
        }

        panelVisible = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        StartCoroutine(RunQueue());
    }

    /// <summary>
    /// 请求显示一条提示。showOnce 的提示重复请求会被忽略。
    /// </summary>
    public void Show(StoryTipId id)
    {
        if (id == StoryTipId.None)
            return;

        StoryTipDefinition def = Find(id);
        if (def == null)
        {
            Debug.LogWarning($"StoryTipsPresenter: 没有配置提示 {id}。", this);
            return;
        }

        if (def.showOnce && alreadyShown.Contains(id))
            return;

        if (queue.Contains(id))
            return;

        // showOnce 的提示在入队时就登记，避免同一帧被塞进来两次
        if (def.showOnce)
            alreadyShown.Add(id);

        queue.Add(id);
        queuedAt.Add(Time.time);
    }

    private void Update()
    {
        if (!panelVisible || panel == null)
            return;

        Transform head = PlayerHead.Transform;
        if (head == null)
            return;

        Vector3 toPanel = anchorPosition - head.position;
        float offAxis = toPanel.sqrMagnitude > 0.0001f
            ? Vector3.Angle(head.forward, toPanel)
            : 0f;

        bool drifted =
            Vector3.Distance(head.position, anchorHeadPosition) > maxDrift;

        if (offAxis > reorientAngle || drifted)
        {
            anchorHeadPosition = head.position;
            anchorPosition = DesiredPosition(head);
        }

        float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        panel.position = Vector3.Lerp(panel.position, anchorPosition, k);
        panel.rotation = Quaternion.Slerp(panel.rotation, FaceRotation(head), k);
    }

    private IEnumerator RunQueue()
    {
        while (true)
        {
            if (queue.Count == 0)
            {
                yield return null;
                continue;
            }

            StoryTipId next = queue[0];
            queue.RemoveAt(0);
            queuedAt.RemoveAt(0);

            yield return PlayChain(next);

            if (gapBetweenTips > 0f)
                yield return new WaitForSeconds(gapBetweenTips);
        }
    }

    /// <summary>
    /// 播一条提示，以及它自动衔接的后续提示。
    /// </summary>
    private IEnumerator PlayChain(StoryTipId first)
    {
        StoryTipId current = first;
        int guard = 0;

        while (current != StoryTipId.None && guard++ < 8)
        {
            StoryTipDefinition def = Find(current);
            if (def == null)
            {
                Debug.LogWarning($"StoryTipsPresenter: 没有配置提示 {current}。", this);
                yield break;
            }

            alreadyShown.Add(current);

            float startTime = Time.time;
            float voiceLength = def.voice != null ? def.voice.length : 0f;

            ApplyImage(def);
            ShowPanel();
            PlayVoice(def);

            yield return Pop(Vector3.zero, currentTargetScale, popInSeconds);

            float holdSeconds = voiceLength > 0f
                ? voiceLength + def.voiceTailSeconds
                : def.silentDuration;

            yield return HoldOrCut(startTime + popInSeconds + holdSeconds);
            yield return Pop(currentTargetScale, Vector3.zero, popOutSeconds);

            HidePanel();

            StoryTipId following = StoryTipId.None;

            if (def.followUp != StoryTipId.None &&
                (!def.followUpOnce || followUpDone.Add(def.id)))
            {
                following = def.followUp;

                // 衔接锚点是语音结束，不是面板消失。没有语音、或者语音被抢断
                // 掐掉了，就从现在算 —— 不能再去等一段已经不播的音频。
                float anchorTime = voiceLength > 0f && !lastHoldCutShort
                    ? startTime + voiceLength
                    : Time.time;

                float fireAt = anchorTime + def.followUpDelay;
                while (Time.time < fireAt)
                    yield return null;
            }

            current = following;
        }
    }

    /// <summary>
    /// 停留到指定时刻。队首已经等太久的话提前收掉，顺手掐掉语音。
    /// </summary>
    private IEnumerator HoldOrCut(float until)
    {
        lastHoldCutShort = false;

        while (Time.time < until)
        {
            if (queue.Count > 0 &&
                maxQueueWait > 0f &&
                Time.time - queuedAt[0] > maxQueueWait)
            {
                lastHoldCutShort = true;

                if (voiceSource != null)
                    voiceSource.Stop();

                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator Pop(Vector3 from, Vector3 to, float seconds)
    {
        if (panel == null)
            yield break;

        panel.localScale = from;

        if (seconds <= 0f)
        {
            panel.localScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds));
            panel.localScale = Vector3.Lerp(from, to, t);
            yield return null;
        }

        panel.localScale = to;
    }

    private void ApplyImage(StoryTipDefinition def)
    {
        float width = def.widthMeters > 0f ? def.widthMeters : 0.7f;
        float aspect = 3f;

        if (def.image != null && def.image.height > 0)
            aspect = def.image.width / (float)def.image.height;

        currentTargetScale = new Vector3(width, width / aspect, 1f);

        if (panelRenderer == null || def.image == null)
            return;

        // .material 拿的是运行时实例，不会脏掉共享的材质资产
        if (runtimeMaterial == null)
            runtimeMaterial = panelRenderer.material;

        if (!string.IsNullOrEmpty(textureProperty) &&
            runtimeMaterial.HasProperty(textureProperty))
        {
            runtimeMaterial.SetTexture(textureProperty, def.image);
            return;
        }

        Debug.LogWarning(
            $"StoryTipsPresenter: 材质上没有属性 '{textureProperty}'，贴图没换上。",
            this);
    }

    private void PlayVoice(StoryTipDefinition def)
    {
        if (voiceSource == null || def.voice == null)
            return;

        // 用 Stop + Play 而不是 PlayOneShot：新提示要干净地替换旧语音，不能叠着放
        voiceSource.Stop();
        voiceSource.clip = def.voice;
        voiceSource.Play();
    }

    private void ShowPanel()
    {
        if (panel == null)
            return;

        panel.localScale = Vector3.zero;
        panel.gameObject.SetActive(true);
        panelVisible = true;

        SnapToView();
    }

    private void HidePanel()
    {
        panelVisible = false;

        if (panel == null)
            return;

        panel.localScale = Vector3.zero;
        panel.gameObject.SetActive(false);
    }

    /// <summary>提示刚弹出时直接落到视野中央，不要从上一次的位置飞过来。</summary>
    private void SnapToView()
    {
        Transform head = PlayerHead.Transform;
        if (head == null)
            return;

        anchorHeadPosition = head.position;
        anchorPosition = DesiredPosition(head);

        panel.position = anchorPosition;
        panel.rotation = FaceRotation(head);
    }

    private Vector3 DesiredPosition(Transform head)
    {
        return head.position
            + head.forward * distanceFromHead
            + Vector3.up * verticalOffset;
    }

    private Quaternion FaceRotation(Transform head)
    {
        Vector3 look = panel.position - head.position;

        if (look.sqrMagnitude < 0.0001f)
            look = head.forward;

        Quaternion rotation =
            Quaternion.LookRotation(look.normalized, Vector3.up);

        return flipFacing
            ? rotation * Quaternion.Euler(0f, 180f, 0f)
            : rotation;
    }

    private StoryTipDefinition Find(StoryTipId id)
    {
        for (int i = 0; i < tips.Count; i++)
        {
            if (tips[i] != null && tips[i].id == id)
                return tips[i];
        }

        return null;
    }
}
