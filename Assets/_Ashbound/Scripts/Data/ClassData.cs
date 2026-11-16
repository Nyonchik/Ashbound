using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Ashbound/Class")]
public class ClassData : ScriptableObject
{
    public string className;
    public Color color = Color.white;
    public string role;
    [TextArea] public string mechanic;
    public string defaultWeapon;
    public Sprite portrait;
    public bool unlocked = true;
    public List<PathData> paths = new();
    [Tooltip("Weapons this class can find in level-ups (besides its other Paths' skills)")] public List<WeaponDef> weapons = new();
    [Tooltip("Path skill used by Paths that don't have their own weapon yet")] public WeaponDef defaultPathWeapon;
    [TextArea] public string unlockHint;
}
