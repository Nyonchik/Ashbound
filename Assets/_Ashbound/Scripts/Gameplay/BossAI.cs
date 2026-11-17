using System.Collections;
using UnityEngine;

/// <summary>
/// Boss attacks. 0 Ash Warden: telegraphed charges that leave fire. 1 Rotting Matriarch: summons minions, spits poison.
/// 2 Hollow King: rings of bullets and teleports. Harder stages (tier 1-2) attack faster and borrow other bosses' moves.
/// </summary>
public class BossAI : MonoBehaviour
{
    Enemy enemy;
    int kind, tier;
    float timerA, timerB, timerC;
    bool busy;
    float dmgMul = 1f;

    public void Begin(Enemy e, int bossIndex, int stageOrder)
    {
        StopAllCoroutines();
        enemy = e; kind = bossIndex % 3; tier = Mathf.Clamp(stageOrder, 0, 2);
        dmgMul = RunSelection.Stage != null ? RunSelection.Stage.enemyDamage : 1f;
        timerA = 2.5f; timerB = 4f; timerC = 6f; busy = false;
        enabled = true;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (enemy != null) enemy.ForcedVelocity = null;
        busy = false;
    }

    float Rate => tier == 0 ? 1f : tier == 1 ? 0.8f : 0.65f;
    static PlayerController P => PlayerController.Instance;
    static RunManager Run => RunManager.Instance;
    Vector2 Pos => transform.position;

    void Update()
    {
        if (enemy == null || P == null || Run == null || Run.IsOver || busy) return;
        float dt = Time.deltaTime;
        timerA -= dt; timerB -= dt; timerC -= dt;

        switch (kind)
        {
            case 0:
                if (timerA <= 0f) { timerA = 4.5f * Rate; StartCoroutine(Charge()); }
                if (tier >= 1 && timerB <= 0f) { timerB = 6f * Rate; BulletRing(8 + tier * 2, 3.5f); }
                break;
            case 1:
                if (timerA <= 0f) { timerA = 7f * Rate; Summon(3 + tier); }
                if (timerB <= 0f) { timerB = 3.4f * Rate; StartCoroutine(Spit(1 + tier)); }
                if (tier >= 2 && timerC <= 0f) { timerC = 7f; StartCoroutine(Charge()); }
                break;
            default:
                if (timerA <= 0f) { timerA = 3.2f * Rate; BulletRing(14 + tier * 4, 4f + tier * 0.5f); }
                if (timerB <= 0f) { timerB = 7f * Rate; StartCoroutine(Teleport()); }
                if (tier >= 1 && timerC <= 0f) { timerC = 5f * Rate; StartCoroutine(Spit(tier)); }
                break;
        }
    }

    // Ash Warden: wind up, dash at the player, leave burning ground behind
    IEnumerator Charge()
    {
        busy = true;
        enemy.ForcedVelocity = Vector2.zero;
        Run.SpawnVfx(Pos, 2.4f, new Color(1f, 0.35f, 0.2f), 0.75f);
        yield return new WaitForSeconds(0.75f);
        Vector2 dir = ((Vector2)P.transform.position - Pos).normalized;
        enemy.ForcedVelocity = dir * (9f + tier * 1.5f);
        float t = 0f, drop = 0f;
        while (t < 0.75f)
        {
            t += Time.deltaTime; drop -= Time.deltaTime;
            if (drop <= 0f)
            {
                drop = 0.12f;
                Hazard.Spawn(Run.Art.firePatch, Pos, new Color(1f, 0.55f, 0.25f), 0.85f, 4f + tier, 7f * dmgMul);
            }
            yield return null;
        }
        enemy.ForcedVelocity = null;
        CameraFollow.Shake(0.1f, 0.15f);
        busy = false;
    }

    // Rotting Matriarch: call in a ring of minions
    void Summon(int n)
    {
        Run.SpawnVfx(Pos, 2.5f, new Color(0.6f, 0.9f, 0.4f), 0.5f);
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            enemy.Owner.SpawnMinion(Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.8f);
        }
    }

    // Lob poison globs at (around) the player; they become puddles
    IEnumerator Spit(int globs)
    {
        for (int g = 0; g < globs; g++)
        {
            Vector2 target = (Vector2)P.transform.position + (g == 0 ? Vector2.zero : Random.insideUnitCircle * 2.5f);
            StartCoroutine(Glob(target));
            yield return new WaitForSeconds(0.15f);
        }
    }

    IEnumerator Glob(Vector2 target)
    {
        const float flight = 0.8f;
        Vector2 start = Pos + Vector2.up * 1.5f;
        Run.Fx(Run.Art.poison, start, new Color(0.6f, 1f, 0.4f), flight, 19).Move(target).Scale(0.35f, 0.6f).Spin(300f).FadeFrom(1f);
        Run.SpawnVfx(target, 1.3f, new Color(0.5f, 1f, 0.3f, 0.6f), flight);   // landing marker
        yield return new WaitForSeconds(flight);
        Hazard.Spawn(Run.Art.poison, target, Color.white, 1.3f, 5f, 6f * dmgMul);
    }

    // Hollow King: ring of bullets
    void BulletRing(int n, float speed)
    {
        float off = Random.value * 360f;
        for (int i = 0; i < n; i++)
        {
            float a = (off + i * 360f / n) * Mathf.Deg2Rad;
            Vector2 d = new(Mathf.Cos(a), Mathf.Sin(a));
            BossBullet.Spawn(Run.Art.bossBullet, Pos + Vector2.up * 1.2f + d * 0.6f, d * speed, 10f * dmgMul, 7f);
        }
        AudioManager.Play("hurt", 0.4f, 0.6f, 0.1f);
    }

    IEnumerator Teleport()
    {
        busy = true;
        enemy.ForcedVelocity = Vector2.zero;
        Run.SpawnVfx(Pos, 2f, new Color(0.6f, 0.4f, 1f), 0.4f);
        yield return new WaitForSeconds(0.4f);
        Vector2 to = (Vector2)P.transform.position + Random.insideUnitCircle.normalized * 4.5f;
        enemy.Teleport(to);
        Run.SpawnVfx(to, 2.5f, new Color(0.6f, 0.4f, 1f), 0.5f);
        yield return new WaitForSeconds(0.3f);
        BulletRing(10 + tier * 2, 3.5f);
        enemy.ForcedVelocity = null;
        busy = false;
    }
}

