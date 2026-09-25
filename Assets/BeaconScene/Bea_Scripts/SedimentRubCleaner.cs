using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SedimentRubCleaner : MonoBehaviour
{
    [Header("Hand Detection")]
    [SerializeField]
    private string palmColliderName = "RightPalmContact";

    [Header("Rubbing Requirements")]
    [SerializeField, Min(0.01f)]
    private float requiredRubDistance = 0.5f;

    [SerializeField, Min(1)]
    private int requiredDirectionChanges = 4;

    [SerializeField, Min(0.001f)]
    private float minimumMovementStep = 0.003f;

    [SerializeField, Min(0.01f)]
    private float minimumStrokeDistance = 0.04f;

    [Header("Temporary Visual")]
    [SerializeField, Range(0.2f, 1f)]
    private float remainingScale = 0.45f;

    [SerializeField]
    private Transform sedimentVisual;

    [Header("Sediment Particles")]
    [SerializeField]
    private ParticleSystem sedimentWearParticlesLeft;

    [SerializeField]
    private ParticleSystem sedimentWearParticlesRight;

    [SerializeField, Min(1)]
    private int particlesPerSwipe = 15;

    [Header("Recovery Progress")]
    [SerializeField]
    private RecoveryManager recoveryManager;

    private Vector3 startingVisualScale;
    private Vector3 lastPalmPosition;
    private Vector3 strokeDirection;

    private float totalRubDistance;
    private float currentStrokeDistance;
    private int directionChanges;

    private bool palmTouching;
    private bool cleaningComplete;
    private bool firstStrokeCounted;

    private Collider sedimentCollider;
    private Renderer sedimentRenderer;

    private InteractionAudio interactionAudio;

    private void Awake()
    {
        interactionAudio = GetComponent<InteractionAudio>();
        sedimentCollider = GetComponent<Collider>();
        sedimentRenderer = GetComponent<Renderer>();

        if (sedimentVisual != null)
        {
            startingVisualScale = sedimentVisual.localScale;
        }
        else
        {
            Debug.LogWarning(
                "No Sediment Visual assigned to " + gameObject.name
            );
        }
    }

    private bool IsCorrectPalm(Collider other)
    {
        return other.name == palmColliderName;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsCorrectPalm(other) || cleaningComplete)
            return;

        palmTouching = true;
        lastPalmPosition = other.transform.position;

        strokeDirection = Vector3.zero;
        currentStrokeDistance = 0f;
        firstStrokeCounted = false;

        Debug.Log("Sediment rubbing started.");
    }

    private void OnTriggerStay(Collider other)
    {
        if (!palmTouching || !IsCorrectPalm(other) || cleaningComplete)
            return;

        Vector3 currentPosition = other.transform.position;
        Vector3 movement = currentPosition - lastPalmPosition;
        lastPalmPosition = currentPosition;

        float movementDistance = movement.magnitude;

        if (movementDistance < minimumMovementStep)
            return;

        Vector3 movementDirection = movement.normalized;
        totalRubDistance += movementDistance;

        if (strokeDirection == Vector3.zero)
        {
            strokeDirection = movementDirection;
            currentStrokeDistance = movementDistance;
        }
        else
        {
            float directionDot =
                Vector3.Dot(strokeDirection, movementDirection);

            bool reversedDirection =
                directionDot < -0.5f &&
                currentStrokeDistance >= minimumStrokeDistance;

            if (reversedDirection)
            {
                directionChanges++;

                EmitParticlesForSwipe(strokeDirection);

                Debug.Log(
                    "Rub direction changes: " +
                    directionChanges + "/" +
                    requiredDirectionChanges
                );

                strokeDirection = movementDirection;
                currentStrokeDistance = movementDistance;
            }
            else
            {
                currentStrokeDistance += movementDistance;

                if (directionDot > 0.5f)
                {
                    strokeDirection = Vector3.Lerp(
                        strokeDirection,
                        movementDirection,
                        0.2f
                    ).normalized;
                }
            }
        }

        if (!firstStrokeCounted &&
            currentStrokeDistance >= minimumStrokeDistance)
        {
            firstStrokeCounted = true;
            directionChanges++;

            EmitParticlesForSwipe(strokeDirection);

            Debug.Log(
                "Rub direction changes: " +
                directionChanges + "/" +
                requiredDirectionChanges
            );
        }

        UpdateTemporaryVisual();

        if (totalRubDistance >= requiredRubDistance &&
            directionChanges >= requiredDirectionChanges)
        {
            CompleteCleaning();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsCorrectPalm(other))
            return;

        palmTouching = false;
    }

    private void EmitParticlesForSwipe(Vector3 swipeDirection)
    {
        if (interactionAudio != null)
        {
            interactionAudio.PlayActionSound();
        }

        if (sedimentWearParticlesLeft == null ||
            sedimentWearParticlesRight == null)
        {
            Debug.LogWarning(
                "Left or right sediment particle system is missing."
            );
            return;
        }

        Vector3 leftToRightDirection =
            sedimentWearParticlesRight.transform.position -
            sedimentWearParticlesLeft.transform.position;

        if (leftToRightDirection.sqrMagnitude < 0.000001f)
        {
            Debug.LogWarning(
                "The left and right particle systems are in the same position."
            );
            return;
        }

        float directionDot = Vector3.Dot(
            swipeDirection.normalized,
            leftToRightDirection.normalized
        );

        if (directionDot > 0f)
        {
            sedimentWearParticlesRight.Emit(particlesPerSwipe);
            Debug.Log("Swipe direction: LEFT TO RIGHT ?emitting RIGHT");
        }
        else
        {
            sedimentWearParticlesLeft.Emit(particlesPerSwipe);
            Debug.Log("Swipe direction: RIGHT TO LEFT ?emitting LEFT");
        }
    }

    private void UpdateTemporaryVisual()
    {
        if (sedimentVisual == null)
            return;

        int visibleStage = Mathf.Min(
            directionChanges,
            requiredDirectionChanges - 1
        );

        float scaleMultiplier =
            1f - ((float)visibleStage / requiredDirectionChanges);

        sedimentVisual.localScale =
            startingVisualScale * scaleMultiplier;
    }

    private void CompleteCleaning()
    {
        cleaningComplete = true;

        // Hide old sphere visual if it is still enabled.
        if (sedimentRenderer != null)
            sedimentRenderer.enabled = false;

        // Remove the new sediment model visually.
        if (sedimentVisual != null)
            sedimentVisual.gameObject.SetActive(false);

        // Stop further rubbing detection.
        sedimentCollider.enabled = false;

        Debug.Log("Sediment cleaning complete.");

        if (recoveryManager != null)
        {
            recoveryManager.RegisterRemoved(gameObject);
        }
        else
        {
            Debug.LogWarning(
                "No RecoveryManager assigned to " + gameObject.name
            );
        }
    }
}