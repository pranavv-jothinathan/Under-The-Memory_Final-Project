using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class UnlockRotationOnFirstGrab : MonoBehaviour
{
    private Rigidbody bottleRigidbody;
    private XRGrabInteractable grabInteractable;
    private RigidbodyConstraints normalConstraints;
    private bool rotationUnlocked;

    private void Awake()
    {
        bottleRigidbody = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();

        normalConstraints = bottleRigidbody.constraints;

        // Preserve the bottle's starting pose.
        bottleRigidbody.constraints =
            normalConstraints | RigidbodyConstraints.FreezeRotation;
    }

    private void OnEnable()
    {
        grabInteractable.firstSelectEntered.AddListener(UnlockRotation);
    }

    private void OnDisable()
    {
        grabInteractable.firstSelectEntered.RemoveListener(UnlockRotation);
    }

    private void UnlockRotation(SelectEnterEventArgs args)
    {
        if (rotationUnlocked)
            return;

        rotationUnlocked = true;
        bottleRigidbody.constraints = normalConstraints;
        bottleRigidbody.WakeUp();
    }
}