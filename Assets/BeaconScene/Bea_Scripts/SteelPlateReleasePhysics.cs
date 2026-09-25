using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class SteelPlateReleasePhysics : MonoBehaviour
{
    [SerializeField] private Rigidbody plateRigidbody;
    [SerializeField] private XRGrabInteractable grabInteractable;

    private bool hasBeenReleased;

    private void Reset()
    {
        plateRigidbody = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        grabInteractable.selectExited.AddListener(OnPlateReleased);
    }

    private void OnDisable()
    {
        grabInteractable.selectExited.RemoveListener(OnPlateReleased);
    }

    private void OnPlateReleased(SelectExitEventArgs args)
    {
        if (hasBeenReleased)
            return;

        hasBeenReleased = true;

        plateRigidbody.isKinematic = false;
        plateRigidbody.useGravity = true;
    }
}