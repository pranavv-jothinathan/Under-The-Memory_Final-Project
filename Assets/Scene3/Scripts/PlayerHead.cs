using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// 玩家头部（XR 相机）的统一解析入口。
///
/// MainScene 里有多个相机打了 MainCamera tag，其中包括手持扫描仪上的
/// 00AScannerCamera / 00AScannerCamera2，所以 Camera.main 可能返回的是扫描仪而不是玩家的头。
/// 任何「玩家在哪 / 玩家看向哪」的判断都走这里，不要直接用 Camera.main。
/// </summary>
public static class PlayerHead
{
    private static Camera cached;

    /// <summary>玩家头部相机。解析不到时返回 null。</summary>
    public static Camera Camera
    {
        get
        {
            if (cached != null && cached.isActiveAndEnabled)
                return cached;

            cached = Resolve();
            return cached;
        }
    }

    public static Transform Transform
    {
        get
        {
            Camera cam = Camera;
            return cam != null ? cam.transform : null;
        }
    }

    public static bool TryGetPosition(out Vector3 position)
    {
        Camera cam = Camera;
        if (cam == null)
        {
            position = Vector3.zero;
            return false;
        }

        position = cam.transform.position;
        return true;
    }

    /// <summary>换场景或重新生成 XR Rig 之后强制重新解析。</summary>
    public static void Invalidate()
    {
        cached = null;
    }

    private static Camera Resolve()
    {
        XROrigin origin = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Exclude);
        if (origin != null && origin.Camera != null)
            return origin.Camera;

        // 类名被本类的 Camera 属性遮蔽，这里必须写全名
        Camera main = UnityEngine.Camera.main;
        if (main != null && !IsHandheldCamera(main))
            return main;

        Camera[] all = UnityEngine.Camera.allCameras;
        for (int i = 0; i < all.Length; i++)
        {
            if (!IsHandheldCamera(all[i]))
                return all[i];
        }

        return main;
    }

    /// <summary>扫描仪那几个相机会跟着手走，不能当成玩家的头。</summary>
    private static bool IsHandheldCamera(Camera cam)
    {
        if (cam == null)
            return false;

        Transform t = cam.transform;
        while (t != null)
        {
            if (t.name.IndexOf("Scanner", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            t = t.parent;
        }

        return false;
    }
}
