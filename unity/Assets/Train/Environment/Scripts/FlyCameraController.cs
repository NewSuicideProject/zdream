using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class FlyCameraController_NewInput : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float boostMultiplier = 3f;
    [SerializeField] private float verticalSpeed = 10f;

    [Header("Look (FPS-like)")]
    [Tooltip("발로란트 감도 느낌으로 숫자 하나로 조절 (0.05~0.5 추천)")]
    [SerializeField] private float lookSensitivity = 0.18f;
    [SerializeField] private float maxPitch = 89f;

    [Header("Options")]
    [SerializeField] private bool lockCursorOnPlay = true;
    [SerializeField] private bool requireRightMouseToLook = false; // 원하면 true로
    [SerializeField] private float mouseDeltaScale = 0.02f; // 마우스 델타 단위 보정(기본 유지)

    private float pitch;

    private void OnEnable()
    {
        if (lockCursorOnPlay)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        pitch = NormalizePitch(transform.eulerAngles.x);
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        HandleCursorToggle();
        HandleLook();
        HandleMove();
    }

    private void HandleCursorToggle()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void HandleLook()
    {
        if (requireRightMouseToLook && !Mouse.current.rightButton.isPressed)
            return;

        // New Input System: Mouse delta는 보통 픽셀 단위에 가까움
        Vector2 delta = Mouse.current.delta.ReadValue();

        float yawDelta = delta.x * lookSensitivity * mouseDeltaScale;
        float pitchDelta = delta.y * lookSensitivity * mouseDeltaScale;

        // yaw (좌우)
        transform.Rotate(0f, yawDelta, 0f, Space.World);

        // pitch (상하)
        pitch -= pitchDelta;
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        var e = transform.eulerAngles;
        transform.eulerAngles = new Vector3(pitch, e.y, 0f);
    }

    private void HandleMove()
    {
        Vector3 move = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) move += transform.forward;
        if (Keyboard.current.sKey.isPressed) move -= transform.forward;
        if (Keyboard.current.dKey.isPressed) move += transform.right;
        if (Keyboard.current.aKey.isPressed) move -= transform.right;

        // 대각선 보정
        if (move.sqrMagnitude > 1f) move.Normalize();

        float up = 0f;
        if (Keyboard.current.spaceKey.isPressed) up += 1f;
        if (Keyboard.current.leftCtrlKey.isPressed) up -= 1f;

        float speed = moveSpeed;
        if (Keyboard.current.leftShiftKey.isPressed) speed *= boostMultiplier;

        Vector3 velocity = move * speed + Vector3.up * (up * verticalSpeed);
        transform.position += velocity * Time.unscaledDeltaTime;
    }

    private static float NormalizePitch(float x)
    {
        if (x > 180f) x -= 360f;
        return x;
    }
}
