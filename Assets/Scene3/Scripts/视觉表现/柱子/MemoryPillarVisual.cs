using UnityEngine;

public class MemoryPillarVisual : MonoBehaviour
{
    [Header("Pillar Renderers")]
    [SerializeField] private Renderer[] pillarRenderers;

    [Header("Glow")]
    [ColorUsage(true, true)]
    [SerializeField]
    private Color glowColor =
        new Color(0.12f, 1.4f, 2.5f, 1f);

    [SerializeField] private float maxGlow = 1.35f;
    [SerializeField] private float pulseSpeed = 1.25f;
    [SerializeField] private float fadeSpeed = 3f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem surfaceParticles;

    private static readonly int EmissionColorID =
        Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock propertyBlock;

    private bool active;
    private float currentGlow;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        active = false;
        currentGlow = 0f;

        ApplyGlow(0f);

        if (surfaceParticles != null)
        {
            surfaceParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    private void Update()
    {
        if (active)
        {
            UpdatePulse();
        }
        else
        {
            UpdateFadeOut();
        }
    }

    private void UpdatePulse()
    {
        float pulse =
            (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;

        float targetGlow =
            pulse * maxGlow;

        currentGlow = Mathf.Lerp(
            currentGlow,
            targetGlow,
            Time.deltaTime * 5f
        );

        ApplyGlow(currentGlow);
    }

    private void UpdateFadeOut()
    {
        if (currentGlow <= 0f)
            return;

        currentGlow = Mathf.MoveTowards(
            currentGlow,
            0f,
            Time.deltaTime * fadeSpeed
        );

        ApplyGlow(currentGlow);
    }

    private void ApplyGlow(float intensity)
    {
        if (pillarRenderers == null)
            return;

        Color finalColor =
            glowColor * intensity;

        foreach (Renderer renderer in pillarRenderers)
        {
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetColor(
                EmissionColorID,
                finalColor
            );

            renderer.SetPropertyBlock(
                propertyBlock
            );
        }
    }

    public void Activate()
    {
        active = true;

        if (surfaceParticles != null &&
            !surfaceParticles.isPlaying)
        {
            surfaceParticles.Play();
        }
    }

    public void Deactivate()
    {
        active = false;

        if (surfaceParticles != null)
        {
            surfaceParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmitting
            );
        }
    }

    public void Complete()
    {
        active = false;
        currentGlow = 0f;

        ApplyGlow(0f);

        if (surfaceParticles != null)
        {
            surfaceParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }
}