using UnityEngine;

public class FurnitureInteractable : MonoBehaviour
{
    private float lastClickTime;
    private const float DOUBLE_CLICK_TIME = 0.3f;

    void OnMouseDown()
    {
        if (Time.time - lastClickTime < DOUBLE_CLICK_TIME)
        {
            // Nutzt die Instance aus PlacementManager
            PlacementManager.Instance.PickUpFurniture(gameObject);
        }
        lastClickTime = Time.time;
    }
}