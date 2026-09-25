using UnityEngine;

/// <summary>
/// Simple fetch dog. Chases a ball the player throws, then returns it.
/// </summary>
public class FetchDog : MonoBehaviour
{
    private enum State
    {
        Idle,
        Chase,
        Return,
        Cooldown
    }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private RuntimeAnimatorController fetchController;
    [SerializeField] private Transform mouthAnchor;
    [Tooltip("Where the dog brings the ball. Drag the XR camera or player. Empty = Camera.main, then spawn point.")]
    [SerializeField] private Transform returnTarget;
    [Tooltip("Leave empty to chase any FetchBall.")]
    [SerializeField] private FetchBall assignedBall;

    [Header("Movement")]
    [SerializeField] private float runSpeed = 4.5f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float pickupDistance = 0.85f;
    [SerializeField] private float dropDistance = 1.6f;
    [SerializeField] private float chaseTriggerDistance = 1.2f;
    [SerializeField] private float maxChaseDistance = 40f;
    [SerializeField] private float groundRayHeight = 1.2f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Timing")]
    [SerializeField] private float dropCooldown = 1.5f;

    [Header("Animation")]
    [SerializeField] private string runningParameter = "IsRunning";

    [Header("Audio")]
    [SerializeField] private AudioClip runBarkClip;
    [SerializeField] private AudioClip deliveredBarkClip;
    [Tooltip("奔跑叫声播完后，隔这么久再播下一次。")]
    [SerializeField] private float runBarkGap = 2f;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = true;

    private State state = State.Idle;
    private FetchBall activeBall;
    private Vector3 homePosition;
    private float cooldownUntil;
    private int runningHash;
    private bool hasRunningParameter;
    private Collider dogCollider;
    private AudioSource barkSource;
    private Coroutine runBarkRoutine;

    private void Awake()
    {
        homePosition = transform.position;
        dogCollider = GetComponent<Collider>();
        runningHash = Animator.StringToHash(runningParameter);

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        SetupAnimator();
        EnsureMouthAnchor();
        EnsureBarkSource();
    }

    private void SetupAnimator()
    {
        if (animator == null)
            return;

        animator.applyRootMotion = false;

        if (fetchController != null)
            animator.runtimeAnimatorController = fetchController;

        hasRunningParameter = false;
        if (animator.runtimeAnimatorController == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == runningHash)
            {
                hasRunningParameter = true;
                break;
            }
        }

        SetRunning(false);
    }

    private void OnEnable()
    {
        FetchBall.Thrown += OnBallThrown;
        FetchBall.Grabbed += OnBallGrabbed;

        if (barkSource != null)
            barkSource.UnPause();
    }

    private void OnDisable()
    {
        FetchBall.Thrown -= OnBallThrown;
        FetchBall.Grabbed -= OnBallGrabbed;

        if (activeBall != null && activeBall.IsCarriedByDog)
            activeBall.DetachFromMouth(Vector3.zero);

        StopRunBarkLoop();
    }

    private void Update()
    {
        switch (state)
        {
            case State.Idle:
                SetRunning(false);
                break;

            case State.Chase:
                TickChase();
                break;

            case State.Return:
                TickReturn();
                break;

            case State.Cooldown:
                SetRunning(false);
                if (Time.time >= cooldownUntil)
                    SetState(State.Idle);
                break;
        }
    }

    public void NotifyBallStolen(FetchBall ball)
    {
        if (activeBall != ball)
            return;

        activeBall = null;
        SetState(State.Idle);
    }

    private void OnBallThrown(FetchBall ball)
    {
        BeginChase(ball);
    }

    private void OnBallGrabbed(FetchBall ball)
    {
        if (activeBall != ball)
            return;

        if (state == State.Chase)
        {
            activeBall = null;
            SetState(State.Idle);
        }
    }

    private void BeginChase(FetchBall ball)
    {
        if (ball == null)
            return;

        if (assignedBall != null && ball != assignedBall)
            return;

        if (state == State.Return && activeBall == ball)
            return;

        if (ball.IsHeldByPlayer || ball.IsCarriedByDog)
            return;

        activeBall = ball;
        SetState(State.Chase);
    }

    private void TickChase()
    {
        if (activeBall == null)
        {
            SetState(State.Idle);
            return;
        }

        if (Vector3.Distance(transform.position, homePosition) > maxChaseDistance)
        {
            activeBall = null;
            SetState(State.Idle);
            return;
        }

        MoveTowards(activeBall.transform.position);
        SetRunning(true);

        if (HorizontalDistance(transform.position, activeBall.transform.position) > pickupDistance)
            return;

        PickupBall();
    }

    private void TickReturn()
    {
        if (activeBall == null)
        {
            SetState(State.Idle);
            return;
        }

        Vector3 target = GetReturnPosition();
        MoveTowards(target);
        SetRunning(true);

        if (HorizontalDistance(transform.position, target) > dropDistance)
            return;

        DropBall();
    }

    private void PickupBall()
    {
        if (mouthAnchor == null || activeBall == null)
            return;

        if (dogCollider != null && activeBall.BallCollider != null)
            Physics.IgnoreCollision(dogCollider, activeBall.BallCollider, true);

        activeBall.AttachToMouth(mouthAnchor, this);
        SetState(State.Return);
    }

    private void DropBall()
    {
        if (activeBall == null)
            return;

        Vector3 away = transform.forward * 1.2f + Vector3.up * 0.4f;
        FetchBall dropping = activeBall;
        activeBall = null;
        dropping.DetachFromMouth(away);

        if (dogCollider != null && dropping.BallCollider != null)
            Physics.IgnoreCollision(dogCollider, dropping.BallCollider, false);

        StopRunBarkLoop();
        PlayDeliveredBark();
        cooldownUntil = Time.time + dropCooldown;
        SetState(State.Cooldown);
    }

    private void MoveTowards(Vector3 worldTarget)
    {
        Vector3 from = Flatten(transform.position);
        Vector3 to = Flatten(worldTarget);
        Vector3 delta = to - from;

        if (delta.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(delta.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                look,
                turnSpeed * Time.deltaTime
            );
        }

        transform.position += transform.forward * (runSpeed * Time.deltaTime);
        SnapToGround();
    }

    private void SnapToGround()
    {
        Vector3 origin = transform.position + Vector3.up * groundRayHeight;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            groundRayHeight * 2f,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        float bestY = float.MinValue;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == dogCollider)
                continue;

            if (hitCollider.transform.IsChildOf(transform))
                continue;

            if (hits[i].point.y > bestY)
            {
                bestY = hits[i].point.y;
                found = true;
            }
        }

        if (!found)
            return;

        Vector3 p = transform.position;
        p.y = bestY;
        transform.position = p;
    }

    private Vector3 GetReturnPosition()
    {
        if (returnTarget != null)
            return returnTarget.position;

        if (Camera.main != null)
            return Camera.main.transform.position;

        return homePosition;
    }

    private void SetRunning(bool running)
    {
        if (animator == null)
            return;

        if (hasRunningParameter)
            animator.SetBool(runningHash, running);
    }

    private void SetState(State next)
    {
        if (state == next)
            return;

        state = next;
        UpdateRunBarkForState();

        if (logStateChanges)
            Debug.Log($"FetchDog: {next}", this);
    }

    public void StopFetchAudio()
    {
        StopRunBarkLoop();
        if (barkSource != null && barkSource.isPlaying)
            barkSource.Stop();
    }

    private void UpdateRunBarkForState()
    {
        bool running = state == State.Chase || state == State.Return;
        if (running)
            StartRunBarkLoop();
        else
            StopRunBarkLoop();
    }

    private void EnsureBarkSource()
    {
        barkSource = GetComponent<AudioSource>();
        if (barkSource == null)
            barkSource = gameObject.AddComponent<AudioSource>();

        barkSource.playOnAwake = false;
        barkSource.loop = false;
        barkSource.spatialBlend = 1f;
        barkSource.rolloffMode = AudioRolloffMode.Linear;
        barkSource.minDistance = 2f;
        barkSource.maxDistance = 28f;
        barkSource.dopplerLevel = 0f;
    }

    private void StartRunBarkLoop()
    {
        if (runBarkRoutine != null)
            return;

        runBarkRoutine = StartCoroutine(RunBarkLoop());
    }

    private void StopRunBarkLoop()
    {
        if (runBarkRoutine == null)
            return;

        StopCoroutine(runBarkRoutine);
        runBarkRoutine = null;

        if (barkSource != null && barkSource.clip == runBarkClip && barkSource.isPlaying)
            barkSource.Stop();
    }

    private System.Collections.IEnumerator RunBarkLoop()
    {
        while (true)
        {
            if (runBarkClip != null && barkSource != null)
            {
                barkSource.clip = runBarkClip;
                barkSource.Play();
                yield return new WaitForSeconds(runBarkClip.length);
            }

            yield return new WaitForSeconds(Mathf.Max(0f, runBarkGap));
        }
    }

    private void PlayDeliveredBark()
    {
        if (deliveredBarkClip == null || barkSource == null)
            return;

        barkSource.PlayOneShot(deliveredBarkClip);
    }

    private void EnsureMouthAnchor()
    {
        if (mouthAnchor != null)
            return;

        Transform head = FindBone("jaw");
        if (head == null)
            head = FindBone("mouth");
        if (head == null)
            head = FindBone("head");

        Transform parent = head != null ? head : transform;
        GameObject anchor = new GameObject("MouthAnchor");
        anchor.transform.SetParent(parent, false);
        anchor.transform.localPosition = head != null
            ? new Vector3(0f, 0.02f, 0.12f)
            : new Vector3(0f, 0.55f, 0.55f);
        mouthAnchor = anchor.transform;
    }

    private Transform FindBone(string namePart)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return child;
        }

        return null;
    }

    private static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
