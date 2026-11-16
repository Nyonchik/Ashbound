using System.Collections.Generic;
using UnityEngine;

public enum PickupKind { Meat, Medkit, Magnet, GoldBag, Chest }

/// <summary>Item dropped on the ground. Shows an off-screen tracker on the HUD until it's collected.</summary>
public class Pickup : MonoBehaviour
{
    public static readonly List<Pickup> Active = new();

    [SerializeField] SpriteRenderer sprite;
    Vector3 basePos;
    float bob;

    public PickupKind Kind { get; private set; }
    public Sprite Icon => sprite.sprite;

    public void Init(PickupKind kind, Sprite icon, Vector2 position)
    {
        Kind = kind;
        sprite.sprite = icon;
        basePos = position;
        transform.position = position;
        bob = Random.value * 6f;
        Active.Add(this);
    }

    void OnDisable() => Active.Remove(this);

    void Update()
    {
        bob += Time.deltaTime * 3f;
        transform.position = basePos + Vector3.up * (Mathf.Sin(bob) * 0.08f);
        var p = PlayerController.Instance;
        if (p == null) return;
        if (((Vector2)p.transform.position - (Vector2)basePos).sqrMagnitude < 0.75f * 0.75f)
        {
            RunManager.Instance?.CollectPickup(this);
            Destroy(gameObject);
        }
    }
}
