using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] SpriteRenderer sprite;

    static readonly List<Enemy> buffer = new();
    readonly List<Enemy> alreadyHit = new();

    Weapon owner;
    Sprite defaultSprite;

    void Awake() => defaultSprite = sprite.sprite;
    Vector2 velocity;
    float life, damage, area, knockback;
    int pierceLeft;
    bool explode;
    Transform home;
    bool returning;
    float speed, totalLife;

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
        home = null; returning = false; this.speed = speed; totalLife = lifetime;
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
        transform.position += (Vector3)(velocity * Time.deltaTime);
        life -= Time.deltaTime;
        if (life <= 0f) { Finish(); return; }

        Enemy.CollectInRadius(transform.position, 0.22f * area, buffer);
        foreach (var e in buffer)
        {
            if (alreadyHit.Contains(e)) continue;
            alreadyHit.Add(e);
            e.TakeDamage(damage, velocity.normalized, knockback);
            if (explode && owner != null) owner.Explode(transform.position, damage * 0.6f);
            if (pierceLeft-- <= 0) { Finish(); return; }
        }
    }

    void Finish()
    {
        if (RunManager.Instance != null) RunManager.Instance.ReleaseProjectile(this);
        else Destroy(gameObject);
    }
}
