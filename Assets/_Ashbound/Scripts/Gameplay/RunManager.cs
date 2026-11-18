using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>Owns one run: time, XP and levels, kills, gold, weapons, passives, upgrades, drops, stages, win/lose and unlocks.</summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("Scene")]
    [SerializeField] PlayerController player;
    [SerializeField] EnemySpawner spawner;
    [SerializeField] HUD hud;
    [SerializeField] LevelUpScreen levelUp;
    [SerializeField] GameOverScreen gameOver;
    [SerializeField] SpriteRenderer ground;

    [Header("Prefabs")]
    [SerializeField] Projectile projectilePrefab;
    [SerializeField] XPGem gemPrefab;
    [SerializeField] Vfx vfxPrefab;
    [SerializeField] SpriteRenderer orbPrefab;
    [SerializeField] Pickup pickupPrefab;
    [SerializeField] DamageNumber damageNumberPrefab;
    [SerializeField] DeathPop deathPopPrefab;

    [Header("Pickup art: meat, medkit, magnet, gold bag, chest")]
    [SerializeField] Sprite[] pickupSprites = new Sprite[5];

    [System.Serializable]
    public class FxArt
    {
        public Sprite lightning, blackHole, fallingStar, iceSpikes, tornado, ghostDagger, frostBurst, starBurst;
        public Sprite wave, waveRing, crater, fireBurst, goldenArrow, firePatch, poison, bossBullet, arenaRing;
    }
    [Header("Effect art (skills, bosses)")]
    [SerializeField] FxArt fxArt = new();
    public FxArt Art => fxArt;
    [SerializeField] float arenaRadius = 6.5f;
    [Header("Particle effects (Ashbound/Blend shader)")]
    [SerializeField] Material additiveSprite, glowParticles, smokeParticles, debrisParticles;
    [SerializeField] Sprite glowSprite;
    [Tooltip("Plain square, used for the hero mechanic bar")] [SerializeField] Sprite barSprite;

    [Header("Content")]
    [SerializeField] WeaponDef fallbackPathWeapon;
    [SerializeField] List<WeaponDef> weaponPool = new();
    [SerializeField] List<PassiveDef> passivePool = new();
    [SerializeField] List<ClassData> classes = new();
    [SerializeField] List<StageData> stages = new();

    [Header("Rules")]
    [SerializeField] float runLength = 900f;
    [SerializeField] int maxWeapons = 5;
    [SerializeField] int maxPassives = 5;

    public float RunTime { get; private set; }
    public bool IsOver { get; private set; }

    int level = 1, xp, xpNeeded, kills, gold, pendingLevelUps, bossesAlive, activeNumbers, bossesKilled;
    float gearGold;
    float lastBossWarning = -1f;
    Weapon pathWeapon;
    readonly List<Weapon> weapons = new();
    readonly List<PassiveDef> passives = new();
    readonly List<int> passiveLevels = new();
    readonly HashSet<string> banished = new();

    ObjectPool<Projectile> projectiles;
    ObjectPool<XPGem> gems;
    ObjectPool<Vfx> vfx;
    ObjectPool<DamageNumber> numbers;
    ObjectPool<DeathPop> pops;
    Transform poolRoot;

    void Awake()
    {
        Instance = this;
        if (SaveSystem.Data.runsPlayed == 0) GameSpeed.Fast = false;
        GameSpeed.Resume();
        poolRoot = new GameObject("Pooled").transform;
        FxParticles.Init(poolRoot, additiveSprite, glowParticles, smokeParticles, debrisParticles, glowSprite);
        projectiles = MakePool(projectilePrefab, 64, 2000);
        gems = MakePool(gemPrefab, 128, 2000);
        vfx = MakePool(vfxPrefab, 64, 1000);
        numbers = MakePool(damageNumberPrefab, 64, 300);
        pops = MakePool(deathPopPrefab, 32, 300);
        fx = new ObjectPool<FxSprite>(() =>
            {
                var go = new GameObject("Fx");
                go.transform.SetParent(poolRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = SpriteMaterial;
                var f = go.AddComponent<FxSprite>();
                f.sr = sr;
                return f;
            },
            x => x.gameObject.SetActive(true), x => x.gameObject.SetActive(false), x => { if (x) Destroy(x.gameObject); },
            false, 64, 1500);
        PlayerController.ArenaCenter = null;
    }

    ObjectPool<FxSprite> fx;
    public Material SpriteMaterial => orbPrefab.sharedMaterial;
    public Sprite FallbackSprite => orbPrefab.sprite;

    /// <summary>One-shot sprite effect (pooled). Chain .Scale/.Move/.Rotate/.Spin/.FadeFrom on the result.</summary>
    public FxSprite Fx(Sprite s, Vector2 pos, Color c, float seconds, int order = 12) =>
        fx.Get().Setup(s != null ? s : FallbackSprite, pos, c, seconds, f => fx.Release(f), order);

    // ---------------- loadout info for the pause screen
    public IReadOnlyList<Weapon> Weapons => weapons;
    public IReadOnlyList<PassiveDef> Passives => passives;
    public int PassiveLevel(PassiveDef p) { int i = passives.IndexOf(p); return i < 0 ? 0 : passiveLevels[i]; }
    public int Level => level;

    // ---------------- boss arena + health bar
    readonly List<(Enemy e, string name)> liveBosses = new();
    SpriteRenderer arena;

    void UpdateBosses()
    {
        liveBosses.RemoveAll(b => b.e == null || !b.e.isActiveAndEnabled || b.e.HealthFraction <= 0f || !b.e.IsBoss);
        if (liveBosses.Count > 0)
        {
            hud.ShowBossBar(liveBosses[0].name, liveBosses[0].e.HealthFraction);
            if (arena != null) arena.transform.Rotate(0f, 0f, 6f * Time.deltaTime);
        }
        else
        {
            hud.HideBossBar();
            if (arena != null) { Destroy(arena.gameObject); arena = null; PlayerController.ArenaCenter = null; }
        }
    }

    ObjectPool<T> MakePool<T>(T prefab, int capacity, int max) where T : Component =>
        new(() => Instantiate(prefab, poolRoot),
            x => x.gameObject.SetActive(true),
            x => x.gameObject.SetActive(false),
            x => { if (x) Destroy(x.gameObject); },
            false, capacity, max);

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        var stats = player.Stats;
        // permanent upgrades from the main menu
        stats.AddMaxHealth(MetaUpgrades.Bonus("health"));
        stats.damageMul += MetaUpgrades.Bonus("damage");
        stats.moveMul += MetaUpgrades.Bonus("speed");
        stats.pickupMul += MetaUpgrades.Bonus("pickup");
        // the hero's equipment
        if (RunSelection.Class != null)
        {
            gearGold = Gear.ApplyTo(stats, RunSelection.Class.className);
            // the hero's own mechanic (Mana, Rage, Focus, Shadowstep, Fortify)
            player.gameObject.AddComponent<HeroMechanic>().Setup(RunSelection.Class.className, Gear.MechanicBoost(RunSelection.Class.className), barSprite);
            ShowTip("mechanic_" + RunSelection.Class.className, MechanicTip(RunSelection.Class.className));
        }

        float lastHealth = stats.Health;
        stats.OnHealthChanged += (h, m) =>
        {
            hud.SetHealth(Mathf.CeilToInt(h), Mathf.CeilToInt(m));
            if (h < lastHealth - 0.01f) hud.Hurt();
            hud.SetLowHealth(h > 0f && h / m < 0.3f);
            lastHealth = h;
        };
        stats.OnDied += () => StartCoroutine(DeathMoment());
        hud.SetHealth(Mathf.CeilToInt(stats.Health), Mathf.CeilToInt(stats.MaxHealth));

        xpNeeded = XpFor(level);
        hud.SetXP(0, xpNeeded, level);
        hud.SetKills(0);
        hud.SetGold(0);

        var stage = RunSelection.Stage != null ? RunSelection.Stage : (stages.Count > 0 ? stages[0] : null);
        RunSelection.Stage = stage;
        spawner.SetStage(stage);
        if (ground != null && stage != null)
        {
            if (stage.groundTile != null) { ground.sprite = stage.groundTile; ground.color = Color.white; }
            else ground.color = stage.groundTint;
        }
        ShowTip("move", "Move with WASD or the arrow keys. Your weapons attack on their own!");

        var cls = RunSelection.Class;
        var path = RunSelection.Path;
        var def = path != null && path.weapon != null ? path.weapon
                : cls != null && cls.defaultPathWeapon != null ? cls.defaultPathWeapon
                : fallbackPathWeapon;
        pathWeapon = AddWeapon(def, true);

        levelUp.SetCharges(2 + Mathf.RoundToInt(MetaUpgrades.Bonus("rerolls")), 1);
        levelUp.OnPicked += option =>
        {
            option.apply?.Invoke();
            RefreshLoadout();
            if (pendingLevelUps > 0) { pendingLevelUps--; OfferLevelUp(); }
        };
        levelUp.OnRerollRequested += () => levelUp.Show(level, BuildOptions());
        levelUp.OnBanished += option =>
        {
            if (!string.IsNullOrEmpty(option.key)) banished.Add(option.key);
            levelUp.Show(level, BuildOptions());
        };

        spawner.Init(this);
        RefreshLoadout();
        AudioManager.PlayMusic(StageMusic);
    }

    static string StageMusic => RunSelection.Stage == null ? "music_stage_ashen" : RunSelection.Stage.order switch { 1 => "music_stage_forest", 2 => "music_stage_capital", _ => "music_stage_ashen" };

    void Update()
    {
        if (IsOver) return;
        RunTime += Time.deltaTime;

        float untilBoss = spawner.NextBossTime - RunTime;
        if (spawner.NextBossTime > 0f && untilBoss > 0f && untilBoss <= 20f)
        {
            hud.ShowBossWarning(spawner.NextBossName, Mathf.CeilToInt(untilBoss));
            if (lastBossWarning != spawner.NextBossTime) { lastBossWarning = spawner.NextBossTime; AudioManager.Play("boss", 0.8f); }
        }
        else hud.HideBossWarning();

        UpdateBosses();
        if (streakTimer > 0f) { streakTimer -= Time.deltaTime; if (streakTimer <= 0f) { streak = 0; hud.SetStreak(0); } }
        // music: boss theme while a boss is alive
        AudioManager.PlayMusic(BossFight ? "music_boss" : StageMusic);
        if (RunTime > 40f) ShowTip("pause", "Press Esc or the pause button to see your whole build.");
        if (RunTime >= runLength && spawner.AllBossesSpawned && bossesAlive <= 0 && !ending) StartCoroutine(VictoryMoment());
    }

    // ---------------- events from gameplay

    public bool BossFight => liveBosses.Count > 0;

    /// <summary>First-run hint: each tip shows once per save.</summary>
    public void ShowTip(string id, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var seen = SaveSystem.Data.seenTips;
        if (seen.Contains(id)) return;
        seen.Add(id);
        SaveSystem.Save();
        hud.ShowTip(text);
    }

    public void OnBossSpawned(Enemy boss, string bossName, string bossTitle)
    {
        bossesAlive++;
        // Clear the arena: every regular mob dies (drops its XP), only the boss remains
        foreach (var e in new List<Enemy>(Enemy.Active)) if (!e.IsBoss && e != boss) e.Kill();
        liveBosses.Add((boss, bossName));
        hud.ShowBossIntro(bossName, bossTitle);
        ShowTip("boss", "Boss fight! You're locked in the arena until it falls. Regular enemies stop coming.");
        if (arena == null)
        {
            Vector2 c = player.transform.position;
            PlayerController.ArenaCenter = c;
            PlayerController.ArenaRadius = arenaRadius;
            arena = CreateOrb(null, Color.white, fxArt.arenaRing);
            arena.name = "Boss Arena";
            arena.transform.position = c;
            arena.sortingOrder = -6;
            float w = arena.sprite != null ? arena.sprite.bounds.size.x : 1f;
            arena.transform.localScale = Vector3.one * (arenaRadius * 2f / w);
            arena.color = fxArt.arenaRing == null ? new Color(1f, 0.5f, 0.2f, 0.25f) : new Color(1f, 1f, 1f, 0.42f);
            boss.Teleport(c + Random.insideUnitCircle.normalized * (arenaRadius * 0.6f));
        }
        CameraFollow.Shake(0.35f, 0.5f);
        AudioManager.Play("boss", 1f);
    }

    static string MechanicTip(string cls) => HeroMechanic.KindFor(cls) switch
    {
        HeroMechanic.Kind.Mana => "Mana (purple bar): your damage keeps growing while you avoid getting hit. Getting hit empties it.",
        HeroMechanic.Kind.Rage => "Rage (red bar): the lower your health, the more damage you deal and the faster you attack.",
        HeroMechanic.Kind.Focus => "Focus (gold bar): stand still to charge it; full Focus means +50% damage. Moving drains it.",
        HeroMechanic.Kind.Shadowstep => "Shadowstep: press SPACE to dash. You turn invisible for a moment and your next hits always crit.",
        HeroMechanic.Kind.Fortify => "Fortify (blue bar): you take 10% less damage, and damage you take charges a shockwave that releases on its own.",
        _ => "",
    };

    /// <summary>Golden elite: glow, a tip the first time.</summary>
    public void OnEliteSpawned(Enemy e)
    {
        AudioManager.Play("elite", 0.7f);
        EliteAura.Attach(e, glowSprite, FxParticles.AdditiveSprite);
        ShowTip("elite", "A golden elite appeared! Much tougher than normal Hollow, but it drops a big XP gem and gold.");
    }

    public void OnEnemyKilled(Enemy e, int xpValue)
    {
        Vector2 position = e.transform.position;
        kills++;
        CountStreak();
        hud.SetKills(kills);
        AudioManager.Play("death", 0.35f, 0.9f + Random.value * 0.25f, 0.05f);
        if (e.Body != null)
        {
            float s = e.transform.localScale.x;
            pops.Get().Play(e.Body.sprite, e.Body.color, e.Body.transform.position, new Vector3(e.FacingSign * s, s, 1f), p => pops.Release(p));
        }
        SpawnVfx(position, 0.3f + e.Radius, Palette.Ember, 0.25f);

        if (Random.value < 0.1f) { gold++; hud.SetGold(gold); }
        SpawnGem(position, xpValue);
        if (e.IsElite)
        {
            gold += 8; hud.SetGold(gold);
            AudioManager.Play("coin", 0.9f);
            FxParticles.Sparks(position, new Color(1f, 0.85f, 0.35f), 24, 6f, 0.22f, 0.5f);
            FxParticles.Glow(position, new Color(1f, 0.8f, 0.3f), 2.5f, 0.35f);
            CameraFollow.Shake(0.1f, 0.15f);
            HitStop(0.06f);
        }

        if (e.IsBoss)
        {
            bossesAlive--; bossesKilled++;
            gold += 50; hud.SetGold(gold);
            CameraFollow.Shake(0.4f, 0.5f);
            HitStop(0.22f);
            FxParticles.Glow(position, new Color(1f, 0.75f, 0.35f), 7f, 0.6f, -9);
            FxParticles.Sparks(position, new Color(1f, 0.8f, 0.4f), 60, 9f, 0.25f, 0.7f);
            FxParticles.Debris(position, new Color(0.35f, 0.32f, 0.3f), 30, 7f, 0.18f, 0.7f);
            SpawnPickup(PickupKind.Chest, position + Vector2.left * 0.8f);
            SpawnPickup(PickupKind.Meat, position + Vector2.right * 0.8f);
            return;
        }

        float r = Random.value;
        // Random drops are rare (~0.26% per kill) and at least 15s apart, so healing stays precious
        if (RunTime < nextDropTime) return;
        PickupKind? drop = r < 0.0012f ? PickupKind.Meat
                         : r < 0.0020f ? PickupKind.GoldBag
                         : r < 0.0024f ? PickupKind.Magnet
                         : r < 0.0026f ? PickupKind.Medkit
                         : null;
        if (drop.HasValue) { SpawnPickup(drop.Value, position); nextDropTime = RunTime + 15f; }
    }

    void SpawnGem(Vector2 position, int value)
    {
        if (XPGem.Active.Count > 350) { AddXP(value); return; }
        gems.Get().Init(this, gems, value, position);
    }

    float nextDropTime = 20f;

    void SpawnPickup(PickupKind kind, Vector2 position)
    {
        if (pickupPrefab == null) return;
        ShowTip("pickup", "Arrows at the screen edge point to drops: meat heals, medkit heals fully, magnet pulls in all XP.");
        var p = Instantiate(pickupPrefab, position, Quaternion.identity);
        p.Init(kind, pickupSprites != null && (int)kind < pickupSprites.Length ? pickupSprites[(int)kind] : null, position);
        if (kind == PickupKind.Chest) p.transform.localScale = Vector3.one * 1.9f;
        if (kind == PickupKind.Magnet)
        {
            p.transform.localScale = Vector3.one * 1.8f;   // easy to spot
            if (glowSprite != null && FxParticles.AdditiveSprite != null)
            {
                var g = new GameObject("Glow");
                g.transform.SetParent(p.transform, false);
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = glowSprite; sr.sharedMaterial = FxParticles.AdditiveSprite;
                sr.color = new Color(0.35f, 0.7f, 1f, 0.55f); sr.sortingOrder = 4;
                g.transform.localScale = Vector3.one * (1.3f / Mathf.Max(0.01f, glowSprite.bounds.size.x));
            }
        }
    }

    public void CollectPickup(Pickup p)
    {
        var stats = player.Stats;
        switch (p.Kind)
        {
            case PickupKind.Meat: stats.Heal(20f); AudioManager.Play("heal", 0.8f); break;
            case PickupKind.Medkit: stats.Heal(stats.MaxHealth); AudioManager.Play("heal", 1f, 1.15f); break;
            case PickupKind.Magnet:
                foreach (var g in XPGem.Active) g.Magnetize();
                AudioManager.Play("magnet", 0.9f);
                break;
            case PickupKind.GoldBag: gold += 15; hud.SetGold(gold); AudioManager.Play("coin", 0.9f); break;
            case PickupKind.Chest:
                AudioManager.Play("coin", 1f);
                OpenBossChest();
                break;
        }
        SpawnVfx(p.transform.position, 0.8f, Palette.Gold, 0.3f);
    }

#if UNITY_EDITOR
    // Editor-only test key: F2 grants one level-up.
    void LateUpdate()
    {
        var k = UnityEngine.InputSystem.Keyboard.current;
        if (k != null && k.f2Key.wasPressedThisFrame && !IsOver && !levelUp.IsOpen) AddXP(xpNeeded - xp);
        if (k != null && k.f3Key.wasPressedThisFrame && !IsOver)
            {
                foreach (var w in weapons) while (w.CanLevel && !w.IsPath) w.LevelUp();
                foreach (var p in new List<PassiveDef>(passives)) while (PassiveLevel(p) < 5) ApplyPassive(p);
                RefreshLoadout();
            }
        if (k != null && k.f4Key.wasPressedThisFrame && !IsOver) player.Stats.TakeDamage(99999f);
        if (k != null && k.f5Key.wasPressedThisFrame && !IsOver && spawner.NextBossTime > RunTime) RunTime = spawner.NextBossTime - 1f;
        if (k != null && k.gKey.wasPressedThisFrame) { player.Stats.godMode = !player.Stats.godMode; Debug.Log("[Ashbound] God mode " + player.Stats.godMode); }
        if (k != null && k.bKey.wasPressedThisFrame && !IsOver) Debug.Log("[Ashbound] Boss set now starts with " + spawner.DebugNextBossSet());
        if (k != null && k.cKey.wasPressedThisFrame && !IsOver) SpawnPickup(PickupKind.Chest, (Vector2)player.transform.position + Vector2.right * 1.5f);
        // K: try the next custom Path skill (replaces the last one tried). L: level it up (Lv 6 = awakened).
        if (k != null && k.kKey.wasPressedThisFrame && !IsOver)
        {
            var all = new List<WeaponDef>();
            foreach (var c in classes) if (c != null) foreach (var pd in c.paths) if (pd.weapon != null && pd.weapon.style >= WeaponStyle.StoneSpikes) all.Add(pd.weapon);
            if (all.Count > 0)
            {
                if (testWeapon != null) { weapons.Remove(testWeapon); Destroy(testWeapon.gameObject); }
                testIndex = (testIndex + 1) % all.Count;
                testWeapon = AddWeapon(all[testIndex], true);
                RefreshLoadout();
                Debug.Log($"[Ashbound] Testing path skill {all[testIndex].weaponName}");
            }
        }
        if (k != null && k.lKey.wasPressedThisFrame && testWeapon != null) { testWeapon.LevelUp(); RefreshLoadout(); }
    }
    Weapon testWeapon; int testIndex = -1;
#endif

    public void AddXP(int amount)
    {
        if (IsOver) return;
        xp += amount;
        while (xp >= xpNeeded)
        {
            xp -= xpNeeded;
            level++;
            xpNeeded = XpFor(level);
            pendingLevelUps++;
            LevelBurst();
        }
        hud.SetXP(xp, xpNeeded, level);
        if (pendingLevelUps > 0 && !levelUp.IsOpen) { pendingLevelUps--; OfferLevelUp(); }
    }

    // ---------------- boss chest: 1-3 random rewards; the Fortune upgrade adds luck

    void OpenBossChest()
    {
        int luck = Mathf.RoundToInt(MetaUpgrades.Bonus("rerolls"));   // Fortune level 0-3
        int rolls = 1;
        if (Random.value < 0.45f + 0.15f * luck) rolls++;
        if (Random.value < 0.10f + 0.12f * luck) rolls++;

        int levelUps = 0, chestGold = 0, chestGems = 0;
        bool healed = false, magnet = false;
        var lines = new List<string>();
        for (int i = 0; i < rolls; i++)
        {
            float r = Random.value * 100f;
            if (r < 32f) levelUps++;
            else if (r < 62f) chestGold += Mathf.RoundToInt(Random.Range(20, 41) * (1f + 0.25f * luck));
            else if (r < 77f) chestGems += Random.Range(3, 7) + luck;
            else if (r < 90f && !healed) healed = true;
            else if (!magnet) magnet = true;
            else levelUps++;
        }
        if (levelUps == 0 && rolls == 1 && Random.value < 0.5f) levelUps = 1;   // a single roll still often levels you

        // rare gear piece, better with Fortune
        GearItem gearItem = null;
        if (Random.value < 0.15f + 0.03f * luck)
        {
            float g = Random.value;
            int rarity = g < 0.07f + 0.03f * luck ? 2 : g < 0.40f + 0.05f * luck ? 1 : 0;
            var unlocked = new List<string>();
            foreach (var c in classes) if (c != null && SaveSystem.IsClassUnlocked(c)) unlocked.Add(c.className);
            gearItem = Gear.Grant(rarity, unlocked);
            SaveSystem.Save();
        }

        if (chestGold > 0) { gold += chestGold; hud.SetGold(gold); lines.Add($"<color=#F2B33D>+{chestGold} gold</color>"); }
        if (chestGems > 0) { SaveSystem.Data.gems += chestGems; SaveSystem.Save(); lines.Add($"<color=#A970FF>+{chestGems} gems</color>"); }
        if (levelUps > 0) lines.Add(levelUps == 1 ? "+1 level up" : $"+{levelUps} level ups");
        if (healed) { player.Stats.Heal(player.Stats.MaxHealth * 0.4f); lines.Add("<color=#4CC38A>Healed 40%</color>"); }
        if (magnet) { foreach (var gm in XPGem.Active) gm.Magnetize(); lines.Add("<color=#3DA5F4>All XP pulled in</color>"); }
        if (gearItem != null)
        {
            var d = Gear.Get(gearItem.id);
            lines.Add($"<color=#{ColorUtility.ToHtmlStringRGB(Gear.RarityColors[gearItem.rarity])}>{Gear.RarityNames[gearItem.rarity]} {(d != null ? d.name : gearItem.id)}</color>");
        }
        if (lines.Count == 0) { gold += 20; hud.SetGold(gold); lines.Add("<color=#F2B33D>+20 gold</color>"); }

        int ups = levelUps;
        hud.ShowChest(string.Join("\n", lines), () => { for (int i = 0; i < ups; i++) QueueLevelUp(); });
    }

    void QueueLevelUp()
    {
        pendingLevelUps++;
        if (!levelUp.IsOpen && !IsOver) { pendingLevelUps--; OfferLevelUp(); }
    }

    static int XpFor(int lv) => 5 + (lv - 1) * 5 + Mathf.Max(0, lv - 20) * 10;

    // ---------------- end of run, rewards and unlocks

    bool ending;

    // slow-motion fall before the game-over screen
    System.Collections.IEnumerator DeathMoment()
    {
        if (ending || IsOver) yield break;
        ending = true;
        Vector2 p = player.transform.position;
        AudioManager.Play("death", 1f, 0.6f);
        CameraFollow.Shake(0.35f, 0.4f);
        FxParticles.Glow(p, new Color(1f, 0.2f, 0.15f, 0.9f), 4f, 1.2f, 20);
        FxParticles.Debris(p, new Color(0.6f, 0.05f, 0.08f), 30, 5f, 0.16f, 0.9f);
        FxParticles.Smoke(p, new Color(0.15f, 0.12f, 0.14f, 0.7f), 12, 0.5f, 1.4f, 1.2f, 2f);
        Time.timeScale = 0.2f;
        yield return new WaitForSecondsRealtime(1.3f);
        EndRun(false);
    }

    // a short celebration before the victory screen
    System.Collections.IEnumerator VictoryMoment()
    {
        ending = true;
        Vector2 p = player.transform.position;
        AudioManager.Play("victory", 0.9f);
        foreach (var e in new List<Enemy>(Enemy.Active)) if (!e.IsBoss) e.Kill();
        for (int i = 0; i < 6; i++)
        {
            FxParticles.Sparks(p + Random.insideUnitCircle * 3f, Color.HSVToRGB(Random.value, 0.5f, 1f), 24, 7f, 0.22f, 0.8f);
            FxParticles.Glow(p, new Color(1f, 0.85f, 0.4f, 0.6f), 6f, 0.5f, -9);
            yield return new WaitForSecondsRealtime(0.2f);
        }
        EndRun(true);
    }

    /// <summary>Damage per skill, best streak and the hero's gear, for the end screen.</summary>
    string Breakdown()
    {
        var all = new List<Weapon>(weapons);
        all.Sort((a, b) => b.TotalDamage.CompareTo(a.TotalDamage));
        float total = 0f; foreach (var w in all) total += w.TotalDamage;
        var sb = new System.Text.StringBuilder();
        sb.Append("<b>DAMAGE</b>\n");
        foreach (var w in all)
        {
            if (w.TotalDamage < 1f) continue;
            float secs = Mathf.Max(1f, RunTime - w.AddedAt);
            string col = ColorUtility.ToHtmlStringRGB(Color.Lerp(w.Def.color, Color.white, 0.35f));
            sb.Append($"<color=#{col}>{w.DisplayName}</color>  {w.TotalDamage:N0}  <color=#B9B1C2>{(total > 0 ? w.TotalDamage / total * 100f : 0):0}%  ·  {w.TotalDamage / secs:N0}/s</color>\n");
        }
        sb.Append($"\nBest kill streak: <b>{bestStreak:N0}</b>");
        var cls = RunSelection.Class;
        if (cls != null)
        {
            var worn = new List<string>();
            foreach (var g in SaveSystem.Data.gear) if (g.equippedBy == cls.className) { var d = Gear.Get(g.id); if (d != null) worn.Add($"<color=#{ColorUtility.ToHtmlStringRGB(Gear.RarityColors[g.rarity])}>{d.name}</color>"); }
            if (worn.Count > 0) sb.Append("\nGear: " + string.Join(", ", worn));
        }
        return sb.ToString();
    }

    void EndRun(bool won)
    {
        if (IsOver) return;
        IsOver = true;
        hud.HideBossWarning();

        var save = SaveSystem.Data;
        var stage = RunSelection.Stage;
        var cls = RunSelection.Class;
        save.runsPlayed++;
        save.totalKills += kills;
        int earned = Mathf.RoundToInt(gold * (stage != null ? stage.goldMultiplier : 1f) * (1f + MetaUpgrades.Bonus("greed") + gearGold));
        save.gold += earned;
        int gemsEarned = bossesKilled * 3 + (RunTime >= 600f ? 5 : 0) + (won ? 20 : 0);
        if (won && stage != null && !save.stagesWon.Contains(stage.stageName)) gemsEarned += 30;
        save.gems += gemsEarned;
        var notes = new List<string> { $"+{earned} gold" };
        if (gemsEarned > 0) notes.Add($"+{gemsEarned} gems");

        if (RunTime >= 600f)
        {
            if (cls != null)
                foreach (var p in cls.paths)
                    if (!SaveSystem.IsPathUnlocked(cls, p))
                    {
                        save.unlockedPaths.Add(SaveSystem.PathKey(cls, p));
                        notes.Add($"New Path: {cls.className} · {p.pathName}");
                        break;
                    }
            var next = stage != null ? stages.Find(s => s.order == stage.order + 1) : null;
            if (next != null && !SaveSystem.IsStageUnlocked(next))
            {
                save.unlockedStages.Add(next.stageName);
                notes.Add($"New stage: {next.stageName}");
            }
        }

        if (won)
        {
            if (stage != null && !save.stagesWon.Contains(stage.stageName))
            {
                save.stagesWon.Add(stage.stageName);
                notes.Add("Crown shard recovered!");
            }
            UnlockClass("Assassin", save.stagesWon.Count >= 1, notes);
            UnlockClass("Tank (Knight)", save.stagesWon.Count >= 2, notes);
        }
        if (save.runsPlayed == 1) notes.Add("x2 speed unlocked!");

        SaveSystem.Save();
        AudioManager.Play(won ? "victory" : "gameover", 0.9f);
        gameOver.Show(won, RunTime, level, kills, gold, string.Join("\n", notes), Breakdown());
    }

    void UnlockClass(string className, bool condition, List<string> notes)
    {
        if (!condition) return;
        var c = classes.Find(x => x != null && x.className == className);
        if (c == null || SaveSystem.IsClassUnlocked(c)) return;
        SaveSystem.Data.unlockedClasses.Add(c.className);
        if (c.paths.Count > 0) SaveSystem.Data.unlockedPaths.Add(SaveSystem.PathKey(c, c.paths[0]));
        notes.Add($"New hero: {c.className}!");
    }

    // ---------------- upgrades

    void OfferLevelUp()
    {
        if (IsOver) return;
        ShowTip("levelup", "Pick one card. Reroll shows new cards, Banish removes a card for the rest of the run.");
        foreach (var w in weapons)
            if (!w.IsPath && !w.IsEvolved && w.Level >= 5 && w.Def.evolvePassive != null)
                ShowTip("evolve", $"{w.Def.weaponName} is maxed! Get {w.Def.evolvePassive.passiveName} to Lv 5 to evolve it.");
        var options = BuildOptions();
        if (options.Count == 0) { player.Stats.Heal(25f); return; }
        levelUp.Show(level, options);
    }

    List<WeaponDef> ClassWeaponPool()
    {
        var cls = RunSelection.Class;
        if (cls == null) return weaponPool;
        // Path skills are individual: only your own Path's skill (given at the start), never another Path's.
        return new List<WeaponDef>(cls.weapons);
    }

    List<UpgradeOption> BuildOptions()
    {
        var candidates = new List<UpgradeOption>();
        foreach (var w in weapons)
        {
            if (w.CanEvolve(PassiveLevel)) candidates.Add(EvolutionOption(w));
            else if (w.CanLevel) candidates.Add(WeaponOption(w));
        }

        int extraWeapons = 0;
        foreach (var w in weapons) if (!w.IsPath) extraWeapons++;
        if (extraWeapons < maxWeapons)
            foreach (var d in ClassWeaponPool())
                if (d != null && !Owns(d)) candidates.Add(NewWeaponOption(d));

        for (int i = 0; i < passives.Count; i++) if (passiveLevels[i] < 5) candidates.Add(PassiveOption(passives[i], passiveLevels[i]));
        if (passives.Count < maxPassives)
            foreach (var p in passivePool)
                if (p != null && !passives.Contains(p)) candidates.Add(PassiveOption(p, 0));

        candidates.RemoveAll(c => banished.Contains(c.key));

        var result = new List<UpgradeOption>();
        var special = candidates.Find(c => c.IsAwakening);
        if (special != null) { result.Add(special); candidates.Remove(special); }
        while (result.Count < 3 && candidates.Count > 0)
        {
            int i = Random.Range(0, candidates.Count);
            result.Add(candidates[i]);
            candidates.RemoveAt(i);
        }
        return result;
    }

    bool Owns(WeaponDef d)
    {
        foreach (var w in weapons) if (w.Def == d || (w.IsEvolved && d.evolvesInto == w.Def)) return true;
        return false;
    }

    static string EvolveHint(WeaponDef d) =>
        d.evolvePassive != null && d.evolvesInto != null ? $"\n<color=#FFD95A>Evolves with {d.evolvePassive.passiveName} Lv 5</color>" : "";

    UpgradeOption WeaponOption(Weapon w)
    {
        int next = w.Level + 1;
        bool awaken = next == 6;
        return new UpgradeOption
        {
            key = "w:" + w.Def.name,
            tag = awaken ? "AWAKENING" : w.IsPath ? "PATH SKILL" : "WEAPON",
            name = awaken && !string.IsNullOrEmpty(w.Def.awakeningName) ? w.Def.awakeningName : w.Def.weaponName,
            levelLine = awaken ? $"{w.Def.weaponName} · Lv 5 » Awakened" : $"Lv {w.Level} » Lv {next}",
            effect = w.NextLevelText + (next == 5 && !w.IsPath ? EvolveHint(w.Def) : ""),
            color = w.Def.color,
            icon = awaken && w.Def.awakenIcon != null ? w.Def.awakenIcon : w.Def.icon,
            fromLevel = w.Level,
            toLevel = next,
            apply = w.LevelUp,
        };
    }

    UpgradeOption EvolutionOption(Weapon w)
    {
        var evolved = w.Def.evolvesInto;
        return new UpgradeOption
        {
            key = "e:" + evolved.name,
            tag = "EVOLUTION",
            name = evolved.weaponName,
            levelLine = $"{w.Def.weaponName} + {w.Def.evolvePassive.passiveName}",
            effect = evolved.levelText != null && evolved.levelText.Length > 0 ? evolved.levelText[0] : "",
            color = evolved.color,
            icon = evolved.icon,
            fromLevel = 5,
            toLevel = 6,
            apply = () =>
            {
                w.Evolve();
                AudioManager.Play("evolve", 1f);
                CameraFollow.Shake(0.25f, 0.35f);
                SpawnVfx(player.transform.position, 3f, Palette.Gold, 0.6f);
            },
        };
    }

    UpgradeOption NewWeaponOption(WeaponDef d) => new()
    {
        key = "w:" + d.name,
        tag = "NEW WEAPON",
        name = d.weaponName,
        levelLine = "Lv 1",
        effect = (d.levelText != null && d.levelText.Length > 0 ? d.levelText[0] : "") + EvolveHint(d),
        color = d.color,
        icon = d.icon,
        fromLevel = 0,
        toLevel = 1,
        apply = () => AddWeapon(d, false),
    };

    UpgradeOption PassiveOption(PassiveDef p, int currentLevel) => new()
    {
        key = "p:" + p.name,
        tag = currentLevel == 0 ? "NEW PASSIVE" : "PASSIVE",
        name = p.passiveName,
        levelLine = currentLevel == 0 ? "Lv 1" : $"Lv {currentLevel} » Lv {currentLevel + 1}",
        effect = p.description + PassiveEvolveHint(p),
        color = p.color,
        icon = p.icon,
        fromLevel = currentLevel,
        toLevel = currentLevel + 1,
        apply = () => ApplyPassive(p),
    };

    string PassiveEvolveHint(PassiveDef p)
    {
        foreach (var w in weapons)
            if (!w.IsEvolved && w.Def.evolvePassive == p && w.Def.evolvesInto != null)
                return $"\n<color=#FFD95A>Lv 5 + {w.Def.weaponName} Lv 5 » {w.Def.evolvesInto.weaponName}</color>";
        return "";
    }

    Weapon AddWeapon(WeaponDef def, bool isPath)
    {
        var go = new GameObject(def.weaponName);
        go.transform.SetParent(player.transform, false);
        var w = go.AddComponent<Weapon>();
        w.Setup(def, player.Stats, isPath);
        weapons.Add(w);
        return w;
    }

    void ApplyPassive(PassiveDef p)
    {
        int i = passives.IndexOf(p);
        if (i < 0) { passives.Add(p); passiveLevels.Add(0); i = passives.Count - 1; }
        passiveLevels[i]++;

        var s = player.Stats;
        switch (p.stat)
        {
            case PassiveStat.Damage: s.damageMul += p.perLevel; break;
            case PassiveStat.Cooldown: s.cooldownMul *= 1f - p.perLevel; break;
            case PassiveStat.MoveSpeed: s.moveMul += p.perLevel; break;
            case PassiveStat.MaxHealth: s.AddMaxHealth(p.perLevel); break;
            case PassiveStat.Area: s.areaMul += p.perLevel; break;
            case PassiveStat.Pickup: s.pickupMul += p.perLevel; break;
        }
        foreach (var w in weapons) w.Refresh();
    }

    void RefreshLoadout()
    {
        if (pathWeapon != null)
            hud.SetPathSkill(pathWeapon.Icon, pathWeapon.Def.color, Mathf.Min(pathWeapon.Level, 5), pathWeapon.Golden, pathWeapon.DisplayName);

        int slot = 0;
        foreach (var w in weapons)
        {
            if (w.IsPath || slot >= 5) continue;
            hud.SetWeapon(slot++, w.Icon, w.Def.color, Mathf.Min(w.Level, 5), w.Golden);
        }
        for (int i = 0; i < passives.Count && i < 5; i++) hud.SetPassive(i, passives[i].icon, passives[i].color, passiveLevels[i]);
    }

    // ---------------- pools for weapons and effects

    public Projectile GetProjectile() => projectiles.Get();
    public void ReleaseProjectile(Projectile p) => projectiles.Release(p);

    public void SpawnVfx(Vector2 position, float radius, Color color, float seconds)
    {
        vfx.Get().Play(position, radius, color, seconds, v => vfx.Release(v));
    }

    public static readonly Color CritColor = new(1f, 0.32f, 0.28f);

    public void ShowDamage(Vector3 position, float amount, bool big, bool crit = false)
    {
        if (crit) AudioManager.Play("crit", 0.35f, 0.9f + Random.value * 0.3f, 0.06f);
        if (damageNumberPrefab == null || activeNumbers > 90 || amount < 1f || !SaveSystem.Data.showDamageNumbers) return;
        activeNumbers++;
        var color = crit ? CritColor : big ? Palette.Gold : amount >= 30f ? new Color(1f, 0.6f, 0.25f) : Color.white;
        float size = crit ? 4.8f : big || amount >= 30f ? 4.2f : 3.2f;
        numbers.Get().Play(position, Mathf.RoundToInt(amount), color, size, n => { activeNumbers--; numbers.Release(n); });
    }

    // ---------------- juice: hit-stop, kill streak, level-up burst

    Coroutine hitStop;
    /// <summary>Freeze the game for a split second so big moments land.</summary>
    public void HitStop(float realSeconds)
    {
        if (IsOver || Time.timeScale <= 0.001f || hitStop != null) return;
        hitStop = StartCoroutine(HitStopRoutine(realSeconds));
    }

    System.Collections.IEnumerator HitStopRoutine(float s)
    {
        const float slow = 0.04f;
        Time.timeScale = slow;
        yield return new WaitForSecondsRealtime(s);
        if (Mathf.Approximately(Time.timeScale, slow)) GameSpeed.Resume();
        hitStop = null;
    }

    int streak, bestStreak;
    float streakTimer;
    static readonly int[] StreakMilestones = { 100, 250, 500, 1000, 2000 };

    void CountStreak()
    {
        streak++; streakTimer = 2.5f;
        if (streak > bestStreak) bestStreak = streak;
        if (streak >= 25) hud.SetStreak(streak);
        foreach (var m in StreakMilestones)
            if (streak == m)
            {
                int bonus = m / 25;
                gold += bonus; hud.SetGold(gold);
                AudioManager.Play("streak", 0.8f);
                Vector2 p = player.transform.position;
                FxParticles.Sparks(p + Vector2.up * 0.5f, new Color(1f, 0.85f, 0.35f), 26, 6f, 0.2f, 0.5f);
                hud.StreakMilestone(m, bonus);
            }
    }

    void LevelBurst()
    {
        Vector2 p = player.transform.position;
        AudioManager.Play("burst", 0.6f);
        FxParticles.Glow(p, new Color(0.4f, 0.8f, 1f, 0.8f), 4f, 0.45f, -9);
        FxParticles.Sparks(p + Vector2.up * 0.4f, new Color(0.5f, 0.85f, 1f), 30, 6f, 0.2f, 0.5f);
        FxParticles.Motes(p, new Color(0.7f, 0.95f, 1f), 12, 0.6f, 0.16f, 0.9f);
        if (Art.waveRing != null)
        {
            float w = Art.waveRing.bounds.size.x;
            Fx(Art.waveRing, p, new Color(0.6f, 0.9f, 1f, 0.8f), 0.45f, -8).Scale(0.4f / w, 5f / w).FadeFrom(0.3f);
        }
        // a gentle push so you get a breath while choosing
        Enemy.CollectInRadius(p, 2.6f, burstBuffer);
        foreach (var e in burstBuffer) if (!e.IsBoss) e.Pull(((Vector2)e.transform.position - p).normalized * 10f);
    }
    static readonly List<Enemy> burstBuffer = new();

    public SpriteRenderer CreateOrb(Transform parent, Color color, Sprite art = null)
    {
        var o = Instantiate(orbPrefab, parent);
        if (art != null) { o.sprite = art; o.color = Color.white; }
        else o.color = color;
        return o;
    }
}
