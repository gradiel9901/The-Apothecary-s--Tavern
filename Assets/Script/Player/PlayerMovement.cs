using UnityEngine;
using UnityEngine.InputSystem;
using Script.Environment;

namespace Script.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 2.0f;
        [SerializeField] private float runSpeed = 5.0f;
        [SerializeField] private float rotationSpeed = 10.0f;

        [Header("Camera Settings")]
        [SerializeField] private float cameraSensitivity = 1.0f;
        [SerializeField] private LayerMask cameraCollisionLayers;
        
        // Internal Camera Properties
        private Transform _mainCamera;
        private Vector2 _lookInput;
        private float _yaw;
        private float _pitch;
        private float _currentDistance = 4.0f;
        private float _targetDistance = 4.0f;
        private Vector3 _cameraPositionVelocity;
        private Vector3 _cameraRotationVelocity;
        
        // Constants for camera feel
        private const float DefaultCamDistance = 4.0f;
        private const float MinCamDistance = 0.5f;
        private const float MaxCamDistance = 10.0f;
        private const float MinCamPitch = -30f;
        private const float MaxCamPitch = 70f;
        private const float CamRadius = 0.2f;

        [Header("Animation Settings")]
        [SerializeField] private Animator animator;
        [SerializeField] private string idleAnimName = "Idle";
        [SerializeField] private string walkAnimName = "Walking";
        [SerializeField] private string runAnimName = "Running";
        [SerializeField] private string carryingIdleAnim = "Carrying_Idle";
        [SerializeField] private string carryingWalkAnim = "Carrying_Walking";
        [SerializeField] private string carryingRunAnim = "Carrying_Run"; 
        [SerializeField] private float transitionDuration = 0.1f;

        private CharacterController _characterController;
        private InputSystem_Actions _inputActions;
        private Vector2 _moveInput;
        private bool _isSprinting;
        private bool _isCarrying;
        private string _currentAnim;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _inputActions = new InputSystem_Actions();
            
            if (Camera.main != null)
            {
                _mainCamera = Camera.main.transform;
                // Check if camera is ANYWHERE inside the player hierarchy (e.g. attached to a bone)
                if (_mainCamera.IsChildOf(transform))
                {
                    _mainCamera.SetParent(null); 
                }
                
                Vector3 euler = _mainCamera.eulerAngles;
                _yaw = euler.y;
                _pitch = euler.x;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnEnable()
        {
            _inputActions.Player.Enable();
            _inputActions.Player.Move.performed += OnMove;
            _inputActions.Player.Move.canceled += OnMove;
            _inputActions.Player.Look.performed += OnLook;
            _inputActions.Player.Look.canceled += OnLook;
            _inputActions.Player.Sprint.performed += OnSprint;
            _inputActions.Player.Sprint.canceled += OnSprint;
        }

        private void OnDisable()
        {
            _inputActions.Player.Move.performed -= OnMove;
            _inputActions.Player.Move.canceled -= OnMove;
            _inputActions.Player.Look.performed -= OnLook;
            _inputActions.Player.Look.canceled -= OnLook;
            _inputActions.Player.Sprint.performed -= OnSprint;
            _inputActions.Player.Sprint.canceled -= OnSprint;
            _inputActions.Player.Disable();
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            _moveInput = context.ReadValue<Vector2>();
        }

        private void OnLook(InputAction.CallbackContext context)
        {
            _lookInput = context.ReadValue<Vector2>();
        }

        private void OnSprint(InputAction.CallbackContext context)
        {
            _isSprinting = context.ReadValueAsButton();
        }

        public void SetCarrying(bool isCarrying)
        {
            _isCarrying = isCarrying;
        }

        public void TogglePlayerInput(bool enable)
        {
            // Toggle cursor state
            Cursor.lockState = enable ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enable;

            // Enable/Disable input actions
            if (enable)
            {
                _inputActions.Player.Enable();
            }
            else
            {
                _inputActions.Player.Disable();
                // Clear any lingering input to stop runaway movement
                _moveInput = Vector2.zero; 
                _lookInput = Vector2.zero;
            }
        }

        private void Update()
        {
            HandleMovement();
            HandleAnimation();
        }

        private void LateUpdate()
        {
            HandleCameraLogic();
        }

        private void HandleMovement()
        {
            if (_moveInput.sqrMagnitude == 0) return;

            // Apply EventManager slow debuff if active
            float eventSpeedMultiplier = EventManager.Instance != null ? EventManager.Instance.GetPlayerSpeedMultiplier() : 1.0f;
            
            float targetSpeed = _isSprinting ? (runSpeed * eventSpeedMultiplier) : (walkSpeed * eventSpeedMultiplier);

            // Apply carry speed logic
            Vector3 moveDirection = Vector3.zero;

            // Camera-relative movement
            if (_mainCamera != null)
            {
                Vector3 cameraForward = _mainCamera.forward;
                Vector3 cameraRight = _mainCamera.right;

                cameraForward.y = 0;
                cameraRight.y = 0;
                cameraForward.Normalize();
                cameraRight.Normalize();

                moveDirection = (cameraForward * _moveInput.y + cameraRight * _moveInput.x).normalized;
            }
            else
            {
                moveDirection = new Vector3(_moveInput.x, 0, _moveInput.y).normalized;
            }

            // Only rotate player character if we are actually moving
            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Move
            _characterController.SimpleMove(moveDirection * targetSpeed);
        }

        private void HandleCameraLogic()
        {
            if (_mainCamera == null) return;

            // Orbit Input (reduced raw speed so it doesn't spin wildly)
            float lookSpeed = 0.5f; 
            _yaw += _lookInput.x * cameraSensitivity * lookSpeed;
            _pitch -= _lookInput.y * cameraSensitivity * lookSpeed;
            _pitch = Mathf.Clamp(_pitch, MinCamPitch, MaxCamPitch);

            // Keep Yaw wrapped between 0 and 360 properly
            if (_yaw < 0f) _yaw += 360f;
            if (_yaw >= 360f) _yaw -= 360f;

            Vector3 targetOffset = new Vector3(0, 1.5f, 0); // Aiming at character's upper body / head
            Vector3 targetPosWithOffset = transform.position + targetOffset;

            // Calculate desired unoccluded position
            Vector3 desiredPosition = CalculateCameraPosition(_yaw, _pitch, DefaultCamDistance, targetPosWithOffset);
            
            // Spherecast to check for wall collisions
            Vector3 direction = desiredPosition - targetPosWithOffset;
            float maxDist = direction.magnitude;

            if (Physics.SphereCast(targetPosWithOffset, CamRadius, direction.normalized, out RaycastHit hit, maxDist, cameraCollisionLayers))
            {
                _targetDistance = Mathf.Clamp(hit.distance - CamRadius, MinCamDistance, MaxCamDistance);
            }
            else
            {
                _targetDistance = DefaultCamDistance;
            }

            // Smooth approach distance
            if (_targetDistance < _currentDistance)
                _currentDistance = _targetDistance; // Snap in quickly to prevent clipping
            else
                _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, Time.deltaTime * 5f); // Smooth out

            // Final Apply Position
            Vector3 finalPosition = CalculateCameraPosition(_yaw, _pitch, _currentDistance, targetPosWithOffset);
            
            // Snap position instantly so it perfectly follows the running player without lag
            _mainCamera.position = finalPosition;

            // Set the rotation directly without SmoothDamp on Eulers (which causes wild spinning at 360 boundaries)
            // The position is smoothly moving in an arc, so instantly looking at the target handles rotation beautifully.
            _mainCamera.rotation = Quaternion.LookRotation(targetPosWithOffset - _mainCamera.position);
        }

        private Vector3 CalculateCameraPosition(float yaw, float pitch, float distance, Vector3 centerTarget)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            return centerTarget - (rotation * Vector3.forward * distance);
        }

        private void HandleAnimation()
        {
            if (animator == null) return;

            string targetAnim = "";
            if (_moveInput.sqrMagnitude > 0.05f) 
            {
                if (_isSprinting)
                {
                    targetAnim = _isCarrying ? carryingRunAnim : runAnimName;
                }
                else
                {
                    targetAnim = _isCarrying ? carryingWalkAnim : walkAnimName;
                }
            }
            else
            {
                targetAnim = _isCarrying ? carryingIdleAnim : idleAnimName;
            }

            PlayAnimation(targetAnim);
        }

        private void PlayAnimation(string newAnim)
        {
            if (_currentAnim == newAnim) return;

            animator.CrossFadeInFixedTime(newAnim, transitionDuration);
            _currentAnim = newAnim;
        }
    }
}
