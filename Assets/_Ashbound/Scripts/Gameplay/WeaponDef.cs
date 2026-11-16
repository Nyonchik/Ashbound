using UnityEngine;

public enum WeaponKind { Projectile, Orbit, Nova }

/// <summary>Bonus added at one upgrade step. Fractions: 0.25 = +25%. cooldown 0.2 = 20% faster.</summary>
[System.Serializable]
public struct LevelBonus
{
    public float damage;
    public int count;
    public float cooldown;
    public float area;
    public int pierce;
    public bool explode;
}

[CreateAssetMenu(menuName = "Ashbound/Weapon")]
public class WeaponDef : ScriptableObject
{
    public string weaponName;
    public string awakeningName;
    public Color color = Color.white;
    public WeaponKind kind;
    public Sprite icon;
    [Tooltip("Projectile / orbiting sprite (optional)")] public Sprite projectile;

    [Header("Base stats (Lv 1)")]
    public float damage = 10f;
    public float cooldown = 1f;
    [Tooltip("Projectile speed, or orbit spin speed")] public float speed = 9f;
    public float area = 1f;
    public float lifetime = 2f;
    public float knockback = 0f;
    public int count = 1;
    public int pierce = 0;
    public bool explode;

    [Header("Upgrades")]
    [Tooltip("Lv 2, Lv 3, Lv 4, Lv 5")] public LevelBonus[] levels = new LevelBonus[4];
    public LevelBonus awakening;

    [Header("Evolution (class weapons)")]
    [Tooltip("At Lv 5, owning this passive lets the weapon evolve")] public PassiveDef evolvePassive;
    public WeaponDef evolvesInto;
    public bool isEvolution;

    [Tooltip("[0] Lv 1, [1..4] Lv 2-5, [5] Awakening")]
    [TextArea] public string[] levelText = new string[6];
}
