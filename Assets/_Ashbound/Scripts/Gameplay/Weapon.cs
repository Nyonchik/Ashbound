using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>One owned weapon or Path skill. Level 1-5, then 6 = Awakened (Path skills) or Evolved (class weapons).</summary>
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
    public Sprite Icon => Awakened && Def.awakenIcon != null ? Def.awakenIcon : Def.icon;
    public string DisplayName => Awakened && !string.IsNullOrEmpty(Def.awakeningName) ? Def.awakeningName : Def.weaponName;

    /// <summary>Weapon at Lv 5 and its paired passive at Lv 5 (max).</summary>
    public bool CanEvolve(Func<PassiveDef, int> passiveLevel) =>
        !IsPath && !IsEvolved && Level >= 5 && Def.evolvesInto != null && Def.evolvePassive != null && passiveLevel(Def.evolvePassive) >= 5;

    static readonly List<Enemy> buffer = new();
    static readonly List<Enemy> buffer2 = new();
    static readonly HashSet<Enemy> chainHit = new();
    readonly List<SpriteRenderer> orbs = new();
    readonly Dictionary<Enemy, float> orbHitTimes = new();
    SpriteRenderer tornado;
    Sprite orbArt;

    PlayerStats stats;
    LevelBonus bonus;
    float timer, orbAngle, tick;

    float Damage => Def.damage * (1f + bonus.damage) * stats.damageMul;
    float Cooldown => Mathf.Max(0.1f, Def.cooldown * Mathf.Max(0.2f, 1f - bonus.cooldown) * stats.cooldownMul);
    float Area => Def.area * (1f + bonus.area) * stats.areaMul;
    int Count => Def.count + bonus.count;
    int Pierce => Def.pierce + bonus.pierce;
    bool Explodes => Def.explode || bonus.explode;

    static RunManager Run => RunManager.Instance;
    static RunManager.FxArt Art => Run.Art;
    Sprite ProjArt => Awakened && Def.awakenProjectile != null ? Def.awakenProjectile : Def.projectile;
    Color AwakeTint => Awakened ? Color.Lerp(Def.color, Palette.Gold, 0.45f) : Def.color;

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
        ClearPersistent();
        Def = evolved;
        IsEvolved = true;
        Level = 5;
        timer = 0.3f;
        Refresh();
    }

    public void LevelUp()
    {
        if (!CanLevel) return;
        Level++;
        Refresh();
    }

    void ClearPersistent()
    {
        foreach (var o in orbs) if (o != null) Destroy(o.gameObject);
        orbs.Clear();
        orbHitTimes.Clear();
        if (tornado != null) Destroy(tornado.gameObject);
        tornado = null;
    }

    void OnDestroy() => ClearPersistent();

    /// <summary>Recompute stats (call after level-ups and passive changes).</summary>
    public void Refresh()
    {
        bonus = default;
        if (!IsEvolved)
            for (int i = 0; i < Mathf.Min(Level - 1, Def.levels.Length); i++) Add(ref bonus, Def.levels[i]);
        if (Awakened) Add(ref bonus, Def.awakening);
        if (Run == null) return;

        switch (Def.style)
        {
            case WeaponStyle.PhantomBlades: BuildOrbs(2, Art.ghostDagger, 0.85f); break;
            case WeaponStyle.Whirlwind:
                BuildOrbs(Mathf.Max(3, Count), ProjArt, 0.8f);
                if (tornado == null)
                {
                    tornado = Run.CreateOrb(null, Color.white, Art.tornado);
                    tornado.name = "Tornado";
                    tornado.sortingOrder = 2;
                }
                break;
            default:
                if (Def.kind == WeaponKind.Orbit) BuildOrbs(Count, ProjArt, Awakened ? 1.3f : 1.1f);
                break;
        }
    }

    void BuildOrbs(int n, Sprite art, float size)
    {
        if (orbArt != art) { foreach (var o in orbs) if (o != null) Destroy(o.gameObject); orbs.Clear(); orbArt = art; }
        while (orbs.Count < n) orbs.Add(Run.CreateOrb(transform, Def.color, art));
        foreach (var o in orbs) o.transform.localScale = Vector3.one * size * Area;
    }

    static void Add(ref LevelBonus a, LevelBonus b)
    {
        a.damage += b.damage; a.count += b.count; a.cooldown += b.cooldown;
        a.area += b.area; a.pierce += b.pierce; a.explode |= b.explode;
    }

    void Update()
    {
        var run = Run;
        if (Def == null || stats.IsDead || run == null || run.IsOver) return;

        switch (Def.style)
        {
            case WeaponStyle.Whirlwind: UpdateWhirlwind(); return;
            case WeaponStyle.PhantomBlades: UpdateOrbs(1.7f, true); break;
        }
        if (Def.kind == WeaponKind.Orbit && Def.style == WeaponStyle.Default) { UpdateOrbs(1.35f, false); return; }

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Cooldown;
        switch (Def.style)
        {
            case WeaponStyle.Chain: CastChain(); break;
            case WeaponStyle.BlackHole: CastBlackHoles(); break;
            case WeaponStyle.Starfall: StartCoroutine(StarVolley()); break;
            case WeaponStyle.Glacier: Glacier(); break;
            case WeaponStyle.PhantomBlades: FireProjectiles(Art.ghostDagger, true, 22f); break;
            case WeaponStyle.Wave: StartCoroutine(Waves()); break;
            case WeaponStyle.Hammer: StartCoroutine(HammerSlam()); break;
            case WeaponStyle.Frost: Nova(1.2f, 0.55f); break;
            default:
                if (Def.kind == WeaponKind.Projectile) FireProjectiles(ProjArt, false, 12f);
                else if (Def.kind == WeaponKind.Nova) Nova(0f, 1f);
                break;
        }
    }

    // ---------------- helpers

    void HitAll(Vector2 c, float r, float dmg, float knock, List<Enemy> list)
    {
        Enemy.CollectInRadius(c, r, list);
        foreach (var e in list) e.TakeDamage(dmg, ((Vector2)e.transform.position - c).normalized, knock);
    }

    static Enemy NearestExcluding(Vector2 p, float maxDist, HashSet<Enemy> exclude)
    {
        Enemy best = null; float bestD = maxDist * maxDist;
        foreach (var e in Enemy.Active)
        {
            if (exclude.Contains(e)) continue;
            float d = ((Vector2)e.transform.position - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    static readonly List<Enemy> onScreen = new();
    static List<Enemy> EnemiesOnScreen()
    {
        onScreen.Clear();
        var cam = Camera.main;
        if (cam == null) return onScreen;
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        foreach (var e in Enemy.Active)
        {
            Vector2 d = (Vector2)e.transform.position - c;
            if (Mathf.Abs(d.x) < w - 0.3f && Mathf.Abs(d.y) < h - 0.3f) onScreen.Add(e);
        }
        return onScreen;
    }

    /// <summary>Stretched lightning sprite from a to b.</summary>
    public static void Bolt(Vector2 a, Vector2 b, Color c, float width = 0.55f, float seconds = 0.18f)
    {
        var s = Art.lightning;
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.05f) return;
        float sw = s != null ? s.bounds.size.x : 1f, sh = s != null ? s.bounds.size.y : 1f;
        var sc = new Vector3(len / sw, width / sh * (Random.value < 0.5f ? 1f : -1f), 1f);
        Run.Fx(s, (a + b) * 0.5f, c, seconds, 17).Rotate(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg).Scale(sc, sc).FadeFrom(0.35f);
    }

    // ---------------- Projectile

    void FireProjectiles(Sprite art, bool boomerang, float spread)
    {
        Vector2 origin = transform.position;
        var target = Enemy.Nearest(origin, 14f);
        Vector2 dir = target != null ? ((Vector2)target.transform.position - origin).normalized : PlayerController.Instance.Facing;
        int n = Count;
        for (int i = 0; i < n; i++)
        {
            float angle = (i - (n - 1) / 2f) * spread;
            Vector2 d = Quaternion.Euler(0f, 0f, angle) * dir;
            var p = Run.GetProjectile();
            p.Launch(this, origin, d, Def.speed, Damage, Area, Pierce, Def.lifetime, Def.knockback, Explodes, AwakeTint, art);
            if (boomerang) p.MakeBoomerang(transform);
        }
    }

    public void Explode(Vector2 position, float damage)
    {
        float r = 1.0f * Area;
        HitAll(position, r, damage, 2f, buffer);
        if (Def.effect != null) Run.Fx(Def.effect, position, Color.white, 0.35f, 16).Scale(Mathf.Min(r * 0.8f, 1.2f), Mathf.Min(r * 2.1f, 3f)).FadeFrom(0.4f);
        else Run.SpawnVfx(position, r, Def.color, 0.3f);
    }

    // ---------------- Nova (Frost Nova, Thunder Hammer base etc.)

    void Nova(float slowSeconds, float slowMul)
    {
        float r = 2.2f * Area;
        Vector2 c = transform.position;
        Enemy.CollectInRadius(c, r, buffer);
        foreach (var e in buffer)
        {
            e.TakeDamage(Damage, ((Vector2)e.transform.position - c).normalized, Def.knockback);
            if (slowSeconds > 0f) e.Slow(slowSeconds, slowMul);
        }
        if (Def.effect != null) Run.Fx(Def.effect, c, new Color(1f, 1f, 1f, 0.8f), 0.45f, -9).Scale(Mathf.Min(r * 0.6f, 2f), Mathf.Min(r * 2f, 6f)).FadeFrom(0.35f);
        else Run.SpawnVfx(c, r, Def.color, 0.45f);
        if (Explodes)
            foreach (var e in buffer)
                if (e != null && e.isActiveAndEnabled) { Explode(e.transform.position, Damage * 0.4f); break; }
    }

    // ---------------- Chain Lightning: hits one enemy, then jumps to its neighbours

    void CastChain()
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.6f;
        chainHit.Clear();
        int hits = 1 + Pierce;
        float range = 3.6f * Mathf.Max(1f, Area);
        Color col = Awakened ? new Color(1f, 0.93f, 0.55f) : Color.white;
        float width = Awakened ? 0.8f : 0.55f;

        for (int b = 0; b < Count; b++)
        {
            var cur = NearestExcluding(origin, 9f, chainHit);
            if (cur == null) break;
            Vector2 from = origin;
            float dmg = Damage;
            for (int h = 0; h < hits && cur != null; h++)
            {
                chainHit.Add(cur);
                Vector2 to = (Vector2)cur.transform.position + Vector2.up * (cur.Radius * 1.2f);
                Bolt(from, to, col, width);
                Run.SpawnVfx(to, 0.4f, col, 0.15f);
                cur.TakeDamage(dmg, (to - from).normalized, Def.knockback);
                from = to;
                dmg *= 0.85f;
                cur = NearestExcluding(from, range, chainHit);
            }
        }

        if (Awakened)   // Thunderstorm Heart: extra strikes from the sky
        {
            var list = EnemiesOnScreen();
            for (int i = 0; i < 3 && list.Count > 0; i++)
            {
                var e = list[Random.Range(0, list.Count)];
                Vector2 p = e.transform.position;
                Bolt(p + new Vector2(Random.Range(-0.6f, 0.6f), 7f), p, col, 0.9f, 0.22f);
                Run.SpawnVfx(p, 0.9f, col, 0.25f);
                HitAll(p, 0.9f, Damage * 1.2f, 1f, buffer);
            }
        }
    }

    // ---------------- Black Hole: opens on the biggest crowd, pulls for ~2s, then explodes

    void CastBlackHoles()
    {
        for (int i = 0; i < Count; i++)
        {
            var spot = DensestSpot(9f);
            if (spot.HasValue) StartCoroutine(BlackHole(spot.Value + Random.insideUnitCircle * (i * 0.8f)));
        }
    }

    Vector2? DensestSpot(float range)
    {
        Vector2 origin = transform.position;
        Vector2? best = null; int bestN = 0, samples = 0;
        float r2 = range * range;
        foreach (var e in Enemy.Active)
        {
            Vector2 p = e.transform.position;
            if ((p - origin).sqrMagnitude > r2) continue;
            if (samples++ > 30) break;
            int n = 0;
            foreach (var o in Enemy.Active) if (((Vector2)o.transform.position - p).sqrMagnitude < 6.25f) n++;
            if (n > bestN) { bestN = n; best = p; }
        }
        return best;
    }

    IEnumerator BlackHole(Vector2 c)
    {
        const float life = 2.1f;
        float r = 1.3f * Area, pullR = 3.8f * Area, size = 2.6f * Area, t = 0f, dmgTick = 0f;
        Run.Fx(Art.blackHole, c, Color.white, life, 13).Scale(0.2f, size).Spin(-280f).FadeFrom(1f);
        while (t < life)
        {
            t += Time.deltaTime; dmgTick -= Time.deltaTime;
            Enemy.CollectInRadius(c, pullR, buffer2);
            foreach (var e in buffer2)
            {
                Vector2 d = c - (Vector2)e.transform.position;
                float dist = d.magnitude;
                if (dist > 0.2f) e.Pull(d / dist * Mathf.Lerp(8f, 3f, dist / pullR));
            }
            if (dmgTick <= 0f) { dmgTick = 0.25f; HitAll(c, r, Damage * 0.3f, 0f, buffer2); }
            yield return null;
        }
        float boom = 3f * Area;
        HitAll(c, boom, Damage * 2.5f, 7f, buffer2);
        Run.Fx(Art.blackHole, c, Color.white, 0.25f, 13).Scale(size, 0.1f).Spin(-600f);
        Run.Fx(Art.starBurst, c, new Color(0.75f, 0.5f, 1f), 0.45f, 15).Scale(boom * 0.5f, boom * 2.2f).FadeFrom(0.3f);
        CameraFollow.Shake(0.12f, 0.2f);
    }

    // ---------------- Starfall: volleys of stars on random enemies on screen

    IEnumerator StarVolley()
    {
        var list = EnemiesOnScreen();
        var picks = new List<Vector2>();
        for (int i = 0; i < Count; i++)
        {
            if (list.Count > 0) { int k = Random.Range(0, list.Count); picks.Add(list[k].transform.position); list.RemoveAt(k); }
            else picks.Add((Vector2)transform.position + Random.insideUnitCircle * 4f);
        }
        foreach (var p in picks)
        {
            StartCoroutine(Star(p));
            yield return new WaitForSeconds(0.07f);
        }
    }

    IEnumerator Star(Vector2 p)
    {
        const float fall = 0.38f;
        Vector2 start = p + new Vector2(1.4f, 7.5f);
        Vector2 d = p - start;
        Run.Fx(Art.fallingStar, start, Color.white, fall, 18).Move(p).Scale(0.9f * Area, 1.1f * Area)
           .Rotate(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f).FadeFrom(1f);
        yield return new WaitForSeconds(fall);
        float r = 1.2f * Area;
        HitAll(p, r, Damage, 3f, buffer2);
        Run.Fx(Art.starBurst, p, Color.white, 0.35f, 16).Scale(Mathf.Min(r * 0.6f, 1f), Mathf.Min(r * 2f, 2.6f)).FadeFrom(0.35f);
    }

    // ---------------- Glacier: ice spikes erupt in rings and slow everything

    void Glacier()
    {
        Vector2 c = transform.position;
        float r = 2.6f * Area;
        Run.Fx(Art.frostBurst, c, new Color(1f, 1f, 1f, 0.75f), 0.5f, -9).Scale(0.6f, Mathf.Min(r * 2.1f, 6f)).FadeFrom(0.3f);
        SpikeRing(c, 7, r * 0.45f, r);
        SpikeRing(c, 12, r * 0.9f, r);
        Enemy.CollectInRadius(c, r, buffer);
        foreach (var e in buffer)
        {
            e.TakeDamage(Damage, ((Vector2)e.transform.position - c).normalized, Def.knockback);
            e.Slow(2.2f, 0.25f);
        }
    }

    void SpikeRing(Vector2 c, int n, float radius, float maxR)
    {
        float off = Random.value * 360f;
        for (int i = 0; i < n; i++)
        {
            float a = (off + i * 360f / n) * Mathf.Deg2Rad;
            Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            StartCoroutine(Spike(p, radius / maxR * 0.12f));
        }
    }

    IEnumerator Spike(Vector2 p, float delay)
    {
        yield return new WaitForSeconds(delay);
        float s = 0.9f * Mathf.Max(1f, Area * 0.8f);
        Run.Fx(Art.iceSpikes, p + Vector2.up * 0.25f, Color.white, 0.8f, 11).Scale(0.15f, s).FadeFrom(0.65f);
    }

    // ---------------- Crimson Whirlwind: one giant tornado around you

    void UpdateWhirlwind()
    {
        Vector2 c = transform.position;
        float t = Time.time;
        if (tornado != null)
        {
            float s = 4.4f * Area;
            tornado.transform.position = c + new Vector2(Mathf.Sin(t * 3f) * 0.12f, s * 0.36f);
            tornado.transform.localScale = new Vector3(s * (1f + Mathf.Sin(t * 13f) * 0.05f), s, 1f);
            tornado.color = new Color(1f, 1f, 1f, 0.6f);
        }
        int n = orbs.Count;
        orbAngle += Def.speed * 45f * Time.deltaTime;
        float radius = 1.1f * Area * (1f + Mathf.Sin(t * 2.2f) * 0.3f);
        for (int i = 0; i < n; i++)
        {
            float a = (orbAngle + i * 360f / n) * Mathf.Deg2Rad;
            var orb = orbs[i].transform;
            orb.localPosition = new Vector3(Mathf.Cos(a) * radius, 0.9f + Mathf.Sin(a) * radius * 0.45f + i * 0.25f, 0f);
            orb.Rotate(0f, 0f, 720f * Time.deltaTime);
        }
        tick -= Time.deltaTime;
        if (tick <= 0f)
        {
            tick = 0.3f;
            HitAll(c, 2.6f * Area, Damage, Def.knockback, buffer);
        }
    }

    // ---------------- Orbit (orbs, axes, shields, phantom daggers)

    void UpdateOrbs(float radiusMul, bool pointAlong)
    {
        int n = orbs.Count;
        if (n == 0) return;
        orbAngle += Def.speed * 40f * Time.deltaTime;
        float radius = radiusMul * Area;
        float now = Time.time;

        for (int i = 0; i < n; i++)
        {
            float a = (orbAngle + i * 360f / n) * Mathf.Deg2Rad;
            var orb = orbs[i].transform;
            orb.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
            if (pointAlong) orb.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
            else orb.Rotate(0f, 0f, 540f * Time.deltaTime);

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

    // ---------------- Tidal Wave: crescent waves roll outward

    IEnumerator Waves()
    {
        int waves = Awakened ? 2 : 1;
        for (int w = 0; w < waves; w++)
        {
            Vector2 c = transform.position;
            float r = 2.3f * Area * (w == 1 ? 1.3f : 1f);
            // a real wave: a ring of rushing water that bursts out from the hero, with a smaller trailing ripple
            var ring = Art.waveRing != null ? Art.waveRing : Art.wave;
            Run.Fx(ring, c, Color.white, 0.6f, -8).Scale(0.4f, r * 2.15f).Spin(-70f).FadeFrom(0.55f);
            StartCoroutine(Ripple(ring, c, r));
            HitAll(c, r, Damage, Def.knockback, buffer);
            yield return new WaitForSeconds(0.35f);
        }
    }

    IEnumerator Ripple(Sprite ring, Vector2 c, float r)
    {
        yield return new WaitForSeconds(0.12f);
        Run.Fx(ring, c, new Color(1f, 1f, 1f, 0.55f), 0.5f, -8).Scale(0.3f, r * 1.5f).Spin(90f).FadeFrom(0.4f);
    }

    // ---------------- Thunder Hammer: ground slam with cracks and lightning

    IEnumerator HammerSlam()
    {
        int slams = Awakened ? 2 : 1;
        for (int s = 0; s < slams; s++)
        {
            Vector2 c = transform.position;
            float r = 2.2f * Area * (s == 1 ? 1.35f : 1f);
            Run.Fx(Art.crater, c, Color.white, 1.0f, -10).Scale(r * 1.4f, r * 1.9f).FadeFrom(0.5f);
            Enemy.CollectInRadius(c, r, buffer);
            int strikes = 0;
            foreach (var e in buffer)
            {
                Vector2 p = e.transform.position;
                e.TakeDamage(Damage, (p - c).normalized, Def.knockback);
                if (strikes < (Awakened ? 6 : 3) && e.isActiveAndEnabled)
                {
                    strikes++;
                    Bolt(p + new Vector2(Random.Range(-0.5f, 0.5f), 6f), p, new Color(0.6f, 0.95f, 1f), 0.7f, 0.2f);
                    e.TakeDamage(Damage * 0.5f, Vector2.zero, 0f);
                }
            }
            if (Explodes && buffer.Count > 0 && buffer[0] != null) Explode(buffer[0].transform.position, Damage * 0.5f);
            CameraFollow.Shake(0.08f, 0.12f);
            yield return new WaitForSeconds(0.35f);
        }
    }

    // ---------------- Level-up card text

    public string NextLevelText => Def.levelText != null && Level < Def.levelText.Length ? Def.levelText[Level] : "";
}
