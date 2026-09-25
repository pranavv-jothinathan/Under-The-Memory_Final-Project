using UnityEngine;

[DisallowMultipleComponent]
public class StaticCrowdActor : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [Tooltip("Animator state name. Population: talk1, listen, cheer. Mixamo: mixamo_com.")]
    [SerializeField] private string populationState;
    [SerializeField] private bool randomStartPhase = true;
    [SerializeField] private bool freezePose;
    [Tooltip("演讲者等需要在镜头外也保持动作的角色勾上。名字为 Speaker 时也会自动打开。")]
    [SerializeField] private bool forceAlwaysAnimate;

    private void Awake()
    {
        EnsureAnimator();
        ApplyCulling();
    }

    private void OnEnable()
    {
        EnsureAnimator();
        ApplyCulling();
        ApplyPose();
    }

    private void EnsureAnimator()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    private bool ShouldAlwaysAnimate()
    {
        if (forceAlwaysAnimate)
            return true;

        Transform t = transform;
        while (t != null)
        {
            if (t.name.Trim() == "Speaker")
                return true;
            t = t.parent;
        }

        return false;
    }

    private void ApplyCulling()
    {
        if (animator == null || !ShouldAlwaysAnimate())
            return;

        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    private void ApplyPose()
    {
        if (animator == null)
            return;

        ApplyCulling();
        animator.speed = 1f;

        if (!string.IsNullOrEmpty(populationState))
        {
            float phase = randomStartPhase ? Random.Range(0f, 1f) : 0f;
            animator.Play(populationState, 0, phase);
            animator.Update(0f);
        }
        else
        {
            animator.Rebind();
            animator.Update(0f);
        }

        if (freezePose)
            animator.speed = 0f;
    }
}
