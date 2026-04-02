using UnityEngine;

[System.Serializable]
public class TranslationEntry
{
    public string key;
    public string english;
    public string albanian;
    public string german;
}

[CreateAssetMenu(fileName = "LocalizationData", menuName = "Localization/Data")]
public class LocalizationData : ScriptableObject
{
    public TranslationEntry[] entries;

    public string GetText(string key, Language language)
    {
        foreach (var entry in entries)
        {
            if (entry.key == key)
            {
                return language switch
                {
                    Language.Albanian => entry.albanian,
                    Language.German   => entry.german,
                    _                 => entry.english,
                };
            }
        }
        Debug.LogWarning($"[Localization] Key '{key}' not found.");
        return key;
    }
}