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
    public InputActionProperty placeAction;   // Assign: XRI RightHand Interaction / Select
    public InputActionProperty editAction;    // Assign: button you want for edit
    public InputActionProperty cancelAction;  // Assign: button you want for cancel
    public InputActionProperty wheelAction;   // Assign: grip / right click / material wheel

    [Header("Placement")]
    public float placeDistance = 4f;
    public float maxDistance = 10f;
    public float gridSize = 0.5f;

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

    private const float PREVIEW_ALPHA = 0.5f;

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
        if (materialWheelController != null && materialWheelController.IsOpen())
            return;

        bool vrPlace = WasPressed(placeAction);
        bool vrEdit = WasPressed(editAction);
        bool vrCancel = WasPressed(cancelAction);
        bool vrWheel = WasPressed(wheelAction);

        if (enableDebugLogs)
        {
            if (vrPlace) Debug.Log("[WallPlacer_VR] trigger pressed");
            if (vrEdit) Debug.Log("[WallPlacer_VR] edit pressed");
            if (vrCancel) Debug.Log("[WallPlacer_VR] cancel pressed");
            if (vrWheel) Debug.Log("[WallPlacer_VR] wheel pressed");
        }

        if (isPlacing)
        {
            UpdatePreviewPosition();

            if (vrPlace)
            {
                Debug.Log("PLACE TRIGGERED");
                PlaceObject();
            }

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
        MakePreviewTransparent(previewInstance);

        isPlacing = true;
        isEditingExistingObject = false;
        editSourceObject = null;

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

        Bounds previewBounds = GetObjectBounds(previewInstance);
        float previewHeight = Mathf.Max(previewBounds.size.y, 0.01f);

        Vector3 pos = rightControllerTransform.position + rightControllerTransform.forward * placeDistance;
        pos = SnapToGrid(pos);

        float supportTopY = FindSupportTopY(pos, previewBounds, out GameObject supportObject);

        if (supportObject != null)
        {
            pos.y = supportTopY + (previewHeight * 0.5f);

            if (enableDebugLogs)
                Debug.Log("[WallPlacer_VR] Preview supported by: " + supportObject.name);
        }
        else
        {
            pos.y = previewHeight * 0.5f;

            if (enableDebugLogs)
                Debug.Log("[WallPlacer_VR] Preview supported by: ground");
        }

        previewInstance.transform.position = pos;
        previewInstance.transform.rotation = Quaternion.identity;

        if (enableDebugLogs)
        {
            Debug.Log("[WallPlacer_VR] Controller = " + rightControllerTransform.name +
                      " | Controller Pos = " + rightControllerTransform.position +
                      " | Controller Forward = " + rightControllerTransform.forward +
                      " | Final Preview Pos = " + previewInstance.transform.position);
        }
    }

    float FindSupportTopY(Vector3 targetPos, Bounds previewBounds, out GameObject supportObject)
    {
        supportObject = null;
        float highestTop = float.MinValue;

        Vector3 halfExtents = new Vector3(
            Mathf.Max(previewBounds.extents.x * 0.4f, 0.05f),
            0.05f,
            Mathf.Max(previewBounds.extents.z * 0.4f, 0.05f)
        );

        Vector3 castOrigin = new Vector3(targetPos.x, maxDistance, targetPos.z);

        RaycastHit[] hits = Physics.BoxCastAll(
            castOrigin,
            halfExtents,
            Vector3.down,
            Quaternion.identity,
            maxDistance * 2f
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            GameObject target = hit.collider.transform.root.gameObject;

            if (target == null)
                continue;

            if (target == previewInstance || target == gameObject)
                continue;

            Bounds targetBounds = GetObjectBounds(target);
            float topY = targetBounds.max.y;

            if (topY > highestTop)
            {
                highestTop = topY;
                supportObject = target;
            }
        }

        return highestTop;
    }

    void PlaceObject()
    {
        if (previewInstance == null)
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

        MakePreviewTransparent(previewInstance);
        ApplySavedMaterials(previewInstance, true);

        Destroy(target);

        isPlacing = true;
        isEditingExistingObject = true;

        if (enableDebugLogs)
            Debug.Log("[WallPlacer_VR] Editing object: " + previewInstance.name);
    }

    void TryOpenMaterialWheelOnLookedObject()
    {
        if (materialWheelController == null)
        {
            Debug.LogError("[WallPlacer_VR] materialWheelController is NULL");
            return;
        }

        if (rightControllerTransform == null)
        {
            Debug.LogError("[WallPlacer_VR] rightControllerTransform is NULL");
            return;
        }

        Ray ray = new Ray(rightControllerTransform.position, rightControllerTransform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance) || hit.collider == null)
        {
            if (enableDebugLogs)
                Debug.Log("[WallPlacer_VR] No object hit for material wheel");
            return;
        }

        GameObject target = hit.collider.transform.root.gameObject;

        if (target == gameObject || target == previewInstance)
            return;

        Renderer rend = target.GetComponentInChildren<Renderer>();
        if (rend == null)
        {
            if (enableDebugLogs)
                Debug.Log("[WallPlacer_VR] Hit object has no renderer");
            return;
        }

        materialWheelController.SelectObject(rend);
        materialWheelController.OpenWheel(0);

        if (enableDebugLogs)
            Debug.Log("wheel opened");
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

        if (enableDebugLogs)
            Debug.Log("[WallPlacer_VR] Placement cancelled");
    }

    Bounds GetObjectBounds(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);

        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;

            for (int i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);

            return bounds;
        }

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        return new Bounds(obj.transform.position, Vector3.one);
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

    void MakePreviewTransparent(GameObject obj)
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
                c.a = PREVIEW_ALPHA;
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