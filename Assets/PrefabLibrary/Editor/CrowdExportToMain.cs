using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把人群资产从工厂拷进主工程（拷贝，不剪切）。.meta 一起带过去，
/// 这样 GUID 不变，组预制体里的引用不会断。
/// </summary>
public static class CrowdExportToMain
{
    private const string MainProject = @"E:\1U3D\1Project\Group2-VR-Project（3DGS_OceanX）";
    private const string CrowdDest = @"Assets\Scene3\Prefabs\人群";
    private const string SourceDest = @"Assets\Scene3\Prefabs\_Source\PopulationSystem";

    /// <summary>PopulationSystem 里真正被人群预制体用到的部分。Code / Editor / Documentation / Scenes 不拷。</summary>
    private static readonly string[] PopulationFolders =
    {
        "Models",
        "Animations",
        "Materials",
        "Textures",
        "Prefabs"
    };

    private static readonly string[] MixamoFiles =
    {
        "Sitting Talking.fbx",
        "Sitting Talking (1).fbx",
        "Sitting Talking (3).fbx",
        "Sitting.fbx",
        "Standing W_Briefcase Idle.fbx"
    };

    [MenuItem("Prefab Library/Export Crowd Assets To Main Project")]
    public static void Export()
    {
        string factoryAssets = Path.GetFullPath(Application.dataPath);

        if (!Directory.Exists(MainProject))
        {
            Debug.LogError("找不到主工程路径：" + MainProject);
            return;
        }

        var log = new List<string>();
        long bytes = 0;

        // 1. 人群预制体 + 单人预制体 + 动画控制器 + 材质 + 两个脚本
        string crowdSource = Path.Combine(factoryAssets, "PrefabLibrary", "Crowd");
        if (!Directory.Exists(crowdSource))
        {
            Debug.LogError("工厂里还没有 Crowd 资产，先跑 Prefab Library/Create Crowd Actors And Groups。");
            return;
        }

        bytes += CopyTree(crowdSource, Path.Combine(MainProject, CrowdDest), log);

        // 2. mixamo FBX 与它们的 .fbm 贴图目录
        string populationSource = Path.Combine(factoryAssets, "PopulationSystem");
        string populationDest = Path.Combine(MainProject, SourceDest);

        foreach (string file in MixamoFiles)
        {
            bytes += CopyFileWithMeta(
                Path.Combine(populationSource, file),
                Path.Combine(populationDest, file),
                log
            );

            string fbm = Path.ChangeExtension(file, null) + ".fbm";
            bytes += CopyTree(
                Path.Combine(populationSource, fbm),
                Path.Combine(populationDest, fbm),
                log
            );
        }

        // 3. PopulationSystem 里被 Man_* / Girl_* 预制体引用的部分
        foreach (string folder in PopulationFolders)
        {
            bytes += CopyTree(
                Path.Combine(populationSource, folder),
                Path.Combine(populationDest, folder),
                log
            );
        }

        AssetDatabase.Refresh();

        Debug.Log(
            "人群资产已拷进主工程：\n" +
            "  " + Path.Combine(MainProject, CrowdDest) + "\n" +
            "  " + Path.Combine(MainProject, SourceDest) + "\n" +
            "  文件 " + log.Count + " 个，合计 " + (bytes / 1024f / 1024f).ToString("F1") + " MB\n" +
            "接着在主工程里跑 Prefab Library/Setup Crowd And Story In MainScene。"
        );
    }

    private static long CopyTree(string source, string dest, List<string> log)
    {
        if (!Directory.Exists(source))
            return 0;

        Directory.CreateDirectory(dest);
        long bytes = 0;

        foreach (string file in Directory.GetFiles(source))
            bytes += CopyFileRaw(file, Path.Combine(dest, Path.GetFileName(file)), log);

        foreach (string dir in Directory.GetDirectories(source))
        {
            string name = Path.GetFileName(dir);
            bytes += CopyTree(dir, Path.Combine(dest, name), log);

            // 目录自身的 .meta 也要跟着走。
            bytes += CopyFileRaw(dir + ".meta", Path.Combine(dest, name + ".meta"), log);
        }

        return bytes;
    }

    private static long CopyFileWithMeta(string source, string dest, List<string> log)
    {
        long bytes = CopyFileRaw(source, dest, log);
        bytes += CopyFileRaw(source + ".meta", dest + ".meta", log);
        return bytes;
    }

    private static long CopyFileRaw(string source, string dest, List<string> log)
    {
        if (!File.Exists(source))
            return 0;

        string folder = Path.GetDirectoryName(dest);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        var info = new FileInfo(source);
        if (File.Exists(dest) && new FileInfo(dest).Length == info.Length && File.GetLastWriteTimeUtc(dest) >= info.LastWriteTimeUtc)
            return 0;

        File.Copy(source, dest, true);
        log.Add(dest);
        return info.Length;
    }
}
