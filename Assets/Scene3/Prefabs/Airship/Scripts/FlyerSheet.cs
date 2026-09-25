using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Single-page flyer. Frozen until first grab unless released in air by the airship.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class FlyerSheet : MonoBehaviour
{
    public const float Width = 0.148f;
    public const float Height = 0.210f;
    public const float Thickness = 0.0012f;

    [SerializeField] private bool sleepUntilGrabbed = true;

    private XRGrabInteractable grab;
    private Rigidbody body;
    private SoftPaper[] papers;
    private IXRSelectInteractor heldBy;

    public bool IsHeldByPlayer => grab != null && grab.isSelected;

    public void ReleaseInAir(Vector3 velocity, Vector3 angularVelocity)
    {
        sleepUntilGrabbed = false;
        if (body == null)
            body = GetComponent<Rigidbody>();

        GrabPhysics.ActivateAfterDrop(body);
        body.linearVelocity = velocity;
        body.angularVelocity = angularVelocity;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        papers = GetComponentsInChildren<SoftPaper>(true);

        if (sleepUntilGrabbed)
            GrabPhysics.SleepUntilGrabbed(body);
        else
            GrabPhysics.ActivateAfterDrop(body);

        if (grab != null)
        {
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = true;
            grab.attachEaseInTime = 0f;
        }
    }

    private void OnEnable()
    {
        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();
        if (grab == null)
            return;

        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        if (grab == null)
            return;

        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    private void LateUpdate()
    {
        Vector3 support = Vector3.zero;
        float flutter = 1f;

        if (IsHeldByPlayer && heldBy != null)
        {
            support = transform.InverseTransformPoint(heldBy.transform.position);
            support.z = 0f;
            flutter = 0.15f;
        }
        else if (body != null && body.linearVelocity.sqrMagnitude < 0.04f)
        {
            flutter = 0.2f;
        }

        if (papers == null)
            return;

        for (int i = 0; i < papers.Length; i++)
        {
            if (papers[i] == null)
                continue;
            papers[i].SetSupportLocal(support);
            papers[i].SetFlutterScale(flutter);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        heldBy = args.interactorObject;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        heldBy = null;
        StartCoroutine(EnableGravityAfterDetach());
    }

    private IEnumerator EnableGravityAfterDetach()
    {
        yield return null;
        GrabPhysics.ActivateAfterDrop(body);
    }
}
