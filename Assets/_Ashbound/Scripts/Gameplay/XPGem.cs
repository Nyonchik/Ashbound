using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class XPGem : MonoBehaviour
{
    public static readonly List<XPGem> Active = new();

    [Tooltip("Blue (1), green (2-4), red (5-19), purple (20+)")] [SerializeField] Sprite[] tierSprites = new Sprite[4];
    [SerializeField] SpriteRenderer sprite;
    RunManager run;
    ObjectPool<XPGem> pool;
    int value;
    bool magnet;
    float speed, bob;

    public void Init(RunManager manager, ObjectPool<XPGem> owner, int xp, Vector2 position)
    {
        run = manager; pool = owner; value = xp;
        magnet = false; speed = 0f; bob = Random.value * 6f;
        transform.position = position;
        int tier = xp >= 20 ? 3 : xp >= 5 ? 2 : xp >= 2 ? 1 : 0;
        if (sprite != null && tierSprites != null && tier < tierSprites.Length && tierSprites[tier] != null) sprite.sprite = tierSprites[tier];
        transform.localScale = Vector3.one * (1f + tier * 0.15f);
        Active.Add(this);
    }

    void OnDisable() => Active.Remove(this);

    public void Magnetize() => magnet = true;

    void Update()
    {
        var p = PlayerController.Instance;
        if (p == null) return;
        Vector2 d = (Vector2)p.transform.position - (Vector2)transform.position;
        float dist = d.magnitude;
        if (!magnet && dist < p.Stats.PickupRadius) magnet = true;

        if (magnet)
        {
            speed += 22f * Time.deltaTime;
            transform.position += (Vector3)(d.normalized * Mathf.Min(speed * Time.deltaTime, dist));
            if (dist < 0.3f)
            {
                AudioManager.Play("gem", 0.35f, 0.9f + Mathf.Min(value, 20) * 0.03f + Random.value * 0.1f, 0.025f);
                run.AddXP(value);
                pool.Release(this);
            }
        }
        else
        {
            bob += Time.deltaTime * 4f;
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Sin(bob) * 10f);
        }
    }
}
