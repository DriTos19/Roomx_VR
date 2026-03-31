using UnityEngine;

public class MiniMapCamera : MonoBehaviour
{
    public Transform target;
    public float height = 20f;
    public bool rotateWithPlayer = false;

    void LateUpdate()
    {
        if (target == null) return;

        // Follow position
        transform.position = new Vector3(
            target.position.x,
            height,
            target.position.z
        );

        // Optional rotation
        if (rotateWithPlayer)
        {
            transform.rotation = Quaternion.Euler(
                90f,
                target.eulerAngles.y,
                0f
            );
        }
        else
        {
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}