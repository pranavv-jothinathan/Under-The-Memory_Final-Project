using UnityEngine;

namespace BOM.Reconstruction
{
    /// <summary>
    /// Drives the reconstruction reveal material and switches to the
    /// original final renderers when reconstruction is complete.
    ///
    /// Reveal renderers use SH_FX_PhotoReveal.
    /// Final renderers use the original building materials.
    /// </summary>
    public sealed class BOM_MeshRevealDriver : MonoBehaviour
    {
        [Header("Reconstruction Renderers")]
        [SerializeField]
        private Renderer[] revealRenderers;

        [Header("Final Building Renderers")]
        [SerializeField]
        private Renderer[] finalRenderers;

        [Header("Completion")]
        [Range(0.9f, 1f)]
        [SerializeField]
        private float finalSwitchThreshold = 0.999f;

        private static readonly int RevealId =
            Shader.PropertyToID("_Reveal");

        private MaterialPropertyBlock propertyBlock;

        private float currentReveal;
        private bool finalStateEnabled;

        private void Awake()
        {
            propertyBlock =
                new MaterialPropertyBlock();

            SetReveal01(0f);
        }

        public void SetReveal01(float reveal01)
        {
            reveal01 = Mathf.Clamp01(reveal01);
            currentReveal = reveal01;

            bool shouldShowFinal =
                reveal01 >= finalSwitchThreshold;

            if (shouldShowFinal)
            {
                EnableFinalState();
            }
            else
            {
                EnableReconstructionState();
                ApplyRevealToRenderers(reveal01);
            }
        }

        public float GetReveal01()
        {
            return currentReveal;
        }

        public void ResetReveal()
        {
            finalStateEnabled = false;
            SetRendererArrayEnabled(
                finalRenderers,
                false
            );

            SetRendererArrayEnabled(
                revealRenderers,
                true
            );

            ApplyRevealToRenderers(0f);
            currentReveal = 0f;
        }

        private void EnableFinalState()
        {
            if (finalStateEnabled)
            {
                return;
            }

            finalStateEnabled = true;

            SetRendererArrayEnabled(
                revealRenderers,
                false
            );

            SetRendererArrayEnabled(
                finalRenderers,
                true
            );
        }

        private void EnableReconstructionState()
        {
            if (!finalStateEnabled)
            {
                SetRendererArrayEnabled(
                    finalRenderers,
                    false
                );

                SetRendererArrayEnabled(
                    revealRenderers,
                    true
                );

                return;
            }

            finalStateEnabled = false;

            SetRendererArrayEnabled(
                finalRenderers,
                false
            );

            SetRendererArrayEnabled(
                revealRenderers,
                true
            );
        }

        private void ApplyRevealToRenderers(
            float reveal01
        )
        {
            if (revealRenderers == null)
            {
                return;
            }

            foreach (Renderer revealRenderer
                     in revealRenderers)
            {
                if (revealRenderer == null)
                {
                    continue;
                }

                revealRenderer.GetPropertyBlock(
                    propertyBlock
                );

                propertyBlock.SetFloat(
                    RevealId,
                    reveal01
                );

                revealRenderer.SetPropertyBlock(
                    propertyBlock
                );
            }
        }

        private static void SetRendererArrayEnabled(
            Renderer[] renderers,
            bool value
        )
        {
            if (renderers == null)
            {
                return;
            }

            foreach (Renderer targetRenderer
                     in renderers)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.enabled =
                        value;
                }
            }
        }
    }
}