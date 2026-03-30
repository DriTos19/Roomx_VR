using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    [Header("VR Setup")]
    public Transform xrCamera; 
    public CanvasGroup canvasGroup;
    public InputActionProperty toggleButton; // XRI LeftHand Interaction/UI Press

    [Header("Liste der Möbel")]
    public Transform slotParent;
    public GameObject slotPrefab;
    public List<InventoryItemData> items = new List<InventoryItemData>(); // Deine 9 Items

    private bool isOpen = false;

    void Start()
    {
        SetMenuState(false); // Startet geschlossen
        PopulateSlots();
    }

    void Update()
    {
        // Toggle Menü (Linker Menu-Button)
        if (toggleButton.action.WasPressedThisFrame())
        {
            // Menü sperren, wenn wir gerade ein Objekt halten
            if (PlacementManager.Instance.IsCarryingObject) return;

            isOpen = !isOpen;
            SetMenuState(isOpen);
            if (isOpen) PositionMenu();
        }
    }

    public void PopulateSlots()
    {
        foreach (Transform child in slotParent) Destroy(child.gameObject);
        foreach (var item in items)
        {
            GameObject slot = Instantiate(slotPrefab, slotParent);
            slot.GetComponent<ItemSlotUI>().Setup(item, this);
        }
    }

    public void SelectItem(InventoryItemData data)
    {
        isOpen = false;
        SetMenuState(false);
        PlacementManager.Instance.StartPlacement(data.prefab3D);
    }

    private void SetMenuState(bool state)
    {
        canvasGroup.alpha = state ? 1 : 0;
        canvasGroup.interactable = state;
        canvasGroup.blocksRaycasts = state;
    }

    void PositionMenu()
    {
        Vector3 targetPos = xrCamera.position + (xrCamera.forward * 1.2f);
        transform.position = targetPos;
        transform.LookAt(new Vector3(xrCamera.position.x, transform.position.y, xrCamera.position.z));
        transform.Rotate(0, 180, 0);
    }
}