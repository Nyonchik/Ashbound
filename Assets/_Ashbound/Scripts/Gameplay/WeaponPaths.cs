using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Custom Path skills: one per Path that used to borrow its hero's default skill.
/// Sprites: Def.projectile = main sprite, Def.effect = secondary sprite (falls back to built-in effects).
/// </summary>
public partial class Weapon
{
    bool IsCustomPath => Def.style >= WeaponStyle.StoneSpikes;

    // state used by some skills
    int frenzyStacks, castCounter;
    float frenzyTimer, auraTick, trailTick, contactTick, lastHealth = -1f, thornsReady;
    static readonly HashSet<Enemy> hitOnce = new();
    static readonly List<Enemy> buffer3 = new();

    Sprite Main => ProjArt;
    Sprite Second => Def.effect;
    float HealCap => Awakened ? 2f : 1f;
    PlayerController Player => PlayerController.Instance;

    static float Fit(Sprite s, float size) => s == null ? size : size / Mathf.Max(0.01f, Mathf.Max(s.bounds.size.x, s.bounds.size.y));

    /// <summary>Draws a sprite stretched between two points (beams, chains).</summary>
    static FxSprite Stretch(Sprite s, Vector2 a, Vector2 b, Color c, float width, float seconds, int order = 17)
    {
        Vector2 d = b - a; float len = d.magnitude;
        if (s == null) s = Art.lightning;
        float sw = s != null ? s.bounds.size.x : 1f, sh = s != null ? s.bounds.size.y : 1f;
        var sc = new Vector3(len / sw, width / sh, 1f);
        return Run.Fx(s, (a + b) * 0.5f, c, seconds, order).Rotate(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg).Scale(sc, sc);
    }

    Vector2 AimDir(float range = 7f)
    {
        var t = Enemy.Nearest(transform.position, range);
        return t != null ? ((Vector2)t.transform.position - (Vector2)transform.position).normalized : Player.Facing;
    }

    void CollectCone(Vector2 c, Vector2 dir, float range, float degrees, List<Enemy> list)
    {
        Enemy.CollectInRadius(c, range, list);
        float cos = Mathf.Cos(degrees * 0.5f * Mathf.Deg2Rad);
        for (int i = list.Count - 1; i >= 0; i--)
        {
            Vector2 d = (Vector2)list[i].transform.position - c;
            if (d.sqrMagnitude > 0.36f && Vector2.Dot(d.normalized, dir) < cos) list.RemoveAt(i);
        }
    }

    void CollectLine(Vector2 a, Vector2 b, float halfWidth, List<Enemy> list)
    {
        list.Clear();
        Vector2 ab = b - a; float len2 = Mathf.Max(0.0001f, ab.sqrMagnitude);
        foreach (var e in Enemy.Active)
        {
            Vector2 p = e.transform.position;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            float r = halfWidth + e.Radius;
            if ((a + ab * t - p).sqrMagnitude <= r * r) list.Add(e);
        }
    }

    /// <summary>Damage an enemy and credit this weapon in the run summary.</summary>
    bool Hit(Enemy e, float amount, Vector2 dir, float knock)
    {
        var prev = Enemy.Source; Enemy.Source = this;
        bool killed = e.TakeDamage(amount, dir, knock);
        Enemy.Source = prev;
        return killed;
    }

    void Heal(float hp) { if (hp > 0f) stats.Heal(Mathf.Min(hp, HealCap)); }

    /// <summary>Instant kill with a blood burst (executions).</summary>
    void Execute(Enemy e)
    {
        Vector2 p = e.transform.position;
        FxParticles.Debris(p, new Color(0.7f, 0.04f, 0.06f), 14, 5f, 0.14f, 0.5f);
        FxParticles.Glow(p, new Color(1f, 0.2f, 0.15f, 0.8f), 1.6f, 0.25f);
        Run.SpawnVfx(p, 0.6f, Palette.Health, 0.2f);
        e.Kill();
    }

    Projectile Shoot(Sprite art, Vector2 from, Vector2 dir, float speed, float dmg, float scale, int pierce, float life)
    {
        var p = Run.GetProjectile();
        p.Launch(this, from, dir, speed, dmg, scale, pierce, life, Def.knockback, false, AwakeTint, art);
        return p;
    }

    // ---------------------------------------------------------------- per frame

    void UpdateCustomPath()
    {
        float dt = Time.deltaTime;
        if (frenzyTimer > 0f) { frenzyTimer -= dt; if (frenzyTimer <= 0f) frenzyStacks = 0; }

        switch (Def.style)   // things that run every frame
        {
            case WeaponStyle.Brambles: UpdateBrambles(dt); break;
            case WeaponStyle.HolyAura when Awakened: BurnAura(dt); break;
            case WeaponStyle.ShieldBash when Awakened: ContactDamage(dt); break;
            case WeaponStyle.PoisonDaggers when Awakened: ToxicTrail(dt); break;
            case WeaponStyle.CursedBlade when Awakened: FrostAura(); break;
        }

        timer -= dt;
        if (timer > 0f) return;
        float cd = Cooldown;
        if (Def.style == WeaponStyle.Claws) cd *= 1f - 0.03f * frenzyStacks;
        timer = cd;
        castCounter++;

        switch (Def.style)
        {
            case WeaponStyle.StoneSpikes: StartCoroutine(StoneSpikes()); break;
            case WeaponStyle.ShadowBolt: ShadowBolts(); break;
            case WeaponStyle.HolyBeam: HolyBeams(); break;
            case WeaponStyle.Claws: StartCoroutine(ClawSlashes()); break;
            case WeaponStyle.WarChains: StartCoroutine(WarChains()); break;
            case WeaponStyle.Cleave: StartCoroutine(Cleaves()); break;
            case WeaponStyle.Traps: HunterCast(); break;
            case WeaponStyle.IceArrows: IceArrows(); break;
            case WeaponStyle.SpectralArrows: SpectralArrows(); break;
            case WeaponStyle.PoisonDaggers: PoisonDaggers(); break;
            case WeaponStyle.Shadowstep: Shadowstep(); break;
            case WeaponStyle.Decoy: Decoys(); break;
            case WeaponStyle.Hemorrhage: Hemorrhage(); break;
            case WeaponStyle.FanOfKnives: StartCoroutine(FanOfKnives()); break;
            case WeaponStyle.HolyAura: HolyPulse(); break;
            case WeaponStyle.ShieldBash: ShieldBash(); break;
            case WeaponStyle.Brambles: if (Awakened) Ironwood(); break;
            case WeaponStyle.Charge: StartCoroutine(Charge()); break;
            case WeaponStyle.CursedBlade: StartCoroutine(CursedSweeps()); break;
        }
    }

    // ---------------------------------------------------------------- Earth: Stone Spikes / Titan's Quake

    IEnumerator StoneSpikes()
    {
        Vector2 c = transform.position;
        Vector2 dir = AimDir(9f);
        int lines = Count;
        float len = 5f * Mathf.Max(1f, Area);
        hitOnce.Clear();
        if (Awakened)
        {
            Run.Fx(Art.crater, c, Color.white, 0.9f, -10).Scale(1.2f, 2.6f * Area * 1.4f).FadeFrom(0.5f);
            Enemy.CollectInRadius(c, 2.6f * Area, buffer3);
            foreach (var e in buffer3) Hit(e, Damage * 0.8f, ((Vector2)e.transform.position - c).normalized, 4f);
            FxParticles.Glow(c, new Color(1f, 0.6f, 0.25f, 0.7f), 5f * Area, 0.4f, -9);
            FxParticles.Debris(c, new Color(0.5f, 0.38f, 0.25f), 24, 7f, 0.16f, 0.6f);
            FxParticles.Smoke(c, new Color(0.55f, 0.45f, 0.35f, 0.6f), 10, 1.4f * Area, 1.3f, 0.9f, 1.5f);
            CameraFollow.Shake(0.08f, 0.15f);
        }
        for (float d = 0.7f; d <= len; d += 0.6f)
        {
            for (int l = 0; l < lines; l++)
            {
                float ang = (l - (lines - 1) / 2f) * 22f;
                Vector2 p = c + (Vector2)(Quaternion.Euler(0, 0, ang) * dir) * d + Random.insideUnitCircle * 0.12f;
                var spr = Main != null ? Main : Art.iceSpikes;
                Run.Fx(spr, p + Vector2.up * 0.2f, Main != null ? Color.white : new Color(0.75f, 0.55f, 0.35f), 0.7f, 11)
                   .Scale(0.1f, Fit(spr, 1.1f * Mathf.Max(1f, Area * 0.85f))).FadeFrom(0.6f);
                FxParticles.Debris(p, new Color(0.55f, 0.4f, 0.25f), 3, 3f, 0.12f, 0.45f, Vector2.up, 120f);
                FxParticles.Smoke(p, new Color(0.6f, 0.5f, 0.38f, 0.55f), 1, 0.2f, 0.7f, 0.6f, 0.6f);
                Enemy.CollectInRadius(p, 0.55f * Area, buffer3);
                foreach (var e in buffer3)
                {
                    if (!hitOnce.Add(e)) continue;
                    Hit(e, Damage, Vector2.up, 0.5f);
                    e.Slow(1.2f, 0.5f);
                    FxParticles.Sparks(e.transform.position, new Color(1f, 0.75f, 0.4f), 4, 3f, 0.16f, 0.25f);
                }
            }
            yield return new WaitForSeconds(0.035f);
        }
    }

    // ---------------------------------------------------------------- Dark: Shadow Bolt / Lich's Hunger

    void ShadowBolts()
    {
        Vector2 o = (Vector2)transform.position + Vector2.up * 0.5f;
        Vector2 dir = AimDir(12f);
        for (int i = 0; i < Count; i++)
        {
            Vector2 d = Quaternion.Euler(0, 0, (i - (Count - 1) / 2f) * 18f) * dir;
            DarkBolt(o, d, Damage, 2);
        }
    }

    void DarkBolt(Vector2 from, Vector2 dir, float dmg, int chainsLeft)
    {
        float healed = 0f;
        Shoot(Main, from, dir, Def.speed, dmg, Area * 0.9f, Pierce, Def.lifetime).Homing(260f)
            .Trail(new Color(0.6f, 0.25f, 1f), 0.45f * Area, 0.3f).OnHit((e, killed) =>
        {
            if (this == null) return;   // weapon removed while the bolt was flying
            FxParticles.Smoke(e.transform.position, new Color(0.2f, 0.08f, 0.3f, 0.7f), 2, 0.2f, 0.7f, 0.5f);
            if (!killed) return;
            FxParticles.Glow(e.transform.position, new Color(0.65f, 0.3f, 1f, 0.8f), 1.6f, 0.3f);
            FxParticles.Sparks(e.transform.position, new Color(0.7f, 0.35f, 1f), 10, 4f, 0.2f, 0.4f);
            FxParticles.Line(e.transform.position, (Vector2)transform.position + Vector2.up * 0.5f, new Color(0.8f, 0.2f, 0.4f), 1.2f, 0.12f, 0.3f, 0.4f);
            if (healed < 0.9f) { healed += 0.3f; stats.Heal(0.3f); }   // life drain
            if (chainsLeft <= 0) return;
            Vector2 at = e.transform.position;
            Run.SpawnVfx(at, 0.7f, new Color(0.6f, 0.3f, 0.9f), 0.25f);
            int splits = Awakened ? 2 : 1;
            for (int s = 0; s < splits; s++)
            {
                var next = Enemy.Nearest(at, 6f);
                Vector2 d = next != null && next != e ? ((Vector2)next.transform.position - at).normalized : Random.insideUnitCircle.normalized;
                if (s > 0) d = Quaternion.Euler(0, 0, s == 1 ? 40f : -40f) * d;
                DarkBolt(at, d, dmg * 0.8f, chainsLeft - 1);
            }
        });
    }

    // ---------------------------------------------------------------- Light: Holy Beam / Seraph's Halo

    void HolyBeams()
    {
        Vector2 o = (Vector2)transform.position + Vector2.up * 0.5f;
        Vector2 dir = AimDir(10f);
        float len = 7f * Mathf.Max(1f, Area), width = 0.7f * Area;
        int n = Count + (Awakened ? 2 : 0);
        hitOnce.Clear();
        int hits = 0;
        for (int i = 0; i < n; i++)
        {
            Vector2 d = i < Count ? (Vector2)(Quaternion.Euler(0, 0, (i - (Count - 1) / 2f) * 15f) * dir)
                                  : (Vector2)(Quaternion.Euler(0, 0, 45f + (i - Count) * 90f) * dir);
            Vector2 end = o + d * len;
            if (FxParticles.GlowSprite != null) Stretch(FxParticles.GlowSprite, o, end, new Color(1f, 0.85f, 0.4f, 0.9f), width * 3f, 0.35f, 14).Additive().FadeFrom(0.2f);
            Stretch(Main, o, end, Color.white, width * 1.6f, 0.3f).FadeFrom(0.3f);
            FxParticles.Glow(o, new Color(1f, 0.9f, 0.5f), 1.6f, 0.3f);
            FxParticles.Glow(end, new Color(1f, 0.85f, 0.4f, 0.8f), 1.2f, 0.3f);
            FxParticles.Line(o, end, new Color(1f, 0.85f, 0.45f), 2.5f, 0.2f, 1.2f, 0.45f);
            CollectLine(o, end, width * 0.5f, buffer3);
            foreach (var e in buffer3)
            {
                if (!hitOnce.Add(e)) continue;
                Hit(e, Damage, d, Def.knockback);
                FxParticles.Sparks(e.transform.position, new Color(1f, 0.9f, 0.5f), 4, 3f, 0.15f, 0.25f);
                hits++;
            }
        }
        if (hits > 0) { Heal(0.3f * Mathf.Min(hits, 3)); FxParticles.Motes(transform.position, new Color(0.6f, 1f, 0.6f), 3, 0.4f); }
    }

    // ---------------------------------------------------------------- Beast: Wolf Claws / Werewolf Frenzy

    IEnumerator ClawSlashes()
    {
        bool howl = Awakened && castCounter % 4 == 0;
        if (howl)
        {
            Vector2 c = transform.position;
            Enemy.CollectInRadius(c, 3.5f * Area, buffer3);
            foreach (var e in buffer3) { Hit(e, Damage, ((Vector2)e.transform.position - c).normalized, 9f); e.Slow(1.5f, 0.5f); }
            var howlRing = Art.waveRing != null ? Art.waveRing : Art.frostBurst;
            Run.Fx(howlRing, c, new Color(1f, 0.55f, 0.5f, 0.6f), 0.45f, -8).Scale(Fit(howlRing, 1f), Fit(howlRing, 7f * Area)).FadeFrom(0.4f);
            FxParticles.Glow(c, new Color(1f, 0.3f, 0.2f, 0.6f), 6f * Area, 0.45f, -9);
            FxParticles.Smoke(c, new Color(0.45f, 0.4f, 0.42f, 0.5f), 12, 2.5f * Area, 1.1f, 0.8f, 3f);
            CameraFollow.Shake(0.06f, 0.12f);
        }
        for (int i = 0; i < Count; i++)
        {
            Vector2 c = transform.position;
            Vector2 dir = AimDir(4.5f);
            float range = 1.9f * Area;
            CollectCone(c, dir, range, 120f, buffer3);
            foreach (var e in buffer3) { Hit(e, Damage, dir, Def.knockback); FxParticles.Debris(e.transform.position, new Color(0.7f, 0.08f, 0.1f), 3, 3f, 0.1f, 0.4f, dir, 70f); }
            if (buffer3.Count > 0) { frenzyStacks = Mathf.Min(10, frenzyStacks + 1); frenzyTimer = 2.5f; }
            FxParticles.Sparks(c + dir * range * 0.6f + Vector2.up * 0.3f, new Color(1f, 0.35f, 0.25f), 8 + frenzyStacks, 5f, 0.17f, 0.25f, dir, 90f, 0.5f);
            if (frenzyStacks >= 6) FxParticles.Motes(c, new Color(1f, 0.3f, 0.2f), 2, 0.4f, 0.15f, 0.5f);
            var spr = Main != null ? Main : Art.fireBurst;
            Run.Fx(spr, c + dir * range * 0.55f + Vector2.up * 0.3f, Color.white, 0.22f, 16)
               .Rotate(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg)
               .Scale(Fit(spr, range * 0.9f), Fit(spr, range * 1.2f)).FadeFrom(0.4f);
            yield return new WaitForSeconds(0.12f);
        }
    }

    // ---------------------------------------------------------------- Warlord: War Chains / Conqueror's Grip

    IEnumerator WarChains()
    {
        int slams = Awakened ? 2 : 1;
        for (int s = 0; s < slams; s++)
        {
            Vector2 c = transform.position;
            int n = Count + 2;
            hitOnce.Clear();
            var targets = new List<Enemy>();
            for (int i = 0; i < n; i++)
            {
                var e = NearestExcluding(c, 6.5f * Mathf.Max(1f, Area), hitOnce);
                if (e == null) break;
                hitOnce.Add(e); targets.Add(e);
            }
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                c = transform.position;
                foreach (var e in targets)
                {
                    if (e == null || !e.isActiveAndEnabled) continue;
                    Vector2 p = e.transform.position, d = c - p;
                    if (d.magnitude > 0.9f) e.Pull(d.normalized * 14f);
                    Stretch(Main, c + Vector2.up * 0.4f, p + Vector2.up * 0.3f, Color.white, 0.35f, 0.04f, 15);
                    if (Random.value < 0.3f) FxParticles.Sparks(p + Vector2.up * 0.3f, new Color(1f, 0.7f, 0.3f), 1, 2f, 0.12f, 0.2f);
                }
                yield return null;
            }
            foreach (var e in targets) if (e != null && e.isActiveAndEnabled) Hit(e, Damage * 0.5f, Vector2.zero, 0f);
            float r = 2.0f * Area;
            Run.Fx(Art.crater, c, Color.white, 0.9f, -10).Scale(r * 1.2f, r * 1.8f).FadeFrom(0.5f);
            Enemy.CollectInRadius(c, r, buffer3);
            foreach (var e in buffer3) Hit(e, Damage * 1.6f, ((Vector2)e.transform.position - c).normalized, 5f);
            FxParticles.Glow(c, new Color(1f, 0.55f, 0.25f, 0.7f), r * 2.4f, 0.35f, -9);
            FxParticles.Debris(c, new Color(0.45f, 0.38f, 0.32f), 18, 6f, 0.15f, 0.5f);
            FxParticles.Smoke(c, new Color(0.5f, 0.45f, 0.4f, 0.55f), 8, r * 0.6f, 1.2f, 0.8f, 2f);
            CameraFollow.Shake(0.08f, 0.14f);
            yield return new WaitForSeconds(0.25f);
        }
    }

    // ---------------------------------------------------------------- Executioner: Great Axe / Headsman

    IEnumerator Cleaves()
    {
        float exec = Awakened ? 0.25f : Level >= 5 ? 0.2f : 0.15f;
        int swings = Awakened ? 2 : 1;
        for (int s = 0; s < swings; s++)
        {
            Vector2 c = transform.position;
            Vector2 dir = AimDir(5f);
            if (s == 1) dir = -dir;
            float range = 2.4f * Area;
            CollectCone(c, dir, range, 150f, buffer3);
            foreach (var e in buffer3)
            {
                if (e == null || !e.isActiveAndEnabled) continue;
                if (!e.IsBoss && e.HealthFraction <= exec) { Execute(e); continue; }
                bool killed = Hit(e, Damage, dir, Def.knockback);
                if (!killed && !e.IsBoss && e.HealthFraction <= exec) Execute(e);
                else FxParticles.Sparks(e.transform.position, new Color(1f, 0.85f, 0.7f), 3, 3f, 0.14f, 0.2f, dir, 80f);
            }
            var spr = Main != null ? Main : Art.wave;
            Run.Fx(spr, c + dir * range * 0.55f + Vector2.up * 0.3f, new Color(1f, 1f, 1f, 0.85f), 0.25f, 16)
               .Rotate(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg)
               .Scale(Fit(spr, range * 0.7f), Fit(spr, range * 1.05f)).FadeFrom(0.45f);
            for (int k = 0; k < 9; k++)
            {
                Vector2 arc = Quaternion.Euler(0, 0, (k - 4) * 17f) * dir;
                FxParticles.Sparks(c + arc * range * 0.85f + Vector2.up * 0.3f, new Color(1f, 0.9f, 0.8f), 1, 2f, 0.16f, 0.25f, arc, 40f);
            }
            CameraFollow.Shake(0.05f, 0.1f);
            yield return new WaitForSeconds(0.2f);
        }
    }

    // ---------------------------------------------------------------- Hunter: Wolf & Traps / Beastmaster

    void HunterCast()
    {
        var list = EnemiesOnScreen();
        for (int i = 0; i < Count + 1; i++)
        {
            Vector2 p;
            if (list.Count > 0)
            {
                var e = list[Random.Range(0, list.Count)];
                Vector2 toPlayer = ((Vector2)transform.position - (Vector2)e.transform.position).normalized;
                p = (Vector2)e.transform.position + toPlayer * 0.9f;
            }
            else p = (Vector2)transform.position + Random.insideUnitCircle * 3f;
            StartCoroutine(Trap(p));
        }
        int wolves = Awakened ? 2 : 1;
        for (int w = 0; w < wolves; w++) StartCoroutine(SpiritWolf(w * 0.2f));
    }

    IEnumerator Trap(Vector2 p)
    {
        var spr = Second != null ? Second : Art.crater;
        var fx = Run.Fx(spr, p, Color.white, 7f, -7).Scale(Fit(spr, 0.8f), Fit(spr, 0.8f)).FadeFrom(0.95f);
        float t = 0f, r = 0.65f * Area;
        while (t < 7f)
        {
            t += Time.deltaTime;
            Enemy.CollectInRadius(p, r, buffer3);
            if (buffer3.Count > 0)
            {
                fx.End();
                float boom = Awakened ? 1.5f * Area : 0f;
                if (boom > 0f) { Enemy.CollectInRadius(p, boom, buffer3); Run.Fx(Art.fireBurst, p, Color.white, 0.3f, 16).Scale(0.4f, Fit(Art.fireBurst, boom * 2f)).FadeFrom(0.4f); }
                foreach (var e in buffer3) { e.Slow(1.6f, 0.05f); Hit(e, Damage, Vector2.zero, 0f); FxParticles.Debris(e.transform.position, new Color(0.7f, 0.08f, 0.1f), 4, 2.5f, 0.1f, 0.4f); }
                FxParticles.Sparks(p, new Color(1f, 0.9f, 0.6f), 10, 4f, 0.15f, 0.25f);
                if (boom > 0f) { FxParticles.Glow(p, new Color(1f, 0.6f, 0.25f), boom * 2f, 0.3f); FxParticles.Smoke(p, new Color(0.35f, 0.3f, 0.3f, 0.6f), 6, 0.6f, 1f, 0.7f, 1.5f); }
                Run.SpawnVfx(p, 0.8f, new Color(0.8f, 0.8f, 0.85f), 0.2f);
                yield break;
            }
            yield return null;
        }
    }

    IEnumerator SpiritWolf(float delay)
    {
        yield return new WaitForSeconds(delay);
        Vector2 from = transform.position;
        var target = Enemy.Nearest(from, 9f);
        if (target == null) yield break;
        Vector2 to = target.transform.position;
        var spr = Main != null ? Main : Art.ghostDagger;
        var fx = Run.Fx(spr, from, Color.white, 0.28f, 17).Move(to).Scale(Fit(spr, 1.1f), Fit(spr, 1.3f)).FadeFrom(0.85f);
        if (to.x < from.x) fx.sr.flipX = true;
        FxParticles.Smoke(from, new Color(0.5f, 0.8f, 1f, 0.5f), 4, 0.3f, 0.8f, 0.5f);
        for (float t = 0f; t < 0.28f; t += Time.deltaTime)
        {
            Vector2 at = Vector2.Lerp(from, to, (t / 0.28f) * (t / 0.28f));
            FxParticles.Sparks(at, new Color(0.5f, 0.85f, 1f), 2, 1f, 0.2f, 0.35f);
            yield return null;
        }
        FxParticles.Glow(to, new Color(0.5f, 0.85f, 1f, 0.8f), 2f, 0.3f);
        FxParticles.Sparks(to, new Color(0.6f, 0.9f, 1f), 12, 4f, 0.18f, 0.35f);
        Enemy.CollectInRadius(to, 1.0f * Area, buffer3);
        foreach (var e in buffer3) Hit(e, Damage * 0.8f, (to - from).normalized, 3f);
        Run.SpawnVfx(to, 1f, new Color(0.5f, 0.8f, 1f), 0.25f);
    }

    // ---------------------------------------------------------------- Frost: Ice Arrows / Winter Ranger

    void IceArrows()
    {
        Vector2 o = (Vector2)transform.position + Vector2.up * 0.4f;
        Vector2 dir = AimDir(14f);
        for (int i = 0; i < Count; i++)
        {
            Vector2 d = Quaternion.Euler(0, 0, (i - (Count - 1) / 2f) * 10f) * dir;
            Shoot(Main, o, d, Def.speed, Damage, Area, Pierce, Def.lifetime).Trail(new Color(0.55f, 0.85f, 1f), 0.3f * Area, 0.2f).OnHit((e, killed) =>
            {
                if (this == null) return;
                bool frozen = e.IsSlowed;
                FxParticles.Smoke(e.transform.position, new Color(0.8f, 0.95f, 1f, 0.5f), 2, 0.2f, 0.6f, 0.5f);
                if (!killed) e.Slow(1.5f, Awakened ? 0.45f : 0.6f);
                if (!frozen && !killed) return;
                Vector2 at = e.transform.position;
                int shards = Awakened ? 6 : 3;
                float off = Random.value * 360f;
                for (int s = 0; s < shards; s++)
                {
                    Vector2 sd = Quaternion.Euler(0, 0, off + s * 360f / shards) * Vector2.right;
                    Shoot(Main, at + sd * 0.4f, sd, 11f, Damage * 0.3f, Area * 0.55f, 0, 0.45f).Trail(new Color(0.6f, 0.9f, 1f), 0.15f, 0.12f);
                }
                Run.Fx(Art.frostBurst, at, new Color(1, 1, 1, 0.8f), 0.3f, 15).Scale(0.3f, Fit(Art.frostBurst, 1.4f)).FadeFrom(0.4f);
                FxParticles.Glow(at, new Color(0.5f, 0.85f, 1f, 0.8f), 1.8f, 0.3f);
                FxParticles.Debris(at, new Color(0.75f, 0.92f, 1f), 10, 5f, 0.12f, 0.45f);
            });
        }
    }

    // ---------------------------------------------------------------- Spirit: Spectral Arrows / Spirit Archer

    void SpectralArrows()
    {
        Vector2 o = (Vector2)transform.position + Vector2.up * 0.5f;
        var list = EnemiesOnScreen();
        FxParticles.Glow(o, new Color(0.7f, 0.4f, 1f, 0.7f), 1.4f, 0.25f);
        for (int i = 0; i < Count; i++)
        {
            Vector2 d = list.Count > 0 ? ((Vector2)list[Random.Range(0, list.Count)].transform.position - o).normalized : Random.insideUnitCircle.normalized;
            Shoot(Main, o, d, Def.speed, Damage, Area * (Awakened ? 1.3f : 1f), Pierce, Def.lifetime).Homing(200f)
                .Trail(new Color(0.7f, 0.4f, 1f), 0.4f * Area, 0.35f)
                .OnHit((e, k) => FxParticles.Motes(e.transform.position, new Color(0.75f, 0.5f, 1f), 3, 0.3f, 0.15f, 0.6f));
        }
    }

    // ---------------------------------------------------------------- Venom: Poison Daggers / Plaguebringer

    void PoisonDaggers()
    {
        Vector2 o = (Vector2)transform.position + Vector2.up * 0.4f;
        Vector2 dir = AimDir(12f);
        float dps = Damage * (Awakened ? 1.2f : 0.8f);
        for (int i = 0; i < Count; i++)
        {
            Vector2 d = Quaternion.Euler(0, 0, (i - (Count - 1) / 2f) * 12f) * dir;
            Shoot(Main, o, d, Def.speed, Damage, Area, Pierce, Def.lifetime).Trail(new Color(0.45f, 1f, 0.35f), 0.2f * Area, 0.15f)
                .OnHit((e, killed) => { if (!killed) e.Poison(dps, 3f); FxParticles.Smoke(e.transform.position, new Color(0.45f, 0.9f, 0.3f, 0.6f), 3, 0.25f, 0.6f, 0.6f, 0.8f); });
        }
    }

    void ToxicTrail(float dt)
    {
        trailTick -= dt;
        if (trailTick > 0f) return;
        trailTick = 0.5f;
        StartCoroutine(ToxicCloud(transform.position));
    }

    IEnumerator ToxicCloud(Vector2 p)
    {
        float r = 1.1f * Area, t = 0f, tick = 0f;
        Run.Fx(Art.poison, p, new Color(0.6f, 1f, 0.5f, 0.45f), 2.5f, -9).Scale(Fit(Art.poison, r * 1.4f), Fit(Art.poison, r * 2.2f)).FadeFrom(0.6f);
        while (t < 2.5f)
        {
            t += Time.deltaTime; tick -= Time.deltaTime;
            if (tick <= 0f)
            {
                tick = 0.5f;
                FxParticles.Smoke(p, new Color(0.4f, 0.85f, 0.3f, 0.45f), 2, r * 0.6f, 1.1f, 1f, 0.4f);
                FxParticles.Motes(p, new Color(0.5f, 1f, 0.35f), 2, r * 0.6f, 0.12f, 0.7f);
                Enemy.CollectInRadius(p, r, buffer3);
                foreach (var e in buffer3) e.Poison(Damage * 0.6f, 2f);
            }
            yield return null;
        }
    }

    // ---------------------------------------------------------------- Shadow: Shadowstep / Phantom

    void Shadowstep()
    {
        Vector2 c = transform.position;
        hitOnce.Clear();
        for (int i = 0; i < Count + 1; i++)
        {
            var e = NearestExcluding(c, 6.5f, hitOnce);
            if (e == null) break;
            hitOnce.Add(e);
            Vector2 p = e.transform.position;
            Stretch(Art.lightning, c + Vector2.up * 0.4f, p + Vector2.up * 0.3f, new Color(0.35f, 0.15f, 0.5f, 0.8f), 0.3f, 0.12f, 15);
            FxParticles.Smoke(p, new Color(0.15f, 0.08f, 0.22f, 0.75f), 5, 0.3f, 0.9f, 0.55f, 1.2f);
            FxParticles.Glow(p + Vector2.up * 0.3f, new Color(0.6f, 0.3f, 1f, 0.7f), 1.5f, 0.25f);
            var spr = Second != null ? Second : Art.starBurst;
            Run.Fx(spr, p + Vector2.up * 0.3f, Color.white, 0.25f, 17).Scale(Fit(spr, 0.6f), Fit(spr, 1.3f)).Spin(200f).FadeFrom(0.45f);
            if (Awakened && castCounter % 5 == 0 && !e.IsBoss) { Execute(e); continue; }
            bool crit = Random.value < 0.25f;
            Hit(e, crit ? Damage * 2f : Damage, (p - c).normalized, Def.knockback);
            FxParticles.Sparks(p + Vector2.up * 0.3f, crit ? new Color(1f, 0.85f, 0.4f) : new Color(0.75f, 0.45f, 1f), crit ? 14 : 7, crit ? 5f : 3.5f, 0.18f, 0.3f);
        }
    }

    // ---------------------------------------------------------------- Trickster: Decoy Clone / Mirror Master

    void Decoys()
    {
        for (int i = 0; i < Count; i++)
        {
            Vector2 p = (Vector2)transform.position + (Count == 1 ? Vector2.zero : (Vector2)(Quaternion.Euler(0, 0, i * 360f / Count) * Vector2.right * 1.6f));
            StartCoroutine(Decoy(p));
        }
    }

    IEnumerator Decoy(Vector2 p)
    {
        const float life = 3f;
        var hero = Player.HeroSprite;
        var fx = Run.Fx(hero != null ? hero : Art.ghostDagger, p + Vector2.up * 0.1f, new Color(0.75f, 0.55f, 1f, 0.75f), life, 9)
                    .Scale(hero != null ? 0.95f : 1f, hero != null ? 1f : 1f).FadeFrom(0.9f);
        Enemy.Decoys.Add(p);
        FxParticles.Smoke(p, new Color(0.45f, 0.3f, 0.6f, 0.7f), 8, 0.4f, 1f, 0.7f, 1.5f);
        float t = 0f, shot = 0f, wisp = 0f;
        while (t < life)
        {
            t += Time.deltaTime; shot -= Time.deltaTime; wisp -= Time.deltaTime;
            if (wisp <= 0f) { wisp = 0.12f; FxParticles.Motes(p + Vector2.up * 0.5f, new Color(0.75f, 0.5f, 1f), 1, 0.4f, 0.14f, 0.6f); }
            if (Awakened && shot <= 0f)
            {
                shot = 0.45f;
                var e = Enemy.Nearest(p, 8f);
                if (e != null) Shoot(Art.ghostDagger, p + Vector2.up * 0.4f, ((Vector2)e.transform.position - p).normalized, 12f, Damage * 0.35f, 0.8f, 1, 1f);
            }
            yield return null;
        }
        Enemy.Decoys.Remove(p);
        float r = 2.2f * Area;
        Enemy.CollectInRadius(p, r, buffer3);
        foreach (var e in buffer3) Hit(e, Damage, ((Vector2)e.transform.position - p).normalized, 6f);
        Run.Fx(Art.starBurst, p, new Color(0.8f, 0.6f, 1f), 0.4f, 16).Scale(0.4f, Fit(Art.starBurst, r * 2f)).FadeFrom(0.35f);
        FxParticles.Glow(p, new Color(0.8f, 0.5f, 1f), r * 2.2f, 0.35f);
        FxParticles.Sparks(p, new Color(0.85f, 0.6f, 1f), 24, 7f, 0.2f, 0.45f);
        FxParticles.Smoke(p, new Color(0.35f, 0.22f, 0.45f, 0.6f), 10, 0.6f, 1.3f, 0.9f, 2.5f);
        CameraFollow.Shake(0.06f, 0.1f);
    }

    // ---------------------------------------------------------------- Blood: Hemorrhage Blade / Vampire Lord

    void Hemorrhage()
    {
        Vector2 c = transform.position;
        float r = 2.0f * Area;
        var spr = Second != null ? Second : Art.waveRing;
        Run.Fx(spr, c + Vector2.up * 0.3f, Color.white, 0.35f, 14).Scale(Fit(spr, r * 1.2f), Fit(spr, r * 2.1f)).Spin(-500f).FadeFrom(0.5f);
        Enemy.CollectInRadius(c, r, buffer3);
        FxParticles.Glow(c + Vector2.up * 0.3f, new Color(1f, 0.15f, 0.15f, 0.55f), r * 2.2f, 0.3f, -9);
        for (int k = 0; k < 12; k++)
        {
            Vector2 d = Quaternion.Euler(0, 0, k * 30f) * Vector2.right;
            FxParticles.Debris(c + Vector2.up * 0.3f + d * r * 0.85f, new Color(0.65f, 0.05f, 0.08f), 1, 3f, 0.12f, 0.45f, d, 40f);
        }
        float dealt = 0f;
        foreach (var e in buffer3) { Hit(e, Damage, ((Vector2)e.transform.position - c).normalized, Def.knockback); dealt += Damage; FxParticles.Debris(e.transform.position, new Color(0.7f, 0.05f, 0.08f), 3, 2.5f, 0.1f, 0.4f); }
        Heal(dealt * 0.02f);
        if (dealt > 0f) FxParticles.Line((Vector2)transform.position + Vector2.up * 0.4f, (Vector2)transform.position + Vector2.up * 1.1f, new Color(1f, 0.25f, 0.3f), 6f, 0.14f, 0.8f, 0.5f);
        if (Awakened)
        {
            var list = EnemiesOnScreen();
            for (int i = 0; i < 4 && list.Count > 0; i++) StartCoroutine(Bat(list[Random.Range(0, list.Count)], i * 0.08f));
        }
    }

    IEnumerator Bat(Enemy target, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (target == null || !target.isActiveAndEnabled) yield break;
        Vector2 from = (Vector2)transform.position + Vector2.up * 0.6f, to = target.transform.position;
        var spr = Main != null ? Main : Art.ghostDagger;
        var fx = Run.Fx(spr, from, Color.white, 0.3f, 18).Move(to).Scale(Fit(spr, 0.6f), Fit(spr, 0.7f)).FadeFrom(0.9f);
        if (to.x < from.x) fx.sr.flipX = true;
        yield return new WaitForSeconds(0.3f);
        if (target != null && target.isActiveAndEnabled) { Hit(target, Damage * 0.6f, Vector2.zero, 0f); stats.Heal(0.15f); FxParticles.Debris(to, new Color(0.7f, 0.05f, 0.08f), 5, 3f, 0.1f, 0.4f); FxParticles.Sparks(to, new Color(1f, 0.3f, 0.3f), 5, 3f, 0.14f, 0.25f); }
    }

    // ---------------------------------------------------------------- Wind: Fan of Knives / Tempest Blade

    IEnumerator FanOfKnives()
    {
        int waves = Awakened ? 2 : 1;
        for (int w = 0; w < waves; w++)
        {
            Vector2 o = (Vector2)transform.position + Vector2.up * 0.4f;
            int n = Count;
            float off = castCounter * 13f + w * 180f / n;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = Quaternion.Euler(0, 0, off + i * 360f / n) * Vector2.right;
                Shoot(Main, o, d, Def.speed, Damage, Area, Pierce, Def.lifetime).Trail(new Color(0.5f, 1f, 0.9f), 0.22f * Area, 0.14f);
            }
            FxParticles.Glow(o, new Color(0.5f, 1f, 0.9f, 0.6f), 1.8f, 0.2f);
            FxParticles.Smoke(o, new Color(0.85f, 1f, 1f, 0.35f), 6, 0.3f, 0.8f, 0.4f, 3f);
            yield return new WaitForSeconds(0.15f);
        }
    }

    // ---------------------------------------------------------------- Paladin: Holy Shield / Crusader

    void HolyPulse()
    {
        Vector2 c = transform.position;
        float r = 2.4f * Area;
        var spr = Main != null ? Main : Art.waveRing;
        Run.Fx(spr, c + Vector2.up * 0.2f, Color.white, 0.5f, -8).Scale(Fit(spr, 0.6f), Fit(spr, r * 2.1f)).FadeFrom(0.4f);
        Enemy.CollectInRadius(c, r, buffer3);
        foreach (var e in buffer3) { Hit(e, Damage, ((Vector2)e.transform.position - c).normalized, Def.knockback); FxParticles.Sparks(e.transform.position, new Color(1f, 0.9f, 0.5f), 3, 2.5f, 0.15f, 0.3f); }
        stats.Heal(Awakened ? 1.2f : 0.6f);
        FxParticles.Glow(c, new Color(1f, 0.85f, 0.4f, 0.6f), r * 2.4f, 0.45f, -9);
        FxParticles.Motes(c, new Color(1f, 0.92f, 0.55f), 14, r * 0.8f, 0.17f, 0.9f);
        FxParticles.Motes(c + Vector2.up * 0.4f, new Color(0.6f, 1f, 0.6f), 4, 0.4f, 0.14f, 0.7f);
    }

    void BurnAura(float dt)
    {
        auraTick -= dt;
        if (auraTick > 0f) return;
        auraTick = 0.4f;
        Vector2 c = transform.position;
        float r = 2.0f * Area;
        Run.Fx(Main != null ? Main : Art.waveRing, c + Vector2.up * 0.2f, new Color(1f, 0.9f, 0.5f, 0.35f), 0.4f, -9)
           .Scale(Fit(Main != null ? Main : Art.waveRing, r * 2f), Fit(Main != null ? Main : Art.waveRing, r * 2.05f)).Spin(40f);
        Enemy.CollectInRadius(c, r, buffer3);
        foreach (var e in buffer3) { Hit(e, Damage * 0.35f, Vector2.zero, 0f); FxParticles.Motes(e.transform.position, new Color(1f, 0.7f, 0.3f), 1, 0.2f, 0.14f, 0.5f); }
        FxParticles.Motes(c, new Color(1f, 0.9f, 0.55f), 3, r * 0.9f, 0.14f, 0.7f);
    }

    // ---------------------------------------------------------------- Bulwark: Shield Bash / Living Fortress

    void ShieldBash()
    {
        Vector2 c = transform.position;
        Vector2 dir = AimDir(5f);
        float range = 2.2f * Area;
        if (Awakened) Enemy.CollectInRadius(c, range, buffer3);
        else CollectCone(c, dir, range, 120f, buffer3);
        foreach (var e in buffer3)
        {
            Hit(e, Damage, ((Vector2)e.transform.position - c).normalized, 9f);
            e.Slow(1f, 0.1f);
            FxParticles.Sparks(e.transform.position + Vector3.up * 0.3f, new Color(1f, 0.95f, 0.6f), 5, 4f, 0.15f, 0.25f, ((Vector2)e.transform.position - c).normalized, 80f);
        }
        FxParticles.Glow(c + dir * range * 0.4f, new Color(0.45f, 0.65f, 1f, 0.7f), range * 1.6f, 0.25f);
        FxParticles.Sparks(c + dir * range * 0.5f, new Color(0.6f, 0.8f, 1f), 12, 6f, 0.17f, 0.3f, dir, Awakened ? 360f : 110f, 0.3f);
        var spr = Main != null ? Main : Art.wave;
        int arcs = Awakened ? 4 : 1;
        for (int i = 0; i < arcs; i++)
        {
            Vector2 d = Quaternion.Euler(0, 0, i * 90f) * dir;
            Run.Fx(spr, c + d * range * 0.5f + Vector2.up * 0.3f, Color.white, 0.25f, 16).Rotate(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg)
               .Scale(Fit(spr, range * 0.6f), Fit(spr, range * 1.1f)).FadeFrom(0.4f);
        }
        CameraFollow.Shake(0.04f, 0.08f);
    }

    void ContactDamage(float dt)
    {
        contactTick -= dt;
        if (contactTick > 0f) return;
        contactTick = 0.3f;
        Enemy.CollectInRadius(transform.position, 0.95f, buffer3);
        foreach (var e in buffer3) { Hit(e, Damage * 0.4f, ((Vector2)e.transform.position - (Vector2)transform.position).normalized, 4f); FxParticles.Sparks(e.transform.position, new Color(0.6f, 0.8f, 1f), 3, 3f, 0.13f, 0.2f); }
    }

    // ---------------------------------------------------------------- Thorns: Bramble Armor / Ironwood

    void UpdateBrambles(float dt)
    {
        // ring of thorns that hurts anything close
        auraTick -= dt;
        Vector2 c = transform.position;
        float r = 1.5f * Area;
        if (auraTick <= 0f)
        {
            auraTick = 0.5f;
            Enemy.CollectInRadius(c, r, buffer3);
            foreach (var e in buffer3) { Hit(e, Damage, ((Vector2)e.transform.position - c).normalized, 1f); FxParticles.Debris(e.transform.position, new Color(0.4f, 0.6f, 0.25f), 2, 2f, 0.1f, 0.35f); }
            var spr = Main != null ? Main : Art.waveRing;
            Run.Fx(spr, c + Vector2.up * 0.15f, new Color(1f, 1f, 1f, 0.8f), 0.5f, -9).Scale(Fit(spr, r * 2f), Fit(spr, r * 2.1f)).Spin(30f).FadeFrom(0.7f);
        }
        // getting hit releases a thorn burst
        thornsReady -= dt;
        float h = stats.Health;
        if (lastHealth >= 0f && h < lastHealth - 0.01f && thornsReady <= 0f)
        {
            thornsReady = 0.5f;
            float br = 3f * Area;
            Enemy.CollectInRadius(c, br, buffer3);
            foreach (var e in buffer3) Hit(e, Damage * 3f, ((Vector2)e.transform.position - c).normalized, 4f);
            FxParticles.Debris(c, new Color(0.35f, 0.55f, 0.2f), 22, 7f, 0.15f, 0.55f);
            FxParticles.Debris(c, new Color(0.45f, 0.32f, 0.18f), 12, 6f, 0.11f, 0.5f);
            FxParticles.Glow(c, new Color(0.6f, 1f, 0.35f, 0.5f), br * 2f, 0.3f, -9);
            CameraFollow.Shake(0.05f, 0.1f);
            var spr = Main != null ? Main : Art.waveRing;
            Run.Fx(spr, c, Color.white, 0.4f, 15).Scale(Fit(spr, 1f), Fit(spr, br * 2f)).Spin(120f).FadeFrom(0.3f);
        }
        lastHealth = h;
    }

    void Ironwood()
    {
        var list = EnemiesOnScreen();
        for (int i = 0; i < 6 && list.Count > 0; i++)
        {
            int k = Random.Range(0, list.Count);
            Vector2 p = list[k].transform.position; list.RemoveAt(k);
            Run.Fx(Art.iceSpikes, p + Vector2.up * 0.25f, new Color(0.55f, 0.75f, 0.35f), 0.8f, 11).Scale(0.15f, Fit(Art.iceSpikes, 1.1f)).FadeFrom(0.65f);
            Enemy.CollectInRadius(p, 0.8f * Area, buffer3);
            foreach (var e in buffer3) { Hit(e, Damage * 2f, Vector2.up, 0f); e.Slow(1.2f, 0.1f); }
            FxParticles.Debris(p, new Color(0.45f, 0.32f, 0.18f), 8, 4f, 0.13f, 0.5f, Vector2.up, 120f);
            FxParticles.Debris(p, new Color(0.35f, 0.6f, 0.2f), 5, 3f, 0.12f, 0.6f, Vector2.up, 160f);
        }
    }

    // ---------------------------------------------------------------- Juggernaut: Charge / Unstoppable

    IEnumerator Charge()
    {
        Vector2 dir = Player.Facing;
        const float dur = 0.3f;
        Player.Dash(dir * 14f, dur);
        stats.Invulnerable(dur + 0.1f);
        hitOnce.Clear();
        float t = 0f, trail = 0f;
        bool killed = false;
        while (t < dur)
        {
            t += Time.deltaTime; trail -= Time.deltaTime;
            Vector2 c = transform.position;
            Enemy.CollectInRadius(c, 1.1f * Area, buffer3);
            foreach (var e in buffer3) if (hitOnce.Add(e)) killed |= Hit(e, Damage, dir, 8f);
            if (Awakened && trail <= 0f)
            {
                trail = 0.06f;
                Run.Fx(Art.crater, c, Color.white, 0.6f, -10).Scale(0.6f, Fit(Art.crater, 1.8f * Area)).FadeFrom(0.4f);
                Enemy.CollectInRadius(c, 1.4f * Area, buffer3);
                foreach (var e in buffer3) killed |= Hit(e, Damage * 0.5f, Vector2.zero, 0f);
            }
            Run.Fx(Player.HeroSprite != null ? Player.HeroSprite : Art.ghostDagger, c + Vector2.up * 0.1f, new Color(0.5f, 0.7f, 1f, 0.45f), 0.25f, 8).Additive();
            FxParticles.Smoke(c, new Color(0.55f, 0.5f, 0.45f, 0.5f), 1, 0.3f, 0.8f, 0.6f, 0.8f);
            FxParticles.Sparks(c + dir * 0.6f + Vector2.up * 0.4f, new Color(0.6f, 0.8f, 1f), 2, 3f, 0.14f, 0.2f, -dir, 60f);
            yield return null;
        }
        Vector2 end = transform.position;
        float r = 1.8f * Area;
        Enemy.CollectInRadius(end, r, buffer3);
        foreach (var e in buffer3) killed |= Hit(e, Damage * 0.8f, ((Vector2)e.transform.position - end).normalized, 6f);
        var spr = Main != null ? Main : Art.waveRing;
        Run.Fx(spr, end, Color.white, 0.35f, -8).Scale(0.4f, Fit(spr, r * 2f)).FadeFrom(0.4f);
        FxParticles.Glow(end, new Color(0.45f, 0.65f, 1f, 0.7f), r * 2.2f, 0.3f, -9);
        FxParticles.Debris(end, new Color(0.5f, 0.42f, 0.35f), 16, 6f, 0.14f, 0.5f);
        FxParticles.Smoke(end, new Color(0.55f, 0.5f, 0.45f, 0.55f), 8, r * 0.5f, 1.2f, 0.8f, 2.2f);
        CameraFollow.Shake(0.07f, 0.12f);
        if (Awakened && killed) timer = Mathf.Min(timer, Cooldown * 0.5f);
    }

    // ---------------------------------------------------------------- Death Knight: Cursed Blade / Dread Lord

    IEnumerator CursedSweeps()
    {
        int sweeps = Count;
        for (int s = 0; s < sweeps; s++)
        {
            Vector2 c = transform.position;
            float r = 2.2f * Area;
            var sweep = Second != null ? Second : Art.waveRing;
            Run.Fx(sweep, c + Vector2.up * 0.3f, Color.white, 0.35f, 14).Scale(Fit(sweep, r * 1.4f), Fit(sweep, r * 2.1f)).Spin(s % 2 == 0 ? -720f : 720f).FadeFrom(0.5f);
            var sword = Main;
            if (sword != null)
                Run.Fx(sword, c + Vector2.up * 0.3f, new Color(1f, 1f, 1f, 0.9f), 0.35f, 16).Scale(Fit(sword, r * 0.8f), Fit(sword, r * 0.85f)).Spin(s % 2 == 0 ? -1030f : 1030f).FadeFrom(0.7f);
            FxParticles.Glow(c + Vector2.up * 0.3f, new Color(0.55f, 0.2f, 0.9f, 0.55f), r * 2.2f, 0.35f, -9);
            for (int k = 0; k < 14; k++)
            {
                Vector2 d = Quaternion.Euler(0, 0, k * 360f / 14f) * Vector2.right;
                FxParticles.Sparks(c + Vector2.up * 0.3f + d * r * 0.85f, new Color(0.7f, 0.35f, 1f), 1, 2.5f, 0.17f, 0.4f, Vector2.Perpendicular(d) * (s % 2 == 0 ? -1f : 1f), 30f);
            }
            FxParticles.Smoke(c, new Color(0.15f, 0.06f, 0.2f, 0.55f), 6, r * 0.7f, 1.1f, 0.7f, 1f);
            Enemy.CollectInRadius(c, r, buffer3);
            int heals = 0;
            foreach (var e in buffer3)
            {
                e.Curse(3f);
                FxParticles.Motes(e.transform.position, new Color(0.65f, 0.3f, 0.95f), 2, 0.2f, 0.13f, 0.6f);
                if (Hit(e, Damage, ((Vector2)e.transform.position - c).normalized, Def.knockback) && Awakened && heals < 3) { heals++; stats.Heal(0.5f); }
            }
            yield return new WaitForSeconds(0.3f);
        }
    }

    void FrostAura()
    {
        Enemy.CollectInRadius(transform.position, 2.5f * Area, buffer3);
        foreach (var e in buffer3) e.Slow(0.2f, 0.6f);
        if (Random.value < 0.5f) FxParticles.Smoke((Vector2)transform.position + Random.insideUnitCircle * 2.2f * Area, new Color(0.7f, 0.9f, 1f, 0.3f), 1, 0.2f, 1f, 0.9f, 0.3f);
    }
}

