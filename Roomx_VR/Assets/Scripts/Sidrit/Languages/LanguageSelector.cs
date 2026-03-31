using UnityEngine;

namespace Sidrit.Languages
{
    public class LanguageSelector : MonoBehaviour
    {
        public void SetEnglish()  => LocalizationManager.Instance.SetLanguage(Language.English);
        public void SetAlbanian() => LocalizationManager.Instance.SetLanguage(Language.Albanian);
        public void SetGerman()   => LocalizationManager.Instance.SetLanguage(Language.German);
    }
}