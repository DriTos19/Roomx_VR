using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using System.Collections.Generic;

public class MaterialWheelController : MonoBehaviour
{
    [Header("Object & Wheel")]
    public Renderer currentObject;
    public RectTransform wheelContainer;
    public RectTransform sliceContainer;
    public Button slicePrefab;

    [Header("Wheel Settings")]
    public int totalButtons = 8;
    public float radius = 120f;

    [Header("XR Input")]
    public XRNode controllerNode = XRNode.RightHand;
  //  public float stickDeadzone = 0.6f;

    [Header("Preview")]
    [Range(0.1f, 1f)]
    public float previewImageScale = 0.8f;

    private bool isOpen = false;
    private int currentSlot = 0;
    private int highlightedIndex = 0;

    private Material selectedBaseMaterial;

    private Dictionary<Renderer, Material[]> originalMaterialsByRenderer = new Dictionary<Renderer, Material[]>();
    private List<Material> materialVariants = new List<Material>();
    private List<Button> spawnedSlices = new List<Button>();

    private InputDevice controllerDevice;

    private bool lastTriggerPressed;
    private bool lastSecondaryPressed;
    private bool stickInUse;

    void Start()
    {
        if (wheelContainer != null)
            wheelContainer.gameObject.SetActive(false);

        InitializeXRDevice();
    }

    void Update()
    {
        if (!controllerDevice.isValid)
            InitializeXRDevice();

        if (!isOpen)
            return;

        HandleStickSelection();
        HandleConfirmCancelInput();
    }

    void InitializeXRDevice()
    {
        controllerDevice = InputDevices.GetDeviceAtXRNode(controllerNode);
    }

    public void SelectObject(Renderer newRenderer)
    {
        if (newRenderer == null)
            return;

        currentObject = newRenderer;

        if (!originalMaterialsByRenderer.ContainsKey(currentObject))
        {
            Material[] source = currentObject.materials;
            Material[] copy = new Material[source.Length];

            for (int i = 0; i < source.Length; i++)
                copy[i] = new Material(source[i]);

            originalMaterialsByRenderer[currentObject] = copy;
        }

        Material[] originals = originalMaterialsByRenderer[currentObject];
        selectedBaseMaterial = originals.Length > 0 ? originals[0] : null;
    }

    public void OpenWheel(int slotIndex = 0)
    {
        if (currentObject == null)
            return;

        currentSlot = slotIndex;

        if (originalMaterialsByRenderer.TryGetValue(currentObject, out Material[] originals))
        {
            if (currentSlot >= 0 && currentSlot < originals.Length)
                selectedBaseMaterial = originals[currentSlot];
        }

        if (selectedBaseMaterial == null)
            return;

        isOpen = true;
        highlightedIndex = 0;
        stickInUse = false;

        Time.timeScale = 0f;

        if (wheelContainer != null)
            wheelContainer.gameObject.SetActive(true);

        BuildMaterialVariants();
        CreateWheel();
    }

    public void CloseWheel()
    {
        isOpen = false;

        Time.timeScale = 1f;

        if (wheelContainer != null)
            wheelContainer.gameObject.SetActive(false);
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    void HandleStickSelection()
    {
        if (!controllerDevice.isValid || materialVariants.Count == 0)
            return;

        Vector2 stick;
        controllerDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out stick);

     /*   if (stick.magnitude < stickDeadzone)
        {
            stickInUse = false;
            return;
        }
*/
     // No deadzone — always read stick
        if (stick.magnitude < 0.01f) // tiny fallback to avoid NaN
        {
            stickInUse = false;
            return;
        }
        
        if (stickInUse)
            return;

        float angle = Mathf.Atan2(stick.y, stick.x);
        if (angle < 0f)
            angle += Mathf.PI * 2f;

        float sliceSize = Mathf.PI * 2f / materialVariants.Count;
        int newIndex = Mathf.RoundToInt(angle / sliceSize) % materialVariants.Count;

        highlightedIndex = newIndex;
        stickInUse = true;
    }

    void HandleConfirmCancelInput()
    {
        bool triggerPressed = false;
        bool secondaryPressed = false;

        if (controllerDevice.isValid)
        {
            controllerDevice.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed);
            controllerDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryPressed);
        }

        bool triggerDown = triggerPressed && !lastTriggerPressed;
        bool secondaryDown = secondaryPressed && !lastSecondaryPressed;

        lastTriggerPressed = triggerPressed;
        lastSecondaryPressed = secondaryPressed;

        if (triggerDown)
            ApplyVariant(highlightedIndex);

        if (secondaryDown)
            CloseWheel();
    }

    void BuildMaterialVariants()
    {
        materialVariants.Clear();

        if (selectedBaseMaterial == null)
            return;

        Color baseColor = GetMaterialColor(selectedBaseMaterial);
        List<Color> colors = GenerateFixedVariantColors(baseColor);

        int count = Mathf.Min(totalButtons, colors.Count);

        for (int i = 0; i < count; i++)
        {
            Material variant = new Material(selectedBaseMaterial);
            SetMaterialColor(variant, colors[i]);
            materialVariants.Add(variant);
        }
    }

    void CreateWheel()
    {
        if (sliceContainer == null || slicePrefab == null || materialVariants.Count == 0)
        {
            Debug.LogWarning("Wheel cannot be created.");
            return;
        }

        foreach (Transform child in sliceContainer)
            Destroy(child.gameObject);

        spawnedSlices.Clear();

        int n = materialVariants.Count;

        RectTransform prefabRT = slicePrefab.GetComponent<RectTransform>();
        float prefabWidth = prefabRT != null ? prefabRT.rect.width : 60f;
        float prefabHeight = prefabRT != null ? prefabRT.rect.height : 60f;
        float prefabButtonSize = Mathf.Max(prefabWidth, prefabHeight);

        float containerRadius = Mathf.Min(sliceContainer.rect.width, sliceContainer.rect.height) * 0.5f;
        float safeRadius = containerRadius - (prefabButtonSize * 0.5f) - 10f;
        float finalRadius = radius;
        
        for (int i = 0; i < n; i++)
        {
            float angle = i * Mathf.PI * 2f / n;
            Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * finalRadius;

            Button slice = Instantiate(slicePrefab, sliceContainer);

            RectTransform sliceRT = slice.GetComponent<RectTransform>();
            sliceRT.anchoredPosition = pos;

            Material variantMat = materialVariants[i];
            Texture previewTexture = GetMaterialTexture(variantMat);

            Color previewColor = GetMaterialColor(variantMat);
            previewColor.a = 1f;

            RawImage preview = slice.transform.Find("PreviewImage")?.GetComponent<RawImage>();
            if (preview != null)
            {
                RectTransform rt = preview.rectTransform;

                float previewSize = prefabButtonSize * previewImageScale;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(previewSize, previewSize);
                rt.anchoredPosition = Vector2.zero;

                if (previewTexture != null)
                    preview.texture = previewTexture;

                preview.color = previewColor;
                preview.raycastTarget = false;
            }

            int capturedIndex = i;
            slice.onClick.RemoveAllListeners();
            slice.onClick.AddListener(() => ApplyVariant(capturedIndex));

            spawnedSlices.Add(slice);
        }
    }

    List<Color> GenerateFixedVariantColors(Color baseColor)
    {
        List<Color> variants = new List<Color>();

        Color rgb1 = new Color(
            Mathf.Clamp01(baseColor.r * 1.35f),
            Mathf.Clamp01(baseColor.g * 0.75f),
            Mathf.Clamp01(baseColor.b * 0.75f),
            1f
        );

        Color rgb2 = new Color(
            Mathf.Clamp01(baseColor.r * 0.75f),
            Mathf.Clamp01(baseColor.g * 1.35f),
            Mathf.Clamp01(baseColor.b * 0.75f),
            1f
        );

        Color rgb3 = new Color(
            Mathf.Clamp01(baseColor.r * 0.75f),
            Mathf.Clamp01(baseColor.g * 0.75f),
            Mathf.Clamp01(baseColor.b * 1.35f),
            1f
        );

        Color rgb4 = new Color(
            Mathf.Clamp01(baseColor.r * 1.25f),
            Mathf.Clamp01(baseColor.g * 0.70f),
            Mathf.Clamp01(baseColor.b * 1.25f),
            1f
        );

        Color bright1 = baseColor * 1.0f;
        Color bright2 = baseColor * 1.45f;
        Color bright3 = baseColor * 0.65f;
        Color bright4 = baseColor * 0.40f;

        bright1.a = 1f;
        bright2.a = 1f;
        bright3.a = 1f;
        bright4.a = 1f;

        variants.Add(rgb1);
        variants.Add(rgb2);
        variants.Add(rgb3);
        variants.Add(rgb4);

        variants.Add(bright1);
        variants.Add(bright2);
        variants.Add(bright3);
        variants.Add(bright4);

        return variants;
    }

    Color GetMaterialColor(Material mat)
    {
        if (mat == null)
            return Color.white;

        if (mat.HasProperty("_BaseColor"))
            return mat.GetColor("_BaseColor");

        if (mat.HasProperty("_Color"))
            return mat.GetColor("_Color");

        return Color.white;
    }

    Texture GetMaterialTexture(Material mat)
    {
        if (mat == null)
            return null;

        if (mat.HasProperty("_BaseMap"))
            return mat.GetTexture("_BaseMap");

        if (mat.HasProperty("_MainTex"))
            return mat.GetTexture("_MainTex");

        return null;
    }

    void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null)
            return;

        color.a = 1f;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

    void ApplyVariant(int variantIndex)
    {
        if (currentObject == null)
            return;

        if (variantIndex < 0 || variantIndex >= materialVariants.Count)
            return;

        Material[] mats = currentObject.materials;

        if (currentSlot < 0 || currentSlot >= mats.Length)
            return;

        mats[currentSlot] = new Material(materialVariants[variantIndex]);
        currentObject.materials = mats;

        CloseWheel();
    }
}