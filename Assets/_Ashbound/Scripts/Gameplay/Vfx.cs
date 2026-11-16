using System;
using UnityEngine;

/// <summary>Expanding, fading ring/blob used for hits, explosions and novas.</summary>
public class Vfx : MonoBehaviour
{
    [SerializeField] SpriteRenderer sprite;

    float t, duration, radius;
    Color color;
    Action<Vfx> release;

    public void Play(Vector2 position, float r, Color c, float seconds, Action<Vfx> onDone)
    {
        transform.position = position;
        radius = r; color = c; duration = seconds; t = 0f; release = onDone;
        Apply(0f);
    }

    void Update()
    {
        t += Time.deltaTime / duration;
        if (t >= 1f) { release?.Invoke(this); return; }
        Apply(t);
    }

    void Apply(float k)
    {
        float ease = 1f - (1f - k) * (1f - k);
        float s = radius * 2f * Mathf.Lerp(0.35f, 1f, ease);
        transform.localScale = new Vector3(s, s, 1f);
        var c = color;
        c.a = (1f - k) * 0.85f;
        sprite.color = c;
    }
}
