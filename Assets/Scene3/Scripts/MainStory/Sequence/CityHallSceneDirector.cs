using System.Collections;
using UnityEngine;

/// <summary>
/// 市政厅两阶段 + 飞艇调度。
/// P0 演讲台扫描完成：一阶段人物出现、背景声渐入、演讲台词播一遍，无空气墙。
/// P1 延迟后：飞艇起飞 + 飞艇声。
/// P2 飞艇到路径中段：由 AirshipFlyover 在落点上方生成传单，自由落体。
/// P3 传单落地：落点上方出现向下箭头。
/// P4 玩家抓起任意传单 且 一阶段人群连续不可见：切二阶段（士兵 + 空气墙），换背景声，抗议台词，箭头消失。强制计时开始。
/// P5 计时到：强制切水下，本阶段人物永久消失。
/// </summary>
public class CityHallSceneDirector : MonoBehaviour
{
    public enum Phase
    {
        Idle,
        StageOne,
        AirshipFlying,
        FlyersFalling,
        WaitingForPickup,
        StageTwo,
        Sealed
    }

    [Header("Scene Nodes")]
    [SerializeField] private GameObject stageOnePeople;
    [SerializeField] private GameObject stageTwoPeople;
    [Tooltip("抗议旗，只在二阶段人物出现时打开。")]
    [SerializeField] private GameObject protestFlag;

    [Header("Visibility")]
    [Tooltip("探针放在阶段一听众身上。")]
    [SerializeField] private ViewVisibilityZone stageOneZone;
    [SerializeField] private float invisibleHold = 0.5f;

    [Header("Audio")]
    [SerializeField] private FadeAudioGroup stageOneAmbience;
    [SerializeField] private FadeAudioGroup stageTwoAmbience;
    [SerializeField] private float ambienceFadeSeconds = 2f;
    [SerializeField] private VoiceLineSequence speechVoiceLine;
    [SerializeField] private VoiceLineSequence protestVoiceLines;

    [Header("Airship")]
    [SerializeField] private AirshipFlyover airship;
    [Tooltip("引导玩家注意的飞艇声，和飞艇自带引擎声叠加。")]
    [SerializeField] private AudioSource airshipStoryAudio;
    [SerializeField] private float airshipDelaySeconds = 8f;

    [Header("Flyer Beacon")]
    [SerializeField] private FlyerPickupBeacon beacon;
    [SerializeField] private Transform dropTarget;
    [Tooltip("等传单落稳的最长时间，超时也直接显示箭头。")]
    [SerializeField] private float settleTimeout = 8f;
    [SerializeField] private float settleSpeed = 0.15f;

    [Header("Forced Ending")]
    [SerializeField] private WorldSwitchManager worldSwitch;
    [Tooltip("从 P4 切二阶段开始计时，到时强制切水下。")]
    [SerializeField] private float forcedEndSeconds = 60f;

    [Header("Fail-safe")]
    [SerializeField] private bool enableFailSafeTimeout;
    [SerializeField] private float failSafeSeconds = 4f;

    [Header("Runtime Debug")]
    [SerializeField] private Phase phase = Phase.Idle;

    private bool flyerPicked;
    private float pickupWaitElapsed;
    private float forcedElapsed;
    private bool forcedTimerRunning;
    private Coroutine airshipRoutine;
    private bool protestStarted;
    private bool divedAfterProtest;
    private bool audioMuted;
    private bool pendingBegin;

    public Phase CurrentPhase => phase;

    public void ApplySettings(bool failSafeEnabled, float failSafeTimeout, float forcedEnd, float airshipDelay)
    {
        enableFailSafeTimeout = failSafeEnabled;
        failSafeSeconds = failSafeTimeout;
        forcedEndSeconds = forcedEnd;
        airshipDelaySeconds = airshipDelay;
    }

    public void Initialize()
    {
        SetActiveSafe(stageOnePeople, false);
        SetActiveSafe(stageTwoPeople, false);
        SetActiveSafe(protestFlag, false);

        if (stageOneAmbience != null)
            stageOneAmbience.SetSilent();

        if (stageTwoAmbience != null)
            stageTwoAmbience.SetSilent();

        if (beacon != null)
            beacon.gameObject.SetActive(true);

        phase = Phase.Idle;
        protestStarted = false;
        divedAfterProtest = false;
        audioMuted = false;
        flyerPicked = false;
        forcedTimerRunning = false;
        pendingBegin = false;
        FetchDogPresence.SetCityHallNpcsPresent(false);
    }

    /// <summary>演讲台扫描完成时调用。真正出人和出声要等到水上。</summary>
    public void Begin()
    {
        if (phase != Phase.Idle || pendingBegin)
            return;

        if (IsSurfaceWorld())
            EnterStageOne();
        else
            pendingBegin = true;
    }

    private void OnEnable()
    {
        if (airship != null)
        {
            airship.FlyersDropped += OnFlyersDropped;
            airship.FirstFlyerPicked += OnFirstFlyerPicked;
            airship.FlyoverCompleted += OnFlyoverCompleted;
        }

        if (worldSwitch != null)
            worldSwitch.WorldChanged += OnWorldChanged;
    }

    private void OnDisable()
    {
        if (airship != null)
        {
            airship.FlyersDropped -= OnFlyersDropped;
            airship.FirstFlyerPicked -= OnFirstFlyerPicked;
            airship.FlyoverCompleted -= OnFlyoverCompleted;
        }

        if (worldSwitch != null)
            worldSwitch.WorldChanged -= OnWorldChanged;
    }

    private void OnWorldChanged(WorldSwitchManager.CurrentWorld world)
    {
        FetchDogPresence.NotifyWorld(world);

        if (world == WorldSwitchManager.CurrentWorld.Surface)
        {
            if (pendingBegin && phase == Phase.Idle)
            {
                pendingBegin = false;
                EnterStageOne();
                return;
            }

            if (divedAfterProtest)
            {
                ClearPeopleAndAudio();
                SetPhase(Phase.Sealed);
                return;
            }

            RestoreAudioIfNeeded();
            return;
        }

        if (phase == Phase.Idle || phase == Phase.Sealed)
            return;

        MuteAudio();
        if (protestStarted)
            divedAfterProtest = true;
    }

    private void Update()
    {
        if (phase == Phase.Idle || phase == Phase.Sealed)
            return;

        TickForcedTimer();

        if (phase != Phase.WaitingForPickup)
            return;

        pickupWaitElapsed += Time.deltaTime;

        if (!flyerPicked)
            return;

        if (CrowdIsHidden())
            EnterStageTwo();
    }

    private void EnterStageOne()
    {
        SetPhase(Phase.StageOne);

        SkyboxDirector.SetMood(SkyboxDirector.Mood.Day);

        SetActiveSafe(stageOnePeople, true);
        SetActiveSafe(stageTwoPeople, false);
        SetActiveSafe(protestFlag, false);

        PlaceAmbienceAt(stageOneAmbience, stageOnePeople);

        if (stageOneAmbience != null)
            stageOneAmbience.FadeIn(ambienceFadeSeconds);

        if (speechVoiceLine != null)
            speechVoiceLine.PlaySequence();

        if (airshipRoutine != null)
            StopCoroutine(airshipRoutine);

        airshipRoutine = StartCoroutine(LaunchAirshipAfterDelay());
        FetchDogPresence.SetCityHallNpcsPresent(true);
    }

    private IEnumerator LaunchAirshipAfterDelay()
    {
        if (airshipDelaySeconds > 0f)
            yield return new WaitForSeconds(airshipDelaySeconds);

        SetPhase(Phase.AirshipFlying);

        if (airship != null)
            airship.BeginFlyover();
        else
            Debug.LogWarning("CityHallSceneDirector: 没有绑定 AirshipFlyover。", this);

        if (airshipStoryAudio != null)
        {
            airshipStoryAudio.loop = true;
            if (!airshipStoryAudio.isPlaying)
                airshipStoryAudio.Play();
        }

        airshipRoutine = null;
    }

    private void OnFlyersDropped()
    {
        if (phase == Phase.Sealed || phase == Phase.StageTwo)
            return;

        SetPhase(Phase.FlyersFalling);
        StartCoroutine(ShowBeaconWhenSettled());
    }

    private IEnumerator ShowBeaconWhenSettled()
    {
        float elapsed = 0f;

        while (elapsed < settleTimeout)
        {
            elapsed += Time.deltaTime;
            if (airship != null && airship.DroppedItemsAtRest(settleSpeed))
                break;

            yield return null;
        }

        if (phase == Phase.Sealed || phase == Phase.StageTwo)
            yield break;

        Vector3 point = ResolveBeaconPoint();
        if (beacon != null)
            beacon.ShowAt(point);

        SetPhase(Phase.WaitingForPickup);
        pickupWaitElapsed = 0f;
    }

    private Vector3 ResolveBeaconPoint()
    {
        if (airship != null)
        {
            Vector3 average = airship.AverageDroppedPosition();
            if (average.sqrMagnitude > 0.0001f)
                return average;
        }

        return dropTarget != null ? dropTarget.position : transform.position;
    }

    private void OnFirstFlyerPicked()
    {
        flyerPicked = true;
    }

    private void EnterStageTwo()
    {
        SetPhase(Phase.StageTwo);
        protestStarted = true;

        SkyboxDirector.SetMood(SkyboxDirector.Mood.Dusk);

        SetActiveSafe(stageOnePeople, false);
        SetActiveSafe(stageTwoPeople, true);
        SetActiveSafe(protestFlag, true);

        if (stageOneAmbience != null)
            stageOneAmbience.FadeOut(ambienceFadeSeconds);

        PlaceAmbienceAt(stageTwoAmbience, stageTwoPeople);

        if (stageTwoAmbience != null)
            stageTwoAmbience.FadeIn(ambienceFadeSeconds);

        if (protestVoiceLines != null)
            protestVoiceLines.PlaySequence();

        if (beacon != null)
            beacon.Hide();

        forcedTimerRunning = true;
        forcedElapsed = 0f;
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
        protestStarted = true;
        divedAfterProtest = true;
        ClearPeopleAndAudio();

        if (worldSwitch != null)
            worldSwitch.RequestSwitch(LensWorldType.Underwater);
        else
            Debug.LogWarning("CityHallSceneDirector: 没有绑定 WorldSwitchManager，无法强制切水下。", this);
    }

    private void ClearPeopleAndAudio()
    {
        SetActiveSafe(stageOnePeople, false);
        SetActiveSafe(stageTwoPeople, false);
        SetActiveSafe(protestFlag, false);
        forcedTimerRunning = false;
        MuteAudio();
        StopHorn();
        if (airship != null)
            airship.SetEnginePlaying(false);

        if (beacon != null)
            beacon.Hide();

        FetchDogPresence.SetCityHallNpcsPresent(false);
    }

    private void MuteAudio()
    {
        audioMuted = true;

        if (stageOneAmbience != null)
            stageOneAmbience.FadeOut(Mathf.Min(0.6f, ambienceFadeSeconds));

        if (stageTwoAmbience != null)
            stageTwoAmbience.FadeOut(Mathf.Min(0.6f, ambienceFadeSeconds));

        if (speechVoiceLine != null)
            speechVoiceLine.Abort();

        if (protestVoiceLines != null)
            protestVoiceLines.Abort();

        StopHorn();
        if (airship != null)
            airship.SetEnginePlaying(false);
    }

    private void RestoreAudioIfNeeded()
    {
        if (!audioMuted || phase == Phase.Idle || phase == Phase.Sealed)
            return;

        audioMuted = false;

        if (phase == Phase.StageTwo)
        {
            PlaceAmbienceAt(stageTwoAmbience, stageTwoPeople);
            if (stageTwoAmbience != null)
                stageTwoAmbience.FadeIn(ambienceFadeSeconds);
        }
        else if (stageOneAmbience != null)
        {
            PlaceAmbienceAt(stageOneAmbience, stageOnePeople);
            stageOneAmbience.FadeIn(ambienceFadeSeconds);
        }

        if (airship != null && airship.IsFlying)
        {
            airship.SetEnginePlaying(true);
            PlayHorn();
        }
    }

    private void PlayHorn()
    {
        if (airshipStoryAudio == null)
            return;

        airshipStoryAudio.loop = true;
        if (!airshipStoryAudio.isPlaying)
            airshipStoryAudio.Play();
    }

    private void StopHorn()
    {
        if (airshipStoryAudio != null && airshipStoryAudio.isPlaying)
            airshipStoryAudio.Stop();
    }

    private void OnFlyoverCompleted()
    {
        StopHorn();
    }

    private bool CrowdIsHidden()
    {
        if (enableFailSafeTimeout && failSafeSeconds > 0f && pickupWaitElapsed >= failSafeSeconds)
            return true;

        if (!IsSurfaceWorld())
            return false;

        if (stageOneZone == null)
            return false;

        return stageOneZone.HasBeenInvisibleFor(invisibleHold);
    }

    private bool IsSurfaceWorld()
    {
        return worldSwitch == null || worldSwitch.currentWorld == WorldSwitchManager.CurrentWorld.Surface;
    }

    private void SetPhase(Phase next)
    {
        phase = next;

        if (stageOneZone != null)
            stageOneZone.ResetTimer();

        Debug.Log("CityHallSceneDirector -> " + next);
    }

    private static void PlaceAmbienceAt(FadeAudioGroup group, GameObject anchor)
    {
        if (group == null || anchor == null)
            return;

        group.transform.position = anchor.transform.position + Vector3.up * 1.6f;
    }

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }
}
