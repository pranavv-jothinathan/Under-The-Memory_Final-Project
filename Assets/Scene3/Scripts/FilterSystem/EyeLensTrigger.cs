using UnityEngine;

public class EyeLensTrigger : MonoBehaviour
{
    public WorldSwitchManager worldSwitchManager;
    public LensMenuController lensMenuController;

    private bool locked = false;

    private void OnTriggerEnter(Collider other)
    {
        if (locked)
            return;

        LensItem lens =
            other.GetComponentInParent<LensItem>();

        if (lens == null)
            return;

        locked = true;

        worldSwitchManager.RequestSwitch(
            lens.worldType
        );

        lensMenuController.ConsumeLens(
            lens
        );

        Invoke(
            nameof(Unlock),
            0.8f
        );
    }

    private void Unlock()
    {
        locked = false;
    }
}