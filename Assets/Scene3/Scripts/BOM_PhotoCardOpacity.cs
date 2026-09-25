using UnityEngine;

namespace BOM.Reconstruction
{
    /// <summary>
    /// Controls the opacity of the physical reconstruction photo.
    ///
    /// The photo starts opaque. Once reconstruction begins, it becomes
    /// partially transparent so the player can see the building behind it.
    ///
    /// The script uses MaterialPropertyBlock so it does not permanently
    /// modify or duplicate the shared material asset.
    /// </summary>
    public sealed class BOM_PhotoCardOpacity : MonoBehaviour
    {
        [Header("Photo Renderers")]
        [SerializeField]
        private Renderer[] targetRenderers;

        [Header("Opacity")]
        [Range(0f, 1f)]
        [SerializeField]
        private float normalAlpha = 1f;

        [Range(0f, 1f)]
        [SerializeField]
        private float reconstructionAlpha = 0.28f;

        [Tooltip("How quickly opacity follows the requested value.")]
        [Min(0.01f)]
        [SerializeField]
        private float fadeSpeed = 5f;

        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");

        private static readonly int ColorId =
            Shader.PropertyToID("_Color");

        private MaterialPropertyBlock propertyBlock;

        private float currentAlpha;
        private float targetAlpha;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers =
                    GetComponentsInChildren<Renderer>(true);
            }

            currentAlpha = normalAlpha;
            targetAlpha = normalAlpha;

            ApplyAlpha(currentAlpha);
        }

        private void Update()
        {
            currentAlpha = Mathf.MoveTowards(
                currentAlpha,
                targetAlpha,
                fadeSpeed * Time.deltaTime
            );

            ApplyAlpha(currentAlpha);
        }

        /// <summary>
        /// progress01:
        /// 0 = normal opaque photo
        /// 1 = reconstruction opacity
        /// </summary>
        public void SetReconstructionAmount(float progress01)
        {
            progress01 = Mathf.Clamp01(progress01);

            targetAlpha = Mathf.Lerp(
                normalAlpha,
                reconstructionAlpha,
                progress01
            );
        }

        public void SetImmediateAlpha(float alpha)
        {
            currentAlpha = Mathf.Clamp01(alpha);
            targetAlpha = currentAlpha;
            ApplyAlpha(currentAlpha);
        }

        public void ResetOpacity()
        {
            targetAlpha = normalAlpha;
        }

        private void ApplyAlpha(float alpha)
        {
            if (targetRenderers == null)
            {
                return;
            }

            foreach (Renderer targetRenderer in targetRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                Material[] materials =
                    targetRenderer.sharedMaterials;

                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    Material material = materials[materialIndex];

                    if (material == null)
                    {
                        continue;
                    }

                    targetRenderer.GetPropertyBlock(
                        propertyBlock,
                        materialIndex
                    );

                    if (material.HasProperty(BaseColorId))
                    {
                        Color color =
                            material.GetColor(BaseColorId);

                        color.a = alpha;

                        propertyBlock.SetColor(
                            BaseColorId,
                            color
                        );
                    }
                    else if (material.HasProperty(ColorId))
                    {
                        Color color =
                            material.GetColor(ColorId);

                        color.a = alpha;

                        propertyBlock.SetColor(
                            ColorId,
                            color
                        );
                    }

                    targetRenderer.SetPropertyBlock(
                        propertyBlock,
                        materialIndex
                    );
                }
            }
        }
    }
}