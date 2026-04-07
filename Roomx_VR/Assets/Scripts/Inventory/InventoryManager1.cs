using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class InventoryManager1 : InventoryManager
{
    public new static InventoryManager1 Instance;

    [Header("Hover Preview UI")]
    public Image hoverPreviewImage;
    public TMP_Text hoverNameText;
    public TMP_Text hoverDescriptionText;

    [Header("Pagination UI")]
    public Button nextPageButton;
    public Button previousPageButton;
    public TMP_Text pageText;

    protected override void Awake()
    {
        Instance = this;
        InventoryManager.Instance = this; // Sets the global instance to this VR version
    }

    protected override void Start()
    {
        base.Start(); // registers purchaseButton listener and Budget/PurchaseManager events

        if (nextPageButton != null)
        {
            nextPageButton.onClick.RemoveAllListeners();
            nextPageButton.onClick.AddListener(NextPage);
        }

        if (previousPageButton != null)
        {
            previousPageButton.onClick.RemoveAllListeners();
            previousPageButton.onClick.AddListener(PreviousPage);
        }

        UpdateInventoryDisplay();
    }

    protected override void Update()
    {
        if (menuToggleButton.action != null && menuToggleButton.action.WasPressedThisFrame())
        {
            // Lock menu if WallPlacer material wheel is active
            if (WallPlacer_VR.Instance != null && WallPlacer_VR.Instance.IsMaterialWheelOpen)
                return;

            isMenuOpen = !isMenuOpen;
            SetMenuState(isMenuOpen);
        }

        if (isMenuOpen) SnapToPlayer();
    }

    void SnapToPlayer()
    {
        if (xrCamera == null) return;

        Vector3 forward = xrCamera.forward;
        forward.y = 0f;
        forward.Normalize();

        transform.position = xrCamera.position + forward * distanceFromPlayer + Vector3.up * menuHeightOffset;
        transform.rotation = Quaternion.LookRotation(forward);
        transform.Rotate(0f, 180f, 0f); // Face the player
    }

    public override void UpdateInventoryDisplay()
    {
        if (slotParent == null || slotPrefab == null) return;
        foreach (Transform child in slotParent) Destroy(child.gameObject);

        int startIndex = currentPage * itemsPerPage;
        int endIndex = Mathf.Min(startIndex + itemsPerPage, allItems.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            GameObject slot = Instantiate(slotPrefab, slotParent);
            ItemSlotUI slotUI = slot.GetComponent<ItemSlotUI>();
            
            if (slotUI != null)
            {
                slotUI.hoverPreviewImage = hoverPreviewImage;
                slotUI.hoverNameText = hoverNameText;
                slotUI.hoverDescriptionText = hoverDescriptionText;

                // Inheritance allows 'this' to be passed as InventoryManager
                slotUI.Setup(allItems[i], this); 
            }
        }

        UpdatePageUI();
    }

    private void UpdatePageUI()
    {
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)allItems.Count / itemsPerPage));
        if (pageText != null) pageText.text = $"Page {currentPage + 1} / {totalPages}";
        if (previousPageButton != null) previousPageButton.interactable = currentPage > 0;
        if (nextPageButton != null) nextPageButton.interactable = (currentPage + 1) * itemsPerPage < allItems.Count;
    }

    public override void SelectItem(InventoryItemData data)
    {
        if (data == null) return;

        if (WallPlacer_VR.Instance != null)
        {
            CloseInventory();
            WallPlacer_VR.Instance.StartPlacement(data);
        }
        else
        {
            base.SelectItem(data);
        }
    }

    protected override void OnPurchaseSuccess(InventoryItemData item)
    {
        CloseInventory();
        if (WallPlacer_VR.Instance != null)
            WallPlacer_VR.Instance.StartPlacement(item);
        else if (PlacementManager.Instance != null)
            PlacementManager.Instance.StartPlacement(item);
    }
}