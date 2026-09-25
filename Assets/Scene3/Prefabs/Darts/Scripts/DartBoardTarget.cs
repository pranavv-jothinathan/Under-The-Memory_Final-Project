using UnityEngine;

/// <summary>
/// Fixed dartboard. Thrown darts that hit this stay stuck until grabbed off.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DartBoardTarget : MonoBehaviour
{
    [SerializeField] private Collider boardCollider;
    [SerializeField] private float faceRadius = 0.22f;

    private Collider[] colliders;

    public Collider BoardCollider => boardCollider;
    public float FaceRadius => faceRadius;
    public Vector3 FaceNormal => transform.forward;

    private void Awake()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeAll;
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (boardCollider == null)
            boardCollider = GetComponentInChildren<Collider>();

        colliders = GetComponentsInChildren<Collider>(true);
        CacheRadius();
    }

    public bool ContainsPoint(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        float radial = new Vector2(local.x, local.y).magnitude;
        return radial <= faceRadius + 0.08f;
    }

    public Collider[] GetColliders()
    {
        if (colliders == null || colliders.Length == 0)
            colliders = GetComponentsInChildren<Collider>(true);
        return colliders;
    }

    public void SetFaceRadius(float radius)
    {
        faceRadius = radius;
    }

    private void CacheRadius()
    {
        if (faceRadius > 0.01f)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 localSize = transform.InverseTransformVector(bounds.size);
        faceRadius = Mathf.Max(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y)) * 0.5f;
    }
}
