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

    [Header("Preview")]
    [Range(0.1f, 1f)]
    public float previewImageScale = 0.8f;

    private bool stickInUse;
    private bool isOpen = false;
    private int currentSlot = 0;
    private int highlightedIndex = -1; // nothing highlighted at open

    private Material[] materialsBeforeWheelOpen;
    private Material wheelBaseMaterialSnapshot;

    private readonly List<Material> materialVariants = new List<Material>();
    private readonly List<Button> spawnedSlices = new List<Button>();

    void Start()
    {
        if (wheelContainer != null)
            wheelContainer.gameObject.SetActive(false);
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
        if (angle < 0f)
            angle += Mathf.PI * 2f;

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

        Material[] currentMats = currentObject.materials;
        if (currentMats == null || currentMats.Length == 0)
            return;

        currentSlot = Mathf.Clamp(slotIndex, 0, currentMats.Length - 1);

// current state only for cancel/restore while wheel is open
        materialsBeforeWheelOpen = new Material[currentMats.Length];
        for (int i = 0; i < currentMats.Length; i++)
            materialsBeforeWheelOpen[i] = new Material(currentMats[i]);

// ORIGINAL state for building fixed button variants
        Material[] originalMats = GetOrCreateOriginalMaterials(currentObject);
        if (originalMats == null || currentSlot >= originalMats.Length)
            return;

        wheelBaseMaterialSnapshot = new Material(originalMats[currentSlot]);

        isOpen = true;
        highlightedIndex = -1; // IMPORTANT: do not auto-preview any color
        stickInUse = false;

        if (wheelContainer != null)
            wheelContainer.gameObject.SetActive(true);

        BuildMaterialVariants();
        CreateWheel();
    }

    public void CloseWheel()
    {
        isOpen = false;
        stickInUse = false;
        highlightedIndex = -1;

        if (wheelContainer != null)
            wheelContainer.gameObject.SetActive(false);

        ClearWheelUI();
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

        // Main baked material
        Material sourceMat = new Material(wheelBaseMaterialSnapshot);

        // -------- FIRST 4 BUTTONS: same baked material, different brightness --------
        float[] brightnessLevels = new float[]
        {
            0.55f, // dark
            0.75f, // darker / medium dark
            1.15f, // light
            1.35f  // lighter
        };

        for (int i = 0; i < brightnessLevels.Length; i++)
        {
            Material variant = new Material(sourceMat);
            ApplyBrightnessToMaterial(variant, brightnessLevels[i]);
            materialVariants.Add(variant);
        }

        // -------- OTHER 4 BUTTONS: soft color-tinted versions --------
        Color[] tintColors = new Color[]
        {
            new Color(1f,   0.4f, 0.4f, 1f), // light red
            new Color(0.4f, 0.6f, 1f, 1f),   // light blue
            new Color(0.4f, 1f,   0.4f, 1f), // light green
            new Color(1f,   0.9f, 0.4f, 1f), // light yellow
        };

        for (int i = 0; i < tintColors.Length; i++)
        {
            Material variant = new Material(sourceMat);
            SetMaterialTint(variant, tintColors[i]);
            materialVariants.Add(variant);
        }
    }
    
    void ApplyBrightnessToMaterial(Material mat, float multiplier)
    {
        if (mat == null)
            return;

        // Base color
        if (mat.HasProperty("_BaseColor"))
        {
            Color c = mat.GetColor("_BaseColor");
            c.r = Mathf.Clamp01(c.r * multiplier);
            c.g = Mathf.Clamp01(c.g * multiplier);
            c.b = Mathf.Clamp01(c.b * multiplier);
            c.a = 1f;
            mat.SetColor("_BaseColor", c);
        }

        // Built-in / Standard
        if (mat.HasProperty("_Color"))
        {
            Color c = mat.GetColor("_Color");
            c.r = Mathf.Clamp01(c.r * multiplier);
            c.g = Mathf.Clamp01(c.g * multiplier);
            c.b = Mathf.Clamp01(c.b * multiplier);
            c.a = 1f;
            mat.SetColor("_Color", c);
        }

        // Fallback
        Color fallback = mat.color;
        fallback.r = Mathf.Clamp01(fallback.r * multiplier);
        fallback.g = Mathf.Clamp01(fallback.g * multiplier);
        fallback.b = Mathf.Clamp01(fallback.b * multiplier);
        fallback.a = 1f;
        mat.color = fallback;
    }

    void SetMaterialTint(Material mat, Color tint)
    {
        if (mat == null)
            return;

        tint.a = 1f;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", tint);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", tint);

        mat.color = tint;
    }
    
    void SetMaterialColorPreserveTextureLook(Material mat, Color color)
    {
        if (mat == null)
            return;

        color.a = 1f;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        mat.color = color;

        // Optional: keep emission off unless you explicitly want glow
        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", Color.black);
    }
    Material[] GetOrCreateOriginalMaterials(Renderer rend)
    {
        if (rend == null)
            return null;

        MaterialWheelObjectState state = rend.GetComponent<MaterialWheelObjectState>();
        if (state == null)
            state = rend.gameObject.AddComponent<MaterialWheelObjectState>();

        if (state.originalMaterials == null || state.originalMaterials.Length == 0)
        {
            Material[] mats = rend.materials;
            state.originalMaterials = new Material[mats.Length];

            for (int i = 0; i < mats.Length; i++)
                state.originalMaterials[i] = new Material(mats[i]);
        }

        return state.originalMaterials;
    }
    void CreateWheel()
    {
        if (sliceContainer == null || slicePrefab == null || materialVariants.Count == 0)
        {
            Debug.LogWarning("Wheel cannot be created.");
            return;
        }

        ClearWheelUI();

        int n = materialVariants.Count;

        RectTransform prefabRT = slicePrefab.GetComponent<RectTransform>();
        float prefabWidth = prefabRT != null ? prefabRT.rect.width : 60f;
        float prefabHeight = prefabRT != null ? prefabRT.rect.height : 60f;
        float prefabButtonSize = Mathf.Max(prefabWidth, prefabHeight);

        for (int i = 0; i < n; i++)
        {
            float angle = i * Mathf.PI * 2f / n;
            Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            Button slice = Instantiate(slicePrefab, sliceContainer);
            RectTransform sliceRT = slice.GetComponent<RectTransform>();
            sliceRT.anchoredPosition = pos;

            Material variantMat = materialVariants[i];
            Texture previewTexture = GetMaterialTexture(variantMat);
            Color previewColor = GetButtonPreviewColor(i);
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

    Color GetButtonPreviewColor(int index)
    {
        switch (index)
        {
            // Keep the first 4 buttons looking like normal neutral previews
            case 0: return new Color(0.85f, 0.85f, 0.85f, 1f);
            case 1: return new Color(0.85f, 0.85f, 0.85f, 1f);
            case 2: return new Color(0.85f, 0.85f, 0.85f, 1f);
            case 3: return new Color(0.85f, 0.85f, 0.85f, 1f);

            // Keep the other 4 as light tinted colors
            case 4: return new Color(1f,   0.4f, 0.4f, 1f); // light red
            case 5: return new Color(0.4f, 0.6f, 1f, 1f);   // light blue
            case 6: return new Color(0.4f, 1f,   0.4f, 1f); // light green
            case 7: return new Color(1f,   0.9f, 0.4f, 1f); // light yellow
        }

        return Color.white;
    }
    
    void ClearWheelUI()
    {
        foreach (Button slice in spawnedSlices)
        {
            if (slice != null)
                Destroy(slice.gameObject);
        }

        spawnedSlices.Clear();
    }

    Color GetMaterialColor(Material mat)
    {
        if (mat == null) return Color.white;
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
        return Color.white;
    }

    Texture GetMaterialTexture(Material mat)
    {
        if (mat == null) return null;
        if (mat.HasProperty("_BaseMap")) return mat.GetTexture("_BaseMap");
        if (mat.HasProperty("_MainTex")) return mat.GetTexture("_MainTex");
        return null;
    }

    void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;

        color.a = 1f;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        mat.color = color;
    }

    public void ApplyHighlightedVariant()
    {
        if (highlightedIndex < 0 || highlightedIndex >= materialVariants.Count)
            return; // IMPORTANT: do nothing if user has not highlighted anything yet

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
    
    public void OpenWheelAuto()
    {
        if (currentObject == null)
            return;

        Material[] mats = currentObject.materials;

        if (mats == null || mats.Length == 0)
            return;

        int bestIndex = 0;

        for (int i = 0; i < mats.Length; i++)
        {
            if (HasBakedTexture(mats[i]))
            {
                bestIndex = i;
                break;
            }
        }

        OpenWheel(bestIndex);
    }
    
    bool HasBakedTexture(Material mat)
    {
        if (mat == null)
            return false;

        // URP / HDRP
        if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null)
            return true;

        // Built-in
        if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null)
            return true;

        return false;
    }
}