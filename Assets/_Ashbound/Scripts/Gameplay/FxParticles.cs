using UnityEngine;

/// <summary>
/// Shared particle effects: glowing sparks (additive), soft smoke and solid debris.
/// Three world-space particle systems; every effect is a burst of emitted particles, so it's cheap.
/// </summary>
public static class FxParticles
{
    static ParticleSystem sparks, smoke, debris;
    public static Material AdditiveSprite { get; private set; }
    public static Material GlowMaterial { get; private set; }
    public static Sprite GlowSprite { get; private set; }

    public static void Init(Transform root, Material additiveSprite, Material glowParticles, Material smokeParticles, Material debrisParticles, Sprite glow)
    {
        AdditiveSprite = additiveSprite; GlowMaterial = glowParticles; GlowSprite = glow;
        sparks = Make(root, "Sparks", glowParticles, 20, 0.06f);
        smoke = Make(root, "Smoke", smokeParticles, 13, 0.03f);
        debris = Make(root, "Debris", debrisParticles, 12, 0.1f);
    }

    static ParticleSystem Make(Transform root, string name, Material mat, int order, float dampen)
    {
        if (mat == null) return null;
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f; main.startLifetime = 0.5f; main.maxParticles = 4000;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em = ps.emission; em.enabled = false;
        var shape = ps.shape; shape.enabled = false;
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1f), new Keyframe(0.7f, 0.75f), new Keyframe(1, 0f)));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0f; lim.dampen = dampen;
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.sortingOrder = order; r.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play();
        return ps;
    }

    static void Emit(ParticleSystem ps, Vector2 pos, Color c, int n, float speed, float size, float life, Vector2 dir, float spreadDeg, float rise, float jitter)
    {
        if (ps == null || n <= 0) return;
        if (!ps.isPlaying) ps.Play();
        var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        float baseAng = dir == Vector2.zero ? 0f : Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float spread = dir == Vector2.zero ? 360f : spreadDeg;
        for (int i = 0; i < n; i++)
        {
            float a = (baseAng + Random.Range(-spread, spread) * 0.5f) * Mathf.Deg2Rad;
            Vector2 v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.35f, 1f) + Vector2.up * rise;
            ep.position = pos + Random.insideUnitCircle * jitter;
            ep.velocity = v;
            ep.startColor = Color.Lerp(c, Color.white, Random.Range(0f, 0.25f));
            ep.startSize = size * Random.Range(0.6f, 1.25f);
            ep.startLifetime = life * Random.Range(0.6f, 1.2f);
            ep.rotation = Random.Range(0f, 360f);
            ps.Emit(ep, 1);
        }
    }

    /// <summary>Glowing sparks flying out (additive).</summary>
    public static void Sparks(Vector2 pos, Color c, int n, float speed = 4f, float size = 0.22f, float life = 0.35f, Vector2 dir = default, float spread = 360f, float jitter = 0.1f)
        => Emit(sparks, pos, c, n, speed, size, life, dir, spread, 0f, jitter);

    /// <summary>Glowing motes that drift upward (embers, holy light, spirit wisps).</summary>
    public static void Motes(Vector2 pos, Color c, int n, float radius, float size = 0.18f, float life = 0.8f)
        => Emit(sparks, pos, c, n, 0.6f, size, life, Vector2.zero, 360f, 1.4f, radius);

    /// <summary>Soft puffs of smoke / mist / poison (normal blend).</summary>
    public static void Smoke(Vector2 pos, Color c, int n, float radius = 0.4f, float size = 0.9f, float life = 0.7f, float speed = 1.2f)
        => Emit(smoke, pos, c, n, speed, size, life, Vector2.zero, 360f, 0.3f, radius);

    /// <summary>Solid chunks: rock, blood drops, leaves, ice shards.</summary>
    public static void Debris(Vector2 pos, Color c, int n, float speed = 4f, float size = 0.13f, float life = 0.5f, Vector2 dir = default, float spread = 360f)
        => Emit(debris, pos, c, n, speed, size, life, dir, spread, 0f, 0.1f);

    /// <summary>Particles spread along a line (beams, chains, slashes).</summary>
    public static void Line(Vector2 a, Vector2 b, Color c, float perUnit, float size = 0.2f, float speed = 1.5f, float life = 0.35f)
    {
        float len = Vector2.Distance(a, b);
        int n = Mathf.Clamp(Mathf.RoundToInt(len * perUnit), 1, 80);
        for (int i = 0; i < n; i++)
            Emit(sparks, Vector2.Lerp(a, b, Random.value), c, 1, speed, size, life, Vector2.zero, 360f, 0f, 0.08f);
    }

    /// <summary>A soft additive light blob.</summary>
    public static FxSprite Glow(Vector2 pos, Color c, float size, float seconds, int order = 15)
    {
        var run = RunManager.Instance;
        if (run == null || GlowSprite == null) return null;
        float s = size / Mathf.Max(0.01f, GlowSprite.bounds.size.x);
        return run.Fx(GlowSprite, pos, c, seconds, order).Additive().Scale(s * 0.6f, s).FadeFrom(0.2f);
    }
}

// v2
