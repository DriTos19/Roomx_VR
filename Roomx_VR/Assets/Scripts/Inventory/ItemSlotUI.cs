using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ItemSlotUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    [Header("UI References")]
    public Image iconImage;
    public TMP_Text nameText;
    public Button button;

    [Header("Hover Preview References")]
    public Image hoverPreviewImage;
    public TMP_Text hoverNameText;
    public TMP_Text hoverDescriptionText;

    private InventoryItemData itemData;
    private InventoryManager manager;

    private bool isHovered = false;

    public void Setup(InventoryItemData data, InventoryManager invManager)
    {
        itemData = data;
        manager = invManager;

        if (data == null)
        {
            Debug.LogError("[ItemSlotUI] Setup failed: data is NULL");
            return;
        }

        if (iconImage == null)
        {
            Debug.LogError("[ItemSlotUI] iconImage is NULL on " + gameObject.name);
        }
        else
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = data.icon != null;
            iconImage.color = Color.white;
        }

        if (nameText == null)
        {
            Debug.LogError("[ItemSlotUI] nameText is NULL on " + gameObject.name);
        }
        else
        {
            nameText.text = data.GetName();
        }

        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();

            // From first script:
            button.onClick.AddListener(() => {
                PurchaseManager.Instance.SelectItem(itemData);
                manager.ShowTooltip(itemData);
            });
        }

        Debug.Log("[ItemSlotUI] Setup complete for item: " + data.GetName());
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (itemData == null)
            return;

        isHovered = true;

        // From first script:
        // intentionally empty - tooltip only shows on click now

        // From second script:
        if (hoverPreviewImage != null)
        {
            hoverPreviewImage.sprite = itemData.icon;
            hoverPreviewImage.enabled = itemData.icon != null;
            hoverPreviewImage.color = Color.white;
            hoverPreviewImage.preserveAspect = true;
        }

        if (hoverNameText != null)
            hoverNameText.text = itemData.GetName();

        if (hoverDescriptionText != null)
            hoverDescriptionText.text = string.IsNullOrWhiteSpace(itemData.description)
                ? ""
                : itemData.description;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // From first script:
        // intentionally empty - tooltip stays visible until buy or close

        // From second script:
        isHovered = false;
        ClearHoverPreview();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isHovered)
        {
            Debug.Log("[ItemSlotUI] Click ignored because slot is not hovered.");
            return;
        }

        if (manager == null)
        {
            Debug.LogError("[ItemSlotUI] inventoryManager is NULL");
            return;
        }

        if (itemData == null)
        {
            Debug.LogError("[ItemSlotUI] itemData is NULL");
            return;
        }

        if (eventData.button != PointerEventData.InputButton.Left)
        {
            Debug.Log("[ItemSlotUI] Click ignored because it was not a left/select click.");
            return;
        }

        Debug.Log("[ItemSlotUI] Item clicked while hovered: " + itemData.GetName());

        // From second script:
        manager.SelectItem(itemData);
    }

    private void ClearHoverPreview()
    {
        if (hoverPreviewImage != null)
        {
            hoverPreviewImage.sprite = null;
            hoverPreviewImage.enabled = false;
        }

        if (hoverNameText != null)
            hoverNameText.text = "";

        if (hoverDescriptionText != null)
            hoverDescriptionText.text = "";
    }

    private void OnDisable()
    {
        // From first script:
        if (manager != null)
            manager.HideTooltip();

        // From second script:
        isHovered = false;
        ClearHoverPreview();
    }
}