using UnityEngine;

public class JellyfishBellPulse : MonoBehaviour
{
    [Header("Objects")]
    public Transform bellObject;      // group2
    public Transform insideObject;    // group4
    public Transform tentacleRoot;    // joint1

    [Header("Bell Pulse")]
    public float speed = 0.35f;

    public float contractXZ = 0.97f;
    public float contractY = 1.0f;

    public float expandXZ = 1.01f;
    public float expandY = 1.0f;

    [Header("Tentacle Follow")]
    public Vector3 tentacleFollowOffset = new Vector3(0f, 0.015f, 0f);

    private Vector3 bellBaseScale;
    private Vector3 insideBaseScale;
    private Vector3 tentacleBaseLocalPosition;

    void Start()
    {
        if (bellObject != null)
        {
            bellBaseScale = bellObject.localScale;
        }

        if (insideObject != null)
        {
            insideBaseScale = insideObject.localScale;
        }

        if (tentacleRoot != null)
        {
            tentacleBaseLocalPosition = tentacleRoot.localPosition;
        }
    }

    void LateUpdate()
    {
        if (bellObject == null) return;

        float t = (Mathf.Sin(Time.time * speed * Mathf.PI * 2f) + 1f) * 0.5f;

        float xz;
        float y;

        if (t < 0.5f)
        {
            float k = t / 0.5f;
            xz = Mathf.Lerp(1f, contractXZ, k);
            y = Mathf.Lerp(1f, contractY, k);
        }
        else
        {
            float k = (t - 0.5f) / 0.5f;
            xz = Mathf.Lerp(contractXZ, expandXZ, k);
            y = Mathf.Lerp(contractY, expandY, k);
        }

        bellObject.localScale = new Vector3(
            bellBaseScale.x * xz,
            bellBaseScale.y * y,
            bellBaseScale.z * xz
        );

        if (insideObject != null)
        {
            insideObject.localScale = new Vector3(
                insideBaseScale.x * Mathf.Lerp(1f, xz, 0.5f),
                insideBaseScale.y * Mathf.Lerp(1f, y, 0.5f),
                insideBaseScale.z * Mathf.Lerp(1f, xz, 0.5f)
            );
        }

        if (tentacleRoot != null)
        {
            float followAmount = Mathf.InverseLerp(1f, contractXZ, xz);
            tentacleRoot.localPosition = tentacleBaseLocalPosition + tentacleFollowOffset * followAmount;
        }
    }
}
