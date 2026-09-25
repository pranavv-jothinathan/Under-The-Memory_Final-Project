using UnityEngine;

/// <summary>
/// Seats the cake on the plate using world renderer bounds.
/// Ignores particle renderers so crumb gizmos cannot lift the cake.
/// </summary>
[ExecuteAlways]
public class CakePlateAligner : MonoBehaviour
{
    [SerializeField] private Transform plate;
    [SerializeField] private Transform cake;

    private void OnEnable()
    {
        if (!Application.isPlaying)
            Align();
    }

    [ContextMenu("Align Cake To Plate")]
    public void Align()
    {
        ResolveRefs();
        if (plate == null || cake == null)
            return;

        if (!TryBounds(plate, out Bounds plateBounds) || !TryBounds(cake, out Bounds cakeBounds))
            return;

        float delta = plateBounds.max.y - cakeBounds.min.y;
        if (Mathf.Abs(delta) < 0.0005f)
            return;

        cake.position += Vector3.up * (delta + 0.001f);
    }

    private void ResolveRefs()
    {
        if (plate != null && cake != null)
            return;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (plate == null && child.name == "Plate")
                plate = child;
            else if (cake == null && (
                child.name.IndexOf("Cake", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("Chocolate", System.StringComparison.OrdinalIgnoreCase) >= 0))
                cake = child;
        }
    }

    private static bool TryBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;
            if (renderer is ParticleSystemRenderer)
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }
}
