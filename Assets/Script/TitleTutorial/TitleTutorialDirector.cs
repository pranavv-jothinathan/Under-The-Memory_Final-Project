using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using Unity.XR.CoreUtils;
using UnityEngine.Events;

/// <summary>
/// Runs the short, action-driven locomotion tutorial in TitleScene.
/// The authored panels are toggled here; the real locomotion components are shared with MainScene.
/// Keyboard shortcuts are retained only for editor-side tutorial testing.
/// </summary>
[DefaultExecutionOrder(200)]
public sealed class TitleTutorialDirector : MonoBehaviour
{
    enum TutorialState
    {
        OpeningDelay,
        WaitForAnyButton,
        WalkingVoice,
        WalkingPractice,
        ModeSwitchVoice,
        WaitForSwim,
        SwimmingVoice,
        SwimmingPractice,
        WaitForTeleport,
        TeleportVoice,
        TeleportPractice,
        WaitForAnyTrigger,
        Loading
    }

    [Header("Panels")]
    [SerializeField] GameObject m_AnyButtonPanel;
    [SerializeField] GameObject m_TutorialPanel;
    [SerializeField] GameObject m_AnyTriggerPanel;
    [SerializeField] GameObject m_TitleObject;
    [SerializeField] Texture2D m_TutorialTexture;
    [SerializeField, Min(0.05f)] float m_PromptWidth = 0.9f;
    [SerializeField, Min(0.05f)] float m_TutorialWidth = 0.85f;

    [Header("Locomotion")]
    [SerializeField] LocomotionModeSwitcher m_ModeSwitcher;
    [SerializeField] ContinuousMoveProvider m_MoveProvider;
    [SerializeField] SnapTurnProvider m_SnapTurnProvider;
    [SerializeField] CharacterJump m_Jump;
    [SerializeField] FreeAimTeleport m_Teleport;

    [Header("Voice")]
    [SerializeField] AudioSource m_VoiceSource;
    [SerializeField] AudioSource m_BackgroundMusic;
    [SerializeField] AudioClip m_ThreeModesVoice;
    [SerializeField] AudioClip m_SwitchModeVoice;
    [SerializeField] AudioClip m_CommonControlsVoice;
    [SerializeField] AudioClip m_WalkingVoice;
    [SerializeField] AudioClip m_SwimmingVoice;
    [SerializeField] AudioClip m_TeleportVoice;
    [SerializeField, Range(0f, 1f)] float m_MusicDuckMultiplier = 0.25f;

    [Header("Timing")]
    [SerializeField, Min(0f)] float m_OpeningDelay = 4f;
    [SerializeField, Min(0f)] float m_BetweenVoiceDelay = 0.3f;
    [SerializeField, Range(0.1f, 1f)] float m_StickThreshold = 0.5f;
    [SerializeField, Min(0.1f)] float m_SwimHoldSeconds = 0.75f;
    [SerializeField, Min(0f)] float m_InputDebounce = 0.25f;

    [Header("Scene")]
    [SerializeField] string m_MainSceneName = "MainScene";
    public UnityEvent m_OnSceneLoaded;

    TutorialState m_State = TutorialState.OpeningDelay;
    InputAction m_AnyButton;
    InputAction m_LeftStick;
    InputAction m_RightStick;
    InputAction m_JumpButton;
    InputAction m_AnyTrigger;
    bool m_WalkMoved;
    bool m_WalkTurned;
    bool m_WalkJumped;
    bool m_Teleported;
    float m_SwimHeldFor;
    float m_AcceptInputAt;
    float m_OriginalMusicVolume;
    Coroutine m_Routine;
    bool m_PrevXrButton;
    bool m_PrevXrTrigger;
    TitleTutorialStepHud m_StepHud;
    static readonly List<UnityEngine.XR.InputDevice> s_XrDevices = new List<UnityEngine.XR.InputDevice>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RegisterSceneBootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapIfMissing()
    {
        TryBootstrap(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryBootstrap(scene);
    }

    static void TryBootstrap(Scene scene)
    {
        if (scene.name != "TitleScene")
            return;
        if (FindFirstObjectByType<TitleTutorialDirector>(FindObjectsInactive.Include) != null)
            return;

        GameObject system = new GameObject("[SYS] TitleTutorial");
        AudioSource voice = system.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;
        system.AddComponent<TitleTutorialDirector>();
    }

    void Awake()
    {
        Debug.Log("[TitleTutorial] Director running.", this);
        PlayerHead.Invalidate();
        XROrigin xrOrigin = ResolveReferences();
        PrepareSceneAndPanels(xrOrigin);
        BuildInputActions();

        SetPanel(m_AnyButtonPanel, false);
        SetPanel(m_TutorialPanel, false);
        SetPanel(m_AnyTriggerPanel, false);
        SetLocomotionAvailable(false);

        if (m_BackgroundMusic != null)
            m_OriginalMusicVolume = m_BackgroundMusic.volume;
    }

    void OnEnable()
    {
        m_AnyButton?.Enable();
        m_LeftStick?.Enable();
        m_RightStick?.Enable();
        m_JumpButton?.Enable();
        m_AnyTrigger?.Enable();

        if (m_ModeSwitcher != null)
            m_ModeSwitcher.ModeChanged += OnModeChanged;
        if (m_Teleport != null)
            m_Teleport.Teleported += OnTeleported;
    }

    void Start()
    {
        m_Routine = StartCoroutine(BeginAfterDelay());
    }

    void OnDisable()
    {
        if (m_ModeSwitcher != null)
            m_ModeSwitcher.ModeChanged -= OnModeChanged;
        if (m_Teleport != null)
            m_Teleport.Teleported -= OnTeleported;

        m_AnyButton?.Disable();
        m_LeftStick?.Disable();
        m_RightStick?.Disable();
        m_JumpButton?.Disable();
        m_AnyTrigger?.Disable();
    }

    void OnDestroy()
    {
        m_AnyButton?.Dispose();
        m_LeftStick?.Dispose();
        m_RightStick?.Dispose();
        m_JumpButton?.Dispose();
        m_AnyTrigger?.Dispose();
    }

    void Update()
    {
        if (Time.unscaledTime < m_AcceptInputAt)
            return;

        switch (m_State)
        {
            case TutorialState.OpeningDelay:
                SetLocomotionAvailable(false);
                break;

            case TutorialState.WaitForAnyButton:
                SetLocomotionAvailable(false);
                if (AnyStartButtonPressed())
                    BeginTutorial();
                break;

            case TutorialState.WalkingVoice:
            case TutorialState.WalkingPractice:
                CaptureWalkingActions();
                if (m_State == TutorialState.WalkingPractice &&
                    m_WalkMoved && m_WalkTurned && m_WalkJumped)
                {
                    StartManagedRoutine(ExplainModeSwitch());
                }
                break;

            case TutorialState.WaitForSwim:
                ApplyKeyboardModeShortcut(PlayerLocomotionMode.Swim);
                break;

            case TutorialState.SwimmingVoice:
            case TutorialState.SwimmingPractice:
                CaptureSwimmingMove();
                if (m_State == TutorialState.SwimmingPractice && m_SwimHeldFor >= m_SwimHoldSeconds)
                {
                    m_State = TutorialState.WaitForTeleport;
                    SetModeSwitchEnabled(true);
                    m_AcceptInputAt = Time.unscaledTime + m_InputDebounce;
                    RefreshHud();
                }
                break;

            case TutorialState.WaitForTeleport:
                ApplyKeyboardModeShortcut(PlayerLocomotionMode.Teleport);
                break;

            case TutorialState.TeleportVoice:
            case TutorialState.TeleportPractice:
                if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                    m_Teleported = true;
                if (m_State == TutorialState.TeleportPractice && m_Teleported)
                    CompleteTutorial();
                break;

            case TutorialState.WaitForAnyTrigger:
                if (AnyTriggerPressed() || EditorContinuePressed())
                    LoadMainScene();
                break;
        }
    }

    IEnumerator BeginAfterDelay()
    {
        yield return new WaitForSecondsRealtime(m_OpeningDelay);
        SetPanel(m_AnyButtonPanel, true);
        m_State = TutorialState.WaitForAnyButton;
        RefreshHud();
        m_PrevXrButton = true;
        m_AcceptInputAt = Time.unscaledTime + m_InputDebounce;
        m_Routine = null;
    }

    void BeginTutorial()
    {
        SetPanel(m_AnyButtonPanel, false);
        SetPanel(m_TitleObject, false);
        SetPanel(m_TutorialPanel, true);
        DuckMusic(true);

        m_WalkMoved = false;
        m_WalkTurned = false;
        m_WalkJumped = false;
        m_SwimHeldFor = 0f;
        m_Teleported = false;

        SetLocomotionAvailable(true);
        m_ModeSwitcher?.SetMode(PlayerLocomotionMode.Walk);
        SetModeSwitchEnabled(false);
        m_AcceptInputAt = Time.unscaledTime + m_InputDebounce;
        StartManagedRoutine(PlayWalkingIntroduction());
        RefreshHud();
    }

    IEnumerator PlayWalkingIntroduction()
    {
        m_State = TutorialState.WalkingVoice;
        RefreshHud();
        yield return PlayVoice(m_ThreeModesVoice);
        yield return VoiceGap();
        yield return PlayVoice(m_CommonControlsVoice);
        yield return VoiceGap();
        yield return PlayVoice(m_WalkingVoice);
        m_State = TutorialState.WalkingPractice;
        RefreshHud();
        m_Routine = null;
    }

    IEnumerator ExplainModeSwitch()
    {
        m_State = TutorialState.ModeSwitchVoice;
        RefreshHud();
        yield return PlayVoice(m_SwitchModeVoice);
        m_State = TutorialState.WaitForSwim;
        RefreshHud();
        SetModeSwitchEnabled(true);
        m_AcceptInputAt = Time.unscaledTime + m_InputDebounce;
        m_Routine = null;
    }

    IEnumerator PlaySwimmingInstruction()
    {
        m_State = TutorialState.SwimmingVoice;
        RefreshHud();
        yield return PlayVoice(m_SwimmingVoice);
        m_State = TutorialState.SwimmingPractice;
        RefreshHud();
        m_Routine = null;
    }

    IEnumerator PlayTeleportInstruction()
    {
        m_State = TutorialState.TeleportVoice;
        RefreshHud();
        yield return PlayVoice(m_TeleportVoice);
        m_State = TutorialState.TeleportPractice;
        RefreshHud();
        m_Routine = null;

        if (m_Teleported)
            CompleteTutorial();
    }

    IEnumerator PlayVoice(AudioClip clip)
    {
        if (m_VoiceSource == null || clip == null)
            yield break;

        m_VoiceSource.Stop();
        m_VoiceSource.clip = clip;
        m_VoiceSource.Play();
        yield return new WaitForSecondsRealtime(clip.length);
    }

    IEnumerator VoiceGap()
    {
        if (m_BetweenVoiceDelay > 0f)
            yield return new WaitForSecondsRealtime(m_BetweenVoiceDelay);
    }

    void CaptureWalkingActions()
    {
        if (ReadStick(m_LeftStick).magnitude >= m_StickThreshold ||
            (Keyboard.current != null && Keyboard.current.wKey.isPressed))
        {
            m_WalkMoved = true;
        }

        if (Mathf.Abs(ReadStick(m_RightStick).x) >= m_StickThreshold ||
            (Keyboard.current != null &&
             (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.rightArrowKey.isPressed)))
        {
            m_WalkTurned = true;
        }

        if ((m_JumpButton != null && m_JumpButton.WasPressedThisFrame()) ||
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame))
        {
            m_WalkJumped = true;
        }
    }

    void CaptureSwimmingMove()
    {
        bool moving = ReadStick(m_LeftStick).magnitude >= m_StickThreshold ||
                      (Keyboard.current != null && Keyboard.current.wKey.isPressed);
        m_SwimHeldFor = moving ? m_SwimHeldFor + Time.unscaledDeltaTime : 0f;
    }

    void OnModeChanged(PlayerLocomotionMode mode)
    {
        if (m_State == TutorialState.WaitForSwim && mode == PlayerLocomotionMode.Swim)
        {
            SetModeSwitchEnabled(false);
            m_SwimHeldFor = 0f;
            StartManagedRoutine(PlaySwimmingInstruction());
            RefreshHud();
        }
        else if (m_State == TutorialState.WaitForTeleport && mode == PlayerLocomotionMode.Teleport)
        {
            SetModeSwitchEnabled(false);
            StartManagedRoutine(PlayTeleportInstruction());
            RefreshHud();
        }
    }

    void OnTeleported()
    {
        if (m_State != TutorialState.TeleportVoice && m_State != TutorialState.TeleportPractice)
            return;

        m_Teleported = true;
        if (m_State == TutorialState.TeleportPractice)
            CompleteTutorial();
    }

    void CompleteTutorial()
    {
        if (m_State == TutorialState.WaitForAnyTrigger || m_State == TutorialState.Loading)
            return;

        if (m_VoiceSource != null)
            m_VoiceSource.Stop();
        if (m_Routine != null)
        {
            StopCoroutine(m_Routine);
            m_Routine = null;
        }

        SetLocomotionAvailable(false);
        SetPanel(m_TutorialPanel, false);
        SetPanel(m_AnyTriggerPanel, true);
        SetPanel(m_TitleObject, false);
        DuckMusic(false);
        RefreshHud();
        m_State = TutorialState.WaitForAnyTrigger;
        m_PrevXrTrigger = true;
        m_AcceptInputAt = Time.unscaledTime + m_InputDebounce;
    }

    void LoadMainScene()
    {
        if (m_State == TutorialState.Loading)
            return;

        m_State = TutorialState.Loading;
        SetPanel(m_AnyTriggerPanel, false);
        SceneManager.LoadScene(m_MainSceneName);
        m_OnSceneLoaded?.Invoke();

    }






    void SetLocomotionAvailable(bool available)
    {
        SetModeSwitchEnabled(false);

        if (available)
        {
            if (m_MoveProvider != null)
                m_MoveProvider.enabled = true;
            if (m_SnapTurnProvider != null)
                m_SnapTurnProvider.enabled = true;
            return;
        }

        if (m_Jump != null)
            m_Jump.SetActive(false);
        if (m_Teleport != null)
            m_Teleport.SetActive(false);
        if (m_MoveProvider != null)
            m_MoveProvider.enabled = false;
        if (m_SnapTurnProvider != null)
            m_SnapTurnProvider.enabled = false;
    }

    void SetModeSwitchEnabled(bool enabled)
    {
        if (m_ModeSwitcher != null)
            m_ModeSwitcher.SetModeSwitchInputEnabled(enabled);
    }

    void ApplyKeyboardModeShortcut(PlayerLocomotionMode target)
    {
        if (Keyboard.current == null || !Keyboard.current.bKey.wasPressedThisFrame || m_ModeSwitcher == null)
            return;

        m_ModeSwitcher.SetMode(target);
    }

    bool AnyStartButtonPressed()
    {
        if (m_AnyButton != null && m_AnyButton.WasPressedThisFrame())
            return true;
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;
        if (GamepadButtonPressed())
            return true;
        return XrButtonWentDown();
    }

    bool AnyTriggerPressed()
    {
        if (m_AnyTrigger != null && m_AnyTrigger.WasPressedThisFrame())
            return true;
        bool held = AnyXrFeatureHeld(UnityEngine.XR.CommonUsages.triggerButton);
        bool pressed = held && !m_PrevXrTrigger;
        m_PrevXrTrigger = held;
        return pressed;
    }

    static bool EditorContinuePressed()
    {
        return Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
    }

    static Vector2 ReadStick(InputAction action)
    {
        return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
    }

    void StartManagedRoutine(IEnumerator routine)
    {
        if (m_Routine != null)
            StopCoroutine(m_Routine);
        m_Routine = StartCoroutine(routine);
    }

    void DuckMusic(bool duck)
    {
        if (m_BackgroundMusic == null)
            return;

        m_BackgroundMusic.volume = duck
            ? m_OriginalMusicVolume * m_MusicDuckMultiplier
            : m_OriginalMusicVolume;
    }

    XROrigin ResolveReferences()
    {
        XROrigin xrOrigin = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
        GameObject xrRoot = xrOrigin != null ? xrOrigin.gameObject : gameObject;
        Transform locomotionTransform = FindChildByName(xrRoot.transform, "Locomotion");
        GameObject locomotionHost = locomotionTransform != null ? locomotionTransform.gameObject : xrRoot;
        locomotionHost.SetActive(true);

        if (m_Jump == null)
            m_Jump = xrRoot.GetComponentInChildren<CharacterJump>(true) ??
                     locomotionHost.AddComponent<CharacterJump>();
        if (m_Teleport == null)
            m_Teleport = xrRoot.GetComponentInChildren<FreeAimTeleport>(true) ??
                         locomotionHost.AddComponent<FreeAimTeleport>();
        if (m_ModeSwitcher == null)
            m_ModeSwitcher = xrRoot.GetComponentInChildren<LocomotionModeSwitcher>(true) ??
                             locomotionHost.AddComponent<LocomotionModeSwitcher>();
        if (m_MoveProvider == null)
            m_MoveProvider = xrRoot.GetComponentInChildren<ContinuousMoveProvider>(true);
        if (m_SnapTurnProvider == null)
            m_SnapTurnProvider = xrRoot.GetComponentInChildren<SnapTurnProvider>(true);
        if (m_VoiceSource == null)
        {
            m_VoiceSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            m_VoiceSource.playOnAwake = false;
            m_VoiceSource.loop = false;
            m_VoiceSource.spatialBlend = 0f;
        }
        if (m_BackgroundMusic == null)
            m_BackgroundMusic = FindObjectByName("Audio")?.GetComponent<AudioSource>();

        return xrOrigin;
    }

    static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
                return child;
        }

        return null;
    }

    void PrepareSceneAndPanels(XROrigin xrOrigin)
    {
        if (m_AnyButtonPanel == null)
            m_AnyButtonPanel = FindObjectByName("按任意键开始");
        if (m_AnyTriggerPanel == null)
            m_AnyTriggerPanel = FindObjectByName("按任意扳机开始");

        if (m_TutorialPanel == null)
            m_TutorialPanel = FindObjectByName("移动教学UI");
        if (m_TitleObject == null)
            m_TitleObject = FindObjectByName("Titl");

        if (m_TutorialPanel == null && m_AnyButtonPanel != null)
        {
            m_TutorialPanel = Instantiate(m_AnyButtonPanel);
            m_TutorialPanel.name = "移动教学UI";
            ApplyTutorialTexture(m_TutorialPanel);
        }

        AttachFollower(m_AnyButtonPanel, m_PromptWidth);
        AttachFollower(m_AnyTriggerPanel, m_PromptWidth);
        AttachFollower(m_TutorialPanel, m_TutorialWidth);
        if (m_TutorialPanel != null)
            m_StepHud = m_TutorialPanel.GetComponent<TitleTutorialStepHud>() ??
                        m_TutorialPanel.AddComponent<TitleTutorialStepHud>();

        StripColliders(m_AnyButtonPanel);
        StripColliders(m_AnyTriggerPanel);
        StripColliders(m_TutorialPanel);

        GameObject starterControls = FindObjectByName("Interactive Controls");
        if (starterControls != null)
            starterControls.SetActive(false);

        Camera xrCamera = xrOrigin != null ? xrOrigin.Camera : null;
        DisableDuplicateListeners(xrCamera);
    }

    static void AttachFollower(GameObject panel, float widthMeters)
    {
        if (panel == null)
            return;

        TitleTutorialPanelFollower follower =
            panel.GetComponent<TitleTutorialPanelFollower>() ??
            panel.AddComponent<TitleTutorialPanelFollower>();
        follower.Configure(widthMeters);
    }

    void RefreshHud()
    {
        if (m_StepHud == null)
            return;

        TitleTutorialStepHud.ModuleState walk = TitleTutorialStepHud.ModuleState.Pending;
        TitleTutorialStepHud.ModuleState swim = TitleTutorialStepHud.ModuleState.Pending;
        TitleTutorialStepHud.ModuleState teleport = TitleTutorialStepHud.ModuleState.Pending;
        bool flashB = false;

        switch (m_State)
        {
            case TutorialState.WalkingVoice:
            case TutorialState.WalkingPractice:
                walk = TitleTutorialStepHud.ModuleState.Active;
                break;
            case TutorialState.ModeSwitchVoice:
            case TutorialState.WaitForSwim:
                walk = TitleTutorialStepHud.ModuleState.Done;
                flashB = true;
                break;
            case TutorialState.SwimmingVoice:
            case TutorialState.SwimmingPractice:
                walk = TitleTutorialStepHud.ModuleState.Done;
                swim = TitleTutorialStepHud.ModuleState.Active;
                break;
            case TutorialState.WaitForTeleport:
                walk = TitleTutorialStepHud.ModuleState.Done;
                swim = TitleTutorialStepHud.ModuleState.Done;
                flashB = true;
                break;
            case TutorialState.TeleportVoice:
            case TutorialState.TeleportPractice:
                walk = TitleTutorialStepHud.ModuleState.Done;
                swim = TitleTutorialStepHud.ModuleState.Done;
                teleport = TitleTutorialStepHud.ModuleState.Active;
                break;
        }

        m_StepHud.SetStates(walk, swim, teleport, flashB);
    }

    void ApplyTutorialTexture(GameObject panel)
    {
        Renderer renderer = panel.GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null || m_TutorialTexture == null)
            return;

        renderer.material = new Material(renderer.sharedMaterial);
        renderer.material.SetTexture("_Texture2D", m_TutorialTexture);
    }

    static void StripColliders(GameObject panel)
    {
        if (panel == null)
            return;

        foreach (Collider collider in panel.GetComponents<Collider>())
            Destroy(collider);
    }

    static GameObject FindObjectByName(string objectName)
    {
        foreach (Transform transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform.name == objectName)
                return transform.gameObject;
        }

        return null;
    }

    void BuildInputActions()
    {
        m_AnyButton = new InputAction("Any tutorial start button", InputActionType.Button);
        AddControllerButtons(m_AnyButton, "LeftHand");
        AddControllerButtons(m_AnyButton, "RightHand");
        m_AnyButton.AddBinding("<XRController>/{PrimaryButton}");
        m_AnyButton.AddBinding("<XRController>/{SecondaryButton}");
        m_AnyButton.AddBinding("<XRController>/{TriggerButton}");
        m_AnyButton.AddBinding("<XRController>/{GripButton}");
        m_AnyButton.AddBinding("<Keyboard>/anyKey");
        m_AnyButton.AddBinding("<Mouse>/leftButton");
        m_AnyButton.AddBinding("<Gamepad>/buttonSouth");
        m_AnyButton.AddBinding("<Gamepad>/buttonNorth");
        m_AnyButton.AddBinding("<Gamepad>/buttonEast");
        m_AnyButton.AddBinding("<Gamepad>/buttonWest");

        m_LeftStick = new InputAction("Tutorial left stick", InputActionType.Value, "<XRController>{LeftHand}/{Primary2DAxis}");
        m_LeftStick.expectedControlType = "Vector2";
        m_LeftStick.AddBinding("<Gamepad>/leftStick");

        m_RightStick = new InputAction("Tutorial right stick", InputActionType.Value, "<XRController>{RightHand}/{Primary2DAxis}");
        m_RightStick.expectedControlType = "Vector2";
        m_RightStick.AddBinding("<Gamepad>/rightStick");

        m_JumpButton = new InputAction("Tutorial jump", InputActionType.Button, "<XRController>{RightHand}/{PrimaryButton}");
        m_JumpButton.AddBinding("<Gamepad>/buttonSouth");

        m_AnyTrigger = new InputAction("Any tutorial trigger", InputActionType.Button);
        m_AnyTrigger.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
        m_AnyTrigger.AddBinding("<XRController>{RightHand}/{TriggerButton}");
        m_AnyTrigger.AddBinding("<Gamepad>/leftTrigger").WithInteraction("press");
        m_AnyTrigger.AddBinding("<Gamepad>/rightTrigger").WithInteraction("press");
    }

    static void AddControllerButtons(InputAction action, string hand)
    {
        string prefix = $"<XRController>{{{hand}}}/";
        action.AddBinding(prefix + "{PrimaryButton}");
        action.AddBinding(prefix + "{SecondaryButton}");
        action.AddBinding(prefix + "{Primary2DAxisClick}");
        action.AddBinding(prefix + "{GripButton}");
        action.AddBinding(prefix + "{TriggerButton}");
        action.AddBinding(prefix + "{MenuButton}");
    }

    static bool GamepadButtonPressed()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null)
            return false;

        return pad.buttonSouth.wasPressedThisFrame ||
               pad.buttonNorth.wasPressedThisFrame ||
               pad.buttonEast.wasPressedThisFrame ||
               pad.buttonWest.wasPressedThisFrame ||
               pad.leftShoulder.wasPressedThisFrame ||
               pad.rightShoulder.wasPressedThisFrame ||
               pad.startButton.wasPressedThisFrame ||
               pad.selectButton.wasPressedThisFrame ||
               pad.leftTrigger.wasPressedThisFrame ||
               pad.rightTrigger.wasPressedThisFrame;
    }

    bool XrButtonWentDown()
    {
        bool held = AnyXrFeatureHeld(UnityEngine.XR.CommonUsages.primaryButton) ||
                    AnyXrFeatureHeld(UnityEngine.XR.CommonUsages.secondaryButton) ||
                    AnyXrFeatureHeld(UnityEngine.XR.CommonUsages.triggerButton) ||
                    AnyXrFeatureHeld(UnityEngine.XR.CommonUsages.gripButton) ||
                    AnyXrFeatureHeld(UnityEngine.XR.CommonUsages.menuButton) ||
                    AnyXrFeatureHeld(UnityEngine.XR.CommonUsages.primary2DAxisClick);
        bool pressed = held && !m_PrevXrButton;
        m_PrevXrButton = held;
        return pressed;
    }

    static bool AnyXrFeatureHeld(UnityEngine.XR.InputFeatureUsage<bool> usage)
    {
        s_XrDevices.Clear();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            UnityEngine.XR.InputDeviceCharacteristics.HeldInHand |
            UnityEngine.XR.InputDeviceCharacteristics.Controller,
            s_XrDevices);
        for (int i = 0; i < s_XrDevices.Count; i++)
        {
            if (s_XrDevices[i].TryGetFeatureValue(usage, out bool pressed) && pressed)
                return true;
        }

        return false;
    }

    static void DisableDuplicateListeners(Camera xrCamera)
    {
        foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (xrCamera != null && listener.gameObject == xrCamera.gameObject)
                continue;

            listener.enabled = false;
            if (listener.transform.parent == null)
                listener.gameObject.SetActive(false);
        }
    }

    static void SetPanel(GameObject panel, bool visible)
    {
        if (panel == null)
            return;

        panel.SetActive(visible);
        if (!visible)
            return;

        TitleTutorialPanelFollower follower = panel.GetComponent<TitleTutorialPanelFollower>();
        if (follower != null)
            follower.SnapToView();
    }
}
