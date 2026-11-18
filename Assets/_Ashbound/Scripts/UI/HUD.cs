using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [SerializeField] Image xpFill, hpFill;
    [SerializeField] TMP_Text levelText, hpText, timerText, killsText, goldText, heroText;
    [SerializeField] Image levelBadge;
    [SerializeField] GameObject bossWarning;
    [SerializeField] TMP_Text bossWarningText;
    [SerializeField] SkillSlot pathSlot;
    [SerializeField] TMP_Text pathSlotLabel;
    [SerializeField] SkillSlot[] weaponSlots = new SkillSlot[5];
    [SerializeField] SkillSlot[] passiveSlots = new SkillSlot[5];
    [SerializeField] GameObject bossBar;
    [SerializeField] Image bossBarFill;
    [SerializeField] TMP_Text bossBarName;
    [Header("Feedback")]
    [SerializeField] Image hurtVignette;
    [SerializeField] RectTransform bossIntro;
    [SerializeField] TMP_Text bossIntroName, bossIntroTitle;
    [SerializeField] RectTransform tipBanner;
    [SerializeField] TMP_Text tipText;
    [SerializeField] ChestPopup chestPopup;
    [SerializeField] Button speedButton;
    float hurtFlash, heartTimer;
    bool lowHealth;
    readonly System.Collections.Generic.Queue<string> tips = new();
    Coroutine tipRoutine;
    [SerializeField] TMP_Text speedLabel;

    float runTime;
    public float RunTime => runTime;

    // kill streak counter (built in code under the HUD canvas)
    TextMeshProUGUI streakText, streakLabel;
    float streakPulse;

    void BuildStreak()
    {
        var parent = killsText != null ? killsText.canvas.transform : transform;
        var root = new GameObject("KillStreak", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-64f, -230f); root.sizeDelta = new Vector2(320f, 110f);
        streakText = MakeText(root, "Count", 64f, new Vector2(0, 0), TextAlignmentOptions.TopRight);
        streakLabel = MakeText(root, "Label", 22f, new Vector2(0, -70f), TextAlignmentOptions.TopRight);
        streakLabel.text = "KILL STREAK"; streakLabel.characterSpacing = 6;
        root.gameObject.SetActive(false);
    }

    TextMeshProUGUI MakeText(RectTransform parent, string name, float size, Vector2 pos, TextAlignmentOptions align)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.rectTransform.SetParent(parent, false);
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = t.rectTransform.pivot = new Vector2(1f, 1f);
        t.rectTransform.anchoredPosition = pos; t.rectTransform.sizeDelta = new Vector2(320f, size * 1.2f);
        t.font = killsText != null ? killsText.font : null; t.fontSize = size; t.alignment = align; t.raycastTarget = false;
        t.color = Palette.Gold; t.outlineWidth = 0.22f; t.outlineColor = Palette.Outline;
        return t;
    }

    public void SetStreak(int n)
    {
        if (streakText == null) return;
        var root = streakText.rectTransform.parent.gameObject;
        if (n <= 0) { root.SetActive(false); return; }
        root.SetActive(true);
        streakText.text = $"x{n:N0}";
        streakText.color = n >= 500 ? new Color(1f, 0.35f, 0.3f) : n >= 100 ? new Color(1f, 0.6f, 0.25f) : Palette.Gold;
        streakPulse = 1f;
    }

    public void StreakMilestone(int m, int bonusGold)
    {
        ShowTip($"{m} kill streak!  +{bonusGold} gold");
        streakPulse = 2.2f;
    }

    void Awake()
    {
        var cls = RunSelection.Class;
        var path = RunSelection.Path;
        heroText.text = cls != null && path != null ? $"{cls.className} · {path.pathName}" : "Mage · Fire";
        if (levelBadge && cls != null) levelBadge.color = cls.color;
        if (pathSlotLabel && path != null) pathSlotLabel.text = path.startingSkill;
        pathSlot.Set(null, path != null ? path.color : Palette.Ember, 1);
        foreach (var s in weaponSlots) s.Set(null, Color.white, 0);
        foreach (var s in passiveSlots) s.Set(null, Color.white, 0);
        bossWarning.SetActive(false);
        if (bossBar) bossBar.SetActive(false);
        if (bossIntro) bossIntro.gameObject.SetActive(false);
        if (tipBanner) tipBanner.gameObject.SetActive(false);
        if (chestPopup) chestPopup.gameObject.SetActive(false);
        if (hurtVignette) hurtVignette.color = new Color(1f, 1f, 1f, 0f);
        BuildStreak();
        SetXP(0, 100, 1);
        if (speedButton != null)
        {
            speedButton.gameObject.SetActive(SaveSystem.Data.runsPlayed >= 1);
            speedButton.onClick.AddListener(ToggleSpeed);
            UpdateSpeedLabel();
        }
    }

    void Update()
    {
        var run = RunManager.Instance;
        runTime = run != null ? run.RunTime : runTime + Time.deltaTime; // stops while Time.timeScale = 0
        int m = (int)(runTime / 60), s = (int)(runTime % 60);
        timerText.text = $"{m:00}:{s:00}";
        UpdateVignette();
        if (streakText != null && streakPulse > 0f)
        {
            streakPulse = Mathf.Max(0f, streakPulse - Time.unscaledDeltaTime * 6f);
            streakText.rectTransform.localScale = Vector3.one * (1f + 0.12f * Mathf.Min(streakPulse, 1.5f));
        }
    }

    // ---------------- hit feedback: red screen edge on hits, pulsing + heartbeat at low HP

    public void Hurt() => hurtFlash = 1f;
    public void SetLowHealth(bool low) => lowHealth = low;

    void UpdateVignette()
    {
        if (!hurtVignette) return;
        bool paused = Time.timeScale == 0f;
        hurtFlash = Mathf.Max(0f, hurtFlash - Time.unscaledDeltaTime * 2.5f);
        float pulse = 0f;
        if (lowHealth)
        {
            pulse = 0.3f + 0.25f * Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.4f)), 6f);
            if (!paused)
            {
                heartTimer -= Time.unscaledDeltaTime;
                if (heartTimer <= 0f) { heartTimer = 0.92f; AudioManager.Play("heartbeat", 0.8f); }
            }
        }
        hurtVignette.color = new Color(1f, 1f, 1f, Mathf.Max(pulse, hurtFlash * 0.7f));
    }

    // ---------------- boss intro card

    public void ShowBossIntro(string bossName, string bossTitle)
    {
        if (!bossIntro) return;
        bossIntroName.text = bossName;
        bossIntroTitle.text = string.IsNullOrEmpty(bossTitle) ? "" : bossTitle;
        StartCoroutine(BossIntroRoutine());
    }

    System.Collections.IEnumerator BossIntroRoutine()
    {
        bossIntro.gameObject.SetActive(true);
        float w = 1400f, t = 0f;
        while (t < 1f) { t += Time.unscaledDeltaTime / 0.35f; float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); bossIntro.anchoredPosition = new Vector2(Mathf.Lerp(-w, 0f, k), bossIntro.anchoredPosition.y); yield return null; }
        yield return new WaitForSecondsRealtime(1.8f);
        t = 0f;
        while (t < 1f) { t += Time.unscaledDeltaTime / 0.3f; float k = Mathf.Clamp01(t) * Mathf.Clamp01(t); bossIntro.anchoredPosition = new Vector2(Mathf.Lerp(0f, w, k), bossIntro.anchoredPosition.y); yield return null; }
        bossIntro.gameObject.SetActive(false);
    }

    // ---------------- first-run tips (queued, one at a time)

    public void ShowTip(string text)
    {
        if (!tipBanner) return;
        tips.Enqueue(text);
        if (tipRoutine == null) tipRoutine = StartCoroutine(TipRoutine());
    }

    System.Collections.IEnumerator TipRoutine()
    {
        var group = tipBanner.GetComponent<CanvasGroup>();
        while (tips.Count > 0)
        {
            tipText.text = tips.Dequeue();
            tipBanner.gameObject.SetActive(true);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f) { if (group) group.alpha = t; yield return null; }
            if (group) group.alpha = 1f;
            yield return new WaitForSecondsRealtime(5f);
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.35f) { if (group) group.alpha = t; yield return null; }
            tipBanner.gameObject.SetActive(false);
        }
        tipRoutine = null;
    }

    // ---------------- boss chest

    public bool ChestOpen => chestPopup && chestPopup.IsOpen;
    public void ShowChest(string rewards, System.Action done)
    {
        if (chestPopup) chestPopup.Play(rewards, done);
        else done?.Invoke();
    }

    public void ToggleSpeed()
    {
        GameSpeed.Fast = !GameSpeed.Fast;
        if (Time.timeScale > 0f) GameSpeed.Resume();
        UpdateSpeedLabel();
    }

    void UpdateSpeedLabel()
    {
        if (speedLabel != null) speedLabel.text = GameSpeed.Fast ? "x2" : "x1";
    }

    public void SetXP(int current, int needed, int level)
    {
        xpFill.fillAmount = Mathf.Clamp01((float)current / needed);
        levelText.text = level.ToString();
    }

    public void SetHealth(int current, int max)
    {
        hpFill.fillAmount = Mathf.Clamp01((float)current / max);
        hpText.text = $"{current} / {max} HP";
    }

    public void SetKills(int kills) => killsText.text = $"{kills:N0} kills";
    public void SetGold(int gold) => goldText.text = $"{gold:N0} gold";

    public void SetPathSkill(int level, bool awakened)
    {
        var path = RunSelection.Path;
        pathSlot.Set(null, path != null ? path.color : Palette.Ember, level, awakened);
    }

    public void SetPathSkill(Sprite icon, Color color, int level, bool awakened, string label)
    {
        pathSlot.Set(icon, color, level, awakened);
        if (pathSlotLabel) pathSlotLabel.text = label;
    }

    public void SetWeapon(int index, Color color, int level, bool awakened = false) => weaponSlots[index].Set(null, color, level, awakened);
    public void SetWeapon(int index, Sprite icon, Color color, int level, bool awakened = false) => weaponSlots[index].Set(icon, color, level, awakened);
    public void SetPassive(int index, Color color, int level) => passiveSlots[index].Set(null, color, level);
    public void SetPassive(int index, Sprite icon, Color color, int level) => passiveSlots[index].Set(icon, color, level);

    public void ShowBossWarning(string bossName, int seconds)
    {
        bossWarning.SetActive(true);
        bossWarningText.text = $"{bossName} arrives in 0:{seconds:00}";
    }

    public void ShowBossBar(string bossName, float fraction)
    {
        if (!bossBar) return;
        if (!bossBar.activeSelf) bossBar.SetActive(true);
        bossBarName.text = bossName;
        bossBarFill.fillAmount = Mathf.Lerp(bossBarFill.fillAmount, fraction, 12f * Time.unscaledDeltaTime);
    }

    public void HideBossBar() { if (bossBar && bossBar.activeSelf) { bossBar.SetActive(false); bossBarFill.fillAmount = 1f; } }

    public void HideBossWarning() => bossWarning.SetActive(false);

    TMP_Text modeText;
    /// <summary>A small line under the timer: Daily curses, Ascension level or Endless record.</summary>
    public void SetModeLine(string text)
    {
        if (timerText == null) return;
        if (modeText == null)
        {
            modeText = Instantiate(timerText, timerText.transform.parent);
            modeText.name = "ModeLine";
            var rt = modeText.rectTransform;
            rt.anchoredPosition = timerText.rectTransform.anchoredPosition + new Vector2(0f, -timerText.rectTransform.rect.height * 0.95f);
            rt.sizeDelta = new Vector2(1000f, 34f);
            modeText.enableAutoSizing = false; modeText.fontSize = 20f; modeText.color = Palette.TextMuted;
            modeText.textWrappingMode = TextWrappingModes.NoWrap; modeText.alignment = TextAlignmentOptions.Top;
        }
        modeText.text = text;
    }
}
