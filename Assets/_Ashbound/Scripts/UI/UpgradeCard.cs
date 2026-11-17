using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class UpgradeOption
{
    public string key, name, tag, levelLine, effect;
    public Color color = Color.white;
    public Sprite icon;
    public int fromLevel, toLevel;   // toLevel 6 = Awakening
    [NonSerialized] public Action apply;
    public bool IsAwakening => toLevel == 6;
}

public class UpgradeCard : MonoBehaviour
{
    [SerializeField] Image frame, icon, tagBg, goldRing, glow;
    [SerializeField] TMP_Text tagText, nameText, levelText, effectText;
    [SerializeField] Image[] pips = new Image[6];
    [SerializeField] Button button;
    Sprite defaultIcon;

    /// <summary>Card color by upgrade type: weapons red, passives blue, Path skills orange, evolutions/awakenings gold.</summary>
    public static Color TypeColor(string tag, bool special)
    {
        if (special || tag == "EVOLUTION" || tag == "AWAKENING") return Palette.Gold;
        if (tag != null && tag.Contains("PASSIVE")) return Palette.XPGem;
        if (tag == "PATH SKILL") return Palette.Accent;
        return Palette.Health;
    }

    public void Setup(UpgradeOption o, Action<UpgradeOption> onPick)
    {
        bool awake = o.IsAwakening;
        tagText.text = o.tag;
        nameText.text = o.name;
        levelText.text = o.levelLine;
        effectText.text = o.effect;
        if (defaultIcon == null) defaultIcon = icon.sprite;
        icon.sprite = o.icon != null ? o.icon : defaultIcon;
        icon.color = o.icon != null ? Color.white : o.color;
        icon.preserveAspect = true;
        icon.rectTransform.localScale = Vector3.one * (o.icon != null ? 1.45f : 1f);

        var type = TypeColor(o.tag, awake);
        frame.color = awake ? Palette.AwakenBg : Palette.Panel;
        tagBg.color = type;
        tagText.color = Palette.Outline;
        goldRing.enabled = true;
        goldRing.color = awake ? Palette.Gold : new Color(type.r, type.g, type.b, 0.9f);
        glow.enabled = awake;

        for (int i = 0; i < pips.Length; i++)
        {
            int lv = i + 1;
            pips[i].color = lv <= o.fromLevel ? Palette.Accent
                          : lv == o.toLevel ? (awake ? Palette.Gold : Color.white)
                          : Palette.Rock;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onPick(o));
    }
}
