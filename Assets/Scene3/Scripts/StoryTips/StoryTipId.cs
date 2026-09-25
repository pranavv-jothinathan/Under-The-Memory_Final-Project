/// <summary>
/// Scene3 主线剧情提示。编号跟策划稿里的图号一一对应。
/// </summary>
public enum StoryTipId
{
    None = 0,

    /// <summary>图2：靠近水下箱子，检测到微弱的档案透镜信号。</summary>
    ArchiveLensDetected = 1,

    /// <summary>图4：从箱子里拿起透镜，提示按 Y 呼出滤镜仓。</summary>
    LensReadyPressY = 2,

    /// <summary>图5：第一次呼出滤镜仓，提示抓左边那片透镜。</summary>
    GrabLeftLens = 3,

    /// <summary>图6：第一次上到水面，还没有任何档案数据。</summary>
    NoArchiveData = 4,

    /// <summary>图7：第一次回到水下，提示去扫发光的柱子。</summary>
    FollowThePillars = 5,

    /// <summary>图8：每个阶段建筑扫描完成。</summary>
    ScanComplete = 6,

    /// <summary>图9：只在第一次扫描完成后衔接一次，提示换透镜看过去。</summary>
    ArchiveDataAvailable = 7,
}
