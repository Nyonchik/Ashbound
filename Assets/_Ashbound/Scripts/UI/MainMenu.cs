using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] TMP_Text goldText, crownText;
    [SerializeField] GameObject settingsPanel, upgradesPanel, heroesPanel;

    void Start()
    {
        Time.timeScale = 1f;
        SettingsPanel.ApplyDisplay();
        AudioManager.PlayMusic("music_menu");
        ClosePanels();
        Refresh();
    }

    public void Refresh()
    {
        var d = SaveSystem.Data;
        if (goldText) goldText.text = d.gold.ToString("N0");
        if (crownText) crownText.text = $"{d.stagesWon.Count} / 3";
    }

    public void Play() => SceneManager.LoadScene("ClassSelect");
    public void OpenSettings() { ClosePanels(); if (settingsPanel) settingsPanel.SetActive(true); }
    public void OpenUpgrades() { ClosePanels(); if (upgradesPanel) upgradesPanel.SetActive(true); }
    public void OpenHeroes() { ClosePanels(); if (heroesPanel) heroesPanel.SetActive(true); }

    public void ClosePanels()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
        if (upgradesPanel) upgradesPanel.SetActive(false);
        if (heroesPanel) heroesPanel.SetActive(false);
        Refresh();
    }

    public void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
