using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("VR Setup")]
    public Transform xrCamera; 
    public CanvasGroup mainCanvasGroup;
    public Transform slotParent;
    public GameObject slotPrefab;

    [Header("Input Actions")]
    public InputActionProperty menuToggleButton; 
    public InputActionProperty uiPressAction;    

    [Header("Menu Settings")]
    public float distanceFromPlayer = 1.2f;
    public float menuHeightOffset = -0.2f;
    public float menuWidthOffset = 0f; 

    [Header("Data & Pagination")]
    public List<InventoryItemData> allItems; 
    private int itemsPerPage = 6; 
    private int currentPage = 0;

    [Header("UI Navigation")]
    public Button nextButton;
    public Button prevButton;

    [Header("Tooltip UI")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI nameText; 
    public TextMeshProUGUI descriptionText;
    public Image previewImage;

    private bool isMenuOpen = false;

    void Awake() { Instance = this; }

    void Start()
    {
        UpdateInventoryDisplay();
        if(nextButton) nextButton.onClick.AddListener(NextPage);
        if(prevButton) prevButton.onClick.AddListener(PreviousPage);
        HideTooltip();
        SetMenuState(false);
    }

    void Update()
    {
        if (menuToggleButton.action.WasPressedThisFrame())
        {
            isMenuOpen = !isMenuOpen;
            SetMenuState(isMenuOpen);
            if(!isMenuOpen) HideTooltip();
        }
        if (isMenuOpen && xrCamera != null) FollowCamera();
    }

    private void FollowCamera()
    {
        Vector3 cameraPos = xrCamera.position;
        Vector3 cameraForward = xrCamera.forward;
        Vector3 cameraRight = xrCamera.right;

        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 targetPosition = cameraPos + (cameraForward * distanceFromPlayer) + (cameraRight * menuWidthOffset);
        targetPosition.y = cameraPos.y + menuHeightOffset;

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
        
        Vector3 lookDirection = transform.position - cameraPos;
        lookDirection.y = 0; 
        if (lookDirection != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookDirection);
    }

    private void SetMenuState(bool state)
    {
        isMenuOpen = state;
        mainCanvasGroup.alpha = state ? 1 : 0;
        mainCanvasGroup.blocksRaycasts = state;
        mainCanvasGroup.interactable = state;
    }

    public void UpdateInventoryDisplay()
    {
        foreach (Transform child in slotParent) Destroy(child.gameObject);
        int startIndex = currentPage * itemsPerPage;
        int endIndex = Mathf.Min(startIndex + itemsPerPage, allItems.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            GameObject newSlot = Instantiate(slotPrefab, slotParent);
            ItemSlotUI slotScript = newSlot.GetComponent<ItemSlotUI>();
            if(slotScript != null) slotScript.Setup(allItems[i], this);
        }

        if(prevButton) prevButton.interactable = currentPage > 0;
        if(nextButton) nextButton.interactable = (currentPage + 1) * itemsPerPage < allItems.Count;
    }

    public void ShowTooltip(InventoryItemData data)
    {
        if (tooltipPanel == null) return;
        tooltipPanel.SetActive(true);
        if(nameText) nameText.text = data.itemName;
        if(descriptionText) descriptionText.text = data.description;
        if(previewImage) previewImage.sprite = data.icon;
    }

    public void HideTooltip() { if (tooltipPanel != null) tooltipPanel.SetActive(false); }

    public void SelectItem(InventoryItemData data)
    {
        if (data != null && data.prefab3D != null)
        {
            PlacementManager.Instance.StartPlacement(data.prefab3D);
            SetMenuState(false);
        }
    }

    public void NextPage() { currentPage++; UpdateInventoryDisplay(); HideTooltip(); }
    public void PreviousPage() { currentPage--; UpdateInventoryDisplay(); HideTooltip(); }

    public static bool IsMenuOpen()
    {
        if (Instance == null) return false;
        return Instance.isMenuOpen;
    }
}