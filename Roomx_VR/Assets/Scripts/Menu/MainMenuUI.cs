using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels (with CanvasGroup)")]
    public CanvasGroup mainMenuPanel;
    public CanvasGroup settingsPanel;
    public CanvasGroup helpPanel;
    public CanvasGroup loadingScreen;

    [Header("Loading UI")]
    public Slider progressBar;
    public TMP_Text loadingText;

    [Header("Audio")]
    public AudioSource backgroundMusic;
    public Image muteIcon;
    public Sprite muteSprite;
    public Sprite unmuteSprite;
    public Slider volumeSlider;

    [Header("Transition Settings")]
    public float fadeDuration = 0.5f;

    private bool isMuted = false;
    private CanvasGroup currentPanel;

    void Start()
    {
        currentPanel = mainMenuPanel;
        SetActivePanel(mainMenuPanel, true);
        SetActivePanel(settingsPanel, false);
        SetActivePanel(helpPanel, false);
        SetActivePanel(loadingScreen, false);

        if (volumeSlider != null)
        {
            volumeSlider.value = backgroundMusic.volume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }
    }

    // --- BUTTON FUNCTIONS ---
    public void StartGame()
    {
        StartCoroutine(LoadGameSceneAsync("SampleScene"));
    }

    private IEnumerator LoadGameSceneAsync(string sceneName)
    {
        // Show loading screen
        SetActivePanel(mainMenuPanel, false);
        SetActivePanel(loadingScreen, true);
        yield return StartCoroutine(FadeCanvasGroup(loadingScreen, 0f, 1f));

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            progressBar.value = progress;
            loadingText.text = "Loading... " + Mathf.RoundToInt(progress * 100f) + "%";

            // When loading reaches 90%, finish fade and activate
            if (asyncLoad.progress >= 0.9f)
            {
                loadingText.text = "Press any key to continue";
                if (Input.anyKeyDown)
                {
                    asyncLoad.allowSceneActivation = true;
                }
            }

            yield return null;
        }
    }

    public void ExitGame() => Application.Quit();

    public void OpenSettings() => StartCoroutine(SwitchPanel(settingsPanel));
    public void OpenHelp() => StartCoroutine(SwitchPanel(helpPanel));
    public void BackToMainMenu() => StartCoroutine(SwitchPanel(mainMenuPanel));

    public void ToggleMute()
    {
        isMuted = !isMuted;
        backgroundMusic.mute = isMuted;
        if (muteIcon != null)
            muteIcon.sprite = isMuted ? muteSprite : unmuteSprite;
    }

    public void SetVolume(float value)
    {
        backgroundMusic.volume = value;
    }

    private IEnumerator SwitchPanel(CanvasGroup newPanel)
    {
        if (currentPanel == newPanel) yield break;
        yield return StartCoroutine(FadeCanvasGroup(currentPanel, 1f, 0f));
        SetActivePanel(currentPanel, false);
        SetActivePanel(newPanel, true);
        yield return StartCoroutine(FadeCanvasGroup(newPanel, 0f, 1f));
        currentPanel = newPanel;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float start, float end)
    {
        float elapsed = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        while (elapsed < fadeDuration)
        {
            cg.alpha = Mathf.Lerp(start, end, elapsed / fadeDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        cg.alpha = end;
        cg.interactable = end > 0.9f;
        cg.blocksRaycasts = end > 0.9f;
    }

    private void SetActivePanel(CanvasGroup cg, bool active)
    {
        cg.alpha = active ? 1f : 0f;
        cg.interactable = active;
        cg.blocksRaycasts = active;
    }
}
