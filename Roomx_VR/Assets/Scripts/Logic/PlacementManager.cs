using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance; // Verhindert Fehler aus image_a5fd87.png

    [Header("Input (Rechte Hand)")]
    public XRRayInteractor rayInteractor;
    public InputActionProperty triggerPress; // XRI RightHand Interaction/Activate Value
    public InputActionProperty rotateAction;  
    public InputActionProperty cancelAction;  

    [Header("Layer & Materialien")]
    public LayerMask groundLayer; // Muss im Inspector auf "Ground" stehen
    public Material validMaterial;   
    public Material invalidMaterial; 

    [Header("Settings")]
    public float gridSize = 0.5f;
    public bool enableSnapping = true;

    private GameObject ghostObject;
    private bool isPlacing = false;
    private float currentRotation = 0f;

    public bool IsCarryingObject => isPlacing;

    void Awake() { Instance = this; }

    public void StartPlacement(GameObject prefab)
    {
        if (ghostObject != null) Destroy(ghostObject);
        ghostObject = Instantiate(prefab);
        PrepareGhost();
    }

    public void PickUpFurniture(GameObject furniture)
    {
        if (isPlacing) return;
        ghostObject = furniture;
        PrepareGhost();
    }

    private void PrepareGhost()
    {
        // WICHTIG: Schaltet alle Collider am Möbel aus, damit der Raycast den Boden trifft
        foreach (var col in ghostObject.GetComponentsInChildren<Collider>()) col.enabled = false;
        isPlacing = true;
    }

    void Update()
    {
        if (!isPlacing || ghostObject == null) return;

        // 1. DREHEN
        float rotateInput = rotateAction.action.ReadValue<Vector2>().x;
        currentRotation += rotateInput * 120f * Time.deltaTime;

        // 2. ABBRECHEN (Rechter Grip)
        if (cancelAction.action.WasPressedThisFrame())
        {
            Destroy(ghostObject);
            isPlacing = false;
            return;
        }

        // 3. POSITIONIEREN & DROP-CHECK
        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            ghostObject.SetActive(true);
            Vector3 targetPos = hit.point;

            if (enableSnapping)
            {
                targetPos.x = Mathf.Round(targetPos.x / gridSize) * gridSize;
                targetPos.z = Mathf.Round(targetPos.z / gridSize) * gridSize;
            }

            ghostObject.transform.position = targetPos;
            ghostObject.transform.rotation = Quaternion.Euler(0, currentRotation, 0);

            // Prüft, ob der getroffene Layer "Ground" ist
            bool isValid = ((1 << hit.collider.gameObject.layer) & groundLayer) != 0;
            
            // Visuelles Feedback
            ApplyMaterial(isValid ? validMaterial : invalidMaterial);

            // 4. PLATZIEREN (Der Drop Part)
            if (triggerPress.action.WasPressedThisFrame() && isValid)
            {
                FinalizePlacement();
            }
        }
        else
        {
            ApplyMaterial(invalidMaterial);
        }
    }

    void FinalizePlacement()
    {
        // Collider wieder einschalten, damit man es wieder aufheben kann
        foreach (var col in ghostObject.GetComponentsInChildren<Collider>()) col.enabled = true;
        ghostObject = null;
        isPlacing = false;
    }

    void ApplyMaterial(Material mat)
    {
        foreach (var rend in ghostObject.GetComponentsInChildren<MeshRenderer>()) rend.material = mat;
    }
}