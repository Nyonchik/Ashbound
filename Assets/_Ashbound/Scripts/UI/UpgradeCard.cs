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

        frame.color = awake ? Palette.AwakenBg : Palette.Panel;
        tagBg.color = awake ? Palette.Gold : Palette.Rock;
        tagText.color = awake ? Palette.Outline : Palette.Text;
        goldRing.enabled = awake;
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
