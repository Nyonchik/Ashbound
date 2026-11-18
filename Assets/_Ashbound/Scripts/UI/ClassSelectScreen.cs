using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ClassSelectScreen : MonoBehaviour
{
    [SerializeField] List<ClassData> classes = new();
    [SerializeField] List<StageData> stages = new();
    [SerializeField] Transform classList;
    [SerializeField] Button classButtonPrefab;
    [SerializeField] Transform pathGrid;
    [SerializeField] PathCard pathCardPrefab;
    [SerializeField] Image portrait;
    [SerializeField] TMP_Text nameText, roleText, mechanicText, weaponText, ascensionText, startLabel;
    [SerializeField] Button startButton, backButton;
    [Header("Stage select")]
    [SerializeField] Button stagePrev, stageNext;
    [SerializeField] TMP_Text stageName, stageInfo;
    [Header("Ascension (difficulty) + mode")]
    [SerializeField] Button ascPrev, ascNext;
    [SerializeField] TMP_Text ascName, ascInfo, titleText;

    ClassData currentClass;
    PathData currentPath;
    int stageIndex;
    readonly List<PathCard> cards = new();
    readonly List<(ClassData data, Image ring)> classButtons = new();

    void Start()
    {
        AudioManager.PlayMusic("music_menu");
        foreach (var c in classes)
        {
            var b = Instantiate(classButtonPrefab, classList);
            bool open = SaveSystem.IsClassUnlocked(c);
            b.GetComponentInChildren<TMP_Text>().text = open ? c.className : c.className + "  <size=70%><color=#B9B1C2>Locked</color></size>";
            var dot = b.transform.Find("Dot");
            if (dot) dot.GetComponent<Image>().color = c.color;
            var ring = b.transform.Find("Ring")?.GetComponent<Image>();
            if (ring) ring.color = c.color;
            classButtons.Add((c, ring));
            var captured = c;
            b.onClick.AddListener(() => SelectClass(captured));
        }
        startButton.onClick.AddListener(StartRun);
        if (backButton) backButton.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));

        stages.Sort((a, b) => a.order.CompareTo(b.order));
        stageIndex = Mathf.Max(0, stages.IndexOf(RunSelection.Stage));
        if (stagePrev) stagePrev.onClick.AddListener(() => ChangeStage(-1));
        if (stageNext) stageNext.onClick.AddListener(() => ChangeStage(1));
        ChangeStage(0);
        if (ascPrev) ascPrev.onClick.AddListener(() => ChangeAscension(-1));
        if (ascNext) ascNext.onClick.AddListener(() => ChangeAscension(1));
        ChangeAscension(0);
        if (titleText && RunSelection.Mode == RunMode.Endless) titleText.text = "Endless run";

        var first = RunSelection.Class != null && classes.Contains(RunSelection.Class) ? RunSelection.Class : (classes.Count > 0 ? classes[0] : null);
        if (first != null) SelectClass(first);
        AudioManager.HookButtons();
    }

    void ChangeStage(int delta)
    {
        if (stages.Count == 0) return;
        stageIndex = (stageIndex + delta + stages.Count) % stages.Count;
        var s = stages[stageIndex];
        bool open = SaveSystem.IsStageUnlocked(s);
        bool won = SaveSystem.Data.stagesWon.Contains(s.stageName);
        if (stageName) stageName.text = $"Stage {s.order + 1} · {s.stageName}" + (won ? "  <color=#FFD95A>(cleared)</color>" : "");
        if (stageInfo) stageInfo.text = open ? s.description : "Locked · survive 10:00 on the previous stage";
        UpdateStart();
    }

    void ChangeAscension(int delta)
    {
        var d = SaveSystem.Data;
        if (d.ascensionUnlocked == 0 && d.stagesWon.Count > 0) d.ascensionUnlocked = 1;   // older saves that already won
        int max = Mathf.Clamp(d.ascensionUnlocked, 0, Ascension.Max);
        d.ascensionSelected = Mathf.Clamp(d.ascensionSelected + delta, 0, max);
        int a = d.ascensionSelected;
        if (ascName) ascName.text = a == 0 ? "Ascension: off" : $"<color=#F08A3C>Ascension {a}</color>";
        if (ascInfo)
            ascInfo.text = a == 0
                ? (max == 0 ? "Win a stage to unlock Ascension 1" : $"Up to A{max} unlocked · harder, but more gold and gems")
                : $"{Ascension.Curses[a - 1]}{(a > 1 ? $" +{a - 1} more" : "")} · <color=#FFD95A>+{a * 10}% rewards</color>";
        if (ascPrev) ascPrev.interactable = a > 0;
        if (ascNext) ascNext.interactable = a < max;
        if (delta != 0) SaveSystem.Save();
    }

    void SelectClass(ClassData c)
    {
        currentClass = c;
        foreach (var (data, ring) in classButtons)
            if (ring) ring.enabled = data == c;

        nameText.text = c.className;
        nameText.color = c.color;
        roleText.text = SaveSystem.IsClassUnlocked(c) ? c.role : "Locked · " + c.unlockHint;
        mechanicText.text = c.mechanic;
        weaponText.text = c.defaultWeapon;
        if (c.portrait) { portrait.sprite = c.portrait; portrait.color = SaveSystem.IsClassUnlocked(c) ? Color.white : new Color(0.25f, 0.25f, 0.3f); }
        else portrait.color = c.color;

        foreach (var card in cards) Destroy(card.gameObject);
        cards.Clear();
        foreach (var p in c.paths)
        {
            var card = Instantiate(pathCardPrefab, pathGrid);
            card.Setup(p, SaveSystem.IsPathUnlocked(c, p), SaveSystem.IsClassUnlocked(c) ? $"survive 10:00 as {c.className}" : c.unlockHint, SelectPath);
            cards.Add(card);
        }
        AudioManager.HookButtons();
        if (cards.Count > 0) SelectPath(cards.Find(x => x.Unlocked) ?? cards[0]);
    }

    void SelectPath(PathCard picked)
    {
        foreach (var card in cards) card.SetSelected(card == picked);
        currentPath = picked.Data;
        ascensionText.text = currentPath.ascension;
        // show this Path's version of the hero
        var art = currentPath.heroArt != null ? currentPath.heroArt : currentClass.portrait;
        if (art != null) portrait.sprite = art;
        UpdateStart();
    }

    void UpdateStart()
    {
        if (currentClass == null || currentPath == null) return;
        bool stageOpen = stages.Count == 0 || SaveSystem.IsStageUnlocked(stages[stageIndex]);
        bool canStart = SaveSystem.IsPathUnlocked(currentClass, currentPath) && stageOpen;
        startButton.interactable = canStart;
        if (startLabel) startLabel.text = canStart ? "Start run" : "Locked";
    }

    void StartRun()
    {
        RunSelection.Class = currentClass;
        RunSelection.Path = currentPath;
        RunSelection.Stage = stages.Count > 0 ? stages[stageIndex] : null;
        if (RunSelection.Mode == RunMode.Daily) RunSelection.Mode = RunMode.Normal;
        RunSelection.Ascension = SaveSystem.Data.ascensionSelected;
        SceneManager.LoadScene("Game");
    }
}
