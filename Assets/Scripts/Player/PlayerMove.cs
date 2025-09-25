using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PsychologicalHorrorFPSController : MonoBehaviour
{
    #region Movement Settings
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 2.8f;
    [SerializeField] private float sprintSpeed = 4.5f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float deceleration = 15f;
    [SerializeField] private float gravity = -12f;
    [SerializeField] private float groundStickForce = -2f;
    #endregion

    #region Camera Settings
    [Header("Camera Settings")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private Camera playerCamera;
    [SerializeField, Range(30f, 300f)] private float mouseSensitivity = 140f;
    [SerializeField] private float baseFOV = 65f;
    [SerializeField] private float sprintFOV = 72f;
    [SerializeField] private float fovTransitionSpeed = 6f;
    [SerializeField] private float minLookAngle = -80f;
    [SerializeField] private float maxLookAngle = 80f;
    
    private float xRotation = 0f;
    private float currentFOV;
    private Vector2 lookInput;
    #endregion

    #region Headbob Settings - Psychological Horror
    [Header("Horror Headbob - Subtle Immersion")]
    [SerializeField, Range(0.005f, 0.04f)] private float walkBobAmplitude = 0.018f;
    [SerializeField, Range(0.008f, 0.06f)] private float sprintBobAmplitude = 0.032f;
    [SerializeField, Range(2f, 8f)] private float walkBobFrequency = 4.2f;
    [SerializeField, Range(3f, 10f)] private float sprintBobFrequency = 6.8f;
    [SerializeField, Range(3f, 12f)] private float bobReturnSpeed = 8f;
    [SerializeField] private AnimationCurve bobCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -2f, 0f)
    );
    
    private Vector3 cameraStartPosition;
    private float bobTimer = 0f;
    private Vector3 currentBobOffset;
    #endregion

    #region Audio Footsteps (For Horror Atmosphere)
    [Header("Footstep Audio")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip[] walkFootsteps;
    [SerializeField] private AudioClip[] sprintFootsteps;
    [SerializeField, Range(0.1f, 1f)] private float footstepVolume = 0.6f;
    [SerializeField, Range(0.8f, 1.2f)] private float footstepPitchVariation = 0.15f;
    
    private float lastFootstepTime;
    private float footstepInterval;
    #endregion

    #region Input & Movement Variables
    private CharacterController controller;
    private InputSystem_Actions inputActions;
    private Vector2 moveInput;
    private Vector2 smoothMoveInput;
    private Vector3 velocity;
    private Vector3 currentVelocity;
    private bool isGrounded;
    private bool isSprinting;
    private bool isMoving;
    private float currentSpeed;
    #endregion

    #region Initialization
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputActions = new InputSystem_Actions();
        
        // Initialize cursor lock
        LockCursor();
        
        // Store camera start position for headbob
        if (cameraHolder != null)
        {
            cameraStartPosition = cameraHolder.localPosition;
            currentBobOffset = Vector3.zero;
        }
            
        // Initialize FOV
        if (playerCamera != null)
        {
            currentFOV = baseFOV;
            playerCamera.fieldOfView = currentFOV;
        }

        // Initialize footstep audio
        SetupFootstepAudio();
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
        
        // Bind input events
        inputActions.Player.Move.performed += OnMoveInput;
        inputActions.Player.Move.canceled += OnMoveInput;
        inputActions.Player.Look.performed += OnLookInput;
        inputActions.Player.Look.canceled += OnLookInput;
    }

    private void OnDisable()
    {
        inputActions?.Player.Disable();
    }

    private void SetupFootstepAudio()
    {
        if (footstepAudioSource == null && GetComponent<AudioSource>() != null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
        }
        
        if (footstepAudioSource != null)
        {
            footstepAudioSource.volume = footstepVolume;
            footstepAudioSource.spatialBlend = 0f; // 2D sound
        }
    }
    #endregion

    #region Input Handling
    private void OnMoveInput(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    private void OnLookInput(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }
    #endregion

    #region Main Update Loop
    private void Update()
    {
        HandleCursorControls();
        HandleMovement();
        HandleMouseLook();
        HandlePsychologicalHeadbob();
        HandleSprintFOV();
        HandleFootstepAudio();
    }
    #endregion

    #region Cursor Management
    private void HandleCursorControls()
    {
        // Unlock cursor with ESC
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }
        
        // Re-lock cursor with left mouse click
        if (Cursor.lockState == CursorLockMode.None && Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    #endregion

    #region Movement System - Smooth & Responsive
    private void HandleMovement()
    {
        // Ground check with small buffer
        isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = groundStickForce;
        }

        // Sprint input
        isSprinting = inputActions.Player.Sprint.ReadValue<float>() > 0f;
        
        // Smooth input for more natural feel
        float inputSmoothSpeed = moveInput.magnitude > 0.1f ? acceleration : deceleration;
        smoothMoveInput = Vector2.Lerp(smoothMoveInput, moveInput, inputSmoothSpeed * Time.deltaTime);
        
        // Check if moving
        isMoving = smoothMoveInput.magnitude > 0.1f;

        // Calculate target speed
        float targetSpeed = (isSprinting && isMoving) ? sprintSpeed : walkSpeed;
        currentSpeed = Mathf.Lerp(currentSpeed, isMoving ? targetSpeed : 0f, 
            (isMoving ? acceleration : deceleration) * Time.deltaTime);

        // Calculate movement direction
        Vector3 moveDirection = transform.right * smoothMoveInput.x + transform.forward * smoothMoveInput.y;
        moveDirection = moveDirection.normalized;

        // Apply horizontal movement with smooth acceleration/deceleration
        Vector3 targetVelocity = moveDirection * currentSpeed;
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 
            (isMoving ? acceleration : deceleration) * Time.deltaTime);

        // Combine with gravity
        Vector3 finalMovement = new Vector3(currentVelocity.x, velocity.y, currentVelocity.z);
        controller.Move(finalMovement * Time.deltaTime);
        
        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
    }
    #endregion

    #region Camera System - Precise & Responsive
    private void HandleMouseLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        // Get mouse delta with sensitivity
        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        // Horizontal rotation (yaw) - rotate the player body
        transform.Rotate(Vector3.up * mouseX);

        // Vertical rotation (pitch) - rotate the camera
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, minLookAngle, maxLookAngle);
        
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
    }
    #endregion

    #region Psychological Horror Headbob
    private void HandlePsychologicalHeadbob()
    {
        if (cameraHolder == null) return;

        if (isMoving && isGrounded && currentSpeed > 0.5f)
        {
            // Get bob parameters based on movement state
            float bobAmplitude = isSprinting ? sprintBobAmplitude : walkBobAmplitude;
            float bobFrequency = isSprinting ? sprintBobFrequency : walkBobFrequency;
            
            // Scale frequency with actual movement speed for more realistic feel
            float speedScale = currentSpeed / (isSprinting ? sprintSpeed : walkSpeed);
            bobFrequency *= speedScale;
            
            // Update bob timer
            bobTimer += Time.deltaTime * bobFrequency;
            
            // Calculate bob offset using curve for natural footstep feel
            float bobPhase = Mathf.Repeat(bobTimer, 1f);
            float curveValue = bobCurve.Evaluate(bobPhase);
            
            // Create subtle bob with slight horizontal sway for realism
            Vector3 targetBobOffset = new Vector3(
                Mathf.Sin(bobTimer * 0.5f) * bobAmplitude * 0.3f,  // Subtle horizontal sway
                curveValue * bobAmplitude,                          // Main vertical bob
                0f
            );
            
            currentBobOffset = Vector3.Lerp(currentBobOffset, targetBobOffset, 
                bobReturnSpeed * Time.deltaTime);
        }
        else
        {
            // Smoothly return to center when not moving
            currentBobOffset = Vector3.Lerp(currentBobOffset, Vector3.zero, 
                bobReturnSpeed * Time.deltaTime);
            
            // Reset timer gradually to avoid jarring transitions
            bobTimer = Mathf.Lerp(bobTimer, 0f, Time.deltaTime * 2f);
        }

        // Apply bob offset to camera
        cameraHolder.localPosition = cameraStartPosition + currentBobOffset;
    }
    #endregion

    #region Sprint FOV System
    private void HandleSprintFOV()
    {
        if (playerCamera == null) return;

        // FOV increases when sprinting and moving for intensity
        float targetFOV = (isSprinting && isMoving && currentSpeed > walkSpeed * 1.2f) ? sprintFOV : baseFOV;
        
        // Smooth FOV transition
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, fovTransitionSpeed * Time.deltaTime);
        playerCamera.fieldOfView = currentFOV;
    }
    #endregion

    #region Footstep Audio System
    private void HandleFootstepAudio()
    {
        if (!isMoving || !isGrounded || footstepAudioSource == null) return;

        // Calculate footstep interval based on movement speed and type
        float baseInterval = isSprinting ? 0.4f : 0.6f;
        footstepInterval = baseInterval / (currentSpeed / walkSpeed);

        // Play footstep sound at appropriate intervals
        if (Time.time - lastFootstepTime >= footstepInterval)
        {
            PlayFootstepSound();
            lastFootstepTime = Time.time;
        }
    }

    private void PlayFootstepSound()
    {
        AudioClip[] footstepClips = isSprinting ? sprintFootsteps : walkFootsteps;
        
        if (footstepClips != null && footstepClips.Length > 0)
        {
            // Pick random footstep sound
            AudioClip clipToPlay = footstepClips[Random.Range(0, footstepClips.Length)];
            
            if (clipToPlay != null)
            {
                // Add slight pitch variation for realism
                footstepAudioSource.pitch = Random.Range(1f - footstepPitchVariation, 1f + footstepPitchVariation);
                footstepAudioSource.PlayOneShot(clipToPlay, footstepVolume);
            }
        }
    }
    #endregion

    #region Public Properties
    public bool IsMoving => isMoving;
    public bool IsSprinting => isSprinting && isMoving && currentSpeed > walkSpeed * 1.2f;
    public bool IsGrounded => isGrounded;
    public float CurrentSpeed => currentSpeed;
    public float MovementProgress => currentSpeed / sprintSpeed;
    #endregion

    #region Editor Validation
    #if UNITY_EDITOR
    private void OnValidate()
    {
        // Ensure movement values are sensible
        walkSpeed = Mathf.Max(0.5f, walkSpeed);
        sprintSpeed = Mathf.Max(walkSpeed + 0.5f, sprintSpeed);
        acceleration = Mathf.Max(1f, acceleration);
        deceleration = Mathf.Max(1f, deceleration);
        
        // Ensure camera values are reasonable
        mouseSensitivity = Mathf.Max(10f, mouseSensitivity);
        baseFOV = Mathf.Clamp(baseFOV, 40f, 100f);
        sprintFOV = Mathf.Clamp(sprintFOV, baseFOV, 100f);
        
        // Ensure headbob values are subtle for horror
        walkBobAmplitude = Mathf.Clamp(walkBobAmplitude, 0.005f, 0.04f);
        sprintBobAmplitude = Mathf.Clamp(sprintBobAmplitude, walkBobAmplitude, 0.06f);
        
        // Ensure footstep volume is reasonable
        footstepVolume = Mathf.Clamp01(footstepVolume);
    }

    private void Reset()
    {
        // Create default bob curve for natural footstep feeling
        bobCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f),
            new Keyframe(0.5f, 1f, 0f, 0f),
            new Keyframe(1f, 0f, -2f, 0f)
        );
    }
    #endif
    #endregion
}