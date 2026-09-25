using UnityEngine;

public class EcoSinkTrigger : MonoBehaviour
{
    [SerializeField] private Animator ecoAnimator;

    [Tooltip("Animator 中下沉动画状态的名字")]
    [SerializeField] private string sinkStateName = "EcoSink";

    private bool hasPlayed;

    private void Awake()
    {
        if (ecoAnimator == null)
        {
            ecoAnimator = GetComponent<Animator>();
        }
    }

    // 给桥梁的 Animation Event 调用
    public void PlayEcoSink()
    {
        if (hasPlayed || ecoAnimator == null)
        {
            return;
        }

        hasPlayed = true;
        ecoAnimator.Play(sinkStateName, 0, 0f);
    }
}