using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image iconImage;
    private InventoryItemData itemData;
    private InventoryManager manager;

    public void Setup(InventoryItemData data, InventoryManager invManager)
    {
        itemData = data;
        manager = invManager;
        if (iconImage != null && data.icon != null) iconImage.sprite = data.icon;

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => {
                PurchaseManager.Instance.SelectItem(itemData);
                manager.ShowTooltip(itemData);
            });
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // intentionally empty - tooltip only shows on click now
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // intentionally empty - tooltip stays visible until buy or close
    }

    private void OnDisable()
    {
        if (manager != null) manager.HideTooltip();
    }
}