using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shop for permanent upgrades. Rows are built at runtime from MetaUpgrades.All.</summary>
public class UpgradesPanel : MonoBehaviour
{
    [SerializeField] RectTransform list;
    [SerializeField] TMP_Text goldText;
    [SerializeField] Button closeButton;
    [SerializeField] MainMenu menu;
    [SerializeField] TMP_FontAsset font, boldFont;
    [SerializeField] Sprite panelSprite, fillSprite;
    [SerializeField] List<Sprite> icons = new();

    class Row { public MetaUpgrades.Def def; public Image[] pips; public TMP_Text price; public Button buy; }
    readonly List<Row> rows = new();

    void OnEnable()
    {
        if (rows.Count == 0) Build();
        Refresh();
    }

    void Build()
    {
        closeButton.onClick.AddListener(() => { if (menu) menu.ClosePanels(); else gameObject.SetActive(false); });
        for (int i = 0; i < MetaUpgrades.All.Length; i++)
        {
            var def = MetaUpgrades.All[i];
            var row = new GameObject(def.name, typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(list, false);
            row.GetComponent<LayoutElement>().preferredHeight = 96;
            var bg = row.GetComponent<Image>(); bg.sprite = panelSprite; bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1.6f; bg.color = Palette.PanelHi;

            var icon = UI<Image>("Icon", row, new Vector2(0, 0.5f), new Vector2(54, 0), new Vector2(64, 64));
            icon.sprite = i < icons.Count ? icons[i] : null; icon.preserveAspect = true; icon.enabled = icon.sprite != null;

            var name = UI<TextMeshProUGUI>("Name", row, new Vector2(0, 0.5f), new Vector2(260, 16), new Vector2(300, 36));
            name.font = boldFont; name.fontSize = 28; name.color = Palette.Text; name.text = def.name; name.alignment = TextAlignmentOptions.MidlineLeft;
            var desc = UI<TextMeshProUGUI>("Desc", row, new Vector2(0, 0.5f), new Vector2(260, -18), new Vector2(300, 30));
            desc.font = font; desc.fontSize = 20; desc.color = Palette.TextMuted; desc.text = def.description; desc.alignment = TextAlignmentOptions.MidlineLeft;

            var pips = new Image[def.maxLevel];
            for (int p = 0; p < def.maxLevel; p++)
            {
                pips[p] = UI<Image>("Pip", row, new Vector2(0, 0.5f), new Vector2(450 + p * 30, 0), new Vector2(22, 12));
                pips[p].sprite = fillSprite; pips[p].type = Image.Type.Sliced; pips[p].pixelsPerUnitMultiplier = 6f;
            }

            var buyImg = UI<Image>("Buy", row, new Vector2(1, 0.5f), new Vector2(-110, 0), new Vector2(190, 64));
            buyImg.sprite = panelSprite; buyImg.type = Image.Type.Sliced; buyImg.pixelsPerUnitMultiplier = 1.6f; buyImg.color = Palette.Accent;
            buyImg.raycastTarget = true;   // the button needs a clickable graphic
            var buy = buyImg.gameObject.AddComponent<Button>(); buy.targetGraphic = buyImg;
            var price = UI<TextMeshProUGUI>("Price", buyImg.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180, 60));
            price.font = boldFont; price.fontSize = 24; price.color = Palette.Outline; price.alignment = TextAlignmentOptions.Center;

            var r = new Row { def = def, pips = pips, price = price, buy = buy };
            buy.onClick.AddListener(() => Buy(r));
            rows.Add(r);
        }
        AudioManager.HookButtons();
    }

    static T UI<T>(string name, RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size) where T : Graphic
    {
        var rt = new GameObject(name, typeof(RectTransform), typeof(T)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var g = rt.GetComponent<T>();
        g.raycastTarget = false;
        return g;
    }

    void Buy(Row r)
    {
        int lv = SaveSystem.UpgradeLevel(r.def.id);
        if (lv >= r.def.maxLevel) return;
        int cost = r.def.Cost(lv);
        if (SaveSystem.Data.gold < cost) return;
        SaveSystem.Data.gold -= cost;
        SaveSystem.SetUpgradeLevel(r.def.id, lv + 1);
        SaveSystem.Save();
        AudioManager.Play("coin", 1f);
        Refresh();
        if (menu) menu.Refresh();
    }

    void Refresh()
    {
        int gold = SaveSystem.Data.gold;
        goldText.text = $"{gold:N0} gold";
        foreach (var r in rows)
        {
            int lv = SaveSystem.UpgradeLevel(r.def.id);
            for (int p = 0; p < r.pips.Length; p++) r.pips[p].color = p < lv ? Palette.Accent : Palette.Rock;
            bool maxed = lv >= r.def.maxLevel;
            int cost = r.def.Cost(lv);
            r.price.text = maxed ? "MAX" : $"{cost} gold";
            r.buy.interactable = !maxed && gold >= cost;
        }
    }
}
