using UnityEngine;
using System.Collections;

public class ToggleMenu : MonoBehaviour
{
    [Header("Menu References")]
    public CanvasGroup menuGroup;     // drag Menu_Panel's CanvasGroup here
    public KeyCode toggleKey = KeyCode.I;
    public float fadeDuration = 0.3f; // seconds

    private bool isVisible = false;
    private Coroutine fadeRoutine;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleMenuVisibility();
        }
    }

    void ToggleMenuVisibility()
    {
        isVisible = !isVisible;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeMenu(isVisible));
    }

    IEnumerator FadeMenu(bool show)
    {
        float startAlpha = menuGroup.alpha;
        float endAlpha = show ? 1f : 0f;
        float elapsed = 0f;

        // Enable interaction while fading in
        if (show)
        {
            menuGroup.interactable = true;
            menuGroup.blocksRaycasts = true;
        }

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            menuGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            yield return null;
        }

        menuGroup.alpha = endAlpha;

        // Disable interaction when faded out
        if (!show)
        {
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;
        }
    }
}