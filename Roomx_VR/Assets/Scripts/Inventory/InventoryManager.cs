using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("VR Setup")]
    public Transform xrCamera;
    public CanvasGroup canvasGroup;
    public InputActionProperty toggleButton;

    [Header("Menu Settings")]
    public float distanceFromPlayer = 1.2f;
    public float menuHeight = 0f;

    [Header("Slots")]
    public Transform slotParent;
    public GameObject slotPrefab;
    public List<InventoryItemData> items = new List<InventoryItemData>();

    private bool isOpen = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SetMenuState(false);
        PopulateSlots();
    }

    void Update()
    {
        if (toggleButton.action.WasPressedThisFrame())
        {
            // Block opening while carrying object
            if (PlacementManager.Instance.IsCarryingObject) return;

            isOpen = !isOpen;
            SetMenuState(isOpen);
        }

        // Every frame snap in front of player while open
        if (isOpen)
            SnapToPlayer();
    }

    void SnapToPlayer()
    {
        if (xrCamera == null) return;

        Vector3 forward = xrCamera.forward;
        forward.y = 0;
        forward.Normalize();

        // Fallback if looking straight up or down
        if (forward.magnitude < 0.1f)
            forward = Vector3.forward;

        transform.position = xrCamera.position
            + forward * distanceFromPlayer
            + Vector3.up * menuHeight;

        transform.rotation = Quaternion.LookRotation(forward);
        transform.Rotate(0, 180, 0);
    }

    public static bool IsMenuOpen()
    {
        return Instance != null && Instance.isOpen;
    }

    public void PopulateSlots()
    {
        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

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
        PlacementManager.Instance.StartPlacement(data.prefab3D, data.name);
    }

    private void SetMenuState(bool state)
    {
        canvasGroup.alpha = state ? 1 : 0;
        canvasGroup.interactable = state;
        canvasGroup.blocksRaycasts = state;
    }
}