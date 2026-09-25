using UnityEngine;

public class ReconstructionTarget : MonoBehaviour
{
    [Header("Underwater Ghost")]
    public GameObject underwaterGhost;

    [Header("Surface Real Building")]
    public GameObject surfaceObject;

    [Header("Ghost Reveal")]
    [Tooltip("扫描进度达到多少时，Ghost才真正激活")]
    [Range(0f, 1f)]
    public float ghostStartProgress = 0.4f;

    [Tooltip("扫描完成后Ghost最终透明度")]
    [Range(0f, 1f)]
    public float finalGhostAlpha = 0.35f;

    private Renderer[] ghostRenderers;

    private bool restored = false;
    private bool ghostActivated = false;

    private MaterialPropertyBlock propertyBlock;

    private static readonly int BaseColorID =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorID =
        Shader.PropertyToID("_Color");

    // =========================================================
    // INITIALIZE
    // =========================================================

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        // -----------------------------------
        // Ghost
        // -----------------------------------

        if (underwaterGhost != null)
        {
            // 即使物体当前未激活，也获取里面所有Renderer
            ghostRenderers =
                underwaterGhost.GetComponentsInChildren<Renderer>(true);

            // 初始彻底关闭
            underwaterGhost.SetActive(false);

            ghostActivated = false;
        }

        // -----------------------------------
        // 水上真实建筑
        // -----------------------------------

        if (surfaceObject != null)
        {
            surfaceObject.SetActive(false);
        }
    }

    // =========================================================
    // SCANNING
    // =========================================================

    public void SetScanProgress(float progress)
    {
        if (restored)
            return;

        progress = Mathf.Clamp01(progress);

        // =====================================================
        // 0% ~ 40%
        // 完全不显示Ghost
        // =====================================================

        if (progress < ghostStartProgress)
        {
            return;
        }

        // =====================================================
        // 第一次达到40%
        // 激活Ghost
        // =====================================================

        if (!ghostActivated)
        {
            ActivateGhost();
        }

        // =====================================================
        // 40% ~ 100%
        // Alpha逐渐升高
        // =====================================================

        float revealProgress =
            Mathf.InverseLerp(
                ghostStartProgress,
                1f,
                progress
            );

        float alpha =
            Mathf.Lerp(
                0f,
                finalGhostAlpha,
                revealProgress
            );

        SetGhostAlpha(alpha);
    }

    // =========================================================
    // ACTIVATE GHOST
    // =========================================================

    private void ActivateGhost()
    {
        if (underwaterGhost == null)
            return;

        ghostActivated = true;

        // 先确保Alpha是0
        SetGhostAlpha(0f);

        // 再激活
        underwaterGhost.SetActive(true);

        Debug.Log(
            $"{name}: Underwater Ghost activated."
        );
    }

    // =========================================================
    // COMPLETE
    // =========================================================

    public void Restore()
    {
        if (restored)
            return;

        restored = true;

        // 如果因为某些原因100%时Ghost还没激活
        if (!ghostActivated)
        {
            ActivateGhost();
        }

        // Ghost保持最终透明度
        SetGhostAlpha(finalGhostAlpha);

        // 解锁水上真实建筑
        if (surfaceObject != null)
        {
            surfaceObject.SetActive(true);
        }

        Debug.Log(
            $"{name}: Reconstruction completed."
        );
    }

    // =========================================================
    // SET GHOST ALPHA
    // =========================================================

    private void SetGhostAlpha(float alpha)
    {
        if (ghostRenderers == null)
            return;

        foreach (Renderer renderer in ghostRenderers)
        {
            if (renderer == null)
                continue;

            Material[] sharedMaterials =
                renderer.sharedMaterials;

            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                Material material =
                    sharedMaterials[i];

                if (material == null)
                    continue;

                propertyBlock.Clear();

                renderer.GetPropertyBlock(
                    propertyBlock,
                    i
                );

                // URP / Shader Graph
                if (material.HasProperty(BaseColorID))
                {
                    Color color =
                        material.GetColor(BaseColorID);

                    color.a = alpha;

                    propertyBlock.SetColor(
                        BaseColorID,
                        color
                    );
                }

                // Standard / 兼容其他Shader
                else if (material.HasProperty(ColorID))
                {
                    Color color =
                        material.GetColor(ColorID);

                    color.a = alpha;

                    propertyBlock.SetColor(
                        ColorID,
                        color
                    );
                }

                renderer.SetPropertyBlock(
                    propertyBlock,
                    i
                );
            }
        }
    }
}