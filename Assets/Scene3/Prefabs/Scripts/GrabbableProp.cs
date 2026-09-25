using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Simple grab: frozen until first grab, then gravity after release.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GrabbableProp : MonoBehaviour
{
    private XRGrabInteractable grab;
    private Rigidbody body;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        GrabPhysics.SleepUntilGrabbed(body);
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

    private void OnGrabbed(SelectEnterEventArgs args)
    {
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
}
