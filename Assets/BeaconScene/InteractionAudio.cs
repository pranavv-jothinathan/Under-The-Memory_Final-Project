using UnityEngine;

public class InteractionAudio : MonoBehaviour
{
    [Header("Audio Clips")]
    [SerializeField] private AudioClip impactClip;
    [SerializeField] private AudioClip actionClip;

    [Header("Impact Settings")]
    [SerializeField] private float minimumImpactVelocity = 0.5f;
    [SerializeField] private float impactCooldown = 0.15f;

    private AudioSource audioSource;
    private float lastImpactTime = -10f;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;

        // Fully 3D spatial audio.
        audioSource.spatialBlend = 1f;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (impactClip == null)
            return;

        if (collision.relativeVelocity.magnitude < minimumImpactVelocity)
            return;

        if (Time.time - lastImpactTime < impactCooldown)
            return;

        lastImpactTime = Time.time;

        audioSource.PlayOneShot(impactClip);
    }

    public void PlayActionSound()
    {
        if (actionClip == null)
            return;

        audioSource.PlayOneShot(actionClip);
    }
}