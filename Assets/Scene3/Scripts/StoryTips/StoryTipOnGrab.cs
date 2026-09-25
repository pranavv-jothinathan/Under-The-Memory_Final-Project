using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 抓起这个可交互物体时弹一条提示。
///
/// 用代码订阅 selectEntered，不往 Inspector 里那份 m_SelectEntered 列表里加东西 ——
/// 水下箱子里那片 SurfaceLens 是组员的物件，它的事件列表已经接了
/// [SYS] FilterSystem.SetActive(true)，不要去动。
/// </summary>
public class StoryTipOnGrab : MonoBehaviour
{
    [SerializeField] private StoryTipId tip = StoryTipId.LensReadyPressY;

    [Tooltip("留空就取本物体上的 Interactable。")]
    [SerializeField] private XRBaseInteractable interactable;

    [Tooltip("勾上表示只触发一次。")]
    [SerializeField] private bool once = true;

    private bool fired;

    private void Awake()
    {
        if (interactable == null)
            interactable = GetComponent<XRBaseInteractable>();

        if (interactable == null)
        {
            Debug.LogWarning(
                $"StoryTipOnGrab: '{name}' 上没有找到 XRBaseInteractable，提示不会触发。",
                this);
        }
    }

    private void OnEnable()
    {
        if (interactable != null)
            interactable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (fired && once)
            return;

        fired = true;
        StoryTipsPresenter.Request(tip);
    }
}
