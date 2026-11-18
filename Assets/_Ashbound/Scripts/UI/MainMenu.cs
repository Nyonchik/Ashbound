using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] TMP_Text goldText, crownText, gemsText;
    [SerializeField] GameObject settingsPanel, upgradesPanel, heroesPanel, unlocksPanel, gearPanel, storePanel, questsPanel, dailyPanel;
    [SerializeField] TMP_Text questsLabel, dailyLabel, heroesLabel;

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
        if (gemsText) gemsText.text = d.gems.ToString("N0");
        // little reminders on the buttons
        int ready = Achievements.Unclaimed();
        if (questsLabel) questsLabel.text = ready > 0 ? $"Quests <color=#FFD95A>({ready})</color>" : "Quests";
        if (dailyLabel) dailyLabel.text = Daily.PlayedToday ? "<color=#8C8699>Daily</color>" : "Daily <color=#FFD95A>!</color>";
        int picks = 0;
        foreach (var n in d.heroNames) picks += HeroLevels.UnspentPicks(n);
        if (heroesLabel) heroesLabel.text = picks > 0 ? $"Heroes <color=#FFD95A>({picks})</color>" : "Heroes";
    }

    public void Play() { RunSelection.Mode = RunMode.Normal; SceneManager.LoadScene("ClassSelect"); }
    public void PlayEndless() { RunSelection.Mode = RunMode.Endless; SceneManager.LoadScene("ClassSelect"); }
    public void OpenQuests() { ClosePanels(); if (questsPanel) questsPanel.SetActive(true); }
    public void OpenDaily() { ClosePanels(); if (dailyPanel) dailyPanel.SetActive(true); }
    public void OpenSettings() { ClosePanels(); if (settingsPanel) settingsPanel.SetActive(true); }
    public void OpenUpgrades() { ClosePanels(); if (upgradesPanel) upgradesPanel.SetActive(true); }
    public void OpenUnlocks() { ClosePanels(); if (unlocksPanel) unlocksPanel.SetActive(true); }
    public void OpenGear() { ClosePanels(); if (gearPanel) gearPanel.SetActive(true); }
    public void OpenStore() { ClosePanels(); if (storePanel) storePanel.SetActive(true); }
    public void OpenHeroes() { ClosePanels(); if (heroesPanel) heroesPanel.SetActive(true); }

    public void ClosePanels()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
        if (upgradesPanel) upgradesPanel.SetActive(false);
        if (heroesPanel) heroesPanel.SetActive(false);
        if (unlocksPanel) unlocksPanel.SetActive(false);
        if (gearPanel) gearPanel.SetActive(false);
        if (storePanel) storePanel.SetActive(false);
        if (questsPanel) questsPanel.SetActive(false);
        if (dailyPanel) dailyPanel.SetActive(false);
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
