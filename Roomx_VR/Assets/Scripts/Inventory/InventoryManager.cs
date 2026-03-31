using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("VR Setup")]
    public Transform xrCamera;
    public CanvasGroup canvasGroup;
    public InputActionProperty toggleButton; // XRI LeftHand Interaction/UI Press

    [Header("Menu Settings")]
    public float distanceFromPlayer = 1.2f;
    public float menuHeight = 0f; // height offset if needed

    [Header("Slots")]
    public Transform slotParent;
    public GameObject slotPrefab;
    public List<InventoryItemData> items = new List<InventoryItemData>();

    private bool isOpen = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        SetMenuState(false);
        PopulateSlots();
    }

    void Update()
    {
        if (toggleButton.action.WasPressedThisFrame())
        {
            if (PlacementManager.Instance.IsCarryingObject) return;

            isOpen = !isOpen;
            SetMenuState(isOpen);
        }

        // Always snap in front of player while open, no lerp
        if (isOpen)
        {
            SnapToPlayer();
        }
    }

    void SnapToPlayer()
    {
        if (xrCamera == null) return;

        Vector3 forward = xrCamera.forward;
        forward.y = 0;
        forward.Normalize();

        // If looking straight up/down fallback to transform.forward
        if (forward.magnitude < 0.1f)
            forward = new Vector3(xrCamera.forward.x, 0, xrCamera.forward.z).normalized;

        transform.position = xrCamera.position 
                             + forward * distanceFromPlayer 
                             + Vector3.up * menuHeight;

        transform.rotation = Quaternion.LookRotation(forward);
        transform.Rotate(0, 180, 0);
    }

    public static bool IsMenuOpen()
    {
        return Instance != null && Instance.isOpen;
    }

    public void PopulateSlots()
    {
        foreach (Transform child in slotParent)
            Destroy(child.gameObject);

        foreach (var item in items)
        {
            GameObject slot = Instantiate(slotPrefab, slotParent);
            slot.GetComponent<ItemSlotUI>().Setup(item, this);
        }
    }

    public void SelectItem(InventoryItemData data)
    {
        isOpen = false;
        SetMenuState(false);
        PlacementManager.Instance.StartPlacement(data.prefab3D);
    }

    private void SetMenuState(bool state)
    {
        canvasGroup.alpha = state ? 1 : 0;
        canvasGroup.interactable = state;
        canvasGroup.blocksRaycasts = state;
    }

    // Snaps menu in front of player when first opened
    void PositionMenu()
    {
        if (xrCamera == null) return;

        Vector3 forward = xrCamera.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 targetPos = xrCamera.position
            + forward * distanceFromPlayer
            + Vector3.up * menuHeight;

        transform.position = targetPos;
        transform.LookAt(new Vector3(
            xrCamera.position.x,
            transform.position.y,
            xrCamera.position.z));
        transform.Rotate(0, 180, 0);
    }

    // Smoothly follows player while open
    void FollowPlayer()
    {
        if (xrCamera == null) return;

        Vector3 forward = xrCamera.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 targetPos = xrCamera.position
            + forward * distanceFromPlayer
            + Vector3.up * menuHeight;

        // Smooth follow
        transform.position = Vector3.Lerp(
            transform.position,
            targetPos,
            Time.deltaTime * 5f);

        // Always face player
        transform.LookAt(new Vector3(
            xrCamera.position.x,
            transform.position.y,
            xrCamera.position.z));
        transform.Rotate(0, 180, 0);
    }
}