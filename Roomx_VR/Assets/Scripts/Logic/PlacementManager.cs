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
    public LayerMask blockedLayers; // Set to Wall + Furniture in Inspector
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
        if (Time.time < placementCooldown) return;

        FurniturePrefabReference existingRef = furniture.GetComponent<FurniturePrefabReference>();
        _currentItemData = existingRef != null ? existingRef.itemData : null;

        foreach (Renderer rend in furniture.GetComponentsInChildren<Renderer>())
            _originalMaterials[rend] = rend.materials;

        FurnitureSaveManager.Instance?.UnregisterFurniture(furniture);

        ghostObject = furniture;
        PrepareGhost();
        canPlaceThisFrame = false;
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

        if (cancelAction.action != null && cancelAction.action.WasPressedThisFrame() &&
            (triggerPress.action == null || !triggerPress.action.WasPressedThisFrame()))
        {
            CancelPlacement();
            return;
        }

        HandlePositioning();

        if (!canPlaceThisFrame) canPlaceThisFrame = true;
    }

    void HandleRotation()
    {
        if (rotateAction.action == null) return;
        float rotateInput = rotateAction.action.ReadValue<Vector2>().x;
        currentRotation += rotateInput * rotationSpeed * Time.deltaTime;
    }

    void HandlePositioning()
    {
        if (rayInteractor == null) return;

        bool gotHit = rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit);

        if (!gotHit)
        {
            Physics.Raycast(
                rayInteractor.transform.position,
                rayInteractor.transform.forward,
                out hit,
                100f,
                groundLayer
            );
            gotHit = hit.collider != null;
        }

        if (gotHit && hit.collider != null)
        {
            ghostObject.SetActive(true);

            int hitLayer = hit.collider.gameObject.layer;

            // Check if ray hit a blocked layer (wall, furniture)
            bool hitBlockedLayer = ((1 << hitLayer) & blockedLayers) != 0;

            // Only valid if hitting ground and NOT hitting blocked layer
            bool hitsGround = ((1 << hitLayer) & groundLayer) != 0;

            // Also check if ghost overlaps with walls or furniture
            bool overlapsBlocked = CheckOverlapWithBlockedLayers();

            isValid = hitsGround && !hitBlockedLayer && !overlapsBlocked;

            Vector3 targetPos = hit.point;

            if (enableSnapping)
            {
                targetPos.x = Mathf.Round(targetPos.x / gridSize) * gridSize;
                targetPos.z = Mathf.Round(targetPos.z / gridSize) * gridSize;
            }

            float bottomOffset = GetBottomToPivotOffset(ghostObject);
            targetPos.y = hit.point.y + bottomOffset;

            ghostObject.transform.position = targetPos;
            ghostObject.transform.rotation = Quaternion.Euler(0, currentRotation, 0);

            ApplyMaterial(isValid ? validMaterial : invalidMaterial);

            if (triggerPress.action != null &&
                triggerPress.action.WasPressedThisFrame() &&
                canPlaceThisFrame && isValid)
            {
                FinalizePlacement();
            }
        }
        else
        {
            isValid = false;
            if (ghostObject != null) ApplyMaterial(invalidMaterial);
        }
    }

    // Checks if the ghost object overlaps with walls or already placed furniture
    bool CheckOverlapWithBlockedLayers()
    {
        if (ghostObject == null) return false;

        Bounds bounds = GetGhostBounds();

        // Slightly shrink bounds to avoid false positives at edges
        Vector3 halfExtents = bounds.extents * 0.85f;

        Collider[] hits = Physics.OverlapBox(
            bounds.center,
            halfExtents,
            ghostObject.transform.rotation,
            blockedLayers
        );

        return hits.Length > 0;
    }

    Bounds GetGhostBounds()
    {
        Renderer[] renderers = ghostObject.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Collider[] cols = ghostObject.GetComponentsInChildren<Collider>();
            if (cols.Length > 0)
            {
                Bounds b = cols[0].bounds;
                foreach (var c in cols) b.Encapsulate(c.bounds);
                return b;
            }
            return new Bounds(ghostObject.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    void FinalizePlacement()
    {
        if (ghostObject == null) return;

        ghostObject.SetActive(true);
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

        if (ghostObject.GetComponent<FurnitureInteractable>() == null)
            ghostObject.AddComponent<FurnitureInteractable>();

        FurnitureSaveManager.Instance?.RegisterFurniture(ghostObject);

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

    float GetBottomToPivotOffset(GameObject obj)
    {
        Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
        bool hasBounds = false;

        foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (!hasBounds)
        {
            foreach (Collider c in obj.GetComponentsInChildren<Collider>())
            {
                if (!hasBounds) { bounds = c.bounds; hasBounds = true; }
                else bounds.Encapsulate(c.bounds);
            }
        }

        if (!hasBounds) return 0f;
        return obj.transform.position.y - bounds.min.y;
    }
}