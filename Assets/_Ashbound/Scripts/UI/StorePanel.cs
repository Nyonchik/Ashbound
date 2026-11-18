using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Store: a free daily chest plus Wooden (gold), Silver and Royal (gems) chests, with an opening animation.</summary>
public class StorePanel : MonoBehaviour
{
    [SerializeField] List<ClassData> classes = new();
    [SerializeField] RectTransform body;
    [SerializeField] Button closeButton;
    [SerializeField] MainMenu menu;
    [SerializeField] TMP_FontAsset font, boldFont;
    [SerializeField] Sprite panelSprite, fillSprite, glowSprite, fallbackChest;

    bool built, opening;
    TMP_Text wallet, freeLabel;
    Button freeButton;
    readonly List<(Gear.Chest c, Button b)> buyButtons = new();
    RectTransform reveal;
    float tick;
    int runId;

    void OnEnable()
    {
        UIKit.Font = font; UIKit.Bold = boldFont; UIKit.Panel = panelSprite; UIKit.Fill = fillSprite;
        if (!built) Build();
        opening = false;
        if (reveal) reveal.gameObject.SetActive(false);
        Refresh();
    }

    Sprite ChestArt(string id) => Gear.Icon(id) ?? fallbackChest;

    void Build()
    {
        built = true;
        closeButton.onClick.AddListener(() => { if (opening) return; if (menu) menu.ClosePanels(); else gameObject.SetActive(false); });

        wallet = UIKit.Text(body, "Wallet", "", true, 28, Palette.Text, TextAlignmentOptions.Center);
        UIKit.At(wallet.rectTransform, 0, 0, 1404, 44);

        const float w = 330, gap = 28, top = -62, h = 540;
        for (int i = 0; i < 4; i++)
        {
            bool free = i == 0;
            var chest = Gear.Chests[free ? 0 : i - 1];
            Color tint = i switch { 0 => Palette.Accent, 1 => Gear.RarityColors[0], 2 => Gear.RarityColors[2], _ => Gear.RarityColors[4] };
            var card = UIKit.Img(body, free ? "FreeChest" : chest.id, panelSprite, tint, 1.6f);   // colored border
            UIKit.At(card.rectTransform, i * (w + gap), top, w, h);
            var inner = UIKit.Img(card.transform, "Inner", panelSprite, Palette.PanelHi, 1.6f); UIKit.Stretch(inner.rectTransform, 5);

            var title = UIKit.Text(card.transform, "Title", free ? "Daily Free Chest" : chest.name, true, 30, tint, TextAlignmentOptions.Center);
            UIKit.At(title.rectTransform, 10, -18, w - 20, 44);
            var glow = UIKit.Icon(card.transform, "Glow", glowSprite); glow.color = new Color(tint.r, tint.g, tint.b, 0.35f);
            UIKit.At(glow.rectTransform, 25, -60, w - 50, w - 50);
            var art = UIKit.Icon(card.transform, "Chest", ChestArt(chest.id));
            UIKit.At(art.rectTransform, 50, -80, w - 100, w - 110);
            var odds = UIKit.Text(card.transform, "Odds", (free ? "A Wooden Chest, free once a day.\n" : "") + chest.blurb, false, 19, Palette.TextMuted, TextAlignmentOptions.Top);
            UIKit.At(odds.rectTransform, 16, -306, w - 32, 140);
            odds.enableAutoSizing = true; odds.fontSizeMin = 13; odds.fontSizeMax = 19;

            string price = free ? "Open free" : chest.gold > 0 ? $"{chest.gold} gold" : $"{chest.gems} gems";
            var c = chest;
            var b = UIKit.Button(card.transform, "Buy", price, free ? Palette.Accent : chest.gold > 0 ? Palette.Gold : Gear.RarityColors[3], () => Open(c, free), 26);
            UIKit.At((RectTransform)b.transform, 30, -h + 90, w - 60, 66);
            if (free) { freeButton = b; freeLabel = b.GetComponentInChildren<TMP_Text>(); }
            else buyButtons.Add((chest, b));
        }

        var how = UIKit.Text(body, "How", "Gems: +3 per boss, +5 for reaching 10:00, +20 for a win (+30 more the first time you win a stage).",
            false, 19, Palette.TextMuted, TextAlignmentOptions.Center);
        UIKit.At(how.rectTransform, 0, top - h - 14, 1404, 30);

        // reveal overlay
        reveal = UIKit.Rect("Reveal", body);
        UIKit.At(reveal, -20, 10, 1444, 680);
        var dim = reveal.gameObject.AddComponent<Image>(); dim.color = new Color(0.05f, 0.05f, 0.07f, 0.96f); dim.raycastTarget = true;
        reveal.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!built) return;
        tick -= Time.unscaledDeltaTime;
        if (tick > 0f) return;
        tick = 1f;
        RefreshFree();
    }

    void RefreshFree()
    {
        var wait = Gear.FreeChestWait();
        bool ready = wait <= System.TimeSpan.Zero;
        freeButton.interactable = ready && !opening;
        freeLabel.text = ready ? "Open free" : $"{(int)wait.TotalHours}h {wait.Minutes:00}m";
    }

    void Refresh()
    {
        var d = SaveSystem.Data;
        wallet.text = $"<color=#FFD95A>{d.gold:N0} gold</color>      <color=#A970FF>{d.gems:N0} gems</color>";
        foreach (var (c, b) in buyButtons) b.interactable = !opening && d.gold >= c.gold && d.gems >= c.gems;
        RefreshFree();
        if (menu) menu.Refresh();
    }

    List<string> Unlocked()
    {
        var list = new List<string>();
        foreach (var c in classes) if (SaveSystem.IsClassUnlocked(c)) list.Add(c.className);
        return list;
    }

    void Open(Gear.Chest c, bool free)
    {
        if (opening) return;
        var item = Gear.Open(c, free, Unlocked());
        if (item == null) { Refresh(); return; }
        StartCoroutine(Reveal(c, item, free));
    }

    IEnumerator Reveal(Gear.Chest c, GearItem item, bool free)
    {
        opening = true;
        int my = ++runId;
        Refresh();
        UIKit.Clear(reveal);
        reveal.gameObject.SetActive(true);
        var col = Gear.RarityColors[item.rarity];

        var glow = UIKit.Icon(reveal, "Glow", glowSprite); UIKit.Center(glow.rectTransform, new Vector2(0, 60), new Vector2(560, 560));
        glow.color = new Color(col.r, col.g, col.b, 0f);
        var chest = UIKit.Icon(reveal, "Chest", ChestArt(c.id)); UIKit.Center(chest.rectTransform, new Vector2(0, 40), new Vector2(300, 260));
        chest.rectTransform.localScale = Vector3.zero;

        // drop in
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.35f;
            float k = Mathf.Clamp01(t);
            chest.rectTransform.localScale = Vector3.one * (k < 0.7f ? Mathf.Lerp(0f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f));
            yield return null;
        }
        // shake, harder for better loot
        float shake = 6f + item.rarity * 3f, dur = 0.5f + item.rarity * 0.12f;
        t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            chest.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 55f) * shake * (1f - t / dur * 0.6f));
            glow.color = new Color(col.r, col.g, col.b, Mathf.Clamp01(t / dur) * 0.5f);
            yield return null;
        }
        chest.rectTransform.localRotation = Quaternion.identity;

        // burst: chest fades, item card grows in
        AudioManager.Play(item.rarity >= 3 ? "evolve" : "levelup", 0.9f);
        AudioManager.Play("coin", 0.8f);
        var card = UIKit.Img(reveal, "Card", panelSprite, col, 1.6f);
        UIKit.Center(card.rectTransform, new Vector2(0, 70), new Vector2(380, 430));
        var inner = UIKit.Img(card.transform, "In", panelSprite, Color.Lerp(Palette.Panel, col, 0.15f), 1.6f); UIKit.Stretch(inner.rectTransform, 6);
        var rar = UIKit.Text(card.transform, "Rarity", Gear.RarityNames[item.rarity].ToUpperInvariant(), true, 26, col, TextAlignmentOptions.Center);
        UIKit.At(rar.rectTransform, 10, -16, 360, 36); rar.characterSpacing = 6;
        var icon = UIKit.Icon(card.transform, "Icon", Gear.Icon(item.id)); UIKit.At(icon.rectTransform, 90, -58, 200, 200);
        var d = Gear.Get(item.id);
        var nm = UIKit.Text(card.transform, "Name", d != null ? d.name : item.id, true, 30, Palette.Text, TextAlignmentOptions.Center);
        UIKit.At(nm.rectTransform, 10, -264, 360, 40);
        string sub = d != null && !string.IsNullOrEmpty(d.classLock) ? $"{d.slot}  ·  {Gear.LockLabel(d)}" : d != null ? d.slot.ToString() : "";
        var sl = UIKit.Text(card.transform, "Slot", sub, false, 19, Palette.TextMuted, TextAlignmentOptions.Center);
        UIKit.At(sl.rectTransform, 10, -304, 360, 28);
        var st = UIKit.Text(card.transform, "Stats", Gear.Describe(item), true, 21, Palette.Text, TextAlignmentOptions.Center);
        UIKit.At(st.rectTransform, 10, -338, 360, 70);
        card.rectTransform.localScale = Vector3.zero;

        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.3f;
            float k = Mathf.Clamp01(t);
            chest.color = new Color(1, 1, 1, 1f - k);
            chest.rectTransform.localScale = Vector3.one * (1f + k * 0.4f);
            card.rectTransform.localScale = Vector3.one * (k < 0.75f ? Mathf.Lerp(0.2f, 1.08f, k / 0.75f) : Mathf.Lerp(1.08f, 1f, (k - 0.75f) / 0.25f));
            glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.25f, k);
            yield return null;
        }

        // buttons
        bool canAgain = !free && SaveSystem.Data.gold >= c.gold && SaveSystem.Data.gems >= c.gems;
        var again = UIKit.Button(reveal, "Again", free ? "Free chest used" : $"Open another ({(c.gold > 0 ? c.gold + " gold" : c.gems + " gems")})", Palette.Accent, () =>
        {
            reveal.gameObject.SetActive(false); opening = false; Open(c, false);
        }, 22);
        again.interactable = canAgain;
        UIKit.Center((RectTransform)again.transform, new Vector2(-170, -250), new Vector2(320, 64));
        var ok = UIKit.Button(reveal, "Ok", "Nice!", Palette.Gold, () =>
        {
            reveal.gameObject.SetActive(false); opening = false; Refresh();
        }, 24);
        UIKit.Center((RectTransform)ok.transform, new Vector2(170, -250), new Vector2(320, 64));

        while (opening && my == runId && glow != null)
        {
            glow.rectTransform.Rotate(0, 0, 25f * Time.unscaledDeltaTime);
            yield return null;
        }
    }
}
