using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

public class WorldSwitchManager : MonoBehaviour
{
    public enum CurrentWorld
    {
        Surface,
        Underwater
    }

    public event Action<CurrentWorld> WorldChanged;

    [Header("Camera")]
    public Camera xrCamera;

    [Header("Layers")]
    public string surfaceLayerName = "SurfaceWorld";
    public string underwaterLayerName = "UnderwaterWorld";

    [Header("World Roots")]
    public GameObject surfaceWorldRoot;
    public GameObject underwaterWorldRoot;

    [Header("Volumes")]
    public GameObject surfaceVolume;
    public GameObject underwaterVolume;

    [Header("Optional Environment")]
    public GameObject surfaceEffects;
    public GameObject underwaterEffects;

    [Header("Transition")]
    public float switchDelay = 0.15f;

    public CurrentWorld currentWorld =
        CurrentWorld.Surface;

    private int surfaceMask;
    private int underwaterMask;

    private bool switching = false;
    private readonly System.Collections.Generic.List<AudioSource> pausedSurfaceAudio =
        new System.Collections.Generic.List<AudioSource>();

    // collider 在场景里本来的开关状态，切世界时用来还原而不是一律打开
    private readonly System.Collections.Generic.Dictionary<Collider, bool> originalColliderEnabled =
        new System.Collections.Generic.Dictionary<Collider, bool>();

    private void Awake()
    {
        int surfaceLayer =
            LayerMask.NameToLayer(surfaceLayerName);

        int underwaterLayer =
            LayerMask.NameToLayer(underwaterLayerName);

        if (surfaceLayer < 0 || underwaterLayer < 0)
        {
            Debug.LogError(
                $"WorldSwitchManager: TagManager 里找不到层 {surfaceLayerName} / {underwaterLayerName}，" +
                "世界切换不会有任何效果。",
                this);
            return;
        }

        surfaceMask = 1 << surfaceLayer;
        underwaterMask = 1 << underwaterLayer;

        ApplyGrabPhysicsMask();
    }

    private void Start()
    {
        ApplyGrabPhysicsMask();
        EnsureWorldRootsActive();
        ApplyWorld(currentWorld);
    }

    public void RequestSwitch(
        LensWorldType targetWorld)
    {
        if (switching)
            return;

        if (
            targetWorld == LensWorldType.Surface &&
            currentWorld == CurrentWorld.Surface
        )
            return;

        if (
            targetWorld == LensWorldType.Underwater &&
            currentWorld == CurrentWorld.Underwater
        )
            return;

        StartCoroutine(
            SwitchRoutine(targetWorld)
        );
    }

    private IEnumerator SwitchRoutine(
        LensWorldType targetWorld)
    {
        switching = true;

        // 后面圆形扩散动画就在这里先播放

        yield return new WaitForSeconds(
            switchDelay
        );

        if (targetWorld == LensWorldType.Surface)
            ApplyWorld(CurrentWorld.Surface);
        else
            ApplyWorld(CurrentWorld.Underwater);

        switching = false;
    }

    private void ApplyWorld(
        CurrentWorld targetWorld)
    {
        currentWorld = targetWorld;

        WorldChanged?.Invoke(currentWorld);

        bool surface =
            currentWorld == CurrentWorld.Surface;

        EnsureWorldRootsActive();
        SetCameraWorldLayer(surface);

        SetColliders(
            surfaceWorldRoot,
            surface
        );

        if (
            surfaceWorldRoot != null &&
            surfaceWorldRoot.transform.parent != null
        )
        {
            SetColliders(
                surfaceWorldRoot.transform.parent.gameObject,
                surface
            );
        }

        SetColliders(
            underwaterWorldRoot,
            !surface
        );

        PauseSurfaceAudio(!surface);

        if (surfaceVolume != null)
            surfaceVolume.SetActive(surface);

        if (underwaterVolume != null)
            underwaterVolume.SetActive(!surface);

        if (surfaceEffects != null)
            surfaceEffects.SetActive(surface);

        if (underwaterEffects != null)
            underwaterEffects.SetActive(!surface);
    }

    /// <summary>
    /// 两个世界的显隐只靠相机 cullingMask，所以两棵树必须一直是激活的。
    ///
    /// 之前有人在 Inspector 里把 ' [WORLD] Surface' 关掉了，结果切上水面只剩留在
    /// Default 的东西能看见 —— 层级修复之后水上内容全在 SurfaceWorld 层，父节点
    /// 一关就整个世界消失。这里连父链一起补开，并把改了谁打到 Console。
    /// </summary>
    private void EnsureWorldRootsActive()
    {
        EnsureActiveWithParents(surfaceWorldRoot);
        EnsureActiveWithParents(underwaterWorldRoot);
    }

    private void EnsureActiveWithParents(GameObject root)
    {
        if (root == null || root.activeInHierarchy)
            return;

        Transform current = root.transform;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
                Debug.LogWarning(
                    $"WorldSwitchManager: '{current.name}' 是关着的，已打开。" +
                    "世界的显隐交给相机层遮罩，不要用 SetActive 关世界根。",
                    current.gameObject);
            }

            current = current.parent;
        }
    }

    private void SetCameraWorldLayer(
        bool surface)
    {
        if (xrCamera == null)
        {
            Debug.LogError(
                "WorldSwitchManager: xrCamera 没接，切世界不会改变任何显隐。",
                this);
            return;
        }

        int mask = xrCamera.cullingMask;

        mask &= ~surfaceMask;
        mask &= ~underwaterMask;

        if (surface)
            mask |= surfaceMask;
        else
            mask |= underwaterMask;

        xrCamera.cullingMask = mask;
    }

    /// <summary>
    /// 关世界时一律关 collider，开世界时只还原到它本来的状态。
    ///
    /// 不能无脑全开：水下清理小游戏的道具（塑料瓶 / 砖块）故意把根节点上的代理
    /// collider 关着，只留子物体上贴合模型的那个。一律打开会把这些代理 collider
    /// 也打开，抓取判定范围变大、物体互相穿插。
    /// </summary>
    private void SetColliders(
        GameObject root,
        bool enabledState)
    {
        if (root == null)
            return;

        Collider[] colliders =
            root.GetComponentsInChildren<Collider>(
                true
            );

        foreach (Collider col in colliders)
        {
            if (col == null)
                continue;

            // 第一次见到某个 collider 时它还是场景里的原始状态，记下来。
            if (!originalColliderEnabled.TryGetValue(col, out bool original))
            {
                original = col.enabled;
                originalColliderEnabled[col] = original;
            }

            col.enabled = enabledState && original;
        }
    }

    private void PauseSurfaceAudio(bool pause)
    {
        if (!pause)
        {
            for (int i = 0; i < pausedSurfaceAudio.Count; i++)
            {
                AudioSource source = pausedSurfaceAudio[i];
                if (source == null || !source.isActiveAndEnabled)
                    continue;

                source.UnPause();
            }

            pausedSurfaceAudio.Clear();
            return;
        }

        pausedSurfaceAudio.Clear();
        if (surfaceWorldRoot == null)
            return;

        AudioSource[] sources = surfaceWorldRoot.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (
                source == null ||
                !source.isActiveAndEnabled ||
                !source.isPlaying
            )
                continue;

            source.Pause();
            pausedSurfaceAudio.Add(source);
        }
    }

    /// <summary>
    /// XRI Near-Far / Direct 默认只打 Default。SurfaceWorld 和 UnderwaterWorld
    /// 上的道具抓不到，只剩玩家身上的扫描仪。
    /// </summary>
    private void ApplyGrabPhysicsMask()
    {
        int worldBits = surfaceMask | underwaterMask;
        if (worldBits == 0)
            return;

        XRDirectInteractor[] directs = FindObjectsByType<XRDirectInteractor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < directs.Length; i++)
        {
            if (directs[i] != null)
                directs[i].physicsLayerMask |= worldBits;
        }

        SphereInteractionCaster[] spheres = FindObjectsByType<SphereInteractionCaster>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < spheres.Length; i++)
        {
            if (spheres[i] != null)
                spheres[i].physicsLayerMask |= worldBits;
        }

        CurveInteractionCaster[] curves = FindObjectsByType<CurveInteractionCaster>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < curves.Length; i++)
        {
            if (curves[i] != null)
                curves[i].raycastMask |= worldBits;
        }

        XRPokeInteractor[] pokes = FindObjectsByType<XRPokeInteractor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < pokes.Length; i++)
        {
            if (pokes[i] != null)
                pokes[i].physicsLayerMask |= worldBits;
        }
    }
}