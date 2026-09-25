using UnityEngine;

/// <summary>
/// 咖啡厅流程：
/// 扫描完成且在水上（或之后第一次切到水上）→ 室外两人 + 内景（含人物和门）一起出现。
/// 门关着并呼吸发光；打开任意一扇后门全部停发光。
/// 第一次走进内景再走出去 → 强制切水下；再切回岸上时 NPC 全关，交谈渐出，音乐保留。
/// 若走进去以后一直不出来，从走进内景那一刻起 60 秒仍会强制切水下，人物永久消失。
/// 一直不进内景的话不计时 —— 那种情况玩家可以自己拿透镜切回水下，主线不受影响。
/// </summary>
public class CafeSceneDirector : MonoBehaviour
{
    public enum Phase
    {
        Idle,
        WaitingForSurface,
        Open,
        NpcsCleared,
        Sealed,
        ForcedDive
    }

    [Header("Scene Nodes")]
    [SerializeField] private GameObject outdoorEnvironment;
    [SerializeField] private GameObject outdoorPeople;
    [SerializeField] private GameObject interiorEnvironment;
    [SerializeField] private GameObject interiorPeople;

    [Header("Doors / Volume")]
    [SerializeField] private CafeDoorSet doors;
    [SerializeField] private CafeInteriorVolume interiorVolume;

    [Header("Audio")]
    [SerializeField] private FadeAudioGroup conversation;
    [SerializeField] private FadeAudioGroup music;
    [SerializeField] private float ambienceFadeSeconds = 2f;
    [SerializeField] private VoiceLineSequence outdoorVoiceLines;
    [SerializeField] private VoiceLineSequence interiorVoiceLines;

    [Header("Forced Ending")]
    [SerializeField] private WorldSwitchManager worldSwitch;
    [Tooltip("从玩家第一次走进内景开始计时，到时强制切水下，本阶段人物永久消失。")]
    [SerializeField] private float forcedEndSeconds = 60f;

    [Header("Runtime Debug")]
    [SerializeField] private Phase phase = Phase.Idle;

    private float forcedElapsed;
    private bool forcedTimerRunning;

    public Phase CurrentPhase => phase;

    public void ApplySettings(bool failSafeEnabled, float failSafeTimeout, float forcedEnd)
    {
        _ = failSafeEnabled;
        _ = failSafeTimeout;
        forcedEndSeconds = forcedEnd;
    }

    public void Initialize()
    {
        SetActiveSafe(outdoorEnvironment, false);
        SetActiveSafe(outdoorPeople, false);
        SetActiveSafe(interiorEnvironment, false);
        SetActiveSafe(interiorPeople, false);

        if (conversation != null)
            conversation.SetSilent();

        if (music != null)
            music.SetSilent();

        if (interiorVolume != null)
            interiorVolume.ResetVisit();

        phase = Phase.Idle;
        forcedTimerRunning = false;
        forcedElapsed = 0f;
        FetchDogPresence.SetCafeNpcsPresent(false);
    }

    public void Begin()
    {
        if (phase != Phase.Idle)
            return;

        if (IsSurfaceWorld())
            EnterOpen();
        else
            SetPhase(Phase.WaitingForSurface);
    }

    private void OnEnable()
    {
        if (worldSwitch != null)
            worldSwitch.WorldChanged += OnWorldChanged;

        if (doors != null)
            doors.FirstOpened += OnFirstDoorOpened;

        if (interiorVolume != null)
        {
            interiorVolume.FirstEntered += OnFirstEnteredInterior;
            interiorVolume.FirstLeftAfterEnter += OnFirstLeftAfterEnter;
        }
    }

    private void OnDisable()
    {
        if (worldSwitch != null)
            worldSwitch.WorldChanged -= OnWorldChanged;

        if (doors != null)
            doors.FirstOpened -= OnFirstDoorOpened;

        if (interiorVolume != null)
        {
            interiorVolume.FirstEntered -= OnFirstEnteredInterior;
            interiorVolume.FirstLeftAfterEnter -= OnFirstLeftAfterEnter;
        }
    }

    private void Update()
    {
        if (phase != Phase.Open)
            return;

        TickForcedTimer();
    }

    private void OnWorldChanged(WorldSwitchManager.CurrentWorld world)
    {
        FetchDogPresence.NotifyWorld(world);

        if (world == WorldSwitchManager.CurrentWorld.Surface)
        {
            if (phase == Phase.WaitingForSurface)
                EnterOpen();
            else if (phase == Phase.ForcedDive)
                ClearNpcsOnReturnToSurface();
            else
                RestoreAudioForSurface();
            return;
        }

        MuteCafeAudio();
    }

    private void MuteCafeAudio()
    {
        float fade = Mathf.Min(0.6f, ambienceFadeSeconds);

        if (music != null)
            music.FadeOut(fade);

        if (conversation != null)
            conversation.FadeOut(fade);

        if (outdoorVoiceLines != null)
            outdoorVoiceLines.Abort();

        if (interiorVoiceLines != null)
            interiorVoiceLines.Abort();
    }

    private void RestoreAudioForSurface()
    {
        if (phase == Phase.Open)
        {
            PlaceAmbienceAtCafe();

            if (music != null)
                music.FadeIn(ambienceFadeSeconds);

            if (conversation != null)
                conversation.FadeIn(ambienceFadeSeconds);
            return;
        }

        if (phase == Phase.NpcsCleared && music != null)
        {
            PlaceAmbienceAtCafe();
            music.FadeIn(ambienceFadeSeconds);
        }
    }

    /// <summary>
    /// 循环背景音挂在 Story Directors 下。Directors 被放到飞艇高度后，
    /// 3D 衰减会让咖啡厅里听不见。只改位置，不改 volume / spatialBlend / maxDistance。
    /// </summary>
    private void PlaceAmbienceAtCafe()
    {
        Transform anchor = null;
        if (interiorEnvironment != null)
            anchor = interiorEnvironment.transform;
        else if (outdoorEnvironment != null)
            anchor = outdoorEnvironment.transform;

        if (anchor == null)
            return;

        Vector3 pos = anchor.position + Vector3.up * 1.6f;
        if (conversation != null)
            conversation.transform.position = pos;
        if (music != null)
            music.transform.position = pos;
    }

    private void EnterOpen()
    {
        SetPhase(Phase.Open);

        SkyboxDirector.SetMood(SkyboxDirector.Mood.Day);

        SetActiveSafe(outdoorEnvironment, true);
        SetActiveSafe(outdoorPeople, true);
        SetActiveSafe(interiorEnvironment, true);
        SetActiveSafe(interiorPeople, true);

        PlaceAmbienceAtCafe();

        if (doors != null)
            doors.SetClosedAndGlowing();

        if (music != null)
            music.FadeIn(ambienceFadeSeconds);

        if (conversation != null)
            conversation.FadeIn(ambienceFadeSeconds);

        if (outdoorVoiceLines != null)
            outdoorVoiceLines.PlaySequence();

        if (interiorVoiceLines != null)
            interiorVoiceLines.PlaySequence();

        if (interiorVolume != null)
            interiorVolume.ResetVisit();

        // 计时不在这里起，等玩家真的走进内景（OnFirstEnteredInterior）
        forcedTimerRunning = false;
        forcedElapsed = 0f;
        FetchDogPresence.SetCafeNpcsPresent(true);
    }

    private void OnFirstDoorOpened()
    {
        if (doors != null)
            doors.StopGlow();
    }

    private void OnFirstEnteredInterior()
    {
        if (phase != Phase.Open)
            return;

        forcedTimerRunning = true;
        forcedElapsed = 0f;

        Debug.Log(
            "CafeSceneDirector: 玩家进入内景，强制结束计时开始 " +
            forcedEndSeconds + "s。");
    }

    private void OnFirstLeftAfterEnter()
    {
        if (phase != Phase.Open)
            return;

        forcedTimerRunning = false;
        SetPhase(Phase.ForcedDive);

        if (worldSwitch != null)
            worldSwitch.RequestSwitch(LensWorldType.Underwater);
        else
            ClearNpcsOnReturnToSurface();
    }

    private void ClearNpcsOnReturnToSurface()
    {
        ClearNpcsKeepMusic();
        SetPhase(Phase.NpcsCleared);
        RestoreAudioForSurface();
    }

    private void ClearNpcsKeepMusic()
    {
        SetActiveSafe(outdoorPeople, false);
        SetActiveSafe(interiorPeople, false);

        if (conversation != null)
            conversation.FadeOut(ambienceFadeSeconds);

        if (outdoorVoiceLines != null)
            outdoorVoiceLines.Abort();

        if (interiorVoiceLines != null)
            interiorVoiceLines.Abort();

        FetchDogPresence.SetCafeNpcsPresent(false);
    }

    private void TickForcedTimer()
    {
        if (!forcedTimerRunning || forcedEndSeconds <= 0f)
            return;

        forcedElapsed += Time.deltaTime;
        if (forcedElapsed < forcedEndSeconds)
            return;

        forcedTimerRunning = false;
        ForceUnderwaterAndSeal();
    }

    private void ForceUnderwaterAndSeal()
    {
        SetPhase(Phase.Sealed);
        ClearNpcsKeepMusic();

        if (worldSwitch != null)
            worldSwitch.RequestSwitch(LensWorldType.Underwater);
        else
            Debug.LogWarning("CafeSceneDirector: 没有绑定 WorldSwitchManager，无法强制切水下。", this);
    }

    private bool IsSurfaceWorld()
    {
        return worldSwitch == null || worldSwitch.currentWorld == WorldSwitchManager.CurrentWorld.Surface;
    }

    private void SetPhase(Phase next)
    {
        phase = next;
        Debug.Log("CafeSceneDirector -> " + next);
    }

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }
}
