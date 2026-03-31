using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Rigidbody rb;
    private Vector3 moveDirection = Vector3.zero;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) Debug.LogWarning("PlayerMove: No Rigidbody found on the player.");
    }

    // Update is called once per frame
    void Update()
    {
        // Get input from WASD keys
        float horizontal = Input.GetAxis("Horizontal"); // A/D or Left/Right
        float vertical = Input.GetAxis("Vertical");     // W/S or Up/Down

        // Build movement in local space (x,z) and normalize
        Vector3 localMove = new Vector3(horizontal, 0f, vertical);
        if (localMove.magnitude > 1f) localMove.Normalize();

        // Store local move; we'll convert to world in FixedUpdate to apply to physics
        moveDirection = localMove;
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Convert local movement to world space based on player's current rotation
        Vector3 worldMove = transform.TransformDirection(moveDirection) * moveSpeed;

        // Preserve current vertical velocity (so gravity and jumps are not clobbered)
        float currentY = rb.velocity.y;

        rb.velocity = new Vector3(worldMove.x, currentY, worldMove.z);
    }
}
