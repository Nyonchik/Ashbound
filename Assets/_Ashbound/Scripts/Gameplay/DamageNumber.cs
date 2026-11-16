using System;
using TMPro;
using UnityEngine;

public class DamageNumber : MonoBehaviour
{
    [SerializeField] TextMeshPro text;

    Vector3 start;
    Color color;
    float t;
    Action<DamageNumber> release;

    public void Play(Vector3 position, int amount, Color c, float size, Action<DamageNumber> onDone)
    {
        start = position + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.2f);
        transform.position = start;
        text.text = amount.ToString();
        text.fontSize = size;
        color = c;
        text.color = c;
        t = 0f;
        release = onDone;
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = t / 0.6f;
        if (k >= 1f) { release?.Invoke(this); return; }
        transform.position = start + Vector3.up * (0.7f * Mathf.Sqrt(k));
        transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Min(1f, k * 4f));
        var c = color;
        c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
        text.color = c;
    }
}
