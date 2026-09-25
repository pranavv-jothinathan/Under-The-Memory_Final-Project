using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Pull a dart off the board and release to glide it like a paper plane.
/// Hits on the board stick; hits on the floor kill spin so it cannot gyro.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ThrowableDart : MonoBehaviour
{
    [Header("Tip")]
    [SerializeField] private Vector3 tipLocalOffset = new Vector3(0f, 0f, -0.06f);

    [Header("Flight")]
    [SerializeField] private float alignSharpness = 18f;
    [SerializeField] private float alignSpeed = 0.35f;
    [SerializeField] private float gravityScale = 0.55f;
    [SerializeField] private float glideSpeed = 6f;
    [SerializeField] private float minStickSpeed = 0.12f;
    [SerializeField] private float embedDepth = 0.018f;
    [SerializeField] private float unstickIgnoreTime = 0.05f;
    [SerializeField] private float unstickNudge = 0.03f;
    [SerializeField] private float dropTipDownDot = 0.72f;
    [SerializeField] private float farGrabDistance = 0.45f;

    [Header("Editor Test")]
    [SerializeField] private bool enableEditorMouseThrow = true;

    private static PhysicsMaterial slideMaterial;

    private Rigidbody body;
    private XRGrabInteractable grab;
    private Collider[] dartColliders;
    private DartBoardTarget stuckBoard;
    private bool stuck;
    private bool flying;
    private bool grounded;
    private bool everGrabbed;
    private float ignoreBoardUntil;
    private Vector3 lastTipPosition;

    private IXRSelectInteractor heldInteractor;
    private Vector3 controllerVelocity;
    private Vector3 lastInteractorPosition;
    private bool haveInteractorPosition;
    private Vector3 launchDirection;
    private bool shouldGlide;

    private bool editorDragging;
    private Vector3 editorLastPosition;
    private Vector3 editorVelocity;

    public bool IsStuck => stuck;
    public bool IsHeldByPlayer => grab != null && grab.isSelected;
    public Rigidbody Body => body;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        dartColliders = GetComponentsInChildren<Collider>(true);
        lastTipPosition = TipWorldPosition();

        body.mass = 0.05f;
        body.linearDamping = 0.1f;
        body.angularDamping = 2.5f;
        ApplySlideMaterial();

        if (grab != null)
        {
            grab.retainTransformParent = false;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwVelocityScale = 0f;
            grab.throwAngularVelocityScale = 0f;
            grab.attachEaseInTime = 0f;
        }

        DartBoardTarget parentBoard = GetComponentInParent<DartBoardTarget>();
        if (parentBoard != null)
        {
            StickTo(parentBoard, false);
            return;
        }

        GrabPhysics.SleepUntilGrabbed(body);
    }

    private void OnEnable()
    {
        if (grab == null)
            return;

        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        if (grab == null)
            return;

        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
    }

    private void Update()
    {
        SampleControllerVelocity();
        UpdateEditorMouseThrow();
    }

    private void FixedUpdate()
    {
        Vector3 tip = TipWorldPosition();

        if (flying && !stuck && !IsHeldByPlayer && !editorDragging)
            TryStickBySweep(lastTipPosition, tip);

        lastTipPosition = tip;

        if (grounded && !stuck && !IsHeldByPlayer)
        {
            body.angularVelocity = Vector3.zero;
            return;
        }

        if (!flying || stuck || IsHeldByPlayer || editorDragging || body.isKinematic)
            return;

        if (body.useGravity && gravityScale < 0.999f)
            body.AddForce(Physics.gravity * (gravityScale - 1f), ForceMode.Acceleration);

        Vector3 velocity = body.linearVelocity;
        if (velocity.sqrMagnitude < alignSpeed * alignSpeed)
            return;

        Vector3 direction = velocity.normalized;
        Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.94f
            ? Vector3.right
            : Vector3.up;
        Quaternion goal = Quaternion.LookRotation(-direction, up);
        float t = 1f - Mathf.Exp(-alignSharpness * Time.fixedDeltaTime);
        body.MoveRotation(Quaternion.Slerp(body.rotation, goal, t));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (TryStickFromCollision(collision))
            return;

        SettleOnSurface(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (TryStickFromCollision(collision))
            return;

        if (grounded)
            body.angularVelocity = Vector3.zero;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        Unstick(true);
        everGrabbed = true;
        flying = false;
        grounded = false;
        editorDragging = false;
        shouldGlide = false;
        body.constraints = RigidbodyConstraints.None;
        heldInteractor = args.interactorObject;
        controllerVelocity = Vector3.zero;
        haveInteractorPosition = false;
        if (grab != null)
            grab.retainTransformParent = false;
        transform.SetParent(null, true);
        SetCollidersEnabled(false);
        lastTipPosition = TipWorldPosition();
        SampleControllerVelocity();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        SampleControllerVelocity();
        launchDirection = GetLaunchDirection(args.interactorObject);
        shouldGlide = !IsGentleDrop();
        heldInteractor = null;
        haveInteractorPosition = false;
        transform.SetParent(null, true);
        SetCollidersEnabled(true);
        StartCoroutine(EnableThrowPhysics());
    }

    public void StickTo(DartBoardTarget board, bool snapPose)
    {
        if (board == null)
            return;

        stuckBoard = board;
        stuck = true;
        flying = false;
        grounded = false;
        editorDragging = false;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.None;
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.detectCollisions = true;

        if (snapPose)
            SnapTipIntoBoard(board, TipWorldPosition(), -board.FaceNormal);

        transform.SetParent(board.transform, true);
        SetIgnoreBoard(board, true);
    }

    public void Unstick(bool keepWorldPose)
    {
        DartBoardTarget board = stuckBoard;
        bool wasStuck = stuck;
        stuck = false;
        stuckBoard = null;
        ignoreBoardUntil = Time.time + unstickIgnoreTime;

        transform.SetParent(null, keepWorldPose);

        if (wasStuck && board != null)
            transform.position += board.FaceNormal * unstickNudge;

        if (board != null)
            SetIgnoreBoard(board, true);

        lastTipPosition = TipWorldPosition();
    }

    private IEnumerator EnableThrowPhysics()
    {
        yield return null;
        if (IsHeldByPlayer || stuck)
            yield break;

        transform.SetParent(null, true);
        GrabPhysics.ActivateAfterDrop(body);
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.detectCollisions = true;
        body.constraints = RigidbodyConstraints.None;
        body.angularVelocity = Vector3.zero;

        if (shouldGlide && launchDirection.sqrMagnitude > 0.01f)
        {
            body.linearVelocity = launchDirection.normalized * glideSpeed;
            flying = true;
            grounded = false;
        }
        else
        {
            body.linearVelocity = Vector3.zero;
            flying = false;
        }

        ignoreBoardUntil = Time.time + unstickIgnoreTime;
        lastTipPosition = TipWorldPosition();
        StartCoroutine(ClearBoardIgnore());
    }

    private bool IsGentleDrop()
    {
        bool tipDown = Vector3.Dot(TipWorldDirection(), Vector3.down) > dropTipDownDot;
        bool still = controllerVelocity.magnitude < 0.4f;
        return tipDown && still;
    }

    private Vector3 GetLaunchDirection(IXRInteractor interactor)
    {
        if (IsFarThrow(interactor))
        {
            Transform origin = GetVelocitySource(interactor);
            if (origin != null)
                return origin.forward;
        }

        Vector3 tip = TipWorldDirection();
        if (tip.sqrMagnitude > 0.01f)
            return tip;

        return Vector3.forward;
    }

    private Vector3 TipWorldDirection()
    {
        return -transform.forward;
    }

    private void SampleControllerVelocity()
    {
        if (heldInteractor == null)
            return;

        Transform source = GetVelocitySource(heldInteractor);
        if (source == null)
            return;

        Vector3 position = source.position;
        if (haveInteractorPosition)
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 instant = (position - lastInteractorPosition) / dt;
            float follow = 1f - Mathf.Exp(-dt / 0.08f);
            controllerVelocity = Vector3.Lerp(controllerVelocity, instant, follow);
        }

        lastInteractorPosition = position;
        haveInteractorPosition = true;
    }

    private bool IsFarThrow(IXRInteractor interactor)
    {
        if (interactor == null)
            return false;

        if (interactor is XRDirectInteractor)
            return false;

        if (interactor is XRRayInteractor)
            return true;

        Transform source = GetVelocitySource(interactor);
        if (source == null)
            return interactor is IXRRayProvider;

        return Vector3.Distance(source.position, transform.position) > farGrabDistance;
    }

    private static Transform GetVelocitySource(IXRInteractor interactor)
    {
        if (interactor is IXRRayProvider rayProvider)
        {
            Transform origin = rayProvider.GetOrCreateRayOrigin();
            if (origin != null)
                return origin;
        }

        return interactor.transform;
    }

    private IEnumerator ClearBoardIgnore()
    {
        yield return new WaitForSeconds(unstickIgnoreTime);
        if (stuck || IsHeldByPlayer)
            yield break;

        DartBoardTarget[] boards = FindObjectsByType<DartBoardTarget>(FindObjectsSortMode.None);
        for (int i = 0; i < boards.Length; i++)
            SetIgnoreBoard(boards[i], false);
    }

    private bool TryStickFromCollision(Collision collision)
    {
        if (!CanStick())
            return false;

        DartBoardTarget board = collision.collider.GetComponentInParent<DartBoardTarget>();
        if (board == null)
            return false;

        ContactPoint contact = collision.GetContact(0);
        StickAt(board, contact.point, contact.normal);
        return true;
    }

    private void SettleOnSurface(Collision collision)
    {
        if (stuck || IsHeldByPlayer || editorDragging)
            return;

        if (collision.collider.GetComponentInParent<DartBoardTarget>() != null)
            return;

        flying = false;
        grounded = true;
        body.angularVelocity = Vector3.zero;

        Vector3 normal = collision.GetContact(0).normal;
        if (Vector3.Dot(normal, Vector3.up) > 0.55f)
        {
            body.constraints = RigidbodyConstraints.FreezeRotation;
            Vector3 slide = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            body.linearVelocity = slide * 0.3f;
        }
        else
        {
            body.linearVelocity *= 0.4f;
        }
    }

    private void TryStickBySweep(Vector3 from, Vector3 to)
    {
        if (!CanStick())
            return;

        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.001f)
            return;

        if (!Physics.SphereCast(from, 0.008f, delta / distance, out RaycastHit hit, distance + 0.04f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return;

        if (hit.rigidbody == body || hit.collider.transform.IsChildOf(transform))
            return;

        DartBoardTarget board = hit.collider.GetComponentInParent<DartBoardTarget>();
        if (board == null)
            return;

        StickAt(board, hit.point, hit.normal);
    }

    private bool CanStick()
    {
        if (stuck || IsHeldByPlayer || editorDragging)
            return false;

        if (Time.time < ignoreBoardUntil)
            return false;

        if (!everGrabbed && !flying)
            return false;

        if (body != null && !body.isKinematic && body.linearVelocity.magnitude < minStickSpeed)
            return false;

        return true;
    }

    private void StickAt(DartBoardTarget board, Vector3 point, Vector3 normal)
    {
        grounded = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.None;

        SnapTipIntoBoard(board, point, normal);
        StickTo(board, false);
    }

    private void SnapTipIntoBoard(DartBoardTarget board, Vector3 hitPoint, Vector3 hitNormal)
    {
        Vector3 face = board.FaceNormal;
        if (face.sqrMagnitude < 0.01f)
            face = Vector3.forward;

        Vector3 intoBoard = Vector3.Dot(hitNormal, face) > 0f ? -face.normalized : -hitNormal.normalized;
        Quaternion planted = Quaternion.LookRotation(-intoBoard, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, planted, 0.55f);

        Vector3 targetTip = hitPoint + intoBoard * embedDepth;
        transform.position += targetTip - TipWorldPosition();
    }

    private Vector3 TipWorldPosition()
    {
        return transform.TransformPoint(tipLocalOffset);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (body != null)
            body.detectCollisions = enabled;
    }

    private void ApplySlideMaterial()
    {
        if (slideMaterial == null)
        {
            slideMaterial = new PhysicsMaterial("DartSlide")
            {
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum,
                staticFriction = 0.18f,
                dynamicFriction = 0.12f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
        }

        if (dartColliders == null)
            return;

        for (int i = 0; i < dartColliders.Length; i++)
        {
            if (dartColliders[i] != null)
                dartColliders[i].sharedMaterial = slideMaterial;
        }
    }

    private void SetIgnoreBoard(DartBoardTarget board, bool ignore)
    {
        if (board == null || dartColliders == null)
            return;

        Collider[] boardColliders = board.GetColliders();
        for (int i = 0; i < dartColliders.Length; i++)
        {
            if (dartColliders[i] == null)
                continue;

            for (int j = 0; j < boardColliders.Length; j++)
            {
                if (boardColliders[j] == null)
                    continue;
                Physics.IgnoreCollision(dartColliders[i], boardColliders[j], ignore);
            }
        }
    }

    private void UpdateEditorMouseThrow()
    {
        if (!enableEditorMouseThrow || !Application.isEditor)
            return;

        if (IsHeldByPlayer)
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
                transform.SetParent(null, true);
                SetCollidersEnabled(true);
                GrabPhysics.ActivateAfterDrop(body);
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.constraints = RigidbodyConstraints.None;
                body.angularVelocity = Vector3.zero;

                bool dragged = editorVelocity.magnitude > 0.4f;
                if (dragged)
                {
                    Vector3 tip = TipWorldDirection();
                    launchDirection = Vector3.Dot(tip, cam.transform.forward) > 0.2f
                        ? tip
                        : cam.transform.forward;
                    body.linearVelocity = launchDirection.normalized * glideSpeed;
                    flying = true;
                    grounded = false;
                }
                else
                {
                    body.linearVelocity = Vector3.zero;
                    flying = false;
                }

                everGrabbed = true;
                ignoreBoardUntil = Time.time + unstickIgnoreTime;
                lastTipPosition = TipWorldPosition();
                StartCoroutine(ClearBoardIgnore());
            }

            return;
        }

        if (!Input.GetMouseButtonDown(0))
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 80f))
            return;

        if (!hit.collider.transform.IsChildOf(transform) && hit.collider.gameObject != gameObject)
            return;

        Unstick(true);
        editorDragging = true;
        flying = false;
        grounded = false;
        everGrabbed = true;
        body.constraints = RigidbodyConstraints.None;
        body.isKinematic = true;
        body.useGravity = false;
        editorLastPosition = transform.position;
        editorVelocity = Vector3.zero;
        FollowMouse(cam);
    }

    private void FollowMouse(Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(cam.transform.forward, transform.position);
        if (!plane.Raycast(ray, out float enter))
            enter = 1.4f;

        Vector3 next = ray.GetPoint(Mathf.Clamp(enter, 0.25f, 8f));
        editorVelocity = (next - editorLastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        editorLastPosition = next;
        transform.position = next;
        lastTipPosition = TipWorldPosition();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.TransformPoint(tipLocalOffset), 0.006f);
        Gizmos.DrawLine(
            transform.TransformPoint(tipLocalOffset),
            transform.TransformPoint(tipLocalOffset) - transform.forward * 0.04f);
    }
#endif
}
