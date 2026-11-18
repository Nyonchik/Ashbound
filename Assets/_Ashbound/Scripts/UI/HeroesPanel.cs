using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows every hero and Path, what's unlocked, and how to unlock the rest.</summary>
public class HeroesPanel : MonoBehaviour
{
    [SerializeField] List<ClassData> classes = new();
    [SerializeField] TMP_Text[] columns;
    [SerializeField] TMP_Text summary;
    [SerializeField] Button closeButton;
    [SerializeField] MainMenu menu;
    [Header("Hero levels + talents (built at runtime)")]
    [SerializeField] RectTransform[] talentAreas;
    [SerializeField] TMP_FontAsset font, boldFont;
    [SerializeField] Sprite panelSprite, fillSprite;

    bool wired;

    void OnEnable()
    {
        if (!wired)
        {
            wired = true;
            closeButton.onClick.AddListener(() => { if (menu) menu.ClosePanels(); else gameObject.SetActive(false); });
        }
        for (int i = 0; i < columns.Length; i++)
        {
            if (i >= classes.Count) { columns[i].text = ""; continue; }
            var c = classes[i];
            bool open = SaveSystem.IsClassUnlocked(c);
            var sb = new StringBuilder();
            sb.Append($"<size=130%><color=#{ColorUtility.ToHtmlStringRGB(c.color)}>{c.className}</color></size>\n");
            sb.Append(open ? "<color=#4CC38A>Unlocked</color>\n\n" : $"<color=#F08A3C>Locked</color>\n<size=85%>{c.unlockHint}</size>\n\n");
            foreach (var p in c.paths)
            {
                bool pOpen = SaveSystem.IsPathUnlocked(c, p);
                string hex = ColorUtility.ToHtmlStringRGB(p.color);
                sb.Append(pOpen ? $"<color=#{hex}>•</color> {p.pathName}\n" : $"<color=#5A566A>•</color> <color=#8C8699>{p.pathName}</color>\n");
            }
            if (open) sb.Append($"\n<size=80%><color=#8C8699>Survive 10:00 as {c.className} to unlock the next Path</color></size>");
            columns[i].text = sb.ToString();
        }
        BuildTalents();
        var d = SaveSystem.Data;
        if (summary) summary.text = $"Runs played: {d.runsPlayed}   ·   Total kills: {d.totalKills:N0}   ·   Crown shards: {d.stagesWon.Count} / 3";
    }

    /// <summary>Under each hero: level, XP bar and the 5 talent rows (Lv 4/8/12/16/20, pick 1 of 2).</summary>
    void BuildTalents()
    {
        if (talentAreas == null) return;
        UIKit.Font = font; UIKit.Bold = boldFont; UIKit.Panel = panelSprite; UIKit.Fill = fillSprite;
        for (int i = 0; i < talentAreas.Length; i++)
        {
            var area = talentAreas[i];
            if (area == null) continue;
            UIKit.Clear(area);
            if (i >= classes.Count) continue;
            var c = classes[i];
            var (lv, into, need) = HeroLevels.Level(c.className);
            var head = UIKit.Text(area, "Level", $"<color=#{ColorUtility.ToHtmlStringRGB(c.color)}>Level {lv}</color>  <size=75%><color=#8C8699>{(need > 0 ? $"{into} / {need} XP" : "MAX")}</color></size>", true, 22, Palette.Text);
            UIKit.At(head.rectTransform, 0, 0, 300, 30);
            var bg = UIKit.Img(area, "XpBar", fillSprite, Palette.Void, 1.6f); UIKit.At(bg.rectTransform, 0, -34, 300, 10);
            var fill = UIKit.Img(bg.transform, "Fill", fillSprite, c.color, 1.6f); UIKit.At(fill.rectTransform, 0, 0, Mathf.Max(4f, 300f * (need > 0 ? (float)into / need : 1f)), 10);
            for (int t = 0; t < 5; t++)
            {
                float y = -56 - t * 62;
                bool open = HeroLevels.TierOpen(c.className, t);
                int pick = HeroLevels.Pick(c.className, t);
                var lbl = UIKit.Text(area, "Tier", $"Lv\n{(t + 1) * 4}", true, 15, open ? Palette.Gold : Palette.TextMuted, TextAlignmentOptions.Center);
                UIKit.At(lbl.rectTransform, 0, y - 6, 48, 46);
                for (int k = 0; k < 2; k++)
                {
                    var tal = HeroLevels.Table[t, k];
                    string label = tal.name.Contains(":") ? tal.name.Substring(tal.name.IndexOf(':') + 1).Trim() : tal.name;
                    bool chosen = pick == k, canPick = open && pick < 0;
                    Color bgc = chosen ? c.color : canPick ? Palette.Gold : Palette.PanelHi;
                    int tier = t, choice = k; string cn = c.className;
                    var b = UIKit.Button(area, "Talent", label, bgc, () => { HeroLevels.Choose(cn, tier, choice); AudioManager.Play("levelup", 0.6f); BuildTalents(); if (menu) menu.Refresh(); }, 15);
                    UIKit.At((RectTransform)b.transform, 52 + k * 124, y, 120, 56);
                    var txt = b.GetComponentInChildren<TextMeshProUGUI>();
                    txt.textWrappingMode = TextWrappingModes.Normal; txt.fontSizeMax = 15; txt.fontSizeMin = 10;
                    // locked or not chosen: dark card with readable gray text; chosen: hero color
                    var cb = b.colors; cb.disabledColor = Color.white; b.colors = cb;
                    if (!canPick && !chosen) { b.image.color = Palette.PanelHi; txt.color = Palette.TextMuted; }
                    b.interactable = canPick;
                }
            }
        }
    }
}
