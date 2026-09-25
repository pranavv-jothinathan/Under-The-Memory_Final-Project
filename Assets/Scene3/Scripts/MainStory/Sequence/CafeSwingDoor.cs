using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// 绕竖轴朝外开的门叶。抓取时跟手的水平方向走，角度夹在 0 到 maxAngle。
/// </summary>
[DisallowMultipleComponent]
public class CafeSwingDoor : MonoBehaviour
{
    [SerializeField] private Transform hinge;
    [SerializeField] private float openSign = 1f;
    [SerializeField] private float maxAngle = 170f;
    [SerializeField] private float grabStartAngle = 4f;
    [SerializeField] private Color glowColor = new Color(0.45f, 1f, 0.55f, 1f);
    [SerializeField] private float glowPulseSpeed = 1.6f;
    [SerializeField] private float glowMin = 0.35f;
    [SerializeField] private float glowMax = 2.4f;

    public event System.Action<CafeSwingDoor> Opened;

    public bool HasOpened { get; private set; }
    public float CurrentAngle { get; private set; }

    private XRSimpleInteractable interactable;
    private IXRSelectInteractor holding;
    private Vector3 closedLocalPosition;
    private Quaternion closedLocalRotation;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private bool glowEnabled = true;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public void Configure(Transform hingeAnchor, float sign, float angleLimit)
    {
        hinge = hingeAnchor;
        openSign = Mathf.Sign(sign) >= 0f ? 1f : -1f;
        maxAngle = Mathf.Clamp(angleLimit, 1f, 179f);
    }

    public void SetGlowEnabled(bool enabled)
    {
        glowEnabled = enabled;
        if (!enabled)
            ApplyGlow(0f);
    }

    private void Awake()
    {
        closedLocalPosition = transform.localPosition;
        closedLocalRotation = transform.localRotation;
        renderers = GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();

        EnsureCollider();
        interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null)
            interactable = gameObject.AddComponent<XRSimpleInteractable>();

        interactable.selectMode = InteractableSelectMode.Multiple;
        interactable.colliders.Clear();
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            if (!col.isTrigger)
                interactable.colliders.Add(col);
        }
    }

    private void OnEnable()
    {
        if (interactable == null)
            interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null)
            return;

        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        if (interactable == null)
            return;

        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.selectExited.RemoveListener(OnSelectExited);
        holding = null;
    }

    private void Update()
    {
        if (holding != null)
            FollowHand();

        ApplyPose();

        if (glowEnabled && !HasOpened)
        {
            float pulse = Mathf.Lerp(glowMin, glowMax, (Mathf.Sin(Time.time * glowPulseSpeed) + 1f) * 0.5f);
            ApplyGlow(pulse);
        }
    }

    private void FollowHand()
    {
        if (hinge == null || holding == null)
            return;

        Vector3 hingePos = hinge.position;
        Vector3 closedForward = ClosedForward();
        Vector3 toHand = holding.transform.position - hingePos;
        toHand.y = 0f;
        if (toHand.sqrMagnitude < 0.0001f)
            return;

        float signed = Vector3.SignedAngle(closedForward, toHand.normalized, Vector3.up);
        float alongOpen = signed * openSign;
        CurrentAngle = Mathf.Clamp(alongOpen, 0f, maxAngle);

        if (!HasOpened && CurrentAngle >= grabStartAngle)
            MarkOpened();
    }

    private void ApplyPose()
    {
        transform.localPosition = closedLocalPosition;
        transform.localRotation = closedLocalRotation;
        if (hinge == null || Mathf.Abs(CurrentAngle) < 0.01f)
            return;

        transform.RotateAround(hinge.position, Vector3.up, CurrentAngle * openSign);
    }

    private Vector3 ClosedWorldPosition()
    {
        if (transform.parent != null)
            return transform.parent.TransformPoint(closedLocalPosition);

        return closedLocalPosition;
    }

    private Vector3 ClosedForward()
    {
        Vector3 hingePos = hinge != null ? hinge.position : ClosedWorldPosition();
        Vector3 fromHinge = ClosedWorldPosition() - hingePos;
        fromHinge.y = 0f;
        if (fromHinge.sqrMagnitude < 0.0001f)
            fromHinge = Vector3.forward;

        return fromHinge.normalized;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        holding = args.interactorObject;
        if (!HasOpened)
            MarkOpened();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (holding == args.interactorObject)
            holding = null;
    }

    private void MarkOpened()
    {
        if (HasOpened)
            return;

        HasOpened = true;
        SetGlowEnabled(false);
        Opened?.Invoke(this);
    }

    private void ApplyGlow(float intensity)
    {
        if (renderers == null)
            return;

        Color color = glowColor * intensity;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }

    private void EnsureCollider()
    {
        if (GetComponentInChildren<Collider>() != null)
            return;

        Bounds b = new Bounds(transform.position, Vector3.one * 0.2f);
        bool any = false;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!any)
            {
                b = renderer.bounds;
                any = true;
            }
            else
            {
                b.Encapsulate(renderer.bounds);
            }
        }

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(b.center);
        Vector3 lossy = transform.lossyScale;
        box.size = new Vector3(
            SafeDiv(b.size.x, lossy.x),
            SafeDiv(b.size.y, lossy.y),
            SafeDiv(b.size.z, lossy.z)
        );
    }

    private static float SafeDiv(float value, float scale)
    {
        return Mathf.Abs(scale) < 0.0001f ? value : value / scale;
    }
}
