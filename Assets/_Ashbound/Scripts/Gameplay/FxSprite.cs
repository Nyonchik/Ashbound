using System;
using UnityEngine;

/// <summary>Pooled one-shot sprite effect: scale, move, spin and fade over its lifetime. Configure with the chained setters.</summary>
public class FxSprite : MonoBehaviour
{
    public SpriteRenderer sr;

    float t, duration, spin, alpha, fadeOutFrom = 0.6f;
    Vector3 scaleFrom = Vector3.one, scaleTo = Vector3.one;
    Vector2 from, to;
    bool moving, easeOutMove;
    Color color;
    Action<FxSprite> release;
    Material normal;

    /// <summary>Draw with additive blending (glows, light).</summary>
    public FxSprite Additive() { if (FxParticles.AdditiveSprite != null) sr.sharedMaterial = FxParticles.AdditiveSprite; return this; }

    public FxSprite Setup(Sprite s, Vector2 pos, Color c, float seconds, Action<FxSprite> onDone, int order)
    {
        if (normal == null) normal = sr.sharedMaterial;
        sr.sharedMaterial = normal;
        sr.sprite = s; sr.sortingOrder = order; sr.flipX = sr.flipY = false;
        color = c; alpha = c.a; duration = Mathf.Max(0.01f, seconds); t = 0f; spin = 0f; fadeOutFrom = 0.6f;
        from = to = pos; moving = false;
        scaleFrom = scaleTo = Vector3.one;
        transform.position = pos; transform.rotation = Quaternion.identity;
        release = onDone;
        Apply();
        return this;
    }

    public FxSprite Scale(float a, float b) => Scale(Vector3.one * a, Vector3.one * b);
    public FxSprite Scale(Vector3 a, Vector3 b) { scaleFrom = a; scaleTo = b; Apply(); return this; }
    public FxSprite Rotate(float degrees) { transform.rotation = Quaternion.Euler(0f, 0f, degrees); return this; }
    public FxSprite Spin(float degreesPerSecond) { spin = degreesPerSecond; return this; }
    public FxSprite Move(Vector2 target, bool easeOut = false) { to = target; moving = true; easeOutMove = easeOut; return this; }
    /// <summary>When (0-1 of the lifetime) the fade-out starts. 1 = never fades.</summary>
    /// <summary>Remove the effect right away.</summary>
    public void End() { if (release != null) { var r = release; release = null; r(this); } }
    public FxSprite FadeFrom(float k) { fadeOutFrom = Mathf.Clamp01(k); return this; }

    void Update()
    {
        t += Time.deltaTime / duration;
        if (t >= 1f) { var r = release; release = null; r?.Invoke(this); return; }
        if (spin != 0f) transform.Rotate(0f, 0f, spin * Time.deltaTime);
        Apply();
    }

    void Apply()
    {
        float k = Mathf.Clamp01(t);
        float ease = 1f - (1f - k) * (1f - k);
        transform.localScale = Vector3.LerpUnclamped(scaleFrom, scaleTo, ease);
        if (moving) transform.position = Vector2.Lerp(from, to, easeOutMove ? ease : k * k);
        float fadeIn = Mathf.Min(1f, k * duration / 0.04f);
        float fadeOut = k <= fadeOutFrom || fadeOutFrom >= 1f ? 1f : 1f - (k - fadeOutFrom) / (1f - fadeOutFrom);
        sr.color = new Color(color.r, color.g, color.b, alpha * fadeIn * fadeOut);
    }
}
