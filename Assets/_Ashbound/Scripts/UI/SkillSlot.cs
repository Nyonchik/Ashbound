using UnityEngine;
using UnityEngine.UI;

public class SkillSlot : MonoBehaviour
{
    [SerializeField] Image frame, icon;
    [SerializeField] Image[] pips = new Image[5];
    Sprite defaultIcon;

    /// <summary>level 0 = empty slot. awakened = gold frame.</summary>
    public void Set(Sprite skillIcon, Color color, int level, bool awakened = false)
    {
        if (defaultIcon == null) defaultIcon = icon.sprite;
        icon.enabled = level > 0;
        icon.sprite = skillIcon != null ? skillIcon : defaultIcon;
        icon.color = skillIcon != null ? Color.white : color;
        icon.rectTransform.localScale = Vector3.one * (skillIcon != null ? 1.6f : 1f);
        for (int i = 0; i < pips.Length; i++)
            pips[i].color = i < level ? Palette.Accent : Palette.Rock;
        frame.color = awakened ? Palette.Gold : Palette.Panel;
    }
}
