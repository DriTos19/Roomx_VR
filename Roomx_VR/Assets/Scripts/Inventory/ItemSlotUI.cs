using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ItemSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    public Image iconImage;
    public TMP_Text nameText;
    public Button button;

    [Header("Hover Preview References")]
    public Image hoverPreviewImage;          // the white image area
    public TMP_Text hoverNameText;           // optional: if you want item name shown there too
    public TMP_Text hoverDescriptionText;    // description shown below

    private InventoryItemData itemData;
    private InventoryManager inventoryManager;

    public void Setup(InventoryItemData data, InventoryManager manager)
    {
        itemData = data;
        inventoryManager = manager;

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
            button.onClick.AddListener(OnSlotClicked);
        }
        else
        {
            Debug.LogError("[ItemSlotUI] No Button found on " + gameObject.name);
        }

        Debug.Log("[ItemSlotUI] Setup complete for item: " + data.GetName());
    }

    public void OnSlotClicked()
    {
        Debug.Log("[ItemSlotUI] OnSlotClicked fired on: " + gameObject.name);

        if (inventoryManager == null)
        {
            Debug.LogError("[ItemSlotUI] inventoryManager is NULL");
            return;
        }

        if (itemData == null)
        {
            Debug.LogError("[ItemSlotUI] itemData is NULL");
            return;
        }

        inventoryManager.SelectItem(itemData);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (itemData == null)
            return;

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
        // Optional: clear when not hovering anything
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
}