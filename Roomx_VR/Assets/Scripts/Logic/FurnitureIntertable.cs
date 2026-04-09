using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;

public class FurnitureInteractable : MonoBehaviour
{
    private float lastClickTime = -999f;
    private const float DOUBLE_CLICK_TIME = 0.35f;
    private bool _wasTriggerPressed = false;

    void Update()
    {
        if (PlacementManager.Instance == null) return;
        if (PlacementManager.Instance.IsCarryingObject) return;
        if (InventoryManager.IsMenuOpen()) return;

        XRRayInteractor rayInteractor = PlacementManager.Instance.rayInteractor;
        if (rayInteractor == null) return;

        bool triggerDown = false;

        // Try Input Action first
        var triggerAction = PlacementManager.Instance.triggerPress.action;
        if (triggerAction != null && triggerAction.WasPressedThisFrame())
        {
            triggerDown = true;
        }

        // Fallback for real PICO device
        if (!triggerDown)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
                devices);

            foreach (var device in devices)
            {
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed))
                {
                    triggerDown = pressed && !_wasTriggerPressed;
                    _wasTriggerPressed = pressed;
                    break;
                }
            }
        }

        if (triggerDown)
            CheckForPickup(rayInteractor);
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
            PlacementManager.Instance.PickUpFurniture(gameObject);
            lastClickTime = -999f;
        }
        else
        {
            lastClickTime = Time.time;
        }
    }
}