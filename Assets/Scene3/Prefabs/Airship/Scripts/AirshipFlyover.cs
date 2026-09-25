using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Random = UnityEngine.Random;

/// <summary>
/// Slow path flyover with engine audio, propeller spin, paper-rain particles,
/// and a few grabbable sheets dropped straight down onto a ground target.
/// Call BeginFlyover() from story.
/// </summary>
public class AirshipFlyover : MonoBehaviour
{
    /// <summary>Raised once the grabbable flyers have been spawned.</summary>
    public event Action FlyersDropped;

    /// <summary>Raised the first time the player grabs any dropped flyer or pamphlet.</summary>
    public event Action FirstFlyerPicked;

    /// <summary>Raised when the visual reaches the last waypoint and the flyover stops.</summary>
    public event Action FlyoverCompleted;

    [Header("Timing")]
    [SerializeField] private bool playOnStart;
    [SerializeField] private float delayBeforeEnter = 5f;
    [SerializeField] private float flightDuration = 16f;

    [Header("Path")]
    [SerializeField] private Transform visual;
    [SerializeField] private Transform[] waypoints;
    [Tooltip("网格朝向相对路径切线的偏航。0 = 鼻子沿起始点指向结束点。模型若反了再改 180。")]
    [SerializeField] private float headingYawOffset = 0f;

    [Header("Audio / Motion")]
    [SerializeField] private AudioSource engineAudio;
    [SerializeField] private Transform[] propellers;
    [SerializeField] private Vector3 propellerAxis = Vector3.forward;
    [SerializeField] private float propellerSpeed = 720f;

    [Header("Paper rain")]
    [SerializeField] private ParticleSystem[] rain;
    [SerializeField] private Transform dropPoint;
    [SerializeField] private FlyerSheet protestPrefab;
    [SerializeField] private FlyerSheet bristolPrefab;
    [SerializeField] private FoldingPamphlet pamphletPrefab;
    [SerializeField] private float interactableDropAt = 0.48f;

    [Header("Ground drop")]
    [Tooltip("传单的地面落点，通常指向场景里的『飞艇传单落点』。")]
    [SerializeField] private Transform dropTarget;
    [Tooltip("在落点上方多高处生成传单。")]
    [SerializeField] private float dropHeight = 7f;
    [Tooltip("落点周围的水平散布半径。")]
    [SerializeField] private float dropSpread = 1.5f;
    [Tooltip("生成传单时相对预制体再乘的缩放。")]
    [SerializeField] private float spawnScale = 2f;

    public bool IsFlying => flying;

    private bool flying;
    private bool droppedInteractables;
    private Coroutine flyRoutine;
    private readonly List<Rigidbody> droppedBodies = new List<Rigidbody>();
    private bool pickedReported;
    private Vector3 meshLocalCenter;
    private bool haveMeshLocalCenter;

    public void BeginFlyover()
    {
        if (flyRoutine != null)
            StopCoroutine(flyRoutine);

        if (visual != null)
            visual.gameObject.SetActive(true);
        ParkAtStart();

        flyRoutine = StartCoroutine(FlyRoutine());
    }

    private void Start()
    {
        SetRain(false);
        ParkAtStart();
        if (visual != null)
            visual.gameObject.SetActive(false);
        if (playOnStart)
            BeginFlyover();
    }

    private void Update()
    {
        if (!flying || propellers == null)
            return;

        float step = propellerSpeed * Time.deltaTime;
        for (int i = 0; i < propellers.Length; i++)
        {
            if (propellers[i] != null)
                propellers[i].Rotate(propellerAxis, step, Space.Self);
        }
    }

    private IEnumerator FlyRoutine()
    {
        if (visual != null)
            visual.gameObject.SetActive(true);
        ParkAtStart();

        if (delayBeforeEnter > 0f)
            yield return new WaitForSeconds(delayBeforeEnter);

        flying = true;
        droppedInteractables = false;
        SetRain(true);
        SetEnginePlaying(true);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.1f, flightDuration);
            float u = Mathf.Clamp01(t);
            EvaluatePath(Smooth(u), out Vector3 pos, out Vector3 tangent);
            ApplyVisualPose(pos, tangent);

            if (!droppedInteractables && u >= interactableDropAt)
            {
                droppedInteractables = true;
                DropInteractables();
            }

            yield return null;
        }

        flying = false;
        SetRain(false);
        SetEnginePlaying(false);
        FlyoverCompleted?.Invoke();
        flyRoutine = null;
    }

    public void SetEnginePlaying(bool on)
    {
        if (engineAudio == null)
            return;

        if (on)
        {
            engineAudio.loop = true;
            if (!engineAudio.isPlaying)
                engineAudio.Play();
            return;
        }

        if (engineAudio.isPlaying)
            engineAudio.Stop();
    }

    private void ParkAtStart()
    {
        flying = false;
        if (visual == null)
            return;

        if (!TryGetPathEnds(out Vector3 startPos, out Vector3 endPos))
            return;

        Vector3 ahead = endPos - startPos;
        if (ahead.sqrMagnitude < 0.0001f)
            ahead = Vector3.forward;

        ZeroUnexpectedMeshOffset();
        ApplyVisualPose(startPos, ahead.normalized);
        CacheMeshLocalCenter();
        ApplyVisualPose(startPos, ahead.normalized);
    }

    private void ApplyVisualPose(Vector3 pathPos, Vector3 tangent)
    {
        if (visual == null)
            return;

        Quaternion rotation = Quaternion.identity;
        if (tangent.sqrMagnitude > 0.0001f)
            rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up)
                * Quaternion.Euler(0f, headingYawOffset, 0f);

        Vector3 offset = haveMeshLocalCenter ? rotation * meshLocalCenter : Vector3.zero;
        visual.SetPositionAndRotation(pathPos - offset, rotation);
    }

    private void ZeroUnexpectedMeshOffset()
    {
        if (visual == null)
            return;

        for (int i = 0; i < visual.childCount; i++)
        {
            Transform child = visual.GetChild(i);
            if (child == null)
                continue;

            Vector3 local = child.localPosition;
            if (Mathf.Abs(local.x) < 0.5f && Mathf.Abs(local.z) < 0.5f)
                continue;

            local.x = 0f;
            local.z = 0f;
            child.localPosition = local;
        }
    }

    private void CacheMeshLocalCenter()
    {
        haveMeshLocalCenter = false;
        meshLocalCenter = Vector3.zero;
        if (visual == null)
            return;

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return;

        bool haveBounds = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            if (!haveBounds)
            {
                bounds = renderer.bounds;
                haveBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!haveBounds)
            return;

        meshLocalCenter = visual.InverseTransformPoint(bounds.center);
        haveMeshLocalCenter = true;
    }

    private void EvaluatePath(float u, out Vector3 pos, out Vector3 tangent)
    {
        pos = visual != null ? visual.position : transform.position;
        tangent = visual != null ? visual.forward : transform.forward;

        if (!TryGetPathEnds(out Vector3 startPos, out Vector3 endPos))
            return;

        pos = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(u));
        tangent = endPos - startPos;
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = visual != null ? visual.forward : Vector3.forward;
        else
            tangent.Normalize();
    }

    /// <summary>
    /// 只用航点列表的第一个和最后一个，中间点即使还挂在飞艇下也不参与路径。
    /// </summary>
    private bool TryGetPathEnds(out Vector3 startPos, out Vector3 endPos)
    {
        startPos = default;
        endPos = default;
        if (waypoints == null || waypoints.Length == 0)
            return false;

        Transform start = null;
        Transform end = null;
        for (int i = 0; i < waypoints.Length; i++)
        {
            Transform point = waypoints[i];
            if (point == null)
                continue;

            if (start == null)
                start = point;
            end = point;
        }

        if (start == null)
            return false;

        startPos = start.position;
        if (end == null || end == start)
        {
            endPos = startPos + start.forward;
            return true;
        }

        endPos = end.position;
        return true;
    }

    /// <summary>Average world position of everything that was dropped.</summary>
    public Vector3 AverageDroppedPosition()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < droppedBodies.Count; i++)
        {
            if (droppedBodies[i] == null)
                continue;

            sum += droppedBodies[i].position;
            count++;
        }

        return count > 0 ? sum / count : Vector3.zero;
    }

    /// <summary>True once every dropped item has settled below the given speed.</summary>
    public bool DroppedItemsAtRest(float speedThreshold)
    {
        if (droppedBodies.Count == 0)
            return false;

        for (int i = 0; i < droppedBodies.Count; i++)
        {
            Rigidbody body = droppedBodies[i];
            if (body == null)
                continue;

            if (body.linearVelocity.magnitude > speedThreshold)
                return false;
        }

        return true;
    }

    private void DropInteractables()
    {
        Vector3 ground = dropTarget != null
            ? dropTarget.position
            : (dropPoint != null ? dropPoint.position : transform.position);

        Vector3 origin = ground + Vector3.up * Mathf.Max(0.5f, dropHeight);

        droppedBodies.Clear();
        SpawnFlyer(protestPrefab, origin);
        SpawnFlyer(protestPrefab, origin);
        SpawnFlyer(bristolPrefab, origin);
        SpawnFlyer(bristolPrefab, origin);
        SpawnPamphlet(origin);

        FlyersDropped?.Invoke();
    }

    private void SpawnFlyer(FlyerSheet prefab, Vector3 origin)
    {
        if (prefab == null)
            return;

        FlyerSheet sheet = Instantiate(prefab, origin + ScatterOffset(), Random.rotation);
        sheet.name = prefab.name;
        ApplySpawnScale(sheet.transform);
        sheet.ReleaseInAir(Vector3.zero, Random.insideUnitSphere * 1.2f);
        Track(sheet.gameObject);
    }

    private void SpawnPamphlet(Vector3 origin)
    {
        if (pamphletPrefab == null)
            return;

        FoldingPamphlet pamphlet = Instantiate(pamphletPrefab, origin + ScatterOffset(), Random.rotation);
        pamphlet.name = pamphletPrefab.name;
        ApplySpawnScale(pamphlet.transform);
        pamphlet.ReleaseInAir(Vector3.zero, Random.insideUnitSphere * 0.9f);
        Track(pamphlet.gameObject);
    }

    private void ApplySpawnScale(Transform spawned)
    {
        if (spawned == null)
            return;

        float scale = Mathf.Max(0.01f, spawnScale);
        spawned.localScale = spawned.localScale * scale;
    }

    private Vector3 ScatterOffset()
    {
        Vector2 flat = Random.insideUnitCircle;
        float radius = Mathf.Lerp(1f, Mathf.Max(1f, dropSpread), flat.magnitude);
        Vector2 scattered = flat.sqrMagnitude > 0.0001f ? flat.normalized * radius : new Vector2(radius, 0f);
        return new Vector3(scattered.x, Random.Range(-0.4f, 0.4f), scattered.y);
    }

    private void Track(GameObject spawned)
    {
        Rigidbody body = spawned.GetComponent<Rigidbody>();
        if (body != null)
            droppedBodies.Add(body);

        XRGrabInteractable grab = spawned.GetComponent<XRGrabInteractable>();
        if (grab != null)
            grab.selectEntered.AddListener(OnDroppedItemGrabbed);
    }

    private void OnDroppedItemGrabbed(SelectEnterEventArgs args)
    {
        if (pickedReported)
            return;

        pickedReported = true;
        FirstFlyerPicked?.Invoke();
    }

    private void SetRain(bool on)
    {
        if (rain == null)
            return;

        for (int i = 0; i < rain.Length; i++)
        {
            ParticleSystem system = rain[i];
            if (system == null)
                continue;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = on;
            if (on)
                system.Play(true);
            else
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
