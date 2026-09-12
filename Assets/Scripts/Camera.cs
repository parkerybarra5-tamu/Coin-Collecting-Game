using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleThirdPersonCamera : MonoBehaviour
{
    [Header("Tracking")]
    [Tooltip("The player or target object the camera will orbit around.")]
    public Transform target;
    [Tooltip("Offset from the target's pivot (e.g., look at character's head/chest instead of feet).")]
    public Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);
    public float distance = 5.0f;

    [Header("Rotation Settings")]
    public float sensitivityX = 0.2f;
    public float sensitivityY = 0.2f;
    public float minVerticalAngle = -20f;
    public float maxVerticalAngle = 80f;

    [Header("Input Settings")]
    [Tooltip("Configure your look input bindings directly here in the Inspector.")]
    public InputAction lookAction;

    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    private void OnEnable()
    {
        // Enable the self-contained input action directly
        lookAction?.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        lookAction?.Disable();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Read Vector2 input directly from our embedded action
        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        _cinemachineTargetYaw += lookInput.x * sensitivityX;
        _cinemachineTargetPitch -= lookInput.y * sensitivityY;

        _cinemachineTargetPitch = Mathf.Clamp(_cinemachineTargetPitch, minVerticalAngle, maxVerticalAngle);

        Vector3 targetPosition = target.position + targetOffset;
        Quaternion rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0.0f);
        Vector3 position = targetPosition - (rotation * Vector3.forward * distance);

        transform.rotation = rotation;
        transform.position = position;
    }

    /// <summary>
    /// Returns the forward vector of the camera projected onto a flat horizontal plane (Y = 0).
    /// </summary>
    /// <summary>
    /// Returns the horizontal forward direction of the camera mapped to a Vector2 (X, Y representing World X, Z).
    /// </summary>
    public Vector2 GetFlatForward()
    {
        Vector3 forward = transform.forward;
        // Project onto XZ plane and normalize the 2D vector
        Vector2 flatFwd = new Vector2(forward.x, forward.z);
        return flatFwd.normalized;
    }

    /// <summary>
    /// Returns the horizontal right direction of the camera mapped to a Vector2 (X, Y representing World X, Z).
    /// </summary>
    public Vector2 GetFlatRight()
    {
        Vector3 right = transform.right;
        // Project onto XZ plane and normalize the 2D vector
        Vector2 flatRight = new Vector2(right.x, right.z);
        return flatRight.normalized;
    }
    public void SetCameraTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
