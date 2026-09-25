using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一控制多层字幕帘的颜色和亮度。走 MaterialPropertyBlock，所以运行时改色
/// 不会生成材质实例，也不会把改动写回材质资源。每层保留自己的相对倍数，
/// 这样远层始终比近层暗、比近层慢。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class MatrixCurtainController : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public Renderer renderer;

        [Range(0f, 2f)]
        public float brightnessScale = 1f;

        [Range(0f, 1f)]
        public float alphaScale = 1f;

        [Range(0f, 10f)]
        public float speedScale = 1f;

        public float seed;
    }

    [Header("颜色（HDR）")]
    [ColorUsage(true, true)]
    public Color matrixColor = new Color(0f, 1f, 0.18f, 1f);

    [ColorUsage(true, true)]
    public Color headColor = new Color(0.7f, 1f, 0.8f, 1f);

    [Header("强度")]
    [Range(0f, 8f)]
    public float brightness = 1.5f;

    [Range(0f, 1f)]
    public float alpha = 1f;

    [Tooltip("整体倍率。每层自己的下落速度存在 speedScale 里，1 就是按生成时标定的速度跑。")]
    [Range(0f, 3f)]
    public float fallSpeed = 1f;

    [Header("层")]
    public List<Layer> layers = new List<Layer>();

    private static readonly int MatrixColorId = Shader.PropertyToID("_MatrixColor");
    private static readonly int HeadColorId = Shader.PropertyToID("_HeadColor");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int FallSpeedId = Shader.PropertyToID("_FallSpeed");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");

    private MaterialPropertyBlock block;
    private Coroutine fadeRoutine;

    private void OnEnable()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    public void Apply()
    {
        if (layers == null)
            return;

        block ??= new MaterialPropertyBlock();

        for (int i = 0; i < layers.Count; i++)
        {
            Layer layer = layers[i];

            if (layer == null || layer.renderer == null)
                continue;

            layer.renderer.GetPropertyBlock(block);
            block.SetColor(MatrixColorId, matrixColor);
            block.SetColor(HeadColorId, headColor);
            block.SetFloat(BrightnessId, brightness * layer.brightnessScale);
            block.SetFloat(AlphaId, alpha * layer.alphaScale);
            block.SetFloat(FallSpeedId, fallSpeed * layer.speedScale);
            block.SetFloat(SeedId, layer.seed);
            layer.renderer.SetPropertyBlock(block);
        }
    }

    public void SetColor(Color color)
    {
        matrixColor = color;
        Apply();
    }

    public void SetHeadColor(Color color)
    {
        headColor = color;
        Apply();
    }

    public void SetBrightness(float value)
    {
        brightness = value;
        Apply();
    }

    public void SetAlpha(float value)
    {
        alpha = Mathf.Clamp01(value);
        Apply();
    }

    public void SetFallSpeed(float value)
    {
        fallSpeed = value;
        Apply();
    }

    /// <summary>剧情里换色用，避免硬切。</summary>
    public void FadeToColor(Color target, float duration)
    {
        if (!Application.isPlaying || duration <= 0f)
        {
            SetColor(target);
            return;
        }

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeRoutine(target, duration));
    }

    private IEnumerator FadeRoutine(Color target, float duration)
    {
        Color from = matrixColor;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            matrixColor = Color.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
            Apply();
            yield return null;
        }

        matrixColor = target;
        Apply();
        fadeRoutine = null;
    }

    [ContextMenu("重新收集子层")]
    public void CollectLayers()
    {
        Renderer[] found = GetComponentsInChildren<Renderer>(true);
        var rebuilt = new List<Layer>(found.Length);

        for (int i = 0; i < found.Length; i++)
        {
            Layer existing = layers?.Find(l => l != null && l.renderer == found[i]);

            rebuilt.Add(existing ?? new Layer
            {
                renderer = found[i],
                seed = i * 137f,
            });
        }

        layers = rebuilt;
        Apply();
    }
}
