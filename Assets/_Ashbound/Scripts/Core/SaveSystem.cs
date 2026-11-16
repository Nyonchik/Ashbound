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
