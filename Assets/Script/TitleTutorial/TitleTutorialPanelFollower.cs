using UnityEngine;

/// <summary>
/// Places a TitleScene world-space quad in front of the XR head the same way
/// Scene3 story tips do: snap on show, then lazy-follow. Never parent to the camera.
/// </summary>
[DefaultExecutionOrder(210)]
public sealed class TitleTutorialPanelFollower : MonoBehaviour
{
    [SerializeField, Min(0.05f)] float m_WidthMeters = 0.8f;
    [SerializeField, Min(0.3f)] float m_DistanceFromHead = 1.2f;
    [SerializeField] float m_VerticalOffset = -0.1f;
    [SerializeField] float m_ReorientAngle = 35f;
    [SerializeField] float m_MaxDrift = 1.5f;
    [SerializeField] float m_FollowSpeed = 4f;
    [SerializeField] bool m_FlipFacing;

    Vector3 m_AnchorPosition;
    Vector3 m_AnchorHeadPosition;
    bool m_HasAnchor;

    public void Configure(float widthMeters)
    {
        if (widthMeters > 0.05f)
            m_WidthMeters = widthMeters;

        ApplyScale();
    }

    public void SnapToView()
    {
        ApplyScale();

        Transform head = PlayerHead.Transform;
        if (head == null)
            return;

        m_AnchorHeadPosition = head.position;
        m_AnchorPosition = DesiredPosition(head);
        m_HasAnchor = true;

        transform.position = m_AnchorPosition;
        transform.rotation = FaceRotation(head);
    }

    void OnEnable()
    {
        SnapToView();
    }

    void LateUpdate()
    {
        Transform head = PlayerHead.Transform;
        if (head == null)
            return;

        if (!m_HasAnchor)
            SnapToView();

        Vector3 toPanel = m_AnchorPosition - head.position;
        float offAxis = toPanel.sqrMagnitude > 0.0001f
            ? Vector3.Angle(head.forward, toPanel)
            : 0f;
        bool drifted = Vector3.Distance(head.position, m_AnchorHeadPosition) > m_MaxDrift;

        if (offAxis > m_ReorientAngle || drifted)
        {
            m_AnchorHeadPosition = head.position;
            m_AnchorPosition = DesiredPosition(head);
        }

        float k = 1f - Mathf.Exp(-m_FollowSpeed * Time.unscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, m_AnchorPosition, k);
        transform.rotation = Quaternion.Slerp(transform.rotation, FaceRotation(head), k);
    }

    void ApplyScale()
    {
        float aspect = ReadAspect();
        transform.localScale = new Vector3(m_WidthMeters, m_WidthMeters / aspect, 1f);
    }

    float ReadAspect()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            Texture texture = null;
            if (renderer.sharedMaterial.HasProperty("_Texture2D"))
                texture = renderer.sharedMaterial.GetTexture("_Texture2D");
            if (texture == null)
                texture = renderer.sharedMaterial.mainTexture;
            if (texture != null && texture.height > 0)
                return texture.width / (float)texture.height;
        }

        Vector3 scale = transform.localScale;
        if (scale.y > 0.001f)
            return Mathf.Abs(scale.x / scale.y);

        return 2.4f;
    }

    Vector3 DesiredPosition(Transform head)
    {
        return head.position
            + head.forward * m_DistanceFromHead
            + Vector3.up * m_VerticalOffset;
    }

    Quaternion FaceRotation(Transform head)
    {
        Vector3 look = transform.position - head.position;
        if (look.sqrMagnitude < 0.0001f)
            look = head.forward;

        Quaternion rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        return m_FlipFacing
            ? rotation * Quaternion.Euler(0f, 180f, 0f)
            : rotation;
    }
}
