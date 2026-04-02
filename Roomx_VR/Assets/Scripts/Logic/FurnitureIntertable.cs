using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class FurnitureInteractable : MonoBehaviour
{
    private float lastClickTime = -999f;
    private const float DOUBLE_CLICK_TIME = 0.35f;

    void Update()
    {
        if (PlacementManager.Instance == null) return;
        if (PlacementManager.Instance.IsCarryingObject) return;
        if (InventoryManager.IsMenuOpen()) return;

        XRRayInteractor rayInteractor = PlacementManager.Instance.rayInteractor;
        if (rayInteractor == null) return;

        var triggerAction = PlacementManager.Instance.triggerPress.action;
        if (triggerAction == null) return;

        if (triggerAction.WasPressedThisFrame())
        {
            CheckForPickup(rayInteractor);
        }
    }

    private void CheckForPickup(XRRayInteractor interactor)
    {
        if (!interactor.TryGetCurrent3DRaycastHit(out RaycastHit hit)) return;

        bool hitThis = hit.collider.gameObject == gameObject ||
                       hit.collider.transform.IsChildOf(transform);

        if (!hitThis) return;

        float timeSinceLastClick = Time.time - lastClickTime;

        if (timeSinceLastClick <= DOUBLE_CLICK_TIME)
        {
            // Double click confirmed — pick up
            PlacementManager.Instance.PickUpFurniture(gameObject);
            lastClickTime = -999f; // reset
        }
        else
        {
            lastClickTime = Time.time;
        }
    }
}