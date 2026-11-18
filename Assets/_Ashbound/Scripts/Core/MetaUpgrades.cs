/// <summary>Permanent upgrades bought with gold in the main menu.</summary>
public static class MetaUpgrades
{
    public class Def
    {
        public string id, name, description;
        public int maxLevel, baseCost;
        public float perLevel;
        public string iconName;
        public int Cost(int currentLevel) => baseCost * (currentLevel + 1);
    }

    public static readonly Def[] All =
    {
        new() { id = "health",  name = "Vitality",  description = "+10 max health",           maxLevel = 5, baseCost = 60,  perLevel = 10f,  iconName = "icon_vitality" },
        new() { id = "damage",  name = "Might",     description = "+5% damage",               maxLevel = 5, baseCost = 80,  perLevel = 0.05f, iconName = "icon_might" },
        new() { id = "speed",   name = "Swiftness", description = "+4% movement speed",       maxLevel = 5, baseCost = 60,  perLevel = 0.04f, iconName = "icon_swiftness" },
        new() { id = "pickup",  name = "Magnet",    description = "+10% XP pickup range",     maxLevel = 5, baseCost = 50,  perLevel = 0.10f, iconName = "icon_magnet" },
        new() { id = "greed",   name = "Greed",     description = "+10% gold from runs",      maxLevel = 5, baseCost = 70,  perLevel = 0.10f, iconName = "pickup_gold" },
        new() { id = "rerolls", name = "Fortune",   description = "+1 reroll per run, better boss chests", maxLevel = 3, baseCost = 120, perLevel = 1f,    iconName = "pickup_chest" },
    };

    public static float Bonus(string id)
    {
        foreach (var d in All)
            if (d.id == id) return SaveSystem.UpgradeLevel(id) * d.perLevel;
        return 0f;
    }
}
