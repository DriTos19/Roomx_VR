using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance;

    [Header("VR Input (Right Hand)")]
    public XRRayInteractor rayInteractor;
    public InputActionProperty triggerPress;
    public InputActionProperty rotateAction;
    public InputActionProperty cancelAction;

    [Header("Layers & Materials")]
    public LayerMask groundLayer;
    public Material validMaterial;
    public Material invalidMaterial;

    [Header("Settings")]
    public float gridSize = 0.5f;
    public bool enableSnapping = true;
    public float rotationSpeed = 120f;

    private GameObject ghostObject;
    private bool isPlacing = false;
    private float currentRotation = 0f;
    private bool isValid = false;
    private bool canPlaceThisFrame = false;

    // Cooldown after placement so double-click pickup doesn't fire instantly
    private float placementCooldown = 0f;
    private const float PLACEMENT_COOLDOWN_TIME = 0.5f;

    private InventoryItemData _currentItemData;
    private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

    public bool IsCarryingObject => isPlacing;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void StartPlacement(InventoryItemData item)
    {
        if (item == null || item.prefab3D == null) return;
        if (isPlacing) CancelPlacement();

        _currentItemData = item;
        ghostObject = Instantiate(item.prefab3D);
        PrepareGhost();
        canPlaceThisFrame = false;
    }

    public void PickUpFurniture(GameObject furniture)
    {
        if (isPlacing || furniture == null) return;

        // Check cooldown so we don't immediately re-pickup after placing
        if (Time.time < placementCooldown) return;

        FurniturePrefabReference existingRef = furniture.GetComponent<FurniturePrefabReference>();
        _currentItemData = existingRef != null ? existingRef.itemData : null;

        foreach (Renderer rend in furniture.GetComponentsInChildren<Renderer>())
            _originalMaterials[rend] = rend.materials;

        FurnitureSaveManager.Instance?.UnregisterFurniture(furniture);

        ghostObject = furniture;
        PrepareGhost();
        canPlaceThisFrame = false; // prevent instant re-place
    }

    private void PrepareGhost()
    {
        if (ghostObject == null) return;

        foreach (var col in ghostObject.GetComponentsInChildren<Collider>())
            col.enabled = false;

        isPlacing = true;
        currentRotation = ghostObject.transform.eulerAngles.y;
    }

    void Update()
    {
        if (!isPlacing || ghostObject == null) return;
        if (InventoryManager.IsMenuOpen()) return;

        HandleRotation();

        if (cancelAction.action.WasPressedThisFrame() && !triggerPress.action.WasPressedThisFrame())
        {
            CancelPlacement();
            return;
        }

        HandlePositioning();

        if (!canPlaceThisFrame) canPlaceThisFrame = true;
    }

    void HandleRotation()
    {
        float rotateInput = rotateAction.action.ReadValue<Vector2>().x;
        currentRotation += rotateInput * rotationSpeed * Time.deltaTime;
    }

    void HandlePositioning()
    {
        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            if (ghostObject == null) return;

            ghostObject.SetActive(true);
            Vector3 targetPos = hit.point;

            if (enableSnapping)
            {
                targetPos.x = Mathf.Round(targetPos.x / gridSize) * gridSize;
                targetPos.z = Mathf.Round(targetPos.z / gridSize) * gridSize;
            }

            ghostObject.transform.position = targetPos;
            ghostObject.transform.rotation = Quaternion.Euler(0, currentRotation, 0);

            isValid = ((1 << hit.collider.gameObject.layer) & groundLayer) != 0;
            ApplyMaterial(isValid ? validMaterial : invalidMaterial);

            if (triggerPress.action.WasPressedThisFrame() && canPlaceThisFrame)
            {
                if (isValid) FinalizePlacement();
            }
        }
        else
        {
            if (ghostObject != null) ApplyMaterial(invalidMaterial);
        }
    }

    void FinalizePlacement()
    {
        if (ghostObject == null) return;

        RestoreOriginalMaterials();

        foreach (var col in ghostObject.GetComponentsInChildren<Collider>())
            col.enabled = true;

        int furnLayer = LayerMask.NameToLayer("Furniture");
        if (furnLayer != -1) SetLayerRecursively(ghostObject, furnLayer);

        FurniturePrefabReference prefabRef = ghostObject.GetComponent<FurniturePrefabReference>();
        if (prefabRef == null) prefabRef = ghostObject.AddComponent<FurniturePrefabReference>();
        if (_currentItemData != null)
        {
            prefabRef.prefabPath = _currentItemData.name;
            prefabRef.itemData = _currentItemData;
        }

        // Add interactable so it can be picked up again
        if (ghostObject.GetComponent<FurnitureInteractable>() == null)
            ghostObject.AddComponent<FurnitureInteractable>();

        FurnitureSaveManager.Instance?.RegisterFurniture(ghostObject);

        // Set cooldown so double-click pickup doesn't fire immediately
        placementCooldown = Time.time + PLACEMENT_COOLDOWN_TIME;

        _currentItemData = null;
        ghostObject = null;
        isPlacing = false;
    }

    public void CancelPlacement()
    {
        RestoreOriginalMaterials();
        if (ghostObject != null)
            Destroy(ghostObject);

        ghostObject = null;
        isPlacing = false;
        _currentItemData = null;
    }

    void ApplyMaterial(Material mat)
    {
        if (mat == null || ghostObject == null) return;
        foreach (var rend in ghostObject.GetComponentsInChildren<MeshRenderer>())
        {
            if (rend == null) continue;
            if (!_originalMaterials.ContainsKey(rend))
                _originalMaterials[rend] = rend.materials;
            rend.material = mat;
        }
    }

    private void RestoreOriginalMaterials()
    {
        foreach (var kvp in _originalMaterials)
            if (kvp.Key != null) kvp.Key.materials = kvp.Value;
        _originalMaterials.Clear();
    }

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, newLayer);
    }
}