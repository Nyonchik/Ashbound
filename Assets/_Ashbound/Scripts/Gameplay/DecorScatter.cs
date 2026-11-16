using System.Collections.Generic;
using UnityEngine;

/// <summary>Scatters decoration sprites around the camera and wraps them, so the ground feels endless.</summary>
public class DecorScatter : MonoBehaviour
{
    [SerializeField] SpriteRenderer prefab;
    [SerializeField] int count = 40;
    [SerializeField] Vector2 area = new(34f, 22f);
    [SerializeField] Vector2 scaleRange = new(0.6f, 1.4f);
    [SerializeField] int seed = 1;

    readonly List<Transform> items = new();

    void Start()
    {
        var rnd = new System.Random(seed);
        float R() => (float)rnd.NextDouble();
        for (int i = 0; i < count; i++)
        {
            var s = Instantiate(prefab, transform);
            s.transform.position = new Vector3((R() - 0.5f) * area.x, (R() - 0.5f) * area.y, 0f);
            float k = Mathf.Lerp(scaleRange.x, scaleRange.y, R());
            s.transform.localScale = new Vector3(R() < 0.5f ? -k : k, k, 1f);
            items.Add(s.transform);
        }
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector2 c = cam.transform.position;
        foreach (var t in items)
        {
            var p = t.position;
            if (p.x - c.x > area.x / 2) p.x -= area.x; else if (p.x - c.x < -area.x / 2) p.x += area.x;
            if (p.y - c.y > area.y / 2) p.y -= area.y; else if (p.y - c.y < -area.y / 2) p.y += area.y;
            t.position = p;
        }
    }
}
