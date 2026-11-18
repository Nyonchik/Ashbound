using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>First-run tutorial: a few guided steps during the first real run. Skippable; can be replayed from Settings.</summary>
public class Tutorial : MonoBehaviour
{
    RunManager run;
    EnemySpawner spawner;
    RectTransform panel;
    TMP_Text text, stepText;
    int step = -1;
    float timer;
    Vector2 startPos;
    int killsAtStep;
    string mechanicLine;

    public static bool ShouldRun => !SaveSystem.Data.tutorialDone && RunSelection.Mode == RunMode.Normal;

    public void Setup(RunManager manager, EnemySpawner sp, Transform canvas, TMP_FontAsset font, string mechanic)
    {
        run = manager; spawner = sp; mechanicLine = mechanic;
        panel = UIKit.Rect("Tutorial", canvas);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 1f); panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -150f); panel.sizeDelta = new Vector2(980f, 128f);
        var bg = panel.gameObject.AddComponent<Image>(); bg.color = new Color(0.08f, 0.07f, 0.1f, 0.88f); bg.raycastTarget = false;
        var edge = UIKit.Rect("Edge", panel); UIKit.Stretch(edge); edge.offsetMin = new Vector2(0, 0); edge.offsetMax = new Vector2(0, -122);
        edge.anchorMin = new Vector2(0, 1); edge.anchorMax = new Vector2(1, 1); edge.sizeDelta = new Vector2(0, 6); edge.anchoredPosition = Vector2.zero; edge.pivot = new Vector2(0.5f, 1f);
        edge.gameObject.AddComponent<Image>().color = Palette.Accent;

        stepText = MakeText("Step", font, 18, Palette.Accent);
        Place(stepText.rectTransform, 24, -14, 600, 26);
        text = MakeText("Text", font, 27, Palette.Text);
        Place(text.rectTransform, 24, -40, 790, 84);

        // Skip button
        var skipRt = UIKit.Rect("Skip", panel);
        skipRt.anchorMin = skipRt.anchorMax = skipRt.pivot = new Vector2(1f, 0.5f);
        skipRt.anchoredPosition = new Vector2(-18f, -4f); skipRt.sizeDelta = new Vector2(140f, 52f);
        var skipImg = skipRt.gameObject.AddComponent<Image>(); skipImg.color = Palette.Rock;
        var skip = skipRt.gameObject.AddComponent<Button>(); skip.targetGraphic = skipImg;
        var sl = MakeText("Label", font, 22, Palette.Text, skipRt);
        UIKit.Stretch(sl.rectTransform); sl.alignment = TextAlignmentOptions.Center; sl.text = "Skip";
        skip.onClick.AddListener(() => { AudioManager.Play("click", 0.6f); Finish(); });

        startPos = PlayerController.Instance != null ? (Vector2)PlayerController.Instance.transform.position : Vector2.zero;
        Next();
    }

    TMP_Text MakeText(string name, TMP_FontAsset font, float size, Color c, Transform parent = null)
    {
        var t = UIKit.Rect(name, parent != null ? parent : panel).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSize = size; t.color = c; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
    }

    void Next()
    {
        step++; timer = 0f; killsAtStep = run.Kills;
        AudioManager.Play("chime", 0.5f);
        string move = $"{Controls.KeyName(Controls.Get(Controls.Action.Up))}{Controls.KeyName(Controls.Get(Controls.Action.Left))}{Controls.KeyName(Controls.Get(Controls.Action.Down))}{Controls.KeyName(Controls.Get(Controls.Action.Right))}";
        string[] lines =
        {
            $"Move with <color=#FFD95A>{move.ToUpperInvariant()}</color> or the arrow keys. Walk around a little.",
            "Your skill attacks on its own. You only move! <color=#FFD95A>Defeat 15 Hollow.</color>",
            "Hollow drop <color=#3DA5F4>blue XP gems</color>. Walk over them to fill the XP bar and <color=#FFD95A>level up</color>.",
            "Nice! Every level you pick a card: a new skill, a passive, or a level for one you have. Max a skill and its passive to <color=#FFD95A>evolve</color> it.",
            mechanicLine,
            $"Golden elites, chests, map events and <color=#E5484D>bosses at 5:00, 10:00 and 15:00</color> are coming. Press <color=#FFD95A>{Controls.KeyName(Controls.Get(Controls.Action.Pause))}</color> to pause. Good luck!",
        };
        if (step >= lines.Length) { Finish(); return; }
        stepText.text = $"TUTORIAL  {step + 1} / {lines.Length}";
        text.text = lines[step];
    }

    void Update()
    {
        if (run == null || step < 0) return;
        if (run.IsOver) { Finish(); return; }
        // stay out of the way of the level-up cards and the pause menu
        bool paused = Time.timeScale <= 0.001f;
        if (panel.gameObject.activeSelf == paused) panel.gameObject.SetActive(!paused);
        if (paused) return;
        timer += Time.unscaledDeltaTime;
        // fewer enemies while you learn
        if (spawner != null) spawner.TutorialMul = step < 3 ? 0.6f : 1f;
        var p = PlayerController.Instance;
        bool done = step switch
        {
            0 => p != null && ((Vector2)p.transform.position - startPos).sqrMagnitude > 9f && timer > 1.5f,
            1 => run.Kills - killsAtStep >= 15,
            2 => run.Level >= 2,
            3 => timer > 7f && Time.timeScale > 0f,
            4 => timer > 8f || (timer > 1f && Controls.Pressed(Controls.Action.Dash) && RunSelection.Class != null && RunSelection.Class.className == "Assassin"),
            _ => timer > 8f,
        };
        if (done) Next();
    }

    void Finish()
    {
        if (spawner != null) spawner.TutorialMul = 1f;
        SaveSystem.Data.tutorialDone = true;
        SaveSystem.Save();
        if (panel != null) Destroy(panel.gameObject);
        Destroy(this);
    }
}
