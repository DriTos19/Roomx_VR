using UnityEngine;
using UnityEngine.EventSystems;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance;

    [Header("Layers")]
    public LayerMask groundLayer;
    public LayerMask furnitureLayer;

    [Header("Materials")]
    public Material validMaterial;
    public Material invalidMaterial;

    [Header("Settings")]
    public float rotationSpeed = 120f;
    public float gridSize = 1f;
    public bool enableSnapping = true;
    public float pickupRange = 10f;          // max raycast distance for pickup
    public float doubleClickDelay = 0.3f;    // time window for double click

    private GameObject ghostObject;
    private InventoryItemData currentItem;

    private float currentRotationY;
    private bool isValidPlacement = true;

    private float lastClickTime = -1f;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
       

        // If holding an object, handle placement
        if (ghostObject != null)
        {
            FollowMouse();
            HandleRotation();

            if (Input.GetMouseButtonDown(0))
            {
                if (!IsPointerOverUI())
                    TryPlaceObject();
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                CancelPlacement();

            return; // skip pickup detection while placing
        }

        // Double-click detection to pick up furniture
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI()) return;

            float timeSinceLastClick = Time.time - lastClickTime;

            if (timeSinceLastClick <= doubleClickDelay)
            {
                // Double click detected — try to pick up
                TryPickUpFurniture();
                lastClickTime = -1f; // reset so triple-click doesn't re-trigger
            }
            else
            {
                lastClickTime = Time.time;
            }
        }
    }

    void TryPickUpFurniture()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, furnitureLayer))
        {
            GameObject target = hit.collider.gameObject;

            // Walk up to root in case collider is on a child
            FurniturePrefabReference refData = target.GetComponentInParent<FurniturePrefabReference>();

            if (refData != null)
                PickUpFurniture(refData.gameObject);
        }
    }

    public void StartPlacement(InventoryItemData item)
    {
        CancelPlacement();

        currentItem = item;
        ghostObject = Instantiate(item.prefab3D);
        currentRotationY = 0f;

        foreach (Collider col in ghostObject.GetComponentsInChildren<Collider>())
            col.enabled = false;

        SetGhostMaterial(validMaterial);
    }

    void FollowMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, groundLayer))
        {
            Vector3 pos = hit.point;

            if (enableSnapping)
                pos = SnapToGrid(pos);

            ghostObject.transform.position = pos;
            isValidPlacement = CheckCollision(pos);
            SetGhostMaterial(isValidPlacement ? validMaterial : invalidMaterial);
        }
    }

    bool CheckCollision(Vector3 position)
    {
        foreach (Collider col in ghostObject.GetComponentsInChildren<Collider>())
            col.enabled = true;

        Collider[] hits = Physics.OverlapBox(
            position,
            GetBounds() / 2f,
            ghostObject.transform.rotation,
            furnitureLayer
        );

        foreach (Collider col in ghostObject.GetComponentsInChildren<Collider>())
            col.enabled = false;

        return hits.Length == 0;
    }

    Vector3 GetBounds()
    {
        Renderer[] renderers = ghostObject.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;

        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        return bounds.size;
    }

    void HandleRotation()
    {
        if (Input.GetKey(KeyCode.Q))
            currentRotationY -= rotationSpeed * Time.deltaTime;

        if (Input.GetKey(KeyCode.E))
            currentRotationY += rotationSpeed * Time.deltaTime;

        ghostObject.transform.rotation = Quaternion.Euler(0, currentRotationY, 0);
    }

    void TryPlaceObject()
    {
        if (!isValidPlacement) return;

        GameObject newObj = Instantiate(
            currentItem.prefab3D,
            ghostObject.transform.position,
            ghostObject.transform.rotation
        );

        // Save/load reference
        FurniturePrefabReference prefabRef = newObj.AddComponent<FurniturePrefabReference>();
        prefabRef.prefabPath = currentItem.name;

        // Register with save manager
        FurnitureSaveManager saveManager = FindObjectOfType<FurnitureSaveManager>();
        if (saveManager != null)
            saveManager.activeFurniture.Add(newObj);

        SetLayerRecursively(newObj, "Furniture");

        CancelPlacement();
    }

    public void PickUpFurniture(GameObject obj)
    {
        FurniturePrefabReference refData = obj.GetComponent<FurniturePrefabReference>();
        if (refData == null) return;

        // Remove from save manager list
        FurnitureSaveManager saveManager = FindObjectOfType<FurnitureSaveManager>();
        if (saveManager != null)
            saveManager.activeFurniture.Remove(obj);

        InventoryManager inv = FindObjectOfType<InventoryManager>();
        if (inv == null) return;

        foreach (var item in inv.items)
        {
            if (item.name == refData.prefabPath)
            {
                Destroy(obj);
                StartPlacement(item);
                return;
            }
        }
    }

    void CancelPlacement()
    {
        if (ghostObject != null)
            Destroy(ghostObject);

        ghostObject = null;
        currentItem = null;
    }

    void SetGhostMaterial(Material mat)
    {
        foreach (Renderer r in ghostObject.GetComponentsInChildren<Renderer>())
            r.material = mat;
    }

    void SetLayerRecursively(GameObject obj, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        obj.layer = layer;

        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layerName);
    }

    Vector3 SnapToGrid(Vector3 pos)
    {
        float x = Mathf.Round(pos.x / gridSize) * gridSize;
        float y = pos.y;
        float z = Mathf.Round(pos.z / gridSize) * gridSize;
        return new Vector3(x, y, z);
    }

    bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}