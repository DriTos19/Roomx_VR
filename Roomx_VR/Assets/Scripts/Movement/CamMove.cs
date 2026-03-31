using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CamMove : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f;
    
    private float xRotation = 0f;
    private float yRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        HandleMovement();
        HandleMouseRotation();
    }

    void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // Get forward direction but only on the horizontal plane (Y = 0)
        Vector3 forward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
        Vector3 right = transform.right;

        Vector3 move = (right * moveX + forward * moveZ) * moveSpeed * Time.deltaTime;
        transform.position += move;
    }

    void HandleMouseRotation()
    {
        // Only rotate camera when cursor is locked (menu is closed)
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Track both rotations
        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Apply both rotations together
        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0);
    }
}
