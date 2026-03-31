using UnityEngine;
using UnityEngine.InputSystem;

public class WallPlacer_VR : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject previewPrefab;
    public GameObject realPrefab;

    [Header("VR Controller")]
    public Transform rightControllerTransform;

    [Header("Input Actions")]
    public InputActionProperty placeAction;
    public InputActionProperty editAction;
    public InputActionProperty cancelAction;
    public InputActionProperty wheelAction;

    [Header("Placement")]
    public float placeDistance = 4f;
    public float maxDistance = 10f;
    public float gridSize = 0.5f;
    public float surfaceOffset = 0f;
    public LayerMask placementSurfaceMask = ~0;
    public float supportPointInset = 0.9f;
    public float maxAllowedHeightDifference = 0.05f;
    public float overlapShrink = 0.95f;

    [Header("Material Wheel")]
    public MaterialWheelController materialWheelController;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    private GameObject previewInstance;
    private GameObject placedObject;
    private GameObject editSourceObject;

    private Material[] lastSavedMaterials;
    private Vector3 oldObjectPosition;
    private Quaternion oldObjectRotation;

    private bool isPlacing;
    private bool isEditingExistingObject;
    private bool canPlaceCurrentPreview;

    private const float PREVIEW_ALPHA = 0.5f;
    private const float INVALID_PREVIEW_ALPHA = 0.2f;

    void OnEnable()
    {
        EnableAction(placeAction);
        EnableAction(editAction);
        EnableAction(cancelAction);
        EnableAction(wheelAction);
    }

    void OnDisable()
    {
        DisableAction(placeAction);
        DisableAction(editAction);
        DisableAction(cancelAction);
        DisableAction(wheelAction);
    }

    void Start()
    {
        StartPlacement();

        if (enableDebugLogs)
        {
            Debug.Log("[WallPlacer_VR] Start");
            Debug.Log("[WallPlacer_VR] rightControllerTransform = " + (rightControllerTransform != null ? rightControllerTransform.name : "NULL"));
            Debug.Log("[WallPlacer_VR] placeAction = " + (placeAction.action != null ? placeAction.action.name : "NULL"));
            Debug.Log("[WallPlacer_VR] editAction = " + (editAction.action != null ? editAction.action.name : "NULL"));
            Debug.Log("[WallPlacer_VR] cancelAction = " + (cancelAction.action != null ? cancelAction.action.name : "NULL"));
            Debug.Log("[WallPlacer_VR] wheelAction = " + (wheelAction.action != null ? wheelAction.action.name : "NULL"));
        }
    }

    void Update()
    {
        bool vrPlace = WasPressed(placeAction);
        bool vrEdit = WasPressed(editAction);
        bool vrCancel = WasPressed(cancelAction);
        bool vrWheel = WasPressed(wheelAction);

        // Handle material wheel first
        if (materialWheelController != null && materialWheelController.IsOpen())
        {
            if (vrCancel)
            {
                materialWheelController.RestoreOriginalMaterials();
                materialWheelController.CloseWheel();

                if (enableDebugLogs)
                    Debug.Log("[WallPlacer_VR] Wheel cancelled -> restored original materials");
            }

            return;
        }

        if (isPlacing)
        {
            UpdatePreviewPosition();

            if (vrPlace && canPlaceCurrentPreview)
                PlaceObject();

            if (vrCancel)
                CancelPlacement();

            if (vrWheel)
                TryOpenMaterialWheelOnLookedObject();
        }
        else
        {
            if (vrEdit)
                TryEditObject();

            if (vrWheel)
                TryOpenMaterialWheelOnLookedObject();
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

    void StartPlacement()
    {
        DestroyIfExists(previewInstance);
        DestroyIfExists(editSourceObject);

        if (previewPrefab == null)
        {
            Debug.LogError("[WallPlacer_VR] previewPrefab is NULL");
            return;
        }

        previewInstance = Instantiate(previewPrefab);
        previewInstance.name = previewPrefab.name + "_Preview";
        previewInstance.SetActive(true);
        MakePreviewTransparent(previewInstance, PREVIEW_ALPHA);

        isPlacing = true;
        isEditingExistingObject = false;
        editSourceObject = null;
        canPlaceCurrentPreview = false;

        if (enableDebugLogs)
            Debug.Log("[WallPlacer_VR] Preview created: " + previewInstance.name);
    }

    void UpdatePreviewPosition()
    {
        if (previewInstance == null)
            return;

        if (rightControllerTransform == null)
        {
            Debug.LogError("[WallPlacer_VR] rightControllerTransform is NULL");
            return;
        }

        Bounds localBounds = GetLocalObjectBounds(previewInstance);
        float bottomToPivotOffset = GetBottomToPivotOffset(localBounds, previewInstance.transform.lossyScale);

        Vector3 targetPos = rightControllerTransform.position + rightControllerTransform.forward * placeDistance;

        // PREVIEW MOVEMENT IS DIRECTLY BASED ON SNAP TO GRID
        Vector3 snappedTargetPos = SnapToGrid(targetPos);

        // Always move preview to the snapped X/Z position.
        // Y is solved from the surface if possible.
        previewInstance.transform.rotation = Quaternion.identity;

        if (TryGetStablePlacementPosition(snappedTargetPos, localBounds, bottomToPivotOffset, out Vector3 solvedPos, out GameObject supportObject))
        {
            previewInstance.transform.position = solvedPos;
            canPlaceCurrentPreview = true;
            SetPreviewAlpha(previewInstance, PREVIEW_ALPHA);

            if (enableDebugLogs && supportObject != null)
                Debug.Log("[WallPlacer_VR] Stable support: " + supportObject.name);
        }
        else
        {
            // Still follow snapped grid position even when invalid.
            // Use fallback Y so the preview continues to visually follow the grid.
            Vector3 fallbackPos = new Vector3(
                snappedTargetPos.x,
                snappedTargetPos.y,
                snappedTargetPos.z
            );

            previewInstance.transform.position = fallbackPos;
            canPlaceCurrentPreview = false;
            SetPreviewAlpha(previewInstance, INVALID_PREVIEW_ALPHA);

            if (enableDebugLogs)
                Debug.Log("[WallPlacer_VR] Invalid placement, but preview still follows snapped grid");
        }
    }

    bool TryGetStablePlacementPosition(Vector3 targetPos, Bounds localBounds, float bottomToPivotOffset, out Vector3 finalPos, out GameObject supportObject)
{
    finalPos = targetPos;
    supportObject = null;

    Vector3 scaledExtents = Vector3.Scale(localBounds.extents, previewInstance.transform.lossyScale);
    Vector3 centerOffset = GetBoundsCenterOffset(localBounds, previewInstance.transform.lossyScale);

    // Box area used to detect what is below the preview.
    Vector3 boxHalfExtents = new Vector3(
        Mathf.Max(scaledExtents.x * 0.95f, 0.01f),
        0.05f,
        Mathf.Max(scaledExtents.z * 0.95f, 0.01f)
    );

    // Start from above and scan downward to find support below the preview footprint.
    Vector3 boxCenter = new Vector3(
        targetPos.x + centerOffset.x,
        maxDistance,
        targetPos.z + centerOffset.z
    );

    Collider[] hits = Physics.OverlapBox(
        boxCenter,
        new Vector3(boxHalfExtents.x, maxDistance, boxHalfExtents.z),
        Quaternion.identity,
        placementSurfaceMask
    );

    Collider bestSupportCollider = null;
    float bestTopY = float.NegativeInfinity;

    foreach (Collider col in hits)
    {
        if (col == null)
            continue;

        GameObject root = col.transform.root.gameObject;

        if (root == previewInstance || root == gameObject)
            continue;

        float topY = col.bounds.max.y;

        // We only care about colliders below the desired target region.
        if (topY <= maxDistance && topY > bestTopY)
        {
            bestTopY = topY;
            bestSupportCollider = col;
        }
    }

    if (bestSupportCollider == null)
        return false;

    supportObject = bestSupportCollider.transform.root.gameObject;

    // Place preview so its collider bottom sits exactly on support collider top.
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

            GameObject root = col.transform.root.gameObject;

            if (root == null)
                continue;

            if (root == previewInstance || root == gameObject)
                continue;

            // Ignore the support object because stacking should touch it.
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

        GameObject source = realPrefab;

        if (isEditingExistingObject && editSourceObject != null)
            source = editSourceObject;

        if (source == null)
        {
            Debug.LogError("[WallPlacer_VR] source is NULL");
            return;
        }

        placedObject = Instantiate(source, previewInstance.transform.position, Quaternion.identity);
        placedObject.SetActive(true);

        ApplySavedMaterials(placedObject, false);

        DestroyIfExists(previewInstance);
        DestroyIfExists(editSourceObject);

        isPlacing = false;
        isEditingExistingObject = false;
        canPlaceCurrentPreview = false;

        if (enableDebugLogs)
            Debug.Log("[WallPlacer_VR] Object placed at " + placedObject.transform.position);
    }

    void TryEditObject()
    {
        if (rightControllerTransform == null)
        {
            Debug.LogError("[WallPlacer_VR] Cannot edit: rightControllerTransform is NULL");
            return;
        }

        Ray ray = new Ray(rightControllerTransform.position, rightControllerTransform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance) || hit.collider == null)
            return;

        GameObject target = hit.collider.transform.root.gameObject;

        if (target == gameObject || target == previewInstance)
            return;

        if (target.CompareTag("Floor"))
        {
            Debug.Log("[WallPlacer_VR] Floor cannot be edited.");
            return;
        }

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

        MakePreviewTransparent(previewInstance, PREVIEW_ALPHA);
        ApplySavedMaterials(previewInstance, true);

        Destroy(target);

        isPlacing = true;
        isEditingExistingObject = true;
        canPlaceCurrentPreview = false;

        if (enableDebugLogs)
            Debug.Log("[WallPlacer_VR] Editing object: " + previewInstance.name);
    }

    void TryOpenMaterialWheelOnLookedObject()
    {
        if (materialWheelController == null || rightControllerTransform == null)
            return;

        Ray ray = new Ray(rightControllerTransform.position, rightControllerTransform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance) || hit.collider == null)
            return;

        GameObject target = hit.collider.transform.root.gameObject;

        if (target == gameObject || target == previewInstance)
            return;

        Renderer rend = target.GetComponentInChildren<Renderer>();
        if (rend == null)
            return;

        // 🔴 SAVE ORIGINAL MATERIALS
        lastSavedMaterials = rend.materials;

        materialWheelController.SelectObject(rend);
        materialWheelController.OpenWheel(0);
    }
    
    void CloseMaterialWheelWithoutApplying()
    {
        if (materialWheelController == null)
            return;

        // 🔴 restore original materials
        materialWheelController.RestoreOriginalMaterials();

        // 🔴 close the wheel
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

        if (enableDebugLogs)
            Debug.Log("[WallPlacer_VR] Placement cancelled");
    }

    Bounds GetLocalObjectBounds(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);

        if (colliders.Length > 0)
        {
            Bounds bounds = new Bounds(
                obj.transform.InverseTransformPoint(colliders[0].bounds.center),
                Vector3.zero
            );

            foreach (Collider c in colliders)
            {
                Bounds worldBounds = c.bounds;

                Vector3 localCenter = obj.transform.InverseTransformPoint(worldBounds.center);
                Vector3 localSize = obj.transform.InverseTransformVector(worldBounds.size);

                Bounds localBounds = new Bounds(
                    localCenter,
                    new Vector3(
                        Mathf.Abs(localSize.x),
                        Mathf.Abs(localSize.y),
                        Mathf.Abs(localSize.z)
                    )
                );

                bounds.Encapsulate(localBounds.min);
                bounds.Encapsulate(localBounds.max);
            }

            return bounds;
        }

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length > 0)
        {
            Bounds bounds = new Bounds(
                obj.transform.InverseTransformPoint(renderers[0].bounds.center),
                Vector3.zero
            );

            foreach (Renderer r in renderers)
            {
                Bounds worldBounds = r.bounds;

                Vector3 localCenter = obj.transform.InverseTransformPoint(worldBounds.center);
                Vector3 localSize = obj.transform.InverseTransformVector(worldBounds.size);

                Bounds localBounds = new Bounds(
                    localCenter,
                    new Vector3(
                        Mathf.Abs(localSize.x),
                        Mathf.Abs(localSize.y),
                        Mathf.Abs(localSize.z)
                    )
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

    Vector3 SnapToGrid(Vector3 pos)
    {
        pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
        pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
        return pos;
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

    void OpenMaterialWheelForPreview()
    {
        if (materialWheelController == null || previewInstance == null)
            return;

        Renderer rend = previewInstance.GetComponentInChildren<Renderer>();
        if (rend == null)
            return;

        materialWheelController.SelectObject(rend);
        materialWheelController.OpenWheel(0);
    }

    void OpenMaterialWheelForPlacedObject()
    {
        if (materialWheelController == null || placedObject == null)
            return;

        Renderer rend = placedObject.GetComponentInChildren<Renderer>();
        if (rend == null)
            return;

        materialWheelController.SelectObject(rend);
        materialWheelController.OpenWheel(0);
    }

    void DestroyIfExists(GameObject obj)
    {
        if (obj != null)
            Destroy(obj);
    }
}