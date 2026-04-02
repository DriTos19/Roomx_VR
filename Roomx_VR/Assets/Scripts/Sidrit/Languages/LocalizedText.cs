using UnityEngine;
using TMPro;

public class LocalizedText : MonoBehaviour
{
    public LocalizationData localizationData;
    public string key;

    private TMP_Text _text;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        LocalizationManager.Instance.onLanguageChanged.AddListener(UpdateText);
        UpdateText();
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.onLanguageChanged.RemoveListener(UpdateText);
    }

    private void UpdateText()
    {
        _text.text = localizationData.GetText(key, LocalizationManager.Instance.CurrentLanguage);
    }
}