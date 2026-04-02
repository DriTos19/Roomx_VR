using UnityEngine;

public enum ItemCategory { All, Bed, Table, Chair, Sofa, Shelf }

[CreateAssetMenu(fileName = "NewInventoryItem", menuName = "Inventory/Item")]
public class InventoryItemData : ScriptableObject
{
    [Header("Basic Info")]
    public string itemName;

    [TextArea(3, 6)] public string description;
    [TextArea(3, 6)] public string descriptionAlbanian;
    [TextArea(3, 6)] public string descriptionGerman;

    public Sprite icon;

    [Header("3D Placement")]
    public GameObject prefab3D;
    public Vector3 placementRotationOffset;

    [Header("Category")]
    public ItemCategory category;

    [Header("Economy")]
    [Min(0)]
    public float price = 0f;

    public string GetLocalizedDescription()
    {
        return LocalizationManager.Instance.CurrentLanguage switch
        {
            Language.Albanian => string.IsNullOrEmpty(descriptionAlbanian) ? description : descriptionAlbanian,
            Language.German   => string.IsNullOrEmpty(descriptionGerman)   ? description : descriptionGerman,
            _                 => description,
        };
    }
}