using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimpleCavePlayer : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed = 3f;
    public float verticalMoveSpeed = 2f;
    public float gravity = 0f;

    [Header("视角")]
    public Transform cameraTransform;
    public float mouseSensitivity = 0.12f;
    public float minLookAngle = -80f;
    public float maxLookAngle = 80f;

    private CharacterController controller;
    private float verticalVelocity;
    private float cameraPitch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null)
        {
            Camera playerCamera = GetComponentInChildren<Camera>();

            if (playerCamera != null)
            {
                cameraTransform = playerCamera.transform;
            }
        }
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleMovement();
        HandleLook();
        HandleCursor();
    }

    private void HandleMovement()
    {
        if (Keyboard.current == null)
            return;

        float horizontal = 0f;
        float forwardInput = 0f;
        float verticalInput = 0f;

        if (Keyboard.current.aKey.isPressed)
            horizontal -= 1f;

        if (Keyboard.current.dKey.isPressed)
            horizontal += 1f;

        if (Keyboard.current.wKey.isPressed)
            forwardInput += 1f;

        if (Keyboard.current.sKey.isPressed)
            forwardInput -= 1f;

        if (Keyboard.current.spaceKey.isPressed)
            verticalInput += 1f;

        if (Keyboard.current.leftCtrlKey.isPressed ||
            Keyboard.current.cKey.isPressed)
        {
            verticalInput -= 1f;
        }

        Vector3 horizontalInput =
            new Vector3(horizontal, 0f, forwardInput);

        if (horizontalInput.sqrMagnitude > 1f)
        {
            horizontalInput.Normalize();
        }

        Vector3 moveDirection =
            transform.right * horizontalInput.x +
            transform.forward * horizontalInput.z;

        moveDirection *= moveSpeed;

        if (Mathf.Abs(gravity) > 0.001f)
        {
            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            verticalVelocity += gravity * Time.deltaTime;
        }
        else
        {
            verticalVelocity = 0f;
        }

        moveDirection.y =
            verticalInput * verticalMoveSpeed +
            verticalVelocity;

        controller.Move(moveDirection * Time.deltaTime);
    }

    private void HandleLook()
    {
        if (Mouse.current == null || cameraTransform == null)
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float mouseX = mouseDelta.x * mouseSensitivity;
        float mouseY = mouseDelta.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(
            cameraPitch,
            minLookAngle,
            maxLookAngle
        );

        cameraTransform.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void HandleCursor()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}