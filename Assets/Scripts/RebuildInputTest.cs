using UnityEngine;
using GaussianSplatting.Runtime;

/// <summary>
/// Hold E to rebuild.
/// Hold Q to dissolve.
/// Release both keys to keep the current state.
/// </summary>
public class RebuildInputTest : MonoBehaviour
{
    [Header("Target")]
    public GaussianRebuildFX rebuildFX;

    [Header("Input")]
    public KeyCode rebuildKey = KeyCode.E;
    public KeyCode dissolveKey = KeyCode.Q;

    [Header("Speed")]
    [Min(0f)]
    public float rebuildSpeed = 0.25f;

    [Min(0f)]
    public float dissolveSpeed = 0.25f;

    [Header("Debug")]
    public bool printProgress = false;

    private void Update()
    {
        if (rebuildFX == null)
        {
            return;
        }

        // 用按键控制时，关闭自动播放。
        rebuildFX.playOnStart = false;

        bool rebuildPressed = Input.GetKey(rebuildKey);
        bool dissolvePressed = Input.GetKey(dissolveKey);

        if (rebuildPressed && !dissolvePressed)
        {
            rebuildFX.progress += Time.deltaTime * rebuildSpeed;
        }
        else if (dissolvePressed && !rebuildPressed)
        {
            rebuildFX.progress -= Time.deltaTime * dissolveSpeed;
        }

        rebuildFX.progress = Mathf.Clamp01(rebuildFX.progress);

        if (printProgress)
        {
            Debug.Log($"Rebuild Progress: {rebuildFX.progress:0.00}");
        }
    }
}