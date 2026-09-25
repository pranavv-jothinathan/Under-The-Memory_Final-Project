using System;
using UnityEngine;

/// <summary>
/// 按剧情阶段切换水上世界的天空盒和天光：咖啡厅 / 市政厅一阶段是白天，
/// 市政厅二阶段（抗议）是黄昏，公交站台是夜晚。切换是瞬间的，没有淡入淡出。
///
/// 只换天空盒是不够的。场景的 AmbientMode 是 Custom，环境光取的是一个固定颜色，
/// 跟天空盒没有任何关系，所以光换天会出现「天黑了但地面还是白天的光」。
/// 这里把环境光、主平行光、雾色一起切过去。
///
/// 水下世界不归这里管：切到水下会恢复成场景原本的天空和光照设置，
/// 组员之后往 Lighting 里配水下天空盒就会自动生效。
/// </summary>
public class SkyboxDirector : MonoBehaviour
{
    public enum Mood
    {
        Day,
        Dusk,
        Night
    }

    /// <summary>一个时间点的完整天光设置。</summary>
    [Serializable]
    public class MoodSettings
    {
        public Material skybox;

        [Header("Ambient")]
        public Color ambient = new Color(0.55f, 0.60f, 0.66f);

        [Header("Directional Light")]
        public Color lightColor = Color.white;
        public float lightIntensity = 1.2f;
        [Tooltip("平行光的欧拉角，决定太阳高度和方位。")]
        public Vector3 lightEuler = new Vector3(50f, -30f, 0f);

        [Header("Fog")]
        public Color fogColor = new Color(0.72f, 0.80f, 0.86f);
        public float fogDensity = 0.004f;
    }

    public static SkyboxDirector Instance { get; private set; }

    [Header("Moods")]
    [SerializeField] private MoodSettings day = new MoodSettings();
    [SerializeField] private MoodSettings dusk = new MoodSettings();
    [SerializeField] private MoodSettings night = new MoodSettings();

    [Header("Refs")]
    [SerializeField] private Light sunLight;
    [SerializeField] private WorldSwitchManager worldSwitch;

    private readonly MoodSettings sceneOriginal = new MoodSettings();
    private Mood currentMood = Mood.Day;
    private bool moodChosen;
    private Material runtimeSkybox;
    private Material runtimeSkyboxSource;

    /// <summary>剧情脚本调用这个切换时间点。</summary>
    public static void SetMood(Mood mood)
    {
        if (Instance != null)
            Instance.ApplyMood(mood);
    }

    private void Awake()
    {
        Instance = this;

        if (sunLight == null)
            sunLight = FindMainDirectionalLight();

        if (worldSwitch == null)
            worldSwitch = FindFirstObjectByType<WorldSwitchManager>();

        CaptureSceneOriginal();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (runtimeSkybox != null)
            Destroy(runtimeSkybox);
    }

    private void OnEnable()
    {
        if (worldSwitch != null)
            worldSwitch.WorldChanged += HandleWorldChanged;
    }

    private void OnDisable()
    {
        if (worldSwitch != null)
            worldSwitch.WorldChanged -= HandleWorldChanged;
    }

    public void ApplyMood(Mood mood)
    {
        currentMood = mood;
        moodChosen = true;

        if (IsSurface())
            ApplyImmediate(Resolve(mood));
    }

    private void HandleWorldChanged(WorldSwitchManager.CurrentWorld world)
    {
        if (world == WorldSwitchManager.CurrentWorld.Surface)
        {
            // 还没走到任何剧情节点就先给白天，免得水上第一眼是水下的天。
            ApplyImmediate(Resolve(moodChosen ? currentMood : Mood.Day));
            return;
        }

        // 水上的天空不带进水下，还回场景原本的设置。
        ApplyImmediate(sceneOriginal);
    }

    private bool IsSurface()
    {
        return worldSwitch == null ||
            worldSwitch.currentWorld == WorldSwitchManager.CurrentWorld.Surface;
    }

    private MoodSettings Resolve(Mood mood)
    {
        switch (mood)
        {
            case Mood.Dusk:
                return dusk;
            case Mood.Night:
                return night;
            default:
                return day;
        }
    }

    private void CaptureSceneOriginal()
    {
        sceneOriginal.skybox = RenderSettings.skybox;
        sceneOriginal.ambient = RenderSettings.ambientLight;
        sceneOriginal.fogColor = RenderSettings.fogColor;
        sceneOriginal.fogDensity = RenderSettings.fogDensity;

        if (sunLight != null)
        {
            sceneOriginal.lightColor = sunLight.color;
            sceneOriginal.lightIntensity = sunLight.intensity;
            sceneOriginal.lightEuler = sunLight.transform.eulerAngles;
        }
    }

    private void ApplyImmediate(MoodSettings target)
    {
        if (target == null)
            return;

        SwapSkybox(target.skybox);
        ApplyLighting(target);
        DynamicGI.UpdateEnvironment();
    }

    private void ApplyLighting(MoodSettings target)
    {
        RenderSettings.ambientLight = target.ambient;
        RenderSettings.fogColor = target.fogColor;
        RenderSettings.fogDensity = target.fogDensity;

        if (sunLight == null)
            return;

        sunLight.color = target.lightColor;
        sunLight.intensity = target.lightIntensity;
        sunLight.transform.rotation = Quaternion.Euler(target.lightEuler);
    }

    /// <summary>
    /// 换天空盒。用材质实例而不是资产本身，避免运行时改到共享材质。
    /// </summary>
    private void SwapSkybox(Material skybox)
    {
        // null 也是合法目标：场景原本就没有有效天空盒，回水下时要还原成没有。
        if (runtimeSkyboxSource == skybox && RenderSettings.skybox == runtimeSkybox)
            return;

        if (runtimeSkybox != null)
            Destroy(runtimeSkybox);

        runtimeSkyboxSource = skybox;
        runtimeSkybox = skybox != null ? new Material(skybox) : null;
        RenderSettings.skybox = runtimeSkybox;
        DynamicGI.UpdateEnvironment();
    }

    private static Light FindMainDirectionalLight()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        Light best = null;

        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type != LightType.Directional)
                continue;

            if (best == null || lights[i].intensity > best.intensity)
                best = lights[i];
        }

        return best;
    }
}
