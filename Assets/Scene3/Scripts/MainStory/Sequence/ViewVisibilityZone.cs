using UnityEngine;

/// <summary>
/// 一组探针点的可见性判定。
/// 某个探针算“可见”需要同时满足：在相机视锥内，且相机到它的连线没有被环境挡住。
/// 全部探针都不可见时，整个区域才算不可见，并累计持续时间用于防抖。
/// </summary>
public class ViewVisibilityZone : MonoBehaviour
{
    [Header("Probes")]
    [SerializeField] private Transform[] probes;

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Occlusion")]
    [Tooltip("参与遮挡判定的层。默认排除玩家自身与触发器。")]
    [SerializeField] private LayerMask occluderMask = ~0;
    [Tooltip("从探针往相机方向缩进的距离，避免打到探针紧贴的家具。")]
    [SerializeField] private float occlusionSkin = 0.15f;

    [Header("Frustum")]
    [Tooltip("视口内缩边距。0 表示整屏都算可见。")]
    [SerializeField] private float viewportMargin = 0.02f;

    public bool IsVisible { get; private set; }
    public float InvisibleDuration { get; private set; }
    public int ProbeCount => probes != null ? probes.Length : 0;

    public bool HasBeenInvisibleFor(float seconds)
    {
        return !IsVisible && InvisibleDuration >= seconds;
    }

    public void ResetTimer()
    {
        InvisibleDuration = 0f;
    }

    private void Update()
    {
        Camera cam = ResolveCamera();
        if (cam == null || probes == null || probes.Length == 0)
        {
            IsVisible = true;
            InvisibleDuration = 0f;
            return;
        }

        bool anyVisible = false;
        for (int i = 0; i < probes.Length; i++)
        {
            if (IsProbeVisible(cam, probes[i]))
            {
                anyVisible = true;
                break;
            }
        }

        IsVisible = anyVisible;
        if (anyVisible)
            InvisibleDuration = 0f;
        else
            InvisibleDuration += Time.deltaTime;
    }

    private bool IsProbeVisible(Camera cam, Transform probe)
    {
        if (probe == null || !probe.gameObject.activeInHierarchy)
            return false;

        Vector3 point = probe.position;
        Vector3 viewport = cam.WorldToViewportPoint(point);
        if (viewport.z <= 0f)
            return false;

        if (viewport.x < viewportMargin || viewport.x > 1f - viewportMargin)
            return false;

        if (viewport.y < viewportMargin || viewport.y > 1f - viewportMargin)
            return false;

        Vector3 eye = cam.transform.position;
        Vector3 toProbe = point - eye;
        float distance = toProbe.magnitude - Mathf.Max(0f, occlusionSkin);
        if (distance <= 0.01f)
            return true;

        return !Physics.Raycast(
            eye,
            toProbe.normalized,
            distance,
            occluderMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null && targetCamera.isActiveAndEnabled)
            return targetCamera;

        // 不能用 Camera.main：扫描仪相机会让「人群在不在玩家视野里」判错。
        targetCamera = PlayerHead.Camera;
        return targetCamera;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (probes == null)
            return;

        Gizmos.color = IsVisible ? Color.green : Color.red;
        for (int i = 0; i < probes.Length; i++)
        {
            if (probes[i] != null)
                Gizmos.DrawWireSphere(probes[i].position, 0.18f);
        }
    }
#endif
}
