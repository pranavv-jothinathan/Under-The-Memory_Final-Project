using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public enum LensWorldType
{
    Surface,
    Underwater
}

public class LensItem : MonoBehaviour
{
    public LensWorldType worldType;

    [HideInInspector]
    public Transform homePoint;

    [HideInInspector]
    public LensMenuController menuController;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    private Rigidbody rb;

    private void Awake()
    {
        grabInteractable =
            GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        rb =
            GetComponent<Rigidbody>();
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

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (menuController != null)
            menuController.OnLensGrabbed(this);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // 如果没有正在切换世界，
        // 松手后自动回到滤镜仓
        if (menuController != null)
            menuController.ReturnLens(this);
    }

    public void ResetToHome()
    {
        if (homePoint == null)
            return;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.SetPositionAndRotation(
            homePoint.position,
            homePoint.rotation
        );

        Physics.SyncTransforms();
    }
}