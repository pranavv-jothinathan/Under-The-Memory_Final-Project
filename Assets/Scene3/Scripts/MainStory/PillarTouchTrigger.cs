using UnityEngine;

/// <summary>
/// 旧的手部触摸触发。场景里 ScanTrigger 上如果还挂着这个，
/// Play 时会自动换成 PillarScanTrigger 并按父节点接到正确柱子。
/// 跑过 Prefab Library / 接上柱子扫描触发 之后就可以删掉本组件。
/// </summary>
public class PillarTouchTrigger : MonoBehaviour
{
    public MemoryPillar pillar;

    [Header("Hand Detection")]
    public LayerMask handLayers;

    private void Awake()
    {
        PillarScanTrigger scan =
            GetComponent<PillarScanTrigger>();

        if (scan == null)
            scan = gameObject.AddComponent<PillarScanTrigger>();

        MemoryPillar parentPillar =
            GetComponentInParent<MemoryPillar>();

        if (scan.pillar == null)
        {
            scan.pillar =
                parentPillar != null
                    ? parentPillar
                    : pillar;
        }

        if (scan.requiredScanTime <= 0f)
            scan.requiredScanTime = 1f;

        Destroy(this);
    }
}
