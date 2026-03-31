using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class FurnitureInteractable : MonoBehaviour
{
    private float lastClickTime;
    private const float DOUBLE_CLICK_TIME = 0.3f;

    void Update()
    {
        // Prüfen, ob der Manager und die benötigten Referenzen existieren
        if (PlacementManager.Instance == null) return;
        if (PlacementManager.Instance.IsCarryingObject) return;
        
        XRRayInteractor rayInteractor = PlacementManager.Instance.rayInteractor;
        if (rayInteractor == null) return;

        // Input-Abfrage über die Action aus dem PlacementManager
        if (PlacementManager.Instance.triggerPress.action != null && 
            PlacementManager.Instance.triggerPress.action.WasPressedThisFrame())
        {
            CheckForPickup(rayInteractor);
        }
    }

    private void CheckForPickup(XRRayInteractor interactor)
    {
        if (interactor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            // Prüfen, ob genau dieses Objekt oder ein Kind davon getroffen wurde
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
            {
                float timeSinceLastClick = Time.time - lastClickTime;

                if (timeSinceLastClick <= DOUBLE_CLICK_TIME)
                {
                    // Dieser Aufruf funktioniert nur, wenn die Methode in PlacementManager existiert!
                    PlacementManager.Instance.PickUpFurniture(gameObject);
                }
                
                lastClickTime = Time.time;
            }
        }
    }
}