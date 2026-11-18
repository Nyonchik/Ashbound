using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Daily challenge: today's hero, Path, stage and 2 curses; one attempt; gems by time survived.</summary>
public class DailyPanel : MonoBehaviour
{
    [SerializeField] List<ClassData> classes = new();
    [SerializeField] List<StageData> stages = new();
    [SerializeField] RectTransform body;
    [SerializeField] Button closeButton;
    [SerializeField] MainMenu menu;
    [SerializeField] TMP_FontAsset font, boldFont;
    [SerializeField] Sprite panelSprite, fillSprite;

    bool wired;
    TMP_Text timer;

    void OnEnable()
    {
        UIKit.Font = font; UIKit.Bold = boldFont; UIKit.Panel = panelSprite; UIKit.Fill = fillSprite;
        if (!wired)
        {
            wired = true;
            closeButton.onClick.AddListener(() => { if (menu) menu.ClosePanels(); else gameObject.SetActive(false); });
        }
        Build();
    }

    void Build()
    {
        UIKit.Clear(body);
        if (classes.Count == 0 || stages.Count == 0) return;
        var (cls, path, stage, ca, cb) = Daily.Pick(classes, stages);

        var art = UIKit.Icon(body, "Hero", path.heroArt != null ? path.heroArt : cls.portrait);
        UIKit.At(art.rectTransform, 0, 0, 260, 260);

        string hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
        var info = UIKit.Text(body, "Info",
            $"<size=120%><color=#{hex(cls.color)}>{cls.className}</color></size>\n" +
            $"Path: <color=#{hex(path.color)}>{path.pathName}</color>\n" +
            $"Stage: {stage.stageName}\n\n" +
            "<color=#F08A3C>Today's curses</color>\n" +
            $"• {Ascension.Curses[ca - 1]}\n• {Ascension.Curses[cb - 1]}",
            true, 24, Palette.Text);
        UIKit.At(info.rectTransform, 290, 0, 470, 260);

        var rule = UIKit.Text(body, "Rules",
            "Everyone gets the same hero, Path, stage and curses today. Your gear, upgrades and talents still count, even if you haven't unlocked this hero yet.\n" +
            "<color=#A970FF>Reward: 10 to 60 gems</color> depending on how long you survive (60 at 15:00). <color=#B9B1C2>One attempt: leaving the run uses it.</color>",
            false, 19, Palette.TextMuted);
        UIKit.At(rule.rectTransform, 0, -280, 760, 120);

        var d = SaveSystem.Data;
        bool played = Daily.PlayedToday;
        string status = played
            ? $"Done for today: survived {d.dailyBest / 60f:0}:{d.dailyBest % 60f:00} for <color=#A970FF>{d.dailyGems} gems</color>"
            : "Ready!";
        var st = UIKit.Text(body, "Status", status, true, 24, played ? Palette.TextMuted : Palette.Gold, TextAlignmentOptions.Center);
        UIKit.At(st.rectTransform, 0, -410, 760, 36);
        timer = UIKit.Text(body, "Timer", "", false, 18, Palette.TextMuted, TextAlignmentOptions.Center);
        UIKit.At(timer.rectTransform, 0, -448, 760, 28);

        var start = UIKit.Button(body, "Start", played ? "Come back tomorrow" : "Start daily", played ? Palette.Rock : Palette.Accent, () =>
        {
            RunSelection.Class = cls; RunSelection.Path = path; RunSelection.Stage = stage;
            RunSelection.Mode = RunMode.Daily; RunSelection.Ascension = 0;
            RunSelection.CurseA = ca; RunSelection.CurseB = cb;
            SceneManager.LoadScene("Game");
        }, 28);
        UIKit.At((RectTransform)start.transform, 200, -486, 360, 76);
        start.interactable = !played;
    }

    void Update()
    {
        if (timer == null) return;
        var left = DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow;
        timer.text = $"New challenge in {(int)left.TotalHours}h {left.Minutes:00}m";
    }
}
