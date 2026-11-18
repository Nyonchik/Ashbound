using System;
using System.Collections.Generic;
using UnityEngine;

public enum GearSlot { Weapon, Helmet, Armor, Gloves, Boots, Amulet }
public enum GearStat { Damage, Health, Armor, Cooldown, Speed, Area, Pickup, Gold, Mechanic }

/// <summary>One piece of gear in the shared bag.</summary>
[Serializable]
public class GearItem
{
    public int uid;
    public string id;
    public int rarity;      // 0 Common .. 4 Legendary
    public int level = 1;
    public string equippedBy = "";   // class name, empty if in the bag
}

/// <summary>
/// Equipment: item definitions, rarities, stats, leveling, merging, selling, chests, and applying gear to a run.
/// Gear is account-wide (one bag), each hero wears their own 6 slots. Weapons are class-locked.
/// </summary>
public static class Gear
{
    public class Def
    {
        public string id, name, classLock;   // empty = every hero; otherwise comma-separated class names
        public GearSlot slot;
        public GearStat main, second;
        public float mainMax, secondMax;      // value at Legendary Lv 50
    }

    static Def D(string id, string name, GearSlot slot, GearStat main, float mainMax, GearStat second = GearStat.Damage, float secondMax = 0f, string cls = null)
        => new() { id = id, name = name, slot = slot, main = main, mainMax = mainMax, second = second, secondMax = secondMax, classLock = cls };

    public static readonly Def[] All =
    {
        D("staff",     "Ember Staff",       GearSlot.Weapon, GearStat.Damage, 0.50f, GearStat.Area, 0.15f, "Mage"),
        D("greataxe",  "Bloodied Greataxe", GearSlot.Weapon, GearStat.Damage, 0.50f, GearStat.Health, 60f, "Berserker"),
        D("longbow",   "Ashwood Longbow",   GearSlot.Weapon, GearStat.Damage, 0.50f, GearStat.Cooldown, 0.09f, "Archer"),
        D("daggers",   "Twin Fangs",        GearSlot.Weapon, GearStat.Damage, 0.50f, GearStat.Speed, 0.10f, "Assassin"),
        D("mace",      "Bulwark Mace",      GearSlot.Weapon, GearStat.Damage, 0.42f, GearStat.Armor, 0.08f, "Tank (Knight)"),
        // one item per slot for every hero, one for certain heroes, plus one hero-only piece that boosts that hero's mechanic
        D("helm",      "Iron Helm",         GearSlot.Helmet, GearStat.Armor, 0.15f),
        D("circlet",   "Seer's Circlet",    GearSlot.Helmet, GearStat.Armor, 0.09f, GearStat.Pickup, 0.45f, "Mage,Archer"),
        D("plate",     "Ashen Plate",       GearSlot.Armor,  GearStat.Health, 150f),
        D("cloak",     "Shadow Cloak",      GearSlot.Armor,  GearStat.Health, 90f, GearStat.Speed, 0.09f, "Assassin,Archer"),
        D("gauntlets", "Swift Gauntlets",   GearSlot.Gloves, GearStat.Cooldown, 0.15f),
        D("wraps",     "Brawler Wraps",     GearSlot.Gloves, GearStat.Cooldown, 0.09f, GearStat.Damage, 0.15f, "Berserker,Tank (Knight)"),
        D("striders",  "Wind Striders",     GearSlot.Boots,  GearStat.Speed, 0.15f),
        D("treads",    "Ember Treads",      GearSlot.Boots,  GearStat.Speed, 0.09f, GearStat.Area, 0.12f, "Mage,Berserker"),
        D("charm",     "Lucky Charm",       GearSlot.Amulet, GearStat.Area, 0.13f, GearStat.Gold, 0.35f),
        D("amulet",    "Arcane Amulet",     GearSlot.Amulet, GearStat.Area, 0.25f, GearStat.Damage, 0f, "Mage"),
        D("archhood",  "Archmage Hood",     GearSlot.Helmet, GearStat.Mechanic, 0.8f, GearStat.Area, 0.12f, "Mage"),
        D("harness",   "Bloodrage Harness", GearSlot.Armor,  GearStat.Mechanic, 0.8f, GearStat.Health, 70f, "Berserker"),
        D("bracers",   "Hawkeye Bracers",   GearSlot.Gloves, GearStat.Mechanic, 0.8f, GearStat.Cooldown, 0.08f, "Archer"),
        D("shadowboots","Shadowstep Boots", GearSlot.Boots,  GearStat.Mechanic, 0.8f, GearStat.Speed, 0.08f, "Assassin"),
        D("bastion",   "Bastion Amulet",    GearSlot.Amulet, GearStat.Mechanic, 0.8f, GearStat.Armor, 0.06f, "Tank (Knight)"),
    };

    public static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
    public static readonly Color[] RarityColors =
    {
        Hex("#B9B1C2"), Hex("#4CC38A"), Hex("#3DA5F4"), Hex("#A970FF"), Hex("#FFB338"),
    };
    static readonly int[] LevelCap = { 10, 20, 30, 40, 50 };
    // share of max at Lv 1 per rarity; leveling to the rarity's cap multiplies it by 1.8 (Legendary Lv 50 = 100%)
    static readonly float[] RarityPower = { 0.12f, 0.18f, 0.26f, 0.38f, 0.556f };
    static readonly int[] SellBase = { 15, 40, 100, 250, 600 };
    public const int MaxRarity = 4;

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    /// <summary>Can this hero wear this item? Empty lock = everyone.</summary>
    public static bool Allows(Def d, string className)
    {
        if (string.IsNullOrEmpty(d.classLock)) return true;
        foreach (var c in d.classLock.Split(',')) if (c.Trim() == className) return true;
        return false;
    }

    /// <summary>"Mage, Archer only" style label, empty for everyone.</summary>
    public static string LockLabel(Def d) => string.IsNullOrEmpty(d.classLock) ? "" : d.classLock.Replace(",", ", ").Replace("Tank (Knight)", "Tank") + " only";

    public static Def Get(string id) { foreach (var d in All) if (d.id == id) return d; return null; }
    public static int Cap(GearItem it) => LevelCap[Mathf.Clamp(it.rarity, 0, MaxRarity)];

    static readonly Dictionary<string, Sprite> icons = new();
    public static Sprite Icon(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (!icons.TryGetValue(id, out var s)) { s = Resources.Load<Sprite>("Gear/" + id); icons[id] = s; }
        return s;
    }

    // ---------------- stats

    /// <summary>0..1 share of the item's max values: rarity sets the base, levels add up to x1.8 by the rarity's cap.</summary>
    public static float Power(GearItem it)
    {
        int r = Mathf.Clamp(it.rarity, 0, MaxRarity);
        float k = (Mathf.Clamp(it.level, 1, LevelCap[r]) - 1f) / (LevelCap[r] - 1f);
        return Mathf.Min(1f, RarityPower[r] * (1f + 0.8f * k));
    }

    public static float Main(GearItem it) { var d = Get(it.id); return d == null ? 0f : d.mainMax * Power(it); }
    public static float Second(GearItem it) { var d = Get(it.id); return d == null ? 0f : d.secondMax * Power(it); }

    public static string StatText(GearStat s, float v) => s switch
    {
        GearStat.Health => $"+{Mathf.RoundToInt(v)} max HP",
        GearStat.Armor => $"-{v * 100f:0.#}% damage taken",
        GearStat.Cooldown => $"-{v * 100f:0.#}% cooldowns",
        GearStat.Speed => $"+{v * 100f:0.#}% move speed",
        GearStat.Area => $"+{v * 100f:0.#}% weapon area",
        GearStat.Pickup => $"+{v * 100f:0.#}% pickup range",
        GearStat.Gold => $"+{v * 100f:0.#}% gold",
        GearStat.Mechanic => $"+{v * 100f:0.#}% hero mechanic power",
        _ => $"+{v * 100f:0.#}% damage",
    };

    public static string Describe(GearItem it)
    {
        var d = Get(it.id);
        if (d == null) return "";
        string s = StatText(d.main, Main(it));
        if (d.secondMax > 0f) s += "\n" + StatText(d.second, Second(it));
        return s;
    }

    /// <summary>Total bonuses a hero's worn gear gives, per stat.</summary>
    public static Dictionary<GearStat, float> Totals(string className)
    {
        var t = new Dictionary<GearStat, float>();
        foreach (var it in SaveSystem.Data.gear)
        {
            if (it.equippedBy != className) continue;
            var d = Get(it.id); if (d == null || !CanWear(it, className)) continue;
            t[d.main] = (t.TryGetValue(d.main, out var a) ? a : 0f) + Main(it);
            if (d.secondMax > 0f) t[d.second] = (t.TryGetValue(d.second, out var b) ? b : 0f) + Second(it);
        }
        return t;
    }

    /// <summary>Applies the hero's gear at the start of a run. Returns the gold bonus.</summary>
    public static float ApplyTo(PlayerStats s, string className)
    {
        float gold = 0f;
        foreach (var kv in Totals(className))
        {
            switch (kv.Key)
            {
                case GearStat.Damage: s.damageMul += kv.Value; break;
                case GearStat.Health: s.AddMaxHealth(Mathf.Round(kv.Value)); break;
                case GearStat.Armor: s.damageReduction = Mathf.Min(0.5f, s.damageReduction + kv.Value); break;
                case GearStat.Cooldown: s.cooldownMul *= 1f - Mathf.Min(0.3f, kv.Value); break;
                case GearStat.Speed: s.moveMul += kv.Value; break;
                case GearStat.Area: s.areaMul += kv.Value; break;
                case GearStat.Pickup: s.pickupMul += kv.Value; break;
                case GearStat.Gold: gold += kv.Value; break;
                case GearStat.Mechanic: break;   // read by HeroMechanic through MechanicBoost
            }
        }
        return gold;
    }

    public static float MechanicBoost(string className) => Totals(className).TryGetValue(GearStat.Mechanic, out var m) ? m : 0f;

    public static float GoldBonus(string className) => Totals(className).TryGetValue(GearStat.Gold, out var g) ? g : 0f;

    // ---------------- bag actions

    public static GearItem Find(int uid) => SaveSystem.Data.gear.Find(g => g.uid == uid);
    public static GearItem Worn(string className, GearSlot slot) =>
        SaveSystem.Data.gear.Find(g => g.equippedBy == className && Get(g.id) != null && Get(g.id).slot == slot);

    public static bool CanWear(GearItem it, string className)
    {
        var d = Get(it.id);
        return d != null && Allows(d, className);
    }

    public static void Equip(GearItem it, string className)
    {
        if (!CanWear(it, className)) return;
        var old = Worn(className, Get(it.id).slot);
        if (old != null) old.equippedBy = "";
        it.equippedBy = className;
        SaveSystem.Save();
    }

    public static void Unequip(GearItem it) { it.equippedBy = ""; SaveSystem.Save(); }

    public static int LevelCost(GearItem it) => Mathf.RoundToInt((20 + 8 * it.level) * (1f + 0.3f * it.rarity));

    public static bool LevelUp(GearItem it)
    {
        if (it.level >= Cap(it)) return false;
        int cost = LevelCost(it);
        if (SaveSystem.Data.gold < cost) return false;
        SaveSystem.Data.gold -= cost;
        it.level++;
        SaveSystem.Save();
        return true;
    }

    /// <summary>Other copies (same item, same rarity) that can be merged with this one.</summary>
    public static List<GearItem> MergeMates(GearItem it)
    {
        var list = new List<GearItem>();
        if (it.rarity >= MaxRarity) return list;
        foreach (var g in SaveSystem.Data.gear)
            if (g != it && g.id == it.id && g.rarity == it.rarity) list.Add(g);
        // prefer copies nobody is wearing
        list.Sort((a, b) => (a.equippedBy == "" ? 0 : 1).CompareTo(b.equippedBy == "" ? 0 : 1));
        return list;
    }

    /// <summary>Merge 3 copies into one of the next rarity. Keeps the highest level and who wears this one.</summary>
    public static bool Merge(GearItem it)
    {
        var mates = MergeMates(it);
        if (mates.Count < 2) return false;
        int lvl = Mathf.Max(it.level, Mathf.Max(mates[0].level, mates[1].level));
        SaveSystem.Data.gear.Remove(mates[0]);
        SaveSystem.Data.gear.Remove(mates[1]);
        it.rarity++;
        it.level = Mathf.Min(lvl, Cap(it));
        SaveSystem.Save();
        return true;
    }

    public static int SellValue(GearItem it)
    {
        int spent = 0;
        var tmp = new GearItem { rarity = it.rarity };
        for (int l = 1; l < it.level; l++) { tmp.level = l; spent += LevelCost(tmp); }
        return SellBase[Mathf.Clamp(it.rarity, 0, MaxRarity)] + Mathf.RoundToInt(spent * 0.4f);
    }

    public static void Sell(GearItem it)
    {
        SaveSystem.Data.gold += SellValue(it);
        SaveSystem.Data.gear.Remove(it);
        SaveSystem.Save();
    }

    public static GearItem Add(string id, int rarity)
    {
        var d = SaveSystem.Data;
        var it = new GearItem { uid = d.nextGearId++, id = id, rarity = Mathf.Clamp(rarity, 0, MaxRarity), level = 1 };
        d.gear.Add(it);
        return it;
    }

    // ---------------- chests

    public class Chest
    {
        public string id, name;
        public int gold, gems;
        public float[] odds;   // per rarity
        public string blurb;
    }

    public static readonly Chest[] Chests =
    {
        new() { id = "chest_wood",   name = "Wooden Chest", gold = 200, odds = new[] { 0.70f, 0.25f, 0.05f, 0f, 0f },  blurb = "Common 70%\nUncommon 25%\nRare 5%" },
        new() { id = "chest_silver", name = "Silver Chest", gems = 40,  odds = new[] { 0f, 0.55f, 0.35f, 0.09f, 0.01f }, blurb = "Uncommon 55%\nRare 35%\nEpic 9%\nLegendary 1%" },
        new() { id = "chest_royal",  name = "Royal Chest",  gems = 120, odds = new[] { 0f, 0f, 0.30f, 0.60f, 0.10f },   blurb = "Rare 30%\nEpic 60%\nLegendary 10%" },
    };

    public const double FreeChestHours = 20;

    public static TimeSpan FreeChestWait()
    {
        var last = DateTime.FromBinary(SaveSystem.Data.lastFreeChest);
        var ready = last.AddHours(FreeChestHours);
        var now = DateTime.UtcNow;
        return SaveSystem.Data.lastFreeChest == 0 || now >= ready ? TimeSpan.Zero : ready - now;
    }

    /// <summary>Pays for and opens a chest. free = the daily free Wooden Chest. Returns null if it can't be afforded.</summary>
    public static GearItem Open(Chest c, bool free, List<string> unlockedClasses)
    {
        var d = SaveSystem.Data;
        if (free)
        {
            if (FreeChestWait() > TimeSpan.Zero) return null;
            d.lastFreeChest = DateTime.UtcNow.ToBinary();
        }
        else
        {
            if (d.gold < c.gold || d.gems < c.gems) return null;
            d.gold -= c.gold; d.gems -= c.gems;
        }

        float r = UnityEngine.Random.value, acc = 0f; int rarity = 0;
        for (int i = 0; i < c.odds.Length; i++) { acc += c.odds[i]; if (r <= acc) { rarity = i; break; } rarity = i; }
        while (rarity < MaxRarity && c.odds[rarity] <= 0f) rarity++;

        var item = Grant(rarity, unlockedClasses);
        SaveSystem.Save();
        return item;
    }

    /// <summary>Adds a random item of this rarity to the bag: a slot evenly, then an item in it (weapons only for heroes you have).</summary>
    public static GearItem Grant(int rarity, List<string> unlockedClasses)
    {
        var slot = (GearSlot)UnityEngine.Random.Range(0, 6);
        var pool = new List<Def>();
        foreach (var def in All)
            if (def.slot == slot && (unlockedClasses == null || unlockedClasses.Exists(c => Allows(def, c))))
                pool.Add(def);
        if (pool.Count == 0) foreach (var def in All) if (def.slot != GearSlot.Weapon) pool.Add(def);
        return Add(pool[UnityEngine.Random.Range(0, pool.Count)].id, rarity);
    }
}
