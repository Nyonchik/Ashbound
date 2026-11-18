using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Equipment screen: pick a hero, see their 6 slots, browse the shared bag, and equip / level up / merge / sell gear.
/// Everything inside "body" is built at runtime.
/// </summary>
public class GearPanel : MonoBehaviour
{
    [SerializeField] List<ClassData> classes = new();
    [SerializeField] RectTransform body;
    [SerializeField] Button closeButton;
    [SerializeField] MainMenu menu;
    [SerializeField] TMP_FontAsset font, boldFont;
    [SerializeField] Sprite panelSprite, fillSprite;

    ClassData hero;
    GearItem selected;
    GearSlot? filter;
    bool built;

    RectTransform heroTabs, slotsRoot, gridContent, detail;
    TMP_Text statsText, bagTitle, goldText;
    Image portrait;
    readonly List<(ClassData c, Button b)> tabs = new();

    static readonly string[] SlotNames = { "Weapon", "Helmet", "Armor", "Gloves", "Boots", "Amulet" };

    void OnEnable()
    {
        UIKit.Font = font; UIKit.Bold = boldFont; UIKit.Panel = panelSprite; UIKit.Fill = fillSprite;
        if (!built) Build();
        if (hero == null || !SaveSystem.IsClassUnlocked(hero)) hero = classes.Find(SaveSystem.IsClassUnlocked);
        selected = null; filter = null;
        Refresh();
    }

    void Build()
    {
        built = true;
        closeButton.onClick.AddListener(() => { if (menu) menu.ClosePanels(); else gameObject.SetActive(false); });

        // ---- left: hero tabs, portrait, slots, totals
        heroTabs = UIKit.At(UIKit.Rect("HeroTabs", body), 0, 0, 780, 60);
        foreach (var c in classes)
        {
            var cls = c;
            var b = UIKit.Button(heroTabs, c.className, ShortName(c), Palette.PanelHi, () => { hero = cls; selected = null; filter = null; Refresh(); }, 22);
            tabs.Add((c, b));
        }
        for (int i = 0; i < tabs.Count; i++) UIKit.At((RectTransform)tabs[i].b.transform, i * 156, 0, 148, 58);

        var stage = UIKit.Img(body, "HeroStage", fillSprite, Palette.Void, 1.5f);
        UIKit.At(stage.rectTransform, 0, -76, 780, 470);
        portrait = UIKit.Icon(stage.transform, "Portrait", null);
        UIKit.Center(portrait.rectTransform, new Vector2(0, -10), new Vector2(300, 360));
        slotsRoot = UIKit.Rect("Slots", stage.transform); UIKit.Stretch(slotsRoot);

        var statsBox = UIKit.Img(body, "Totals", fillSprite, Palette.Void, 1.5f);
        UIKit.At(statsBox.rectTransform, 0, -560, 780, 140);
        statsText = UIKit.Text(statsBox.transform, "Text", "", false, 20, Palette.Text);
        UIKit.Stretch(statsText.rectTransform, 16);
        statsText.enableAutoSizing = true; statsText.fontSizeMin = 13; statsText.fontSizeMax = 20;

        // ---- right: bag grid + detail
        const float rx = 810, rw = 814;
        bagTitle = UIKit.Text(body, "BagTitle", "", true, 24, Palette.Text, TextAlignmentOptions.MidlineLeft);
        UIKit.At(bagTitle.rectTransform, rx, 0, 470, 50);
        goldText = UIKit.Text(body, "Gold", "", true, 24, Palette.Gold, TextAlignmentOptions.MidlineRight);
        UIKit.At(goldText.rectTransform, rx + 470, 0, 200, 50);
        var all = UIKit.Button(body, "ShowAll", "Show all", Palette.PanelHi, () => { filter = null; Refresh(); }, 20);
        UIKit.At((RectTransform)all.transform, rx + rw - 130, -2, 130, 46);
        all.GetComponentInChildren<TMP_Text>().color = Palette.Text;

        var view = UIKit.Img(body, "BagView", fillSprite, Palette.Void, 1.5f, true);
        UIKit.At(view.rectTransform, rx, -58, rw, 372);
        view.gameObject.AddComponent<RectMask2D>();
        gridContent = UIKit.Rect("Content", view.transform);
        gridContent.anchorMin = gridContent.anchorMax = new Vector2(0, 1); gridContent.pivot = new Vector2(0, 1);
        gridContent.anchoredPosition = Vector2.zero; gridContent.sizeDelta = new Vector2(rw, 0);
        var grid = gridContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(112, 112); grid.spacing = new Vector2(12, 12); grid.padding = new RectOffset(14, 14, 14, 14);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 6;
        gridContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.content = gridContent; sr.viewport = view.rectTransform; sr.horizontal = false; sr.scrollSensitivity = 40f;
        sr.movementType = ScrollRect.MovementType.Clamped;

        var det = UIKit.Img(body, "Detail", fillSprite, Palette.PanelHi, 1.5f);
        UIKit.At(det.rectTransform, rx, -444, rw, 256);
        detail = det.rectTransform;
    }

    static string ShortName(ClassData c) => c.className.StartsWith("Tank") ? "Tank" : c.className;

    void Refresh()
    {
        if (menu) menu.Refresh();
        goldText.text = $"{SaveSystem.Data.gold:N0} gold";
        foreach (var (c, b) in tabs)
        {
            bool open = SaveSystem.IsClassUnlocked(c);
            b.interactable = open;
            ((Image)b.targetGraphic).color = c == hero ? c.color : Palette.PanelHi;
            b.GetComponentInChildren<TMP_Text>().color = c == hero ? Palette.Outline : open ? Palette.Text : Palette.TextMuted;
        }
        if (hero == null) return;
        portrait.sprite = hero.portrait; portrait.enabled = hero.portrait != null;
        BuildSlots();
        BuildTotals();
        BuildGrid();
        BuildDetail();
    }

    // ---------------- hero slots

    void BuildSlots()
    {
        UIKit.Clear(slotsRoot);
        for (int i = 0; i < 6; i++)
        {
            var slot = (GearSlot)i;
            float x = i < 3 ? 30 : 780 - 30 - 128;
            float y = -24 - (i % 3) * 146;
            var it = Gear.Worn(hero.className, slot);
            var cell = Cell(slotsRoot, it, SlotNames[i], () =>
            {
                filter = slot;
                selected = Gear.Worn(hero.className, slot);
                Refresh();
            });
            UIKit.At(cell, x, y, 128, 128);
            if (filter == slot) Outline(cell, Palette.Accent);
        }
    }

    void BuildTotals()
    {
        var t = Gear.Totals(hero.className);
        var parts = new List<string>();
        foreach (var kv in t) parts.Add(Gear.StatText(kv.Key, kv.Value));
        statsText.text = $"<b>{hero.className} gear bonus</b>\n" +
                         (parts.Count == 0 ? "<color=#B9B1C2>Nothing equipped yet. Open chests in the Store, then tap a slot or an item to equip it.</color>" : string.Join("   ·   ", parts));
    }

    // ---------------- bag

    void BuildGrid()
    {
        UIKit.Clear(gridContent);
        var items = new List<GearItem>(SaveSystem.Data.gear);
        if (filter != null) items.RemoveAll(g => Gear.Get(g.id) == null || Gear.Get(g.id).slot != filter.Value);
        items.Sort((a, b) =>
        {
            var da = Gear.Get(a.id); var db = Gear.Get(b.id);
            int c = (da != null ? (int)da.slot : 9).CompareTo(db != null ? (int)db.slot : 9);
            if (c != 0) return c;
            c = b.rarity.CompareTo(a.rarity); if (c != 0) return c;
            c = b.level.CompareTo(a.level); if (c != 0) return c;
            return string.CompareOrdinal(a.id, b.id);
        });
        bagTitle.text = filter == null ? $"Bag  ·  {SaveSystem.Data.gear.Count} items" : $"Bag  ·  {SlotNames[(int)filter.Value]} ({items.Count})";
        foreach (var it in items)
        {
            var item = it;
            var cell = Cell(gridContent, item, null, () => { selected = item; Refresh(); });
            if (item == selected) Outline(cell, Palette.Text);
            if (!string.IsNullOrEmpty(item.equippedBy))
            {
                var tag = UIKit.Img(cell, "Worn", fillSprite, Palette.Outline, 4f);
                UIKit.At(tag.rectTransform, 6, -6, 30, 26);
                var tt = UIKit.Text(tag.transform, "E", "E", true, 17, item.equippedBy == hero.className ? Palette.Accent : Palette.TextMuted, TextAlignmentOptions.Center);
                UIKit.Stretch(tt.rectTransform);
            }
            if (hero != null && !Gear.CanWear(item, hero.className)) cell.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;
        }
        if (items.Count == 0) bagTitle.text += "  ·  <color=#B9B1C2>empty - open chests in the Store</color>";
    }

    RectTransform Cell(Transform parent, GearItem it, string emptyLabel, System.Action onClick)
    {
        var bg = UIKit.Img(parent, it != null ? it.id : "Empty", panelSprite, Palette.Panel, 1.6f, true);
        var btn = bg.gameObject.AddComponent<Button>(); btn.targetGraphic = bg;
        btn.onClick.AddListener(() => { AudioManager.Play("click", 0.5f); onClick(); });
        var rt = bg.rectTransform;
        var ring = UIKit.Img(rt, "Rarity", panelSprite, it != null ? Gear.RarityColors[it.rarity] : Palette.Rock, 1.6f);
        UIKit.Stretch(ring.rectTransform, 3);
        var inner = UIKit.Img(rt, "Inner", panelSprite, it != null ? Color.Lerp(Palette.Void, Gear.RarityColors[it.rarity], 0.18f) : Palette.Void, 1.6f);
        UIKit.Stretch(inner.rectTransform, 7);
        if (it != null)
        {
            var icon = UIKit.Icon(rt, "Icon", Gear.Icon(it.id));
            UIKit.Stretch(icon.rectTransform, 14);
            if (icon.sprite == null)
            {
                var d = Gear.Get(it.id);
                var ph = UIKit.Text(rt, "Name", d != null ? d.name : it.id, true, 16, Palette.Text, TextAlignmentOptions.Center);
                UIKit.Stretch(ph.rectTransform, 12);
            }
            var lv = UIKit.Text(rt, "Level", $"Lv {it.level}", true, 17, Palette.Text, TextAlignmentOptions.BottomRight);
            UIKit.Stretch(lv.rectTransform, 10);
            lv.outlineWidth = 0.25f; lv.outlineColor = Palette.Outline;
        }
        else if (emptyLabel != null)
        {
            var l = UIKit.Text(rt, "Label", emptyLabel, true, 18, Palette.TextMuted, TextAlignmentOptions.Center);
            UIKit.Stretch(l.rectTransform, 8);
        }
        return rt;
    }

    void Outline(RectTransform cell, Color c)
    {
        var o = UIKit.Img(cell, "Selected", panelSprite, c, 1.6f);
        UIKit.Stretch(o.rectTransform, -5);
        o.transform.SetAsFirstSibling();
    }

    // ---------------- selected item

    void BuildDetail()
    {
        UIKit.Clear(detail);
        if (selected == null || !SaveSystem.Data.gear.Contains(selected))
        {
            selected = null;
            var hint = UIKit.Text(detail, "Hint", filter == null
                ? "Tap an item to see it.\nTap one of the hero's slots to show only that kind of gear.\n\nMerge 3 copies of the same item and rarity into the next rarity.\nLevel items up with gold, up to their rarity's cap."
                : $"Nothing in the {SlotNames[(int)filter.Value]} slot. Pick one from the bag above.", false, 20, Palette.TextMuted);
            UIKit.Stretch(hint.rectTransform, 22);
            return;
        }

        var it = selected;
        var d = Gear.Get(it.id);
        var box = UIKit.Img(detail, "IconBox", panelSprite, Gear.RarityColors[it.rarity], 1.6f);
        UIKit.At(box.rectTransform, 20, -20, 150, 150);
        var boxIn = UIKit.Img(box.transform, "In", panelSprite, Color.Lerp(Palette.Void, Gear.RarityColors[it.rarity], 0.18f), 1.6f);
        UIKit.Stretch(boxIn.rectTransform, 6);
        var icon = UIKit.Icon(box.transform, "Icon", Gear.Icon(it.id)); UIKit.Stretch(icon.rectTransform, 16);

        var name = UIKit.Text(detail, "Name", d != null ? d.name : it.id, true, 30, Palette.Text);
        UIKit.At(name.rectTransform, 190, -16, 600, 40);
        string lock_ = d != null && !string.IsNullOrEmpty(d.classLock) ? $"  ·  {d.classLock} only" : "";
        var rar = UIKit.Text(detail, "Rarity",
            $"<color=#{ColorUtility.ToHtmlStringRGB(Gear.RarityColors[it.rarity])}>{Gear.RarityNames[it.rarity]}</color>  ·  {SlotNames[(int)d.slot]}  ·  Lv {it.level} / {Gear.Cap(it)}{lock_}",
            false, 20, Palette.TextMuted);
        UIKit.At(rar.rectTransform, 190, -58, 610, 30);
        var stats = UIKit.Text(detail, "Stats", Gear.Describe(it), true, 22, Palette.Text);
        UIKit.At(stats.rectTransform, 190, -92, 400, 70);
        if (!string.IsNullOrEmpty(it.equippedBy))
        {
            var worn = UIKit.Text(detail, "Worn", $"Worn by {it.equippedBy}", false, 18, Palette.Accent);
            UIKit.At(worn.rectTransform, 590, -92, 210, 30);
        }

        // buttons
        float bx = 20, by = -186, bw = 186, bh = 52, gap = 12;
        bool mine = it.equippedBy == hero.className;
        bool canWear = Gear.CanWear(it, hero.className);
        var equip = UIKit.Button(detail, "Equip", mine ? "Unequip" : canWear ? $"Equip on {ShortName(hero)}" : "Can't wear", mine ? Palette.PanelHi : Palette.Accent, () =>
        {
            if (mine) Gear.Unequip(it); else { Gear.Equip(it, hero.className); AudioManager.Play("levelup", 0.5f); }
            Refresh();
        }, 20);
        equip.interactable = mine || canWear;
        if (mine) equip.GetComponentInChildren<TMP_Text>().color = Palette.Text;
        UIKit.At((RectTransform)equip.transform, bx, by, bw, bh);

        bool maxed = it.level >= Gear.Cap(it);
        int cost = Gear.LevelCost(it);
        var lvl = UIKit.Button(detail, "LevelUp", maxed ? "Max level" : $"Level up  {cost}g", Palette.Gold, () =>
        {
            if (Gear.LevelUp(it)) AudioManager.Play("coin", 0.8f);
            Refresh();
        }, 20);
        lvl.interactable = !maxed && SaveSystem.Data.gold >= cost;
        UIKit.At((RectTransform)lvl.transform, bx + (bw + gap), by, bw, bh);

        int mates = Gear.MergeMates(it).Count;
        bool canMerge = it.rarity < Gear.MaxRarity && mates >= 2;
        var merge = UIKit.Button(detail, "Merge",
            it.rarity >= Gear.MaxRarity ? "Top rarity" : $"Merge  {Mathf.Min(mates + 1, 3)}/3",
            Gear.RarityColors[Mathf.Min(it.rarity + 1, Gear.MaxRarity)], () =>
            {
                if (Gear.Merge(it)) AudioManager.Play("evolve", 0.8f);
                Refresh();
            }, 20);
        merge.interactable = canMerge;
        UIKit.At((RectTransform)merge.transform, bx + 2 * (bw + gap), by, bw, bh);

        var sell = UIKit.Button(detail, "Sell", $"Sell  +{Gear.SellValue(it)}g", Palette.PanelHi, () =>
        {
            Gear.Sell(it); selected = null; AudioManager.Play("coin", 0.7f); Refresh();
        }, 20);
        sell.GetComponentInChildren<TMP_Text>().color = Palette.Text;
        sell.interactable = string.IsNullOrEmpty(it.equippedBy);
        UIKit.At((RectTransform)sell.transform, bx + 3 * (bw + gap), by, bw, bh);
    }
}
