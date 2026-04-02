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

    [Header("Budget UI")]
    public TMP_Text balanceLabel;
    public TMP_Text priceLabel;
    public Button purchaseButton;
    public GameObject insufficientFundsNotice;

    private bool isMenuOpen = false;
    private Coroutine _noticeRoutine;

    void Awake() { Instance = this; }

    void Start()
    {
        UpdateInventoryDisplay();
        if (nextButton) nextButton.onClick.AddListener(NextPage);
        if (prevButton) prevButton.onClick.AddListener(PreviousPage);

        // tooltip starts hidden
        HideTooltip();
        SetMenuState(false);

        BudgetManager.Instance.onBalanceChanged.AddListener(RefreshBalanceUI);
        PurchaseManager.Instance.onItemSelected.AddListener(RefreshPurchaseUI);
        PurchaseManager.Instance.onPurchaseFailed.AddListener(_ => ShowInsufficientFunds());
        PurchaseManager.Instance.onPurchaseSuccess.AddListener(OnPurchaseSuccess);

        if (purchaseButton != null)
            purchaseButton.onClick.AddListener(() => PurchaseManager.Instance.PurchaseSelected());

        RefreshBalanceUI(BudgetManager.Instance.Balance);
        SetPurchaseButtonInteractable(false);

        if (insufficientFundsNotice != null)
            insufficientFundsNotice.SetActive(false);
    }

    void OnDestroy()
    {
        if (BudgetManager.Instance != null)
            BudgetManager.Instance.onBalanceChanged.RemoveListener(RefreshBalanceUI);
        if (PurchaseManager.Instance != null)
        {
            PurchaseManager.Instance.onItemSelected.RemoveListener(RefreshPurchaseUI);
            PurchaseManager.Instance.onPurchaseSuccess.RemoveListener(OnPurchaseSuccess);
        }
    }

    void Update()
    {
        if (menuToggleButton.action.WasPressedThisFrame())
        {
            isMenuOpen = !isMenuOpen;
            SetMenuState(isMenuOpen);
            if (!isMenuOpen) HideTooltip();
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
            if (slotScript != null) slotScript.Setup(allItems[i], this);
        }

        if (prevButton) prevButton.interactable = currentPage > 0;
        if (nextButton) nextButton.interactable = (currentPage + 1) * itemsPerPage < allItems.Count;
    }

    // called on click from ItemSlotUI — stays open until buy
    public void ShowTooltip(InventoryItemData data)
    {
        if (tooltipPanel == null) return;
        tooltipPanel.SetActive(true);
        if (nameText) nameText.text = data.itemName;
        if (descriptionText) descriptionText.text = data.GetLocalizedDescription();
        if (previewImage) previewImage.sprite = data.icon;
        PurchaseManager.Instance.SelectItem(data);
    }

    // only called when menu closes or page changes
    public void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    public void SelectItem(InventoryItemData data)
    {
        if (data != null && data.prefab3D != null)
            PurchaseManager.Instance.SelectItem(data);
    }

    public void HideMenu()
    {
        SetMenuState(false);
        HideTooltip();
    }

    public void NextPage() { currentPage++; UpdateInventoryDisplay(); HideTooltip(); }
    public void PreviousPage() { currentPage--; UpdateInventoryDisplay(); HideTooltip(); }

    public static bool IsMenuOpen()
    {
        if (Instance == null) return false;
        return Instance.isMenuOpen;
    }

    private void RefreshBalanceUI(float balance)
    {
        if (balanceLabel != null)
            balanceLabel.text = $"{balance:F0}$";
        if (PurchaseManager.Instance.SelectedItem != null)
            RefreshPurchaseUI(PurchaseManager.Instance.SelectedItem);
    }

    private void RefreshPurchaseUI(InventoryItemData item)
    {
        if (item == null) { SetPurchaseButtonInteractable(false); return; }
        if (priceLabel != null)
            priceLabel.text = item.price > 0 ? $"{item.price:F0}$" : "Free";
        SetPurchaseButtonInteractable(BudgetManager.Instance.CanAfford(item.price));
    }

    private void SetPurchaseButtonInteractable(bool state)
    {
        if (purchaseButton != null)
            purchaseButton.interactable = state;
    }

    private void ShowInsufficientFunds()
    {
        if (insufficientFundsNotice == null) return;
        if (_noticeRoutine != null) StopCoroutine(_noticeRoutine);
        _noticeRoutine = StartCoroutine(FlashNotice());
    }

    private IEnumerator FlashNotice()
    {
        insufficientFundsNotice.SetActive(true);
        yield return new WaitForSeconds(2f);
        insufficientFundsNotice.SetActive(false);
    }

    private void OnPurchaseSuccess(InventoryItemData item)
    {
        HideMenu();
        PlacementManager.Instance.StartPlacement(item);
    }
}