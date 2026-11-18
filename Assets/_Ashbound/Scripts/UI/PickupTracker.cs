using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shows an arrow at the screen edge pointing to each pickup that is off-screen.</summary>
public class PickupTracker : MonoBehaviour
{
    [SerializeField] RectTransform container;
    [SerializeField] Sprite backgroundSprite, arrowSprite;
    [SerializeField] float margin = 70f;
    [SerializeField] int maxShown = 10;
    [Tooltip("Icons for off-screen elites and bosses")] [SerializeField] Sprite eliteIcon, bossIcon;

    class Indicator { public RectTransform root, arrow; public Image icon, arrowImg; }
    readonly List<Indicator> pool = new();

    void LateUpdate()
    {
        var cam = Camera.main;
        int used = 0;
        if (cam != null)
        {
            Rect r = container.rect;
            Vector2 half = new(r.width / 2f - margin, r.height / 2f - margin);
            foreach (var p in Pickup.Active)
            {
                if (used >= maxShown) break;
                if (Point(cam, p.transform.position, half, used, p.Icon, Color.white, Palette.Accent)) used++;
            }
            // golden elites and bosses
            foreach (var e in Enemy.Active)
            {
                if (used >= maxShown) break;
                if (!e.IsElite && !e.IsBoss) continue;
                bool boss = e.IsBoss;
                var ic = boss ? bossIcon : eliteIcon;
                if (Point(cam, e.transform.position, half, used, ic, boss ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.85f, 0.4f), boss ? Palette.Health : Palette.Gold)) used++;
            }
        }
        for (int i = used; i < pool.Count; i++) pool[i].root.gameObject.SetActive(false);
    }

    bool Point(Camera cam, Vector3 world, Vector2 half, int index, Sprite icon, Color iconColor, Color arrowColor)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.x > 0 && sp.x < Screen.width && sp.y > 0 && sp.y < Screen.height) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(container, sp, null, out Vector2 local)) return false;
        if (local.sqrMagnitude < 0.01f) return false;
        Vector2 dir = local.normalized;
        float t = Mathf.Min(half.x / Mathf.Max(0.0001f, Mathf.Abs(dir.x)), half.y / Mathf.Max(0.0001f, Mathf.Abs(dir.y)));
        var ind = Get(index);
        ind.root.gameObject.SetActive(true);
        ind.root.anchoredPosition = dir * t;
        ind.icon.sprite = icon; ind.icon.color = iconColor; ind.icon.enabled = icon != null;
        ind.arrowImg.color = arrowColor;
        ind.arrow.anchoredPosition = dir * 46f;
        ind.arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        return true;
    }

    Indicator Get(int i)
    {
        while (pool.Count <= i)
        {
            var root = new GameObject("Tracker", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            root.SetParent(container, false);
            root.sizeDelta = new Vector2(66, 66);
            var bg = root.GetComponent<Image>();
            bg.sprite = backgroundSprite; bg.color = new Color(0.14f, 0.13f, 0.17f, 0.92f); bg.raycastTarget = false;

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.rectTransform.SetParent(root, false);
            icon.rectTransform.sizeDelta = new Vector2(48, 48);
            icon.preserveAspect = true; icon.raycastTarget = false;

            var arrow = new GameObject("Arrow", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            arrow.rectTransform.SetParent(root, false);
            arrow.rectTransform.sizeDelta = new Vector2(28, 28);
            arrow.sprite = arrowSprite; arrow.color = Palette.Accent; arrow.raycastTarget = false;

            pool.Add(new Indicator { root = root, icon = icon, arrow = arrow.rectTransform, arrowImg = arrow });
        }
        return pool[i];
    }
}
