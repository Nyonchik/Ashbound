using UnityEngine;

/// <summary>Enemy bullet that damages the player on touch.</summary>
public class BossBullet : MonoBehaviour
{
    Vector2 velocity;
    float life, damage;

    public static void Spawn(Sprite s, Vector2 pos, Vector2 vel, float dmg, float lifetime, float size = 0.6f)
    {
        var go = new GameObject("BossBullet");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s != null ? s : RunManager.Instance.FallbackSprite;
        if (s == null) sr.color = new Color(0.8f, 0.3f, 0.6f);
        sr.sharedMaterial = RunManager.Instance.SpriteMaterial;
        sr.sortingOrder = 18;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * size;
        var b = go.AddComponent<BossBullet>();
        b.velocity = vel; b.life = lifetime; b.damage = dmg;
    }

    void Update()
    {
        var run = RunManager.Instance;
        if (run == null || run.IsOver) { Destroy(gameObject); return; }
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.right = velocity;
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }
        var p = PlayerController.Instance;
        if (p != null && ((Vector2)p.transform.position + Vector2.up * 0.4f - (Vector2)transform.position).sqrMagnitude < 0.25f)
        {
            p.Stats.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}

