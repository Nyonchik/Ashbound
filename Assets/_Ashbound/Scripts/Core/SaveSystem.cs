using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    public int gold;
    public int runsPlayed;
    public int totalKills;
    public List<string> unlockedPaths = new();
    public List<string> unlockedClasses = new();
    public List<string> unlockedStages = new();
    public List<string> stagesWon = new();
    public List<string> upgradeIds = new();
    public List<int> upgradeLevels = new();
    public float musicVolume = 0.5f;
    public float sfxVolume = 0.8f;
    public bool fullscreen = true;
    public int resolutionIndex = -1;
    public bool showDamageNumbers = true;
    public bool screenShake = true;
    public List<string> seenTips = new();
    // equipment + store
    public List<GearItem> gear = new();
    public int nextGearId = 1;
    public int gems = 40;            // existing saves start with 40 gems too
    public long lastFreeChest;
    // Phase 3: ascension, hero levels, achievements, quests, daily, endless
    public int ascensionUnlocked;          // highest Ascension level you may pick
    public int ascensionSelected;
    public List<string> heroNames = new();
    public List<int> heroXp = new();
    public List<string> talentPicks = new();   // "Mage:0=1"
    public List<string> statKeys = new();
    public List<float> statValues = new();
    public List<string> achievementsClaimed = new();
    public string questDate = "";
    public List<string> questIds = new();
    public List<float> questBase = new();
    public List<string> questClaimed = new();
    public string dailyDate = "";
    public float dailyBest;                // seconds survived in today's daily
    public int dailyGems;
    public float endlessBest;
    // Phase 4: settings + tutorial
    public List<string> keyBinds = new();   // Controls.Action order; empty = defaults
    public float shakeStrength = 1f;        // 0 off .. 1 full
    public bool colorblindNumbers;
    public float hudScale = 1f;
    public bool tutorialDone;
}

/// <summary>Progress and settings, stored as JSON in PlayerPrefs.</summary>
public static class SaveSystem
{
    const string Key = "ashbound.save.v1";
    static SaveData data;

    public static SaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    public static void Load()
    {
        string json = PlayerPrefs.GetString(Key, "");
        data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
        data ??= new SaveData();
    }

    public static void Save()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }

    public static void ResetProgress()
    {
        var keep = Data;
        data = new SaveData { musicVolume = keep.musicVolume, sfxVolume = keep.sfxVolume, fullscreen = keep.fullscreen, resolutionIndex = keep.resolutionIndex };
        Save();
    }

    public static string PathKey(ClassData c, PathData p) => c.className + "/" + p.pathName;

    public static bool IsClassUnlocked(ClassData c) => c != null && (c.unlocked || Data.unlockedClasses.Contains(c.className));

    public static bool IsPathUnlocked(ClassData c, PathData p) =>
        IsClassUnlocked(c) && p != null && (p.unlocked || Data.unlockedPaths.Contains(PathKey(c, p)));

    public static bool IsStageUnlocked(StageData s) => s != null && (s.order == 0 || Data.unlockedStages.Contains(s.stageName));

    public static int UpgradeLevel(string id)
    {
        int i = Data.upgradeIds.IndexOf(id);
        return i < 0 ? 0 : Data.upgradeLevels[i];
    }

    public static void SetUpgradeLevel(string id, int level)
    {
        int i = Data.upgradeIds.IndexOf(id);
        if (i < 0) { Data.upgradeIds.Add(id); Data.upgradeLevels.Add(level); }
        else Data.upgradeLevels[i] = level;
    }
}
