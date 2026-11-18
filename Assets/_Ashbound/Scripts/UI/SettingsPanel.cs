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
    [SerializeField] Button damageNumbersButton, shakeButton;
    [Header("Phase 4")]
    [SerializeField] Button colorblindButton, hudScaleButton, controlsButton, tutorialButton;
    [SerializeField] TMP_FontAsset font, boldFont;
    [SerializeField] Sprite panelSprite, fillSprite;

    static readonly float[] ShakeSteps = { 0f, 0.35f, 0.65f, 1f };
    static readonly string[] ShakeNames = { "Off", "Low", "Medium", "Full" };
    static readonly float[] HudSteps = { 0.85f, 1f, 1.15f, 1.3f };

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
        if (damageNumbersButton) damageNumbersButton.onClick.AddListener(() => { SaveSystem.Data.showDamageNumbers = !SaveSystem.Data.showDamageNumbers; SaveSystem.Save(); UpdateLabels(); });
        if (shakeButton) shakeButton.onClick.AddListener(() =>
        {
            var d = SaveSystem.Data;
            int i = (ShakeIndex() + 1) % ShakeSteps.Length;
            d.shakeStrength = ShakeSteps[i]; d.screenShake = i > 0;
            SaveSystem.Save(); UpdateLabels();
        });
        if (colorblindButton) colorblindButton.onClick.AddListener(() => { SaveSystem.Data.colorblindNumbers = !SaveSystem.Data.colorblindNumbers; SaveSystem.Save(); UpdateLabels(); });
        if (hudScaleButton) hudScaleButton.onClick.AddListener(() =>
        {
            int i = System.Array.FindIndex(HudSteps, x => Mathf.Abs(x - SaveSystem.Data.hudScale) < 0.01f);
            SaveSystem.Data.hudScale = HudSteps[(i + 1) % HudSteps.Length];
            SaveSystem.Save(); UpdateLabels();
        });
        if (controlsButton) controlsButton.onClick.AddListener(OpenControls);
        if (tutorialButton) tutorialButton.onClick.AddListener(() =>
        {
            SaveSystem.Data.tutorialDone = false;
            if (!SaveSystem.Data.seenTips.Contains("tutorial_replay")) SaveSystem.Data.seenTips.Add("tutorial_replay");
            SaveSystem.Save(); UpdateLabels();
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
        if (damageNumbersButton) damageNumbersButton.GetComponentInChildren<TMPro.TMP_Text>().text = d.showDamageNumbers ? "Damage numbers: On" : "Damage numbers: Off";
        if (shakeButton) shakeButton.GetComponentInChildren<TMPro.TMP_Text>().text = "Screen shake: " + ShakeNames[ShakeIndex()];
        if (colorblindButton) colorblindButton.GetComponentInChildren<TMP_Text>().text = d.colorblindNumbers ? "Colorblind numbers: On" : "Colorblind numbers: Off";
        if (hudScaleButton) hudScaleButton.GetComponentInChildren<TMP_Text>().text = $"HUD size: {Mathf.RoundToInt(d.hudScale * 100)}%";
        if (tutorialButton) tutorialButton.GetComponentInChildren<TMP_Text>().text = !d.tutorialDone && (d.runsPlayed == 0 || d.seenTips.Contains("tutorial_replay")) ? "Tutorial: next run" : "Replay tutorial";
        resetLabel.text = confirmReset ? "Click again to erase all progress" : "Reset progress";
    }

    static int ShakeIndex()
    {
        var d = SaveSystem.Data;
        if (!d.screenShake) return 0;
        int best = 3; float diff = 9f;
        for (int i = 1; i < ShakeSteps.Length; i++) if (Mathf.Abs(ShakeSteps[i] - d.shakeStrength) < diff) { diff = Mathf.Abs(ShakeSteps[i] - d.shakeStrength); best = i; }
        return best;
    }

    // ---------------- key rebinding (built at runtime)

    RectTransform controlsRoot;
    readonly List<(Controls.Action a, Button b)> keyButtons = new();
    int waiting = -1;

    void OpenControls()
    {
        UIKit.Font = font; UIKit.Bold = boldFont; UIKit.Panel = panelSprite; UIKit.Fill = fillSprite;
        if (controlsRoot == null)
        {
            controlsRoot = UIKit.Rect("Controls", transform);
            UIKit.Stretch(controlsRoot);
            var shade = controlsRoot.gameObject.AddComponent<Image>(); shade.color = new Color(0.03f, 0.03f, 0.05f, 0.75f); shade.raycastTarget = true;
            var panel = UIKit.Img(controlsRoot, "Panel", panelSprite, Palette.Panel, 1.6f, true);
            UIKit.Center(panel.rectTransform, Vector2.zero, new Vector2(720, 760));
            var title = UIKit.Text(panel.transform, "Title", "CONTROLS", true, 44, Palette.Accent, TextAlignmentOptions.Center);
            UIKit.At(title.rectTransform, 0, -30, 720, 60);
            var hint = UIKit.Text(panel.transform, "Hint", "Click a key, then press the new one. Arrow keys always move, Esc always pauses.", false, 19, Palette.TextMuted, TextAlignmentOptions.Center);
            UIKit.At(hint.rectTransform, 40, -92, 640, 50);
            for (int i = 0; i < Controls.All.Length; i++)
            {
                var a = Controls.All[i];
                float y = -156 - i * 74;
                var lbl = UIKit.Text(panel.transform, "Label", Controls.Label(a), true, 26, Palette.Text, TextAlignmentOptions.MidlineLeft);
                UIKit.At(lbl.rectTransform, 60, y, 320, 62);
                int idx = i;
                var b = UIKit.Button(panel.transform, "Key", "", Palette.PanelHi, () => { waiting = idx; RefreshKeys(); }, 26);
                UIKit.At((RectTransform)b.transform, 400, y, 260, 62);
                keyButtons.Add((a, b));
            }
            var reset = UIKit.Button(panel.transform, "Reset", "Reset to default", Palette.Rock, () => { Controls.ResetDefaults(); waiting = -1; RefreshKeys(); }, 24);
            reset.GetComponentInChildren<TMP_Text>().color = Palette.Text;
            UIKit.At((RectTransform)reset.transform, 60, -620, 290, 72);
            var done = UIKit.Button(panel.transform, "Done", "Done", Palette.Accent, () => { waiting = -1; controlsRoot.gameObject.SetActive(false); }, 28);
            UIKit.At((RectTransform)done.transform, 370, -620, 290, 72);
        }
        controlsRoot.gameObject.SetActive(true);
        controlsRoot.SetAsLastSibling();
        waiting = -1;
        RefreshKeys();
    }

    void RefreshKeys()
    {
        for (int i = 0; i < keyButtons.Count; i++)
        {
            var (a, b) = keyButtons[i];
            UIKit.SetLabel(b, i == waiting ? "<color=#FFD95A>Press a key...</color>" : Controls.KeyName(Controls.Get(a)));
            b.image.color = i == waiting ? Palette.Rock : Palette.PanelHi;
            var t = b.GetComponentInChildren<TMP_Text>(); if (t) t.color = Palette.Text;
        }
    }

    void Update()
    {
        if (waiting < 0 || controlsRoot == null || !controlsRoot.gameObject.activeSelf) return;
        var key = Controls.AnyKeyPressed();
        if (key == UnityEngine.InputSystem.Key.None) return;
        if (key == UnityEngine.InputSystem.Key.Escape && Controls.All[waiting] != Controls.Action.Pause) { waiting = -1; RefreshKeys(); return; }
        Controls.Set(Controls.All[waiting], key);
        waiting = -1;
        RefreshKeys();
    }
}
