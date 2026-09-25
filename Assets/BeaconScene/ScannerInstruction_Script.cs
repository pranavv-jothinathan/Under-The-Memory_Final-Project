using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(AudioSource))]
public class ScannerBoundaryAudio : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform playerHead;

    [Header("Audio")]
    [SerializeField] private AudioClip instructionClip;

    [Header("Second Playback")]
    [SerializeField] private float delayBeforeSecondPlay = 2f;

    private BoxCollider boundary;
    private AudioSource audioSource;

    private bool wasInside = false;
    private bool hasPlayed = false;
    private bool sequenceRunning = false;

    private void Awake()
    {
        boundary = GetComponent<BoxCollider>();
        audioSource = GetComponent<AudioSource>();

        boundary.isTrigger = true;

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    private void Update()
    {
        if (playerHead == null)
            return;

        bool isInside = IsPlayerInsideBoundary();

        // Trigger when the player's head enters the boundary
        if (isInside && !wasInside)
        {
            if (!hasPlayed && !sequenceRunning)
            {
                StartCoroutine(PlayInstructionSequence());
            }
        }

        wasInside = isInside;
    }

    private IEnumerator PlayInstructionSequence()
    {
        if (instructionClip == null)
            yield break;

        sequenceRunning = true;
        hasPlayed = true;

        // First playback
        audioSource.clip = instructionClip;
        audioSource.Play();

        Debug.Log("Scanner instruction: first playback started.");

        // Wait until the first playback finishes
        while (audioSource.isPlaying)
        {
            yield return null;
        }

        // Wait for the delay specified in the Inspector
        yield return new WaitForSeconds(delayBeforeSecondPlay);

        // Play the exact same audio again
        audioSource.Play();

        Debug.Log("Scanner instruction: second playback started.");

        // Wait until second playback finishes
        while (audioSource.isPlaying)
        {
            yield return null;
        }

        sequenceRunning = false;
    }

    private bool IsPlayerInsideBoundary()
    {
        Vector3 localPosition =
            boundary.transform.InverseTransformPoint(playerHead.position);

        Vector3 difference =
            localPosition - boundary.center;

        Vector3 halfSize =
            boundary.size * 0.5f;

        return
            Mathf.Abs(difference.x) <= halfSize.x &&
            Mathf.Abs(difference.y) <= halfSize.y &&
            Mathf.Abs(difference.z) <= halfSize.z;
    }
}