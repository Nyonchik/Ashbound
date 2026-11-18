using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Each hero's own mechanic, shown as a small bar under the hero.
/// Mage Mana: damage grows while you avoid getting hit. Berserker Rage: lower HP = more damage and faster attacks.
/// Archer Focus: standing still charges stronger attacks. Assassin Shadowstep: Space to dash (brief invisibility, guaranteed crits) + crit chance.
/// Tank Fortify: takes 10% less damage; damage taken builds a shockwave that releases on its own.
/// Gear can boost the mechanic ("boost", 0..~0.8).
/// </summary>
public class HeroMechanic : MonoBehaviour
{
    public enum Kind { None, Mana, Rage, Focus, Shadowstep, Fortify }
    public Kind Mode { get; private set; }

    PlayerStats stats;
    PlayerController pc;
    float boost, value, appliedDamage, appliedCd = 1f, lastHealth = -1f;
    float dashReady, stealth, fortify;
    SpriteRenderer barBg, barFill;
    static readonly System.Collections.Generic.List<Enemy> hits = new();

    public static Kind KindFor(string className) => className switch
    {
        "Mage" => Kind.Mana,
        "Berserker" => Kind.Rage,
        "Archer" => Kind.Focus,
        "Assassin" => Kind.Shadowstep,
        "Tank (Knight)" => Kind.Fortify,
        _ => Kind.None,
    };

    public void Setup(string className, float gearBoost, Sprite barSprite)
    {
        stats = GetComponent<PlayerStats>();
        pc = GetComponent<PlayerController>();
        Mode = KindFor(className);
        boost = Mathf.Max(0f, gearBoost);
        if (Mode == Kind.Fortify) stats.damageReduction = Mathf.Min(0.5f, stats.damageReduction + 0.1f);
        if (Mode == Kind.None || barSprite == null) return;
        barBg = Bar("MechanicBar", barSprite, new Color(0.05f, 0.05f, 0.07f, 0.75f), 30, 0.9f, 0.11f);
        barFill = Bar("MechanicFill", barSprite, Color, 31, 0.86f, 0.07f);
    }

    Color Color => Mode switch
    {
        Kind.Mana => new Color(0.55f, 0.45f, 1f),
        Kind.Rage => new Color(1f, 0.3f, 0.25f),
        Kind.Focus => new Color(1f, 0.82f, 0.35f),
        Kind.Shadowstep => new Color(0.45f, 1f, 0.6f),
        _ => new Color(0.4f, 0.65f, 1f),
    };

    SpriteRenderer Bar(string name, Sprite s, Color c, int order, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.28f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.color = c; sr.sortingOrder = order;
        var b = s.bounds.size;
        go.transform.localScale = new Vector3(w / b.x, h / b.y, 1f);
        return sr;
    }

    void Update()
    {
        if (stats == null || Mode == Kind.None) return;
        var run = RunManager.Instance;
        if (run == null || run.IsOver || stats.IsDead) return;
        float dt = Time.deltaTime;
        float h = stats.Health;
        bool hurt = lastHealth >= 0f && h < lastHealth - 0.01f;
        float taken = hurt ? lastHealth - h : 0f;
        lastHealth = h;

        float dmgBonus = 0f, cdFactor = 1f;
        float k = 1f + boost * 0.6f;   // gear makes the mechanic stronger
        switch (Mode)
        {
            case Kind.Mana:
                value = hurt ? 0f : Mathf.Min(1f, value + dt / (12f / (1f + boost)));
                dmgBonus = value * 0.4f * k;
                if (value >= 1f && Random.value < dt * 3f) FxParticles.Motes(transform.position + Vector3.up * 0.4f, Color, 1, 0.4f, 0.14f, 0.6f);
                break;

            case Kind.Rage:
                value = 1f - h / Mathf.Max(1f, stats.MaxHealth);
                dmgBonus = value * 0.6f * k;
                cdFactor = 1f - Mathf.Min(0.45f, value * 0.3f * k);
                if (value > 0.5f && Random.value < dt * 4f) FxParticles.Motes(transform.position + Vector3.up * 0.3f, Color, 1, 0.4f, 0.15f, 0.5f);
                break;

            case Kind.Focus:
                value = pc.IsMoving ? Mathf.Max(0f, value - dt * 3f) : Mathf.Min(1f, value + dt / (2f / (1f + boost)));
                dmgBonus = value * 0.5f * k;
                if (value >= 1f && Random.value < dt * 3f) FxParticles.Sparks(transform.position + Vector3.up * 0.5f, Color, 1, 1.2f, 0.14f, 0.4f);
                break;

            case Kind.Shadowstep:
                ShadowUpdate(dt);
                break;

            case Kind.Fortify:
                fortify += taken;
                float need = 30f / (1f + boost * 0.5f);
                value = Mathf.Clamp01(fortify / need);
                if (fortify >= need) { fortify = 0f; Shockwave(); }
                break;
        }

        stats.damageMul += dmgBonus - appliedDamage; appliedDamage = dmgBonus;
        stats.cooldownMul *= cdFactor / appliedCd; appliedCd = cdFactor;

        if (barFill != null)
        {
            var t = barFill.transform; var b = barFill.sprite.bounds.size;
            t.localScale = new Vector3(Mathf.Max(0.001f, 0.86f * value) / b.x, t.localScale.y, 1f);
            t.localPosition = new Vector3(-0.43f + 0.43f * value, -0.28f, 0f);
            barFill.color = value >= 0.999f ? Color.Lerp(Color, UnityEngine.Color.white, 0.35f + Mathf.Sin(Time.time * 8f) * 0.2f) : Color;
        }
    }

    void ShadowUpdate(float dt)
    {
        float cd = 3f / (1f + boost * 0.6f);
        dashReady = Mathf.Max(0f, dashReady - dt);
        value = 1f - dashReady / cd;
        stealth = Mathf.Max(0f, stealth - dt);
        Enemy.CritChance = stealth > 0f ? 1f : 0.1f + boost * 0.15f;

        var r = pc.HeroRenderer;
        if (r != null) stats.SetBaseColor(r, stealth > 0.5f ? new Color(1f, 1f, 1f, 0.35f) : UnityEngine.Color.white);

        var kb = Keyboard.current;
        if (kb != null && Controls.Pressed(Controls.Action.Dash) && dashReady <= 0f && Time.timeScale > 0f)
        {
            dashReady = cd;
            stealth = 1.5f;   // 0.5s invisible + 1s of guaranteed crits
            Vector2 dir = pc.Facing;
            FxParticles.Smoke(transform.position, new Color(0.12f, 0.1f, 0.16f, 0.7f), 8, 0.3f, 0.9f, 0.6f, 1.5f);
            pc.Dash(dir * 16f, 0.18f);
            stats.Invulnerable(0.5f);
            AudioManager.Play("click", 0.6f, 0.7f);
        }
    }

    void Shockwave()
    {
        Vector2 c = transform.position;
        float r = 3f * (1f + boost * 0.3f);
        float dmg = 30f * stats.damageMul * (1f + boost);
        Enemy.CollectInRadius(c, r, hits);
        foreach (var e in hits) e.TakeDamage(dmg, ((Vector2)e.transform.position - c).normalized, 8f);
        FxParticles.Glow(c, new Color(0.45f, 0.65f, 1f, 0.8f), r * 2.2f, 0.4f, -9);
        FxParticles.Sparks(c, new Color(0.6f, 0.8f, 1f), 30, 8f, 0.2f, 0.45f);
        FxParticles.Debris(c, new Color(0.5f, 0.45f, 0.4f), 16, 6f, 0.14f, 0.5f);
        var run = RunManager.Instance;
        if (run != null && run.Art.waveRing != null)
        {
            var s = run.Art.waveRing; float w = s.bounds.size.x;
            run.Fx(s, c, new Color(0.6f, 0.8f, 1f, 0.8f), 0.4f, -8).Scale(0.5f / w, r * 2f / w).FadeFrom(0.3f);
        }
        CameraFollow.Shake(0.12f, 0.18f);
    }

    void OnDisable() { Enemy.CritChance = 0f; }
}
