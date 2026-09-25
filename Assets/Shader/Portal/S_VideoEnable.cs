using UnityEngine;
using UnityEngine.Video;

public class EnableOnTrigger : MonoBehaviour
{
    [Header("VideoPlayer")]
    public GameObject objectC; // 360VideoPlayer

    [Header("Video Clip")]
    public VideoClip videoClipForThisObject;

    [Tooltip("Player 离开触发器时是否关闭视频物体")]
    public bool disableOnExit = true;

    private MeshRenderer objectBRenderer; // 当前触发器物体自身的 MeshRenderer
    private VideoPlayer videoPlayer;

    private void Awake()
    {
        if (objectC != null)
        {
            // 即使 VideoPlayer 位于禁用的子物体中也能找到
            videoPlayer = objectC.GetComponentInChildren<VideoPlayer>(true);
        }

        objectBRenderer = GetComponent<MeshRenderer>();

        Debug.Log(
            $"[EnableOnTrigger] Awake - VideoObject = {objectC}, VideoClip = {videoClipForThisObject}",
            this
        );
    }

    private void Reset()
    {
        Collider col = GetComponent<Collider>();

        if (col != null)
        {
            col.isTrigger = true;
        }
        else
        {
            Debug.LogWarning("[EnableOnTrigger] Reset - 当前物体没有 Collider。", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 只要进入的 Collider 本身带有 Player Tag 即可
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (objectC == null)
        {
            Debug.LogWarning("[EnableOnTrigger] objectC 未指定。", this);
            return;
        }

        Debug.Log($"[EnableOnTrigger] Player entered: {other.name}", this);

        // 先启用，再播放
        objectC.SetActive(true);

        if (videoPlayer != null)
        {
            if (videoClipForThisObject != null)
            {
                videoPlayer.clip = videoClipForThisObject;
            }

            videoPlayer.Play();
        }
        else
        {
            Debug.LogWarning("[EnableOnTrigger] 找不到 objectC 上的 VideoPlayer。", this);
        }

        // 隐藏当前触发器物体自身的网格
        if (objectBRenderer != null)
        {
            objectBRenderer.enabled = false;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        Debug.Log($"[EnableOnTrigger] Player exited: {other.name}", this);

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        if (disableOnExit && objectC != null)
        {
            objectC.SetActive(false);
        }

        if (objectBRenderer != null)
        {
            objectBRenderer.enabled = true;
        }
    }
}
