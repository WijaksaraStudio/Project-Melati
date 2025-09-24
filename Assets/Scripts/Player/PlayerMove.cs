using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovementInput : MonoBehaviour
{
    #region Movement Settings
    [Header("Movement Settings")]
    public float walkSpeed = 3.5f;
    public float sprintSpeed = 6.0f;
    public float acceleration = 8f;
    public float gravity = -9.81f;
    #endregion

    #region Camera Settings
    [Header("Camera Settings")]
    public Transform cameraHolder;   // Parent kosong
    public Transform cam;            // Main Camera
    [Range(30f, 200f)] public float mouseSensitivity = 80f;
    public float baseFOV = 60f;
    public float sprintFOV = 70f;
    public float fovSmooth = 6f;

    private Camera camComponent;
    private float xRotation = 0f;
    private float currentFOV;
    #endregion

    #region Headbob Settings
    [Header("Headbob Settings")]
    [Range(0.001f, 0.05f)] public float walkBobAmount = 0.015f;
    [Range(0.001f, 0.05f)] public float sprintBobAmount = 0.03f;
    [Range(1f, 20f)] public float walkBobFrequency = 7f;
    [Range(1f, 20f)] public float sprintBobFrequency = 12f;
    [Range(1f, 20f)] public float idleBobFrequency = 2f;
    [Range(0.001f, 0.02f)] public float idleBobAmount = 0.003f;
    public float bobSmooth = 10f;

    private Vector3 holderStartPos;
    private float bobTimer;
    #endregion

    #region Camera Tilt Settings
    [Header("Camera Tilt Settings")]
    public float tiltAmount = 3f;          // miring saat strafe
    public float tiltSmooth = 6f;
    private float currentTilt;
    #endregion

    #region Sprint Tilt + Kick
    [Header("Sprint Tilt & Kick")]
    public float sprintTilt = 5f;
    public float sprintTiltSmooth = 6f;
    private float sprintTiltCurrent;

    public float moveKickStrength = 3f;
    public float stopKickStrength = -2f;
    public float kickSmooth = 8f;
    private float targetKick = 0f;
    private float currentKick = 0f;
    private bool wasMoving = false;
    #endregion

    #region Private Vars
    private CharacterController controller;
    private InputSystem_Actions inputActions;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;
    private bool isGrounded;
    private float targetSpeed;
    private float currentSpeed;
    #endregion

    #region Unity Callbacks
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputActions = new InputSystem_Actions();
        Cursor.lockState = CursorLockMode.Locked;

        if (cameraHolder != null) holderStartPos = cameraHolder.localPosition;

        camComponent = cam.GetComponent<Camera>();
        if (camComponent != null) currentFOV = baseFOV;
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;
    }

    private void OnDisable() => inputActions.Player.Disable();

    private void Update()
    {
        HandleMovement();
        HandleCamera();
        HandleHeadBob();
        HandleFOV();
    }
    #endregion

    #region Movement
    void HandleMovement()
    {
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        bool isSprinting = inputActions.Player.Sprint.ReadValue<float>() > 0;
        targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * acceleration);

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        Vector3 motion = (move * currentSpeed) + velocity;

        controller.Move(motion * Time.deltaTime);
        velocity.y += gravity * Time.deltaTime;
    }
    #endregion

    #region Camera
    void HandleCamera()
    {
        // mouse look
        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -75f, 75f);

        // sprint tilt
        bool isSprinting = inputActions.Player.Sprint.ReadValue<float>() > 0;
        float targetTiltX = isSprinting ? -sprintTilt : 0f;
        sprintTiltCurrent = Mathf.Lerp(sprintTiltCurrent, targetTiltX, Time.deltaTime * sprintTiltSmooth);

        // lean tilt (strafe kiri/kanan)
        float targetTilt = moveInput.x * tiltAmount;
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, Time.deltaTime * tiltSmooth);

        // camera kick
        bool isMoving = moveInput.magnitude > 0.1f;
        if (isMoving && !wasMoving) targetKick = moveKickStrength;
        else if (!isMoving && wasMoving) targetKick = stopKickStrength;
        else targetKick = 0f;
        currentKick = Mathf.Lerp(currentKick, targetKick, Time.deltaTime * kickSmooth);
        wasMoving = isMoving;

        // apply rotation
        cam.localRotation = Quaternion.Euler(xRotation + sprintTiltCurrent + currentKick, 0f, currentTilt);
        transform.Rotate(Vector3.up * mouseX);
    }
    #endregion

    #region Headbob
    void HandleHeadBob()
    {
        if (cameraHolder == null) return;

        bool isMoving = controller.velocity.magnitude > 0.1f && isGrounded;
        bool isSprinting = inputActions.Player.Sprint.ReadValue<float>() > 0;

        Vector3 offset = Vector3.zero;

        if (isMoving)
        {
            float freq = isSprinting ? sprintBobFrequency : walkBobFrequency;
            float amp = isSprinting ? sprintBobAmount : walkBobAmount;

            bobTimer += Time.deltaTime * freq;
            offset.y = Mathf.Sin(bobTimer) * amp * 1.2f;
            offset.x = Mathf.Cos(bobTimer * 0.5f) * amp * 1.6f;
        }
        else
        {
            // subtle breathing saat idle
            bobTimer += Time.deltaTime * idleBobFrequency;
            offset.y = Mathf.Sin(bobTimer) * idleBobAmount;
        }

        Vector3 targetPos = holderStartPos + offset;
        cameraHolder.localPosition = Vector3.Lerp(cameraHolder.localPosition, targetPos, bobSmooth * Time.deltaTime);
    }
    #endregion

    #region FOV
    void HandleFOV()
    {
        if (camComponent == null) return;

        bool isSprinting = inputActions.Player.Sprint.ReadValue<float>() > 0 && moveInput.magnitude > 0.1f;
        float targetFOV = isSprinting ? sprintFOV : baseFOV;
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * fovSmooth);
        camComponent.fieldOfView = currentFOV;
    }
    #endregion
}
