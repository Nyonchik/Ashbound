using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Every unlock in the game and how to get it: heroes, stages, Paths, crown shards.</summary>
public class UnlocksPanel : MonoBehaviour
{
    [SerializeField] List<ClassData> classes = new();
    [SerializeField] List<StageData> stages = new();
    [SerializeField] TMP_Text[] columns;
    [SerializeField] Button closeButton;
    [SerializeField] MainMenu menu;
    bool wired;

    const string Open = "<color=#4CC38A>Unlocked</color>";
    const string Locked = "<color=#F08A3C>Locked</color>";

    void OnEnable()
    {
        if (!wired)
        {
            wired = true;
            closeButton.onClick.AddListener(() => { if (menu) menu.ClosePanels(); else gameObject.SetActive(false); });
        }
        Build();
    }

    void Build()
    {
        var d = SaveSystem.Data;

        var a = new StringBuilder();
        a.Append("<size=120%><b>HEROES</b></size>\n");
        foreach (var c in classes)
        {
            bool open = SaveSystem.IsClassUnlocked(c);
            a.Append($"<b>{c.className}</b>  {(open ? Open : Locked)}\n");
            if (!open && !string.IsNullOrEmpty(c.unlockHint)) a.Append($"<color=#B9B1C2>{c.unlockHint}</color>\n");
        }
        a.Append("\n<size=120%><b>STAGES</b></size>\n");
        var sorted = new List<StageData>(stages);
        sorted.Sort((x, y) => x.order.CompareTo(y.order));
        for (int i = 0; i < sorted.Count; i++)
        {
            var s = sorted[i];
            bool open = SaveSystem.IsStageUnlocked(s);
            bool won = d.stagesWon.Contains(s.stageName);
            a.Append($"<b>{s.stageName}</b>  {(open ? Open : Locked)}{(won ? "  <color=#FFD95A>crown shard</color>" : "")}\n");
            if (!open && i > 0) a.Append($"<color=#B9B1C2>Survive 10:00 on {sorted[i - 1].stageName}</color>\n");
        }
        a.Append($"\n<size=120%><b>CROWN SHARDS  {d.stagesWon.Count} / 3</b></size>\n");
        a.Append("<color=#B9B1C2>Win a run on a stage (survive 15:00 and beat every boss) to recover its shard.</color>\n");
        a.Append($"\n<b>x2 speed</b>  {(d.runsPlayed >= 1 ? Open : Locked)}\n<color=#B9B1C2>Finish your first run.</color>");

        var b = new StringBuilder();
        var c2 = new StringBuilder();
        for (int i = 0; i < classes.Count; i++) AppendPaths(i < 3 ? b : c2, classes[i]);

        if (columns.Length > 0) columns[0].text = a.ToString();
        if (columns.Length > 1) columns[1].text = b.ToString();
        if (columns.Length > 2) columns[2].text = c2.ToString() + "\n<color=#B9B1C2>Each time you survive 10:00 with a hero, their next locked Path opens (top to bottom).</color>";
    }

    static void AppendPaths(StringBuilder sb, ClassData c)
    {
        sb.Append($"<size=120%><b>{c.className.ToUpperInvariant()} PATHS</b></size>\n");
        foreach (var p in c.paths)
        {
            bool open = SaveSystem.IsPathUnlocked(c, p);
            sb.Append(open ? $"<color=#4CC38A>•</color> {p.pathName}\n" : $"<color=#5C5866>•</color> <color=#8E8799>{p.pathName}</color>  <color=#F08A3C>survive 10:00</color>\n");
        }
        sb.Append('\n');
    }
}
