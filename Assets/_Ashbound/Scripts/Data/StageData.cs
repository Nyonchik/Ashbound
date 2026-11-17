using UnityEngine;

[CreateAssetMenu(menuName = "Ashbound/Stage")]
public class StageData : ScriptableObject
{
    public string stageName;
    [TextArea] public string description;
    public int order;
    public Color groundTint = Color.white;
    public Color enemyTint = Color.white;
    public float enemyHealth = 1f;
    public float enemyDamage = 1f;
    public float spawnRate = 1f;
    public float goldMultiplier = 1f;
    [Tooltip("Optional art for this stage's walker, hound and brute (same order as the spawner's enemy types)")]
    public Sprite[] enemySprites = new Sprite[3];
    [Tooltip("Optional ground tile for this stage")] public Sprite groundTile;
}
