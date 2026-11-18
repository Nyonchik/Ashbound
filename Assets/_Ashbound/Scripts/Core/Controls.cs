using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>Rebindable keyboard controls, saved with the rest of the settings. Arrow keys always move too.</summary>
public static class Controls
{
    public enum Action { Up, Down, Left, Right, Dash, Pause }
    public static readonly Action[] All = { Action.Up, Action.Down, Action.Left, Action.Right, Action.Dash, Action.Pause };
    static readonly Key[] Defaults = { Key.W, Key.S, Key.A, Key.D, Key.Space, Key.Escape };

    public static string Label(Action a) => a switch
    {
        Action.Up => "Move up", Action.Down => "Move down", Action.Left => "Move left", Action.Right => "Move right",
        Action.Dash => "Dash (Assassin)", _ => "Pause",
    };

    public static Key Get(Action a)
    {
        var d = SaveSystem.Data;
        int i = (int)a;
        if (d.keyBinds != null && i < d.keyBinds.Count && Enum.TryParse(d.keyBinds[i], out Key k) && k != Key.None) return k;
        return Defaults[i];
    }

    public static void Set(Action a, Key key)
    {
        var d = SaveSystem.Data;
        while (d.keyBinds.Count < All.Length) d.keyBinds.Add(Defaults[d.keyBinds.Count].ToString());
        // a key can only do one thing: swap with whatever had it
        for (int i = 0; i < All.Length; i++)
            if (i != (int)a && Get(All[i]) == key) d.keyBinds[i] = Get(a).ToString();
        d.keyBinds[(int)a] = key.ToString();
        SaveSystem.Save();
        Changed?.Invoke();
    }

    public static void ResetDefaults()
    {
        SaveSystem.Data.keyBinds.Clear();
        SaveSystem.Save();
        Changed?.Invoke();
    }

    public static event System.Action Changed;

    public static string KeyName(Key k)
    {
        var kb = Keyboard.current;
        if (kb != null) { var c = kb[k]; if (c != null && !string.IsNullOrEmpty(c.displayName)) return c.displayName; }
        return k.ToString();
    }

    public static string Path(Action a) => "<Keyboard>/" + (Keyboard.current != null && Keyboard.current[Get(a)] != null ? Keyboard.current[Get(a)].name : Get(a).ToString().ToLowerInvariant());

    public static bool Pressed(Action a)
    {
        var kb = Keyboard.current;
        return kb != null && kb[Get(a)].wasPressedThisFrame;
    }

    /// <summary>The key pressed this frame, if any (for rebinding).</summary>
    public static Key AnyKeyPressed()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.anyKey.wasPressedThisFrame) return Key.None;
        foreach (KeyControl k in kb.allKeys) if (k != null && k.wasPressedThisFrame) return k.keyCode;
        return Key.None;
    }
}
