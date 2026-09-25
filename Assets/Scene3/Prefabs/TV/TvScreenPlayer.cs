using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 咖啡厅内景电视：把视频播到屏幕材质上，配一条独立的 3D 播报音轨，
/// 并跟着世界切换暂停 / 恢复。
///
/// 音频不走 VideoPlayer，是为了让声音有方位感，也避免 mp4 自带音轨和播报 mp3 打架。
/// 屏幕开自发光，否则在咖啡厅室内光下会灰扑扑看不清。
/// </summary>
public class TvScreenPlayer : MonoBehaviour
{
    [Header("Screen")]
    [Tooltip("屏幕在 Renderer.materials 里的下标。TV 的槽 0 是外壳 tv，槽 1 是 Screen。")]
    [SerializeField] private int screenMaterialIndex = 1;
    [SerializeField] private int renderTextureWidth = 1280;
    [SerializeField] private int renderTextureHeight = 720;
    [Tooltip("屏幕自发光强度。太高会在暗处糊成一片白。")]
    [SerializeField] private float emissionIntensity = 1.4f;

    [Header("Video")]
    [Tooltip("按顺序循环播放。")]
    [SerializeField] private List<VideoClip> clips = new List<VideoClip>();

    [Header("Audio")]
    [SerializeField] private AudioClip broadcastClip;
    [SerializeField] private float volume = 0.7f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 14f;

    private Renderer screenRenderer;
    private Material screenMaterial;
    private RenderTexture screenTexture;
    private VideoPlayer videoPlayer;
    private AudioSource audioSource;
    private WorldSwitchManager worldSwitch;

    private int clipIndex;
    private bool suspended;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        screenRenderer = GetComponent<Renderer>();
        if (screenRenderer == null)
        {
            Debug.LogError("TvScreenPlayer: 需要挂在带 Renderer 的物体上。", this);
            enabled = false;
            return;
        }

        EnsureScreenTexture();
        EnsureVideoPlayer();
        EnsureAudioSource();
        BindScreenMaterial();
    }

    private void OnEnable()
    {
        worldSwitch = FindFirstObjectByType<WorldSwitchManager>();
        if (worldSwitch != null)
        {
            worldSwitch.WorldChanged += OnWorldChanged;
            ApplyWorld(worldSwitch.currentWorld);
            return;
        }

        StartPlayback();
    }

    private void OnDisable()
    {
        if (worldSwitch != null)
            worldSwitch.WorldChanged -= OnWorldChanged;

        worldSwitch = null;
        Suspend();
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnClipFinished;

        if (screenMaterial != null)
            Destroy(screenMaterial);

        if (screenTexture != null)
        {
            screenTexture.Release();
            Destroy(screenTexture);
        }
    }

    private void OnWorldChanged(WorldSwitchManager.CurrentWorld world)
    {
        ApplyWorld(world);
    }

    private void ApplyWorld(WorldSwitchManager.CurrentWorld world)
    {
        if (world == WorldSwitchManager.CurrentWorld.Surface)
            StartPlayback();
        else
            Suspend();
    }

    private void StartPlayback()
    {
        if (videoPlayer == null)
            return;

        if (suspended && videoPlayer.isPaused)
        {
            videoPlayer.Play();
            if (audioSource != null && broadcastClip != null)
                audioSource.UnPause();

            suspended = false;
            return;
        }

        suspended = false;

        if (clips.Count > 0)
        {
            if (videoPlayer.clip == null)
                PlayClip(clipIndex);
            else if (!videoPlayer.isPlaying)
                videoPlayer.Play();
        }

        if (audioSource != null && broadcastClip != null && !audioSource.isPlaying)
            audioSource.Play();
    }

    private void Suspend()
    {
        suspended = true;

        if (videoPlayer != null && videoPlayer.isPlaying)
            videoPlayer.Pause();

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Pause();
    }

    private void PlayClip(int index)
    {
        if (clips.Count == 0)
            return;

        clipIndex = ((index % clips.Count) + clips.Count) % clips.Count;
        VideoClip clip = clips[clipIndex];
        if (clip == null)
            return;

        videoPlayer.clip = clip;
        videoPlayer.Play();
    }

    private void OnClipFinished(VideoPlayer source)
    {
        PlayClip(clipIndex + 1);
    }

    private void EnsureScreenTexture()
    {
        screenTexture = new RenderTexture(
            Mathf.Max(64, renderTextureWidth),
            Mathf.Max(64, renderTextureHeight),
            0,
            RenderTextureFormat.Default)
        {
            name = "RT_TvScreen",
        };

        screenTexture.Create();
    }

    private void EnsureVideoPlayer()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null)
            videoPlayer = gameObject.AddComponent<VideoPlayer>();

        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = screenTexture;

        // 声音走下面的 AudioSource，这里关掉避免和播报音轨重叠
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

        // 播放列表由 OnClipFinished 推进，所以单个片子不循环
        videoPlayer.isLooping = false;
        videoPlayer.loopPointReached += OnClipFinished;
    }

    private void EnsureAudioSource()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.clip = broadcastClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = volume;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.dopplerLevel = 0f;
    }

    private void BindScreenMaterial()
    {
        if (screenRenderer == null)
            return;

        Material[] materials = screenRenderer.materials;
        if (screenMaterialIndex < 0 || screenMaterialIndex >= materials.Length)
        {
            Debug.LogWarning(
                $"TvScreenPlayer: 屏幕材质下标 {screenMaterialIndex} 超出范围（共 {materials.Length} 个槽）。",
                this);
            return;
        }

        // renderer.materials 返回的已经是实例，不会污染共享的 Screen.mat
        screenMaterial = materials[screenMaterialIndex];

        screenMaterial.SetTexture(BaseMapId, screenTexture);

        // 原始 Screen 材质底色是深灰，会把视频压暗
        if (screenMaterial.HasProperty(BaseColorId))
            screenMaterial.SetColor(BaseColorId, Color.white);

        if (screenMaterial.HasProperty(EmissionMapId))
        {
            screenMaterial.EnableKeyword("_EMISSION");
            screenMaterial.SetTexture(EmissionMapId, screenTexture);
            screenMaterial.SetColor(EmissionColorId, Color.white * emissionIntensity);

            // 屏幕内容每帧都在变，不能参与烘焙
            screenMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        screenRenderer.materials = materials;
    }
}
