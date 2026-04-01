using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuLoader : MonoBehaviour
{
    public void GoToMainMenu()
    {
        SceneManager.LoadScene("Menu");
    }
}
