using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SimpleFlyController : MonoBehaviour
{
    [Header("Mouse Look Settings")]
    public float lookSensitivity = 2f;
    public float lookSmoothing = 5f;
    public float maxPitch = 89f;

    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float fastMultiplier = 3f;

    float yaw;
    float pitch;
    Vector2 smoothLook;
    bool cursorLocked = true;

    void Start()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;

        LockCursor(true);
    }

    void Update()
    {
        HandleCursorToggle();
        if (cursorLocked)
        {
            HandleMouseLook();
            HandleMovement();
        }
    }

    void HandleCursorToggle()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            LockCursor(!cursorLocked);
    }

    void LockCursor(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void HandleMouseLook()
    {
        Vector2 mouseInput = new Vector2(
            Input.GetAxis("Mouse X"),
            Input.GetAxis("Mouse Y")
        );

        // Smooth mouse movement (optional)
        smoothLook = Vector2.Lerp(smoothLook, mouseInput, Time.deltaTime * lookSmoothing);

        yaw += smoothLook.x * lookSensitivity;
        pitch -= smoothLook.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0);
    }

    void HandleMovement()
    {
        float moveX = Input.GetAxisRaw("Horizontal");   // A/D
        float moveY = 0f;
        float moveZ = Input.GetAxisRaw("Vertical");     // W/S

        if (Input.GetKey(KeyCode.E)) moveY += 1f;       // Up
        if (Input.GetKey(KeyCode.Q)) moveY -= 1f;       // Down

        Vector3 move = new Vector3(moveX, moveY, moveZ).normalized;
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
        transform.position += transform.TransformDirection(move) * speed * Time.deltaTime;
    }
}