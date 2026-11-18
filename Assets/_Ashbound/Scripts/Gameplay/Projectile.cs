using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] SpriteRenderer sprite;

    static readonly List<Enemy> buffer = new();
    readonly List<Enemy> alreadyHit = new();

    Weapon owner;
    Sprite defaultSprite;

    TrailRenderer trail;
    Color trailColor;

    void Awake()
    {
        defaultSprite = sprite.sprite;
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = FxParticles.GlowMaterial;
        trail.sortingOrder = 15;
        trail.minVertexDistance = 0.05f;
        trail.numCapVertices = 2;
        trail.emitting = false;
    }

    /// <summary>Glowing trail behind the projectile.</summary>
    public Projectile Trail(Color c, float width, float seconds)
    {
        if (trail == null) return this;
        if (trail.sharedMaterial == null) trail.sharedMaterial = FxParticles.GlowMaterial;
        if (trail.sharedMaterial == null) return this;
        trailColor = c;
        trail.time = seconds;
        trail.widthCurve = new AnimationCurve(new Keyframe(0, width), new Keyframe(1, 0));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) }, new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.Clear();
        trail.emitting = true;
        return this;
    }
    Vector2 velocity;
    float life, damage, area, knockback;
    int pierceLeft;
    bool explode;
    Transform home;
    bool returning;
    float speed, totalLife, homing;
    System.Action<Enemy, bool> onHit;

    /// <summary>Called on every hit with (enemy, killed).</summary>
    public Projectile OnHit(System.Action<Enemy, bool> cb) { onHit = cb; return this; }
    /// <summary>Steer toward the nearest enemy not hit yet (degrees per second).</summary>
    public Projectile Homing(float degreesPerSecond) { homing = degreesPerSecond; return this; }

    /// <summary>Fly out, then come back to this transform (hits enemies both ways).</summary>
    public void MakeBoomerang(Transform target) { home = target; pierceLeft = 999; }

    public void Launch(Weapon weapon, Vector2 position, Vector2 direction, float speed, float dmg, float areaScale,
                       int pierce, float lifetime, float knock, bool explodes, Color color, Sprite art = null)
    {
        owner = weapon;
        transform.position = position;
        transform.right = direction;
        transform.localScale = Vector3.one * areaScale;
        velocity = direction * speed;
        damage = dmg; area = areaScale; pierceLeft = pierce; life = lifetime; knockback = knock; explode = explodes;
        sprite.sprite = art != null ? art : defaultSprite;
        sprite.color = art != null ? Color.white : color;
        alreadyHit.Clear();
        home = null; returning = false; this.speed = speed; totalLife = lifetime; homing = 0f; onHit = null;
        Trail(Color.Lerp(color, Color.white, 0.2f), 0.16f * areaScale, 0.12f);   // every projectile gets a short glowing trail
    }

    void Update()
    {
        if (home != null)
        {
            if (!returning && life < totalLife * 0.5f) { returning = true; alreadyHit.Clear(); }
            if (returning)
            {
                Vector2 to = (Vector2)home.position + Vector2.up * 0.4f - (Vector2)transform.position;
                if (to.sqrMagnitude < 0.16f) { Finish(); return; }
                velocity = to.normalized * speed * 1.3f;
                transform.right = velocity;
                life = 1f;
            }
        }
        if (homing > 0f && home == null)
        {
            Enemy best = null; float bd = 64f;
            foreach (var en in Enemy.Active)
            {
                if (alreadyHit.Contains(en)) continue;
                float d = ((Vector2)en.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = en; }
            }
            if (best != null)
            {
                Vector2 want = ((Vector2)best.transform.position - (Vector2)transform.position).normalized;
                float ang = Vector2.SignedAngle(velocity, want);
                velocity = Quaternion.Euler(0, 0, Mathf.Clamp(ang, -homing * Time.deltaTime, homing * Time.deltaTime)) * velocity;
                transform.right = velocity;
            }
        }
        transform.position += (Vector3)(velocity * Time.deltaTime);
        life -= Time.deltaTime;
        if (life <= 0f) { Finish(); return; }

        Enemy.CollectInRadius(transform.position, 0.22f * area, buffer);
        foreach (var e in buffer)
        {
            if (alreadyHit.Contains(e)) continue;
            alreadyHit.Add(e);
            var prevSrc = Enemy.Source; Enemy.Source = owner;
            bool killed = e.TakeDamage(damage, velocity.normalized, knockback);
            Enemy.Source = prevSrc;
            onHit?.Invoke(e, killed);
            FxParticles.Sparks(transform.position, trailColor, 5, 3.5f, 0.16f, 0.25f, -velocity.normalized, 140f);
            if (explode && owner != null) owner.Explode(transform.position, damage * 0.6f);
            if (pierceLeft-- <= 0) { Finish(); return; }
        }
    }

    void Finish()
    {
        if (trail != null) trail.emitting = false;
        if (RunManager.Instance != null) RunManager.Instance.ReleaseProjectile(this);
        else Destroy(gameObject);
    }
}
