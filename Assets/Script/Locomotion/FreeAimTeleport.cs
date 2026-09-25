using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

/// <summary>
/// Teleport mode: stick Y integrates ray length (forward longer, back shorter).
/// Stick X still snap-turns. Release Y to confirm. The sphere is the body center.
/// </summary>
public class FreeAimTeleport : MonoBehaviour
{
    [SerializeField] Transform m_RayOrigin;
    [SerializeField] Transform m_BodyCenter;
    [SerializeField] Transform m_Origin;
    [SerializeField] CharacterController m_CharacterController;
    [SerializeField] InputActionReference m_AimStickAction;
    [SerializeField] string m_AimStickFallbackPath = "<XRController>{RightHand}/{Primary2DAxis}";

    [Header("Length")]
    [SerializeField] [Tooltip("How fast the ray grows or shrinks when pushing the right stick forward or back, in meters per second.")]
    float m_LengthChangeSpeed = 10f;
    [SerializeField] float m_MinDistance;
    [SerializeField] float m_MaxDistance = 25f;
    [SerializeField] float m_Deadzone = 0.35f;
    [SerializeField] float m_VisibleDistance = 0.1f;
    [SerializeField] float m_SurfaceBackoff = 0.35f;

    [Header("Visuals")]
    [SerializeField] float m_LineWidth = 0.012f;
    [SerializeField] Color m_ValidColor = new Color(0.25f, 0.85f, 1f, 0.95f);
    [SerializeField] Color m_BlockedColor = new Color(1f, 0.25f, 0.2f, 0.95f);
    [SerializeField] LayerMask m_BlockingMask = ~0;

    InputAction m_AimStick;
    bool m_OwnsAimStick;
    bool m_Active;
    bool m_WasAdjustingLength;
    bool m_LandingValid;
    float m_Distance;
    Vector3 m_LandingPoint;

    LineRenderer m_Line;
    Transform m_Marker;
    Renderer m_MarkerRenderer;
    Material m_LineMaterial;
    Material m_MarkerMaterial;

    public event System.Action Teleported;

    public void SetActive(bool active)
    {
        m_Active = active;
        if (!active)
            ResetAim();
    }

    void Awake()
    {
        var xrOrigin = GetComponentInParent<XROrigin>();
        if (m_Origin == null)
            m_Origin = xrOrigin != null ? xrOrigin.Origin.transform : transform.root;
        if (m_BodyCenter == null)
            m_BodyCenter = xrOrigin != null && xrOrigin.Camera != null ? xrOrigin.Camera.transform : Camera.main?.transform;
        if (m_CharacterController == null)
            m_CharacterController = GetComponentInParent<CharacterController>();
        if (m_RayOrigin == null)
        {
            var right = FindChildByName(transform.root, "Right Controller");
            m_RayOrigin = right != null ? right : transform;
        }

        BuildVisuals();
    }

    void OnEnable()
    {
        m_AimStick = LocomotionModeSwitcher.ResolveValueAction(
            m_AimStickAction,
            m_AimStickFallbackPath,
            out m_OwnsAimStick);
        m_AimStick?.Enable();
    }

    void OnDisable()
    {
        ResetAim();
        if (m_OwnsAimStick && m_AimStick != null)
        {
            m_AimStick.Disable();
            m_AimStick.Dispose();
        }

        m_AimStick = null;
        m_OwnsAimStick = false;
    }

    void OnDestroy()
    {
        if (m_LineMaterial != null)
            Destroy(m_LineMaterial);
        if (m_MarkerMaterial != null)
            Destroy(m_MarkerMaterial);
    }

    void Update()
    {
        if (!m_Active || m_AimStick == null || m_RayOrigin == null || m_BodyCenter == null || m_Origin == null)
        {
            HideVisuals();
            return;
        }

        Vector2 stick = m_AimStick.ReadValue<Vector2>();
        bool adjusting = Mathf.Abs(stick.y) >= m_Deadzone;

        if (adjusting)
        {
            m_Distance += stick.y * m_LengthChangeSpeed * Time.deltaTime;
            m_Distance = Mathf.Clamp(m_Distance, m_MinDistance, m_MaxDistance);
        }

        if (m_Distance >= m_VisibleDistance)
            UpdateAim();
        else
            HideVisuals();

        if (m_WasAdjustingLength && !adjusting)
            TryConfirm();

        m_WasAdjustingLength = adjusting;
    }

    void TryConfirm()
    {
        if (m_Distance < m_VisibleDistance || !m_LandingValid)
            return;

        TeleportTo(m_LandingPoint);
        ResetAim();
    }

    void UpdateAim()
    {
        Vector3 direction = m_RayOrigin.forward;
        Vector3 origin = m_RayOrigin.position + direction * 0.08f;
        Vector3 candidate;
        bool blockedByGeometry = false;
        float aimDistance = m_Distance;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, m_Distance, m_BlockingMask, QueryTriggerInteraction.Ignore))
        {
            aimDistance = Mathf.Max(0f, hit.distance - m_SurfaceBackoff);
            candidate = origin + direction * aimDistance;
            if (hit.distance <= m_SurfaceBackoff)
                blockedByGeometry = true;
        }
        else
        {
            candidate = origin + direction * m_Distance;
        }

        m_LandingPoint = candidate;
        m_LandingValid = !blockedByGeometry && aimDistance >= m_VisibleDistance && CanFitAt(candidate);

        ShowAim(origin, candidate, m_LandingValid);
    }

    bool CanFitAt(Vector3 bodyCenter)
    {
        Vector3 cameraOffset = m_BodyCenter.position - m_Origin.position;
        Vector3 newOrigin = bodyCenter - cameraOffset;

        if (m_CharacterController == null)
            return !Physics.CheckSphere(bodyCenter, 0.2f, m_BlockingMask, QueryTriggerInteraction.Ignore);

        Vector3 worldUp = m_Origin.up;
        float height = Mathf.Max(m_CharacterController.height, m_CharacterController.radius * 2f);
        Vector3 center = newOrigin + m_Origin.TransformVector(m_CharacterController.center);
        Vector3 bottom = center - worldUp * (height * 0.5f - m_CharacterController.radius);
        Vector3 top = center + worldUp * (height * 0.5f - m_CharacterController.radius);
        return !Physics.CheckCapsule(bottom, top, m_CharacterController.radius, m_BlockingMask, QueryTriggerInteraction.Ignore);
    }

    void TeleportTo(Vector3 bodyCenter)
    {
        Vector3 cameraOffset = m_BodyCenter.position - m_Origin.position;
        Vector3 newOrigin = bodyCenter - cameraOffset;

        if (m_CharacterController != null)
            m_CharacterController.enabled = false;

        m_Origin.position = newOrigin;

        if (m_CharacterController != null)
            m_CharacterController.enabled = true;

        Teleported?.Invoke();
    }

    void BuildVisuals()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        m_LineMaterial = new Material(shader);
        m_MarkerMaterial = new Material(shader);

        var lineObject = new GameObject("TeleportAimLine");
        lineObject.transform.SetParent(transform, false);
        m_Line = lineObject.AddComponent<LineRenderer>();
        m_Line.positionCount = 2;
        m_Line.useWorldSpace = true;
        m_Line.widthMultiplier = m_LineWidth;
        m_Line.material = m_LineMaterial;
        m_Line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        m_Line.receiveShadows = false;
        m_Line.enabled = false;

        var markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        markerObject.name = "TeleportBodyMarker";
        Destroy(markerObject.GetComponent<Collider>());
        markerObject.transform.SetParent(transform, false);
        markerObject.transform.localScale = Vector3.one * 0.22f;
        m_Marker = markerObject.transform;
        m_MarkerRenderer = markerObject.GetComponent<Renderer>();
        m_MarkerRenderer.sharedMaterial = m_MarkerMaterial;
        m_MarkerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        markerObject.SetActive(false);
    }

    void ShowAim(Vector3 start, Vector3 end, bool valid)
    {
        Color color = valid ? m_ValidColor : m_BlockedColor;
        m_LineMaterial.color = color;
        m_MarkerMaterial.color = color;

        m_Line.enabled = true;
        m_Line.SetPosition(0, start);
        m_Line.SetPosition(1, end);

        m_Marker.gameObject.SetActive(true);
        m_Marker.position = end;
    }

    void HideVisuals()
    {
        m_LandingValid = false;
        if (m_Line != null)
            m_Line.enabled = false;
        if (m_Marker != null)
            m_Marker.gameObject.SetActive(false);
    }

    void ResetAim()
    {
        m_Distance = 0f;
        m_WasAdjustingLength = false;
        HideVisuals();
    }

    static Transform FindChildByName(Transform root, string name)
    {
        if (root == null)
            return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }

        return null;
    }
}
