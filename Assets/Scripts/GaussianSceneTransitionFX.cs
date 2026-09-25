using UnityEngine;

namespace GaussianSplatting.Runtime
{
    /// <summary>
    /// Per-object whole-scene Gaussian Splat transition VFX.
    ///
    /// Incoming:
    ///     far sparse spiral dust -> normal scene
    ///
    /// Outgoing:
    ///     normal scene -> far sparse spiral dust
    ///
    /// Put this component on every GaussianSplatRenderer object
    /// that participates in scene transition.
    /// </summary>
    [ExecuteAlways]
    public class GaussianSceneTransitionFX : MonoBehaviour
    {
        public enum TransitionMode
        {
            Incoming = 0,
            Outgoing = 1
        }

        [Header("Main")]
        public bool effectEnabled = true;

        [Range(0f, 1f)]
        [Tooltip("Incoming: 0 = far dust, 1 = normal. Outgoing: 0 = normal, 1 = far dust.")]
        public float progress = 1f;

        public TransitionMode mode = TransitionMode.Incoming;

        [Header("Spiral / Distance")]
        [Min(0f)]
        [Tooltip("How far points travel along the camera direction during transition.")]
        public float distance = 6f;

        [Range(0f, 12f)]
        [Tooltip("How many spiral turns the floating points perform.")]
        public float spiralTurns = 3.5f;

        [Min(0f)]
        [Tooltip("How much the dust cloud floats upward while transitioning.")]
        public float lift = 1.5f;

        [Header("Dust Point Cloud")]
        [Range(0f, 1f)]
        [Tooltip("Visible splat ratio in the far dust state.")]
        public float sparseDensity = 0.16f;

        [Range(0.001f, 1f)]
        [Tooltip("Splat size multiplier in the far dust state.")]
        public float dustPointScale = 0.035f;

        [Range(0f, 1f)]
        [Tooltip("Opacity multiplier in the far dust state.")]
        public float dustAlpha = 0.06f;

        [Header("Look")]
        [Range(0f, 1f)]
        [Tooltip("0 = neutral grayscale dust, 1 = cold blue-white dust.")]
        public float coldTint = 0.65f;

        [Range(0f, 0.75f)]
        [Tooltip("Random per-splat timing noise.")]
        public float perSplatDelayNoise = 0.25f;

        public void SetIncoming(float value)
        {
            mode = TransitionMode.Incoming;
            progress = Mathf.Clamp01(value);
            effectEnabled = true;
        }

        public void SetOutgoing(float value)
        {
            mode = TransitionMode.Outgoing;
            progress = Mathf.Clamp01(value);
            effectEnabled = true;
        }

        public void SetNormalVisible()
        {
            progress = 1f;
            mode = TransitionMode.Incoming;
            effectEnabled = false;
        }

        public void SetHiddenAsDust()
        {
            progress = 0f;
            mode = TransitionMode.Incoming;
            effectEnabled = true;
        }

        private void OnValidate()
        {
            progress = Mathf.Clamp01(progress);
            distance = Mathf.Max(0f, distance);
            lift = Mathf.Max(0f, lift);
            dustPointScale = Mathf.Max(0.001f, dustPointScale);
        }
    }
}