using UnityEngine;

/// <summary>
/// TEMPORARY: fakes XP, damage, kills and level-ups so the HUD can be tested before gameplay exists.
/// Delete this component once real gameplay drives the HUD.
/// </summary>
public class HUDTester : MonoBehaviour
{
    [SerializeField] HUD hud;
    [SerializeField] LevelUpScreen levelUp;
    [SerializeField] int levelUpEvery = 3;

    float t;
    int xp, level = 1, kills, hp = 120, pathLevel = 1, weaponLevel;

    void Start()
    {
        if (levelUp) levelUp.OnPicked += _ =>
        {
            pathLevel = Mathf.Min(pathLevel + 1, 6);
            weaponLevel = Mathf.Min(weaponLevel + 1, 5);
            hud.SetPathSkill(Mathf.Min(pathLevel, 5), pathLevel == 6);
            hud.SetWeapon(0, new Color(0.24f, 0.65f, 0.96f), weaponLevel);
            if (weaponLevel >= 2) hud.SetPassive(0, new Color(0.44f, 0.89f, 0.82f), weaponLevel - 1);
        };
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t < 0.15f) return;
        t = 0;
        xp += 9; kills += 3;
        hp = Mathf.Max(1, hp - 1);
        if (xp >= 100)
        {
            xp = 0; level++;
            if (levelUp && level % levelUpEvery == 0) levelUp.ShowTest(level);
        }
        hud.SetXP(xp, 100, level);
        hud.SetHealth(hp, 120);
        hud.SetKills(kills);
        hud.SetGold(kills / 4);

        float time = hud.RunTime;
        if (time > 20f && time < 30f) hud.ShowBossWarning("The Ash Warden", Mathf.CeilToInt(30f - time));
        else hud.HideBossWarning();
    }
}
