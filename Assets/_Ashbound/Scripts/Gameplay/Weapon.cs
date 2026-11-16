using System.Collections.Generic;
using UnityEngine;

/// <summary>One owned weapon or Path skill. Level 1-5, then 6 = Awakened.</summary>
public class Weapon : MonoBehaviour
{
    public WeaponDef Def { get; private set; }
    public int Level { get; private set; }
    public bool IsPath { get; private set; }
    public bool IsEvolved { get; private set; }
    public int MaxLevel => IsPath ? 6 : 5;
    public bool Awakened => IsPath && Level >= 6;
    public bool Golden => Awakened || IsEvolved;
    public bool CanLevel => !IsEvolved && Level < MaxLevel;
    public bool CanEvolve(System.Collections.Generic.ICollection<PassiveDef> owned) =>
        !IsPath && !IsEvolved && Level >= 5 && Def.evolvesInto != null && Def.evolvePassive != null && owned.Contains(Def.evolvePassive);

    static readonly List<Enemy> buffer = new();
    readonly List<SpriteRenderer> orbs = new();
    readonly Dictionary<Enemy, float> orbHitTimes = new();

    PlayerStats stats;
    LevelBonus bonus;
    float timer, orbAngle;

    float Damage => Def.damage * (1f + bonus.damage) * stats.damageMul;
    float Cooldown => Mathf.Max(0.1f, Def.cooldown * Mathf.Max(0.2f, 1f - bonus.cooldown) * stats.cooldownMul);
    float Area => Def.area * (1f + bonus.area) * stats.areaMul;
    int Count => Def.count + bonus.count;
    int Pierce => Def.pierce + bonus.pierce;
    bool Explodes => Def.explode || bonus.explode;

    public void Setup(WeaponDef def, PlayerStats owner, bool isPath)
    {
        Def = def; stats = owner; IsPath = isPath; Level = 1;
        timer = 0.4f;
        Refresh();
    }

    public void Evolve()
    {
        var evolved = Def.evolvesInto;
        if (evolved == null) return;
        foreach (var o in orbs) if (o != null) Destroy(o.gameObject);
        orbs.Clear();
        orbHitTimes.Clear();
        Def = evolved;
        IsEvolved = true;
        Level = 5;
        Refresh();
    }

    public void LevelUp()
    {
        if (!CanLevel) return;
        Level++;
        Refresh();
    }

    /// <summary>Recompute stats (call after level-ups and passive changes).</summary>
    public void Refresh()
    {
        bonus = default;
        if (!IsEvolved)
            for (int i = 0; i < Mathf.Min(Level - 1, Def.levels.Length); i++) Add(ref bonus, Def.levels[i]);
        if (Awakened) Add(ref bonus, Def.awakening);
        if (Def.kind == WeaponKind.Orbit) RebuildOrbs();
    }

    static void Add(ref LevelBonus a, LevelBonus b)
    {
        a.damage += b.damage; a.count += b.count; a.cooldown += b.cooldown;
        a.area += b.area; a.pierce += b.pierce; a.explode |= b.explode;
    }

    void Update()
    {
        var run = RunManager.Instance;
        if (Def == null || stats.IsDead || run == null || run.IsOver) return;

        switch (Def.kind)
        {
            case WeaponKind.Projectile:
                timer -= Time.deltaTime;
                if (timer <= 0f) { timer = Cooldown; FireProjectiles(); }
                break;
            case WeaponKind.Nova:
                timer -= Time.deltaTime;
                if (timer <= 0f) { timer = Cooldown; Nova(); }
                break;
            case WeaponKind.Orbit:
                UpdateOrbs();
                break;
        }
    }

    // ---------------- Projectile

    void FireProjectiles()
    {
        Vector2 origin = transform.position;
        var target = Enemy.Nearest(origin, 14f);
        Vector2 dir = target != null ? ((Vector2)target.transform.position - origin).normalized : PlayerController.Instance.Facing;
        int n = Count;
        const float spread = 12f;
        for (int i = 0; i < n; i++)
        {
            float angle = (i - (n - 1) / 2f) * spread;
            Vector2 d = Quaternion.Euler(0f, 0f, angle) * dir;
            RunManager.Instance.GetProjectile().Launch(this, origin, d, Def.speed, Damage, Area, Pierce, Def.lifetime, Def.knockback, Explodes, Def.color, Def.projectile);
        }
    }

    public void Explode(Vector2 position, float damage)
    {
        float r = 1.0f * Area;
        Enemy.CollectInRadius(position, r, buffer);
        foreach (var e in buffer) e.TakeDamage(damage, ((Vector2)e.transform.position - position).normalized, 2f);
        RunManager.Instance.SpawnVfx(position, r, Def.color, 0.3f);
    }

    // ---------------- Nova

    void Nova()
    {
        float r = 2.2f * Area;
        Vector2 c = transform.position;
        Enemy.CollectInRadius(c, r, buffer);
        foreach (var e in buffer) e.TakeDamage(Damage, ((Vector2)e.transform.position - c).normalized, Def.knockback);
        RunManager.Instance.SpawnVfx(c, r, Def.color, 0.45f);
        if (Explodes)
            foreach (var e in buffer)
                if (e != null && e.isActiveAndEnabled) { Explode(e.transform.position, Damage * 0.4f); break; }
    }

    // ---------------- Orbit

    void RebuildOrbs()
    {
        while (orbs.Count < Count) orbs.Add(RunManager.Instance.CreateOrb(transform, Def.color, Def.projectile));
        foreach (var o in orbs) o.transform.localScale = Vector3.one * 1.1f * Area;
    }

    void UpdateOrbs()
    {
        int n = orbs.Count;
        if (n == 0) return;
        orbAngle += Def.speed * 40f * Time.deltaTime;
        float radius = 1.35f * Area;
        float now = Time.time;

        for (int i = 0; i < n; i++)
        {
            float a = (orbAngle + i * 360f / n) * Mathf.Deg2Rad;
            var orb = orbs[i].transform;
            orb.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
            orb.Rotate(0f, 0f, 540f * Time.deltaTime);

            Enemy.CollectInRadius(orb.position, 0.38f * Area, buffer);
            foreach (var e in buffer)
            {
                if (orbHitTimes.TryGetValue(e, out float next) && now < next) continue;
                orbHitTimes[e] = now + 0.35f;
                e.TakeDamage(Damage, ((Vector2)e.transform.position - (Vector2)transform.position).normalized, Def.knockback);
            }
        }
        if (orbHitTimes.Count > 600) orbHitTimes.Clear();
    }

    // ---------------- Level-up card text

    public string NextLevelText => Def.levelText != null && Level < Def.levelText.Length ? Def.levelText[Level] : "";
}
