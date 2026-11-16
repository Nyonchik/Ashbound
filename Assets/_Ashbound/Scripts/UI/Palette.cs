using UnityEngine;

/// <summary>Ashbound colors, matching the Art Bible and UI designs.</summary>
public static class Palette
{
    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    public static readonly Color Void      = Hex("#1C1B22");
    public static readonly Color Ground    = Hex("#2A2833");
    public static readonly Color Rock      = Hex("#3A3744");
    public static readonly Color Outline   = Hex("#14131A");
    public static readonly Color Panel     = Hex("#24222C");
    public static readonly Color PanelHi   = Hex("#2E2B38");
    public static readonly Color Accent    = Hex("#F08A3C");
    public static readonly Color Text      = Hex("#EDE6DA");
    public static readonly Color TextMuted = Hex("#B9B1C2");
    public static readonly Color Health    = Hex("#E5484D");
    public static readonly Color Gold      = Hex("#FFD95A");
    public static readonly Color Ember     = Hex("#FF8A3D");
    public static readonly Color Hollow    = Hex("#6E7568");
    public static readonly Color XPGem     = Hex("#3DA5F4");
    public static readonly Color AwakenBg  = Hex("#2B2620");
}
