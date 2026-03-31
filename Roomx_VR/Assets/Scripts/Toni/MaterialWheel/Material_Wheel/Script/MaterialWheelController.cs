using UnityEngine;
using UnityEngine.UI;
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

    private bool isOpen = false;
    private int currentSlot = 0;

    private Material selectedBaseMaterial;

    private Dictionary<Renderer, Material[]> originalMaterialsByRenderer = new Dictionary<Renderer, Material[]>();
    private List<Material> materialVariants = new List<Material>();

    void Start()
    {
        if (wheelContainer != null)
            wheelContainer.gameObject.SetActive(false);
    }

    // =========================
    // SELECT OBJECT
    // =========================
    public void SelectObject(Renderer newRenderer)
    {
        if (newRenderer == null) return;

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

    // =========================
    // OPEN WHEEL
    // =========================
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

        if (wheelContainer != null)
            wheelContainer.gameObject.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        BuildMaterialVariants();
        CreateWheel();
    }

    // =========================
    // CLOSE WHEEL
    // =========================
    public void CloseWheel()
    {
        isOpen = false;

        if (wheelContainer != null)
            wheelContainer.gameObject.SetActive(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    // =========================
    // BUILD VARIANTS
    // =========================
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

    // =========================
    // CREATE UI WHEEL
    // =========================
    void CreateWheel()
    {
        if (sliceContainer == null || slicePrefab == null || materialVariants.Count == 0)
        {
            Debug.LogWarning("Wheel cannot be created.");
            return;
        }

        foreach (Transform child in sliceContainer)
            Destroy(child.gameObject);

        int n = materialVariants.Count;

        // Auto-fit radius
        float containerRadius = Mathf.Min(sliceContainer.rect.width, sliceContainer.rect.height) * 0.5f;
        float buttonSize = slicePrefab.GetComponent<RectTransform>().rect.width;
        float safeRadius = containerRadius - (buttonSize * 0.5f) - 10f;
        float finalRadius = Mathf.Min(radius, safeRadius);

        for (int i = 0; i < n; i++)
        {
            float angle = i * Mathf.PI * 2f / n;
            Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * finalRadius;

            Button slice = Instantiate(slicePrefab, sliceContainer);
            slice.GetComponent<RectTransform>().anchoredPosition = pos;

            Material variantMat = materialVariants[i];
            Texture previewTexture = GetMaterialTexture(variantMat);

            Color previewColor = GetMaterialColor(variantMat);
            previewColor.a = 1f;

            RawImage preview = slice.transform.Find("PreviewImage")?.GetComponent<RawImage>();
            if (preview != null)
            {
                RectTransform rt = preview.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                if (previewTexture != null)
                    preview.texture = previewTexture;

                preview.color = previewColor;
                preview.raycastTarget = false;
            }

            int capturedIndex = i;
            slice.onClick.AddListener(() => ApplyVariant(capturedIndex));
        }
    }

    // =========================
    // COLOR VARIANTS (IMPROVED)
    // =========================
    List<Color> GenerateFixedVariantColors(Color baseColor)
    {
        List<Color> variants = new List<Color>();

        Color.RGBToHSV(baseColor, out float h, out float s, out float v);

        Color c0 = Color.HSVToRGB(h, s * 1.00f, v * 1.00f); // original
        Color c1 = Color.HSVToRGB(h, s * 0.95f, v * 1.12f); // brighter
        Color c2 = Color.HSVToRGB(h, s * 0.80f, v * 1.22f); // brighter + softer
        Color c3 = Color.HSVToRGB(h, s * 0.60f, v * 1.30f); // pastel light
        Color c4 = Color.HSVToRGB(h, s * 1.05f, v * 0.90f); // slightly darker
        Color c5 = Color.HSVToRGB(h, s * 1.10f, v * 0.75f); // darker
        Color c6 = Color.HSVToRGB(h, s * 0.85f, v * 0.60f); // muted dark
        Color c7 = Color.HSVToRGB(h, s * 1.15f, v * 1.08f); // vivid bright

        c0.a = c1.a = c2.a = c3.a = c4.a = c5.a = c6.a = c7.a = 1f;

        variants.Add(c0);
        variants.Add(c1);
        variants.Add(c2);
        variants.Add(c3);
        variants.Add(c4);
        variants.Add(c5);
        variants.Add(c6);
        variants.Add(c7);

        return variants;
    }

    // =========================
    // MATERIAL HELPERS
    // =========================
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

    // =========================
    // APPLY MATERIAL
    // =========================
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