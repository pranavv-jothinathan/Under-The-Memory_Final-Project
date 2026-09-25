using UnityEngine;

/// <summary>
/// 公交站：扫描完成 → 等切到水上 → 人物出现 + 环境声渐入 + 台词依次播完。
/// 没有视野机制，也不强制切水下。
/// </summary>
public class BusStopSceneDirector : MonoBehaviour
{
    [Header("Scene Nodes")]
    [SerializeField] private GameObject people;

    [Header("Audio")]
    [SerializeField] private FadeAudioGroup ambience;
    [SerializeField] private float ambienceFadeSeconds = 2f;
    [SerializeField] private VoiceLineSequence voiceLines;

    [Header("World")]
    [SerializeField] private WorldSwitchManager worldSwitch;

    [Header("Runtime Debug")]
    [SerializeField] private bool started;

    private bool pendingBegin;

    public bool HasStarted => started;

    private void Awake()
    {
        ResolveWorldSwitch();
    }

    private void OnEnable()
    {
        ResolveWorldSwitch();

        if (worldSwitch != null)
            worldSwitch.WorldChanged += OnWorldChanged;
    }

    private void OnDisable()
    {
        if (worldSwitch != null)
            worldSwitch.WorldChanged -= OnWorldChanged;
    }

    public void Initialize()
    {
        SetActiveSafe(people, false);

        if (ambience != null)
            ambience.SetSilent();

        started = false;
        pendingBegin = false;
    }

    /// <summary>公交站扫描完成时调用。音频要等到水上再播。</summary>
    public void Begin()
    {
        if (started || pendingBegin)
            return;

        if (IsSurfaceWorld())
            EnterSurface();
        else
            pendingBegin = true;
    }

    private void OnWorldChanged(WorldSwitchManager.CurrentWorld world)
    {
        if (world != WorldSwitchManager.CurrentWorld.Surface)
            return;

        if (!pendingBegin || started)
            return;

        pendingBegin = false;
        EnterSurface();
    }

    private void EnterSurface()
    {
        if (started)
            return;

        started = true;

        SkyboxDirector.SetMood(SkyboxDirector.Mood.Night);

        SetActiveSafe(people, true);

        if (ambience != null && people != null)
            ambience.transform.position = people.transform.position + Vector3.up * 1.6f;

        if (ambience != null)
            ambience.FadeIn(ambienceFadeSeconds);

        if (voiceLines != null)
            voiceLines.PlaySequence();

        Debug.Log("BusStopSceneDirector -> Begin");
    }

    private void ResolveWorldSwitch()
    {
        if (worldSwitch == null)
            worldSwitch = FindFirstObjectByType<WorldSwitchManager>();
    }

    private bool IsSurfaceWorld()
    {
        ResolveWorldSwitch();
        return worldSwitch == null ||
            worldSwitch.currentWorld == WorldSwitchManager.CurrentWorld.Surface;
    }

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }
}
