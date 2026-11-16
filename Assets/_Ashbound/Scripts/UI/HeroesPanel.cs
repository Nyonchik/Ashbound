using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows every hero and Path, what's unlocked, and how to unlock the rest.</summary>
public class HeroesPanel : MonoBehaviour
{
    [SerializeField] List<ClassData> classes = new();
    [SerializeField] TMP_Text[] columns;
    [SerializeField] TMP_Text summary;
    [SerializeField] Button closeButton;
    [SerializeField] MainMenu menu;

    bool wired;

    void OnEnable()
    {
        if (!wired)
        {
            wired = true;
            closeButton.onClick.AddListener(() => { if (menu) menu.ClosePanels(); else gameObject.SetActive(false); });
        }
        for (int i = 0; i < columns.Length; i++)
        {
            if (i >= classes.Count) { columns[i].text = ""; continue; }
            var c = classes[i];
            bool open = SaveSystem.IsClassUnlocked(c);
            var sb = new StringBuilder();
            sb.Append($"<size=130%><color=#{ColorUtility.ToHtmlStringRGB(c.color)}>{c.className}</color></size>\n");
            sb.Append(open ? "<color=#4CC38A>Unlocked</color>\n\n" : $"<color=#F08A3C>Locked</color>\n<size=85%>{c.unlockHint}</size>\n\n");
            foreach (var p in c.paths)
            {
                bool pOpen = SaveSystem.IsPathUnlocked(c, p);
                string hex = ColorUtility.ToHtmlStringRGB(p.color);
                sb.Append(pOpen ? $"<color=#{hex}>•</color> {p.pathName}\n" : $"<color=#5A566A>•</color> <color=#8C8699>{p.pathName}</color>\n");
            }
            if (open) sb.Append($"\n<size=80%><color=#8C8699>Survive 10:00 as {c.className} to unlock the next Path</color></size>");
            columns[i].text = sb.ToString();
        }
        var d = SaveSystem.Data;
        if (summary) summary.text = $"Runs played: {d.runsPlayed}   ·   Total kills: {d.totalKills:N0}   ·   Crown shards: {d.stagesWon.Count} / 3";
    }
}
