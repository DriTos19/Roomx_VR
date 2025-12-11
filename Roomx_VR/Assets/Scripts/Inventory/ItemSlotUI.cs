using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image iconImage;    // child Icon image
    public Image hoverImage;   // optional highlight image (alpha 0 default)

    private InventoryItemData itemData;
    private InventoryManager manager;

    public void Setup(InventoryItemData data, InventoryManager mgr)
    {
        itemData = data;
        manager = mgr;

        if (iconImage != null) iconImage.sprite = data.icon;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverImage != null)
        {
            var c = hoverImage.color;
            c.a = 0.12f;
            hoverImage.color = c;
        }
        transform.localScale = Vector3.one * 1.05f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverImage != null)
        {
            var c = hoverImage.color;
            c.a = 0f;
            hoverImage.color = c;
        }
        transform.localScale = Vector3.one;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager != null) manager.ShowItemDetails(itemData);
    }
}