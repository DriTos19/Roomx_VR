using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ItemSlotUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    public Image iconImage;

    private InventoryItemData itemData;
    private InventoryManager inventoryManager;

    public void Setup(InventoryItemData item, InventoryManager manager)
    {
        itemData = item;
        inventoryManager = manager;

        if (iconImage != null && item.icon != null)
            iconImage.sprite = item.icon;

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners(); // prevent duplicates
            btn.onClick.AddListener(OnClick);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        inventoryManager.ShowItemDetails(itemData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        inventoryManager.HideDescription();
    }

    private void OnClick()
    {
        PurchaseManager.Instance.SelectItem(itemData);
        inventoryManager.ShowItemDetails(itemData);
    }
}