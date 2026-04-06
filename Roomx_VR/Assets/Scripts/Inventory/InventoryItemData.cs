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
    
    [Header("Manual Height Override")]
    public bool useManualPlacementHeight = false;
    public float manualPlacementHeight = 2.5f;
    public float manualHeightAdjustSpeed = 2f;
    public float minManualPlacementHeight = 1f;
    public float maxManualPlacementHeight = 10f;

    public string GetName()
    {
        return string.IsNullOrEmpty(itemName) ? "Unnamed Item" : itemName;
    }

    public bool HasIcon()
    {
        return icon != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(itemName))
            itemName = name;

        if (prefab3D == null)
        {
            Debug.LogWarning($"[InventoryItemData] {name} has no prefab assigned!", this);
        }
        else if (prefab3D.scene.IsValid())
        {
            Debug.LogError($"[InventoryItemData] {name} is using a scene object instead of a prefab asset: {prefab3D.name}", this);
        }

        if (minManualPlacementHeight > maxManualPlacementHeight)
            maxManualPlacementHeight = minManualPlacementHeight;
    }
#endif
}