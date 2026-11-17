using UnityEngine;

[CreateAssetMenu(menuName = "Ashbound/Path")]
public class PathData : ScriptableObject
{
    public string pathName;
    public Color color = Color.white;
    public string startingSkill;
    [TextArea] public string playsLike;
    [TextArea] public string ascension;
    public bool unlocked = true;
    [Tooltip("The Path skill the hero starts the run with")] public WeaponDef weapon;
    [Tooltip("This Path's version of the hero (outfit/colors). Falls back to the class portrait.")] public Sprite heroArt;
}
