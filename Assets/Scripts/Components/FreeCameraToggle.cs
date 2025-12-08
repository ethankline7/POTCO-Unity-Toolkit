using UnityEngine;

/// <summary>
/// Toggle between player camera and free-flying camera with Tab key
/// Uses the actual player camera so effects like ocean waves still work
/// </summary>
public class FreeCameraToggle : MonoBehaviour
{
    [Header("Toggle Key")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Header("Free Camera Settings")]
    [SerializeField] private float moveSpeed = 20f;
    [SerializeField] private float fastMoveSpeed = 50f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float smoothTime = 0.1f;

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private MonoBehaviour playerCameraScript; // PlayerCamera or FirstPersonController
    [SerializeField] private MonoBehaviour playerController; // PlayerController script

    private bool isInFreeCamera = false;
    private Vector3 freeCameraVelocity;
    private float freeCameraYaw;
    private float freeCameraPitch;

    // Saved player camera state
    private Transform savedPlayerCameraParent;
    private Vector3 savedPlayerCameraLocalPosition;
    private Quaternion savedPlayerCameraLocalRotation;

    private void Start()
    {
        // Auto-find player camera if not assigned
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // Auto-find player camera script
        if (playerCamera != null && playerCameraScript == null)
        {
            playerCameraScript = playerCamera.GetComponent<Player.PlayerCamera>();
            if (playerCameraScript == null)
            {
                playerCameraScript = playerCamera.GetComponent<FirstPersonController>();
            }
        }

        // Auto-find player controller
        if (playerController == null)
        {
            playerController = GetComponent<Player.PlayerController>();
            if (playerController == null)
            {
                playerController = GetComponent<FirstPersonController>();
            }
        }

        Debug.Log("✅ FreeCameraToggle initialized - Press Tab to toggle free camera");
    }

    private void Update()
    {
        // Toggle camera mode
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleCamera();
        }

        // Handle free camera controls
        if (isInFreeCamera)
        {
            HandleFreeCameraMovement();
            HandleFreeCameraRotation();
        }
    }

    private void ToggleCamera()
    {
        isInFreeCamera = !isInFreeCamera;

        if (isInFreeCamera)
        {
            // Switch TO free camera
            EnterFreeCamera();
        }
        else
        {
            // Switch BACK to player camera
            ExitFreeCamera();
        }
    }

    private void EnterFreeCamera()
    {
        Debug.Log("🎥 Entering free camera mode");

        // Save player camera parent and local transform
        savedPlayerCameraParent = playerCamera.transform.parent;
        savedPlayerCameraLocalPosition = playerCamera.transform.localPosition;
        savedPlayerCameraLocalRotation = playerCamera.transform.localRotation;

        // Initialize rotation angles from current camera rotation
        Vector3 eulerAngles = playerCamera.transform.eulerAngles;
        freeCameraYaw = eulerAngles.y;
        freeCameraPitch = eulerAngles.x;
        if (freeCameraPitch > 180f) freeCameraPitch -= 360f; // Normalize to -180 to 180

        // Unparent camera from player (makes it independent)
        playerCamera.transform.SetParent(null);

        // Disable PlayerCamera script so it doesn't try to control the camera
        if (playerCameraScript != null)
        {
            playerCameraScript.enabled = false;
        }

        // Freeze player movement
        if (playerController != null)
        {
            playerController.enabled = false;
        }

        // Lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ExitFreeCamera()
    {
        Debug.Log("🎥 Exiting free camera mode");

        // Re-parent camera back to player
        playerCamera.transform.SetParent(savedPlayerCameraParent);
        playerCamera.transform.localPosition = savedPlayerCameraLocalPosition;
        playerCamera.transform.localRotation = savedPlayerCameraLocalRotation;

        // Re-enable PlayerCamera script
        if (playerCameraScript != null)
        {
            playerCameraScript.enabled = true;
        }

        // Unfreeze player movement
        if (playerController != null)
        {
            playerController.enabled = true;
        }

        // Unlock cursor (let player camera script handle cursor state)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HandleFreeCameraMovement()
    {
        // Get input
        float horizontal = Input.GetAxis("Horizontal"); // A/D or Left/Right
        float vertical = Input.GetAxis("Vertical");     // W/S or Up/Down
        float upDown = 0f;

        // Q/E for up/down movement
        if (Input.GetKey(KeyCode.E)) upDown = 1f;
        if (Input.GetKey(KeyCode.Q)) upDown = -1f;

        // Calculate movement direction
        Vector3 moveDirection = Vector3.zero;
        moveDirection += playerCamera.transform.right * horizontal;
        moveDirection += playerCamera.transform.forward * vertical;
        moveDirection += Vector3.up * upDown;

        // Normalize to prevent faster diagonal movement
        if (moveDirection.magnitude > 1f)
        {
            moveDirection.Normalize();
        }

        // Apply speed (hold Shift for faster movement)
        float speed = Input.GetKey(KeyCode.LeftShift) ? fastMoveSpeed : moveSpeed;
        Vector3 targetVelocity = moveDirection * speed;

        // Smooth movement
        freeCameraVelocity = Vector3.Lerp(freeCameraVelocity, targetVelocity, smoothTime);

        // Apply movement to player camera
        playerCamera.transform.position += freeCameraVelocity * Time.deltaTime;
    }

    private void HandleFreeCameraRotation()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // Update rotation
        freeCameraYaw += mouseX * mouseSensitivity;
        freeCameraPitch -= mouseY * mouseSensitivity;

        // Clamp pitch to prevent flipping
        freeCameraPitch = Mathf.Clamp(freeCameraPitch, -89f, 89f);

        // Apply rotation to player camera
        playerCamera.transform.rotation = Quaternion.Euler(freeCameraPitch, freeCameraYaw, 0f);
    }
}
