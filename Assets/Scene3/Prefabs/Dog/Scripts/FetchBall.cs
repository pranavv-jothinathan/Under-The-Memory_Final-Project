using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// VR grab / throw ball. While the dog carries it, physics is disabled and
/// the ball is snapped to the mouth every LateUpdate so it cannot be knocked off.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class FetchBall : MonoBehaviour
{
    public static readonly List<FetchBall> ActiveBalls = new List<FetchBall>();

    public static event System.Action<FetchBall> Thrown;
    public static event System.Action<FetchBall> Grabbed;

    [Header("Throw")]
    [Tooltip("Release speed above this counts as a throw the dog will chase.")]
    [SerializeField] private float throwSpeedThreshold = 0.6f;

    [Header("Editor Test")]
    [Tooltip("Play Mode: click-drag the ball in the Game view, or move it in the Scene view.")]
    [SerializeField] private bool enableEditorMouseThrow = true;

    [SerializeField] private float editorThrowForce = 8f;

    private Rigidbody body;
    private XRGrabInteractable grab;
    private Collider ballCollider;
    private Collider[] ballColliders;
    private Transform originalParent;
    private Transform mouthFollow;
    private bool carriedByDog;
    private FetchDog carryingDog;
    private Vector3 lastTrackedPosition;
    private bool movedRecently;

    private bool grabbedByPlayer;
    private bool editorDragging;
    private Vector3 editorLastPosition;
    private Vector3 editorVelocity;

    public bool IsCarriedByDog => carriedByDog;
    public bool IsHeldByPlayer => grab != null && grab.isSelected;
    public bool WasMovedRecently => movedRecently;
    public Rigidbody Body => body;
    public Collider BallCollider => ballCollider;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        ballCollider = GetComponent<Collider>();
        ballColliders = GetComponentsInChildren<Collider>(true);
        grab = GetComponent<XRGrabInteractable>();
        originalParent = transform.parent;
        lastTrackedPosition = transform.position;
        GrabPhysics.SleepUntilGrabbed(body);
    }

    private void OnEnable()
    {
        if (!ActiveBalls.Contains(this))
            ActiveBalls.Add(this);

        lastTrackedPosition = transform.position;

        if (grab == null)
            return;

        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        ActiveBalls.Remove(this);

        if (grab == null)
            return;

        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
    }

    private void Update()
    {
        UpdateMovedFlag();
        UpdateEditorMouseThrow();
    }

    private void LateUpdate()
    {
        if (!carriedByDog || mouthFollow == null)
            return;

        transform.SetPositionAndRotation(
            mouthFollow.position,
            mouthFollow.rotation
        );

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private void UpdateMovedFlag()
    {
        if (carriedByDog || IsHeldByPlayer)
        {
            movedRecently = false;
            lastTrackedPosition = transform.position;
            return;
        }

        Vector3 current = transform.position;
        current.y = 0f;
        Vector3 last = lastTrackedPosition;
        last.y = 0f;

        float moved = Vector3.Distance(current, last);
        movedRecently = moved > 0.08f;
        lastTrackedPosition = transform.position;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (carriedByDog && carryingDog != null)
            carryingDog.NotifyBallStolen(this);

        DetachFromMouthKeepWorldPose();
        SetCarryPhysics(false);
        grabbedByPlayer = true;
        Grabbed?.Invoke(this);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (carriedByDog)
            return;

        GrabPhysics.ActivateAfterDrop(body);
        if (!grabbedByPlayer)
            return;

        grabbedByPlayer = false;
        TryBroadcastThrow(body.linearVelocity);
    }

    public void AttachToMouth(Transform mouth, FetchDog dog)
    {
        carryingDog = dog;
        carriedByDog = true;
        movedRecently = false;
        mouthFollow = mouth;

        SetCarryPhysics(true);

        transform.SetParent(null, true);
        transform.SetPositionAndRotation(mouth.position, mouth.rotation);
    }

    public void DetachFromMouth(Vector3 dropVelocity)
    {
        Vector3 releasePosition = mouthFollow != null
            ? mouthFollow.position
            : transform.position;

        DetachFromMouthKeepWorldPose();
        transform.position = releasePosition + Vector3.up * 0.05f;
        SetCarryPhysics(false);

        body.linearVelocity = dropVelocity;
        body.angularVelocity = Vector3.zero;
        lastTrackedPosition = transform.position;
        movedRecently = false;
        grabbedByPlayer = false;
    }

    public void NotifyThrownManually()
    {
        Thrown?.Invoke(this);
    }

    private void SetCarryPhysics(bool carried)
    {
        SetBallCollidersEnabled(!carried);

        if (grab != null)
            grab.enabled = !carried;

        body.detectCollisions = !carried;
        body.interpolation = carried
            ? RigidbodyInterpolation.None
            : RigidbodyInterpolation.Interpolate;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = carried;
        body.useGravity = !carried;
    }

    private void DetachFromMouthKeepWorldPose()
    {
        if (!carriedByDog)
            return;

        carriedByDog = false;
        carryingDog = null;
        mouthFollow = null;
        transform.SetParent(originalParent, true);
    }

    private void SetBallCollidersEnabled(bool enabled)
    {
        if (ballColliders == null)
            return;

        for (int i = 0; i < ballColliders.Length; i++)
        {
            if (ballColliders[i] != null)
                ballColliders[i].enabled = enabled;
        }
    }

    private void TryBroadcastThrow(Vector3 velocity)
    {
        if (velocity.magnitude < throwSpeedThreshold)
            return;

        Thrown?.Invoke(this);
    }

    private void UpdateEditorMouseThrow()
    {
        if (!enableEditorMouseThrow)
            return;

        if (!Application.isEditor)
            return;

        if (IsHeldByPlayer || carriedByDog)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        if (editorDragging)
        {
            if (Input.GetMouseButton(0))
            {
                FollowMouse(cam);
            }
            else
            {
                editorDragging = false;
                SetCarryPhysics(false);

                Vector3 throwVelocity = editorVelocity * editorThrowForce;
                if (throwVelocity.sqrMagnitude < 0.01f)
                    throwVelocity = cam.transform.forward * editorThrowForce;

                body.linearVelocity = throwVelocity;
                Thrown?.Invoke(this);
            }

            return;
        }

        if (!Input.GetMouseButtonDown(0))
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 80f))
            return;

        if (hit.collider != ballCollider && !hit.collider.transform.IsChildOf(transform))
            return;

        editorDragging = true;
        editorLastPosition = transform.position;
        editorVelocity = Vector3.zero;

        if (carriedByDog && carryingDog != null)
            carryingDog.NotifyBallStolen(this);

        DetachFromMouthKeepWorldPose();
        body.isKinematic = true;
        Grabbed?.Invoke(this);
        FollowMouse(cam);
    }

    private void FollowMouse(Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);
        if (!plane.Raycast(ray, out float enter))
            enter = 3f;

        Vector3 next = ray.GetPoint(Mathf.Clamp(enter, 0.5f, 20f));
        editorVelocity = (next - editorLastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        editorLastPosition = next;
        transform.position = next;
    }
}
