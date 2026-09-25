using UnityEngine;

public class ScannableTarget : MonoBehaviour
{
    [Header("Story")]
    public MainStoryManager storyManager;

    [Header("Reconstruction")]
    public ReconstructionTarget reconstruction;

    [Header("Scan Settings")]
    public float requiredScanTime = 8f;

    [Header("Runtime Debug")]
    [SerializeField] private bool scanEnabled = false;
    [SerializeField] private bool completed = false;

    [SerializeField]
    [Range(0f, 1f)]
    private float scanProgress = 0f;

    private float currentScanTime = 0f;

    public float ScanProgress => scanProgress;

    public bool IsCompleted => completed;

    public bool CanScan =>
        scanEnabled && !completed;

    // =========================================================

    public void SetScanEnabled(bool enabled)
    {
        scanEnabled = enabled;

        Debug.Log(
            $"{name}: Scan Enabled = {scanEnabled}"
        );
    }

    // =========================================================

    public void AddScanTime(float amount)
    {
        if (!scanEnabled)
            return;

        if (completed)
            return;

        currentScanTime += amount;

        if (requiredScanTime <= 0f)
        {
            scanProgress = 1f;
        }
        else
        {
            scanProgress =
                Mathf.Clamp01(
                    currentScanTime /
                    requiredScanTime
                );
        }

        // ---------------------------------
        // 实时通知 Ghost
        // ---------------------------------

        if (reconstruction != null)
        {
            reconstruction.SetScanProgress(
                scanProgress
            );
        }

        // ---------------------------------

        if (scanProgress >= 1f)
        {
            CompleteScan();
        }
    }

    // =========================================================

    private void CompleteScan()
    {
        if (completed)
            return;

        completed = true;
        scanEnabled = false;
        scanProgress = 1f;

        Debug.Log(
            $"{name}: SCAN COMPLETE!"
        );

        if (reconstruction != null)
        {
            reconstruction.Restore();
        }

        if (storyManager != null)
        {
            storyManager.NotifyTargetCompleted(
                this
            );
        }
    }
}