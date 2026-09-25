using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Three-panel letter-fold pamphlet. Hold it and pull the cover with the other hand.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class FoldingPamphlet : MonoBehaviour
{
    public const float PanelWidth = 0.072f;
    public const float PanelHeight = 0.118f;
    public const float Thickness = 0.0011f;

    [SerializeField] private Transform leftHinge;
    [SerializeField] private Transform rightHinge;
    [SerializeField] private Transform leftPanel;
    [SerializeField] private Transform rightPanel;
    [SerializeField] private BoxCollider bodyCollider;
    [SerializeField] private bool sleepUntilGrabbed = true;
    [SerializeField] private float foldSharpness = 14f;
    [SerializeField] private float pullRadius = 0.13f;

    private XRGrabInteractable grab;
    private Rigidbody body;
    private SoftPaper[] papers;
    private IXRSelectInteractor heldBy;
    private XRBaseInputInteractor leftHand;
    private XRBaseInputInteractor rightHand;
    private float nextHandRefresh;

    private float leftFold;
    private float rightFold;
    private bool pullingLeft;
    private bool pullingRight;
    private int editorPose;

    public bool IsHeldByPlayer => grab != null && grab.isSelected;

    public void ReleaseInAir(Vector3 velocity, Vector3 angularVelocity)
    {
        sleepUntilGrabbed = false;
        if (body == null)
            body = GetComponent<Rigidbody>();

        GrabPhysics.ActivateAfterDrop(body);
        body.linearVelocity = velocity;
        body.angularVelocity = angularVelocity;
        SetFoldImmediate(0f, 0f);
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        papers = GetComponentsInChildren<SoftPaper>(true);
        ResolveRefs();

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

        SetFoldImmediate(0f, 0f);
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

    private void Update()
    {
        HandleEditorToggle();
        RefreshHands();
        UpdateFoldTargets();
        ApplyFold();
        UpdateCollider();
        UpdatePaperSag();
    }

    public void ReleaseAfterGrab()
    {
        GrabPhysics.ActivateAfterDrop(body);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        heldBy = args.interactorObject;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        heldBy = null;
        pullingLeft = false;
        pullingRight = false;
        GrabPhysics.ActivateAfterDrop(body);
    }

    private void ResolveRefs()
    {
        if (leftHinge == null)
            leftHinge = transform.Find("LeftHinge");
        if (rightHinge == null)
            rightHinge = transform.Find("RightHinge");
        if (leftPanel == null && leftHinge != null)
            leftPanel = leftHinge.Find("Left");
        if (rightPanel == null && rightHinge != null)
            rightPanel = rightHinge.Find("Right");
        if (bodyCollider == null)
            bodyCollider = GetComponent<BoxCollider>();
    }

    private void HandleEditorToggle()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.oKey.wasPressedThisFrame)
            return;

        editorPose = (editorPose + 1) % 3;
        pullingLeft = false;
        pullingRight = false;
        if (editorPose == 0)
            SetFoldImmediate(0f, 0f);
        else if (editorPose == 1)
            SetFoldImmediate(1f, 0f);
        else
            SetFoldImmediate(1f, 1f);
    }

    private void UpdateFoldTargets()
    {
        if (!IsHeldByPlayer)
        {
            pullingLeft = false;
            pullingRight = false;
            SnapFolds();
            return;
        }

        XRBaseInputInteractor offHand = OffHand();
        bool offGrip = offHand != null && offHand.selectInput != null && offHand.selectInput.ReadIsPerformed();
        Vector3 offPos = offHand != null ? offHand.transform.position : Vector3.zero;

        if (!offGrip)
        {
            pullingLeft = false;
            pullingRight = false;
            SnapFolds();
            return;
        }

        float leftDist = leftPanel != null ? Vector3.Distance(offPos, leftPanel.position) : 999f;
        float rightDist = rightPanel != null ? Vector3.Distance(offPos, rightPanel.position) : 999f;

        if (!pullingLeft && !pullingRight)
        {
            if (leftDist < pullRadius)
                pullingLeft = true;
            else if (leftFold > 0.55f && rightDist < pullRadius)
                pullingRight = true;
        }

        Vector3 local = transform.InverseTransformPoint(offPos);

        if (pullingLeft)
        {
            float closedX = 0f;
            float openX = -PanelWidth * 1.05f;
            leftFold = Mathf.Clamp01(Mathf.InverseLerp(closedX, openX, local.x));
            if (leftFold < 0.45f)
                rightFold = Mathf.MoveTowards(rightFold, 0f, Time.deltaTime * 3f);
        }
        else if (pullingRight)
        {
            float closedX = 0f;
            float openX = PanelWidth * 1.05f;
            rightFold = Mathf.Clamp01(Mathf.InverseLerp(closedX, openX, local.x));
        }
        else
        {
            SnapFolds();
        }
    }

    private void SnapFolds()
    {
        float leftTarget = leftFold >= 0.5f ? 1f : 0f;
        float rightTarget = rightFold >= 0.5f ? 1f : 0f;
        if (leftTarget < 0.5f)
            rightTarget = 0f;

        float speed = Mathf.Max(0.5f, foldSharpness * 0.18f);
        leftFold = Mathf.MoveTowards(leftFold, leftTarget, Time.deltaTime * speed);
        rightFold = Mathf.MoveTowards(rightFold, rightTarget, Time.deltaTime * speed);
    }

    private void ApplyFold()
    {
        float leftAngle = Mathf.Lerp(178f, 0f, Smooth(leftFold));
        float rightAngle = Mathf.Lerp(-178f, 0f, Smooth(rightFold));

        if (leftHinge != null)
            leftHinge.localRotation = Quaternion.Euler(0f, leftAngle, 0f);
        if (rightHinge != null)
            rightHinge.localRotation = Quaternion.Euler(0f, rightAngle, 0f);
    }

    private void SetFoldImmediate(float left, float right)
    {
        leftFold = left;
        rightFold = right;
        ApplyFold();
        UpdateCollider();
    }

    private void UpdateCollider()
    {
        if (bodyCollider == null)
            return;

        float open = Mathf.Max(leftFold, rightFold);
        float width = Mathf.Lerp(PanelWidth + 0.01f, PanelWidth * 3.05f, Smooth(open));
        float depth = Mathf.Lerp(0.008f, 0.004f, Smooth(open));
        bodyCollider.center = Vector3.zero;
        bodyCollider.size = new Vector3(width, PanelHeight + 0.01f, depth);
    }

    private void UpdatePaperSag()
    {
        Vector3 support = Vector3.zero;
        float flutter = IsHeldByPlayer ? 0.12f : (body != null && body.linearVelocity.sqrMagnitude < 0.04f ? 0.18f : 1f);
        if (IsHeldByPlayer && heldBy != null)
        {
            support = transform.InverseTransformPoint(heldBy.transform.position);
            support.z = 0f;
        }

        if (papers == null)
            return;

        for (int i = 0; i < papers.Length; i++)
        {
            if (papers[i] == null)
                continue;
            papers[i].SetSupportLocal(papers[i].transform.InverseTransformPoint(transform.TransformPoint(support)));
            papers[i].SetFlutterScale(flutter);
        }
    }

    private XRBaseInputInteractor OffHand()
    {
        if (heldBy is XRBaseInputInteractor held)
        {
            if (leftHand != null && leftHand != held)
                return leftHand;
            if (rightHand != null && rightHand != held)
                return rightHand;
        }

        return null;
    }

    private void RefreshHands()
    {
        if (Time.unscaledTime < nextHandRefresh)
            return;

        nextHandRefresh = Time.unscaledTime + 0.4f;
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

    private static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
