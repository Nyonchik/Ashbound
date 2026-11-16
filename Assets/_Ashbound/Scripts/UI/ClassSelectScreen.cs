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
        SceneManager.LoadScene("Game");
    }
}
