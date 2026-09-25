using UnityEngine;

/// <summary>
/// Props stay frozen until the player grabs and releases them once.
/// </summary>
public static class GrabPhysics
{
    public static void SleepUntilGrabbed(Rigidbody body)
    {
        if (body == null)
            return;

        body.isKinematic = true;
        body.useGravity = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    public static void ActivateAfterDrop(Rigidbody body)
    {
        if (body == null)
            return;

        body.isKinematic = false;
        body.useGravity = true;
    }
}
