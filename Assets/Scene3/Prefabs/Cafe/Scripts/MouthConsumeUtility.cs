using UnityEngine;

public static class MouthConsumeUtility
{
    public static Transform FindHead()
    {
        Camera cam = Camera.main;
        if (cam != null)
            return cam.transform;

        cam = Object.FindFirstObjectByType<Camera>();
        return cam != null ? cam.transform : null;
    }

    public static bool IsNearMouth(Vector3 worldPoint, float distance)
    {
        Transform head = FindHead();
        if (head == null)
            return false;

        return Vector3.Distance(worldPoint, head.position) <= distance;
    }

    public static bool IsTiltedToDrink(Transform cup, float maxUpDot)
    {
        return Vector3.Dot(cup.up, Vector3.up) <= maxUpDot;
    }
}
