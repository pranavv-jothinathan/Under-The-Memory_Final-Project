using UnityEngine;

/// <summary>
/// 让一个 AudioSource 只在指定的世界里发声，切到另一个世界就暂停。
///
/// 和 <see cref="SurfaceWorldAudio"/> 的区别是这个可以选水上或水下，
/// 而且用 Pause / UnPause 保持播放相位，适合呼吸声这类连续循环的环境音。
///
/// 需要它是因为 WorldSwitchManager.PauseSurfaceAudio 只管 surfaceWorldRoot 子树，
/// 而呼吸声挂在 XR Origin 上跟着玩家走，不在任何一个世界的根节点底下。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class WorldScopedAudio : MonoBehaviour
{
    public enum Scope
    {
        Surface,
        Underwater
    }

    [Tooltip("这个音只在哪个世界里响。")]
    [SerializeField] private Scope playIn = Scope.Underwater;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private WorldSwitchManager worldSwitchManager;

    private bool pausedByThis;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (worldSwitchManager == null)
            worldSwitchManager = FindFirstObjectByType<WorldSwitchManager>();
    }

    private void OnEnable()
    {
        if (worldSwitchManager != null)
        {
            worldSwitchManager.WorldChanged += HandleWorldChanged;
            Apply(worldSwitchManager.currentWorld);
        }
    }

    private void OnDisable()
    {
        if (worldSwitchManager != null)
            worldSwitchManager.WorldChanged -= HandleWorldChanged;
    }

    private void HandleWorldChanged(WorldSwitchManager.CurrentWorld world)
    {
        Apply(world);
    }

    private void Apply(WorldSwitchManager.CurrentWorld world)
    {
        if (audioSource == null)
            return;

        bool surface = world == WorldSwitchManager.CurrentWorld.Surface;
        bool shouldPlay = surface == (playIn == Scope.Surface);

        if (shouldPlay)
        {
            if (!audioSource.isPlaying)
            {
                if (pausedByThis)
                    audioSource.UnPause();
                else
                    audioSource.Play();
            }

            pausedByThis = false;
            return;
        }

        if (audioSource.isPlaying)
        {
            audioSource.Pause();
            pausedByThis = true;
        }
    }
}
