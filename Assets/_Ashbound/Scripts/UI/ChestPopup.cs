using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Boss chest opening: the chest drops in, shakes, bursts open with a glow, then shows the reward.</summary>
public class ChestPopup : MonoBehaviour
{
    [SerializeField] CanvasGroup group;
    [SerializeField] RectTransform chest;
    [SerializeField] RectTransform glow;
    [SerializeField] TMP_Text title, reward;

    public bool IsOpen => gameObject.activeSelf;

    public void Play(int gold, Action done)
    {
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(Run(gold, done));
    }

    IEnumerator Run(int gold, Action done)
    {
        GameSpeed.Pause();
        group.alpha = 0f;
        chest.localScale = Vector3.zero;
        chest.localRotation = Quaternion.identity;
        glow.localScale = Vector3.zero;
        title.text = "TREASURE!";
        reward.text = "";

        float t = 0f;
        while (t < 1f) { t += Time.unscaledDeltaTime / 0.15f; group.alpha = Mathf.Clamp01(t); yield return null; }

        // drop in with a bounce
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.35f;
            float k = Mathf.Clamp01(t);
            float s = k < 0.7f ? Mathf.Lerp(0f, 1.2f, k / 0.7f) : Mathf.Lerp(1.2f, 1f, (k - 0.7f) / 0.3f);
            chest.localScale = Vector3.one * s;
            yield return null;
        }

        // shake
        t = 0f;
        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            chest.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 60f) * 8f * (1f - t * 1.5f));
            yield return null;
        }
        chest.localRotation = Quaternion.identity;

        // burst open
        AudioManager.Play("evolve", 0.8f);
        AudioManager.Play("coin", 1f);
        reward.text = $"<color=#F2B33D>+{gold} gold</color>\n+1 level up";
        t = 0f;
        while (t < 1.1f)
        {
            t += Time.unscaledDeltaTime;
            glow.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.4f, Mathf.Clamp01(t / 0.3f));
            glow.Rotate(0f, 0f, 60f * Time.unscaledDeltaTime);
            chest.localScale = Vector3.one * (1f + Mathf.Sin(t * 9f) * 0.04f);
            yield return null;
        }

        t = 1f;
        while (t > 0f) { t -= Time.unscaledDeltaTime / 0.2f; group.alpha = Mathf.Clamp01(t); yield return null; }
        gameObject.SetActive(false);
        GameSpeed.Resume();
        done?.Invoke();
    }
}
