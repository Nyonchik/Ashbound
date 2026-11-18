using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] Enemy enemyPrefab;
    [SerializeField] int maxAlive = 400;

    [SerializeField] EnemyType[] types =
    {
        new() { name = "Hollow Walker", health = 10, speed = 1.4f, damage = 4, scale = 1f, xp = 1, tint = Color.white, startTime = 0, weight = 10 },
        new() { name = "Hollow Hound", health = 6, speed = 2.5f, damage = 4, scale = 0.8f, xp = 2, tint = new Color(1f, 0.88f, 0.75f), startTime = 60, weight = 5 },
        new() { name = "Hollow Brute", health = 45, speed = 1.0f, damage = 9, scale = 1.6f, xp = 5, tint = new Color(0.78f, 0.84f, 0.78f), startTime = 150, weight = 2 },
    };

    [Tooltip("3 bosses per stage, in stage order: Ashen Fields, Hollow Forest, Ruined Capital")]
    [SerializeField] EnemyType[] bosses =
    {
        // Ashen Fields
        new() { name = "The Ash Warden", title = "Keeper of the Ember Gate", health = 2500, speed = 1.3f, damage = 16, scale = 3f, xp = 40, tint = new Color(0.75f, 0.75f, 0.85f), isBoss = true, startTime = 300 },
        new() { name = "The Rotting Matriarch", title = "Mother of the Rot", health = 7000, speed = 1.5f, damage = 20, scale = 3.2f, xp = 80, tint = new Color(0.85f, 0.7f, 0.9f), isBoss = true, startTime = 600 },
        new() { name = "The Hollow King", title = "Lord of the Hollow", health = 16000, speed = 1.4f, damage = 26, scale = 3.6f, xp = 150, tint = new Color(0.62f, 0.58f, 0.8f), isBoss = true, startTime = 900 },
        // Hollow Forest
        new() { name = "The Elder Treant", title = "Heart of the Rotwood", health = 2500, speed = 1.0f, damage = 16, scale = 3.2f, xp = 40, tint = new Color(0.62f, 0.85f, 0.55f), isBoss = true, startTime = 300 },
        new() { name = "Gray Fang", title = "Alpha of the Hollow Pack", health = 6000, speed = 2.1f, damage = 18, scale = 2.8f, xp = 80, tint = new Color(0.75f, 0.85f, 0.75f), isBoss = true, startTime = 600 },
        new() { name = "The Mycelium Colossus", title = "Bloom of a Thousand Spores", health = 16000, speed = 1.1f, damage = 26, scale = 3.8f, xp = 150, tint = new Color(0.7f, 0.9f, 0.6f), isBoss = true, startTime = 900 },
        // Ruined Capital
        new() { name = "The Fallen Champion", title = "Last Knight of the Capital", health = 2500, speed = 1.6f, damage = 18, scale = 3f, xp = 40, tint = new Color(0.7f, 0.78f, 0.95f), isBoss = true, startTime = 300 },
        new() { name = "The Plague Alchemist", title = "Brewer of the Black Rot", health = 6500, speed = 1.3f, damage = 20, scale = 3f, xp = 80, tint = new Color(0.75f, 0.9f, 0.75f), isBoss = true, startTime = 600 },
        new() { name = "The Hollow Emperor", title = "Crown of Ash and Bone", health = 16000, speed = 1.4f, damage = 28, scale = 3.8f, xp = 150, tint = new Color(0.8f, 0.75f, 1f), isBoss = true, startTime = 900 },
    };

    [Tooltip("Enemies spawned per second over run time (seconds)")]
    [SerializeField] AnimationCurve spawnsPerSecond = new(new Keyframe(0, 1.0f), new Keyframe(60, 2f), new Keyframe(120, 3.5f), new Keyframe(300, 7f), new Keyframe(600, 12f), new Keyframe(900, 18f));
    [Tooltip("Enemy health multiplier over run time (seconds)")]
    [SerializeField] AnimationCurve healthScale = new(new Keyframe(0, 1f), new Keyframe(120, 1.7f), new Keyframe(300, 3.2f), new Keyframe(600, 7f), new Keyframe(900, 13f));
    [Tooltip("Enemy damage multiplier over run time (seconds)")]
    [SerializeField] AnimationCurve damageScale = new(new Keyframe(0, 1f), new Keyframe(300, 1.6f), new Keyframe(600, 2.4f), new Keyframe(900, 3.3f));
    [Header("Elites")]
    [SerializeField] float eliteStart = 90f, eliteEvery = 50f, eliteHealth = 10f, eliteDamage = 2f, eliteScale = 1.45f;
    [SerializeField] int eliteXp = 12;
    float nextElite = -1f;
    EnemyType[] eliteTypes;

    [Header("Props + events")]
    [Tooltip("Crate, brazier, statue")] [SerializeField] Sprite[] propSprites = new Sprite[3];
    [SerializeField] Sprite goblinSprite, shrineSprite;
    [SerializeField] int maxProps = 7;
    float propTimer = 3f, nextEvent = 70f;
    EnemyType[] propTypes;
    EnemyType goblinType;
    /// <summary>Cursed shrine: more enemies that hit harder while it's active.</summary>
    public float EventSpawnMul = 1f, EventDamageMul = 1f;
    readonly List<Enemy> scratch = new();

    ObjectPool<Enemy> pool;
    float hpMul = 1f, dmgMul = 1f, spawnMul = 1f;
    Color tint = Color.white;
    RunManager run;
    float accumulator;
    int nextBoss, bossBase, stageOrder;
    EnemyType Boss(int i) => bosses[Mathf.Clamp(bossBase + i, 0, bosses.Length - 1)];

#if UNITY_EDITOR
    /// <summary>Editor test: switch to the next stage's boss trio and restart the boss order.</summary>
    int debugEvent;
    public void DebugEvent()
    {
        float t = run.RunTime;
        switch (debugEvent++ % 4) { case 0: StartCoroutine(Swarm(t)); break; case 1: Encircle(t); break; case 2: Goblin(t); break; default: run.SpawnShrine(shrineSprite); break; }
    }
    public string DebugNextBossSet() { bossBase = (bossBase + 3) % bosses.Length; nextBoss = 0; return Boss(0).name; }
#endif
    public float NextBossTime => nextBoss < 3 ? Boss(nextBoss).startTime : -1f;
    public string NextBossName => nextBoss < 3 ? Boss(nextBoss).name : "";
    public bool AllBossesSpawned => nextBoss >= 3;

    public void SetStage(StageData stage)
    {
        if (stage == null) return;
        stageOrder = Mathf.Max(0, stage.order);
        bossBase = stageOrder * 3 + 2 < bosses.Length ? stageOrder * 3 : 0;
        hpMul = stage.enemyHealth; dmgMul = stage.enemyDamage; spawnMul = stage.spawnRate; tint = stage.enemyTint;
        // Stage art: walker, hound and brute get this stage's own sprites (copies, so the scene data stays untouched)
        if (stage.enemySprites != null)
            for (int i = 0; i < types.Length && i < stage.enemySprites.Length; i++)
                if (stage.enemySprites[i] != null)
                {
                    var t = types[i];
                    types[i] = new EnemyType { name = t.name, health = t.health, speed = t.speed, damage = t.damage, scale = t.scale, xp = t.xp,
                        tint = Color.white, startTime = t.startTime, weight = t.weight, sprite = stage.enemySprites[i] };
                }
    }

    public void Init(RunManager manager)
    {
        run = manager;
        pool = new ObjectPool<Enemy>(
            () => Instantiate(enemyPrefab, transform),
            e => e.gameObject.SetActive(true),
            e => e.gameObject.SetActive(false),
            e => { if (e) Destroy(e.gameObject); },
            false, 200, 2000);
    }

    void Update()
    {
        if (run == null || run.IsOver) return;
        float t = run.RunTime;

        // During a boss fight only the boss (and its own minions) are in the arena
        if (!run.BossFight) accumulator += spawnsPerSecond.Evaluate(t) * spawnMul * EventSpawnMul * Time.deltaTime;
        UpdateProps(t);
        UpdateEvents(t);
        while (accumulator >= 1f)
        {
            accumulator -= 1f;
            if (Enemy.Active.Count < maxAlive) Spawn(Pick(t), healthScale.Evaluate(t) * hpMul);
        }

        // a golden elite every ~50s (not during boss fights)
        if (nextElite < 0f) nextElite = eliteStart;
        if (!run.BossFight && t >= nextElite)
        {
            nextElite = t + eliteEvery;
            var et = Elite(Pick(t));
            var el = Spawn(et, healthScale.Evaluate(t) * hpMul);
            if (el != null) { el.Body.color = et.tint; RunManager.Instance.OnEliteSpawned(el); }
        }

        if (nextBoss < 3 && t >= Boss(nextBoss).startTime)
        {
            var def = Boss(nextBoss);
            var boss = Spawn(def, hpMul);
            if (boss != null)
            {
                if (!boss.TryGetComponent(out BossAI ai)) ai = boss.gameObject.AddComponent<BossAI>();
                ai.Begin(boss, bossBase + nextBoss, stageOrder);
                run.OnBossSpawned(boss, def.name, def.title);
            }
            nextBoss++;
        }
    }

    EnemyType Pick(float t)
    {
        float total = 0f;
        foreach (var et in types) if (t >= et.startTime) total += et.weight;
        float r = Random.value * total;
        foreach (var et in types)
        {
            if (t < et.startTime) continue;
            r -= et.weight;
            if (r <= 0f) return et;
        }
        return types[0];
    }

    EnemyType Elite(EnemyType basic)
    {
        eliteTypes ??= new EnemyType[types.Length];
        int i = System.Array.IndexOf(types, basic); if (i < 0) i = 0;
        if (eliteTypes[i] == null)
            eliteTypes[i] = new EnemyType
            {
                name = "Elite " + basic.name, health = basic.health * eliteHealth, speed = basic.speed * 1.05f, damage = basic.damage * eliteDamage,
                scale = basic.scale * eliteScale, xp = basic.xp * eliteXp, tint = new Color(1f, 0.82f, 0.35f), sprite = basic.sprite,
                startTime = 0f, weight = 0f, isElite = true,
            };
        else eliteTypes[i].sprite = basic.sprite;
        return eliteTypes[i];
    }

    /// <summary>Boss minions: a walker or hound right next to the boss.</summary>
    public void SpawnMinion(Vector2 pos, int typeIndex = 0)
    {
        if (Enemy.Active.Count >= maxAlive + 60) return;
        var type = types[Mathf.Clamp(typeIndex, 0, types.Length - 1)];
        var e = pool.Get();
        e.Init(this, type, healthScale.Evaluate(run.RunTime) * hpMul, dmgMul * damageScale.Evaluate(run.RunTime), tint, pos);
        if (e.TryGetComponent(out BossAI ai)) ai.enabled = false;
    }

    Enemy Spawn(EnemyType type, float hpScale)
    {
        var cam = Camera.main;
        var p = PlayerController.Instance;
        if (cam == null || p == null) return null;
        float h = cam.orthographicSize, w = h * cam.aspect;
        float r = Mathf.Sqrt(w * w + h * h) + 1f;
        float a = Random.value * Mathf.PI * 2f;
        Vector2 pos = (Vector2)p.transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        var e = pool.Get();
        e.Init(this, type, hpScale, type.isBoss ? dmgMul : dmgMul * damageScale.Evaluate(run.RunTime) * EventDamageMul, type.isElite || type.isProp || type.isGoblin ? Color.white : tint, pos);
        if (!type.isBoss && e.TryGetComponent(out BossAI ai)) ai.enabled = false;
        return e;
    }

    public void Despawn(Enemy e) => pool.Release(e);

    Enemy SpawnAt(EnemyType type, Vector2 pos, float hpScale)
    {
        if (Enemy.Active.Count >= maxAlive + 80) return null;
        var e = pool.Get();
        e.Init(this, type, hpScale, type.isProp || type.isGoblin ? 0f : dmgMul * damageScale.Evaluate(run.RunTime) * EventDamageMul, type.isElite || type.isProp || type.isGoblin ? Color.white : tint, pos);
        if (e.TryGetComponent(out BossAI ai)) ai.enabled = false;
        return e;
    }

    // ---------------- breakable props: crates, braziers, rare statues

    void UpdateProps(float t)
    {
        propTimer -= Time.deltaTime;
        if (propTimer > 0f) return;
        propTimer = 4f;
        var p = PlayerController.Instance;
        if (p == null) return;
        int count = 0;
        scratch.Clear();
        foreach (var e in Enemy.Active) if (e.IsProp) { count++; if (((Vector2)e.transform.position - (Vector2)p.transform.position).sqrMagnitude > 26f * 26f) scratch.Add(e); }
        foreach (var e in scratch) { e.Vanish(); count--; }
        if (count >= maxProps || run.BossFight) return;
        propTypes ??= new[]
        {
            new EnemyType { name = "Crate", health = 12, speed = 0, damage = 0, scale = 1f, xp = 0, tint = Color.white, isProp = true, propKind = 0, sprite = propSprites.Length > 0 ? propSprites[0] : null },
            new EnemyType { name = "Brazier", health = 18, speed = 0, damage = 0, scale = 1f, xp = 0, tint = Color.white, isProp = true, propKind = 1, sprite = propSprites.Length > 1 ? propSprites[1] : null },
            new EnemyType { name = "Statue", health = 40, speed = 0, damage = 0, scale = 1.15f, xp = 0, tint = Color.white, isProp = true, propKind = 2, sprite = propSprites.Length > 2 ? propSprites[2] : null },
        };
        float r = Random.value;
        var type = r < 0.5f ? propTypes[0] : r < 0.85f ? propTypes[1] : propTypes[2];
        float a = Random.value * Mathf.PI * 2f, d = Random.Range(9f, 14f);
        Vector2 pos = (Vector2)p.transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
        if (PlayerController.ArenaCenter.HasValue) return;
        SpawnAt(type, pos, 1f + t / 300f);
    }

    // ---------------- map events

    void UpdateEvents(float t)
    {
        if (t < nextEvent || run.BossFight) return;
        float untilBoss = NextBossTime - t;
        if (NextBossTime > 0f && untilBoss < 25f) { nextEvent = t + untilBoss + 30f; return; }
        nextEvent = t + Random.Range(70f, 100f);
        switch (Random.Range(0, 4))
        {
            case 0: StartCoroutine(Swarm(t)); break;
            case 1: Encircle(t); break;
            case 2: Goblin(t); break;
            default: run.SpawnShrine(shrineSprite); break;
        }
    }

    System.Collections.IEnumerator Swarm(float t)
    {
        run.EventBanner("SWARM!", "A pack of Hollow hounds is rushing in!");
        var p = PlayerController.Instance;
        var cam = Camera.main;
        if (p == null || cam == null) yield break;
        var hound = types.Length > 1 ? types[1] : types[0];
        float baseAng = Random.value * Mathf.PI * 2f;
        float r = Mathf.Sqrt(Mathf.Pow(cam.orthographicSize * cam.aspect, 2) + Mathf.Pow(cam.orthographicSize, 2)) + 1f;
        for (int i = 0; i < 36; i++)
        {
            if (run.IsOver || run.BossFight) yield break;
            float a = baseAng + Random.Range(-0.45f, 0.45f);
            Vector2 pos = (Vector2)p.transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            SpawnAt(hound, pos, healthScale.Evaluate(t) * hpMul * 0.7f);
            yield return new WaitForSeconds(0.08f);
        }
    }

    void Encircle(float t)
    {
        run.EventBanner("SURROUNDED!", "The Hollow close in from every side. Break out!");
        var p = PlayerController.Instance;
        if (p == null) return;
        const int n = 32;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            Vector2 pos = (Vector2)p.transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 8.5f;
            SpawnAt(types[0], pos, healthScale.Evaluate(t) * hpMul);
        }
    }

    void Goblin(float t)
    {
        var p = PlayerController.Instance;
        if (p == null) return;
        goblinType ??= new EnemyType { name = "Treasure Goblin", health = 60, speed = 3.3f, damage = 0, scale = 1.1f, xp = 25, tint = Color.white, isGoblin = true, sprite = goblinSprite };
        float a = Random.value * Mathf.PI * 2f;
        Vector2 pos = (Vector2)p.transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5.5f;
        var g = SpawnAt(goblinType, pos, healthScale.Evaluate(t) * hpMul);
        if (g == null) return;
        if (goblinSprite == null) g.Body.color = new Color(0.4f, 1f, 0.45f);
        run.EventBanner("TREASURE GOBLIN!", "Catch it before it escapes (15s) for gold and gems!");
        AudioManager.Play("goblin", 0.9f);
        StartCoroutine(GoblinTimer(g));
    }

    System.Collections.IEnumerator GoblinTimer(Enemy g)
    {
        float life = 15f;
        while (life > 0f && g != null && g.isActiveAndEnabled && g.IsGoblin && g.HealthFraction > 0f)
        {
            life -= Time.deltaTime;
            if (Random.value < Time.deltaTime * 8f) FxParticles.Sparks(g.transform.position, new Color(1f, 0.85f, 0.3f), 1, 1.5f, 0.15f, 0.4f);
            yield return null;
        }
        if (g != null && g.isActiveAndEnabled && g.IsGoblin && g.HealthFraction > 0f)
        {
            FxParticles.Smoke(g.transform.position, new Color(0.3f, 0.3f, 0.3f, 0.7f), 8, 0.4f, 1f, 0.6f, 1.5f);
            g.Vanish();
            run.EventBanner("", "The goblin escaped with its loot...");
        }
    }

    public void OnEnemyKilled(Enemy e, int xp)
    {
        run.OnEnemyKilled(e, xp);
        pool.Release(e);
    }
}
