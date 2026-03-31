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

    public bool IsCarryingObject => isPlacing;

    void Awake() { Instance = this; }

    public void StartPlacement(GameObject prefab)
    {
        if (isPlacing) CancelPlacement();
        if (prefab == null) return;

        ghostObject = Instantiate(prefab);
        PrepareGhost();
    }

    private void PrepareGhost()
    {
        if (ghostObject == null) return;
        
        // Collider deaktivieren, damit der Raycast den Boden trifft
        foreach (var col in ghostObject.GetComponentsInChildren<Collider>())
            col.enabled = false;

        isPlacing = true;
        currentRotation = ghostObject.transform.eulerAngles.y;
    }

    void Update()
    {
        // Wenn nichts platziert wird oder das Objekt fehlt, Update stoppen
        if (!isPlacing || ghostObject == null) return;
        if (InventoryManager.IsMenuOpen()) return;

        HandleRotation();
        
        if (cancelAction.action.WasPressedThisFrame()) 
            CancelPlacement();

        HandlePositioning();
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
            // SICHERHEIT: Falls ghostObject währenddessen zerstört wurde
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

            // Layer Check: Trifft der Strahl den Boden?
            bool isValid = hit.collider != null && ((1 << hit.collider.gameObject.layer) & groundLayer) != 0;

            // FIX FÜR NULL-REFERENCE (Alt: Zeile 94)
            ApplyMaterial(isValid ? validMaterial : invalidMaterial);

            // PLATZIEREN: Nur wenn grün (isValid)
            if (triggerPress.action.WasPressedThisFrame() && isValid)
            {
                Debug.Log("Objekt erfolgreich platziert!");
                FinalizePlacement();
            }
        }
        else
        {
            if (ghostObject != null) ghostObject.SetActive(false);
        }
    }

    void FinalizePlacement()
    {
        if (ghostObject == null) return;

        Debug.Log("Finalisiere Platzierung für: " + ghostObject.name);

        // 1. Alle Collider wieder einschalten, damit man es später wieder aufheben kann
        foreach (var col in ghostObject.GetComponentsInChildren<Collider>())
        {
            col.enabled = true;
        }

        // 2. Den Shader/Material wieder auf Standard setzen (Wichtig!)
        // Hier entfernen wir die rote/grüne Markierung
        MeshRenderer[] renderers = ghostObject.GetComponentsInChildren<MeshRenderer>();
        foreach (var rend in renderers)
        {
            // Wir setzen das Material zurück, das das Objekt ursprünglich hatte
            // Oder wir weisen hier ein neutrales Standard-Material zu
        }

        // 3. Das Objekt auf einen Layer setzen, der NICHT der Ground-Layer ist
        int furnLayer = LayerMask.NameToLayer("Furniture");
        if (furnLayer != -1) 
        {
            SetLayerRecursively(ghostObject, furnLayer);
        }

        // 4. DIE REFERENZ LÖSEN (DAS IST DER ENTSCHEIDENDE PUNKT)
        // Wir setzen nur die Variable im Script auf null, damit Update() aufhört es zu bewegen.
        // Das Objekt selbst bleibt in der Hierarchy bestehen!
        ghostObject = null;
        isPlacing = false;
    
        Debug.Log("Objekt erfolgreich in der Szene verankert.");
    }

    public void CancelPlacement()
    {
        if (ghostObject != null) Destroy(ghostObject);
        ghostObject = null;
        isPlacing = false;
    }

    void ApplyMaterial(Material mat)
    {
        // SICHERHEIT: Wenn kein Material im Inspector zugewiesen wurde
        if (mat == null || ghostObject == null) return;

        MeshRenderer[] renderers = ghostObject.GetComponentsInChildren<MeshRenderer>();
        foreach (var rend in renderers)
        {
            if (rend != null) // Prüfen, ob der Renderer wirklich existiert
            {
                rend.material = mat;
            }
        }
    }

    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, newLayer);
    }
    public void PickUpFurniture(GameObject furniture)
    {
        if (isPlacing || furniture == null) return;

        // Das Möbelstück wird zum neuen "Ghost"
        ghostObject = furniture;
    
        // Collider ausschalten, damit der Raycast zum Boden durchgeht
        foreach (var col in ghostObject.GetComponentsInChildren<Collider>())
            col.enabled = false;

        isPlacing = true;
        currentRotation = ghostObject.transform.eulerAngles.y;
    
        Debug.Log("Möbelstück zum Verschieben aufgehoben: " + furniture.name);
    }
}