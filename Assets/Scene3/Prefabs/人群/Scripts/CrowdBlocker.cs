using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class CrowdBlocker : MonoBehaviour
{
    private void Reset()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = false;
        box.size = new Vector3(6f, 2.5f, 0.6f);
        box.center = new Vector3(0f, 1.25f, 0f);
    }
}
