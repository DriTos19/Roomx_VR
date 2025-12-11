using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR;

public class InventoryManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject inventoryMenu;
    public Transform slotParent;
    public GameObject slotPrefab;

    [Header("Description UI")]
    public GameObject descriptionPanelRoot;
    public CanvasGroup descriptionCanvasGroup;
    public Image descriptionImage;
    public TMP_Text descriptionText;

    [Header("Items (data)")]
    public List<InventoryItemData> items = new List<InventoryItemData>();

    private bool menuActive = false;
    private Coroutine fadeCoroutine;

    private InputDevice rightController;

    void Start()
    {
        // Debug to confirm Start is running
        Debug.Log("InventoryManager Start() running");

        // Menu starts hidden
        if (inventoryMenu != null)
            inventoryMenu.SetActive(false);

        if (descriptionCanvasGroup != null)
        {
            descriptionCanvasGroup.alpha = 0f;
            descriptionCanvasGroup.interactable = false;
            descriptionCanvasGroup.blocksRaycasts = false;
        }

        if (descriptionImage != null) descriptionImage.sprite = null;
        if (descriptionText != null) descriptionText.text = "";

        PopulateSlots();

        // Get the right controller
        Debug.Log("Attempting to get right controller...");
        rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }

    void Update()
    {
        // Print every frame to confirm Update is running
        Debug.Log("UPDATE RUNNING");

        // 1. Controller validity
        Debug.Log("RIGHT VALID = " + rightController.isValid);

        // If controller lost, reacquire
        if (!rightController.isValid)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
                devices
            );

            if (devices.Count > 0)
            {
                rightController = devices[0];
                Debug.Log("Re-acquired right controller!");
            }
        }

        // 2. Try reading the primary button (A)
        bool aPressed = false;
        bool gotValue = rightController.TryGetFeatureValue(CommonUsages.primaryButton, out aPressed);

        Debug.Log($"PRIMARY GOTVALUE:{gotValue} VALUE:{aPressed}");

        // Open menu when pressed
        if (gotValue && aPressed)
        {
            Debug.Log("PRIMARY BUTTON TRIGGERED → TOGGLE MENU");
            ToggleMenu();
        }
    }

    private void ToggleMenu()
    {
        menuActive = !menuActive;
        inventoryMenu.SetActive(menuActive);

        if (!menuActive && descriptionCanvasGroup)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            descriptionCanvasGroup.alpha = 0f;
            descriptionCanvasGroup.interactable = false;
            descriptionCanvasGroup.blocksRaycasts = false;
        }
    }

    void PopulateSlots()
    {
        if (slotParent == null || slotPrefab == null) return;

        for (int i = slotParent.childCount - 1; i >= 0; i--)
            Destroy(slotParent.GetChild(i).gameObject);

        foreach (var item in items)
        {
            GameObject slotObj = Instantiate(slotPrefab, slotParent);
            ItemSlotUI slotUI = slotObj.GetComponent<ItemSlotUI>();
            if (slotUI != null) slotUI.Setup(item, this);
        }
    }

    public void ShowItemDetails(InventoryItemData itemData)
    {
        throw new System.NotImplementedException();
    }
}
