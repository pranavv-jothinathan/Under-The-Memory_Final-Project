using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// EndingScene: when the ending clip on this VideoPlayer finishes, hide the
/// video quads and show 3DGS_Models. Does not touch BGM AudioSources.
/// </summary>
public class EndingVideoFinale : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private GameObject[] quadsToHide;
    [SerializeField] private GameObject gaussianRoot;
    [SerializeField] private Material daytimeSkybox;

    private bool finished;

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (gaussianRoot == null)
            gaussianRoot = FindNamed("3DGS_Models", true);

        if (quadsToHide == null || quadsToHide.Length == 0)
        {
            quadsToHide = new[]
            {
                gameObject,
                FindNamed("video (1)", false),
                FindNamed("Quad", false)
            };
        }
    }

    private void OnEnable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached += OnVideoEnded;
    }

    private void OnDisable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoEnded;
    }

    private void OnVideoEnded(VideoPlayer source)
    {
        if (finished)
            return;

        finished = true;

        if (quadsToHide != null)
        {
            for (int i = 0; i < quadsToHide.Length; i++)
            {
                if (quadsToHide[i] != null)
                    quadsToHide[i].SetActive(false);
            }
        }

        HideNamed("video (1)");
        HideNamed("Video (1)");

        if (gaussianRoot != null)
            gaussianRoot.SetActive(true);

        if (daytimeSkybox != null)
            RenderSettings.skybox = daytimeSkybox;
    }

    private static void HideNamed(string name)
    {
        GameObject found = FindNamed(name, false);
        if (found != null)
            found.SetActive(false);
    }

    private static GameObject FindNamed(string name, bool includeInactive)
    {
        GameObject[] all = FindObjectsByType<GameObject>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name)
                return all[i];
        }

        return null;
    }
}
