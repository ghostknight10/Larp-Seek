using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Simple First-Person WASD + Mouse Look controller for roaming around the cafeteria map.
/// Press Escape to unlock the cursor, click anywhere in the Game view to lock it again.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonRoam : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 5.5f;
    public float sprintSpeed = 9.5f;
    public float jumpHeight = 1.2f;
    public float gravity = -19.6f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 0.15f;
    public Transform cameraTransform;

    [Header("Third-Person Mode (when morphed)")]
    public bool isThirdPerson = false;
    public float thirdPersonDistance = 3.2f;
    public float thirdPersonHeight = 1.2f;
    public Vector3 firstPersonEyeOffset = new Vector3(0f, 1.62f, 0f);

    [Header("Control Gates")]
    public bool inputEnabled = true;
    public bool autoManageCursor = true;
    [Tooltip("Look still works but WASD/jump are ignored (downed spectators, Hunter during hide phase).")]
    public bool movementLocked = false;

    // Spectate / free-look: the camera looks around on its own yaw while the body (and any prop
    // disguise) keeps its rotation, so other players see no movement at all.
    bool _freeLook;
    float _freeYaw;
    float _planarSpeed;
    public bool IsFreeLook => _freeLook;
    public bool IsStationary => _cc != null && _cc.isGrounded && _planarSpeed < 0.15f;

    public void SetFreeLook(bool on)
    {
        if (_freeLook == on) return;
        _freeLook = on;
        if (!on) _freeYaw = 0f;
    }

    // Spectator fly-cam (downed Hiders): the camera detaches from the body and flies freely with
    // WASD (+ Space / Ctrl for up / down, Shift to go faster). The body itself never moves.
    [Header("Spectator Fly-Cam")]
    public float flySpeed = 7f;
    public float flyFastSpeed = 16f;
    bool _fly;
    Vector3 _flyPos;
    float _flyYaw;
    float _flyStartY;
    public bool IsSpectatorFlying => _fly;

    public void SetSpectatorFly(bool on)
    {
        if (_fly == on) return;
        _fly = on;
        if (on)
        {
            _freeLook = false;
            if (cameraTransform != null)
            {
                _flyPos = cameraTransform.position;
                _flyYaw = cameraTransform.eulerAngles.y;
            }
            else
            {
                _flyPos = transform.position + Vector3.up * 1.6f;
                _flyYaw = transform.eulerAngles.y;
            }
            _flyStartY = _flyPos.y;
        }
        else
        {
            _freeYaw = 0f;
        }
        UpdateCameraTransform();
    }

    CharacterController _cc;
    float _pitch;
    float _verticalVelocity;
    bool _cursorLocked = false;

    public bool IsCursorLocked => _cursorLocked;
    public float VerticalVelocity => _verticalVelocity;

    public void Bounce(float force)
    {
        _verticalVelocity = force;
    }

    public void SetSpeed(float walk, float sprint)
    {
        walkSpeed = walk;
        sprintSpeed = sprint;
    }

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        if (cameraTransform == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }
        // Retro look: no anti-aliasing of any kind on the player camera.
        RetroScreenEffect.ConfigureCamera(GetComponentInChildren<Camera>(true));
        if (MultiplayerSessionManager.Instance == null && inputEnabled && autoManageCursor)
            SetCursorLock(true);
    }

    void Update()
    {
        if (!inputEnabled) return;

        if (autoManageCursor)
            HandleCursorToggle();
        if (_cursorLocked)
            HandleLook();
        HandleMove();
    }

    void HandleCursorToggle()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLock(false);
        else if (!_cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            SetCursorLock(true);
#else
        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLock(false);
        else if (!_cursorLocked && Input.GetMouseButtonDown(0))
            SetCursorLock(true);
#endif
    }

    public void SetCursorLock(bool locked)
    {
        _cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void HandleLook()
    {
        Vector2 delta = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            delta = Mouse.current.delta.ReadValue() * mouseSensitivity;
#else
        delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * (mouseSensitivity * 12f);
#endif
        if (_fly)
            _flyYaw = Mathf.Repeat(_flyYaw + delta.x, 360f);
        else if (_freeLook)
            _freeYaw = Mathf.Repeat(_freeYaw + delta.x + 180f, 360f) - 180f;
        else
            transform.Rotate(Vector3.up * delta.x);
        _pitch = Mathf.Clamp(_pitch - delta.y, -75f, 80f);
        UpdateCameraTransform();
    }

    void LateUpdate()
    {
        UpdateCameraTransform();
    }

    void UpdateCameraTransform()
    {
        if (cameraTransform == null) return;

        if (_fly)
        {
            cameraTransform.SetPositionAndRotation(_flyPos, Quaternion.Euler(_pitch, _flyYaw, 0f));
            return;
        }

        if (!isThirdPerson)
        {
            cameraTransform.localPosition = firstPersonEyeOffset;
            cameraTransform.localRotation = Quaternion.Euler(_pitch, _freeYaw, 0f);
        }
        else
        {
            Quaternion orbitRot = Quaternion.Euler(_pitch, _freeYaw, 0f);
            Vector3 pivotLocal = new Vector3(0f, thirdPersonHeight, 0f);
            Vector3 desiredLocalPos = pivotLocal + orbitRot * new Vector3(0f, 0f, -thirdPersonDistance);

            // Prevent camera from clipping behind walls
            Vector3 worldPivot = transform.TransformPoint(pivotLocal);
            Vector3 worldDesired = transform.TransformPoint(desiredLocalPos);
            Vector3 dir = worldDesired - worldPivot;
            if (Physics.SphereCast(worldPivot, 0.18f, dir.normalized, out RaycastHit hit, dir.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform))
                {
                    float safeDist = Mathf.Max(0.4f, hit.distance - 0.15f);
                    desiredLocalPos = pivotLocal + orbitRot * new Vector3(0f, 0f, -safeDist);
                }
            }

            cameraTransform.localPosition = desiredLocalPos;
            cameraTransform.localRotation = orbitRot;
        }
    }

    void HandleMove()
    {
        Vector2 input = Vector2.zero;
        bool sprint = false;
        bool jump = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  input.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  input.x -= 1f;
            sprint = kb.leftShiftKey.isPressed;
            jump = kb.spaceKey.wasPressedThisFrame;
        }
#else
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
        sprint = Input.GetKey(KeyCode.LeftShift);
        jump = Input.GetKeyDown(KeyCode.Space);
#endif

        if (input.sqrMagnitude > 1f) input.Normalize();

        if (_fly)
        {
            float up = 0f;
#if ENABLE_INPUT_SYSTEM
            if (kb != null)
            {
                if (kb.spaceKey.isPressed || kb.eKey.isPressed) up += 1f;
                if (kb.leftCtrlKey.isPressed || kb.cKey.isPressed || kb.qKey.isPressed) up -= 1f;
            }
#else
            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E)) up += 1f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.Q)) up -= 1f;
#endif
            Quaternion look = Quaternion.Euler(_pitch, _flyYaw, 0f);
            Vector3 dir = look * new Vector3(input.x, 0f, input.y) + Vector3.up * up;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            _flyPos += dir * (sprint ? flyFastSpeed : flySpeed) * Time.deltaTime;
            _flyPos.y = Mathf.Clamp(_flyPos.y, _flyStartY - 8f, _flyStartY + 35f);
            // The body stays put (it only settles under gravity).
            input = Vector2.zero;
            jump = false;
        }

        if (movementLocked || _freeLook)
        {
            input = Vector2.zero;
            jump = false;
        }

        float speed = sprint ? sprintSpeed : walkSpeed;
        Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

        if (_cc.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        if (_cc.isGrounded && jump)
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _verticalVelocity += gravity * Time.deltaTime;
        move.y = _verticalVelocity;

        _planarSpeed = new Vector2(move.x, move.z).magnitude;
        _cc.Move(move * Time.deltaTime);
    }
}
