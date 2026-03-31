using UnityEngine;

public class SimpleMovement : MonoBehaviour
{
    public float speed = 5f;

    void Update()
    {
        float x = Input.GetAxis("Horizontal"); // A / D
        float z = Input.GetAxis("Vertical");   // W / S

        Vector3 movement = new Vector3(x, 0f, z);
        transform.Translate(movement * (speed * Time.deltaTime), Space.World);
    }
}