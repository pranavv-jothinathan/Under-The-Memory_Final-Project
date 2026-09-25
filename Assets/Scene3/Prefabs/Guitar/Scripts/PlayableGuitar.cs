using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

/// <summary>
/// Wearable guitar. Bring it to the chest to strap on. Right-hand B takes it off.
/// Left Grip near the neck is an FPS-style off-hand hold.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class PlayableGuitar : MonoBehaviour
{
    [Header("Colliders")]
    [SerializeField] private BoxCollider bodyCollider;
    [SerializeField] private BoxCollider neckZone;
    [SerializeField] private BoxCollider strumZone;

    [Header("Wear")]
    [SerializeField] private float wearDistance = 0.28f;
    [SerializeField] private float chestDrop = 0.42f;
    [SerializeField] private float chestForward = 0.12f;
    [SerializeField] private Vector3 wearEuler = new Vector3(-8f, 18f, 52f);
    [SerializeField] private float wearSnapSharpness = 18f;
    [SerializeField] private float bodyYawSharpness = 8f;
    [SerializeField] private float unwearCooldown = 1.2f;
    [SerializeField] private float wearResumeDistance = 0.40f;

    [Header("Neck Grip")]
    [SerializeField] private float neckHoldRadius = 0.10f;
    [SerializeField] private float neckFollowMax = 0.04f;
    [SerializeField] private float neckTiltMax = 8f;

    [Header("Strum")]
    [SerializeField] private float strumSpeed = 0.25f;
    [SerializeField] private float strumReach = 0.06f;
    [SerializeField] private float strumHapticAmplitude = 0.55f;
    [SerializeField] private float strumHapticDuration = 0.06f;
    [SerializeField] private float strumHapticCooldown = 0.14f;

    private XRGrabInteractable grab;
    private Rigidbody body;
    private GuitarStrumAudio strumAudio;

    private bool worn;
    private bool justWorn;
    private bool mustLeaveChest;
    private float wearBlockedUntil;
    private float smoothedYaw;
    private bool haveSmoothedYaw;
    private InputAction unwearAction;

    private XRBaseInputInteractor leftHand;
    private XRBaseInputInteractor rightHand;
    private Vector3 leftPos;
    private Vector3 rightPos;
    private Vector3 leftVel;
    private Vector3 rightVel;
    private float leftTrigger;
    private float rightTrigger;
    private bool leftGrip;
    private bool rightGrip;
    private bool neckHeld;

    private Vector3 lastLeftPos;
    private Vector3 lastRightPos;
    private bool haveLastLeft;
    private bool haveLastRight;
    private float nextHandRefresh;
    private float nextStrumHapticTime;

    public bool IsWorn => worn;
    public bool IsHeldByPlayer => grab != null && grab.isSelected;
    public bool IsNeckHeld => neckHeld;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        strumAudio = GetComponent<GuitarStrumAudio>();
        ResolveCollider(ref bodyCollider, "BodyCollider");
        ResolveCollider(ref neckZone, "NeckZone");
        ResolveCollider(ref strumZone, "StrumZone");

        ClearStaticFlags();
        FitPlayZones();
        StripStrap();
        GrabPhysics.SleepUntilGrabbed(body);
        SetBodySolid(true);

        if (grab != null)
        {
            grab.enabled = true;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.useDynamicAttach = true;
            grab.attachEaseInTime = 0f;
            AssignGrabColliders();
        }
    }

    private void OnEnable()
    {
        EnsureUnwearAction();
        if (unwearAction != null)
            unwearAction.Enable();

        if (grab == null)
            grab = GetComponent<XRGrabInteractable>();
        if (grab == null)
            return;

        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        if (unwearAction != null)
            unwearAction.Disable();

        if (grab == null)
            return;

        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnDestroy()
    {
        if (unwearAction == null)
            return;

        unwearAction.Disable();
        unwearAction.Dispose();
        unwearAction = null;
    }

    private void Update()
    {
        RefreshHands();

        if (!worn)
        {
            neckHeld = false;
            if (CanWearNow() && IsHeldByPlayer && IsNearChest())
                Wear();
            return;
        }

        UpdateNeckHold();

        if (IsRightHandStrumming())
            TryPulseStrumHaptic();

        if (WasUnwearPressed())
            Unwear(rightHand);
    }

    private void LateUpdate()
    {
        if (worn)
        {
            LockWornPhysics();
            ApplyWornPose();
        }

        bool hideBody = worn || IsHeldByPlayer;
        SetBodySolid(!hideBody);
        if (body != null && hideBody)
            body.detectCollisions = false;
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        SetBodySolid(false);
        if (body != null)
            body.detectCollisions = false;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (worn)
            return;

        StartCoroutine(EnableGravityAfterDetach());
    }

    private IEnumerator EnableGravityAfterDetach()
    {
        yield return null;
        if (worn)
            yield break;

        SetBodySolid(true);
        if (body != null)
        {
            body.detectCollisions = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
        GrabPhysics.ActivateAfterDrop(body);
    }

    private void Wear()
    {
        if (worn)
            return;

        worn = true;
        justWorn = true;
        neckHeld = false;
        haveSmoothedYaw = false;

        if (grab != null)
            grab.throwOnDetach = false;

        SetBodySolid(false);
        LockWornPhysics();
        DropFromHands();

        if (grab != null)
            grab.enabled = false;

        LockWornPhysics();
        ApplyWornPose();
        StartCoroutine(RelockWornPhysicsAfterXriDrop());
    }

    private void LockWornPhysics()
    {
        if (body == null)
            return;

        body.isKinematic = true;
        body.useGravity = false;
        body.detectCollisions = false;
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        SetBodySolid(false);
    }

    private IEnumerator RelockWornPhysicsAfterXriDrop()
    {
        yield return null;
        if (worn)
            LockWornPhysics();

        yield return new WaitForFixedUpdate();
        if (worn)
            LockWornPhysics();
    }

    private void Unwear(XRBaseInputInteractor interactor)
    {
        if (!worn)
            return;

        worn = false;
        neckHeld = false;
        mustLeaveChest = true;
        wearBlockedUntil = Time.time + unwearCooldown;
        if (strumAudio != null)
            strumAudio.StopPlaying();

        if (grab != null)
        {
            grab.enabled = true;
            grab.throwOnDetach = true;
            grab.useDynamicAttach = true;
            if (grab.interactionManager == null)
                grab.interactionManager = FindFirstObjectByType<XRInteractionManager>();
        }

        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
        }

        SetBodySolid(false);

        if (interactor != null && grab != null && grab.interactionManager != null)
            grab.interactionManager.SelectEnter((IXRSelectInteractor)interactor, (IXRSelectInteractable)grab);

        if (grab != null && !grab.isSelected && body != null)
        {
            if (TryGetChestAnchor(out Vector3 chest, out Quaternion bodyRot, out Transform head))
                transform.position = chest + bodyRot * Vector3.forward * (0.18f * HeadScale(head));

            SetBodySolid(true);
            body.detectCollisions = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            GrabPhysics.ActivateAfterDrop(body);
        }
    }

    private void EnsureUnwearAction()
    {
        if (unwearAction != null)
            return;

        unwearAction = new InputAction(
            "UnwearGuitar",
            InputActionType.Button,
            "<XRController>{RightHand}/{secondaryButton}");
    }

    private bool WasUnwearPressed()
    {
        return unwearAction != null && unwearAction.WasPressedThisFrame();
    }

    private void DropFromHands()
    {
        if (grab == null || grab.interactionManager == null)
            return;

        XRInteractionManager manager = grab.interactionManager;
        while (grab.interactorsSelecting.Count > 0)
            manager.SelectExit(grab.interactorsSelecting[0], grab);
    }

    private bool CanWearNow()
    {
        if (Time.time < wearBlockedUntil)
            return false;

        if (!mustLeaveChest)
            return true;

        if (DistanceToChest() > Scaled(wearResumeDistance))
            mustLeaveChest = false;

        return !mustLeaveChest;
    }

    private float DistanceToChest()
    {
        if (!TryGetChestAnchor(out Vector3 chest, out _, out _))
            return float.MaxValue;

        return Vector3.Distance(GetBodyWorldCenter(), chest);
    }

    private bool IsNearChest()
    {
        if (!TryGetChestAnchor(out Vector3 chest, out Quaternion bodyRot, out Transform head))
            return false;

        float scale = HeadScale(head);
        Vector3 guitarBody = GetBodyWorldCenter();
        if (Vector3.Distance(guitarBody, chest) > wearDistance * scale)
            return false;

        Vector3 toGuitar = guitarBody - head.position;
        Vector3 forward = bodyRot * Vector3.forward;
        if (Vector3.Dot(toGuitar, forward) < 0.04f * scale)
            return false;

        return guitarBody.y < head.position.y - 0.22f * scale
            && guitarBody.y > head.position.y - 0.70f * scale;
    }

    private void ApplyWornPose()
    {
        if (!TryGetChestAnchor(out Vector3 chest, out Quaternion bodyRot, out _))
            return;

        Quaternion wearRot = bodyRot * Quaternion.Euler(wearEuler);
        Vector3 localBody = BodyLocalCenter();
        Vector3 targetPos = chest - wearRot * localBody;
        Quaternion targetRot = wearRot;

        if (neckHeld && leftHand != null)
            ApplyOffhandGrip(chest, wearRot, localBody, ref targetPos, ref targetRot);

        if (justWorn)
        {
            transform.SetPositionAndRotation(targetPos, targetRot);
            justWorn = false;
            return;
        }

        float t = 1f - Mathf.Exp(-wearSnapSharpness * Time.deltaTime);
        transform.SetPositionAndRotation(
            Vector3.Lerp(transform.position, targetPos, t),
            Quaternion.Slerp(transform.rotation, targetRot, t));
    }

    private void ApplyOffhandGrip(
        Vector3 chest,
        Quaternion wearRot,
        Vector3 localBody,
        ref Vector3 targetPos,
        ref Quaternion targetRot)
    {
        Vector3 gripLocal = NeckGripLocal();
        Vector3 gripAtRest = chest - wearRot * localBody + wearRot * gripLocal;
        Vector3 desired = Vector3.ClampMagnitude(leftPos - gripAtRest, neckFollowMax);
        Vector3 from = gripAtRest - chest;
        Vector3 to = gripAtRest + desired - chest;
        if (from.sqrMagnitude < 0.0001f || to.sqrMagnitude < 0.0001f)
            return;

        Quaternion extra = Quaternion.FromToRotation(from.normalized, to.normalized);
        extra = Quaternion.RotateTowards(Quaternion.identity, extra, neckTiltMax);
        targetRot = extra * wearRot;
        targetPos = chest - targetRot * localBody;
    }

    private void UpdateNeckHold()
    {
        if (leftHand == null || !leftGrip)
        {
            neckHeld = false;
            return;
        }

        if (neckHeld)
            return;

        neckHeld = DistanceToCollider(neckZone, leftPos) <= neckHoldRadius;
    }

    private void RefreshHands()
    {
        if (Time.unscaledTime >= nextHandRefresh)
        {
            nextHandRefresh = Time.unscaledTime + 0.4f;
            FindHands();
        }

        SampleHand(leftHand, ref leftPos, ref leftVel, ref leftTrigger, ref leftGrip, ref lastLeftPos, ref haveLastLeft);
        SampleHand(rightHand, ref rightPos, ref rightVel, ref rightTrigger, ref rightGrip, ref lastRightPos, ref haveLastRight);
    }

    private void FindHands()
    {
        leftHand = null;
        rightHand = null;
        XRBaseInputInteractor[] found = FindObjectsByType<XRBaseInputInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Transform head = MouthConsumeUtility.FindHead();

        for (int i = 0; i < found.Length; i++)
        {
            XRBaseInputInteractor interactor = found[i];
            if (interactor == null || !interactor.isActiveAndEnabled)
                continue;

            string n = interactor.gameObject.name;
            if (n.Contains("Teleport") || n.Contains("Gaze") || n.Contains("Locomotion"))
                continue;

            InteractorHandedness handedness = interactor.handedness;
            if (handedness == InteractorHandedness.None && head != null)
            {
                float side = Vector3.Dot(interactor.transform.position - head.position, head.right);
                handedness = side < 0f ? InteractorHandedness.Left : InteractorHandedness.Right;
            }

            if (handedness == InteractorHandedness.Left && leftHand == null)
                leftHand = interactor;
            else if (handedness == InteractorHandedness.Right && rightHand == null)
                rightHand = interactor;
        }
    }

    private static void SampleHand(
        XRBaseInputInteractor hand,
        ref Vector3 pos,
        ref Vector3 vel,
        ref float trigger,
        ref bool grip,
        ref Vector3 lastPos,
        ref bool haveLast)
    {
        if (hand == null)
        {
            vel = Vector3.zero;
            trigger = 0f;
            grip = false;
            haveLast = false;
            return;
        }

        pos = hand.transform.position;
        trigger = hand.activateInput != null ? hand.activateInput.ReadValue() : 0f;
        grip = hand.selectInput != null && hand.selectInput.ReadIsPerformed();

        if (haveLast && Time.deltaTime > 0.0001f)
            vel = (pos - lastPos) / Time.deltaTime;
        else
            vel = Vector3.zero;

        lastPos = pos;
        haveLast = true;
    }

    private bool TryGetChestAnchor(out Vector3 chest, out Quaternion bodyRot, out Transform head)
    {
        head = MouthConsumeUtility.FindHead();
        if (head == null)
        {
            chest = default;
            bodyRot = Quaternion.identity;
            return false;
        }

        Vector3 forward = head.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        float yaw = Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y;
        if (!haveSmoothedYaw)
        {
            smoothedYaw = yaw;
            haveSmoothedYaw = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-bodyYawSharpness * Time.deltaTime);
            smoothedYaw = Mathf.LerpAngle(smoothedYaw, yaw, t);
        }

        bodyRot = Quaternion.Euler(0f, smoothedYaw, 0f);
        float scale = HeadScale(head);
        chest = head.position
            + Vector3.down * (chestDrop * scale)
            + bodyRot * Vector3.forward * (chestForward * scale);
        return true;
    }

    private float Scaled(float meters)
    {
        return meters * HeadScale(MouthConsumeUtility.FindHead());
    }

    private static float HeadScale(Transform head)
    {
        return head != null ? Mathf.Max(0.01f, head.lossyScale.y) : 1f;
    }

    private Vector3 GetBodyWorldCenter()
    {
        return transform.TransformPoint(BodyLocalCenter());
    }

    private Vector3 BodyLocalCenter()
    {
        return bodyCollider != null ? bodyCollider.center : new Vector3(0f, 0.28f, 0.06f);
    }

    private Vector3 NeckGripLocal()
    {
        return neckZone != null ? neckZone.center : new Vector3(0f, 0.70f, 0.05f);
    }

    public bool IsRightHandStrumming()
    {
        if (rightHand == null || strumZone == null)
            return false;

        if (DistanceToCollider(strumZone, rightPos) > strumReach)
            return false;

        Vector3 neckAxis = transform.up;
        Vector3 across = rightVel - neckAxis * Vector3.Dot(rightVel, neckAxis);
        return across.magnitude >= strumSpeed;
    }

    private void TryPulseStrumHaptic()
    {
        if (Time.unscaledTime < nextStrumHapticTime)
            return;

        nextStrumHapticTime = Time.unscaledTime + strumHapticCooldown;

        if (rightHand != null && rightHand.SendHapticImpulse(strumHapticAmplitude, strumHapticDuration))
            return;

        HapticsUtility.SendHapticImpulse(
            strumHapticAmplitude,
            strumHapticDuration,
            HapticsUtility.Controller.Right);
    }

    private static float DistanceToCollider(Collider zone, Vector3 point)
    {
        if (zone == null)
            return float.MaxValue;

        Vector3 closest = zone.ClosestPoint(point);
        return Vector3.Distance(closest, point);
    }

    private void FitPlayZones()
    {
        if (bodyCollider != null)
        {
            bodyCollider.isTrigger = false;
            bodyCollider.center = new Vector3(0f, 0.26f, 0.05f);
            bodyCollider.size = new Vector3(0.34f, 0.50f, 0.10f);
        }

        if (neckZone != null)
        {
            neckZone.isTrigger = true;
            neckZone.center = new Vector3(0f, 0.70f, 0.05f);
            neckZone.size = new Vector3(0.055f, 0.10f, 0.055f);
        }

        if (strumZone != null)
        {
            strumZone.isTrigger = true;
            strumZone.center = new Vector3(0f, 0.34f, 0.14f);
            strumZone.size = new Vector3(0.18f, 0.10f, 0.08f);
        }
    }

    private void SetBodySolid(bool solid)
    {
        if (bodyCollider != null)
            bodyCollider.enabled = solid;

        if (neckZone != null)
            neckZone.enabled = true;

        if (strumZone != null)
            strumZone.enabled = true;
    }

    private void StripStrap()
    {
        Transform strap = transform.Find("Strap");
        if (strap != null)
            Destroy(strap.gameObject);
    }

    private void AssignGrabColliders()
    {
        if (grab == null)
            return;

        grab.colliders.Clear();
        if (bodyCollider != null)
            grab.colliders.Add(bodyCollider);
        if (neckZone != null)
            grab.colliders.Add(neckZone);
    }

    private void ResolveCollider(ref BoxCollider collider, string childName)
    {
        if (collider != null)
            return;

        Transform child = transform.Find(childName);
        if (child != null)
            collider = child.GetComponent<BoxCollider>();
    }

    private void ClearStaticFlags()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
            transforms[i].gameObject.isStatic = false;
    }
}
