using UnityEngine;

public class LensMenuController : MonoBehaviour
{
    /// <summary>滤镜仓成功打开一次。剧情提示靠它知道玩家按了 Y。</summary>
    public event System.Action MenuOpened;

    [Header("Dock Root")]
    [Tooltip("只移动这个物体，不要移动整个 FilterSystem")]
    public Transform dockRoot;

    [Header("Dock")]
    public GameObject dockBackground;

    [Header("Lenses")]
    public LensItem surfaceLens;
    public LensItem underwaterLens;

    [Header("Spawn Points")]
    public Transform surfaceSpawnPoint;
    public Transform underwaterSpawnPoint;

    [Header("Placement")]
    public Transform head;

    [Tooltip("正常情况下滤镜仓距离玩家眼睛多远")]
    public float distanceFromHead = 0.4f;

    [Tooltip("滤镜仓相对玩家眼睛的上下偏移")]
    public float verticalOffset = -0.05f;

    [Header("Placement Collision")]
    [Tooltip("用于检测墙体、建筑等，建议只勾 SurfaceWorld 和 UnderwaterWorld")]
    public LayerMask environmentMask;

    [Tooltip("检测菜单整体占用空间的大概半径")]
    public float placementCheckRadius = 0.15f;

    [Tooltip("菜单与墙体之间额外保留的距离")]
    public float wallPadding = 0.18f;

    [Tooltip("菜单至少离玩家多远")]
    public float minimumDistanceFromHead = 0.25f;

    private bool menuOpen = false;
    private LensItem activeLens;

    private void Start()
    {
        if (surfaceLens != null)
        {
            surfaceLens.homePoint = surfaceSpawnPoint;
            surfaceLens.menuController = this;
        }

        if (underwaterLens != null)
        {
            underwaterLens.homePoint = underwaterSpawnPoint;
            underwaterLens.menuController = this;
        }

        CloseMenu();
    }

    public void ToggleMenu()
    {
        if (menuOpen)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }

    public void OpenMenu()
    {
        if (dockRoot == null || head == null)
        {
            Debug.LogWarning(
                "LensMenuController: Dock Root 或 Head 没有设置。"
            );

            return;
        }

        PositionDock();

        if (dockBackground != null)
        {
            dockBackground.SetActive(true);
        }

        if (surfaceLens != null)
        {
            surfaceLens.gameObject.SetActive(true);
            surfaceLens.ResetToHome();
        }

        if (underwaterLens != null)
        {
            underwaterLens.gameObject.SetActive(true);
            underwaterLens.ResetToHome();
        }

        activeLens = null;
        menuOpen = true;

        MenuOpened?.Invoke();
    }

    public void CloseMenu()
    {
        if (dockBackground != null)
        {
            dockBackground.SetActive(false);
        }

        // 如果当前没有玩家手里正在拿着的透镜，
        // 关闭菜单时就隐藏两个透镜。
        if (activeLens == null)
        {
            if (surfaceLens != null)
            {
                surfaceLens.gameObject.SetActive(false);
            }

            if (underwaterLens != null)
            {
                underwaterLens.gameObject.SetActive(false);
            }
        }

        menuOpen = false;
    }

    private void PositionDock()
    {
        Vector3 forward = head.forward.normalized;

        float desiredDistance = distanceFromHead;

        RaycastHit hit;

        // 检查玩家正前方有没有墙、建筑或其他环境物体。
        if (Physics.SphereCast(
            head.position,
            placementCheckRadius,
            forward,
            out hit,
            distanceFromHead,
            environmentMask,
            QueryTriggerInteraction.Ignore))
        {
            desiredDistance =
                Mathf.Max(
                    minimumDistanceFromHead,
                    hit.distance - wallPadding
                );
        }

        Vector3 targetPosition =
            head.position
            + forward * desiredDistance
            + Vector3.up * verticalOffset;

        dockRoot.position = targetPosition;

        // 让 Dock 正面始终朝向玩家。
        Vector3 lookDirection =
            dockRoot.position - head.position;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            dockRoot.rotation =
                Quaternion.LookRotation(
                    lookDirection.normalized,
                    Vector3.up
                );
        }
    }

    public void OnLensGrabbed(LensItem lens)
    {
        if (lens == null)
            return;

        activeLens = lens;

        // 抓起任意一个 Lens 后，背景立即消失。
        if (dockBackground != null)
        {
            dockBackground.SetActive(false);
        }

        // 隐藏另一个 Lens。
        if (lens == surfaceLens)
        {
            if (underwaterLens != null)
            {
                underwaterLens.gameObject.SetActive(false);
            }
        }
        else if (lens == underwaterLens)
        {
            if (surfaceLens != null)
            {
                surfaceLens.gameObject.SetActive(false);
            }
        }

        menuOpen = false;
    }

    public void ReturnLens(LensItem lens)
    {
        if (lens == null)
            return;

        // 只有当前玩家拿着的那个透镜松手时才处理。
        if (activeLens != lens)
            return;

        activeLens = null;

        // 松手后透镜直接消失。
        // 下次按 Y 时会重新生成回 SpawnPoint。
        lens.gameObject.SetActive(false);
    }

    public void ConsumeLens(LensItem lens)
    {
        // 透镜已经被放到眼睛附近并完成世界切换后，
        // 清掉整个滤镜菜单状态。

        activeLens = null;

        if (surfaceLens != null)
        {
            surfaceLens.gameObject.SetActive(false);
        }

        if (underwaterLens != null)
        {
            underwaterLens.gameObject.SetActive(false);
        }

        if (dockBackground != null)
        {
            dockBackground.SetActive(false);
        }

        menuOpen = false;
    }
}