using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Notwendig für Hover-Events

public class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => {
                manager.SelectItem(itemData);
                // Tooltip schließen, wenn das Item ausgewählt wurde
                manager.HideTooltip();
            });
        }
    }

    // Wird aufgerufen, wenn der VR-Strahl auf den Button zeigt
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (manager != null && itemData != null)
        {
            manager.ShowTooltip(itemData);
        }
    }

    // Wird aufgerufen, wenn der VR-Strahl den Button verlässt
    public void OnPointerExit(PointerEventData eventData)
    {
        if (manager != null)
        {
            manager.HideTooltip();
        }
    }

    // Falls das Objekt zerstört wird (z.B. beim Seitenwechsel), Tooltip sicherheitshalber schließen
    private void OnDisable()
    {
        if (manager != null) manager.HideTooltip();
    }
}