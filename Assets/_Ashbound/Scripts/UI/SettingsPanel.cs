using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    [SerializeField] Slider musicSlider, sfxSlider;
    [SerializeField] Button fullscreenButton, resPrev, resNext, resetButton, closeButton;
    [SerializeField] TMP_Text fullscreenLabel, resolutionLabel, resetLabel;
    [SerializeField] MainMenu menu;

    bool wired, confirmReset;

    static List<Vector2Int> Resolutions()
    {
        var list = new List<Vector2Int>();
        foreach (var r in Screen.resolutions)
        {
            var v = new Vector2Int(r.width, r.height);
            if (!list.Contains(v)) list.Add(v);
        }
        if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
        return list;
    }

    public static void ApplyDisplay()
    {
        var d = SaveSystem.Data;
        var list = Resolutions();
        if (d.resolutionIndex >= 0 && d.resolutionIndex < list.Count)
        {
            var r = list[d.resolutionIndex];
            Screen.SetResolution(r.x, r.y, d.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }
        else Screen.fullScreenMode = d.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    void OnEnable()
    {
        Wire();
        var d = SaveSystem.Data;
        musicSlider.SetValueWithoutNotify(d.musicVolume);
        sfxSlider.SetValueWithoutNotify(d.sfxVolume);
        confirmReset = false;
        UpdateLabels();
    }

    void Wire()
    {
        if (wired) return;
        wired = true;
        musicSlider.onValueChanged.AddListener(v => { SaveSystem.Data.musicVolume = v; AudioManager.ApplyVolumes(); SaveSystem.Save(); });
        sfxSlider.onValueChanged.AddListener(v => { SaveSystem.Data.sfxVolume = v; SaveSystem.Save(); AudioManager.Play("gem", 0.6f, 1f, 0.1f); });
        fullscreenButton.onClick.AddListener(() => { SaveSystem.Data.fullscreen = !SaveSystem.Data.fullscreen; Apply(); });
        resPrev.onClick.AddListener(() => StepResolution(-1));
        resNext.onClick.AddListener(() => StepResolution(1));
        resetButton.onClick.AddListener(() =>
        {
            if (!confirmReset) { confirmReset = true; UpdateLabels(); return; }
            SaveSystem.ResetProgress();
            confirmReset = false;
            UpdateLabels();
            if (menu) menu.Refresh();
        });
        closeButton.onClick.AddListener(() => { if (menu) menu.ClosePanels(); else gameObject.SetActive(false); });
    }

    void StepResolution(int delta)
    {
        var list = Resolutions();
        int i = SaveSystem.Data.resolutionIndex;
        if (i < 0 || i >= list.Count) i = Mathf.Max(0, list.IndexOf(new Vector2Int(Screen.width, Screen.height)));
        SaveSystem.Data.resolutionIndex = (i + delta + list.Count) % list.Count;
        Apply();
    }

    void Apply()
    {
        SaveSystem.Save();
        ApplyDisplay();
        UpdateLabels();
    }

    void UpdateLabels()
    {
        var d = SaveSystem.Data;
        fullscreenLabel.text = d.fullscreen ? "Fullscreen: On" : "Fullscreen: Off";
        var list = Resolutions();
        var r = d.resolutionIndex >= 0 && d.resolutionIndex < list.Count ? list[d.resolutionIndex] : new Vector2Int(Screen.width, Screen.height);
        resolutionLabel.text = $"{r.x} × {r.y}";
        resetLabel.text = confirmReset ? "Click again to erase all progress" : "Reset progress";
    }
}
