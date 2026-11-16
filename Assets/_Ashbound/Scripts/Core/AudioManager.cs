using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Plays sound effects and music from Resources/Audio. Created on first use and kept between scenes.</summary>
public class AudioManager : MonoBehaviour
{
    static AudioManager instance;

    readonly Dictionary<string, AudioClip> clips = new();
    readonly Dictionary<string, float> lastPlayed = new();
    readonly List<AudioSource> sfx = new();
    AudioSource music;
    string currentMusic;
    int next;

    static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("AudioManager");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<AudioManager>();
                instance.Init();
            }
            return instance;
        }
    }

    void Init()
    {
        foreach (var c in Resources.LoadAll<AudioClip>("Audio")) clips[c.name] = c;
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.playOnAwake = false;
        for (int i = 0; i < 16; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            sfx.Add(s);
        }
        ApplyVolumes();
        SceneManager.sceneLoaded += (scene, mode) => HookButtons();
        HookButtons();
    }

    public static void ApplyVolumes()
    {
        var a = Instance;
        a.music.volume = SaveSystem.Data.musicVolume * 0.55f;
    }

    public static void Play(string name, float volume = 1f, float pitch = 1f, float minInterval = 0.03f)
    {
        var a = Instance;
        if (!a.clips.TryGetValue(name, out var clip)) return;
        float now = Time.unscaledTime;
        if (a.lastPlayed.TryGetValue(name, out float last) && now - last < minInterval) return;
        a.lastPlayed[name] = now;
        var s = a.sfx[a.next];
        a.next = (a.next + 1) % a.sfx.Count;
        s.pitch = pitch;
        s.PlayOneShot(clip, volume * SaveSystem.Data.sfxVolume);
    }

    public static void PlayMusic(string name)
    {
        var a = Instance;
        if (a.currentMusic == name) return;
        a.currentMusic = name;
        if (!a.clips.TryGetValue(name, out var clip)) { a.music.Stop(); return; }
        a.music.clip = clip;
        a.music.Play();
    }

    /// <summary>Adds a click sound to every button in the loaded scenes that doesn't have one yet.</summary>
    public static void HookButtons()
    {
        foreach (var b in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (!b.gameObject.scene.IsValid() || b.GetComponent<ClickSoundHooked>() != null) continue;
            b.gameObject.AddComponent<ClickSoundHooked>();
            b.onClick.AddListener(() => Play("click", 0.6f));
        }
    }
}
