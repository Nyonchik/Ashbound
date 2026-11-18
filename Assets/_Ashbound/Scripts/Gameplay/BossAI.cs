using System.Collections;
using UnityEngine;

/// <summary>
/// Boss attacks. Each stage has its own three bosses (kind = stage * 3 + slot):
///  Ashen Fields:   0 Ash Warden (charge + fire trail)       1 Rotting Matriarch (minions + poison)  2 Hollow King (bullet rings + teleport)
///  Hollow Forest:  3 Elder Treant (root lines + saplings)   4 Gray Fang (leaps + wolf pack)         5 Mycelium Colossus (spore fields + spit)
///  Ruined Capital: 6 Fallen Champion (charge + sword waves) 7 Plague Alchemist (flask rain + spit)  8 Hollow Emperor (spirals + teleport + rings)
/// Later stages attack a little faster. Every attack is telegraphed so it can be dodged.
/// </summary>
public class BossAI : MonoBehaviour
{
    Enemy enemy;
    int kind, tier;
    float timerA, timerB, timerC;
    bool busy;
    float dmgMul = 1f;

    public void Begin(Enemy e, int bossKind, int stageOrder)
    {
        StopAllCoroutines();
        enemy = e; kind = Mathf.Clamp(bossKind, 0, 8); tier = Mathf.Clamp(stageOrder, 0, 2);
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

    float Rate => tier == 0 ? 1f : tier == 1 ? 0.9f : 0.8f;
    static PlayerController P => PlayerController.Instance;
    static RunManager Run => RunManager.Instance;
    Vector2 Pos => transform.position;
    Vector2 PlayerPos => P.transform.position;

    void Update()
    {
        if (enemy == null || P == null || Run == null || Run.IsOver || busy) return;
        float dt = Time.deltaTime;
        timerA -= dt; timerB -= dt; timerC -= dt;

        switch (kind)
        {
            case 0: // Ash Warden
                if (timerA <= 0f) { timerA = 4.5f * Rate; StartCoroutine(Charge(true)); }
                break;
            case 1: // Rotting Matriarch
                if (timerA <= 0f) { timerA = 7f * Rate; Summon(3, 0); }
                if (timerB <= 0f) { timerB = 3.4f * Rate; StartCoroutine(Spit(1)); }
                break;
            case 2: // Hollow King
                if (timerA <= 0f) { timerA = 3.2f * Rate; BulletRing(14, 4f); }
                if (timerB <= 0f) { timerB = 7f * Rate; StartCoroutine(Teleport()); }
                break;
            case 3: // Elder Treant
                if (timerA <= 0f) { timerA = 4f * Rate; StartCoroutine(RootLines(3)); }
                if (timerB <= 0f) { timerB = 8f * Rate; Summon(4, 0); }
                break;
            case 4: // Gray Fang
                if (timerA <= 0f) { timerA = 3.6f * Rate; StartCoroutine(Leap()); }
                if (timerB <= 0f) { timerB = 9f * Rate; Summon(4, 1); }
                break;
            case 5: // Mycelium Colossus
                if (timerA <= 0f) { timerA = 6f * Rate; SporeFields(3); }
                if (timerB <= 0f) { timerB = 3.8f * Rate; StartCoroutine(Spit(2)); }
                if (timerC <= 0f) { timerC = 9f * Rate; BulletRing(10, 3f); }
                break;
            case 6: // Fallen Champion
                if (timerA <= 0f) { timerA = 5f * Rate; StartCoroutine(Charge(false)); }
                if (timerB <= 0f) { timerB = 2.8f * Rate; StartCoroutine(SwordWave(5)); }
                break;
            case 7: // Plague Alchemist
                if (timerA <= 0f) { timerA = 4.5f * Rate; StartCoroutine(FlaskRain(6)); }
                if (timerB <= 0f) { timerB = 3.2f * Rate; StartCoroutine(Spit(1)); }
                break;
            default: // Hollow Emperor
                if (timerA <= 0f) { timerA = 6f * Rate; StartCoroutine(Spiral(2.4f)); }
                if (timerB <= 0f) { timerB = 8f * Rate; StartCoroutine(Teleport()); }
                if (timerC <= 0f) { timerC = 3.5f * Rate; BulletRing(16, 4f); }
                break;
        }
    }

    // ---------------- moves

    // Wind up, then dash at the player. The Ash Warden leaves burning ground.
    IEnumerator Charge(bool fireTrail)
    {
        busy = true;
        enemy.ForcedVelocity = Vector2.zero;
        Run.SpawnVfx(Pos, 2.4f, new Color(1f, 0.35f, 0.2f), 0.75f);
        yield return new WaitForSeconds(0.75f);
        Vector2 dir = (PlayerPos - Pos).normalized;
        enemy.ForcedVelocity = dir * 9.5f;
        float t = 0f, drop = 0f;
        while (t < 0.75f)
        {
            t += Time.deltaTime; drop -= Time.deltaTime;
            if (fireTrail && drop <= 0f)
            {
                drop = 0.12f;
                Hazard.Spawn(Run.Art.firePatch, Pos, new Color(1f, 0.55f, 0.25f), 0.85f, 4f, 7f * dmgMul);
            }
            yield return null;
        }
        enemy.ForcedVelocity = null;
        CameraFollow.Shake(0.1f, 0.15f);
        busy = false;
    }

    // Call in a ring of minions (type 0 walker, 1 hound/wolf)
    void Summon(int n, int type)
    {
        Run.SpawnVfx(Pos, 2.5f, new Color(0.6f, 0.9f, 0.4f), 0.5f);
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            enemy.Owner.SpawnMinion(Pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.8f, type);
        }
    }

    // Lob poison globs at (around) the player; they become puddles
    IEnumerator Spit(int globs)
    {
        for (int g = 0; g < globs; g++)
        {
            Vector2 target = PlayerPos + (g == 0 ? Vector2.zero : Random.insideUnitCircle * 2.5f);
            StartCoroutine(Glob(target, 1.3f));
            yield return new WaitForSeconds(0.15f);
        }
    }

    IEnumerator Glob(Vector2 target, float radius)
    {
        const float flight = 0.8f;
        Vector2 start = Pos + Vector2.up * 1.5f;
        Run.Fx(Run.Art.poison, start, new Color(0.6f, 1f, 0.4f), flight, 19).Move(target).Scale(0.35f, 0.6f).Spin(300f).FadeFrom(1f);
        Run.SpawnVfx(target, radius, new Color(0.5f, 1f, 0.3f, 0.6f), flight);   // landing marker
        yield return new WaitForSeconds(flight);
        Hazard.Spawn(Run.Art.poison, target, Color.white, radius, 5f, 6f * dmgMul);
    }

    // Ring of bullets
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
        Vector2 to = PlayerPos + Random.insideUnitCircle.normalized * 4.5f;
        enemy.Teleport(to);
        Run.SpawnVfx(to, 2.5f, new Color(0.6f, 0.4f, 1f), 0.5f);
        yield return new WaitForSeconds(0.3f);
        BulletRing(10, 3.5f);
        enemy.ForcedVelocity = null;
        busy = false;
    }

    // Elder Treant: lines of roots burst out of the ground toward the player (marked first)
    IEnumerator RootLines(int lines)
    {
        busy = true;
        enemy.ForcedVelocity = Vector2.zero;
        Vector2 origin = Pos;
        Vector2 baseDir = (PlayerPos - origin).normalized;
        const int segments = 9;
        const float step = 0.85f;
        var tint = new Color(0.62f, 0.48f, 0.3f);
        for (int l = 0; l < lines; l++)
        {
            Vector2 dir = Quaternion.Euler(0f, 0f, (l - (lines - 1) / 2f) * 22f) * baseDir;
            for (int s = 1; s <= segments; s++) Run.SpawnVfx(origin + dir * (s * step), 0.45f, new Color(1f, 0.3f, 0.2f, 0.7f), 0.7f);
        }
        yield return new WaitForSeconds(0.7f);
        for (int s = 1; s <= segments; s++)
        {
            for (int l = 0; l < lines; l++)
            {
                Vector2 dir = Quaternion.Euler(0f, 0f, (l - (lines - 1) / 2f) * 22f) * baseDir;
                Vector2 p = origin + dir * (s * step);
                Run.Fx(Run.Art.iceSpikes, p + Vector2.up * 0.2f, tint, 0.7f, 11).Scale(0.2f, 0.9f).FadeFrom(0.6f);
                if ((PlayerPos - p).sqrMagnitude < 0.5f) P.Stats.TakeDamage(12f * dmgMul);
            }
            yield return new WaitForSeconds(0.06f);
        }
        enemy.ForcedVelocity = null;
        busy = false;
    }

    // Gray Fang: crouch, then pounce onto the spot where the player was, with a shockwave
    IEnumerator Leap()
    {
        busy = true;
        enemy.ForcedVelocity = Vector2.zero;
        Vector2 target = PlayerPos;
        Run.SpawnVfx(target, 1.8f, new Color(1f, 0.3f, 0.2f, 0.8f), 0.6f);     // landing marker
        yield return new WaitForSeconds(0.55f);
        Vector2 from = Pos;
        float t = 0f;
        const float dur = 0.4f;
        while (t < dur)
        {
            t += Time.deltaTime;
            enemy.ForcedVelocity = (target - from) / dur;
            yield return null;
        }
        enemy.ForcedVelocity = Vector2.zero;
        Run.SpawnVfx(Pos, 2f, new Color(0.85f, 0.85f, 0.8f), 0.4f);
        CameraFollow.Shake(0.15f, 0.2f);
        if ((PlayerPos - Pos).sqrMagnitude < 1.8f * 1.8f) P.Stats.TakeDamage(14f * dmgMul);
        yield return new WaitForSeconds(0.3f);
        enemy.ForcedVelocity = null;
        busy = false;
    }

    // Mycelium Colossus: big spore fields around the player
    void SporeFields(int n)
    {
        for (int i = 0; i < n; i++)
        {
            Vector2 p = PlayerPos + (i == 0 ? Vector2.zero : Random.insideUnitCircle.normalized * Random.Range(2.5f, 4f));
            StartCoroutine(Glob(p, 1.9f));
        }
    }

    // Fallen Champion: a fan of sword waves aimed at the player
    IEnumerator SwordWave(int n)
    {
        busy = true;
        enemy.ForcedVelocity = Vector2.zero;
        Run.SpawnVfx(Pos + Vector2.up, 1.6f, new Color(0.8f, 0.85f, 1f), 0.45f);
        yield return new WaitForSeconds(0.45f);
        Vector2 dir = (PlayerPos - Pos).normalized;
        for (int i = 0; i < n; i++)
        {
            Vector2 d = Quaternion.Euler(0f, 0f, (i - (n - 1) / 2f) * 14f) * dir;
            BossBullet.Spawn(Run.Art.bossBullet, Pos + Vector2.up * 1.2f + d * 0.8f, d * 6f, 11f * dmgMul, 4f, 0.75f);
        }
        AudioManager.Play("hurt", 0.4f, 0.8f, 0.1f);
        yield return new WaitForSeconds(0.2f);
        enemy.ForcedVelocity = null;
        busy = false;
    }

    // Plague Alchemist: flasks rain down on marked spots and burst into poison
    IEnumerator FlaskRain(int n)
    {
        for (int i = 0; i < n; i++)
        {
            Vector2 p = PlayerPos + (i == 0 ? Vector2.zero : Random.insideUnitCircle * 4f);
            StartCoroutine(Flask(p));
            yield return new WaitForSeconds(0.12f);
        }
    }

    IEnumerator Flask(Vector2 p)
    {
        Run.SpawnVfx(p, 1.1f, new Color(0.6f, 1f, 0.3f, 0.7f), 0.9f);
        Run.Fx(Run.Art.bossBullet, p + new Vector2(0.5f, 7f), new Color(0.6f, 1f, 0.5f), 0.9f, 19).Move(p).Scale(0.5f, 0.6f).Spin(400f).FadeFrom(1f);
        yield return new WaitForSeconds(0.9f);
        if ((PlayerPos - p).sqrMagnitude < 1.1f * 1.1f) P.Stats.TakeDamage(10f * dmgMul);
        Hazard.Spawn(Run.Art.poison, p, Color.white, 1.1f, 3.5f, 5f * dmgMul);
    }

    // Hollow Emperor: a rotating double spiral of bullets
    IEnumerator Spiral(float duration)
    {
        busy = true;
        enemy.ForcedVelocity = Vector2.zero;
        float t = 0f, ang = Random.value * 360f, fire = 0f;
        while (t < duration)
        {
            t += Time.deltaTime; fire -= Time.deltaTime;
            if (fire <= 0f)
            {
                fire = 0.1f;
                for (int arm = 0; arm < 2; arm++)
                {
                    float a = (ang + arm * 180f) * Mathf.Deg2Rad;
                    Vector2 d = new(Mathf.Cos(a), Mathf.Sin(a));
                    BossBullet.Spawn(Run.Art.bossBullet, Pos + Vector2.up * 1.2f + d * 0.6f, d * 3.6f, 9f * dmgMul, 6f);
                }
                ang += 17f;
            }
            yield return null;
        }
        enemy.ForcedVelocity = null;
        busy = false;
    }
}
