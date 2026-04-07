using UnityEngine;
using System.Collections;
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
    protected int itemsPerPage = 6;
    protected int currentPage = 0;

    [Header("UI Navigation")]
    public Button nextButton;
    public Button prevButton;

    [Header("Tooltip UI")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public Image previewImage;

    [Header("Budget UI")]
    public TMP_Text balanceLabel;
    public TMP_Text priceLabel;
    public Button purchaseButton;
    public GameObject insufficientFundsNotice;

    protected bool isMenuOpen = false;
    private Coroutine _noticeRoutine;

    protected virtual void Awake() 
    { 
        if (Instance == null) Instance = this; 
    }

    // FIX for Error CS0117: IsMenuOpen
    public static bool IsMenuOpen()
    {
        return Instance != null && Instance.isMenuOpen;
    }

    protected virtual void Start()
    {
        UpdateInventoryDisplay();
        if (nextButton) nextButton.onClick.AddListener(NextPage);
        if (prevButton) prevButton.onClick.AddListener(PreviousPage);

        HideTooltip();
        SetMenuState(false);

        if (BudgetManager.Instance != null)
            BudgetManager.Instance.onBalanceChanged.AddListener(RefreshBalanceUI);
        
        if (PurchaseManager.Instance != null)
        {
            PurchaseManager.Instance.onItemSelected.AddListener(RefreshPurchaseUI);
            PurchaseManager.Instance.onPurchaseFailed.AddListener(_ => ShowInsufficientFunds());
            PurchaseManager.Instance.onPurchaseSuccess.AddListener(OnPurchaseSuccess);
        }

        if (purchaseButton != null)
            purchaseButton.onClick.AddListener(() => PurchaseManager.Instance.PurchaseSelected());

        if (BudgetManager.Instance != null) RefreshBalanceUI(BudgetManager.Instance.Balance);
        SetPurchaseButtonInteractable(false);

        if (insufficientFundsNotice != null)
            insufficientFundsNotice.SetActive(false);
    }

    protected virtual void Update()
    {
        if (menuToggleButton.action != null && menuToggleButton.action.WasPressedThisFrame())
        {
            isMenuOpen = !isMenuOpen;
            SetMenuState(isMenuOpen);
            if (!isMenuOpen) HideTooltip();
        }
        if (isMenuOpen && xrCamera != null) FollowCamera();
    }

    protected virtual void FollowCamera()
    {
        Vector3 cameraPos = xrCamera.position;
        Vector3 cameraForward = xrCamera.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        Vector3 targetPosition = cameraPos + (cameraForward * distanceFromPlayer);
        targetPosition.y = cameraPos.y + menuHeightOffset;

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 10f);
        Vector3 lookDirection = transform.position - cameraPos;
        lookDirection.y = 0;
        if (lookDirection != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookDirection);
    }

    public virtual void SetMenuState(bool state)
    {
        isMenuOpen = state;
        if (mainCanvasGroup != null)
        {
            mainCanvasGroup.alpha = state ? 1 : 0;
            mainCanvasGroup.blocksRaycasts = state;
            mainCanvasGroup.interactable = state;
        }
    }

    // FIX for Error CS1061: CloseInventory
    public virtual void CloseInventory()
    {
        SetMenuState(false);
        HideTooltip();
    }

    public virtual void UpdateInventoryDisplay()
    {
        if (slotParent == null) return;
        foreach (Transform child in slotParent) Destroy(child.gameObject);
        
        int startIndex = currentPage * itemsPerPage;
        int endIndex = Mathf.Min(startIndex + itemsPerPage, allItems.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            GameObject newSlot = Instantiate(slotPrefab, slotParent);
            ItemSlotUI slotScript = newSlot.GetComponent<ItemSlotUI>();
            if (slotScript != null) slotScript.Setup(allItems[i], this);
        }

        if (prevButton) prevButton.interactable = currentPage > 0;
        if (nextButton) nextButton.interactable = (currentPage + 1) * itemsPerPage < allItems.Count;
    }

    // FIX for Error CS1061: SelectItem
    public virtual void SelectItem(InventoryItemData data)
    {
        if (data != null && PurchaseManager.Instance != null)
            PurchaseManager.Instance.SelectItem(data);
    }

    public void ShowTooltip(InventoryItemData data)
    {
        if (tooltipPanel == null) return;
        tooltipPanel.SetActive(true);
        if (nameText) nameText.text = data.itemName;
        if (descriptionText) descriptionText.text = data.GetLocalizedDescription();
        if (previewImage) previewImage.sprite = data.icon;
        SelectItem(data);
    }

    public void HideTooltip() { if (tooltipPanel != null) tooltipPanel.SetActive(false); }

    public virtual void NextPage() { currentPage++; UpdateInventoryDisplay(); HideTooltip(); }
    public virtual void PreviousPage() { currentPage--; UpdateInventoryDisplay(); HideTooltip(); }

    private void RefreshBalanceUI(float balance) { if (balanceLabel) balanceLabel.text = $"{balance:F0}$"; }
    private void RefreshPurchaseUI(InventoryItemData item)
    {
        if (priceLabel && item != null) priceLabel.text = $"{item.price:F0}$";
        SetPurchaseButtonInteractable(item != null);
    }
    private void SetPurchaseButtonInteractable(bool state) { if (purchaseButton) purchaseButton.interactable = state; }
    private void ShowInsufficientFunds() { if (insufficientFundsNotice) StartCoroutine(FlashNotice()); }
    private IEnumerator FlashNotice() { insufficientFundsNotice.SetActive(true); yield return new WaitForSeconds(2f); insufficientFundsNotice.SetActive(false); }
    protected virtual void OnPurchaseSuccess(InventoryItemData item)
    {
        CloseInventory();
        if (PlacementManager.Instance != null)
            PlacementManager.Instance.StartPlacement(item);
    }
}