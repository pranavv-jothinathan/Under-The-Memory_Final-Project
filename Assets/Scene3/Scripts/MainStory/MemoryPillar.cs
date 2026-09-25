using UnityEngine;

public class MemoryPillar : MonoBehaviour
{
    [Header("Story")]
    public MainStoryManager storyManager;

    [Header("Graffiti")]
    public GameObject graffitiObject;

    [Header("Beam")]
    public LineRenderer beamLine;
    public Transform beamStart;
    public Transform beamTarget;

    [Header("Runtime Debug")]
    [SerializeField] private bool isAvailable = false;
    [SerializeField] private bool isActivated = false;
    [SerializeField] private bool isCompleted = false;

    public bool CanActivate =>
        isAvailable &&
        !isActivated &&
        !isCompleted;

    private void Awake()
    {
        if (beamLine != null)
            beamLine.enabled = false;
    }

    private void Update()
    {
        if (!isActivated || beamLine == null)
            return;

        if (beamStart == null || beamTarget == null)
            return;

        beamLine.SetPosition(0, beamStart.position);
        beamLine.SetPosition(1, beamTarget.position);
    }

    public void SetAvailable(bool available)
    {
        isAvailable = available;

        if (graffitiObject != null)
        {
            graffitiObject.SetActive(available);
        }

        if (!available && beamLine != null)
        {
            beamLine.enabled = false;
        }
    }

    public void TouchGraffiti()
    {
        if (!isAvailable)
            return;

        if (isActivated)
            return;

        if (isCompleted)
            return;

        ActivatePillar();
    }

    private void ActivatePillar()
    {
        isActivated = true;

        // 涂鸦熄灭
        if (graffitiObject != null)
        {
            graffitiObject.SetActive(false);
        }

        // 打开指向建筑的光束
        if (beamLine != null)
        {
            beamLine.enabled = true;
        }

        if (storyManager != null)
        {
            storyManager.NotifyPillarActivated(this);
        }

        Debug.Log($"{name}: Pillar activated.");
    }

    public void MarkCompleted()
    {
        isCompleted = true;
        isAvailable = false;

        if (graffitiObject != null)
        {
            graffitiObject.SetActive(false);
        }

        // 扫描完成以后，光束也关闭
        if (beamLine != null)
        {
            beamLine.enabled = false;
        }

        Debug.Log($"{name}: Pillar completed.");
    }
}