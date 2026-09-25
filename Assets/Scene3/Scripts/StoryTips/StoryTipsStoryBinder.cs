using System.Collections;
using UnityEngine;

/// <summary>
/// 把剧情事件接到 StoryTipsPresenter 上。
///
/// [SYS] FilterSystem 开局是关着的（抓到箱子里那片透镜才通电），所以
/// LensMenuController 和 WorldSwitchManager 在 Start 的时候还不是活动对象。
/// 必须用 FindObjectsInactive.Include 去找，而且要反复重试直到找到。
/// </summary>
public class StoryTipsStoryBinder : MonoBehaviour
{
    [SerializeField] private StoryTipsPresenter presenter;

    [Tooltip("第一次切到水上之后，等这么久再弹「没有档案数据」。")]
    [Min(0f)]
    [SerializeField] private float noArchiveDataDelay = 4f;

    [Tooltip("没找到管理器时的重试间隔（秒）。")]
    [Min(0.1f)]
    [SerializeField] private float rebindInterval = 0.5f;

    private LensMenuController lensMenu;
    private WorldSwitchManager worldSwitch;
    private MainStoryManager storyManager;

    private WorldSwitchManager.CurrentWorld lastWorld;
    private bool surfaceSeen;
    private Coroutine pendingNoArchiveData;

    private void OnEnable()
    {
        if (presenter == null)
            presenter = GetComponent<StoryTipsPresenter>();

        StartCoroutine(BindLoop());
    }

    private void OnDisable()
    {
        if (lensMenu != null)
            lensMenu.MenuOpened -= OnLensMenuOpened;

        if (worldSwitch != null)
            worldSwitch.WorldChanged -= OnWorldChanged;

        if (storyManager != null)
            storyManager.TargetCompleted -= OnTargetCompleted;

        lensMenu = null;
        worldSwitch = null;
        storyManager = null;
    }

    private IEnumerator BindLoop()
    {
        while (lensMenu == null || worldSwitch == null || storyManager == null)
        {
            TryBind();

            if (lensMenu != null && worldSwitch != null && storyManager != null)
                break;

            yield return new WaitForSeconds(rebindInterval);
        }
    }

    private void TryBind()
    {
        if (lensMenu == null)
        {
            lensMenu = FindFirstObjectByType<LensMenuController>(
                FindObjectsInactive.Include);

            if (lensMenu != null)
                lensMenu.MenuOpened += OnLensMenuOpened;
        }

        if (worldSwitch == null)
        {
            worldSwitch = FindFirstObjectByType<WorldSwitchManager>(
                FindObjectsInactive.Include);

            if (worldSwitch != null)
            {
                // 订阅那一刻的世界当基线。WorldSwitchManager.Start 会立刻广播一次
                // 「当前世界」，不过滤掉的话第一条水下提示会被它白白消耗掉。
                lastWorld = worldSwitch.currentWorld;
                worldSwitch.WorldChanged += OnWorldChanged;
            }
        }

        if (storyManager == null)
        {
            storyManager = FindFirstObjectByType<MainStoryManager>(
                FindObjectsInactive.Include);

            if (storyManager != null)
                storyManager.TargetCompleted += OnTargetCompleted;
        }
    }

    private void OnLensMenuOpened()
    {
        if (presenter != null)
            presenter.Show(StoryTipId.GrabLeftLens);
    }

    private void OnWorldChanged(WorldSwitchManager.CurrentWorld world)
    {
        if (world == lastWorld)
            return;

        lastWorld = world;

        if (world == WorldSwitchManager.CurrentWorld.Surface)
        {
            surfaceSeen = true;

            if (pendingNoArchiveData != null)
                StopCoroutine(pendingNoArchiveData);

            pendingNoArchiveData = StartCoroutine(
                ShowAfterDelay(StoryTipId.NoArchiveData, noArchiveDataDelay));

            return;
        }

        // 又下水了。延迟中的「没有档案数据」作废，不要等玩家回到水下才弹。
        if (pendingNoArchiveData != null)
        {
            StopCoroutine(pendingNoArchiveData);
            pendingNoArchiveData = null;
        }

        // 没上过水面就不算「回到水下」
        if (surfaceSeen && presenter != null)
            presenter.Show(StoryTipId.FollowThePillars);
    }

    private void OnTargetCompleted(int stageIndex)
    {
        if (presenter != null)
            presenter.Show(StoryTipId.ScanComplete);
    }

    private IEnumerator ShowAfterDelay(StoryTipId tip, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        pendingNoArchiveData = null;

        if (presenter != null)
            presenter.Show(tip);
    }
}
