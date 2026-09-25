using UnityEngine;

public class MainStoryManager : MonoBehaviour
{
    /// <summary>某个阶段的建筑扫描完成，参数是阶段下标（0 咖啡厅 / 1 市政厅 / 2 公交站）。</summary>
    public event System.Action<int> TargetCompleted;

    [Header("Main Story Order")]
    public MemoryPillar[] pillars;
    public ScannableTarget[] scanTargets;

    [Header("Surface Extra Scenes")]
    [Tooltip("Shown after the cafe scan completes.")]
    public GameObject cafeExtraScene;
    [Tooltip("Shown after the city hall scan completes.")]
    public GameObject cityHallExtraScene;
    [Tooltip("Shown with the city hall extras after that scan completes.")]
    public GameObject cityHallSharedExtraScene;
    [Tooltip("Shown after the bus stop scan completes.")]
    public GameObject busStopExtraScene;

    [Header("Scene Directors")]
    [Tooltip("咖啡厅四段状态机。")]
    public CafeSceneDirector cafeDirector;
    [Tooltip("市政厅两阶段 + 飞艇调度。")]
    public CityHallSceneDirector cityHallDirector;
    [Tooltip("公交站人物与音频。")]
    public BusStopSceneDirector busStopDirector;

    [Header("Sequence Tuning")]
    [Tooltip("视野判定卡住时的保底推进。本版默认关闭。")]
    public bool 启用保底超时 = false;
    [Tooltip("保底超时秒数。")]
    public float 保底超时秒数 = 4f;
    [Tooltip("从玩家第一次走进咖啡厅内景开始计时，到时强制切水下。")]
    public float 咖啡厅强制结束秒数 = 60f;
    [Tooltip("市政厅从二阶段开始计时，到时强制切水下。")]
    public float 市政厅阶段二强制结束秒数 = 60f;
    [Tooltip("演讲台扫描完成后多久放飞艇。")]
    public float 飞艇延迟秒数 = 8f;

    [Header("Runtime Debug")]
    [SerializeField] private int currentStage = 0;

    private void Start()
    {
        DisableTestRigIfPresent();
        InitializeStory();
    }

    private static void DisableTestRigIfPresent()
    {
        GameObject[] all = FindObjectsByType<GameObject>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null &&
                all[i].name == "TestRig" &&
                all[i].transform.parent == null)
            {
                all[i].SetActive(false);
                return;
            }
        }
    }

    private void InitializeStory()
    {
        if (pillars.Length != scanTargets.Length)
        {
            Debug.LogError(
                "MainStoryManager: Pillars and ScanTargets count must match!"
            );
        }

        HideAllSurfaceExtras();
        InitializeDirectors();

        // ==============================
        // 初始化所有阶段
        // ==============================
        for (int i = 0; i < pillars.Length; i++)
        {
            // 所有柱子剧情交互先关闭
            if (pillars[i] != null)
            {
                pillars[i].SetAvailable(false);

                // 所有柱子的视觉提示先关闭
                MemoryPillarVisual visual =
                    pillars[i].GetComponent<MemoryPillarVisual>();

                if (visual != null)
                {
                    visual.Deactivate();
                }
            }

            // 所有扫描目标关闭
            if (i < scanTargets.Length &&
                scanTargets[i] != null)
            {
                scanTargets[i].SetScanEnabled(false);
            }
        }

        // ==============================
        // 开启第一阶段
        // ==============================
        if (pillars.Length > 0 &&
            pillars[0] != null)
        {
            currentStage = 0;

            ActivateStage(currentStage);
        }

        Debug.Log("Main Story Started.");
    }

    /// <summary>
    /// 开启指定剧情阶段。
    /// 包括：
    /// 1. 开放柱子交互
    /// 2. 开启柱子呼吸发光
    /// 3. 开启柱子表面粒子
    /// </summary>
    private void ActivateStage(int index)
    {
        if (index < 0 ||
            index >= pillars.Length)
        {
            return;
        }

        MemoryPillar pillar =
            pillars[index];

        if (pillar == null)
            return;

        // 开放柱子剧情交互
        pillar.SetAvailable(true);

        // 开启视觉提示
        MemoryPillarVisual visual =
            pillar.GetComponent<MemoryPillarVisual>();

        if (visual != null)
        {
            visual.Activate();
        }

        Debug.Log(
            $"Stage {index + 1} activated. " +
            $"Pillar {index + 1} is now glowing."
        );
    }

    /// <summary>
    /// 玩家成功扫描当前柱子。
    /// </summary>
    public void NotifyPillarActivated(
        MemoryPillar pillar
    )
    {
        int index =
            GetPillarIndex(pillar);

        if (index < 0)
            return;

        if (index != currentStage)
        {
            Debug.LogWarning(
                "This pillar is not the current story pillar."
            );

            return;
        }

        // 柱子触发以后
        // 开放对应扫描目标
        if (index < scanTargets.Length &&
            scanTargets[index] != null)
        {
            scanTargets[index]
                .SetScanEnabled(true);
        }

        // 第一根柱子之后：建筑扫描点云贴在 ScanSurface 上。
        // 不关 PointDots，也不改组员的 3DGS 射线。
        if (index == 0)
        {
            EnableBuildingSurfacePointCloud();
        }

        // 柱子扫完立刻熄灭呼吸发光，光束已经在 ActivatePillar 里打开。
        MemoryPillarVisual visual =
            pillar.GetComponent<MemoryPillarVisual>();

        if (visual != null)
        {
            visual.Deactivate();
        }

        Debug.Log(
            $"Pillar {index + 1} activated. " +
            $"Scan target unlocked."
        );
    }

    /// <summary>
    /// 当前扫描目标重建完成。
    /// </summary>
    public void NotifyTargetCompleted(
        ScannableTarget target
    )
    {
        int index =
            GetTargetIndex(target);

        if (index < 0)
            return;

        if (index != currentStage)
            return;

        Debug.Log(
            $"Target {index + 1} reconstruction completed."
        );

        TargetCompleted?.Invoke(index);

        ShowSurfaceExtrasForCompletedStage(index);

        // ==============================
        // 当前柱子彻底完成
        // ==============================
        if (pillars[index] != null)
        {
            MemoryPillar currentPillar =
                pillars[index];

            // 原有剧情完成逻辑
            currentPillar.MarkCompleted();

            // 关闭呼吸发光和粒子
            MemoryPillarVisual visual =
                currentPillar
                    .GetComponent<MemoryPillarVisual>();

            if (visual != null)
            {
                visual.Complete();
            }
        }

        // ==============================
        // 前进到下一阶段
        // ==============================
        currentStage++;

        if (currentStage < pillars.Length)
        {
            ActivateStage(currentStage);

            Debug.Log(
                $"Pillar {currentStage + 1} " +
                $"is now available."
            );
        }
        else
        {
            Debug.Log(
                "ALL MAIN RECONSTRUCTIONS COMPLETE!"
            );
        }
    }

    private void HideAllSurfaceExtras()
    {
        SetExtraActive(cafeExtraScene, false);
        SetExtraActive(cityHallExtraScene, false);
        SetExtraActive(cityHallSharedExtraScene, false);
        SetExtraActive(busStopExtraScene, false);
    }

    private void InitializeDirectors()
    {
        if (cafeDirector != null)
        {
            cafeDirector.ApplySettings(
                启用保底超时,
                保底超时秒数,
                咖啡厅强制结束秒数
            );

            cafeDirector.Initialize();
        }

        if (cityHallDirector != null)
        {
            cityHallDirector.ApplySettings(
                启用保底超时,
                保底超时秒数,
                市政厅阶段二强制结束秒数,
                飞艇延迟秒数
            );

            cityHallDirector.Initialize();
        }

        if (busStopDirector != null)
            busStopDirector.Initialize();
    }

    /// <summary>
    /// 激活对应的附加场景根节点，并通知该阶段的 director 开始。
    /// 附加场景内部的人物子节点由 director 控制，这里只开根。
    /// </summary>
    private void ShowSurfaceExtrasForCompletedStage(int index)
    {
        if (index == 0)
        {
            SetExtraActive(cafeExtraScene, true);

            if (cafeDirector != null)
                cafeDirector.Begin();

            return;
        }

        if (index == 1)
        {
            SetExtraActive(cityHallExtraScene, true);
            SetExtraActive(cityHallSharedExtraScene, true);

            if (cityHallDirector != null)
                cityHallDirector.Begin();

            return;
        }

        if (index == 2)
        {
            SetExtraActive(busStopExtraScene, true);

            if (busStopDirector != null)
                busStopDirector.Begin();
        }
    }

    private static void SetExtraActive(GameObject extra, bool active)
    {
        if (extra != null)
            extra.SetActive(active);
    }

    private static void EnableBuildingSurfacePointCloud()
    {
        ScannerBehavior[] scanners =
            FindObjectsByType<ScannerBehavior>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0; i < scanners.Length; i++)
        {
            if (scanners[i] != null)
                scanners[i].EnableSurfacePointCloudMode();
        }
    }

    private int GetPillarIndex(
        MemoryPillar pillar
    )
    {
        for (int i = 0;
             i < pillars.Length;
             i++)
        {
            if (pillars[i] == pillar)
                return i;
        }

        return -1;
    }

    private int GetTargetIndex(
        ScannableTarget target
    )
    {
        for (int i = 0;
             i < scanTargets.Length;
             i++)
        {
            if (scanTargets[i] == target)
                return i;
        }

        return -1;
    }
}
