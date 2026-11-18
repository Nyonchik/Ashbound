using UnityEngine;

/// <summary>Map event: stand on the shrine for 1.5s to get +40% damage / +15% speed for 40s, but enemies spawn twice as fast and hit 30% harder.</summary>
public class CursedShrine : MonoBehaviour
{
    RunManager run;
    EnemySpawner spawner;
    float charge, life = 30f, active;
    bool used;
    SpriteRenderer glow;

    public void Setup(RunManager r, EnemySpawner s)
    {
        run = r; spawner = s;
        if (FxParticles.GlowSprite != null && FxParticles.AdditiveSprite != null)
        {
            var g = new GameObject("Glow"); g.transform.SetParent(transform, false);
            glow = g.AddComponent<SpriteRenderer>();
            glow.sprite = FxParticles.GlowSprite; glow.sharedMaterial = FxParticles.AdditiveSprite; glow.sortingOrder = 4;
            g.transform.localScale = Vector3.one * (2.4f / Mathf.Max(0.01f, FxParticles.GlowSprite.bounds.size.x) / Mathf.Max(0.01f, transform.localScale.x));
            glow.color = new Color(0.7f, 0.35f, 1f, 0.45f);
        }
    }

    void Update()
    {
        var p = PlayerController.Instance;
        if (run == null || run.IsOver || p == null) { Destroy(gameObject); return; }
        if (!used)
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }
            bool on = ((Vector2)p.transform.position - (Vector2)transform.position).sqrMagnitude < 1.4f * 1.4f;
            charge = on ? charge + Time.deltaTime : Mathf.Max(0f, charge - Time.deltaTime * 2f);
            if (glow) glow.color = new Color(0.7f, 0.35f, 1f, 0.35f + charge * 0.4f + Mathf.Sin(Time.time * 4f) * 0.08f);
            if (on && Random.value < Time.deltaTime * 12f) FxParticles.Motes(transform.position, new Color(0.75f, 0.45f, 1f), 1, 0.6f, 0.15f, 0.7f);
            if (charge >= 1.5f) Activate(p);
            return;
        }
        active -= Time.deltaTime;
        if (Random.value < Time.deltaTime * 10f) FxParticles.Motes(p.transform.position, new Color(0.75f, 0.4f, 1f), 1, 0.5f, 0.14f, 0.6f);
        if (active <= 0f) End(p);
    }

    void Activate(PlayerController p)
    {
        used = true; active = 40f;
        p.Stats.damageMul += 0.4f; p.Stats.moveMul += 0.15f;
        spawner.EventSpawnMul = 2f; spawner.EventDamageMul = 1.3f;
        AudioManager.Play("shrine", 0.9f);
        FxParticles.Glow(transform.position, new Color(0.75f, 0.4f, 1f), 5f, 0.6f, -9);
        FxParticles.Sparks(transform.position, new Color(0.8f, 0.5f, 1f), 40, 7f, 0.22f, 0.6f);
        run.EventBanner("", "<color=#B98CFF><b>Shrine taken!</b></color>  +40% damage for 40s. The Hollow are coming...");
        foreach (var r in GetComponentsInChildren<SpriteRenderer>()) r.enabled = false;
    }

    void End(PlayerController p)
    {
        p.Stats.damageMul -= 0.4f; p.Stats.moveMul -= 0.15f;
        spawner.EventSpawnMul = 1f; spawner.EventDamageMul = 1f;
        run.EventBanner("", "The shrine's curse fades.");
        Destroy(gameObject);
    }
}
