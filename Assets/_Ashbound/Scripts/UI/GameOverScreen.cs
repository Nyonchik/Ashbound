using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverScreen : MonoBehaviour
{
    [SerializeField] TMP_Text title, stats;
    [SerializeField] TMP_Text rewards;
    [SerializeField] TMP_Text breakdown;
    [SerializeField] Image icon, frameRing;
    [SerializeField] Sprite winIcon, loseIcon;
    [SerializeField] Button retryButton, menuButton;

    public bool IsOpen => gameObject.activeSelf;

    void Awake()
    {
        retryButton.onClick.AddListener(() => { SceneManager.LoadScene(SceneManager.GetActiveScene().name); });
        menuButton.onClick.AddListener(() => { Time.timeScale = 1f; SceneManager.LoadScene("MainMenu"); });
    }

    public void Show(bool won, float time, int level, int kills, int gold, string notes = "", string details = "")
    {
        title.text = won ? "VICTORY" : "YOU FELL";
        title.color = won ? Palette.Gold : Palette.Health;
        int m = (int)(time / 60f), s = (int)(time % 60f);
        stats.text = $"Survived {m:00}:{s:00}\nLevel {level}  ·  {kills:N0} kills  ·  {gold:N0} gold";
        if (rewards != null) rewards.text = notes;
        if (breakdown != null) breakdown.text = details;
        if (icon) { icon.sprite = won ? winIcon : loseIcon; icon.enabled = icon.sprite != null; icon.preserveAspect = true; }
        if (frameRing) frameRing.color = won ? Palette.Gold : new Color(0.9f, 0.28f, 0.3f, 0.8f);
        gameObject.SetActive(true);
        Time.timeScale = 0f;
    }
}
