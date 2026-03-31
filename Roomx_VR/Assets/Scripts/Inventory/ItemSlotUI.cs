using UnityEngine;
using UnityEngine.UI;

public class ItemSlotUI : MonoBehaviour
{
    public Image iconImage;
    private InventoryItemData itemData;
    private InventoryManager manager;

    public void Setup(InventoryItemData data, InventoryManager invManager)
    {
        itemData = data;
        manager = invManager;
        if(iconImage != null && data.icon != null) iconImage.sprite = data.icon;

        Button btn = GetComponent<Button>();
        if(btn != null) 
        {
            btn.onClick.RemoveAllListeners(); // Verhindert doppelte Klicks
            btn.onClick.AddListener(() => manager.SelectItem(itemData));
        }
    }
}