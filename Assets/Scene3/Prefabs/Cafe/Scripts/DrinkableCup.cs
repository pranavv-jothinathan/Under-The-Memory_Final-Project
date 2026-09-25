using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Grab a cup, tilt it near the head to sip. Three sips empty the fitted liquid mesh.
/// Stays kinematic until grabbed so Scene dragging is not fighting gravity.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DrinkableCup : MonoBehaviour
{
    [SerializeField] private CupLiquidFill liquid;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip drinkClip;
    [SerializeField] private int sipCount = 3;
    [SerializeField] private float mouthDistance = 0.25f;
    [SerializeField] private float drinkTiltDot = 0.72f;
    [SerializeField] private float sipCooldown = 0.85f;
    [SerializeField] private float rimHeight = 0.07f;
    [SerializeField] private bool enableEditorMouseGrab = true;

    private XRGrabInteractable grab;
    private Rigidbody body;
    private int remainingSips;
    private float nextSipTime;
    private bool editorDragging;

    public bool IsHeldByPlayer => grab != null && grab.isSelected;
    public int RemainingSips => remainingSips;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        if (liquid == null)
            liquid = GetComponentInChildren<CupLiquidFill>(true);
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        ClearStaticFlags();
        GrabPhysics.SleepUntilGrabbed(body);

        remainingSips = Mathf.Max(1, sipCount);
        ApplyFillImmediate();
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
        UpdateEditorMouseGrab();

        if (!CanSip())
            return;

        if (Time.time < nextSipTime)
            return;

        Vector3 rim = transform.position + transform.up * rimHeight;
        if (!MouthConsumeUtility.IsNearMouth(rim, mouthDistance))
            return;

        if (!editorDragging && !MouthConsumeUtility.IsTiltedToDrink(transform, drinkTiltDot))
            return;

        Sip();
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        editorDragging = false;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        StartCoroutine(EnableGravityAfterDetach());
    }

    private IEnumerator EnableGravityAfterDetach()
    {
        yield return null;
        GrabPhysics.ActivateAfterDrop(body);
    }

    private void ClearStaticFlags()
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
            transforms[i].gameObject.isStatic = false;
    }

    private bool CanSip()
    {
        if (remainingSips <= 0)
            return false;

        return IsHeldByPlayer || editorDragging;
    }

    private void Sip()
    {
        remainingSips--;
        nextSipTime = Time.time + sipCooldown;
        ApplyFill();

        if (audioSource != null && drinkClip != null)
        {
            audioSource.Stop();
            audioSource.PlayOneShot(drinkClip);
        }
    }

    private void ApplyFill()
    {
        if (liquid == null)
            return;

        liquid.FillAmount = remainingSips / (float)Mathf.Max(1, sipCount);
    }

    private void ApplyFillImmediate()
    {
        ApplyFill();
        if (liquid != null)
            liquid.Rebuild(true);
    }

    private void UpdateEditorMouseGrab()
    {
        if (!enableEditorMouseGrab || !Application.isEditor)
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
                StartCoroutine(EnableGravityAfterDetach());
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

        editorDragging = true;
        body.isKinematic = true;
        FollowMouse(cam);
    }

    private void FollowMouse(Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(cam.transform.forward, transform.position);
        if (!plane.Raycast(ray, out float enter))
            enter = 1.2f;

        transform.position = ray.GetPoint(Mathf.Clamp(enter, 0.2f, 8f));
    }
}
