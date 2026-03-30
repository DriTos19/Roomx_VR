using UnityEngine;
using UnityEngine.UI;

public class ItemSlotUI : MonoBehaviour
{
    public Image iconImage;

    private InventoryItemData itemData;
    private InventoryManager inventoryManager;

    public void Setup(InventoryItemData item, InventoryManager manager)
    {
        itemData = item;
        inventoryManager = manager;

        iconImage.sprite = item.icon;

        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        inventoryManager.ShowItemDetails(itemData);
    }
}