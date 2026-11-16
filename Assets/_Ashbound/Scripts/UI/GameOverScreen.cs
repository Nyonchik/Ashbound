using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverScreen : MonoBehaviour
{
    [SerializeField] TMP_Text title, stats;
    [SerializeField] TMP_Text rewards;
    [SerializeField] Button retryButton, menuButton;

    public bool IsOpen => gameObject.activeSelf;

    void Awake()
    {
        retryButton.onClick.AddListener(() => { SceneManager.LoadScene(SceneManager.GetActiveScene().name); });
        menuButton.onClick.AddListener(() => { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); });
    }

    public void Show(bool won, float time, int level, int kills, int gold, string notes = "")
    {
        title.text = won ? "VICTORY" : "YOU FELL";
        title.color = won ? Palette.Gold : Palette.Health;
        int m = (int)(time / 60f), s = (int)(time % 60f);
        stats.text = $"Survived {m:00}:{s:00}\nLevel {level}  ·  {kills:N0} kills  ·  {gold:N0} gold";
        if (rewards != null) rewards.text = notes;
        gameObject.SetActive(true);
        Time.timeScale = 0f;
    }
}
