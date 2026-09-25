using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class UnderwaterPlayerMovement : MonoBehaviour
{
    [Header("移动速度")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float verticalSpeed = 2f;

    [Header("视角控制")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;

    [Header("水下漂浮感")]
    [SerializeField] private float acceleration = 4f;
    [SerializeField] private float deceleration = 3f;

    private CharacterController controller;
    private Camera playerCamera;

    private Vector3 currentVelocity;
    private float cameraPitch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
        {
            Debug.LogError("Player 子物体下面没有找到 Camera。");
        }
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        LookAround();
        MoveUnderwater();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LookAround()
    {
        if (playerCamera == null)
            return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // 左右转动整个玩家
        transform.Rotate(Vector3.up * mouseX);

        // 上下只转相机
        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -maxLookAngle,
            maxLookAngle
        );

        playerCamera.transform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void MoveUnderwater()
    {
        if (playerCamera == null)
            return;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float forwardInput = Input.GetAxisRaw("Vertical");

        float verticalInput = 0f;

        if (Input.GetKey(KeyCode.Space))
        {
            verticalInput = 1f;
        }

        if (Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.C))
        {
            verticalInput = -1f;
        }

        // 移动方向跟随相机朝向
        Vector3 cameraForward = playerCamera.transform.forward;
        Vector3 cameraRight = playerCamera.transform.right;

        // 前后左右保持水平移动
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 horizontalMovement =
            cameraForward * forwardInput +
            cameraRight * horizontal;

        if (horizontalMovement.sqrMagnitude > 1f)
        {
            horizontalMovement.Normalize();
        }

        Vector3 targetVelocity =
            horizontalMovement * moveSpeed +
            Vector3.up * verticalInput * verticalSpeed;

        float smoothSpeed =
            targetVelocity.sqrMagnitude > 0.01f
            ? acceleration
            : deceleration;

        currentVelocity = Vector3.Lerp(
            currentVelocity,
            targetVelocity,
            smoothSpeed * Time.deltaTime
        );

        controller.Move(currentVelocity * Time.deltaTime);
    }
}