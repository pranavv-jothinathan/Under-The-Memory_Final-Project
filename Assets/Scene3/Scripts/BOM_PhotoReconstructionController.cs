using UnityEngine;

namespace BOM.Reconstruction
{
    /// <summary>
    /// Main controller for photo-based reconstruction.
    ///
    /// Workflow:
    /// 1. Player moves the photo to the target pose.
    /// 2. Position and rotation are checked continuously.
    /// 3. While aligned, reconstruction progress increases.
    /// 4. If the photo moves away, progress pauses.
    /// 5. Surface points appear first.
    /// 6. The reconstruction mesh progressively fills.
    /// 7. At completion, the original building renderers appear.
    /// </summary>
    public sealed class
        BOM_PhotoReconstructionController : MonoBehaviour
    {
        [Header("Photo Alignment")]
        [Tooltip(
            "Child transform on the photo representing " +
            "the intended alignment pose."
        )]
        [SerializeField]
        private Transform photoAlignmentReference;

        [Tooltip(
            "Target pose that the photo must match."
        )]
        [SerializeField]
        private Transform targetPose;

        [Min(0.01f)]
        [Tooltip(
            "Maximum allowed distance between the photo " +
            "reference and target pose, in metres."
        )]
        [SerializeField]
        private float maximumPositionDistance = 0.14f;

        [Range(1f, 90f)]
        [Tooltip(
            "Maximum allowed angular difference in degrees."
        )]
        [SerializeField]
        private float maximumRotationAngle = 15f;

        [Tooltip(
            "Require the photo to remain aligned before " +
            "progress begins."
        )]
        [Min(0f)]
        [SerializeField]
        private float alignmentHoldTime = 0.15f;

        [Header("Reconstruction Timing")]
        [Min(0.1f)]
        [SerializeField]
        private float reconstructionDuration = 8f;

        [Tooltip(
            "Point cloud reaches maximum density at this " +
            "overall progress value."
        )]
        [Range(0.05f, 0.95f)]
        [SerializeField]
        private float pointCloudFullProgress = 0.48f;

        [Tooltip(
            "Point cloud begins fading away at this progress."
        )]
        [Range(0.05f, 1f)]
        [SerializeField]
        private float pointCloudFadeStart = 0.72f;

        [Tooltip(
            "Mesh material begins appearing at this progress."
        )]
        [Range(0f, 0.9f)]
        [SerializeField]
        private float meshRevealStartProgress = 0.22f;

        [Header("Photo Opacity Timing")]
        [Tooltip(
            "How much overall reconstruction progress is " +
            "needed before the photo reaches its lower opacity."
        )]
        [Range(0.001f, 0.5f)]
        [SerializeField]
        private float photoFadeProgressRange = 0.08f;

        [Header("System References")]
        [SerializeField]
        private BOM_SurfacePointCloud surfacePointCloud;

        [SerializeField]
        private BOM_MeshRevealDriver meshRevealDriver;

        [SerializeField]
        private BOM_PhotoCardOpacity photoCardOpacity;

        [Header("Optional Feedback")]
        [Tooltip(
            "Optional object enabled while the photo is aligned."
        )]
        [SerializeField]
        private GameObject alignmentFeedback;

        [Tooltip(
            "Optional object enabled after reconstruction completes."
        )]
        [SerializeField]
        private GameObject completionFeedback;

        [Header("Debug")]
        [Tooltip(
            "Hold this key in the Editor to simulate alignment."
        )]
        [SerializeField]
        private KeyCode debugAlignmentKey = KeyCode.G;

        [Tooltip(
            "Press this key to reset the reconstruction."
        )]
        [SerializeField]
        private KeyCode debugResetKey = KeyCode.R;

        [Range(0f, 1f)]
        [SerializeField]
        private float reconstructionProgress;

        [SerializeField]
        private bool isCurrentlyAligned;

        [SerializeField]
        private float currentPositionDistance;

        [SerializeField]
        private float currentRotationAngle;

        private float currentAlignmentHoldTime;
        private bool hasCompleted;

        public float ReconstructionProgress =>
            reconstructionProgress;

        public bool IsCurrentlyAligned =>
            isCurrentlyAligned;

        public bool HasCompleted =>
            hasCompleted;

        private void Start()
        {
            ResetReconstruction();
        }

        private void Update()
        {
            if (Input.GetKeyDown(debugResetKey))
            {
                ResetReconstruction();
            }

            EvaluateAlignment();

            if (!hasCompleted)
            {
                UpdateReconstructionProgress();
            }

            UpdateVisuals();
        }

        private void EvaluateAlignment()
        {
            bool hasRequiredReferences =
                photoAlignmentReference != null &&
                targetPose != null;

            if (!hasRequiredReferences)
            {
                isCurrentlyAligned = false;
                currentPositionDistance =
                    float.PositiveInfinity;

                currentRotationAngle = 180f;
                return;
            }

            currentPositionDistance =
                Vector3.Distance(
                    photoAlignmentReference.position,
                    targetPose.position
                );

            currentRotationAngle =
                Quaternion.Angle(
                    photoAlignmentReference.rotation,
                    targetPose.rotation
                );

            bool positionMatches =
                currentPositionDistance <=
                maximumPositionDistance;

            bool rotationMatches =
                currentRotationAngle <=
                maximumRotationAngle;

            bool debugAligned =
                Input.GetKey(debugAlignmentKey);

            isCurrentlyAligned =
                debugAligned ||
                (positionMatches && rotationMatches);

            if (alignmentFeedback != null)
            {
                alignmentFeedback.SetActive(
                    isCurrentlyAligned &&
                    !hasCompleted
                );
            }
        }

        private void UpdateReconstructionProgress()
        {
            if (!isCurrentlyAligned)
            {
                currentAlignmentHoldTime = 0f;
                return;
            }

            currentAlignmentHoldTime +=
                Time.deltaTime;

            if (currentAlignmentHoldTime <
                alignmentHoldTime)
            {
                return;
            }

            reconstructionProgress +=
                Time.deltaTime /
                reconstructionDuration;

            reconstructionProgress =
                Mathf.Clamp01(
                    reconstructionProgress
                );

            if (reconstructionProgress >= 1f)
            {
                CompleteReconstruction();
            }
        }

        private void UpdateVisuals()
        {
            UpdatePhotoOpacity();
            UpdatePointCloud();
            UpdateMeshReveal();
        }

        private void UpdatePhotoOpacity()
        {
            if (photoCardOpacity == null)
            {
                return;
            }

            float fadeAmount =
                Mathf.InverseLerp(
                    0f,
                    photoFadeProgressRange,
                    reconstructionProgress
                );

            fadeAmount =
                Smooth01(fadeAmount);

            photoCardOpacity.SetReconstructionAmount(
                fadeAmount
            );
        }

        private void UpdatePointCloud()
        {
            if (surfacePointCloud == null)
            {
                return;
            }

            float pointVisibility;

            if (reconstructionProgress <=
                pointCloudFullProgress)
            {
                pointVisibility =
                    Mathf.InverseLerp(
                        0f,
                        pointCloudFullProgress,
                        reconstructionProgress
                    );

                pointVisibility =
                    Smooth01(pointVisibility);
            }
            else
            {
                pointVisibility =
                    1f -
                    Mathf.InverseLerp(
                        pointCloudFadeStart,
                        1f,
                        reconstructionProgress
                    );

                pointVisibility =
                    Smooth01(pointVisibility);
            }

            surfacePointCloud.SetVisibility01(
                Mathf.Clamp01(pointVisibility)
            );
        }

        private void UpdateMeshReveal()
        {
            if (meshRevealDriver == null)
            {
                return;
            }

            float meshReveal =
                Mathf.InverseLerp(
                    meshRevealStartProgress,
                    1f,
                    reconstructionProgress
                );

            meshReveal =
                Smooth01(meshReveal);

            meshRevealDriver.SetReveal01(
                meshReveal
            );
        }

        private void CompleteReconstruction()
        {
            reconstructionProgress = 1f;
            hasCompleted = true;

            if (completionFeedback != null)
            {
                completionFeedback.SetActive(true);
            }
        }

        [ContextMenu("Reset Reconstruction")]
        public void ResetReconstruction()
        {
            reconstructionProgress = 0f;
            currentAlignmentHoldTime = 0f;
            isCurrentlyAligned = false;
            hasCompleted = false;

            if (meshRevealDriver != null)
            {
                meshRevealDriver.ResetReveal();
            }

            if (surfacePointCloud != null)
            {
                surfacePointCloud.SetVisibility01(
                    0f,
                    true
                );
            }

            if (photoCardOpacity != null)
            {
                photoCardOpacity.SetImmediateAlpha(
                    1f
                );
            }

            if (alignmentFeedback != null)
            {
                alignmentFeedback.SetActive(false);
            }

            if (completionFeedback != null)
            {
                completionFeedback.SetActive(false);
            }
        }

        public void SetProgressForDebug(
            float progress01
        )
        {
            reconstructionProgress =
                Mathf.Clamp01(progress01);

            hasCompleted =
                reconstructionProgress >= 1f;

            UpdateVisuals();
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);

            return value *
                   value *
                   (3f - 2f * value);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (targetPose == null)
            {
                return;
            }

            Gizmos.color =
                isCurrentlyAligned
                    ? Color.green
                    : Color.yellow;

            Gizmos.DrawWireSphere(
                targetPose.position,
                maximumPositionDistance
            );

            Gizmos.DrawLine(
                targetPose.position,
                targetPose.position +
                targetPose.forward * 0.25f
            );

            Gizmos.DrawLine(
                targetPose.position,
                targetPose.position +
                targetPose.up * 0.18f
            );
        }
#endif
    }
}