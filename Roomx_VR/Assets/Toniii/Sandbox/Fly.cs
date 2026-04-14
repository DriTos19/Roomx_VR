using UnityEngine;

public class VRPlayerFloat : MonoBehaviour
{
    public float floatSpeed = 2f;
    public float deadzone = 0.2f;

    void Update()
    {
        if (WallPlacer_VR.Instance == null)
            return;

        // Only float when the player is NOT placing/editing/material-wheel state
        if (!WallPlacer_VR.Instance.CanMove)
            return;

        Vector2 input = WallPlacer_VR.Instance.MovementInput;

        float vertical = input.y;

        if (Mathf.Abs(vertical) < deadzone)
            return;

        transform.position += Vector3.up * vertical * floatSpeed * Time.deltaTime;
    }
}