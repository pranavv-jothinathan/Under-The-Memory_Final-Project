using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Walk-mode jump. Owns gravity while active so it does not double-apply
/// with Continuous Move Provider.
/// </summary>
public class CharacterJump : MonoBehaviour
{
    [SerializeField] CharacterController m_CharacterController;
    [SerializeField] InputActionReference m_JumpAction;
    [SerializeField] string m_JumpFallbackPath = "<XRController>{RightHand}/{PrimaryButton}";
    [SerializeField] float m_JumpSpeed = 4.2f;
    [SerializeField] float m_GravityMultiplier = 1f;

    InputAction m_Jump;
    bool m_OwnsJump;
    bool m_Active;
    float m_VerticalSpeed;
    bool m_WasGrounded = true;

    public void SetActive(bool active)
    {
        m_Active = active;
        if (!active)
            m_VerticalSpeed = 0f;
    }

    void Awake()
    {
        if (m_CharacterController == null)
            m_CharacterController = GetComponentInParent<CharacterController>();
    }

    void OnEnable()
    {
        m_Jump = LocomotionModeSwitcher.ResolveAction(m_JumpAction, m_JumpFallbackPath, out m_OwnsJump);
        m_Jump?.Enable();
    }

    void OnDisable()
    {
        if (m_OwnsJump && m_Jump != null)
        {
            m_Jump.Disable();
            m_Jump.Dispose();
        }

        m_Jump = null;
        m_OwnsJump = false;
    }

    void Update()
    {
        if (!m_Active || m_CharacterController == null || !m_CharacterController.enabled)
            return;

        bool grounded = m_CharacterController.isGrounded;
        bool jumpPressed = m_Jump != null && m_Jump.WasPressedThisFrame();

        if (grounded)
        {
            if (!m_WasGrounded)
                m_VerticalSpeed = 0f;

            if (jumpPressed)
                m_VerticalSpeed = m_JumpSpeed;
            else if (m_VerticalSpeed < 0f)
                m_VerticalSpeed = -2f;
        }
        else
        {
            m_VerticalSpeed += Physics.gravity.y * m_GravityMultiplier * Time.deltaTime;
        }

        m_CharacterController.Move(Vector3.up * m_VerticalSpeed * Time.deltaTime);
        m_WasGrounded = grounded;
    }
}
