using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunMode { Normal, Daily, Endless }

/// <summary>Ascension A1-A10: each level adds one curse (cumulative) and +10% gold/gems.</summary>
public static class Ascension
{
    public const int Max = 10;
    public static readonly string[] Curses =
    {
        "Enemies have +15% health",
        "Enemies move 10% faster",
        "Elites appear every 35s",
        "Healing is 30% weaker",
        "Enemies hit 15% harder",
        "Bosses have +30% health",
        "1 fewer reroll",
        "Enemies spawn 20% faster",
        "Enemies have another +20% health",
        "Fewer drops and 10% less XP",
    };

    public static int Level => RunSelection.Mode == RunMode.Daily ? 0 : Mathf.Clamp(RunSelection.Ascension, 0, Max);
    static bool On(int lv) => Level >= lv || RunSelection.DailyCurse(lv);

    public static float HpMul => (On(1) ? 1.15f : 1f) * (On(9) ? 1.2f : 1f);
    public static float SpeedMul => On(2) ? 1.1f : 1f;
    public static float EliteEvery => On(3) ? 35f : 50f;
    public static float HealMul => On(4) ? 0.7f : 1f;
    public static float DamageMul => On(5) ? 1.15f : 1f;
    public static float BossHpMul => On(6) ? 1.3f : 1f;
    public static int RerollPenalty => On(7) ? 1 : 0;
    public static float SpawnMul => On(8) ? 1.2f : 1f;
    public static float DropMul => On(10) ? 0.5f : 1f;
    public static float XpMul => On(10) ? 0.9f : 1f;
    public static float RewardMul => 1f + 0.1f * Level;

    public static string Describe(int level)
    {
        if (level <= 0) return "No curses. Win a stage to unlock Ascension 1.";
        var lines = new List<string>();
        for (int i = 0; i < level && i < Curses.Length; i++) lines.Add($"A{i + 1}: {Curses[i]}");
        return string.Join("\n", lines) + $"\n<color=#FFD95A>Rewards +{level * 10}% gold and gems</color>";
    }
}

/// <summary>Hero levels 1-20 earned across runs; every 4 levels pick 1 of 2 talents.</summary>
public static class HeroLevels
{
    public const int MaxLevel = 20;
    public static int XpFor(int level) => 80 + 40 * level;   // XP to go from level to level+1

    public class Talent { public string name; public GearStat stat; public float value; }
    // tier (0-4, unlocked at levels 4/8/12/16/20) x choice (0/1)
    public static readonly Talent[,] Table =
    {
        { new() { name = "Battle Hardened: +10% damage", stat = GearStat.Damage, value = 0.10f }, new() { name = "Wide Reach: +12% area", stat = GearStat.Area, value = 0.12f } },
        { new() { name = "Thick Skin: +25 max HP", stat = GearStat.Health, value = 25f }, new() { name = "Quick Hands: -8% cooldowns", stat = GearStat.Cooldown, value = 0.08f } },
        { new() { name = "Inner Power: +30% hero mechanic", stat = GearStat.Mechanic, value = 0.3f }, new() { name = "Treasure Sense: +30% pickup, +10% gold", stat = GearStat.Pickup, value = 0.3f } },
        { new() { name = "Veteran: +12% damage", stat = GearStat.Damage, value = 0.12f }, new() { name = "Fleet Foot: +10% move speed", stat = GearStat.Speed, value = 0.10f } },
        { new() { name = "Mastery: +50% hero mechanic", stat = GearStat.Mechanic, value = 0.5f }, new() { name = "Iron Will: -8% damage taken", stat = GearStat.Armor, value = 0.08f } },
    };

    static int Index(string cls)
    {
        var d = SaveSystem.Data;
        int i = d.heroNames.IndexOf(cls);
        if (i < 0) { d.heroNames.Add(cls); d.heroXp.Add(0); i = d.heroNames.Count - 1; }
        return i;
    }

    public static int TotalXp(string cls) => SaveSystem.Data.heroXp[Index(cls)];

    public static (int level, int into, int need) Level(string cls)
    {
        int xp = TotalXp(cls), lv = 1;
        while (lv < MaxLevel && xp >= XpFor(lv)) { xp -= XpFor(lv); lv++; }
        return (lv, xp, lv >= MaxLevel ? 0 : XpFor(lv));
    }

    public static int AddXp(string cls, int amount)
    {
        int before = Level(cls).level;
        SaveSystem.Data.heroXp[Index(cls)] += Mathf.Max(0, amount);
        return Level(cls).level - before;
    }

    public static int Pick(string cls, int tier)
    {
        string key = cls + ":" + tier;
        foreach (var k in SaveSystem.Data.talentPicks) if (k.StartsWith(key + "=")) return int.Parse(k.Substring(key.Length + 1));
        return -1;
    }

    public static bool TierOpen(string cls, int tier) => Level(cls).level >= (tier + 1) * 4;

    public static void Choose(string cls, int tier, int choice)
    {
        if (!TierOpen(cls, tier) || Pick(cls, tier) >= 0) return;
        SaveSystem.Data.talentPicks.Add($"{cls}:{tier}={choice}");
        SaveSystem.Save();
    }

    public static int UnspentPicks(string cls)
    {
        int n = 0;
        for (int t = 0; t < 5; t++) if (TierOpen(cls, t) && Pick(cls, t) < 0) n++;
        return n;
    }

    /// <summary>Applies chosen talents; returns (gold bonus, mechanic boost).</summary>
    public static (float gold, float mechanic) ApplyTo(PlayerStats s, string cls)
    {
        float gold = 0f, mech = 0f;
        for (int t = 0; t < 5; t++)
        {
            int p = Pick(cls, t);
            if (p < 0) continue;
            var tal = Table[t, p];
            switch (tal.stat)
            {
                case GearStat.Damage: s.damageMul += tal.value; break;
                case GearStat.Area: s.areaMul += tal.value; break;
                case GearStat.Health: s.AddMaxHealth(tal.value); break;
                case GearStat.Cooldown: s.cooldownMul *= 1f - tal.value; break;
                case GearStat.Mechanic: mech += tal.value; break;
                case GearStat.Pickup: s.pickupMul += tal.value; gold += 0.1f; break;
                case GearStat.Speed: s.moveMul += tal.value; break;
                case GearStat.Armor: s.damageReduction = Mathf.Min(0.5f, s.damageReduction + tal.value); break;
            }
        }
        return (gold, mech);
    }

    /// <summary>XP a hero earns from a run.</summary>
    public static int RunXp(int kills, float seconds, int bosses, bool won) => kills / 10 + Mathf.RoundToInt(seconds / 6f) + bosses * 25 + (won ? 100 : 0);
}

/// <summary>Lifetime stats, achievements (claim for rewards) and 3 daily quests.</summary>
public static class Achievements
{
    public class Def { public string id, name, description, stat; public float target; public int gems, gold; }

    public static readonly Def[] All =
    {
        new() { id = "kills1",   name = "First Blood",        description = "Defeat 1,000 Hollow",            stat = "kills", target = 1000, gems = 5, gold = 50 },
        new() { id = "kills2",   name = "Hollow Hunter",      description = "Defeat 25,000 Hollow",           stat = "kills", target = 25000, gems = 15, gold = 200 },
        new() { id = "kills3",   name = "Ashbringer",         description = "Defeat 250,000 Hollow",          stat = "kills", target = 250000, gems = 40, gold = 800 },
        new() { id = "boss1",    name = "Giant Slayer",       description = "Defeat 5 bosses",                stat = "bosses", target = 5, gems = 10, gold = 100 },
        new() { id = "boss2",    name = "Kingbreaker",        description = "Defeat 30 bosses",               stat = "bosses", target = 30, gems = 30, gold = 400 },
        new() { id = "elite1",   name = "Gold Rush",          description = "Defeat 25 golden elites",        stat = "elites", target = 25, gems = 10, gold = 150 },
        new() { id = "goblin1",  name = "Pickpocket",         description = "Catch 5 treasure goblins",       stat = "goblins", target = 5, gems = 15, gold = 100 },
        new() { id = "props1",   name = "Vandal",             description = "Break 100 crates, braziers or statues", stat = "props", target = 100, gems = 10, gold = 100 },
        new() { id = "win1",     name = "Crown Bearer",       description = "Win a run",                      stat = "wins", target = 1, gems = 10, gold = 100 },
        new() { id = "win2",     name = "Champion",           description = "Win 10 runs",                    stat = "wins", target = 10, gems = 30, gold = 400 },
        new() { id = "streak1",  name = "Rampage",            description = "Reach a 500 kill streak",        stat = "streak", target = 500, gems = 10, gold = 100 },
        new() { id = "streak2",  name = "Unstoppable Force",  description = "Reach a 2,000 kill streak",      stat = "streak", target = 2000, gems = 25, gold = 300 },
        new() { id = "evo1",     name = "Evolution",          description = "Evolve a weapon 5 times",        stat = "evolutions", target = 5, gems = 10, gold = 100 },
        new() { id = "syn1",     name = "Combo Artist",       description = "Turn on 20 synergies",           stat = "synergies", target = 20, gems = 10, gold = 100 },
        new() { id = "asc3",     name = "Rising Heat",        description = "Win at Ascension 3",             stat = "ascension", target = 3, gems = 20, gold = 200 },
        new() { id = "asc10",    name = "Ashbound",           description = "Win at Ascension 10",            stat = "ascension", target = 10, gems = 100, gold = 2000 },
        new() { id = "endless",  name = "Endless Night",      description = "Survive 20:00 in Endless mode",  stat = "endless", target = 1200, gems = 25, gold = 300 },
        new() { id = "daily",    name = "Daily Devotion",     description = "Finish 7 daily challenges",      stat = "dailies", target = 7, gems = 25, gold = 200 },
        new() { id = "hero10",   name = "Seasoned",           description = "Reach hero level 10",            stat = "herolevel", target = 10, gems = 15, gold = 150 },
        new() { id = "gear1",    name = "Collector",          description = "Own an Epic or better item",     stat = "epic", target = 1, gems = 15, gold = 100 },
    };

    // stats that keep the best value instead of adding up
    static readonly HashSet<string> MaxStats = new() { "streak", "ascension", "endless", "herolevel", "epic" };

    /// <summary>Older saves: count what was already done before achievements existed.</summary>
    static void Migrate()
    {
        var d = SaveSystem.Data;
        if (d.statKeys.Contains("_v1")) return;
        d.statKeys.Add("_v1"); d.statValues.Add(1f);
        Seed(d, "kills", d.totalKills); Seed(d, "runs", d.runsPlayed); Seed(d, "wins", d.stagesWon.Count);
    }

    static void Seed(SaveData d, string key, float value)
    {
        int i = d.statKeys.IndexOf(key);
        if (i < 0) { d.statKeys.Add(key); d.statValues.Add(value); }
        else d.statValues[i] = Mathf.Max(d.statValues[i], value);
    }

    public static float Get(string key)
    {
        Migrate();
        var d = SaveSystem.Data;
        int i = d.statKeys.IndexOf(key);
        return i < 0 ? 0f : d.statValues[i];
    }

    public static void Add(string key, float amount)
    {
        Migrate();
        var d = SaveSystem.Data;
        int i = d.statKeys.IndexOf(key);
        if (i < 0) { d.statKeys.Add(key); d.statValues.Add(0f); i = d.statKeys.Count - 1; }
        d.statValues[i] = MaxStats.Contains(key) ? Mathf.Max(d.statValues[i], amount) : d.statValues[i] + amount;
    }

    public static bool Done(Def a) => Get(a.stat) >= a.target;
    public static bool Claimed(Def a) => SaveSystem.Data.achievementsClaimed.Contains(a.id);

    public static void Claim(Def a)
    {
        if (!Done(a) || Claimed(a)) return;
        var d = SaveSystem.Data;
        d.achievementsClaimed.Add(a.id);
        d.gems += a.gems; d.gold += a.gold;
        SaveSystem.Save();
    }

    public static int Unclaimed() { int n = 0; foreach (var a in All) if (Done(a) && !Claimed(a)) n++; n += Quests.Unclaimed(); return n; }
}

/// <summary>3 daily quests, progress measured from the stats at the start of the day.</summary>
public static class Quests
{
    public class Def { public string id, text, stat; public float target; public int gems; }
    static readonly Def[] Pool =
    {
        new() { id = "q_kills", text = "Defeat 2,000 Hollow", stat = "kills", target = 2000, gems = 6 },
        new() { id = "q_kills2", text = "Defeat 5,000 Hollow", stat = "kills", target = 5000, gems = 10 },
        new() { id = "q_elites", text = "Defeat 4 golden elites", stat = "elites", target = 4, gems = 8 },
        new() { id = "q_bosses", text = "Defeat 2 bosses", stat = "bosses", target = 2, gems = 8 },
        new() { id = "q_props", text = "Break 15 props", stat = "props", target = 15, gems = 5 },
        new() { id = "q_goblin", text = "Catch a treasure goblin", stat = "goblins", target = 1, gems = 8 },
        new() { id = "q_time", text = "Survive 20 minutes in total", stat = "time", target = 1200, gems = 6 },
        new() { id = "q_syn", text = "Turn on 3 synergies", stat = "synergies", target = 3, gems = 6 },
        new() { id = "q_runs", text = "Play 3 runs", stat = "runs", target = 3, gems = 5 },
    };

    static string Today => DateTime.UtcNow.ToString("yyyyMMdd");

    static void Roll()
    {
        var d = SaveSystem.Data;
        if (d.questDate == Today && d.questIds.Count == 3) return;
        d.questDate = Today;
        d.questIds.Clear(); d.questBase.Clear(); d.questClaimed.Clear();
        var rng = new System.Random(int.Parse(Today));
        var picks = new List<Def>(Pool);
        for (int i = 0; i < 3; i++)
        {
            var q = picks[rng.Next(picks.Count)];
            picks.RemoveAll(x => x.stat == q.stat);
            d.questIds.Add(q.id); d.questBase.Add(Achievements.Get(q.stat));
        }
        SaveSystem.Save();
    }

    public static List<(Def def, float progress, bool claimed)> Today3()
    {
        Roll();
        var d = SaveSystem.Data;
        var list = new List<(Def, float, bool)>();
        for (int i = 0; i < d.questIds.Count; i++)
        {
            var q = Array.Find(Pool, x => x.id == d.questIds[i]);
            if (q == null) continue;
            list.Add((q, Achievements.Get(q.stat) - d.questBase[i], d.questClaimed.Contains(q.id)));
        }
        return list;
    }

    public static void Claim(Def q)
    {
        var d = SaveSystem.Data;
        int i = d.questIds.IndexOf(q.id);
        if (i < 0 || d.questClaimed.Contains(q.id) || Achievements.Get(q.stat) - d.questBase[i] < q.target) return;
        d.questClaimed.Add(q.id);
        d.gems += q.gems;
        SaveSystem.Save();
    }

    public static int Unclaimed() { int n = 0; foreach (var (q, p, c) in Today3()) if (!c && p >= q.target) n++; return n; }
}

/// <summary>Daily challenge: same hero, Path, stage and 2 curses for everyone that day; one attempt; gems by time survived.</summary>
public static class Daily
{
    public static string Today => DateTime.UtcNow.ToString("yyyyMMdd");
    public static bool PlayedToday => SaveSystem.Data.dailyDate == Today;

    public static (ClassData cls, PathData path, StageData stage, int curseA, int curseB) Pick(List<ClassData> classes, List<StageData> stages)
    {
        var rng = new System.Random(int.Parse(Today) * 7 + 13);
        var c = classes[rng.Next(classes.Count)];
        var p = c.paths[rng.Next(c.paths.Count)];
        var s = stages[rng.Next(stages.Count)];
        int a = rng.Next(1, 11), b;
        do b = rng.Next(1, 11); while (b == a);
        return (c, p, s, a, b);
    }

    public static int Reward(float seconds) => 10 + Mathf.RoundToInt(50f * Mathf.Clamp01(seconds / 900f));
}
