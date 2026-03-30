using UnityEngine;

/// <summary>
/// This component stores the reference to the original prefab ScriptableObject
/// so we can load the correct prefab when deserializing saved furniture
/// </summary>
public partial class FurniturePrefabReference : MonoBehaviour
{
    public string prefabPath; // The name of the InventoryItemData ScriptableObject
}
