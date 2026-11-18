using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Editor menu: copy the player's save (PlayerPrefs JSON) to Logs/save_backup.json and put it back.</summary>
public static class AshboundSaveTools
{
    const string Key = "ashbound.save.v1";
    static string BackupPath => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "save_backup.json");

    [MenuItem("Ashbound/Save/Back Up Save", priority = 40)]
    public static void Backup()
    {
        string json = PlayerPrefs.GetString(Key, "");
        File.WriteAllText(BackupPath, json);
        Debug.Log($"[Ashbound] Save backed up ({json.Length} chars) to {BackupPath}");
    }

    [MenuItem("Ashbound/Save/Restore Save", priority = 41)]
    public static void Restore()
    {
        if (!File.Exists(BackupPath)) { Debug.LogWarning("[Ashbound] No save backup found"); return; }
        string json = File.ReadAllText(BackupPath);
        PlayerPrefs.SetString(Key, json);
        PlayerPrefs.Save();
        SaveSystem.Load();
        Debug.Log($"[Ashbound] Save restored ({json.Length} chars)");
    }
}
