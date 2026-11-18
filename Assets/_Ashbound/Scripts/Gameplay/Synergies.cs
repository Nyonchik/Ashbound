using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Automatic combos: owning skills of matching elements turns on a bonus.
/// Elements come from WeaponDef.element (Fire, Frost, Lightning, Poison, Shadow, Holy, Blood, Earth, Arcane, Physical).
/// Hooks: Modify (before damage) and After (after damage) are called by Enemy.TakeDamage for weapon hits.
/// </summary>
public static class Synergies
{
    public class Def
    {
        public string id, name, description;
        public Color color;
        public Func<Dictionary<string, int>, bool> needs;
    }

    static bool Has(Dictionary<string, int> c, string e, int n = 1) => c.TryGetValue(e, out var k) && k >= n;

    public static readonly Def[] All =
    {
        new() { id = "steam",    name = "Steam Burst",     color = new Color(0.9f, 0.9f, 0.95f), description = "Fire hits on slowed enemies explode in steam", needs = c => Has(c, "Fire") && Has(c, "Frost") },
        new() { id = "supercon", name = "Superconductor",  color = new Color(0.5f, 0.9f, 1f),    description = "Lightning deals +60% to slowed enemies",         needs = c => Has(c, "Lightning") && Has(c, "Frost") },
        new() { id = "shatter",  name = "Shatterstone",    color = new Color(0.75f, 0.65f, 0.5f), description = "Earth deals +50% to slowed enemies",            needs = c => Has(c, "Earth") && Has(c, "Frost") },
        new() { id = "resonance",name = "Arcane Resonance",color = new Color(0.7f, 0.5f, 1f),    description = "Arcane hits sometimes burst on nearby enemies",   needs = c => Has(c, "Arcane", 2) },
        new() { id = "void",     name = "Void Rift",       color = new Color(0.55f, 0.3f, 0.85f), description = "Arcane kills tear open a small shadow blast",    needs = c => Has(c, "Arcane") && Has(c, "Shadow") },
        new() { id = "venom",    name = "Venom Edge",      color = new Color(0.45f, 1f, 0.4f),   description = "Physical hits also poison",                       needs = c => Has(c, "Poison") && Has(c, "Physical") },
        new() { id = "molten",   name = "Molten Edge",     color = new Color(1f, 0.55f, 0.25f),  description = "Physical and Arcane hits set enemies burning",    needs = c => Has(c, "Fire") && (Has(c, "Physical") || Has(c, "Arcane")) },
        new() { id = "bleed",    name = "Bloodletting",    color = new Color(0.9f, 0.2f, 0.25f), description = "Physical hits deal +25% to wounded enemies",      needs = c => Has(c, "Blood") && Has(c, "Physical") },
        new() { id = "steel",    name = "Sharpened Steel", color = new Color(0.85f, 0.85f, 0.9f), description = "Two Physical skills: Physical hits +15%",        needs = c => Has(c, "Physical", 2) },
        new() { id = "charged",  name = "Charged Blades",  color = new Color(0.6f, 0.85f, 1f),   description = "Physical hits sometimes arc lightning",           needs = c => Has(c, "Lightning") && Has(c, "Physical") },
        new() { id = "judgment", name = "Judgment",        color = new Color(1f, 0.9f, 0.5f),    description = "Holy hits curse enemies (+25% damage taken)",     needs = c => Has(c, "Holy") && (Has(c, "Arcane") || Has(c, "Physical") || Has(c, "Frost")) },
        new() { id = "dread",    name = "Dread Frost",     color = new Color(0.6f, 0.75f, 1f),   description = "Shadow hits on slowed enemies deal +40%",         needs = c => Has(c, "Shadow") && Has(c, "Frost") },
    };

    static readonly HashSet<string> active = new();
    public static IEnumerable<string> ActiveIds => active;
    public static bool IsActive(string id) => active.Contains(id);
    static bool busy;   // no chain reactions from synergy damage itself
    static readonly List<Enemy> buffer = new();

    static Dictionary<string, int> Count(IEnumerable<WeaponDef> defs)
    {
        var c = new Dictionary<string, int>();
        foreach (var d in defs)
            if (d != null && !string.IsNullOrEmpty(d.element)) c[d.element] = (c.TryGetValue(d.element, out var k) ? k : 0) + 1;
        return c;
    }

    /// <summary>Recompute active combos. Returns the ones that just turned on.</summary>
    public static List<Def> Refresh(IEnumerable<WeaponDef> owned)
    {
        var c = Count(owned);
        var fresh = new List<Def>();
        foreach (var s in All)
        {
            bool on = s.needs(c);
            if (on && active.Add(s.id)) fresh.Add(s);
            else if (!on) active.Remove(s.id);
        }
        return fresh;
    }

    public static void Clear() => active.Clear();

    /// <summary>Combos that taking this weapon would newly unlock (for level-up cards).</summary>
    public static List<Def> WouldUnlock(IEnumerable<WeaponDef> owned, WeaponDef extra)
    {
        var list = new List<WeaponDef>(owned) { extra };
        var c = Count(list);
        var res = new List<Def>();
        foreach (var s in All) if (!active.Contains(s.id) && s.needs(c)) res.Add(s);
        return res;
    }

    public static Def Get(string id) { foreach (var s in All) if (s.id == id) return s; return null; }

    public static float Modify(Weapon src, Enemy e, float amount)
    {
        if (busy || src == null || active.Count == 0) return amount;
        string el = src.Element;
        if (e.IsSlowed)
        {
            if (el == "Lightning" && active.Contains("supercon")) amount *= 1.6f;
            if (el == "Earth" && active.Contains("shatter")) amount *= 1.5f;
            if (el == "Shadow" && active.Contains("dread")) amount *= 1.4f;
        }
        if (el == "Physical")
        {
            if (active.Contains("steel")) amount *= 1.15f;
            if (active.Contains("bleed") && e.HealthFraction < 0.5f) amount *= 1.25f;
        }
        return amount;
    }

    public static void After(Weapon src, Enemy e, float amount, bool killed)
    {
        if (busy || src == null || active.Count == 0 || e == null) return;
        string el = src.Element;
        Vector2 p = e.transform.position;
        busy = true;
        try
        {
            if (el == "Fire" && e.IsSlowed && active.Contains("steam") && Random.value < 0.5f)
            {
                Burst(p, 1.4f, amount * 0.5f, new Color(0.9f, 0.92f, 1f, 0.6f));
                FxParticles.Smoke(p, new Color(0.9f, 0.92f, 0.95f, 0.5f), 4, 0.4f, 1f, 0.6f, 1.5f);
            }
            if (el == "Arcane" && active.Contains("resonance") && Random.value < 0.12f) Burst(p, 1.2f, amount * 0.8f, new Color(0.7f, 0.5f, 1f, 0.7f));
            if (el == "Arcane" && killed && active.Contains("void")) Burst(p, 1.2f, amount * 0.5f, new Color(0.35f, 0.15f, 0.55f, 0.8f));
            if (!killed)
            {
                if (el == "Physical" && active.Contains("venom")) e.Poison(amount * 0.3f, 2.5f);
                if ((el == "Physical" || el == "Arcane") && active.Contains("molten")) { e.Poison(amount * 0.25f, 2f); if (Random.value < 0.3f) FxParticles.Sparks(p, new Color(1f, 0.55f, 0.2f), 2, 2f, 0.14f, 0.3f); }
                if (el == "Holy" && active.Contains("judgment")) e.Curse(2f);
            }
            if (el == "Physical" && active.Contains("charged") && Random.value < 0.12f)
            {
                var n = Enemy.Nearest(p, 3f);
                if (n != null && n != e)
                {
                    var prev = Enemy.Source; Enemy.Source = src;
                    n.TakeDamage(amount * 0.5f, Vector2.zero, 0f);
                    Enemy.Source = prev;
                    FxParticles.Line(p, n.transform.position, new Color(0.6f, 0.85f, 1f), 5f, 0.14f, 0.5f, 0.2f);
                }
            }
        }
        finally { busy = false; }

        void Burst(Vector2 c, float r, float dmg, Color col)
        {
            Enemy.CollectInRadius(c, r, buffer);
            var prev = Enemy.Source; Enemy.Source = src;
            foreach (var o in buffer) if (o != e) o.TakeDamage(dmg, ((Vector2)o.transform.position - c).normalized, 1f);
            Enemy.Source = prev;
            FxParticles.Glow(c, col, r * 2.2f, 0.25f);
            FxParticles.Sparks(c, col, 6, 3f, 0.15f, 0.3f);
        }
    }
}
