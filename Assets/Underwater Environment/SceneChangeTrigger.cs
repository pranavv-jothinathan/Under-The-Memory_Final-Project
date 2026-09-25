using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneChangeTrigger : MonoBehaviour
{
    [Header("场景设置")]
    [SerializeField] private string nextSceneName = "EndingScene";

    [Header("渐黑设置")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float blackScreenDuration = 0.3f;

    private bool isLoading;

    private void Start()
    {
        // 游戏开始时保证遮罩是透明的
        if (fadeImage != null)
        {
            Color color = fadeImage.color;
            color.a = 0f;
            fadeImage.color = color;

            // 避免透明图片挡住鼠标或UI操作
            fadeImage.raycastTarget = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isLoading)
            return;

        bool isPlayer =
            other.CompareTag("Player");

        if (!isPlayer)
            return;

        isLoading = true;
        StartCoroutine(FadeAndLoadScene());
    }

    private IEnumerator FadeAndLoadScene()
    {
        if (fadeImage == null)
        {
            Debug.LogError("没有把 FadeImage 拖进 SceneChangeTrigger 的 Fade Image 位置。");
            isLoading = false;
            yield break;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("没有填写下一个场景名称。");
            isLoading = false;
            yield break;
        }

        // 从透明逐渐变成黑色
        float elapsedTime = 0f;
        Color color = fadeImage.color;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;

            float alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
            color.a = alpha;
            fadeImage.color = color;

            yield return null;
        }

        color.a = 1f;
        fadeImage.color = color;

        // 保持短暂黑屏
        yield return new WaitForSeconds(blackScreenDuration);

        // 加载下一个场景
        LoadScenes(nextSceneName);
    }

    public void LoadScenes(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

}