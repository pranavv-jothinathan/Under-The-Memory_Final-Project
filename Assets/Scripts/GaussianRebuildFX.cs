using UnityEngine;

namespace GaussianSplatting.Runtime
{
    /// <summary>
    /// Per-object Gaussian Splat rebuild VFX settings.
    ///
    /// Put this script on the Gaussian Splat object that should rebuild,
    /// for example: House1_Normal.
    ///
    /// Do not put it on GroundVolume or other Gaussian Splat objects
    /// if you want only the house to be affected.
    /// </summary>
    [ExecuteAlways]
    public class GaussianRebuildFX : MonoBehaviour
    {
        [Header("Main")]
        public bool effectEnabled = true;

        [Range(0f, 1f)]
        [Tooltip("0 = scattered sparse ghost points, 0.5 = gathered sparse ghost house, 1 = fully rebuilt normal house.")]
        public float progress = 0f;

        [Header("Auto Play")]
        [Tooltip("Turn this off if you want to control progress with RebuildInputTest.")]
        public bool playOnStart = false;

        [Min(0.01f)]
        public float duration = 4f;

        [Header("Height Range")]
        [Tooltip("If enabled, calculates minY and maxY from child renderers. If it does not work for your GS object, disable this and set Min Y / Max Y manually.")]
        public bool autoBoundsFromRenderers = true;

        [Tooltip("Bottom height in local space.")]
        public float minY = -1f;

        [Tooltip("Top height in local space.")]
        public float maxY = 1f;

        [Header("Ghost Point Cloud")]
        [Range(0f, 1f)]
        [Tooltip("Visible splat ratio in ghost state. Lower means sparser.")]
        public float sparseDensity = 0.18f;

        [Range(0.001f, 1f)]
        [Tooltip("Ghost splat size multiplier. Smaller means more point-cloud-like.")]
        public float ghostPointScale = 0.045f;

        [Range(0f, 2f)]
        [Tooltip("Ghost opacity multiplier.")]
        public float ghostAlpha = 0.45f;

        [Min(0f)]
        [Tooltip("How far ghost points scatter away from their original position.")]
        public float scatter = 1.25f;

        [Header("Rebuild Edge")]
        [Range(0.001f, 0.5f)]
        [Tooltip("Softness of the bottom-up color/density restoration band.")]
        public float rebuildBand = 0.08f;

        [Range(0f, 0.5f)]
        [Tooltip("Random delay per splat, making the rebuild less mechanical.")]
        public float perSplatDelayNoise = 0.12f;

        private float timer;

        private void OnEnable()
        {
            CalculateBoundsIfNeeded();
        }

        private void Start()
        {
            if (playOnStart)
            {
                timer = 0f;
                progress = 0f;
            }
        }

        private void Update()
        {
            if (playOnStart && Application.isPlaying)
            {
                timer += Time.deltaTime;
                progress = Mathf.Clamp01(timer / Mathf.Max(duration, 0.01f));
            }

            if (autoBoundsFromRenderers)
            {
                CalculateBoundsIfNeeded();
            }
        }

        [ContextMenu("Calculate Bounds Now")]
        public void CalculateBoundsIfNeeded()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            bool hasBounds = false;
            Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);
            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;

            foreach (Renderer r in renderers)
            {
                if (r == null)
                {
                    continue;
                }

                Bounds wb = r.bounds;
                Vector3 c = wb.center;
                Vector3 e = wb.extents;

                Vector3[] corners =
                {
                    c + new Vector3(-e.x, -e.y, -e.z),
                    c + new Vector3( e.x, -e.y, -e.z),
                    c + new Vector3(-e.x,  e.y, -e.z),
                    c + new Vector3( e.x,  e.y, -e.z),

                    c + new Vector3(-e.x, -e.y,  e.z),
                    c + new Vector3( e.x, -e.y,  e.z),
                    c + new Vector3(-e.x,  e.y,  e.z),
                    c + new Vector3( e.x,  e.y,  e.z)
                };

                foreach (Vector3 corner in corners)
                {
                    Vector3 lp = worldToLocal.MultiplyPoint3x4(corner);

                    if (!hasBounds)
                    {
                        localBounds = new Bounds(lp, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(lp);
                    }
                }
            }

            if (hasBounds)
            {
                minY = localBounds.min.y;
                maxY = localBounds.max.y;
            }
        }

        [ContextMenu("Play Rebuild")]
        public void Play()
        {
            playOnStart = true;
            timer = 0f;
            progress = 0f;
        }

        [ContextMenu("Stop Auto Play")]
        public void StopAutoPlay()
        {
            playOnStart = false;
        }

        [ContextMenu("Reset Rebuild")]
        public void ResetRebuild()
        {
            playOnStart = false;
            timer = 0f;
            progress = 0f;
        }

        [ContextMenu("Complete Rebuild")]
        public void CompleteRebuild()
        {
            playOnStart = false;
            timer = duration;
            progress = 1f;
        }
    }
}