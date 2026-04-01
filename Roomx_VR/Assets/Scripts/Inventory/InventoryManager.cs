using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("VR Setup")]
    public Transform xrCamera;
    public CanvasGroup canvasGroup;
    public InputActionProperty toggleButton;
    [Header("Hover Preview UI")]
    public Image hoverPreviewImage;
    public TMP_Text hoverNameText;
    public TMP_Text hoverDescriptionText;

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

    void OnEnable()
    {
        if (toggleButton.action != null)
            toggleButton.action.Enable();
    }

    void OnDisable()
    {
        if (toggleButton.action != null)
            toggleButton.action.Disable();
    }

    void Start()
    {
        SetMenuState(false);
        PopulateSlots();
    }

    void Update()
    {
        if (toggleButton.action != null && toggleButton.action.WasPressedThisFrame())
        {
            if (WallPlacer_VR.Instance != null && WallPlacer_VR.Instance.IsMaterialWheelOpen)
                return;

            isOpen = !isOpen;
            SetMenuState(isOpen);

            Debug.Log("[InventoryManager] Inventory toggled: " + isOpen);
        }

        if (isOpen)
            SnapToPlayer();
    }

    void SnapToPlayer()
    {
        if (xrCamera == null)
            return;

        Vector3 forward = xrCamera.forward;
        forward.y = 0f;
        forward.Normalize();

        if (forward.magnitude < 0.1f)
            forward = new Vector3(xrCamera.forward.x, 0f, xrCamera.forward.z).normalized;

        transform.position = xrCamera.position + forward * distanceFromPlayer + Vector3.up * menuHeight;
        transform.rotation = Quaternion.LookRotation(forward);
        transform.Rotate(0f, 180f, 0f);
    }

    public static bool IsMenuOpen()
    {
        return Instance != null && Instance.isOpen;
    }

    public void CloseInventory()
    {
        isOpen = false;
        SetMenuState(false);

        Debug.Log("[InventoryManager] Inventory closed");
    }

    public void PopulateSlots()
    {
        if (slotParent == null)
        {
            Debug.LogError("[InventoryManager] slotParent is NULL");
            return;
        }

        if (slotPrefab == null)
        {
            Debug.LogError("[InventoryManager] slotPrefab is NULL");
            return;
        }

        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        foreach (var item in items)
        {
            if (item == null)
            {
                Debug.LogWarning("[InventoryManager] Null item in items list");
                continue;
            }

            GameObject slot = Instantiate(slotPrefab, slotParent);
            slot.name = item.GetName();

            ItemSlotUI slotUI = slot.GetComponent<ItemSlotUI>();
            if (slotUI == null)
            {
                Debug.LogError("[InventoryManager] slotPrefab does not have ItemSlotUI");
                continue;
            }
            slotUI.hoverPreviewImage = hoverPreviewImage;
            slotUI.hoverNameText = hoverNameText;
            slotUI.hoverDescriptionText = hoverDescriptionText;

            Debug.Log("[InventoryManager] Creating slot for: " + item.GetName());
            slotUI.Setup(item, this);
        }
    }

    public void SelectItem(InventoryItemData data)
    {
        Debug.Log("[InventoryManager] SelectItem called");

        if (data == null)
        {
            Debug.LogError("[InventoryManager] data is NULL");
            return;
        }

        if (data.prefab3D == null)
        {
            Debug.LogError("[InventoryManager] prefab3D is NULL for item: " + data.GetName());
            return;
        }

        if (WallPlacer_VR.Instance == null)
        {
            Debug.LogError("[InventoryManager] WallPlacer_VR.Instance is NULL");
            return;
        }

        Debug.Log("[InventoryManager] Starting placement for: " + data.GetName());

        isOpen = false;
        SetMenuState(false);

        WallPlacer_VR.Instance.StartPlacement(data);
    }

    private void SetMenuState(bool state)
    {
        if (canvasGroup == null)
        {
            Debug.LogError("[InventoryManager] canvasGroup is NULL");
            return;
        }

        canvasGroup.alpha = state ? 1f : 0f;
        canvasGroup.interactable = state;
        canvasGroup.blocksRaycasts = state;
    }
}