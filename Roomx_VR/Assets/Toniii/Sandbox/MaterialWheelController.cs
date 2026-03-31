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
    public float radius = 8.5f;

    [Header("XR Input")]
    public XRNode controllerNode = XRNode.RightHand;

    [Header("Preview")]
    [Range(0.1f, 1f)]
    public float previewImageScale = 0.8f;

    private bool isOpen = false;
    private int currentSlot = 0;
    private int highlightedIndex = 0;

    private Material selectedBaseMaterial;
    private Material[] materialsBeforeWheelOpen;
    private Material wheelBaseMaterialSnapshot;

    private readonly List<Material> materialVariants = new List<Material>();
    private readonly List<Button> spawnedSlices = new List<Button>();

    private InputDevice controllerDevice;
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
    }

    public Renderer GetCurrentRenderer()
    {
        return currentObject;
    }

    public void OpenWheel(int slotIndex = 0)
    {
        if (currentObject == null)
            return;

        currentSlot = slotIndex;

        Material[] currentMats = currentObject.materials;

        if (currentMats == null || currentMats.Length == 0)
            return;

        if (currentSlot < 0 || currentSlot >= currentMats.Length)
            return;

        materialsBeforeWheelOpen = new Material[currentMats.Length];
        for (int i = 0; i < currentMats.Length; i++)
            materialsBeforeWheelOpen[i] = new Material(currentMats[i]);

        wheelBaseMaterialSnapshot = new Material(currentMats[currentSlot]);
        selectedBaseMaterial = new Material(currentMats[currentSlot]);

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

        wheelBaseMaterialSnapshot = null;
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    public void RestoreOriginalMaterials()
    {
        if (currentObject == null || materialsBeforeWheelOpen == null)
            return;

        Material[] mats = new Material[materialsBeforeWheelOpen.Length];

        for (int i = 0; i < materialsBeforeWheelOpen.Length; i++)
            mats[i] = new Material(materialsBeforeWheelOpen[i]);

        currentObject.materials = mats;
    }

    void HandleStickSelection()
    {
        if (!controllerDevice.isValid || materialVariants.Count == 0)
            return;

        Vector2 stick;
        controllerDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out stick);

        if (stick.magnitude < 0.2f)
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

    void BuildMaterialVariants()
    {
        materialVariants.Clear();

        if (wheelBaseMaterialSnapshot == null)
            return;

        Material sourceMat = new Material(wheelBaseMaterialSnapshot);
        Color baseColor = GetMaterialColor(sourceMat);

        List<Color> colors = new List<Color>();

        Color lighter1 = MultiplyColor(baseColor, 1.15f);
        Color lighter2 = MultiplyColor(baseColor, 1.35f);
        Color darker1 = MultiplyColor(baseColor, 0.85f);
        Color darker2 = MultiplyColor(baseColor, 0.65f);

        Color reddish = new Color(
            Mathf.Clamp01(baseColor.r * 1.20f),
            Mathf.Clamp01(baseColor.g * 0.90f),
            Mathf.Clamp01(baseColor.b * 0.90f),
            1f
        );

        Color greenish = new Color(
            Mathf.Clamp01(baseColor.r * 0.90f),
            Mathf.Clamp01(baseColor.g * 1.20f),
            Mathf.Clamp01(baseColor.b * 0.90f),
            1f
        );

        Color bluish = new Color(
            Mathf.Clamp01(baseColor.r * 0.90f),
            Mathf.Clamp01(baseColor.g * 0.90f),
            Mathf.Clamp01(baseColor.b * 1.20f),
            1f
        );

        Color magentaish = new Color(
            Mathf.Clamp01(baseColor.r * 1.15f),
            Mathf.Clamp01(baseColor.g * 0.85f),
            Mathf.Clamp01(baseColor.b * 1.15f),
            1f
        );

        lighter1.a = 1f;
        lighter2.a = 1f;
        darker1.a = 1f;
        darker2.a = 1f;

        colors.Add(lighter1);
        colors.Add(lighter2);
        colors.Add(darker1);
        colors.Add(darker2);
        colors.Add(reddish);
        colors.Add(greenish);
        colors.Add(bluish);
        colors.Add(magentaish);

        int count = Mathf.Min(totalButtons, colors.Count);

        for (int i = 0; i < count; i++)
        {
            Material variant = new Material(sourceMat);
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

        float finalRadius = radius;

        for (int i = 0; i < n; i++)
        {
            float angle = i * Mathf.PI * 2f / n;
            Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * finalRadius;

            Button slice = Instantiate(slicePrefab, sliceContainer);
            RectTransform sliceRT = slice.GetComponent<RectTransform>();
            sliceRT.anchoredPosition = pos;

            ColorBlock cb = slice.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.pressedColor = Color.white;
            cb.selectedColor = Color.white;
            cb.disabledColor = Color.white;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0f;
            slice.colors = cb;
            slice.transition = Selectable.Transition.None;

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

                preview.texture = previewTexture;
                preview.color = previewColor;
                preview.raycastTarget = false;
            }

            int capturedIndex = i;
            slice.onClick.RemoveAllListeners();
            slice.onClick.AddListener(() =>
            {
                highlightedIndex = capturedIndex;
                ApplyVariant(capturedIndex);
            });

            spawnedSlices.Add(slice);
        }
    }

    Color MultiplyColor(Color source, float multiplier)
    {
        return new Color(
            Mathf.Clamp01(source.r * multiplier),
            Mathf.Clamp01(source.g * multiplier),
            Mathf.Clamp01(source.b * multiplier),
            1f
        );
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