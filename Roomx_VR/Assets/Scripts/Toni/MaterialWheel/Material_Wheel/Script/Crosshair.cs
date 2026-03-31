using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class CrosshairSelector : MonoBehaviour
{
    public MaterialWheelController materialWheelController;
    public float maxDistance = 10f;

    void Update()
    {
        if (materialWheelController == null) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = new Ray(transform.position, transform.forward);
        Debug.DrawRay(transform.position, transform.forward * maxDistance, Color.red);

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            {
                Renderer r = hit.collider.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    materialWheelController.SelectObject(r);
                    materialWheelController.OpenWheel(0);
                }
            }
        }
    }
}