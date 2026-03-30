using UnityEngine;

public class FurnitureInteractable : MonoBehaviour
{
    private float lastClickTime;
    private float doubleClickTime = 0.25f;

    void OnMouseDown()
    {
        if (Time.time - lastClickTime < doubleClickTime)
        {
            PlacementManager.Instance.PickUpFurniture(gameObject);
        }

        lastClickTime = Time.time;
    }
}