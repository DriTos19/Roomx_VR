using UnityEngine;
public class MiniMapFollow : MonoBehaviour
{
    public Transform player;
    public float height = 20f;

    void LateUpdate()
    {
        // Get the player's current position
        Vector3 pos = player.position;
        // Set the Y position to the fixed height
        pos.y = height;
        // Apply the new position to this object
        transform.position = pos;
    }
}