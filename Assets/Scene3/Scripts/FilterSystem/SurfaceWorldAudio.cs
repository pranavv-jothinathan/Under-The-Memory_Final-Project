using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SurfaceWorldAudio : MonoBehaviour
{
    [SerializeField] private WorldSwitchManager worldSwitchManager;
    [SerializeField] private AudioSource audioSource;

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
            worldSwitchManager.WorldChanged += HandleWorldChanged;

        Apply(GetCurrentWorld());
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

    private WorldSwitchManager.CurrentWorld GetCurrentWorld()
    {
        if (worldSwitchManager != null)
            return worldSwitchManager.currentWorld;

        return WorldSwitchManager.CurrentWorld.Underwater;
    }

    private void Apply(WorldSwitchManager.CurrentWorld world)
    {
        if (audioSource == null)
            return;

        bool surface = world == WorldSwitchManager.CurrentWorld.Surface;

        if (surface)
        {
            audioSource.enabled = true;
            if (!audioSource.isPlaying)
                audioSource.Play();
        }
        else
        {
            audioSource.Stop();
            audioSource.enabled = false;
        }
    }
}
