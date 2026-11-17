using UnityEngine;
using UnityEngine.Pool;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] Enemy enemyPrefab;
    [SerializeField] int maxAlive = 400;

    [SerializeField] EnemyType[] types =
    {
        new() { name = "Hollow Walker", health = 10, speed = 1.4f, damage = 3, scale = 1f, xp = 1, tint = Color.white, startTime = 0, weight = 10 },
        new() { name = "Hollow Hound", health = 6, speed = 2.5f, damage = 3, scale = 0.8f, xp = 2, tint = new Color(1f, 0.88f, 0.75f), startTime = 60, weight = 5 },
        new() { name = "Hollow Brute", health = 45, speed = 1.0f, damage = 7, scale = 1.6f, xp = 5, tint = new Color(0.78f, 0.84f, 0.78f), startTime = 150, weight = 2 },
    };

    [SerializeField] EnemyType[] bosses =
    {
        new() { name = "The Ash Warden", health = 900, speed = 1.3f, damage = 16, scale = 3f, xp = 40, tint = new Color(0.75f, 0.75f, 0.85f), isBoss = true, startTime = 300 },
        new() { name = "The Rotting Matriarch", health = 2600, speed = 1.5f, damage = 20, scale = 3.2f, xp = 80, tint = new Color(0.85f, 0.7f, 0.9f), isBoss = true, startTime = 600 },
        new() { name = "The Hollow King", health = 6500, speed = 1.4f, damage = 28, scale = 3.6f, xp = 150, tint = new Color(0.62f, 0.58f, 0.8f), isBoss = true, startTime = 900 },
    };

    [Tooltip("Enemies spawned per second over run time (seconds)")]
    [SerializeField] AnimationCurve spawnsPerSecond = new(new Keyframe(0, 1.0f), new Keyframe(60, 2f), new Keyframe(120, 3.5f), new Keyframe(300, 7f), new Keyframe(600, 12f), new Keyframe(900, 18f));
    [Tooltip("Enemy health multiplier over run time (seconds)")]
    [SerializeField] AnimationCurve healthScale = new(new Keyframe(0, 1f), new Keyframe(300, 2.5f), new Keyframe(600, 5f), new Keyframe(900, 9f));

    ObjectPool<Enemy> pool;
    float hpMul = 1f, dmgMul = 1f, spawnMul = 1f;
    Color tint = Color.white;
    RunManager run;
    float accumulator;
    int nextBoss;

    public float NextBossTime => nextBoss < bosses.Length ? bosses[nextBoss].startTime : -1f;
    public string NextBossName => nextBoss < bosses.Length ? bosses[nextBoss].name : "";
    public bool AllBossesSpawned => nextBoss >= bosses.Length;

    public void SetStage(StageData stage)
    {
        if (stage == null) return;
        hpMul = stage.enemyHealth; dmgMul = stage.enemyDamage; spawnMul = stage.spawnRate; tint = stage.enemyTint;
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

        accumulator += spawnsPerSecond.Evaluate(t) * spawnMul * Time.deltaTime;
        while (accumulator >= 1f)
        {
            accumulator -= 1f;
            if (Enemy.Active.Count < maxAlive) Spawn(Pick(t), healthScale.Evaluate(t) * hpMul);
        }

        if (nextBoss < bosses.Length && t >= bosses[nextBoss].startTime)
        {
            var boss = Spawn(bosses[nextBoss], hpMul);
            if (boss != null)
            {
                if (!boss.TryGetComponent(out BossAI ai)) ai = boss.gameObject.AddComponent<BossAI>();
                ai.Begin(boss, nextBoss, RunSelection.Stage != null ? RunSelection.Stage.order : 0);
                run.OnBossSpawned(boss, bosses[nextBoss].name);
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

    /// <summary>Boss minions: a walker or hound right next to the boss.</summary>
    public void SpawnMinion(Vector2 pos)
    {
        if (Enemy.Active.Count >= maxAlive + 60) return;
        var type = types[Random.value < 0.6f ? 0 : Mathf.Min(1, types.Length - 1)];
        var e = pool.Get();
        e.Init(this, type, healthScale.Evaluate(run.RunTime) * hpMul, dmgMul, tint, pos);
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
        e.Init(this, type, hpScale, dmgMul, tint, pos);
        if (!type.isBoss && e.TryGetComponent(out BossAI ai)) ai.enabled = false;
        return e;
    }

    public void OnEnemyKilled(Enemy e, int xp)
    {
        run.OnEnemyKilled(e, xp);
        pool.Release(e);
    }
}
