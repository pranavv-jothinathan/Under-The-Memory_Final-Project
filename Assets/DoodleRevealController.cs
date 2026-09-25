using System.Collections;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Reveals a 3D memory object by moving and scaling it from a doodle on a wall
/// to a chosen end point. Call Reveal() from an XR touch event later.
/// </summary>
public class DoodleRevealController : MonoBehaviour
{
    [Header("Required References")]
    [SerializeField] private GameObject doodleVisual;
    [SerializeField] private Transform revealedObject;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform curvePoint;
    [SerializeField] private Transform endPoint;

    [Header("Reveal Settings")]
    [Min(0.1f)]
    [SerializeField] private float duration = 1.5f;
    [Range(0.001f, 1f)]
    [SerializeField] private float startingScale = 0.05f;
    [SerializeField] private AnimationCurve movementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool hideDoodleAfterReveal = true;
    [SerializeField] private AudioSource revealAudio;

    [Header("Motion Style")]
    [Range(0f, 30f)]
    [SerializeField] private float wobbleDegrees = 8f;
    [Range(0.25f, 3f)]
    [SerializeField] private float wobbleCycles = 1.25f;
    [Range(0f, 0.5f)]
    [SerializeField] private float landingBounce = 0.12f;
    [Range(0.3f, 0.9f)]
    [SerializeField] private float growthEndTime = 0.72f;

    private Vector3 finalLocalScale;
    private Quaternion finalRotation;
    private bool isRevealed;
    private Coroutine revealRoutine;

    private void Awake()
    {
        if (!ReferencesAreValid())
        {
            enabled = false;
            return;
        }

        // The flower is positioned at its final location while authoring the scene.
        finalLocalScale = revealedObject.localScale;
        finalRotation = revealedObject.rotation;
        PrepareHiddenState();
    }

    private void Update()
    {
        // Temporary Editor/PC test. XR touch will call Reveal() directly later.
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Reveal();
        }
#else
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Reveal();
        }
#endif
    }

    /// <summary>
    /// Starts the reveal. This public method can be connected to an XR event.
    /// </summary>
    public void Reveal()
    {
        if (isRevealed || revealRoutine != null || !enabled)
        {
            return;
        }

        revealRoutine = StartCoroutine(RevealRoutine());
    }

    /// <summary>
    /// Restores the initial state for testing or replaying the interaction.
    /// </summary>
    public void ResetReveal()
    {
        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
            revealRoutine = null;
        }

        isRevealed = false;
        PrepareHiddenState();
    }

    private IEnumerator RevealRoutine()
    {
        isRevealed = true;
        revealedObject.gameObject.SetActive(true);
        revealedObject.position = startPoint.position;
        revealedObject.rotation = finalRotation;
        revealedObject.localScale = finalLocalScale * startingScale;

        if (revealAudio != null)
        {
            revealAudio.Play();
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float curvedTime = movementCurve.Evaluate(normalizedTime);

            revealedObject.position = QuadraticBezier(
                startPoint.position,
                curvePoint.position,
                endPoint.position,
                curvedTime);

            float scaleFactor = CalculateScaleFactor(normalizedTime);
            revealedObject.localScale = finalLocalScale * scaleFactor;

            float wobble = Mathf.Sin(
                normalizedTime * Mathf.PI * 2f * wobbleCycles)
                * (1f - normalizedTime)
                * wobbleDegrees;

            revealedObject.rotation =
                Quaternion.AngleAxis(wobble, Vector3.forward) * finalRotation;

            yield return null;
        }

        revealedObject.position = endPoint.position;
        revealedObject.rotation = finalRotation;
        revealedObject.localScale = finalLocalScale;

        if (hideDoodleAfterReveal && doodleVisual != null)
        {
            doodleVisual.SetActive(false);
        }

        revealRoutine = null;
    }

    private void PrepareHiddenState()
    {
        revealedObject.position = startPoint.position;
        revealedObject.rotation = finalRotation;
        revealedObject.localScale = finalLocalScale * startingScale;
        revealedObject.gameObject.SetActive(false);

        if (doodleVisual != null)
        {
            doodleVisual.SetActive(true);
        }
    }

    private bool ReferencesAreValid()
    {
        if (revealedObject != null
            && startPoint != null
            && curvePoint != null
            && endPoint != null)
        {
            return true;
        }

        Debug.LogError(
            "DoodleRevealController requires Revealed Object, Start Point, Curve Point, and End Point references.",
            this);
        return false;
    }

    private float CalculateScaleFactor(float normalizedTime)
    {
        if (normalizedTime < growthEndTime)
        {
            float growthTime = Mathf.Clamp01(normalizedTime / growthEndTime);
            growthTime = Mathf.SmoothStep(0f, 1f, growthTime);
            return Mathf.Lerp(startingScale, 1f, growthTime);
        }

        float landingTime = Mathf.InverseLerp(
            growthEndTime,
            1f,
            normalizedTime);

        float dampedBounce = Mathf.Sin(landingTime * Mathf.PI * 2f)
            * (1f - landingTime)
            * landingBounce;

        return 1f + dampedBounce;
    }

    private static Vector3 QuadraticBezier(
        Vector3 start,
        Vector3 control,
        Vector3 end,
        float time)
    {
        Vector3 firstLine = Vector3.Lerp(start, control, time);
        Vector3 secondLine = Vector3.Lerp(control, end, time);
        return Vector3.Lerp(firstLine, secondLine, time);
    }
}
