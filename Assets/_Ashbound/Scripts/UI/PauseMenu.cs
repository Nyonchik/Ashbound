using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Pause overlay: your whole loadout as cards, volume sliders, Resume / Restart / Main menu.</summary>
public class PauseMenu : MonoBehaviour
{
    [SerializeField] GameObject overlay;
    [SerializeField] GameObject levelUpScreen; // don't pause on top of a level-up
    [SerializeField] GameObject gameOverScreen;
    [SerializeField] GameObject chestPopup;

    [Header("Loadout cards")]
    [SerializeField] RectTransform loadoutGrid;
    [SerializeField] TMP_FontAsset font, boldFont;
    [SerializeField] Sprite cardSprite, ringSprite;
    [SerializeField] TMP_Text statsText;

    [Header("Settings")]
    [SerializeField] Slider musicSlider, sfxSlider;
    [SerializeField] Button restartButton;
    [Tooltip("Wired in code (scene-saved listeners on prefab instances can get lost)")]
    [SerializeField] Button pauseButton, resumeButton, menuButton;

    public bool IsPaused => overlay.activeSelf;

    void Start()
    {
        overlay.SetActive(false);
        if (musicSlider)
        {
            musicSlider.SetValueWithoutNotify(SaveSystem.Data.musicVolume);
            musicSlider.onValueChanged.AddListener(v => { SaveSystem.Data.musicVolume = v; AudioManager.ApplyVolumes(); });
        }
        if (sfxSlider)
        {
            sfxSlider.SetValueWithoutNotify(SaveSystem.Data.sfxVolume);
            sfxSlider.onValueChanged.AddListener(v => { SaveSystem.Data.sfxVolume = v; AudioManager.Play("gem", 0.6f, 1f, 0.1f); });
        }
        if (restartButton) restartButton.onClick.AddListener(Restart);
        if (pauseButton) { pauseButton.onClick.RemoveAllListeners(); pauseButton.onClick.AddListener(Toggle); }
        if (resumeButton) { resumeButton.onClick.RemoveAllListeners(); resumeButton.onClick.AddListener(Toggle); }
        if (menuButton) { menuButton.onClick.RemoveAllListeners(); menuButton.onClick.AddListener(ToMainMenu); }
    }

    void Update()
    {
        if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame)) Toggle();
    }

    public void Toggle()
    {
        if (levelUpScreen && levelUpScreen.activeSelf) return;
        if (gameOverScreen && gameOverScreen.activeSelf) return;
        if (chestPopup && chestPopup.activeSelf) return;
        bool pause = !overlay.activeSelf;
        overlay.SetActive(pause);
        if (pause) { GameSpeed.Pause(); BuildLoadout(); }
        else { GameSpeed.Resume(); SaveSystem.Save(); }
    }

    public void Restart()
    {
        SaveSystem.Save();
        GameSpeed.Resume();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ToMainMenu()
    {
        SaveSystem.Save();
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    // ---------------- loadout cards

    void BuildLoadout()
    {
        var run = RunManager.Instance;
        if (run == null || loadoutGrid == null) return;
        for (int i = loadoutGrid.childCount - 1; i >= 0; i--) Destroy(loadoutGrid.GetChild(i).gameObject);

        foreach (var w in run.Weapons)
        {
            string tag = w.IsEvolved ? "EVOLVED" : w.Awakened ? "AWAKENED" : w.IsPath ? "PATH SKILL" : "WEAPON";
            string lvl = w.IsEvolved ? "Evolved" : w.Awakened ? "Awakened" : $"Lv {w.Level} / {w.MaxLevel}";
            string next;
            if (w.IsEvolved || w.Awakened) next = w.Def.levelText != null && w.Def.levelText.Length > 0 ? w.Def.levelText[w.IsEvolved ? 0 : Mathf.Min(5, w.Def.levelText.Length - 1)] : "";
            else if (w.CanLevel) next = "Next: " + w.NextLevelText;
            else next = w.Def.evolvePassive != null ? $"Evolves with {w.Def.evolvePassive.passiveName} Lv 5" : "Max level";
            Card(w.Icon, w.DisplayName, tag, lvl, next, UpgradeCard.TypeColor(w.IsPath ? "PATH SKILL" : "WEAPON", w.Golden));
        }
        foreach (var p in run.Passives)
        {
            int lv = run.PassiveLevel(p);
            Card(p.icon, p.passiveName, "PASSIVE", $"Lv {lv} / 5", p.description + (lv >= 5 ? " (max)" : ""), UpgradeCard.TypeColor("PASSIVE", false));
        }

        if (statsText)
        {
            var s = PlayerController.Instance != null ? PlayerController.Instance.Stats : null;
            var sb = new StringBuilder();
            sb.Append($"Level {run.Level}");
            if (s != null) sb.Append($"   ·   {Mathf.CeilToInt(s.Health)} / {Mathf.CeilToInt(s.MaxHealth)} HP   ·   Damage x{s.damageMul:0.00}   ·   Speed x{s.moveMul:0.00}");
            var syns = new List<string>();
            foreach (var id in Synergies.ActiveIds) { var d = Synergies.Get(id); if (d != null) syns.Add($"<color=#{ColorUtility.ToHtmlStringRGB(d.color)}>{d.name}</color>"); }
            if (syns.Count > 0) sb.Append("\nSynergies: " + string.Join("  ·  ", syns));
            statsText.text = sb.ToString();
        }
    }

    void Card(Sprite icon, string name, string tag, string level, string info, Color type)
    {
        var card = New<Image>("Card", loadoutGrid);
        card.sprite = cardSprite; card.type = Image.Type.Sliced; card.pixelsPerUnitMultiplier = 1.6f; card.color = Palette.PanelHi;
        var ring = New<Image>("Ring", card.rectTransform);
        Stretch(ring.rectTransform, -2);
        ring.sprite = ringSprite; ring.type = Image.Type.Sliced; ring.pixelsPerUnitMultiplier = 1.6f; ring.color = type;

        var box = New<Image>("IconBox", card.rectTransform);
        Place(box.rectTransform, new Vector2(0, 0.5f), new Vector2(52, 0), new Vector2(80, 80));
        box.sprite = cardSprite; box.type = Image.Type.Sliced; box.pixelsPerUnitMultiplier = 2f; box.color = Palette.Void;
        var ic = New<Image>("Icon", box.rectTransform);
        Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
        ic.sprite = icon; ic.preserveAspect = true; ic.enabled = icon != null;

        var tagT = Text("Tag", card.rectTransform, tag, boldFont, 14, type, new Vector2(104, -12), new Vector2(130, 20));
        tagT.characterSpacing = 4;
        Text("Level", card.rectTransform, level, boldFont, 15, Palette.TextMuted, new Vector2(226, -12), new Vector2(104, 20)).alignment = TextAlignmentOptions.TopRight;
        var n = Text("Name", card.rectTransform, name, boldFont, 23, Palette.Text, new Vector2(104, -32), new Vector2(226, 30));
        n.enableAutoSizing = true; n.fontSizeMin = 15; n.fontSizeMax = 23;
        var t = Text("Info", card.rectTransform, info, font, 15, Palette.TextMuted, new Vector2(104, -62), new Vector2(226, 34));
        t.textWrappingMode = TextWrappingModes.Normal; t.enableAutoSizing = true; t.fontSizeMin = 11; t.fontSizeMax = 16;
    }

    static T New<T>(string name, RectTransform parent) where T : Graphic
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(T));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var g = go.GetComponent<T>();
        g.raycastTarget = false;
        return g;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.one * inset; rt.offsetMax = -Vector2.one * inset;
    }

    static TextMeshProUGUI Text(string name, RectTransform parent, string text, TMP_FontAsset f, float size, Color c, Vector2 topLeft, Vector2 box)
    {
        var t = New<TextMeshProUGUI>(name, parent);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = topLeft; rt.sizeDelta = box;
        t.font = f; t.text = text; t.fontSize = size; t.color = c;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Ellipsis;
        t.alignment = TextAlignmentOptions.TopLeft;
        return t;
    }
}
