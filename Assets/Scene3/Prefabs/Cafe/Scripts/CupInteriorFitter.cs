using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Samples a cup / glass mesh and builds a radius-over-height profile of the inner wall.
/// Handle vertices are ignored so a mug still fits as a tapered cylinder.
/// </summary>
public static class CupInteriorFitter
{
    [System.Serializable]
    public struct RadiusKey
    {
        public float height01;
        public float radius;

        public RadiusKey(float height01, float radius)
        {
            this.height01 = height01;
            this.radius = radius;
        }
    }

    public struct FitResult
    {
        public float bottomY;
        public float height;
        public Vector2 axisXZ;
        public RadiusKey[] profile;
    }

    public static bool TryFit(Mesh mesh, int sliceCount, float inset, out FitResult result)
    {
        result = default;
        if (mesh == null)
            return false;

        Vector3[] vertices = mesh.vertices;
        if (vertices == null || vertices.Length < 8)
            return false;

        Bounds bounds = mesh.bounds;
        Vector2 axis = ComputeAxis(vertices, bounds);

        float yMin = bounds.min.y;
        float yMax = bounds.max.y;
        float height = yMax - yMin;
        if (height < 0.001f)
            return false;

        int slices = Mathf.Max(6, sliceCount);
        List<RadiusKey> keys = new List<RadiusKey>(slices);

        for (int i = 0; i < slices; i++)
        {
            float t = slices == 1 ? 0.5f : i / (float)(slices - 1);
            // Stay off the floor disc and the rim lip.
            float y = yMin + height * Mathf.Lerp(0.08f, 0.90f, t);
            float band = height * 0.045f;
            if (!TryInnerRadius(vertices, axis, y, band, out float radius))
                continue;

            keys.Add(new RadiusKey(t, radius * inset));
        }

        if (keys.Count < 2)
            return false;

        SmoothRadii(keys);

        float liquidBottom = yMin + height * 0.06f;
        float liquidHeight = height * 0.78f;

        result = new FitResult
        {
            bottomY = liquidBottom,
            height = liquidHeight,
            axisXZ = axis,
            profile = keys.ToArray()
        };
        return true;
    }

    private static Vector2 ComputeAxis(Vector3[] vertices, Bounds bounds)
    {
        Vector2 axis = new Vector2(bounds.center.x, bounds.center.z);
        List<float> radii = new List<float>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            float dx = vertices[i].x - axis.x;
            float dz = vertices[i].z - axis.y;
            radii.Add(Mathf.Sqrt(dx * dx + dz * dz));
        }

        radii.Sort();
        float median = radii[radii.Count / 2];
        float handleLimit = median * 1.25f;

        Vector2 sum = Vector2.zero;
        int count = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            float dx = vertices[i].x - axis.x;
            float dz = vertices[i].z - axis.y;
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            if (r > handleLimit)
                continue;

            sum.x += vertices[i].x;
            sum.y += vertices[i].z;
            count++;
        }

        if (count > 0)
            axis = sum / count;

        return axis;
    }

    private static bool TryInnerRadius(
        Vector3[] vertices,
        Vector2 axis,
        float y,
        float band,
        out float innerRadius)
    {
        innerRadius = 0f;
        List<float> radii = new List<float>(64);

        for (int i = 0; i < vertices.Length; i++)
        {
            if (Mathf.Abs(vertices[i].y - y) > band)
                continue;

            float dx = vertices[i].x - axis.x;
            float dz = vertices[i].z - axis.y;
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            if (r > 0.0005f)
                radii.Add(r);
        }

        if (radii.Count < 6)
            return false;

        radii.Sort();
        float median = radii[radii.Count / 2];

        List<float> wall = new List<float>(radii.Count);
        for (int i = 0; i < radii.Count; i++)
        {
            if (radii[i] <= median * 1.3f)
                wall.Add(radii[i]);
        }

        if (wall.Count < 4)
            wall = radii;

        // Inner wall is the smaller shell of the glass thickness.
        int index = Mathf.Clamp(Mathf.RoundToInt(wall.Count * 0.18f), 0, wall.Count - 1);
        innerRadius = wall[index];
        return innerRadius > 0.001f;
    }

    private static void SmoothRadii(List<RadiusKey> keys)
    {
        if (keys.Count < 3)
            return;

        float[] smoothed = new float[keys.Count];
        for (int i = 0; i < keys.Count; i++)
        {
            float sum = keys[i].radius;
            int n = 1;
            if (i > 0)
            {
                sum += keys[i - 1].radius;
                n++;
            }

            if (i < keys.Count - 1)
            {
                sum += keys[i + 1].radius;
                n++;
            }

            smoothed[i] = sum / n;
        }

        for (int i = 0; i < keys.Count; i++)
        {
            RadiusKey key = keys[i];
            key.radius = smoothed[i];
            keys[i] = key;
        }
    }
}
