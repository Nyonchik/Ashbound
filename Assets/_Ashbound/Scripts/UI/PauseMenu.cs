using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [SerializeField] GameObject overlay;
    [SerializeField] GameObject levelUpScreen; // don't pause on top of a level-up
    [SerializeField] GameObject gameOverScreen;

    public bool IsPaused => overlay.activeSelf;

    void Start() => overlay.SetActive(false);

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Toggle();
    }

    public void Toggle()
    {
        if (levelUpScreen && levelUpScreen.activeSelf) return;
        if (gameOverScreen && gameOverScreen.activeSelf) return;
        bool pause = !overlay.activeSelf;
        overlay.SetActive(pause);
        if (pause) GameSpeed.Pause(); else GameSpeed.Resume();
    }

    public void ToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}
