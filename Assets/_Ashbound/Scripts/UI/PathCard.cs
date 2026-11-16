using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PathCard : MonoBehaviour
{
    [SerializeField] TMP_Text nameText, skillText, playsText;
    [SerializeField] Image dot, selectionRing;
    [SerializeField] GameObject lockedLabel;
    [SerializeField] Button button;
    [SerializeField] CanvasGroup group;

    public PathData Data { get; private set; }
    public bool Unlocked { get; private set; }

    public void Setup(PathData data, bool unlocked, string lockHint, Action<PathCard> onPick)
    {
        Data = data;
        Unlocked = unlocked;
        nameText.text = data.pathName;
        skillText.text = data.startingSkill;
        playsText.text = data.playsLike;
        dot.color = data.color;
        selectionRing.color = data.color;
        lockedLabel.SetActive(!unlocked);
        var label = lockedLabel.GetComponent<TMP_Text>();
        if (label != null) label.text = "Locked · " + lockHint;
        group.alpha = unlocked ? 1f : 0.6f;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onPick(this));
        SetSelected(false);
    }

    public void SetSelected(bool on) => selectionRing.enabled = on;
}
