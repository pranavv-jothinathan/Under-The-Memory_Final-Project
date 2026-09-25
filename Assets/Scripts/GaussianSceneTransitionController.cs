using UnityEngine;
using GaussianSplatting.Runtime;

/// <summary>
/// Controls whole-scene Gaussian Splat transition.
///
/// For your current scene:
///     From Scene: House1_Normal / House1
///     To Scene:   GuangChang
///
/// Press T:
///     House scene dissolves into spiral floating dust,
///     GuangChang flies back from far dust and restores.
///
/// Press Y:
///     Reverse transition.
/// </summary>
public class GaussianSceneTransitionController : MonoBehaviour
{
    [Header("Scene Root Objects")]
    [Tooltip("Old scene root. For example: House1_Normal or House1.")]
    public GameObject fromSceneRoot;

    [Tooltip("New scene root. For example: GuangChang.")]
    public GameObject toSceneRoot;

    [Header("Manual FX Targets")]
    [Tooltip("Optional. If empty, this script will auto collect GaussianSceneTransitionFX under From Scene Root.")]
    public GaussianSceneTransitionFX[] fromScene;

    [Tooltip("Optional. If empty, this script will auto collect GaussianSceneTransitionFX under To Scene Root.")]
    public GaussianSceneTransitionFX[] toScene;

    [Header("Input")]
    public KeyCode transitionKey = KeyCode.T;
    public KeyCode reverseKey = KeyCode.Y;

    [Header("Timing")]
    [Min(0.01f)]
    public float duration = 5f;

    [Range(0f, 1f)]
    [Tooltip("0 = simultaneous. 0.18 means new scene starts restoring after old scene has dissolved a bit.")]
    public float incomingDelay = 0.18f;

    [Range(0f, 1f)]
    [Tooltip("Higher value makes incoming scene restore more slowly and softly.")]
    public float incomingSoftness = 0.12f;

    [Header("Auto")]
    public bool playOnStart = false;

    [Header("Visibility")]
    [Tooltip("If true, the new scene object is disabled before transition starts.")]
    public bool disableToSceneBeforeStart = true;

    [Tooltip("If true, the old scene object is disabled after forward transition finishes.")]
    public bool disableFromSceneWhenFinished = true;

    [Header("Debug / Editor Preview")]
    [Range(0f, 1f)]
    public float previewProgress = 0f;

    public bool usePreviewProgressInEditMode = false;
    public bool printProgress = false;

    private bool isPlayingTransition;
    private bool reverse;
    private float timer;

    private void Awake()
    {
        RefreshTargets();
    }

    private void Start()
    {
        RefreshTargets();

        if (disableToSceneBeforeStart && toSceneRoot != null)
        {
            toSceneRoot.SetActive(false);
        }

        SetNormal(fromScene);
        SetHiddenDust(toScene);

        if (playOnStart)
        {
            BeginForward();
        }
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            if (Input.GetKeyDown(transitionKey))
            {
                BeginForward();
            }

            if (Input.GetKeyDown(reverseKey))
            {
                BeginReverse();
            }

            if (isPlayingTransition)
            {
                timer += Time.deltaTime;

                float raw = Mathf.Clamp01(timer / Mathf.Max(duration, 0.01f));
                float t = reverse ? 1f - raw : raw;

                ApplyTransition(t);

                if (raw >= 1f)
                {
                    FinishTransition();
                }
            }
        }
        else
        {
            if (usePreviewProgressInEditMode)
            {
                RefreshTargets();
                ApplyTransition(previewProgress);
            }
        }
    }

    [ContextMenu("Refresh Targets")]
    public void RefreshTargets()
    {
        if ((fromScene == null || fromScene.Length == 0) && fromSceneRoot != null)
        {
            fromScene = fromSceneRoot.GetComponentsInChildren<GaussianSceneTransitionFX>(true);
        }

        if ((toScene == null || toScene.Length == 0) && toSceneRoot != null)
        {
            toScene = toSceneRoot.GetComponentsInChildren<GaussianSceneTransitionFX>(true);
        }
    }

    [ContextMenu("Begin Forward")]
    public void BeginForward()
    {
        RefreshTargets();

        reverse = false;
        timer = 0f;
        isPlayingTransition = true;

        SetSceneActive(fromSceneRoot, true);
        SetSceneActive(toSceneRoot, true);

        ApplyTransition(0f);
    }

    [ContextMenu("Begin Reverse")]
    public void BeginReverse()
    {
        RefreshTargets();

        reverse = true;
        timer = 0f;
        isPlayingTransition = true;

        SetSceneActive(fromSceneRoot, true);
        SetSceneActive(toSceneRoot, true);

        ApplyTransition(1f);
    }

    [ContextMenu("Preview Start")]
    public void PreviewStart()
    {
        RefreshTargets();
        previewProgress = 0f;
        ApplyTransition(previewProgress);
    }

    [ContextMenu("Preview Middle")]
    public void PreviewMiddle()
    {
        RefreshTargets();
        previewProgress = 0.5f;
        ApplyTransition(previewProgress);
    }

    [ContextMenu("Preview End")]
    public void PreviewEnd()
    {
        RefreshTargets();
        previewProgress = 1f;
        ApplyTransition(previewProgress);
    }

    private void ApplyTransition(float t)
    {
        t = Mathf.Clamp01(t);

        float oldOut = Smooth01(t);

        float denom = Mathf.Max(0.0001f, 1f - incomingDelay + incomingSoftness);
        float newIn = Mathf.Clamp01((t - incomingDelay) / denom);
        newIn = Smooth01(newIn);

        if (fromScene != null)
        {
            foreach (GaussianSceneTransitionFX fx in fromScene)
            {
                if (fx == null) continue;
                fx.SetOutgoing(oldOut);
            }
        }

        if (toScene != null)
        {
            foreach (GaussianSceneTransitionFX fx in toScene)
            {
                if (fx == null) continue;
                fx.SetIncoming(newIn);
            }
        }

        if (printProgress)
        {
            Debug.Log($"Scene Transition t={t:0.00}, outgoing={oldOut:0.00}, incoming={newIn:0.00}");
        }
    }

    private void FinishTransition()
    {
        isPlayingTransition = false;

        if (!reverse)
        {
            SetHiddenDust(fromScene);
            SetNormal(toScene);

            if (disableFromSceneWhenFinished)
            {
                SetSceneActive(fromSceneRoot, false);
            }

            SetSceneActive(toSceneRoot, true);
        }
        else
        {
            SetNormal(fromScene);
            SetHiddenDust(toScene);

            SetSceneActive(fromSceneRoot, true);

            if (disableFromSceneWhenFinished)
            {
                SetSceneActive(toSceneRoot, false);
            }
        }
    }

    private static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    private static void SetSceneActive(GameObject root, bool active)
    {
        if (root != null)
        {
            root.SetActive(active);
        }
    }

    private static void SetNormal(GaussianSceneTransitionFX[] targets)
    {
        if (targets == null) return;

        foreach (GaussianSceneTransitionFX fx in targets)
        {
            if (fx == null) continue;
            fx.SetNormalVisible();
        }
    }

    private static void SetHiddenDust(GaussianSceneTransitionFX[] targets)
    {
        if (targets == null) return;

        foreach (GaussianSceneTransitionFX fx in targets)
        {
            if (fx == null) continue;
            fx.SetHiddenAsDust();
        }
    }
}