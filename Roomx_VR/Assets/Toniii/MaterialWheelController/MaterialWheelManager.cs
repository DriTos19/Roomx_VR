using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MaterialWheelManager : MonoBehaviour
{
    [Header("Object & Wheel")]
    public Renderer currentObject;
    public RectTransform wheelContainer;
    public RectTransform sliceContainer;
    public Button slicePrefab;

    [Header("Wheel Settings")]
    public int totalButtons = 8;
    public float radius = 8.5f;

    private bool stickInUse;

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



    void Start()
    {
        if (wheelContainer != null)
            wheelContainer.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isOpen)
            return;
    }

    public void UpdateJoystickHighlight(Vector2 stick)
    {
        if (!isOpen || materialVariants.Count == 0)
            return;

        if (stick.magnitude < 0.2f)
        {
            stickInUse = false;
            return;
        }

        if (stickInUse)
            return;

        float angle = Mathf.Atan2(stick.y, stick.x);
        if (angle < 0f) angle += Mathf.PI * 2f;

        int newIndex = Mathf.RoundToInt(angle / (Mathf.PI * 2f / materialVariants.Count)) % materialVariants.Count;

        if (newIndex != highlightedIndex)
        {
            highlightedIndex = newIndex;
            PreviewVariant(highlightedIndex);
        }

        stickInUse = true;
    }

    void PreviewVariant(int variantIndex)
    {
        if (currentObject == null) return;
        if (variantIndex < 0 || variantIndex >= materialVariants.Count) return;

        Material[] mats = currentObject.materials;
        if (currentSlot < 0 || currentSlot >= mats.Length) return;

        mats[currentSlot] = new Material(materialVariants[variantIndex]);
        currentObject.materials = mats;
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

    void BuildMaterialVariants()
    {
        materialVariants.Clear();

        if (wheelBaseMaterialSnapshot == null)
            return;

        Material sourceMat = new Material(wheelBaseMaterialSnapshot);

        // Use strongly distinct colors so any change is immediately visible regardless of base material
        List<Color> colors = new List<Color>
        {
            new Color(1f,   0.2f, 0.2f, 1f), // red
            new Color(0.2f, 1f,   0.2f, 1f), // green
            new Color(0.2f, 0.4f, 1f,   1f), // blue
            new Color(1f,   0.9f, 0.1f, 1f), // yellow
            new Color(0.2f, 0.9f, 0.9f, 1f), // cyan
            new Color(0.9f, 0.2f, 0.9f, 1f), // magenta
            new Color(1f,   1f,   1f,   1f), // white
            new Color(0.1f, 0.1f, 0.1f, 1f), // black
        };

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
        if (mat == null) return;
        color.a = 1f;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", color);

        // Fallback: Unity's built-in color setter works across Legacy, Standard and URP shaders
        mat.color = color;
    }

    public void ApplyHighlightedVariant()
    {
        ApplyVariant(highlightedIndex);
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