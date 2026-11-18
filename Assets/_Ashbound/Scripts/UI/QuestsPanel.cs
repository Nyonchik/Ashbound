using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Daily quests (3 a day) and lifetime achievements; finished ones can be claimed for gems and gold.</summary>
public class QuestsPanel : MonoBehaviour
{
    [SerializeField] RectTransform body;
    [SerializeField] Button closeButton;
    [SerializeField] MainMenu menu;
    [SerializeField] TMP_FontAsset font, boldFont;
    [SerializeField] Sprite panelSprite, fillSprite;

    bool wired;

    static readonly Color Gem = new(0.66f, 0.44f, 1f);

    void OnEnable()
    {
        UIKit.Font = font; UIKit.Bold = boldFont; UIKit.Panel = panelSprite; UIKit.Fill = fillSprite;
        if (!wired)
        {
            wired = true;
            closeButton.onClick.AddListener(() => { if (menu) menu.ClosePanels(); else gameObject.SetActive(false); });
        }
        // stats that are checked here rather than in a run
        foreach (var g in SaveSystem.Data.gear) if (g.rarity >= 3) Achievements.Add("epic", 1);
        foreach (var n in SaveSystem.Data.heroNames) Achievements.Add("herolevel", HeroLevels.Level(n).level);
        Build();
    }

    void Build()
    {
        UIKit.Clear(body);
        var d = SaveSystem.Data;

        var head = UIKit.Text(body, "DailyHead", "DAILY QUESTS  <size=70%><color=#8C8699>new ones every day</color></size>", true, 24, Palette.TextMuted);
        UIKit.At(head.rectTransform, 0, 0, 1404, 34);
        int i = 0;
        foreach (var (q, progress, claimed) in Quests.Today3())
        {
            var cap = q;
            Card(body, i * 474f, -40f, 456f, 112f, q.text, "", progress, q.target, claimed, $"+{q.gems} gems", () => Quests.Claim(cap));
            i++;
        }

        int done = 0; foreach (var a in Achievements.All) if (Achievements.Claimed(a)) done++;
        var head2 = UIKit.Text(body, "AchHead", $"ACHIEVEMENTS  <size=70%><color=#8C8699>{done} / {Achievements.All.Length} claimed</color></size>", true, 24, Palette.TextMuted);
        UIKit.At(head2.rectTransform, 0, -170, 1404, 34);
        // unclaimed-but-done first, then in progress, then claimed
        var list = new List<Achievements.Def>(Achievements.All);
        list.Sort((x, y) => Rank(x).CompareTo(Rank(y)));
        for (int k = 0; k < list.Count; k++)
        {
            var a = list[k];
            int col = k % 4, row = k / 4;
            string reward = (a.gems > 0 ? $"+{a.gems} gems" : "") + (a.gold > 0 ? $"\n<color=#FFD95A>+{a.gold} gold</color>" : "");
            Card(body, col * 354f, -210f - row * 106f, 340f, 96f, a.name, a.description, Achievements.Get(a.stat), a.target, Achievements.Claimed(a), reward, () => Achievements.Claim(a));
        }
        if (menu) menu.Refresh();
    }

    static int Rank(Achievements.Def a) => Achievements.Claimed(a) ? 2 : Achievements.Done(a) ? 0 : 1;

    void Card(RectTransform parent, float x, float y, float w, float h, string title, string desc, float value, float target, bool claimed, string reward, System.Action claim)
    {
        bool done = value >= target;
        Color edge = claimed ? Palette.Rock : done ? Palette.Gold : Palette.PanelHi;
        var card = UIKit.Img(parent, "Card", panelSprite, edge, 1.6f);
        UIKit.At(card.rectTransform, x, y, w, h);
        var inner = UIKit.Img(card.transform, "Inner", panelSprite, claimed ? Palette.Void : Palette.PanelHi, 1.6f); UIKit.Stretch(inner.rectTransform, 3);

        float textW = w - 128;
        var t = UIKit.Text(card.transform, "Title", title, true, desc.Length > 0 ? 21 : 22, claimed ? Palette.TextMuted : Palette.Text);
        UIKit.At(t.rectTransform, 14, -8, textW, 28);
        if (desc.Length > 0)
        {
            var dsc = UIKit.Text(card.transform, "Desc", desc, false, 16, Palette.TextMuted);
            UIKit.At(dsc.rectTransform, 14, -33, textW, 22);
            dsc.textWrappingMode = TMPro.TextWrappingModes.NoWrap; dsc.enableAutoSizing = true; dsc.fontSizeMin = 11; dsc.fontSizeMax = 16;
        }
        // progress bar
        float k = Mathf.Clamp01(target <= 0 ? 1f : value / target);
        var barBg = UIKit.Img(card.transform, "Bar", fillSprite, Palette.Void, 1.6f); UIKit.At(barBg.rectTransform, 14, -(h - 22), textW, 10);
        var bar = UIKit.Img(barBg.transform, "Fill", fillSprite, done ? Palette.Gold : Palette.Accent, 1.6f);
        UIKit.At(bar.rectTransform, 0, 0, Mathf.Max(4f, textW * k), 10);
        var num = UIKit.Text(card.transform, "Num", Short(Mathf.Min(value, target)) + " / " + Short(target), false, 14, Palette.TextMuted);
        UIKit.At(num.rectTransform, 14, -(h - 42), textW, 20);

        var rw = UIKit.Text(card.transform, "Reward", reward.Trim(), true, 15, Gem, TextAlignmentOptions.Center);
        UIKit.At(rw.rectTransform, w - 116, -10, 104, 40);
        if (claimed)
        {
            var c = UIKit.Text(card.transform, "Claimed", "Claimed", true, 18, Palette.TextMuted, TextAlignmentOptions.Center);
            UIKit.At(c.rectTransform, w - 116, -(h - 48), 104, 36);
        }
        else if (done)
        {
            var b = UIKit.Button(card.transform, "Claim", done ? "Claim" : "...", done ? Palette.Gold : Palette.Rock, () =>
            {
                claim();
                AudioManager.Play("coin", 1f);
                Build();
            }, 20);
            UIKit.At((RectTransform)b.transform, w - 112, -(h - 42), 98, 34);
        }
    }

    static string Short(float v) => v >= 10000 ? (v / 1000f).ToString("0.#") + "k" : v >= 100 ? Mathf.FloorToInt(v).ToString("N0") : Mathf.FloorToInt(v).ToString();
}
