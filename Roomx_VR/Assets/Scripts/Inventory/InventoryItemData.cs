using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item")]
public class InventoryItemData : ScriptableObject
{
    public string itemName;
    [TextArea(3,6)] public string description;
    public Sprite icon;         // UI image
    public GameObject prefab3D; // Your 3D furniture prefab
}
