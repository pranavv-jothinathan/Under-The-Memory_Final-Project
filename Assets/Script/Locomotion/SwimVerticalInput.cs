using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

/// <summary>
/// Unused. Swim vertical now comes from head-relative fly on DynamicMoveProvider.
/// Kept so the XR Origin prefab does not lose the component reference.
/// </summary>
public class SwimVerticalInput : MonoBehaviour
{
    [SerializeField] CharacterController m_CharacterController;
    [SerializeField] Transform m_Origin;
    [SerializeField] InputActionReference m_AscendAction;
    [SerializeField] InputActionReference m_DescendAction;
    [SerializeField] string m_AscendFallbackPath = "<XRController>{RightHand}/{PrimaryButton}";
    [SerializeField] string m_DescendFallbackPath = "<XRController>{LeftHand}/{PrimaryButton}";
    [SerializeField] float m_VerticalSpeed = 2.2f;

    InputAction m_Ascend;
    InputAction m_Descend;
    bool m_OwnsAscend;
    bool m_OwnsDescend;
    bool m_Active;

    public void SetActive(bool active)
    {
        m_Active = active;
    }

    void Awake()
    {
        if (m_CharacterController == null)
            m_CharacterController = GetComponentInParent<CharacterController>();

        if (m_Origin == null)
        {
            var xrOrigin = GetComponentInParent<XROrigin>();
            m_Origin = xrOrigin != null ? xrOrigin.Origin.transform : transform.root;
        }
    }

    void OnEnable()
    {
        m_Ascend = LocomotionModeSwitcher.ResolveAction(m_AscendAction, m_AscendFallbackPath, out m_OwnsAscend);
        m_Descend = LocomotionModeSwitcher.ResolveAction(m_DescendAction, m_DescendFallbackPath, out m_OwnsDescend);
        m_Ascend?.Enable();
        m_Descend?.Enable();
    }

    void OnDisable()
    {
        DisposeAction(ref m_Ascend, m_OwnsAscend);
        DisposeAction(ref m_Descend, m_OwnsDescend);
        m_OwnsAscend = false;
        m_OwnsDescend = false;
    }

    void Update()
    {
        if (!m_Active)
            return;

        float dir = 0f;
        if (m_Ascend != null && m_Ascend.IsPressed())
            dir += 1f;
        if (m_Descend != null && m_Descend.IsPressed())
            dir -= 1f;

        if (Mathf.Approximately(dir, 0f))
            return;

        Vector3 motion = Vector3.up * (dir * m_VerticalSpeed * Time.deltaTime);
        if (m_CharacterController != null && m_CharacterController.enabled)
            m_CharacterController.Move(motion);
        else if (m_Origin != null)
            m_Origin.position += motion;
    }

    static void DisposeAction(ref InputAction action, bool owns)
    {
        if (owns && action != null)
        {
            action.Disable();
            action.Dispose();
        }

        action = null;
    }
}
