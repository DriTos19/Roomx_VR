using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

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

    [Header("Description")]
    public CanvasGroup descriptionCanvasGroup;
    public Image descriptionImage;
    public TMP_Text descriptionText;

    [Header("Items")]
    public List<InventoryItemData> items = new List<InventoryItemData>();

    [Header("Budget UI")]
    public TMP_Text balanceLabel;

    [Header("Purchase UI")]
    public Button purchaseButton;
    public TMP_Text priceLabel;
    public GameObject insufficientFundsNotice;

    private bool isOpen = false;
    private Coroutine _noticeRoutine;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SetMenuState(false);

        if (descriptionCanvasGroup != null)
        {
            descriptionCanvasGroup.alpha = 0;
            descriptionCanvasGroup.blocksRaycasts = false;
        }

        PopulateSlots();

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

    void Update()
    {
        // VR toggle
        if (toggleButton.action != null && toggleButton.action.WasPressedThisFrame())
        {
            if (PlacementManager.Instance.IsCarryingObject) return;

            isOpen = !isOpen;
            SetMenuState(isOpen);
        }

        // fallback keyboard (optional)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isOpen = !isOpen;
            SetMenuState(isOpen);
        }

        if (isOpen)
        {
            SnapToPlayer();
        }
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

    void SnapToPlayer()
    {
        if (xrCamera == null) return;

        Vector3 forward = xrCamera.forward;
        forward.y = 0;
        forward.Normalize();

        if (forward.magnitude < 0.1f)
            forward = new Vector3(xrCamera.forward.x, 0, xrCamera.forward.z).normalized;

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

    void SetMenuState(bool state)
    {
        isOpen = state;

        canvasGroup.alpha = state ? 1 : 0;
        canvasGroup.interactable = state;
        canvasGroup.blocksRaycasts = state;

        if (!state)
            HideDescription();
    }

    void PopulateSlots()
    {
        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        foreach (var item in items)
        {
            GameObject slot = Instantiate(slotPrefab, slotParent);
            slot.GetComponent<ItemSlotUI>().Setup(item, this);
        }
    }

    // ---------- ITEM UI ----------

    public void ShowItemDetails(InventoryItemData item)
    {
        descriptionImage.sprite = item.icon;
        descriptionText.text = $"<b>{item.itemName}</b>\n\n{item.description}";

        descriptionCanvasGroup.alpha = 1;
        descriptionCanvasGroup.blocksRaycasts = true;

        PurchaseManager.Instance.SelectItem(item);
    }

    public void HideDescription()
    {
        descriptionCanvasGroup.alpha = 0;
        descriptionCanvasGroup.blocksRaycasts = false;
    }

    // ---------- PURCHASE ----------

    private void RefreshBalanceUI(float balance)
    {
        if (balanceLabel != null)
            balanceLabel.text = $"Balance: ${balance:F0}";

        if (PurchaseManager.Instance.SelectedItem != null)
            RefreshPurchaseUI(PurchaseManager.Instance.SelectedItem);
    }

    private void RefreshPurchaseUI(InventoryItemData item)
    {
        if (item == null)
        {
            SetPurchaseButtonInteractable(false);
            return;
        }

        if (priceLabel != null)
            priceLabel.text = item.price > 0 ? $"${item.price:F0}" : "Free";

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

        if (_noticeRoutine != null)
            StopCoroutine(_noticeRoutine);

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
        SetMenuState(false);
        PlacementManager.Instance.StartPlacement(item.prefab3D);
    }
}