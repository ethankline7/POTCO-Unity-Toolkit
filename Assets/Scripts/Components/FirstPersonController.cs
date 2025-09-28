using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 6.0f;
    public float runSpeed = 12.0f;
    public float mouseSensitivity = 2.0f;

    [Header("Jump Settings")]
    public float jumpHeight = 8.0f;
    public float gravity = -9.81f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask = -1;

    [Header("Auto-Setup")]
    public bool autoAddMeshColliders = true;
    public float cameraFarClipPlane = 10000f;

    private CharacterController controller;
    private Camera playerCamera;
    private Vector3 velocity;
    private bool isGrounded;
    private float xRotation = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera != null)
        {
            playerCamera.farClipPlane = cameraFarClipPlane;
        }

        Cursor.lockState = CursorLockMode.Locked;

        if (autoAddMeshColliders)
        {
            AddMeshCollidersToProps();
        }

        SetupGroundCheck();
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();
        HandleJump();
    }

    void HandleMouseLook()
    {
        if (playerCamera == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMovement()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        controller.Move(move * currentSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    void SetupGroundCheck()
    {
        if (groundCheck == null)
        {
            GameObject groundCheckObj = new GameObject("GroundCheck");
            groundCheckObj.transform.SetParent(transform);
            groundCheckObj.transform.localPosition = new Vector3(0, -1f, 0);
            groundCheck = groundCheckObj.transform;
        }
    }

    void AddMeshCollidersToProps()
    {
        POTCO.ObjectListInfo[] objectListInfos = FindObjectsByType<POTCO.ObjectListInfo>(FindObjectsSortMode.None);
        int colliderCount = 0;

        foreach (POTCO.ObjectListInfo objectInfo in objectListInfos)
        {
            if (objectInfo.GetComponent<Collider>() == null)
            {
                MeshFilter meshFilter = objectInfo.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    MeshCollider meshCollider = objectInfo.gameObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshFilter.sharedMesh;
                    meshCollider.convex = false;
                    colliderCount++;
                }
                else
                {
                    MeshFilter[] childMeshFilters = objectInfo.GetComponentsInChildren<MeshFilter>();
                    foreach (MeshFilter childMeshFilter in childMeshFilters)
                    {
                        if (childMeshFilter.GetComponent<Collider>() == null && childMeshFilter.sharedMesh != null)
                        {
                            MeshCollider meshCollider = childMeshFilter.gameObject.AddComponent<MeshCollider>();
                            meshCollider.sharedMesh = childMeshFilter.sharedMesh;
                            meshCollider.convex = false;
                            colliderCount++;
                        }
                    }
                }
            }
        }

        Debug.Log($"FirstPersonController: Added {colliderCount} mesh colliders to world props");
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }

    public void ToggleCursor()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}