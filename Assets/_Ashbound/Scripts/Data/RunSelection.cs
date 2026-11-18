/// <summary>Carries the chosen class, Path, stage and mode from the menus into the Game scene.</summary>
public static class RunSelection
{
    public static ClassData Class;
    public static PathData Path;
    public static StageData Stage;
    public static RunMode Mode = RunMode.Normal;
    /// <summary>Chosen Ascension (difficulty) level, 0-10.</summary>
    public static int Ascension;
    /// <summary>Daily challenge: the two Ascension curses (1-10) active today.</summary>
    public static int CurseA, CurseB;
    public static bool DailyCurse(int level) => Mode == RunMode.Daily && (level == CurseA || level == CurseB);
}
