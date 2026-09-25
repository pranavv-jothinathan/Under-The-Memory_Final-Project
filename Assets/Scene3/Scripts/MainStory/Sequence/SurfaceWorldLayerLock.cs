using UnityEngine;

/// <summary>
/// 挂在附加场景根上：自己和之后拖进来的子物体都锁到 SurfaceWorld，下水后相机不会再画出来。
/// </summary>
[DisallowMultipleComponent]
public class SurfaceWorldLayerLock : MonoBehaviour
{
    [SerializeField] private string surfaceLayerName = "SurfaceWorld";

    private int surfaceLayer = -1;

    private void OnEnable()
    {
        Apply();
    }

    private void OnTransformChildrenChanged()
    {
        Apply();
    }

    public void Apply()
    {
        if (surfaceLayer < 0)
            surfaceLayer = LayerMask.NameToLayer(surfaceLayerName);

        if (surfaceLayer < 0)
            return;

        SetLayerRecursive(transform, surfaceLayer);
    }

    private static void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursive(root.GetChild(i), layer);
    }
}
