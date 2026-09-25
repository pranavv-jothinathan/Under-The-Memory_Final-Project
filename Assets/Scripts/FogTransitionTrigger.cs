using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FogTransitionTrigger : MonoBehaviour
{
    [System.Serializable]
    public class FogSettings
    {
        [Header("雾颜色")]
        public Color fogColor = Color.gray;

        [Header("雾浓度")]
        [Min(0f)]
        public float fogDensity = 0.02f;

        [Header("过渡时长（秒）")]
        [Min(0f)]
        public float transitionDuration = 2f;
    }

    [Header("进入区域")]
    [Tooltip("关闭后，Player 进入此区域时不改变雾效")]
    public bool enableOnEnter = true;

    public FogSettings onEnter = new FogSettings();

    [Header("离开区域")]
    [Tooltip("关闭后，Player 离开此区域时不改变雾效")]
    public bool enableOnExit = true;

    public FogSettings onExit = new FogSettings();

    private Coroutine fogCoroutine;




    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || !enableOnEnter)
            return;

        StartFogTransition(onEnter);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") || !enableOnExit)
            return;

        StartFogTransition(onExit);
    }

    private void StartFogTransition(FogSettings target)
    {
        if (fogCoroutine != null)
            StopCoroutine(fogCoroutine);

        fogCoroutine = StartCoroutine(TransitionFog(target));
    }

    private IEnumerator TransitionFog(FogSettings target)
    {
        RenderSettings.fog = true;

        Color startColor = RenderSettings.fogColor;
        float startDensity = RenderSettings.fogDensity;

        if (target.transitionDuration <= 0f)
        {
            RenderSettings.fogColor = target.fogColor;
            RenderSettings.fogDensity = target.fogDensity;
            fogCoroutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < target.transitionDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / target.transitionDuration);

            RenderSettings.fogColor = Color.Lerp(startColor, target.fogColor, t);
            RenderSettings.fogDensity = Mathf.Lerp(startDensity, target.fogDensity, t);

            yield return null;
        }

        RenderSettings.fogColor = target.fogColor;
        RenderSettings.fogDensity = target.fogDensity;
        fogCoroutine = null;
    }
}
