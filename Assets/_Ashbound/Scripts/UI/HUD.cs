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
    [SerializeField] Button speedButton;
    [SerializeField] TMP_Text speedLabel;

    float runTime;
    public float RunTime => runTime;

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
}
