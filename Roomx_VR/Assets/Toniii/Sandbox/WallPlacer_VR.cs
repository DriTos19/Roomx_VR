using UnityEngine;
using UnityEngine.InputSystem;

public class WallPlacer_VR : MonoBehaviour
{
    public static WallPlacer_VR Instance;

    [Header("Controllers")]
    public Transform rightControllerTransform;
    public Transform leftControllerTransform;

    [Header("Right Hand Input")]
    public InputActionProperty rightPlaceAction;
    public InputActionProperty rightInventoryGripAction;
    public InputActionProperty rightEditAction;
    public InputActionProperty rightHeightAdjustAction;
    public InputActionProperty cancelAction;

    [Header("Left Hand Input")]
    public InputActionProperty leftWheelGripAction;
    public InputActionProperty leftApplyMaterialAction;
    public InputActionProperty leftJoystickAction;

    [Header("Movement Input")]
    public InputActionProperty movementAction;

    private float suppressEditUntilTime = 0f;
    private const float EDIT_INPUT_SUPPRESS_DURATION = 0.25f;

    [Header("Placement")]
    public float placeDistance = 2.2f;
    public float maxDistance = 6.0f;
    public float joystickDeadzone = 0.2f;
    public float gridSize = 0.5f;
    public float surfaceOffset = 0f;
    public LayerMask placementSurfaceMask = ~0;
    public float overlapShrink = 0.95f;

    [Header("Rotation")]
    public float rotationSpeed = 90f; // degrees per second

    [Header("Preview")]
    public float controllerUpOffset = -0.02f;
    public bool snapXZToGrid = true;

    [Header("Manual Height Placement")]
    public bool lockManualHeightXZToGrid = true;

    [Header("Placed Objects")]
    public Transform placedObjectsParent;
    public bool autoCreatePlacedObjectsParent = true;

    [Header("Safety")]
    public bool disableScriptsOnPreview = true;
    public bool disableScriptsOnPlacedObjects = false;
    public bool makePreviewCollidersTriggers = true;

    [Header("Material Wheel")]
    public MaterialWheelManager materialWheelController;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    private GameObject previewInstance;
    private GameObject placedObject;
    private GameObject editSourceObject;

    private float manualHeightAdjustSpeed = 2f;

    private Material[] lastSavedMaterials;
    private Vector3 oldObjectPosition;
    private Quaternion oldObjectRotation;

    private bool isPlacing;
    private bool isEditingExistingObject;
    private bool canPlaceCurrentPreview;

    private InventoryItemData currentSelectedItem;
    private Vector3 currentPlacementRotationOffset = Vector3.zero;

    private bool useManualPlacementHeight = false;
    private float manualPlacementHeight = 2.5f;
    private float minManualPlacementHeight = 1f;
    private float maxManualPlacementHeight = 10f;

    private const float PREVIEW_ALPHA = 0.5f;
    private const float INVALID_PREVIEW_ALPHA = 0.2f;

    public bool IsCarryingObject => isPlacing && previewInstance != null;
    public bool IsMaterialWheelOpen => materialWheelController != null && materialWheelController.IsOpen();

    public bool CanMove
    {
        get
        {
            if (IsMaterialWheelOpen) return false;
            if (isPlacing) return false;
            if (isEditingExistingObject) return false;
            return true;
        }
    }

    public Vector2 MovementInput
    {
        get
        {
            if (!CanMove || movementAction.action == null)
                return Vector2.zero;

            Vector2 input = movementAction.action.ReadValue<Vector2>();
            return input.magnitude < joystickDeadzone ? Vector2.zero : input;
        }
    }

    public bool ShouldBlockMovement
    {
        get
        {
            if (IsMaterialWheelOpen)
                return true;

            if (isPlacing && useManualPlacementHeight && rightHeightAdjustAction.action != null)
            {
                Vector2 stick = rightHeightAdjustAction.action.ReadValue<Vector2>();
                if (Mathf.Abs(stick.y) >= joystickDeadzone)
                    return true;
            }

            return false;
        }
    }

    void Awake()
    {
        Instance = this;
        EnsurePlacedObjectsParent();

        if (enableDebugLogs)
            Debug.Log("[WallPlacer_VR] Instance set on: " + gameObject.name);
    }

    void OnEnable()
    {
        EnableAction(rightPlaceAction);
        EnableAction(rightEditAction);
        EnableAction(cancelAction);
        EnableAction(leftWheelGripAction);
        EnableAction(leftApplyMaterialAction);
        EnableAction(leftJoystickAction);
        EnableAction(rightHeightAdjustAction);
        EnableAction(movementAction);
    }

    void OnDisable()
    {
        DisableAction(rightPlaceAction);
        DisableAction(rightEditAction);
        DisableAction(cancelAction);
        DisableAction(leftWheelGripAction);
        DisableAction(leftApplyMaterialAction);
        DisableAction(leftJoystickAction);
        DisableAction(rightHeightAdjustAction);
        DisableAction(movementAction);
    }

    void Update()
    {
        bool rightPlacePressed = WasPressed(rightPlaceAction);
        bool rightEditPressed = WasPressed(rightEditAction);
        bool cancelPressed = WasPressed(cancelAction);
        bool leftWheelPressed = WasPressed(leftWheelGripAction);
        bool leftApplyPressed = WasPressed(leftApplyMaterialAction);

        if (cancelPressed)
        {
            HandleCancel();
            return;
        }

        if (IsMaterialWheelOpen)
        {
            if (leftJoystickAction.action != null)
                materialWheelController.UpdateJoystickHighlight(leftJoystickAction.action.ReadValue<Vector2>());

            if (rightPlacePressed || leftApplyPressed)
                TryApplyCurrentMaterialSelection();

            return;
        }

        if (leftWheelPressed)
        {
            TryOpenMaterialWheelOnLookedObject();
            return;
        }

        if (isPlacing)
        {
            AdjustPreviewRotation();
            AdjustManualPlacementHeight();
            UpdatePreviewPosition();

            if (rightPlacePressed && canPlaceCurrentPreview)
                PlaceObject();
        }
        else
        {
            if (rightEditPressed && Time.time >= suppressEditUntilTime)
                TryEditObject();
        }
    }

    void HandleCancel()
    {
        if (IsMaterialWheelOpen)
        {
            materialWheelController.RestoreOriginalMaterials();
            materialWheelController.CloseWheel();
            return;
        }

        if (InvetoryController.Instance != null && InvetoryController.IsMenuOpen())
        {
            InvetoryController.Instance.CloseInventory();
            return;
        }

        if (isPlacing)
            CancelPlacement();
    }

    public void StartPlacement(InventoryItemData itemData1)
    {
        if (itemData1 == null)
        {
            Debug.LogError("[WallPlacer_VR] itemData is NULL");
            return;
        }

        if (itemData1.prefab3D == null)
        {
            Debug.LogError("[WallPlacer_VR] itemData.prefab3D is NULL for item: " + itemData1.GetName());
            return;
        }

        if (rightControllerTransform == null)
        {
            Debug.LogError("[WallPlacer_VR] rightControllerTransform is NULL");
            return;
        }

        currentSelectedItem = itemData1;
        currentPlacementRotationOffset = itemData1.placementRotationOffset;

        useManualPlacementHeight = itemData1.useManualPlacementHeight;
        manualPlacementHeight = itemData1.manualPlacementHeight;
        manualHeightAdjustSpeed = itemData1.manualHeightAdjustSpeed;
        minManualPlacementHeight = itemData1.minManualPlacementHeight;
        maxManualPlacementHeight = itemData1.maxManualPlacementHeight;

        lastSavedMaterials = null;
        isEditingExistingObject = false;
        editSourceObject = null;
        SuppressEditInputTemporarily();
        StartPlacementInternal(itemData1.prefab3D);
    }

    void StartPlacementInternal(GameObject prefab)
    {
        DestroyIfExists(previewInstance);
        DestroyIfExists(editSourceObject);

        previewInstance = Instantiate(prefab);
        previewInstance.name = prefab.name + "_Preview";
        previewInstance.SetActive(true);

        if (disableScriptsOnPreview)
            DisableBehavioursForPreview(previewInstance);

        if (makePreviewCollidersTriggers)
            ConvertPreviewCollidersToTriggers(previewInstance);

        MakePreviewTransparent(previewInstance, PREVIEW_ALPHA);

        isPlacing = true;
        isEditingExistingObject = false;
        editSourceObject = null;
        canPlaceCurrentPreview = false;

        if (enableDebugLogs)
            Debug.Log("[WallPlacer_VR] Preview created: " + previewInstance.name);

        UpdatePreviewPosition();
    }

    void AdjustPreviewRotation()
    {
        if (!isPlacing || previewInstance == null)
            return;

        if (leftJoystickAction.action == null)
            return;

        Vector2 joystickValue = leftJoystickAction.action.ReadValue<Vector2>();
        float x = joystickValue.x;

        if (Mathf.Abs(x) < joystickDeadzone)
            return;

        currentPlacementRotationOffset.y += x * rotationSpeed * Time.deltaTime;
    }

    void AdjustManualPlacementHeight()
    {
        if (!isPlacing || !useManualPlacementHeight)
            return;

        if (rightHeightAdjustAction.action == null)
            return;

        Vector2 joystickValue = rightHeightAdjustAction.action.ReadValue<Vector2>();
        float y = joystickValue.y;

        if (Mathf.Abs(y) < joystickDeadzone)
            return;

        manualPlacementHeight += y * manualHeightAdjustSpeed * Time.deltaTime;
        manualPlacementHeight = Mathf.Clamp(manualPlacementHeight, minManualPlacementHeight, maxManualPlacementHeight);

        if (currentSelectedItem != null)
            currentSelectedItem.manualPlacementHeight = manualPlacementHeight;
    }

    void UpdatePreviewPosition()
    {
        if (previewInstance == null || rightControllerTransform == null)
            return;

        // Rotation must be set before reading bounds
        previewInstance.transform.rotation = Quaternion.Euler(currentPlacementRotationOffset);

        Vector3 targetPos =
            rightControllerTransform.position +
            rightControllerTransform.forward * placeDistance +
            rightControllerTransform.up * controllerUpOffset;

        if (snapXZToGrid)
            targetPos = SnapToGridXZ(targetPos);

        if (useManualPlacementHeight)
        {
            if (lockManualHeightXZToGrid)
                targetPos = new Vector3(SnapToGridValue(targetPos.x), targetPos.y, SnapToGridValue(targetPos.z));

            if (TryFindSupportY(targetPos, out float supportY))
            {
                SnapPreviewBottomToY(supportY + manualPlacementHeight + surfaceOffset, targetPos);
                canPlaceCurrentPreview = true;
                SetPreviewAlpha(previewInstance, PREVIEW_ALPHA);
            }
            else
            {
                SnapPreviewBottomToY(manualPlacementHeight + surfaceOffset, targetPos);
                canPlaceCurrentPreview = true;
                SetPreviewAlpha(previewInstance, PREVIEW_ALPHA);
            }

            return;
        }

        if (TryFindSupportY(targetPos, out float groundY))
        {
            SnapPreviewBottomToY(groundY + surfaceOffset, targetPos);
            canPlaceCurrentPreview = true;
            SetPreviewAlpha(previewInstance, PREVIEW_ALPHA);
        }
        else
        {
            previewInstance.transform.position = targetPos;
            canPlaceCurrentPreview = false;
            SetPreviewAlpha(previewInstance, INVALID_PREVIEW_ALPHA);
        }
    }

    bool TryFindSupportY(Vector3 targetPos, out float supportY)
    {
        supportY = 0f;

        Vector3 rayOrigin = new Vector3(targetPos.x, targetPos.y + 25f, targetPos.z);
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 50f, placementSurfaceMask);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (previewInstance != null && hit.collider.transform.IsChildOf(previewInstance.transform))
                continue;

            if (hit.collider.CompareTag("Ground"))
            {
                supportY = hit.point.y;
                return true;
            }

            GameObject hitRoot = GetPlacedObjectRoot(hit.collider.transform);
            if (hitRoot != null && placedObjectsParent != null && hitRoot.transform.parent == placedObjectsParent)
            {
                supportY = hit.collider.bounds.max.y;
                return true;
            }
        }

        return false;
    }

    void SnapPreviewBottomToY(float targetY, Vector3 targetPos)
    {
        previewInstance.transform.position = new Vector3(targetPos.x, targetY, targetPos.z);

        Bounds b = GetWorldRendererBounds(previewInstance);

        float correction = targetY - b.min.y;
        previewInstance.transform.position = new Vector3(targetPos.x, targetY + correction, targetPos.z);
    }

    Bounds GetWorldRendererBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        Collider[] cols = obj.GetComponentsInChildren<Collider>(true);
        if (cols.Length > 0)
        {
            Bounds b = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) b.Encapsulate(cols[i].bounds);
            return b;
        }

        return new Bounds(obj.transform.position, Vector3.one);
    }

    void PlaceObject()
    {
        if (previewInstance == null || !canPlaceCurrentPreview)
            return;

        GameObject source = currentSelectedItem != null ? currentSelectedItem.prefab3D : null;

        if (isEditingExistingObject && editSourceObject != null)
            source = editSourceObject;

        if (source == null)
        {
            Debug.LogError("[WallPlacer_VR] PlaceObject failed: source is null");
            return;
        }

        if (source.name.Contains("PlacedObjects"))
        {
            Debug.LogError("[WallPlacer_VR] Refusing to place source because it looks like a container object: " + source.name);
            return;
        }

        EnsurePlacedObjectsParent();

        placedObject = Instantiate(source, previewInstance.transform.position, previewInstance.transform.rotation);
        PlacedItemData dataHolder = placedObject.GetComponent<PlacedItemData>();
        if (dataHolder == null)
            dataHolder = placedObject.AddComponent<PlacedItemData>();

        dataHolder.itemData1 = currentSelectedItem;
        if (placedObjectsParent != null)
            placedObject.transform.SetParent(placedObjectsParent, true);

        placedObject.SetActive(true);

        if (disableScriptsOnPlacedObjects)
            DisableBehavioursForPlacedObject(placedObject);

        if (isEditingExistingObject)
            ApplySavedMaterials(placedObject, false);

        if (enableDebugLogs)
            Debug.Log("[WallPlacer_VR] Placed object: " + placedObject.name);

        // ── Register with FurnitureSaveManager ──────────────────────────────
        if (currentSelectedItem != null)
        {
            FurniturePrefabReference prefabRef = placedObject.GetComponent<FurniturePrefabReference>();
            if (prefabRef == null) prefabRef = placedObject.AddComponent<FurniturePrefabReference>();
            prefabRef.prefabPath = currentSelectedItem.name; // matches the key FurnitureSaveManager uses
            prefabRef.itemData   = currentSelectedItem;

            FurnitureSaveManager.Instance?.RegisterFurniture(placedObject);
        }
        // ────────────────────────────────────────────────────────────────────

        DestroyIfExists(previewInstance);
        DestroyIfExists(editSourceObject);

        isPlacing = false;
        isEditingExistingObject = false;
        canPlaceCurrentPreview = false;
        lastSavedMaterials = null;
        useManualPlacementHeight = false;
        SuppressEditInputTemporarily();
    }

    void TryEditObject()
    {
        if (rightControllerTransform == null)
            return;

        Ray ray = new Ray(rightControllerTransform.position, rightControllerTransform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance) || hit.collider == null)
            return;

        GameObject target = GetPlacedObjectRoot(hit.collider.transform);
        if (target == gameObject || target == previewInstance)
            return;

        if (target.CompareTag("Ground"))
            return;

        Renderer rend = target.GetComponentInChildren<Renderer>();
        if (rend == null)
            return;

        InventoryItemData itemData1 = target.GetComponent<PlacedItemData>()?.itemData1;

        if (itemData1 != null)
        {
            currentSelectedItem = itemData1;

            useManualPlacementHeight = itemData1.useManualPlacementHeight;
            manualPlacementHeight = itemData1.manualPlacementHeight;
            manualHeightAdjustSpeed = itemData1.manualHeightAdjustSpeed;
            minManualPlacementHeight = itemData1.minManualPlacementHeight;
            maxManualPlacementHeight = itemData1.maxManualPlacementHeight;
        }

        lastSavedMaterials = rend.materials;
        oldObjectPosition = target.transform.position;
        oldObjectRotation = target.transform.rotation;

        currentPlacementRotationOffset = target.transform.eulerAngles;

        DestroyIfExists(editSourceObject);
        editSourceObject = Instantiate(target, target.transform.position, target.transform.rotation);
        editSourceObject.SetActive(false);

        DestroyIfExists(previewInstance);
        previewInstance = Instantiate(target, target.transform.position, target.transform.rotation);
        previewInstance.name = target.name + "_Preview";
        previewInstance.SetActive(true);

        if (disableScriptsOnPreview)
            DisableBehavioursForPreview(previewInstance);

        if (makePreviewCollidersTriggers)
            ConvertPreviewCollidersToTriggers(previewInstance);

        MakePreviewTransparent(previewInstance, PREVIEW_ALPHA);
        ApplySavedMaterials(previewInstance, true);

        isPlacing = true;
        isEditingExistingObject = true;
        canPlaceCurrentPreview = false;

        // ── Unregister the original before destroying it ─────────────────────
        FurnitureSaveManager.Instance?.UnregisterFurniture(target);
        // ────────────────────────────────────────────────────────────────────

        Destroy(target);
    }

    void TryOpenMaterialWheelOnLookedObject()
    {
        if (materialWheelController == null || leftControllerTransform == null)
            return;

        Ray ray = new Ray(leftControllerTransform.position, leftControllerTransform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance) || hit.collider == null)
            return;

        GameObject target = GetPlacedObjectRoot(hit.collider.transform);
        if (target == gameObject || target == previewInstance)
            return;

        Renderer rend = target.GetComponentInChildren<Renderer>();
        if (rend == null)
            return;

        lastSavedMaterials = rend.materials;
        materialWheelController.SelectObject(rend);
        materialWheelController.OpenWheelAuto();
    }

    void TryApplyCurrentMaterialSelection()
    {
        if (materialWheelController == null || !materialWheelController.IsOpen())
            return;

        materialWheelController.ApplyHighlightedVariant();
    }

    void CancelPlacement()
    {
        DestroyIfExists(previewInstance);

        if (isEditingExistingObject && editSourceObject != null)
        {
            GameObject restored = Instantiate(editSourceObject, oldObjectPosition, oldObjectRotation);
            restored.SetActive(true);
            ApplySavedMaterials(restored, false);

            // ── Restore registration on cancel ───────────────────────────────
            FurniturePrefabReference oldRef = editSourceObject.GetComponent<FurniturePrefabReference>();
            if (oldRef != null)
            {
                FurniturePrefabReference newRef = restored.GetComponent<FurniturePrefabReference>();
                if (newRef == null) newRef = restored.AddComponent<FurniturePrefabReference>();
                newRef.prefabPath = oldRef.prefabPath;
                newRef.itemData   = oldRef.itemData;
            }
            FurnitureSaveManager.Instance?.RegisterFurniture(restored);
            // ─────────────────────────────────────────────────────────────────
        }

        DestroyIfExists(editSourceObject);

        isPlacing = false;
        isEditingExistingObject = false;
        canPlaceCurrentPreview = false;
        lastSavedMaterials = null;
        useManualPlacementHeight = false;
        SuppressEditInputTemporarily();
    }

    void EnsurePlacedObjectsParent()
    {
        if (placedObjectsParent != null)
        {
            if (!placedObjectsParent.gameObject.scene.IsValid())
            {
                Debug.LogWarning("[WallPlacer_VR] placedObjectsParent was assigned to a prefab asset. Clearing it.");
                placedObjectsParent = null;
            }
            else
            {
                return;
            }
        }

        if (!autoCreatePlacedObjectsParent)
            return;

        GameObject root = GameObject.Find("PlacedObjects");
        if (root == null)
            root = new GameObject("PlacedObjects");

        placedObjectsParent = root.transform;
    }

    void DisableBehavioursForPreview(GameObject obj)
    {
        MonoBehaviour[] behaviours = obj.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            if (behaviour == this)
                continue;

            behaviour.enabled = false;
        }
    }

    void DisableBehavioursForPlacedObject(GameObject obj)
    {
        MonoBehaviour[] behaviours = obj.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            behaviour.enabled = false;
        }
    }

    void ConvertPreviewCollidersToTriggers(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);

        foreach (Collider col in colliders)
        {
            if (col == null)
                continue;

            MeshCollider meshCol = col as MeshCollider;
            if (meshCol != null)
            {
                if (!meshCol.convex)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"[WallPlacer_VR] Skipping trigger conversion for concave MeshCollider on {meshCol.gameObject.name}", meshCol);

                    continue;
                }
            }

            col.isTrigger = true;
        }
    }

    bool WasPressed(InputActionProperty actionProperty)
    {
        return actionProperty.action != null && actionProperty.action.WasPressedThisFrame();
    }

    void EnableAction(InputActionProperty actionProperty)
    {
        if (actionProperty.action != null)
            actionProperty.action.Enable();
    }

    void DisableAction(InputActionProperty actionProperty)
    {
        if (actionProperty.action != null)
            actionProperty.action.Disable();
    }

    void ApplySavedMaterials(GameObject obj, bool isPreview)
    {
        if (lastSavedMaterials == null)
            return;

        Renderer rend = obj.GetComponentInChildren<Renderer>();
        if (rend == null)
            return;

        Material[] mats = rend.materials;

        for (int i = 0; i < mats.Length && i < lastSavedMaterials.Length; i++)
        {
            mats[i] = new Material(lastSavedMaterials[i]);

            if (isPreview)
            {
                Shader transparentShader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
                if (transparentShader != null)
                    mats[i].shader = transparentShader;

                Color c = mats[i].color;
                c.a = PREVIEW_ALPHA;
                mats[i].color = c;
            }
        }

        rend.materials = mats;
    }

    GameObject GetPlacedObjectRoot(Transform hitTransform)
    {
        if (hitTransform == null)
            return null;

        if (placedObjectsParent != null)
        {
            Transform current = hitTransform;

            while (current != null)
            {
                if (current.parent == placedObjectsParent)
                    return current.gameObject;

                current = current.parent;
            }
        }

        return hitTransform.root.gameObject;
    }

    void MakePreviewTransparent(GameObject obj, float alpha)
    {
        Renderer[] rends = obj.GetComponentsInChildren<Renderer>(true);
        Shader transparentShader = Shader.Find("Legacy Shaders/Transparent/Diffuse");

        foreach (Renderer r in rends)
        {
            Material[] mats = r.materials;

            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = new Material(mats[i]);

                if (transparentShader != null)
                    mats[i].shader = transparentShader;

                Color c = mats[i].color;
                c.a = alpha;
                mats[i].color = c;
            }

            r.materials = mats;
        }
    }

    void SetPreviewAlpha(GameObject obj, float alpha)
    {
        if (obj == null)
            return;

        Renderer[] rends = obj.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in rends)
        {
            Material[] mats = r.materials;

            for (int i = 0; i < mats.Length; i++)
            {
                Color c = mats[i].color;
                c.a = alpha;
                mats[i].color = c;
            }

            r.materials = mats;
        }
    }

    void SuppressEditInputTemporarily()
    {
        suppressEditUntilTime = Time.time + EDIT_INPUT_SUPPRESS_DURATION;
    }

    Vector3 SnapToGridXZ(Vector3 pos)
    {
        pos.x = SnapToGridValue(pos.x);
        pos.z = SnapToGridValue(pos.z);
        return pos;
    }

    float SnapToGridValue(float value)
    {
        return Mathf.Round(value / gridSize) * gridSize;
    }

    void DestroyIfExists(GameObject obj)
    {
        if (obj != null)
            Destroy(obj);
    }
}