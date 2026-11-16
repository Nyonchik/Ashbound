using UnityEngine;

/// <summary>Central place for time scale, so pausing and resuming keeps the x2 speed choice.</summary>
public static class GameSpeed
{
    public static bool Fast;
    public static float Scale => Fast ? 2f : 1f;
    public static void Resume() => Time.timeScale = Scale;
    public static void Pause() => Time.timeScale = 0f;
}
