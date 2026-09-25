using UnityEngine;

public class DebugDisableLogger : MonoBehaviour
{
    void OnDisable()
    {
        Debug.LogError("XROrigin closed", this);
        Debug.LogError(StackTraceUtility.ExtractStackTrace());
    }
}
