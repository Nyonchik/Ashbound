using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Small helpers for menus that build their contents at runtime (gear, store).</summary>
public static class UIKit
{
    public static TMP_FontAsset Font, Bold;
    public static Sprite Panel, Fill;

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>Anchor + pivot at the top-left of the parent; pos is the top-left corner (y down is negative).</summary>
    public static RectTransform At(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    public static RectTransform Center(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.one * inset; rt.offsetMax = -Vector2.one * inset;
    }

    public static Image Img(Transform parent, string name, Sprite s, Color c, float ppu = 1.6f, bool raycast = false)
    {
        var img = Rect(name, parent).gameObject.AddComponent<Image>();
        img.sprite = s; img.color = c; img.raycastTarget = raycast;
        if (s != null && s.border != Vector4.zero) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = ppu; }
        return img;
    }

    public static Image Icon(Transform parent, string name, Sprite s)
    {
        var img = Rect(name, parent).gameObject.AddComponent<Image>();
        img.sprite = s; img.preserveAspect = true; img.raycastTarget = false; img.enabled = s != null;
        return img;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string text, bool bold, float size, Color c,
                                       TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
    {
        var t = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = bold ? Bold : Font; t.text = text; t.fontSize = size; t.color = c; t.alignment = align;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    public static Button Button(Transform parent, string name, string label, Color bg, Action onClick, float fontSize = 24f)
    {
        var img = Img(parent, name, Panel, bg, 1.6f, true);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors; cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f); cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f); b.colors = cb;
        var t = Text(img.transform, "Label", label, true, fontSize, Palette.Outline, TextAlignmentOptions.Center);
        Stretch(t.rectTransform, 4);
        t.textWrappingMode = TextWrappingModes.NoWrap; t.enableAutoSizing = true; t.fontSizeMin = 12; t.fontSizeMax = fontSize;
        if (onClick != null) b.onClick.AddListener(() => { AudioManager.Play("click", 0.6f); onClick(); });
        return b;
    }

    public static void SetLabel(Button b, string label) { var t = b.GetComponentInChildren<TextMeshProUGUI>(); if (t) t.text = label; }

    public static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject); }
}
