using UnityEngine;

/// <summary>
/// 玩家走近这个物体时弹一条提示。
///
/// 刻意用距离判定而不是触发盒：挂点 ScannerBox_Box 的 scale 是非等比的
/// （34.8 / 26.8 / 15.7），子物体上的球或盒触发器会被拉歪；而且它在
/// UnderwaterWorld 层，WorldSwitchManager.SetColliders() 切世界时会把整棵
/// 水下子树的 collider 关掉。距离判定这两个坑都不沾。
/// </summary>
public class StoryTipProximity : MonoBehaviour
{
    [SerializeField] private StoryTipId tip = StoryTipId.ArchiveLensDetected;

    [Tooltip("玩家头部进入这个半径（米）就触发。")]
    [Min(0.1f)]
    [SerializeField] private float radius = 2.5f;

    [Tooltip("留空就用本物体的位置。挂点轴心不在物体中间时用它校正。")]
    [SerializeField] private Transform anchorOverride;

    [Tooltip("勾上表示只触发一次。")]
    [SerializeField] private bool once = true;

    [Tooltip("检测间隔（秒）。不需要每帧算。")]
    [Min(0.02f)]
    [SerializeField] private float checkInterval = 0.2f;

    private bool fired;
    private float nextCheckTime;

    private Vector3 AnchorPosition =>
        anchorOverride != null ? anchorOverride.position : transform.position;

    private void Update()
    {
        if (fired && once)
            return;

        if (Time.time < nextCheckTime)
            return;

        nextCheckTime = Time.time + checkInterval;

        // 不能用 Camera.main：扫描仪上的相机也带 MainCamera tag
        if (!PlayerHead.TryGetPosition(out Vector3 headPosition))
            return;

        if ((headPosition - AnchorPosition).sqrMagnitude > radius * radius)
            return;

        fired = true;
        StoryTipsPresenter.Request(tip);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.85f, 0.95f, 0.85f);
        Gizmos.DrawWireSphere(AnchorPosition, radius);
    }
#endif
}
