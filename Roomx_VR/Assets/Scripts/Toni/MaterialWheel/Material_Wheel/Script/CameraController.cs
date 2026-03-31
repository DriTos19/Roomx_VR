using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 4f;        // walking speed
    public float lookSpeed = 2f;        // mouse sensitivity
    public float playerHeight = 0f;     // y-position of camera (ground level)

    [Header("References")]
    public MaterialWheelController materialWheelController;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // freeze camera when wheel is open
        if (materialWheelController != null && materialWheelController.IsOpen())
            return;

        HandleMouseLook();
        HandleMovement();
    }

    void HandleMouseLook()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        rotationX += mouseDelta.x * lookSpeed * Time.deltaTime;
        rotationY -= mouseDelta.y * lookSpeed * Time.deltaTime;
        rotationY = Mathf.Clamp(rotationY, -80f, 80f); // prevents flipping

        transform.localEulerAngles = new Vector3(rotationY, rotationX, 0f);
    }

    void HandleMovement()
    {
        Vector3 move = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) move += transform.forward;
        if (Keyboard.current.sKey.isPressed) move -= transform.forward;
        if (Keyboard.current.aKey.isPressed) move -= transform.right;
        if (Keyboard.current.dKey.isPressed) move += transform.right;

        move = move.normalized; // prevent diagonal speed boost

        // Calculate new position but lock Y to playerHeight
        Vector3 newPos = transform.position + move * moveSpeed * Time.deltaTime;
        newPos.y = playerHeight; // lock height to ground
        transform.position = newPos;
    }
}