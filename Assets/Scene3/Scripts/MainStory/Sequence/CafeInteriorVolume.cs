using UnityEngine;

/// <summary>
/// 用 XR 相机是否落在盒里判断玩家进没进咖啡厅内景。
/// 第一次进来时发一次事件，第一次进来再出去时也发一次。
/// </summary>
public class CafeInteriorVolume : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector3 boxCenter;
    [SerializeField] private Vector3 boxSize = Vector3.one;

    /// <summary>玩家第一次走进内景。强制结束的计时从这里起算。</summary>
    public event System.Action FirstEntered;

    public event System.Action FirstLeftAfterEnter;

    public bool HasEntered { get; private set; }
    public bool HasLeftAfterEnter { get; private set; }
    public bool IsInside { get; private set; }

    public void Configure(Vector3 worldCenter, Vector3 worldSize)
    {
        boxCenter = worldCenter;
        boxSize = worldSize;
        transform.position = worldCenter;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        if (HasLeftAfterEnter)
            return;

        Camera cam = ResolveCamera();
        if (cam == null)
            return;

        bool inside = Contains(cam.transform.position);
        if (inside && !IsInside)
        {
            if (!HasEntered)
            {
                HasEntered = true;
                FirstEntered?.Invoke();
            }
        }
        else if (!inside && IsInside && HasEntered && !HasLeftAfterEnter)
        {
            HasLeftAfterEnter = true;
            FirstLeftAfterEnter?.Invoke();
        }

        IsInside = inside;
    }

    public void ResetVisit()
    {
        HasEntered = false;
        HasLeftAfterEnter = false;
        IsInside = false;
    }

    private bool Contains(Vector3 worldPoint)
    {
        Vector3 half = boxSize * 0.5f;
        Vector3 local = worldPoint - boxCenter;
        return Mathf.Abs(local.x) <= half.x
            && Mathf.Abs(local.y) <= half.y
            && Mathf.Abs(local.z) <= half.z;
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
            return targetCamera;

        // 不能用 Camera.main：它可能是手持扫描仪上的相机，
        // 放下扫描仪就会被误判成玩家离开了咖啡厅，触发强制下水。
        targetCamera = PlayerHead.Camera;
        return targetCamera;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.25f);
        Gizmos.DrawCube(boxCenter, boxSize);
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.8f);
        Gizmos.DrawWireCube(boxCenter, boxSize);
    }
#endif
}
