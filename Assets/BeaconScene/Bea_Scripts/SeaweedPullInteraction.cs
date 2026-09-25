using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]

public class SeaweedPullInteraction : MonoBehaviour
{
    [Header("Tear Settings")]
    [SerializeField] private float tearPullDistance = 0.08f;

    [Header("Physics")]
    [Tooltip(
        "Keep the seaweed kinematic until it is torn or cut. The blade and the " +
        "hand-held scanner are kinematic bodies, so a dynamic seaweed gets shoved " +
        "out of the blade's path before BladeCutTrigger can reach SeaweedCutZone."
    )]
    [SerializeField] private bool holdStillUntilDetached = true;

    [Header("Completion Event")]
    public UnityEvent onDetached;

    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;
    private ConfigurableJoint anchorJoint;

    private Transform pullingHand;
    private Vector3 handStartPosition;

    private bool isHeld;
    private bool isDetached;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        anchorJoint = GetComponent<ConfigurableJoint>();

        rb.useGravity = false;
        rb.isKinematic = holdStillUntilDetached;
    }

    private void OnEnable()
    {
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.selectExited.RemoveListener(OnReleased);
    }

    private void Update()
    {
        if (!isHeld || isDetached || pullingHand == null)
            return;

        float handPullDistance =
            Vector3.Distance(pullingHand.position, handStartPosition);

        if (handPullDistance >= tearPullDistance)
        {
            TearFree();
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        isHeld = true;

        pullingHand =
            args.interactorObject.GetAttachTransform(grabInteractable);

        handStartPosition = pullingHand.position;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        isHeld = false;
        pullingHand = null;
    }

    private void TearFree()
    {
        isDetached = true;

        if (anchorJoint != null)
        {
            // Remove the restrictions immediately.
            anchorJoint.xMotion = ConfigurableJointMotion.Free;
            anchorJoint.yMotion = ConfigurableJointMotion.Free;
            anchorJoint.zMotion = ConfigurableJointMotion.Free;

            anchorJoint.angularXMotion = ConfigurableJointMotion.Free;
            anchorJoint.angularYMotion = ConfigurableJointMotion.Free;
            anchorJoint.angularZMotion = ConfigurableJointMotion.Free;

            Destroy(anchorJoint);
        }

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.WakeUp();

        onDetached?.Invoke();

        Debug.Log("Seaweed torn free from scanner.");
    }
}