using UnityEngine;
using UnityEngine.Events;

public class PurchaseManager : MonoBehaviour
{
    public static PurchaseManager Instance { get; private set; }

    public UnityEvent<InventoryItemData> onPurchaseSuccess = new UnityEvent<InventoryItemData>();
    public UnityEvent<InventoryItemData> onPurchaseFailed  = new UnityEvent<InventoryItemData>();
    public UnityEvent<InventoryItemData> onItemSelected    = new UnityEvent<InventoryItemData>();

    public InventoryItemData SelectedItem { get; private set; }

    public void SelectItem(InventoryItemData item)
    {
        SelectedItem = item;
        onItemSelected.Invoke(item);
    }

    public void PurchaseSelected()
    {
        if (SelectedItem == null) return;
        Purchase(SelectedItem);
    }

    public void Purchase(InventoryItemData item)
    {
        if (item == null) return;

        if (BudgetManager.Instance.TrySpend(item.price))
        {
            onPurchaseSuccess.Invoke(item);
        }
        else
        {
            onPurchaseFailed.Invoke(item);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
}