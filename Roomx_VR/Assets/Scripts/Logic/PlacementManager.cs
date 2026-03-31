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

    public bool IsCarryingObject => isPlacing;

    void Awake() 
    { 
        if (Instance == null) Instance = this; 
    }

    public void StartPlacement(GameObject prefab)
    {
        if (isPlacing) CancelPlacement();
        if (prefab == null) return;

        ghostObject = Instantiate(prefab);
        PrepareGhost();
        canPlaceThisFrame = false; // Verhindert Sofort-Platzierung beim Menüklick
    }

    public void PickUpFurniture(GameObject furniture)
    {
        if (isPlacing || furniture == null) return;

        ghostObject = furniture;
        PrepareGhost();
        canPlaceThisFrame = true; 
    }

    private void PrepareGhost()
    {
        if (ghostObject == null) return;
        
        // Collider aus, damit der Raycast nicht am Möbelstück hängen bleibt
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
        
        // WICHTIG: Nur abbrechen, wenn NICHT gleichzeitig platziert wird
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

            // Check ob der Boden getroffen wurde
            isValid = ((1 << hit.collider.gameObject.layer) & groundLayer) != 0;
            ApplyMaterial(isValid ? validMaterial : invalidMaterial);

            if (triggerPress.action.WasPressedThisFrame() && canPlaceThisFrame)
            {
                if (isValid) FinalizePlacement();
                else Debug.Log("Ungültige Position - Kein Platzieren möglich.");
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

        foreach (var col in ghostObject.GetComponentsInChildren<Collider>())
            col.enabled = true;

        // Objekt auf den Furniture-Layer setzen
        int furnLayer = LayerMask.NameToLayer("Furniture");
        if (furnLayer != -1) SetLayerRecursively(ghostObject, furnLayer);

        ghostObject = null;
        isPlacing = false;
        Debug.Log("Objekt erfolgreich platziert!");
    }

    public void CancelPlacement()
    {
        if (ghostObject != null) 
        {
            Destroy(ghostObject);
            Debug.Log("Platzierung abgebrochen, Objekt gelöscht.");
        }
        ghostObject = null;
        isPlacing = false;
    }

    void ApplyMaterial(Material mat)
    {
        if (mat == null || ghostObject == null) return;
        MeshRenderer[] renderers = ghostObject.GetComponentsInChildren<MeshRenderer>();
        foreach (var rend in renderers)
        {
            if (rend != null) rend.material = mat;
        }
    }

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, newLayer);
    }
}