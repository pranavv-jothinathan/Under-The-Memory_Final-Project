using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// One cake slice. Grab it off the plate, bring it to the mouth to eat.
/// Plays chew audio and a crumb burst, then hides the slice.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EatableSlice : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip chewClip;
    [SerializeField] private ParticleSystem crumbs;
    [SerializeField] private float mouthDistance = 0.22f;
    [SerializeField] private bool enableEditorMouseGrab = true;

    private XRGrabInteractable grab;
    private Rigidbody body;
    private bool eaten;
    private bool editorDragging;

    public bool IsHeldByPlayer => grab != null && grab.isSelected;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        GrabPhysics.SleepUntilGrabbed(body);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }
    }

    private void OnDestroy()
    {
        if (grab == null)
            return;

        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    private void Update()
    {
        UpdateEditorMouseGrab();

        if (eaten)
            return;

        if (!IsHeldByPlayer && !editorDragging)
            return;

        if (!MouthConsumeUtility.IsNearMouth(transform.position, mouthDistance))
            return;

        Eat();
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        DetachFromPlate();
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        GrabPhysics.ActivateAfterDrop(body);
    }

    private void DetachFromPlate()
    {
        if (transform.parent == null)
            return;

        transform.SetParent(null, true);
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void Eat()
    {
        eaten = true;
        editorDragging = false;

        if (chewClip != null)
        {
            GameObject audioGo = new GameObject("ChewAudio");
            audioGo.transform.position = transform.position;
            AudioSource src = audioGo.AddComponent<AudioSource>();
            src.clip = chewClip;
            src.spatialBlend = 1f;
            src.playOnAwake = false;
            src.Play();
            Destroy(audioGo, Mathf.Min(chewClip.length, 1.35f));
        }

        if (crumbs != null)
        {
            crumbs.transform.SetParent(null, true);
            crumbs.Play(true);
            Destroy(crumbs.gameObject, 2f);
        }

        foreach (Collider collider in GetComponentsInChildren<Collider>())
            collider.enabled = false;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (crumbs != null && renderer.transform.IsChildOf(crumbs.transform))
                continue;
            renderer.enabled = false;
        }

        if (grab != null)
            grab.enabled = false;

        body.isKinematic = true;
        Destroy(gameObject, 0.05f);
    }

    private void UpdateEditorMouseGrab()
    {
        if (!enableEditorMouseGrab || !Application.isEditor || eaten)
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
                GrabPhysics.ActivateAfterDrop(body);
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
        DetachFromPlate();
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
