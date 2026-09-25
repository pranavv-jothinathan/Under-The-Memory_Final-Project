using UnityEngine;

public class PlayerMovementAudio : MonoBehaviour
{
    public AudioSource swimmingAudio;

    [Header("Movement Detection")]
    public float movementThreshold = 0.01f;

    [Header("Fade")]
    public float fadeSpeed = 3f;
    public float swimmingVolume = 1f;

    private Vector3 lastPosition;

    void Start()
    {
        lastPosition = transform.position;

        if (swimmingAudio != null)
        {
            swimmingAudio.loop = true;
            swimmingAudio.volume = 0f;
        }
    }

    void Update()
    {
        float movementDistance = Vector3.Distance(transform.position, lastPosition);

        bool isMoving = movementDistance > movementThreshold;

        if (swimmingAudio != null)
        {
            float targetVolume = isMoving ? swimmingVolume : 0f;

            swimmingAudio.volume = Mathf.MoveTowards(
                swimmingAudio.volume,
                targetVolume,
                fadeSpeed * Time.deltaTime
            );

            if (isMoving && !swimmingAudio.isPlaying)
            {
                swimmingAudio.Play();
            }

            if (!isMoving && swimmingAudio.volume <= 0.001f && swimmingAudio.isPlaying)
            {
                swimmingAudio.Stop();
            }
        }

        lastPosition = transform.position;
    }
}