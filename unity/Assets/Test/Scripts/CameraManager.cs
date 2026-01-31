using UnityEngine;
using UnityEngine.InputSystem;

namespace Test.Scripts {
    public class CameraManager : MonoBehaviour {
        [SerializeField] private InputActionAsset inputActions;

        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float fastMoveMultiplier = 5f;
        [SerializeField] private float lookSensitivity = 0.1f;
        [SerializeField] private float minVerticalAngle = -80f;
        [SerializeField] private float maxVerticalAngle = 80f;

        private InputAction _downAction;
        private InputAction _fastMoveAction;
        private InputAction _lookAction;
        private InputAction _moveAction;
        private float _rotationX;
        private float _rotationY;
        private InputAction _upAction;

        private void Awake() {
            if (!inputActions) {
                return;
            }

            InputActionMap cameraMap = inputActions.FindActionMap("Camera");

            _moveAction = cameraMap?.FindAction("Move");
            _lookAction = cameraMap?.FindAction("Look");
            _upAction = cameraMap?.FindAction("Up");
            _downAction = cameraMap?.FindAction("Down");
            _fastMoveAction = cameraMap?.FindAction("FastMove");
        }

        private void Update() {
            HandleMovement();
            HandleRotation();

            if (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) {
                ToggleCursor();
            }
        }

        private void OnEnable() {
            _moveAction?.Enable();
            _lookAction?.Enable();
            _upAction?.Enable();
            _downAction?.Enable();
            _fastMoveAction?.Enable();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable() {
            _moveAction?.Disable();
            _lookAction?.Disable();
            _upAction?.Disable();
            _downAction?.Disable();
            _fastMoveAction?.Disable();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void HandleMovement() {
            Vector3 movement = Vector3.zero;

            if (_moveAction != null) {
                Vector2 moveInput = _moveAction.ReadValue<Vector2>();
                movement += transform.right * moveInput.x;
                movement += transform.forward * moveInput.y;
            }

            if (_upAction != null && _upAction.IsPressed()) {
                movement += Vector3.up;
            }

            if (_downAction != null && _downAction.IsPressed()) {
                movement += Vector3.down;
            }

            float currentSpeed = moveSpeed;
            if (_fastMoveAction != null && _fastMoveAction.IsPressed()) {
                currentSpeed *= fastMoveMultiplier;
            }

            transform.position += movement.normalized * (currentSpeed * Time.deltaTime);
        }

        private void HandleRotation() {
            if (_lookAction == null) {
                return;
            }

            Vector2 lookInput = _lookAction.ReadValue<Vector2>();

            _rotationX += lookInput.x * lookSensitivity;
            _rotationY -= lookInput.y * lookSensitivity;

            _rotationY = Mathf.Clamp(_rotationY, minVerticalAngle, maxVerticalAngle);

            transform.rotation = Quaternion.Euler(_rotationY, _rotationX, 0f);
        }

        private static void ToggleCursor() {
            if (Cursor.lockState == CursorLockMode.Locked) {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            } else {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
