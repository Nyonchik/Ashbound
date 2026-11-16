using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpScreen : MonoBehaviour
{
    [SerializeField] UpgradeCard[] cards = new UpgradeCard[3];
    [SerializeField] TMP_Text subtitle;
    [SerializeField] Button rerollButton, banishButton;
    [SerializeField] TMP_Text rerollLabel, banishLabel;

    public event Action<UpgradeOption> OnPicked;
    public event Action OnRerollRequested;
    public event Action<UpgradeOption> OnBanished;
    public bool IsOpen => gameObject.activeSelf;

    int rerolls, banishes;
    bool banishMode, wired;

    public void SetCharges(int rerollCount, int banishCount)
    {
        rerolls = rerollCount;
        banishes = banishCount;
        Wire();
        UpdateLabels();
    }

    void Wire()
    {
        if (wired) return;
        wired = true;
        if (rerollButton != null) rerollButton.onClick.AddListener(() =>
        {
            if (rerolls <= 0) return;
            rerolls--;
            banishMode = false;
            UpdateLabels();
            OnRerollRequested?.Invoke();
        });
        if (banishButton != null) banishButton.onClick.AddListener(() =>
        {
            if (banishes <= 0) return;
            banishMode = !banishMode;
            UpdateLabels();
        });
    }

    void UpdateLabels()
    {
        if (rerollLabel != null) rerollLabel.text = $"Reroll ({rerolls} left)";
        if (banishLabel != null) banishLabel.text = banishMode ? "Click a card to banish" : $"Banish ({banishes} left)";
        if (rerollButton != null) rerollButton.interactable = rerolls > 0;
        if (banishButton != null) banishButton.interactable = banishes > 0;
    }

    public void Show(int newLevel, List<UpgradeOption> options)
    {
        Wire();
        bool wasOpen = gameObject.activeSelf;
        subtitle.text = $"Level {newLevel} · choose one";
        for (int i = 0; i < cards.Length; i++)
        {
            bool has = i < options.Count;
            cards[i].gameObject.SetActive(has);
            if (has) cards[i].Setup(options[i], Pick);
        }
        gameObject.SetActive(true);
        GameSpeed.Pause();
        if (!wasOpen) AudioManager.Play("levelup", 0.8f);
        UpdateLabels();
    }

    void Pick(UpgradeOption o)
    {
        if (banishMode)
        {
            banishMode = false;
            banishes--;
            UpdateLabels();
            OnBanished?.Invoke(o);
            return;
        }
        gameObject.SetActive(false);
        GameSpeed.Resume();
        OnPicked?.Invoke(o);
    }

    [ContextMenu("Show test cards")]
    public void ShowTest() => ShowTest(15);

    public void ShowTest(int level)
    {
        Show(level, new List<UpgradeOption>
        {
            new() { tag = "AWAKENING", name = "Inferno Core", levelLine = "Fireball · Lv 5 » Awakened",
                    effect = "Fireballs split into 3 extra explosions on impact",
                    color = new Color(1f, 0.42f, 0.21f), fromLevel = 5, toLevel = 6 },
            new() { tag = "WEAPON", name = "Frost Nova", levelLine = "Lv 2 » Lv 3",
                    effect = "Nova now freezes enemies for 0.5s",
                    color = new Color(0.24f, 0.65f, 0.96f), fromLevel = 2, toLevel = 3 },
            new() { tag = "NEW WEAPON", name = "Magic Missile", levelLine = "Lv 1",
                    effect = "Fires 2 homing missiles at the nearest enemies",
                    color = new Color(0.73f, 0.65f, 1f), fromLevel = 0, toLevel = 1 },
        });
    }
}
