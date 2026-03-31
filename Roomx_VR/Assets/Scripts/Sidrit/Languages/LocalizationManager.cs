using UnityEngine;
using UnityEngine.Events;

namespace Sidrit.Languages
{
    public enum Language { English, Albanian, German }

    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        public UnityEvent onLanguageChanged = new UnityEvent();

        private const string SAVE_KEY = "Language";

        public Language CurrentLanguage { get; private set; } = Language.English;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        public void SetLanguage(Language language)
        {
            CurrentLanguage = language;
            PlayerPrefs.SetInt(SAVE_KEY, (int)language);
            PlayerPrefs.Save();
            onLanguageChanged.Invoke();
        }

        private void Load()
        {
            if (PlayerPrefs.HasKey(SAVE_KEY))
                CurrentLanguage = (Language)PlayerPrefs.GetInt(SAVE_KEY);
        }
    }
}