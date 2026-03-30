using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using System.Collections.Generic;

public class VRUIPointer : MonoBehaviour
{
    public float rayLength = 10f;
    public Camera uiCamera;

    private InputDevice rightController;
    private bool wasTriggerPressed = false;

    void Update()
    {
        // Get controller
        if (!rightController.isValid)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
                devices);
            if (devices.Count > 0)
                rightController = devices[0];
        }

        rightController.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed);

        // Trigger just pressed
        if (triggerPressed && !wasTriggerPressed)
        {
            PointerEventClick();
        }

        wasTriggerPressed = triggerPressed;

        // Editor mouse fallback
        #if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
            PointerEventClick();
        #endif
    }

    void PointerEventClick()
    {
        // Cast ray from controller forward
        Ray ray = new Ray(transform.position, transform.forward);

        // Use GraphicRaycaster instead of Physics
        GraphicRaycaster[] raycasters = FindObjectsOfType<GraphicRaycaster>();

        foreach (var raycaster in raycasters)
        {
            var eventData = new UnityEngine.EventSystems.PointerEventData(
                UnityEngine.EventSystems.EventSystem.current);

            // Convert ray to screen point using UI camera
            Camera cam = raycaster.GetComponent<Canvas>().worldCamera;
            if (cam == null) cam = Camera.main;

            eventData.position = cam.WorldToScreenPoint(
                transform.position + transform.forward * 5f);

            var results = new List<UnityEngine.EventSystems.RaycastResult>();
            raycaster.Raycast(eventData, results);

            foreach (var result in results)
            {
                var button = result.gameObject.GetComponentInParent<Button>();
                if (button != null && button.interactable)
                {
                    button.onClick.Invoke();
                    Debug.Log("Clicked: " + button.gameObject.name);
                    return;
                }
            }
        }
    }
}