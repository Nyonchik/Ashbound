using System;
using UnityEngine;

/// <summary>Quick squash-and-fade copy of an enemy when it dies.</summary>
public class DeathPop : MonoBehaviour
{
    [SerializeField] SpriteRenderer sprite;

    Vector3 baseScale;
    Color color;
    float t;
    Action<DeathPop> release;

    public void Play(Sprite s, Color c, Vector3 position, Vector3 scale, Action<DeathPop> onDone)
    {
        sprite.sprite = s;
        color = c;
        transform.position = position;
        baseScale = scale;
        transform.localScale = scale;
        t = 0f;
        release = onDone;
        sprite.color = c;
    }

    void Update()
    {
        t += Time.deltaTime / 0.25f;
        if (t >= 1f) { release?.Invoke(this); return; }
        transform.localScale = new Vector3(baseScale.x * (1f + 0.35f * t), baseScale.y * (1f - t), 1f);
        var c = Color.Lerp(color, new Color(1f, 0.55f, 0.25f), t);
        c.a = 1f - t;
        sprite.color = c;
    }
}
