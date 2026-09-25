using System;
using System.Collections.Generic;
using UnityEngine;

namespace BOM.Reconstruction
{
    /// <summary>
    /// Samples static particles across the surface of one or more MeshFilters.
    ///
    /// The particles do not simulate movement. Their positions are generated
    /// once and then progressively displayed according to visibility01.
    ///
    /// Important:
    /// Source FBX meshes must have Read/Write Enabled.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class BOM_SurfacePointCloud : MonoBehaviour
    {
        [Header("Source Building")]
        [Tooltip(
            "Root containing the building MeshFilters. " +
            "Usually use the Final building copy."
        )]
        [SerializeField]
        private Transform sourceRoot;

        [Header("Point Generation")]
        [Min(100)]
        [SerializeField]
        private int pointCount = 20000;

        [Min(0.001f)]
        [SerializeField]
        private float pointSize = 0.018f;

        [SerializeField]
        private Color pointColor =
            new Color(0.75f, 0.88f, 1f, 0.85f);

        [SerializeField]
        private int randomSeed = 12345;

        [Tooltip(
            "Rebuild the sampled points when Play Mode begins."
        )]
        [SerializeField]
        private bool buildOnStart = true;

        [Header("Runtime Updating")]
        [Tooltip(
            "Minimum change in visible particle count before " +
            "SetParticles is called again."
        )]
        [Min(1)]
        [SerializeField]
        private int minimumParticleUpdateStep = 32;

        private ParticleSystem particleSystemComponent;
        private ParticleSystem.Particle[] generatedParticles;

        private int currentVisibleCount = -1;
        private bool hasBuiltPoints;

        private readonly List<SurfaceTriangle>
            surfaceTriangles = new List<SurfaceTriangle>();

        private float totalSurfaceArea;

        private struct SurfaceTriangle
        {
            public Vector3 pointA;
            public Vector3 pointB;
            public Vector3 pointC;
            public float cumulativeArea;
        }

        private void Awake()
        {
            particleSystemComponent =
                GetComponent<ParticleSystem>();

            ConfigureParticleSystem();
        }

        private void Start()
        {
            if (buildOnStart)
            {
                BuildPointCloud();
            }

            SetVisibility01(0f, true);
        }

        private void ConfigureParticleSystem()
        {
            ParticleSystem.MainModule main =
                particleSystemComponent.main;

            main.loop = false;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.startLifetime = 100000f;
            main.startSize = pointSize;
            main.maxParticles = Mathf.Max(
                main.maxParticles,
                pointCount
            );

            main.simulationSpace =
                ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission =
                particleSystemComponent.emission;

            emission.enabled = false;
        }

        [ContextMenu("Build Point Cloud")]
        public void BuildPointCloud()
        {
            if (sourceRoot == null)
            {
                Debug.LogError(
                    $"{nameof(BOM_SurfacePointCloud)} on " +
                    $"{name}: Source Root has not been assigned.",
                    this
                );

                return;
            }

            surfaceTriangles.Clear();
            totalSurfaceArea = 0f;

            MeshFilter[] meshFilters =
                sourceRoot.GetComponentsInChildren<MeshFilter>(
                    true
                );

            Matrix4x4 worldToPointCloudLocal =
                transform.worldToLocalMatrix;

            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (meshFilter == null ||
                    meshFilter.sharedMesh == null)
                {
                    continue;
                }

                Mesh mesh = meshFilter.sharedMesh;

                if (!mesh.isReadable)
                {
                    Debug.LogWarning(
                        $"Mesh '{mesh.name}' is not readable. " +
                        "Select its FBX asset and enable " +
                        "'Read/Write Enabled' in the Model tab.",
                        meshFilter
                    );

                    continue;
                }

                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;

                Matrix4x4 meshToPointCloudLocal =
                    worldToPointCloudLocal *
                    meshFilter.transform.localToWorldMatrix;

                for (int triangleIndex = 0;
                     triangleIndex < triangles.Length;
                     triangleIndex += 3)
                {
                    Vector3 pointA =
                        meshToPointCloudLocal.MultiplyPoint3x4(
                            vertices[
                                triangles[triangleIndex]
                            ]
                        );

                    Vector3 pointB =
                        meshToPointCloudLocal.MultiplyPoint3x4(
                            vertices[
                                triangles[triangleIndex + 1]
                            ]
                        );

                    Vector3 pointC =
                        meshToPointCloudLocal.MultiplyPoint3x4(
                            vertices[
                                triangles[triangleIndex + 2]
                            ]
                        );

                    float triangleArea =
                        Vector3.Cross(
                            pointB - pointA,
                            pointC - pointA
                        ).magnitude * 0.5f;

                    if (triangleArea <= 0.0000001f)
                    {
                        continue;
                    }

                    totalSurfaceArea += triangleArea;

                    surfaceTriangles.Add(
                        new SurfaceTriangle
                        {
                            pointA = pointA,
                            pointB = pointB,
                            pointC = pointC,
                            cumulativeArea =
                                totalSurfaceArea
                        }
                    );
                }
            }

            if (surfaceTriangles.Count == 0 ||
                totalSurfaceArea <= 0f)
            {
                Debug.LogError(
                    $"{nameof(BOM_SurfacePointCloud)} on " +
                    $"{name}: No readable surface triangles found.",
                    this
                );

                hasBuiltPoints = false;
                return;
            }

            generatedParticles =
                new ParticleSystem.Particle[pointCount];

            UnityEngine.Random.State previousRandomState =
                UnityEngine.Random.state;

            UnityEngine.Random.InitState(randomSeed);

            for (int particleIndex = 0;
                 particleIndex < pointCount;
                 particleIndex++)
            {
                SurfaceTriangle triangle =
                    SelectRandomTriangle();

                Vector3 sampledPosition =
                    SamplePointInTriangle(
                        triangle.pointA,
                        triangle.pointB,
                        triangle.pointC
                    );

                generatedParticles[particleIndex] =
                    new ParticleSystem.Particle
                    {
                        position = sampledPosition,
                        startSize = pointSize,
                        startColor = pointColor,
                        startLifetime = 100000f,
                        remainingLifetime = 100000f
                    };
            }

            ShuffleParticles(generatedParticles);

            UnityEngine.Random.state =
                previousRandomState;

            hasBuiltPoints = true;
            currentVisibleCount = -1;

            SetVisibility01(0f, true);

            Debug.Log(
                $"Built {pointCount} surface points from " +
                $"{surfaceTriangles.Count} triangles for '{name}'.",
                this
            );
        }

        public void SetVisibility01(
            float visibility01,
            bool forceUpdate = false
        )
        {
            visibility01 = Mathf.Clamp01(visibility01);

            if (!hasBuiltPoints ||
                generatedParticles == null)
            {
                if (visibility01 > 0f)
                {
                    BuildPointCloud();
                }

                if (!hasBuiltPoints)
                {
                    return;
                }
            }

            int requestedVisibleCount =
                Mathf.RoundToInt(
                    visibility01 *
                    generatedParticles.Length
                );

            requestedVisibleCount = Mathf.Clamp(
                requestedVisibleCount,
                0,
                generatedParticles.Length
            );

            bool countChangedEnough =
                Mathf.Abs(
                    requestedVisibleCount -
                    currentVisibleCount
                ) >= minimumParticleUpdateStep;

            bool reachedBoundary =
                requestedVisibleCount == 0 ||
                requestedVisibleCount ==
                generatedParticles.Length;

            if (!forceUpdate &&
                !countChangedEnough &&
                !reachedBoundary)
            {
                return;
            }

            particleSystemComponent.SetParticles(
                generatedParticles,
                requestedVisibleCount
            );

            currentVisibleCount =
                requestedVisibleCount;
        }

        public void ClearPointCloud()
        {
            if (particleSystemComponent != null)
            {
                particleSystemComponent.Clear(
                    true
                );
            }

            currentVisibleCount = 0;
        }

        private SurfaceTriangle SelectRandomTriangle()
        {
            float randomArea =
                UnityEngine.Random.Range(
                    0f,
                    totalSurfaceArea
                );

            int lowerIndex = 0;
            int upperIndex =
                surfaceTriangles.Count - 1;

            while (lowerIndex < upperIndex)
            {
                int middleIndex =
                    (lowerIndex + upperIndex) / 2;

                if (surfaceTriangles[middleIndex]
                    .cumulativeArea < randomArea)
                {
                    lowerIndex =
                        middleIndex + 1;
                }
                else
                {
                    upperIndex =
                        middleIndex;
                }
            }

            return surfaceTriangles[lowerIndex];
        }

        private static Vector3 SamplePointInTriangle(
            Vector3 pointA,
            Vector3 pointB,
            Vector3 pointC
        )
        {
            float randomU =
                UnityEngine.Random.value;

            float randomV =
                UnityEngine.Random.value;

            if (randomU + randomV > 1f)
            {
                randomU = 1f - randomU;
                randomV = 1f - randomV;
            }

            return pointA +
                   randomU * (pointB - pointA) +
                   randomV * (pointC - pointA);
        }

        private static void ShuffleParticles(
            ParticleSystem.Particle[] particles
        )
        {
            for (int index = particles.Length - 1;
                 index > 0;
                 index--)
            {
                int randomIndex =
                    UnityEngine.Random.Range(
                        0,
                        index + 1
                    );

                ParticleSystem.Particle temporary =
                    particles[index];

                particles[index] =
                    particles[randomIndex];

                particles[randomIndex] =
                    temporary;
            }
        }
    }
}