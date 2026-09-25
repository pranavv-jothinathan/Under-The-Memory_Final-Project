using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.VFX;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ScannerBehavior : MonoBehaviour
{
    [Header("VFX")]
    public VisualEffect vfx;
    public RenderTexture renderTexture;
    public Camera scanCam;

    [Header("Audio")]
    [Tooltip("扫描音。不填则自动找同棵扫描仪树下名为 Scan 的 AudioSource。")]
    public AudioSource scanAudio;

    [Header("Raycast")]
    public Camera rayCamera;
    public float rayDistance = 100f;
    public LayerMask rayMask = ~0;

    [Header("Input - VR")]
    [Tooltip("已停用。写死左手扳机会让没握持的那只手也能触发特效。")]
    public InputAction triggerAction =
        new InputAction(
            "ScannerTrigger",
            InputActionType.Button,
            "<XRController>{LeftHand}/triggerPressed"
        );

    [Header("Input - Keyboard")]
    public Key keyboardKey = Key.Space;

    [Header("Surface Point Cloud")]
    [Tooltip("第一根柱子之后是否把扫描点云收窄到 ScanSurface 层。")]
    public bool narrowCullingOnSurfaceMode = true;

    [Tooltip("同时把 scanCam 的 FOV 压窄。组员的 3DGS 扫描要求 FOV 保持 50，默认关闭。")]
    public bool narrowFovOnSurfaceMode;

    [Header("Debug")]
    [Tooltip("打开后在 Console 打印 3DGS 射线打到了什么，用来排查扫不到的问题。")]
    public bool logGaussianRay;

    private const float SurfaceScanFov = 16.927452f;
    private const string VfxCameraFovProperty = "Camera_fieldOfView";

    private XRGrabInteractable grab;
    private bool scanning;
    private bool surfacePointCloudMode;
    private GameObject lastLoggedHit;

    // 记录本轮扫描已经触发过的目标
    private readonly HashSet<GaussianHitReceiver> hitReceivers = new();

    /// <summary>
    /// 只认握持手的 activate（通常是那只手的扳机）和键盘。
    /// 没拿着扫描仪的那只手扣扳机不会出音效、特效，也不会推进扫描。
    /// </summary>
    public bool IsScanInputHeld =>
        ReadHoldingHandActivate() ||
        IsKeyboardPressed();

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        if (grab == null)
            grab = GetComponentInParent<XRGrabInteractable>();

        if (grab == null)
            grab = GetComponentInChildren<XRGrabInteractable>(true);

        if (scanAudio == null)
            scanAudio = FindScanAudioSource();

        SuppressGrabActivatedAudio();
    }

    private void OnDisable()
    {
        StopScan();
    }

    private void Update()
    {
        if (IsScanInputHeld)
        {
            UpdateScan();
            CastRayCheck();
        }
        else
        {
            StopScan();
        }
    }

    /// <summary>
    /// Scene grab activate still calls this. Audio and VFX both start in StartScan.
    /// </summary>
    public void TriggerScan()
    {
        UpdateScan();
        CastRayCheck();
    }

    /// <summary>
    /// After the first memory pillar: scan dots sit on 扫描层 (ScanSurface) meshes.
    /// Does not change Ghost, PointDots grab on/off, or 3DGS rays.
    /// </summary>
    public void EnableSurfacePointCloudMode()
    {
        if (surfacePointCloudMode)
            return;

        surfacePointCloudMode = true;

        if (scanCam != null)
        {
            if (narrowCullingOnSurfaceMode)
            {
                int scanSurfaceLayer = LayerMask.NameToLayer("ScanSurface");

                if (scanSurfaceLayer >= 0)
                    scanCam.cullingMask = 1 << scanSurfaceLayer;
            }

            if (narrowFovOnSurfaceMode)
                scanCam.fieldOfView = SurfaceScanFov;
        }

        if (narrowFovOnSurfaceMode)
            ApplySurfaceVfxCameraFov();
    }

    private bool ReadHoldingHandActivate()
    {
        if (grab == null || !grab.isSelected)
            return false;

        XRBaseInputInteractor inputInteractor =
            grab.firstInteractorSelecting as XRBaseInputInteractor;

        if (inputInteractor == null || inputInteractor.activateInput == null)
            return false;

        return inputInteractor.activateInput.ReadIsPerformed();
    }

    private bool IsKeyboardPressed()
    {
        return Keyboard.current != null &&
            Keyboard.current[keyboardKey].isPressed;
    }

    private void UpdateScan()
    {
        if (!scanning)
        {
            StartScan();
        }

        RenderScan();
    }

    private void StartScan()
    {
        scanning = true;

        hitReceivers.Clear();
        lastLoggedHit = null;

        if (scanAudio != null && !scanAudio.isPlaying)
            scanAudio.Play();

        if (vfx == null)
            return;

        vfx.SendEvent("OnDotsPlay");
        vfx.SendEvent("OnScanPlay");
        vfx.SendEvent("OnScrollPlay");
    }

    private void StopScan()
    {
        if (!scanning)
            return;

        scanning = false;

        if (scanAudio != null)
            scanAudio.Stop();

        if (vfx == null)
            return;

        vfx.SendEvent("OnDotsStop");
        vfx.SendEvent("OnScanStop");
        vfx.SendEvent("OnScrollStop");
    }

    private AudioSource FindScanAudioSource()
    {
        Transform searchRoot = transform;

        if (grab != null && grab.transform.parent != null)
            searchRoot = grab.transform.parent;
        else if (grab != null)
            searchRoot = grab.transform;

        AudioSource[] sources =
            searchRoot.GetComponentsInChildren<AudioSource>(true);

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] != null && sources[i].gameObject.name == "Scan")
                return sources[i];
        }

        return null;
    }

    /// <summary>
    /// Inspector 里 Activated 还挂着 PlayOneShot。音效改由 StartScan/StopScan
    /// 和特效一起开关，这里关掉那条以免扣一次扳机播两遍。
    /// </summary>
    private void SuppressGrabActivatedAudio()
    {
        if (grab == null)
            return;

        int count = grab.activated.GetPersistentEventCount();

        for (int i = 0; i < count; i++)
        {
            string methodName = grab.activated.GetPersistentMethodName(i);

            if (methodName == "PlayOneShot" || methodName == "Play")
            {
                grab.activated.SetPersistentListenerState(
                    i,
                    UnityEventCallState.Off
                );
            }
        }
    }

    private void RenderScan()
    {
        if (scanCam == null || renderTexture == null)
            return;

        scanCam.targetTexture = renderTexture;
        scanCam.Render();

        if (vfx != null && vfx.HasTexture("RenderTexture"))
        {
            vfx.SetTexture("RenderTexture", renderTexture);
        }

        if (surfacePointCloudMode && narrowFovOnSurfaceMode)
            ApplySurfaceVfxCameraFov();
    }

    private void ApplySurfaceVfxCameraFov()
    {
        if (vfx == null || !vfx.HasFloat(VfxCameraFovProperty))
            return;

        vfx.SetFloat(
            VfxCameraFovProperty,
            SurfaceScanFov * Mathf.Deg2Rad
        );
    }

    private void CastRayCheck()
    {
        // 兜底不能用 Camera.main：场景里扫描仪相机也带 MainCamera tag。
        Camera cam = rayCamera != null ? rayCamera : scanCam;

        if (cam == null)
            return;

        Ray ray = new Ray(
            cam.transform.position,
            cam.transform.forward
        );

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                rayDistance,
                rayMask,
                QueryTriggerInteraction.Ignore))
        {
            LogRay("3DGS ray hit nothing (检查层 3dgs 的 collider 是否被世界切换关掉了)", null);
            return;
        }

        if (hit.collider == null || !hit.collider.CompareTag("3dgs"))
        {
            LogRay("3DGS ray hit an object without the 3dgs tag", hit.collider);
            return;
        }

        GaussianHitReceiver receiver =
            hit.collider.GetComponentInParent<GaussianHitReceiver>();

        if (receiver == null)
        {
            LogRay("3DGS ray hit a 3dgs collider but found no GaussianHitReceiver", hit.collider);
            return;
        }

        if (hitReceivers.Add(receiver))
        {
            LogRay("3DGS reveal triggered", hit.collider);
            receiver.OnScannerHit();
        }
    }

    private void LogRay(string message, Collider hitCollider)
    {
        if (!logGaussianRay)
            return;

        GameObject hitObject = hitCollider != null ? hitCollider.gameObject : null;

        // 每帧都打会刷屏，只在命中对象变化时打一次。
        if (hitObject == lastLoggedHit)
            return;

        lastLoggedHit = hitObject;

        if (hitObject == null)
        {
            Debug.Log($"[ScannerBehavior] {message}", this);
            return;
        }

        Debug.Log(
            $"[ScannerBehavior] {message}: {hitObject.name} " +
            $"(layer {LayerMask.LayerToName(hitObject.layer)}, tag {hitObject.tag})",
            hitObject
        );
    }
}
