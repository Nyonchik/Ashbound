using UnityEngine;

/// <summary>Ground area (fire, poison) that hurts the player while standing in it.</summary>
public class Hazard : MonoBehaviour
{
    SpriteRenderer sr;
    float life, total, damage, radius, tick;
    Color color;

    public static void Spawn(Sprite s, Vector2 pos, Color tint, float radius, float lifetime, float dmg)
    {
        var go = new GameObject("Hazard");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s != null ? s : RunManager.Instance.FallbackSprite;
        sr.sharedMaterial = RunManager.Instance.SpriteMaterial;
        sr.sortingOrder = -8;
        go.transform.position = pos;
        float w = sr.sprite.bounds.size.x;
        go.transform.localScale = Vector3.one * (radius * 2f / Mathf.Max(0.01f, w));
        var h = go.AddComponent<Hazard>();
        h.sr = sr; h.life = h.total = lifetime; h.damage = dmg; h.radius = radius; h.color = tint;
        sr.color = new Color(tint.r, tint.g, tint.b, 0f);
    }

    void Update()
    {
        var run = RunManager.Instance;
        if (run == null || run.IsOver) { Destroy(gameObject); return; }
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }
        float k = 1f - life / total;
        float a = Mathf.Min(1f, k * total / 0.2f) * Mathf.Min(1f, life / 0.5f);
        sr.color = new Color(color.r, color.g, color.b, a * 0.95f);

        tick -= Time.deltaTime;
        var p = PlayerController.Instance;
        if (tick <= 0f && p != null && ((Vector2)p.transform.position - (Vector2)transform.position).sqrMagnitude < radius * radius * 0.7f)
        {
            tick = 0.5f;
            p.Stats.TakeDamage(damage);
        }
    }
}
