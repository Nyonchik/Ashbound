using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyType
{
    public string name = "Hollow Walker";
    [Tooltip("Bosses: subtitle on the intro card")] public string title;
    public float health = 10f;
    public float speed = 1.5f;
    public float damage = 8f;
    public float scale = 1f;
    public int xp = 1;
    public Color tint = new(0.43f, 0.46f, 0.41f);
    public bool isBoss;
    [Tooltip("Optional art that replaces the default enemy sprite")] public Sprite sprite;
    [Tooltip("Seconds into the run when this type starts appearing (bosses: when they spawn)")] public float startTime;
    [Tooltip("Relative spawn chance")] public float weight = 1f;
}

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> Active = new();
    static readonly Color HitColor = new(1f, 0.45f, 0.45f);

    [SerializeField] SpriteRenderer body;
    [SerializeField] Transform visual;

    Rigidbody2D rb;
    EnemySpawner owner;
    float hp, speed, damage, flash, bob, radius;
    int xp;
    Vector2 knock, pull;
    Color baseColor;
    float slowTimer, slowMul = 1f;
    static readonly Color FrozenTint = new(0.55f, 0.85f, 1f);
    Color CurrentColor => slowTimer > 0f ? baseColor * FrozenTint : baseColor;

    /// <summary>Slow (mul 0.25 = 75% slower) for a while; tints the enemy icy.</summary>
    public void Slow(float seconds, float mul)
    {
        if (dead) return;
        slowTimer = Mathf.Max(slowTimer, seconds);
        slowMul = IsBoss ? Mathf.Max(mul, 0.6f) : mul;
        if (flash <= 0f) body.color = CurrentColor;
    }

    /// <summary>Drag toward a point this physics step (black holes). Bosses resist.</summary>
    public void Pull(Vector2 velocity) { if (!dead) pull += IsBoss ? velocity * 0.25f : velocity; }
    bool dead;

    public bool IsBoss { get; private set; }
    public float Radius => radius;
    public float HealthFraction => hp <= 0 ? 0 : hp / maxHp;
    float maxHp;

    Sprite defaultSprite;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultSprite = body.sprite;
    }

    void OnDisable() => Active.Remove(this);

    public SpriteRenderer Body => body;
    public EnemySpawner Owner => owner;
    /// <summary>Set by boss attacks (charges, wind-ups). Null = normal chasing.</summary>
    public Vector2? ForcedVelocity;
    /// <summary>Dies right away (gives XP and counts as a kill).</summary>
    public void Kill() { if (dead) return; dead = true; owner.OnEnemyKilled(this, xp); }

    public void Teleport(Vector2 p) { rb.position = p; transform.position = p; knock = Vector2.zero; }
    public float FacingSign => visual != null ? Mathf.Sign(visual.localScale.x) : 1f;

    public void Init(EnemySpawner spawner, EnemyType type, float healthScale, float damageScale, Color stageTint, Vector2 position)
    {
        owner = spawner;
        maxHp = hp = type.health * healthScale;
        speed = type.speed;
        damage = type.damage * damageScale;
        xp = type.xp;
        IsBoss = type.isBoss;
        radius = type.sprite != null ? type.sprite.bounds.extents.x * 0.75f * type.scale : 0.34f * type.scale;
        transform.localScale = Vector3.one * type.scale;
        transform.position = position;
        rb.position = position;
        rb.linearVelocity = Vector2.zero;
        body.sprite = type.sprite != null ? type.sprite : defaultSprite;
        baseColor = type.tint * stageTint;
        body.color = baseColor;
        knock = Vector2.zero; pull = Vector2.zero; slowTimer = 0f; slowMul = 1f; ForcedVelocity = null;
        flash = 0f;
        dead = false;
        bob = Random.value * 10f;
        Active.Add(this);
    }

    void FixedUpdate()
    {
        var p = PlayerController.Instance;
        if (p == null || dead) return;
        Vector2 dir = ((Vector2)p.transform.position - rb.position).normalized;
        if (ForcedVelocity.HasValue) { rb.linearVelocity = ForcedVelocity.Value * (slowTimer > 0f ? Mathf.Max(slowMul, 0.6f) : 1f); pull = Vector2.zero; return; }
        float sp = slowTimer > 0f ? speed * slowMul : speed;
        rb.linearVelocity = dir * sp + knock + pull;
        pull = Vector2.zero;
        knock = Vector2.Lerp(knock, Vector2.zero, 8f * Time.fixedDeltaTime);
    }

    void Update()
    {
        var p = PlayerController.Instance;
        if (p != null && !dead)
        {
            Vector2 toPlayer = (Vector2)p.transform.position - (Vector2)transform.position;
            if (toPlayer.sqrMagnitude < (radius + 0.4f) * (radius + 0.4f)) p.Stats.TakeDamage(damage);

            if (visual)
            {
                bob += Time.deltaTime * 9f;
                float s = Mathf.Sin(bob) * 0.05f;
                float side = toPlayer.x < 0 ? -1f : 1f;
                visual.localScale = new Vector3(side * (1f + s), 1f - s, 1f);
            }
        }

        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f && flash <= 0f) body.color = baseColor;
        }
        if (flash > 0f)
        {
            flash -= Time.deltaTime;
            body.color = flash > 0f ? HitColor : CurrentColor;
        }
    }

    public void TakeDamage(float amount, Vector2 knockDir, float knockForce)
    {
        if (dead) return;
        hp -= amount;
        RunManager.Instance?.ShowDamage(transform.position + Vector3.up * (radius * 2.2f), amount, IsBoss);
        flash = 0.08f;
        body.color = HitColor;
        if (!IsBoss) knock += knockDir * knockForce;
        if (hp <= 0f)
        {
            dead = true;
            owner.OnEnemyKilled(this, xp);
        }
    }

    // ---- queries (fill a list first, then damage, so kills don't break iteration)

    public static void CollectInRadius(Vector2 center, float r, List<Enemy> results)
    {
        results.Clear();
        for (int i = 0; i < Active.Count; i++)
        {
            var e = Active[i];
            float rr = r + e.radius;
            if (((Vector2)e.transform.position - center).sqrMagnitude <= rr * rr) results.Add(e);
        }
    }

    public static Enemy Nearest(Vector2 from, float maxDistance)
    {
        Enemy best = null;
        float bestD = maxDistance * maxDistance;
        for (int i = 0; i < Active.Count; i++)
        {
            float d = ((Vector2)Active[i].transform.position - from).sqrMagnitude;
            if (d < bestD) { bestD = d; best = Active[i]; }
        }
        return best;
    }
}
