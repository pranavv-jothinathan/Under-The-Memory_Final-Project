using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using XR.Interaction.Toolkit.Samples;

public enum PlayerLocomotionMode
{
    Walk,
    Swim,
    Teleport
}

/// <summary>
/// Cycles Walk / Swim / Teleport with the right-hand B button.
/// Teleport keeps Snap Turn for stick X and disables Turn Around so stick Y can shorten the ray.
/// Disables the stock XRI parabola teleport so it cannot fight this system.
/// </summary>
public class LocomotionModeSwitcher : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] PlayerLocomotionMode m_StartMode = PlayerLocomotionMode.Walk;

    [Header("Input")]
    [SerializeField] InputActionReference m_ModeSwitchAction;
    [SerializeField] string m_ModeSwitchFallbackPath = "<XRController>{RightHand}/{SecondaryButton}";

    [Header("Providers")]
    [SerializeField] DynamicMoveProvider m_MoveProvider;
    [SerializeField] SnapTurnProvider m_SnapTurnProvider;
    [SerializeField] ContinuousTurnProvider m_ContinuousTurnProvider;
    [SerializeField] CharacterJump m_Jump;
    [SerializeField] SwimVerticalInput m_SwimVertical;
    [SerializeField] FreeAimTeleport m_Teleport;

    InputAction m_ModeSwitch;
    bool m_OwnsModeSwitch;
    PlayerLocomotionMode m_Mode;
    bool m_SavedTurnAround = true;
    bool m_HasSavedTurnAround;
    bool m_ModeSwitchInputAllowed = true;
    GameObject[] m_StockTeleportInteractors;

    public PlayerLocomotionMode mode => m_Mode;
    public event Action<PlayerLocomotionMode> ModeChanged;

    void Awake()
    {
        if (m_MoveProvider == null)
            m_MoveProvider = GetComponentInChildren<DynamicMoveProvider>(true);
        if (m_SnapTurnProvider == null)
            m_SnapTurnProvider = GetComponentInChildren<SnapTurnProvider>(true);
        if (m_ContinuousTurnProvider == null)
            m_ContinuousTurnProvider = GetComponentInChildren<ContinuousTurnProvider>(true);
        if (m_Jump == null)
            m_Jump = GetComponent<CharacterJump>();
        if (m_SwimVertical == null)
            m_SwimVertical = GetComponent<SwimVerticalInput>();
        if (m_Teleport == null)
            m_Teleport = GetComponent<FreeAimTeleport>();

        SetMode(m_StartMode);
    }

    void OnEnable()
    {
        m_ModeSwitch = ResolveAction(m_ModeSwitchAction, m_ModeSwitchFallbackPath, out m_OwnsModeSwitch);
        if (m_ModeSwitch != null)
        {
            m_ModeSwitch.Enable();
            m_ModeSwitch.performed += OnModeSwitch;
        }
    }

    void OnDisable()
    {
        if (m_ModeSwitch != null)
            m_ModeSwitch.performed -= OnModeSwitch;
        if (m_OwnsModeSwitch && m_ModeSwitch != null)
        {
            m_ModeSwitch.Disable();
            m_ModeSwitch.Dispose();
        }

        m_ModeSwitch = null;
        m_OwnsModeSwitch = false;
    }

    IEnumerator Start()
    {
        yield return null;
        DisableStockParabolaTeleport();
        SetMode(m_StartMode);
    }

    void LateUpdate()
    {
        if (m_StockTeleportInteractors == null)
            return;

        for (int i = 0; i < m_StockTeleportInteractors.Length; i++)
        {
            var go = m_StockTeleportInteractors[i];
            if (go != null && go.activeSelf)
                go.SetActive(false);
        }
    }

    void OnModeSwitch(InputAction.CallbackContext context)
    {
        if (!context.performed || !m_ModeSwitchInputAllowed)
            return;

        PlayerLocomotionMode next = m_Mode switch
        {
            PlayerLocomotionMode.Walk => PlayerLocomotionMode.Swim,
            PlayerLocomotionMode.Swim => PlayerLocomotionMode.Teleport,
            _ => PlayerLocomotionMode.Walk
        };
        SetMode(next);
    }

    public void SetModeSwitchInputEnabled(bool enabled)
    {
        m_ModeSwitchInputAllowed = enabled;
    }

    public void SetMode(PlayerLocomotionMode next)
    {
        bool changed = m_Mode != next;
        m_Mode = next;

        bool walk = next == PlayerLocomotionMode.Walk;
        bool swim = next == PlayerLocomotionMode.Swim;
        bool teleport = next == PlayerLocomotionMode.Teleport;

        if (m_MoveProvider != null)
        {
            m_MoveProvider.enabled = true;
            m_MoveProvider.enableFly = swim;
            m_MoveProvider.useGravity = false;
        }

        if (m_SnapTurnProvider != null)
        {
            if (!m_HasSavedTurnAround)
            {
                m_SavedTurnAround = m_SnapTurnProvider.enableTurnAround;
                m_HasSavedTurnAround = true;
            }

            m_SnapTurnProvider.enabled = true;
            m_SnapTurnProvider.enableTurnAround = teleport ? false : m_SavedTurnAround;
        }

        if (m_ContinuousTurnProvider != null)
            m_ContinuousTurnProvider.enabled = false;

        if (m_Jump != null)
            m_Jump.SetActive(walk);
        if (m_SwimVertical != null)
            m_SwimVertical.SetActive(false);
        if (m_Teleport != null)
            m_Teleport.SetActive(teleport);

        Debug.Log($"Locomotion mode: {next}", this);
        if (changed)
            ModeChanged?.Invoke(next);
    }

    void DisableStockParabolaTeleport()
    {
        Transform root = transform.root;
        var found = new List<GameObject>();
        foreach (var ray in root.GetComponentsInChildren<XRRayInteractor>(true))
        {
            if (ray == null || ray.gameObject.name != "Teleport Interactor")
                continue;

            found.Add(ray.gameObject);
            ray.gameObject.SetActive(false);
        }

        m_StockTeleportInteractors = found.ToArray();

        var teleportation = transform.Find("Teleportation");
        if (teleportation != null)
            teleportation.gameObject.SetActive(false);

        foreach (var manager in root.GetComponentsInChildren<InputActionManager>(true))
            DisableTeleportActions(manager);
    }

    static void DisableTeleportActions(InputActionManager manager)
    {
        if (manager == null || manager.actionAssets == null)
            return;

        foreach (var asset in manager.actionAssets)
        {
            if (asset == null)
                continue;

            DisableAction(asset, "XRI Left Locomotion", "Teleport Mode");
            DisableAction(asset, "XRI Left Locomotion", "Teleport Mode Cancel");
            DisableAction(asset, "XRI Right Locomotion", "Teleport Mode");
            DisableAction(asset, "XRI Right Locomotion", "Teleport Mode Cancel");
        }
    }

    static void DisableAction(InputActionAsset asset, string mapName, string actionName)
    {
        var action = asset.FindActionMap(mapName, false)?.FindAction(actionName, false);
        if (action != null && action.enabled)
            action.Disable();
    }

    internal static InputAction ResolveAction(
        InputActionReference reference,
        string fallbackPath,
        out bool ownsAction)
    {
        if (reference != null && reference.action != null)
        {
            ownsAction = false;
            return reference.action;
        }

        ownsAction = true;
        var action = new InputAction(type: InputActionType.Button, binding: fallbackPath);
        return action;
    }

    internal static InputAction ResolveValueAction(
        InputActionReference reference,
        string fallbackPath,
        out bool ownsAction)
    {
        if (reference != null && reference.action != null)
        {
            ownsAction = false;
            return reference.action;
        }

        ownsAction = true;
        var action = new InputAction(type: InputActionType.Value, binding: fallbackPath);
        action.expectedControlType = "Vector2";
        return action;
    }
}
