using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject controlsPanel;
    public GameObject menuPanel;

    private void Start()
    {
        controlsPanel.SetActive(false);
    }

    public void PlayGame()
    {
        SceneManager.LoadScene("MainGame");
    }

    public void OpenControls()
    {
        controlsPanel.SetActive(true);
        menuPanel.SetActive(false);
    }

    public void CloseControls()
    {
        controlsPanel.SetActive(false);
        menuPanel.SetActive(true);
    }

    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}