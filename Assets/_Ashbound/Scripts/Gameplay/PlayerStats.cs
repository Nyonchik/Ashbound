using System;
using System.Collections;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public float baseMaxHealth = 120f;
    public float baseMoveSpeed = 4.2f;
    public float basePickupRadius = 1.8f;
    public float invulnerableTime = 0.75f;

    [HideInInspector] public float damageMul = 1f, cooldownMul = 1f, areaMul = 1f, moveMul = 1f, pickupMul = 1f;
    float bonusHealth;

    [SerializeField] SpriteRenderer[] flashRenderers;
    [Tooltip("Faint circle that shows the XP pickup radius")] [SerializeField] Transform pickupRing;
    Color[] baseColors;
    float invuln;

    public float MaxHealth => baseMaxHealth + bonusHealth;
    public float Health { get; private set; }
    public float MoveSpeed => baseMoveSpeed * moveMul;
    public float PickupRadius => basePickupRadius * pickupMul;
    public bool IsDead => Health <= 0f;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;

    void Awake()
    {
        Health = MaxHealth;
        baseColors = new Color[flashRenderers.Length];
        for (int i = 0; i < flashRenderers.Length; i++) baseColors[i] = flashRenderers[i].color;
    }

    void Update()
    {
        if (invuln > 0f) invuln -= Time.deltaTime;
        if (pickupRing != null) pickupRing.localScale = Vector3.one * (PickupRadius * 2f);
    }

    [System.NonSerialized] public bool godMode;   // editor test key G

    public void TakeDamage(float amount)
    {
        if (IsDead || invuln > 0f || godMode) return;
        Health = Mathf.Max(0f, Health - amount);
        invuln = invulnerableTime;
        AudioManager.Play("hurt", 0.7f, 1f, 0.1f);
        CameraFollow.Shake(0.12f, 0.15f);
        StopAllCoroutines();
        StartCoroutine(Flash());
        OnHealthChanged?.Invoke(Health, MaxHealth);
        if (Health <= 0f) OnDied?.Invoke();
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Health = Mathf.Min(MaxHealth, Health + amount);
        OnHealthChanged?.Invoke(Health, MaxHealth);
    }

    public void AddMaxHealth(float amount)
    {
        bonusHealth += amount;
        Health += amount;
        OnHealthChanged?.Invoke(Health, MaxHealth);
    }

    public void SetBaseColor(SpriteRenderer r, Color c)
    {
        for (int i = 0; i < flashRenderers.Length; i++)
            if (flashRenderers[i] == r) baseColors[i] = c;
        r.color = c;
    }

    IEnumerator Flash()
    {
        for (int i = 0; i < flashRenderers.Length; i++) flashRenderers[i].color = Color.white;
        yield return new WaitForSeconds(0.08f);
        for (int i = 0; i < flashRenderers.Length; i++) flashRenderers[i].color = Color.Lerp(baseColors[i], Palette.Health, 0.5f);
        yield return new WaitForSeconds(0.08f);
        for (int i = 0; i < flashRenderers.Length; i++) flashRenderers[i].color = baseColors[i];
    }
}
