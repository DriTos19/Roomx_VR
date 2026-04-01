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
    public InputActionProperty rightHeightAdjustAction;   // NEW
    public InputActionProperty cancelAction;

    [Header("Left Hand Input")]
    public InputActionProperty leftWheelGripAction;
    public InputActionProperty leftApplyMaterialAction;
    public InputActionProperty leftJoystickAction;
    private float suppressEditUntilTime = 0f;
    private const float EDIT_INPUT_SUPPRESS_DURATION = 0.25f;
    [Header("Placement")]
    public float placeDistance = 2.2f;
    public float minPlaceDistance = 1.0f;
    public float maxDistance = 6.0f;
    public float distanceAdjustSpeed = 2f;
    public float joystickDeadzone = 0.2f;
    public float gridSize = 0.5f;
    public float surfaceOffset = 0f;
    public LayerMask placementSurfaceMask = ~0;
    public float overlapShrink = 0.95f;

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
            if (leftApplyPressed)
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
            AdjustPlacementDistance();
            AdjustManualPlacementHeight();   // NEW
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

        if (InventoryManager.Instance != null && InventoryManager.IsMenuOpen())
        {
            InventoryManager.Instance.CloseInventory();
            return;
        }

        if (isPlacing)
            CancelPlacement();
    }

    public void StartPlacement(InventoryItemData itemData)
    {
        if (itemData == null)
        {
            Debug.LogError("[WallPlacer_VR] itemData is NULL");
            return;
        }

        if (itemData.prefab3D == null)
        {
            Debug.LogError("[WallPlacer_VR] itemData.prefab3D is NULL for item: " + itemData.GetName());
            return;
        }

        if (rightControllerTransform == null)
        {
            Debug.LogError("[WallPlacer_VR] rightControllerTransform is NULL");
            return;
        }

        currentSelectedItem = itemData;
        currentPlacementRotationOffset = itemData.placementRotationOffset;

        useManualPlacementHeight = itemData.useManualPlacementHeight;
        manualPlacementHeight = itemData.manualPlacementHeight;
        manualHeightAdjustSpeed = itemData.manualHeightAdjustSpeed;   // NEW
        minManualPlacementHeight = itemData.minManualPlacementHeight;
        maxManualPlacementHeight = itemData.maxManualPlacementHeight;

        

        lastSavedMaterials = null;
        isEditingExistingObject = false;
        editSourceObject = null;
        SuppressEditInputTemporarily();
        StartPlacementInternal(itemData.prefab3D);
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

    void AdjustPlacementDistance()
    {
        if (leftJoystickAction.action == null)
            return;

        Vector2 joystickValue = leftJoystickAction.action.ReadValue<Vector2>();
        float y = joystickValue.y;

        if (Mathf.Abs(y) < joystickDeadzone)
            return;

        placeDistance += y * distanceAdjustSpeed * Time.deltaTime;
        placeDistance = Mathf.Clamp(placeDistance, minPlaceDistance, maxDistance);
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

        Bounds localBounds = GetLocalObjectBounds(previewInstance);
        float bottomToPivotOffset = GetBottomToPivotOffset(localBounds, previewInstance.transform.lossyScale);

        Vector3 rawTargetPos =
            rightControllerTransform.position +
            rightControllerTransform.forward * placeDistance +
            rightControllerTransform.up * controllerUpOffset;

        Vector3 targetPos = rawTargetPos;

        if (snapXZToGrid)
            targetPos = SnapToGridXZ(targetPos);

        previewInstance.transform.rotation = Quaternion.Euler(currentPlacementRotationOffset);

        if (useManualPlacementHeight)
        {
            Vector3 manualPos = new Vector3(
                targetPos.x,
                manualPlacementHeight + bottomToPivotOffset + surfaceOffset,
                targetPos.z
            );

            if (lockManualHeightXZToGrid)
                manualPos = new Vector3(SnapToGridValue(manualPos.x), manualPos.y, SnapToGridValue(manualPos.z));

            previewInstance.transform.position = manualPos;
            canPlaceCurrentPreview = true;
            SetPreviewAlpha(previewInstance, PREVIEW_ALPHA);
            return;
        }

        if (TryGetStablePlacementPosition(targetPos, localBounds, bottomToPivotOffset, out Vector3 solvedPos, out _))
        {
            previewInstance.transform.position = solvedPos;
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

    bool TryGetStablePlacementPosition(Vector3 targetPos, Bounds localBounds, float bottomToPivotOffset, out Vector3 finalPos, out GameObject supportObject)
    {
        finalPos = targetPos;
        supportObject = null;

        Vector3 scaledExtents = Vector3.Scale(localBounds.extents, previewInstance.transform.lossyScale);
        Vector3 centerOffset = GetBoundsCenterOffset(localBounds, previewInstance.transform.lossyScale);

        Vector3 halfExtents = new Vector3(
            Mathf.Max(scaledExtents.x * 0.95f, 0.01f),
            0.05f,
            Mathf.Max(scaledExtents.z * 0.95f, 0.01f)
        );

        Vector3 boxCenter = new Vector3(
            targetPos.x + centerOffset.x,
            maxDistance,
            targetPos.z + centerOffset.z
        );

        Collider[] hits = Physics.OverlapBox(
            boxCenter,
            new Vector3(halfExtents.x, maxDistance, halfExtents.z),
            Quaternion.identity,
            placementSurfaceMask
        );

        Collider bestSupportCollider = null;
        float bestTopY = float.NegativeInfinity;

        foreach (Collider col in hits)
        {
            if (col == null)
                continue;

            GameObject root = GetPlacedObjectRoot(col.transform);
            if (root == previewInstance || root == gameObject)
                continue;

            float topY = col.bounds.max.y;

            if (topY <= maxDistance && topY > bestTopY)
            {
                bestTopY = topY;
                bestSupportCollider = col;
            }
        }

        if (bestSupportCollider == null)
            return false;

        supportObject = bestSupportCollider.transform.root.gameObject;

        Vector3 candidatePos = new Vector3(
            targetPos.x,
            bestSupportCollider.bounds.max.y + bottomToPivotOffset + surfaceOffset,
            targetPos.z
        );

        if (WouldOverlapAtPosition(candidatePos, localBounds, supportObject))
            return false;

        finalPos = candidatePos;
        return true;
    }

    bool WouldOverlapAtPosition(Vector3 candidatePos, Bounds localBounds, GameObject supportObject)
    {
        Vector3 scaledExtents = Vector3.Scale(localBounds.extents, previewInstance.transform.lossyScale);
        Vector3 centerOffset = GetBoundsCenterOffset(localBounds, previewInstance.transform.lossyScale);

        Vector3 halfExtents = scaledExtents * overlapShrink;
        Vector3 worldCenter = candidatePos + centerOffset;

        Collider[] overlaps = Physics.OverlapBox(
            worldCenter,
            halfExtents,
            Quaternion.identity,
            placementSurfaceMask
        );

        foreach (Collider col in overlaps)
        {
            if (col == null)
                continue;

            GameObject root = GetPlacedObjectRoot(col.transform);
            if (root == null)
                continue;

            if (root == previewInstance || root == gameObject)
                continue;

            if (root == supportObject)
                continue;

            return true;
        }

        return false;
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

        // Safety: do not allow cloning scene container/prefab asset by mistake
        if (source.name.Contains("PlacedObjects"))
        {
            Debug.LogError("[WallPlacer_VR] Refusing to place source because it looks like a container object: " + source.name);
            return;
        }

        EnsurePlacedObjectsParent();

        placedObject = Instantiate(source, previewInstance.transform.position, previewInstance.transform.rotation);

        if (placedObjectsParent != null)
            placedObject.transform.SetParent(placedObjectsParent, true);

        placedObject.SetActive(true);

        if (disableScriptsOnPlacedObjects)
            DisableBehavioursForPlacedObject(placedObject);

        if (isEditingExistingObject)
            ApplySavedMaterials(placedObject, false);

        if (enableDebugLogs)
            Debug.Log("[WallPlacer_VR] Placed object: " + placedObject.name);

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

        if (target.CompareTag("Floor"))
            return;

        Renderer rend = target.GetComponentInChildren<Renderer>();
        if (rend == null)
            return;

        lastSavedMaterials = rend.materials;
        oldObjectPosition = target.transform.position;
        oldObjectRotation = target.transform.rotation;

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
        materialWheelController.OpenWheel(0);
    }

    void TryApplyCurrentMaterialSelection()
    {
        if (materialWheelController == null || !materialWheelController.IsOpen())
            return;

        materialWheelController.CloseWheel();
    }

    void CancelPlacement()
    {
        DestroyIfExists(previewInstance);

        if (isEditingExistingObject && editSourceObject != null)
        {
            GameObject restored = Instantiate(editSourceObject, oldObjectPosition, oldObjectRotation);
            restored.SetActive(true);
            ApplySavedMaterials(restored, false);
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
            // If someone assigned a prefab asset instead of a scene object, ignore it
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

    Bounds GetLocalObjectBounds(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);

        if (colliders.Length > 0)
        {
            Bounds bounds = new Bounds(obj.transform.InverseTransformPoint(colliders[0].bounds.center), Vector3.zero);

            foreach (Collider c in colliders)
            {
                Bounds worldBounds = c.bounds;
                Vector3 localCenter = obj.transform.InverseTransformPoint(worldBounds.center);
                Vector3 localSize = obj.transform.InverseTransformVector(worldBounds.size);

                Bounds localBounds = new Bounds(
                    localCenter,
                    new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z))
                );

                bounds.Encapsulate(localBounds.min);
                bounds.Encapsulate(localBounds.max);
            }

            return bounds;
        }

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length > 0)
        {
            Bounds bounds = new Bounds(obj.transform.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);

            foreach (Renderer r in renderers)
            {
                Bounds worldBounds = r.bounds;
                Vector3 localCenter = obj.transform.InverseTransformPoint(worldBounds.center);
                Vector3 localSize = obj.transform.InverseTransformVector(worldBounds.size);

                Bounds localBounds = new Bounds(
                    localCenter,
                    new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z))
                );

                bounds.Encapsulate(localBounds.min);
                bounds.Encapsulate(localBounds.max);
            }

            return bounds;
        }

        return new Bounds(Vector3.zero, Vector3.one);
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

    Vector3 GetBoundsCenterOffset(Bounds localBounds, Vector3 lossyScale)
    {
        return new Vector3(
            localBounds.center.x * lossyScale.x,
            localBounds.center.y * lossyScale.y,
            localBounds.center.z * lossyScale.z
        );
    }

    float GetBottomToPivotOffset(Bounds localBounds, Vector3 lossyScale)
    {
        float bottomLocalY = localBounds.min.y;
        float bottomWorldY = bottomLocalY * lossyScale.y;
        return -bottomWorldY;
    }

    void DestroyIfExists(GameObject obj)
    {
        if (obj != null)
            Destroy(obj);
    }
}