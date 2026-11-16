using UnityEngine;

public enum PassiveStat { Damage, Cooldown, MoveSpeed, MaxHealth, Area, Pickup }

[CreateAssetMenu(menuName = "Ashbound/Passive")]
public class PassiveDef : ScriptableObject
{
    public string passiveName;
    public Color color = Color.white;
    public PassiveStat stat;
    public Sprite icon;
    [Tooltip("Per level. Fractions for %, flat HP for MaxHealth.")] public float perLevel = 0.1f;
    [TextArea] public string description;
}
