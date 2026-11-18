using UnityEngine;

/// <summary>Golden glow and drifting sparks under an elite enemy. Lives on the pooled enemy and hides itself when it isn't an elite.</summary>
public class EliteAura : MonoBehaviour
{
    Enemy enemy;
    SpriteRenderer glow;
    float mote;

    public static void Attach(Enemy e, Sprite glowSprite, Material additive)
    {
        if (!e.TryGetComponent(out EliteAura a)) a = e.gameObject.AddComponent<EliteAura>();
        a.enemy = e;
        if (a.glow == null && glowSprite != null && additive != null)
        {
            var g = new GameObject("EliteGlow");
            g.transform.SetParent(e.transform, false);
            g.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            a.glow = g.AddComponent<SpriteRenderer>();
            a.glow.sprite = glowSprite; a.glow.sharedMaterial = additive; a.glow.sortingOrder = -2;
            g.transform.localScale = Vector3.one * (1.8f / Mathf.Max(0.01f, glowSprite.bounds.size.x));
        }
        a.enabled = true;
    }

    void Update()
    {
        bool on = enemy != null && enemy.IsElite && enemy.isActiveAndEnabled;
        if (glow != null)
        {
            glow.enabled = on;
            if (on) glow.color = new Color(1f, 0.8f, 0.3f, 0.45f + Mathf.Sin(Time.time * 5f) * 0.12f);
        }
        if (!on) return;
        mote -= Time.deltaTime;
        if (mote <= 0f) { mote = 0.15f; FxParticles.Motes(transform.position, new Color(1f, 0.85f, 0.35f), 1, 0.5f, 0.15f, 0.7f); }
    }
}
