// Ashbound > Build Everything
// One-click builder: imports UI sprites and fonts, creates class/Path data, UI prefabs,
// and the MainMenu, ClassSelect and Game scenes, fully wired. Safe to run again:
// it rebuilds prefabs and the three scenes from scratch (manual scene edits are overwritten).
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class AshboundBuilder
{
    const string Root = "Assets/_Ashbound";
    const string ArtUI = Root + "/Art/UI";
    const string Fonts = Root + "/Fonts";
    const string PrefabDir = Root + "/Prefabs/UI";
    const string ScenesDir = Root + "/Scenes";
    static readonly string MainMenuPath = ScenesDir + "/MainMenu.unity";
    static readonly string ClassSelectPath = ScenesDir + "/ClassSelect.unity";
    static readonly string GamePath = ScenesDir + "/Game.unity";

    static Sprite sPanel, sFill, sRing, sCircle, sGlow, sSquare;
    static TMP_FontAsset fTitle, fBold, fBlack;
    static Material mTitleGlow;
    static int tmpImportAttempts;
    static Sprite wBlob, wHead, wCap, wDot, wOrb, wBolt, wGem, wRing, wSoft, wShadow, wRock;
    static Material mUnlit;
    static readonly Dictionary<string, Sprite> art = new();
    static Sprite menuBg, groundTile, gemArt;

    // Loads a sprite from Art/InGame (or another Art subfolder). Returns null if the file isn't there.
    static Sprite ArtFile(string relPath, float ppu, bool tiled = false, bool bottomPivot = false)
    {
        string path = $"{Root}/Art/{relPath}";
        if (!System.IO.File.Exists(path)) return null;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu;
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)(bottomPivot ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
        if (tiled) settings.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(settings);
        ti.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = tiled;
        ti.alphaIsTransparency = !tiled;
        ti.maxTextureSize = 2048;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite Icon(string name) => ArtFile($"InGame/Icons/{name}.png", 100);
    static Sprite FxArtFile(string name) => ArtFile($"InGame/Effects/{name}.png", 256);
    static Sprite Proj(string name) => name == null ? null : ArtFile($"InGame/Projectiles/{name}.png", 200);

    static readonly Dictionary<string, (string icon, string proj)> weaponArt = new()
    {
        ["Path_Fireball"] = ("icon_fireball", "proj_fireball"),
        ["Path_TidalWave"] = ("icon_tidal_wave", null),
        ["Path_ChainLightning"] = ("icon_chain_lightning", "proj_lightning"),
        ["Path_BloodAxes"] = ("icon_blood_axes", "proj_blood_axe"),
        ["Path_ThunderHammer"] = ("icon_thunder_hammer", null),
        ["Path_PiercingShot"] = ("icon_piercing_shot", "proj_arrow"),
        ["Path_Multishot"] = ("icon_multishot", "proj_arrow"),
        ["ArcaneOrb"] = ("icon_arcane_orb", "proj_arcane_orb"),
        ["MagicMissile"] = ("icon_magic_missile", "proj_missile"),
        ["FrostNova"] = ("icon_frost_nova", null),
        ["SpinningAxe"] = ("icon_spinning_axe", "proj_steel_axe"),
        ["ThrowingDaggers"] = ("icon_throwing_daggers", "proj_dagger"),
        ["Path_ShadowDaggers"] = ("icon_throwing_daggers", "proj_dagger"),
        ["Path_GuardianShields"] = ("icon_vitality", null),
        ["Evo_BlackHole"] = ("evo_black_hole", "proj_arcane_orb"),
        ["Evo_Starfall"] = ("evo_starfall", "proj_missile"),
        ["Evo_Glacier"] = ("evo_glacier", null),
        ["Evo_CrimsonWhirlwind"] = ("evo_crimson_whirlwind", "proj_blood_axe"),
        ["Evo_PhantomBlades"] = ("evo_phantom_blades", "proj_dagger"),
    };
    static readonly Dictionary<string, WeaponDef> pathWeapons = new();

    static readonly Color Amber = Hex("#F2B33D");

    // ------------------------------------------------------------------ entry point

    [MenuItem("Ashbound/Build Everything", priority = 0)]
    public static void BuildEverything()
    {
        if (!EnsureTMPEssentials()) return;

        bool scenesExist = AssetDatabase.LoadAssetAtPath<SceneAsset>(GamePath) != null;
        if (scenesExist && !EditorUtility.DisplayDialog("Ashbound",
                "Rebuild the UI prefabs and the MainMenu, ClassSelect and Game scenes?\n\nChanges you made by hand in those scenes will be replaced.",
                "Rebuild", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        try
        {
            Progress("Sprites", 0.05f); LoadSprites();
            Progress("Fonts", 0.15f); LoadFonts();
            Progress("World sprites", 0.2f); LoadWorldSprites();
            Progress("Weapons & passives", 0.25f); var content = BuildContent(); contentCache = content;
            Progress("Class & Path data", 0.3f); var classes = BuildData(); classesCache = classes;
            Progress("Stages", 0.35f); stagesCache = BuildStages();
            Progress("Prefabs", 0.45f); var prefabs = BuildPrefabs(); prefabsCache = prefabs;
            var gameplay = BuildGameplayPrefabs();
            Progress("Main menu scene", 0.6f); BuildMainMenu();
            Progress("Class select scene", 0.75f); BuildClassSelect(classes, prefabs);
            Progress("Game scene", 0.9f); BuildGame(prefabs, gameplay, content);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(ClassSelectPath, true),
                new EditorBuildSettingsScene(GamePath, true),
            };
            PlayerSettings.productName = "Ashbound";
            AssetDatabase.SaveAssets();
        }
        finally { EditorUtility.ClearProgressBar(); }

        EditorSceneManager.OpenScene(MainMenuPath);
        Debug.Log("[Ashbound] Build complete: MainMenu, ClassSelect and Game scenes are ready. Press Play.");
        EditorUtility.DisplayDialog("Ashbound", "Done! The MainMenu scene is open.\n\nPress Play, click Play, pick a hero and Path, then Start run.\nMove with WASD or the arrow keys. Weapons fire on their own. Esc pauses.", "Let's go");
    }

    static Content contentCache;
    static List<ClassData> classesCache;
    static List<StageData> stagesCache;

    [MenuItem("Ashbound/Build macOS App", priority = 20)]
    public static void BuildMacApp()
    {
        string outDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Builds"));
        System.IO.Directory.CreateDirectory(outDir);
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { MainMenuPath, ClassSelectPath, GamePath },
            locationPathName = System.IO.Path.Combine(outDir, "Ashbound.app"),
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"[Ashbound] macOS build: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, errors {report.summary.totalErrors} -> {opts.locationPathName}");
    }

    static List<StageData> BuildStages()
    {
        if (!AssetDatabase.IsValidFolder($"{Root}/Data/Stages")) AssetDatabase.CreateFolder($"{Root}/Data", "Stages");
        StageData St(string file, string name, string desc, int order, string ground, string enemy, float hp, float dmg, float spawn, float gold)
        {
            var s = LoadOrCreate<StageData>($"{Root}/Data/Stages/{file}.asset");
            s.stageName = name; s.description = desc; s.order = order; s.groundTint = Hex(ground); s.enemyTint = Hex(enemy);
            s.enemyHealth = hp; s.enemyDamage = dmg; s.spawnRate = spawn; s.goldMultiplier = gold;
            EditorUtility.SetDirty(s);
            return s;
        }
        Sprite En(string f) => ArtFile($"InGame/Enemies/{f}.png", 256, bottomPivot: true);
        Sprite Gr(string f) => ArtFile($"World/{f}.png", 200, tiled: true);
        var list = new List<StageData>
        {
            St("Stage1_AshenFields", "Ashen Fields", "Where the Hollow first rose. A fair fight.", 0, "#FFFFFF", "#FFFFFF", 1f, 1f, 1f, 1f),
            St("Stage2_HollowForest", "Hollow Forest", "Tougher Hollow, more of them. Gold x1.3", 1, "#A8D49A", "#B7E3A4", 1.5f, 1.1f, 1.2f, 1.3f),
            St("Stage3_RuinedCapital", "Ruined Capital", "The Hollow King's streets. Gold x1.6", 2, "#9AA6C8", "#B4BEDD", 2.2f, 1.3f, 1.4f, 1.6f),
        };
        void Art(StageData st, string ground, params string[] enemies)
        {
            var g = Gr(ground);
            st.groundTile = g;
            st.enemySprites = new Sprite[3];
            bool any = false;
            for (int i = 0; i < 3 && i < enemies.Length; i++) { st.enemySprites[i] = En(enemies[i]); any |= st.enemySprites[i] != null; }
            if (g != null) st.groundTint = Color.white;
            if (any) st.enemyTint = Color.white;
            EditorUtility.SetDirty(st);
        }
        list[0].groundTile = null; list[0].enemySprites = new Sprite[3]; EditorUtility.SetDirty(list[0]);
        Art(list[1], "ground_forest", "forest_walker", "forest_wolf", "forest_brute");
        Art(list[2], "ground_capital", "capital_soldier", "capital_ghoul", "capital_golem");
        AssetDatabase.SaveAssets();
        return list;
    }

    static void Progress(string what, float t) => EditorUtility.DisplayProgressBar("Ashbound builder", what, t);

    static bool EnsureTMPEssentials()
    {
        if (Resources.Load<TMP_Settings>("TMP Settings") != null) return true;
        if (tmpImportAttempts++ > 1)
        {
            EditorUtility.DisplayDialog("Ashbound", "TextMeshPro Essentials could not be imported automatically.\nUse Window > TextMeshPro > Import TMP Essential Resources, then run Ashbound > Build Everything again.", "OK");
            return false;
        }
        AssetDatabase.importPackageCompleted += OnTmpImported;
        TMP_PackageResourceImporter.ImportResources(true, false, false);
        return false;
    }

    static void OnTmpImported(string packageName)
    {
        AssetDatabase.importPackageCompleted -= OnTmpImported;
        EditorApplication.delayCall += BuildEverything;
    }

    // ------------------------------------------------------------------ assets

    static void LoadSprites()
    {
        sPanel = ImportSprite("ui_panel.png", 32);
        sRing = ImportSprite("ui_ring.png", 32);
        sFill = ImportSprite("ui_fill.png", 16);
        sCircle = ImportSprite("ui_circle.png", 0);
        sGlow = ImportSprite("ui_glow.png", 0);
        sSquare = ImportSprite("ui_square.png", 0);
    }

    static Sprite ImportSprite(string file, int border)
    {
        string path = $"{ArtUI}/{file}";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) throw new System.Exception($"[Ashbound] Missing sprite file {path}");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spriteBorder = new Vector4(border, border, border, border);
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void LoadFonts()
    {
        fTitle = FontAsset("Cinzel-ExtraBold");
        fBold = FontAsset("Nunito-Bold");
        fBlack = FontAsset("Nunito-Black");

        string glowPath = $"{Fonts}/Cinzel-ExtraBold Glow.mat";
        mTitleGlow = AssetDatabase.LoadAssetAtPath<Material>(glowPath);
        if (mTitleGlow == null)
        {
            mTitleGlow = new Material(Shader.Find("TextMeshPro/Distance Field"));
            AssetDatabase.CreateAsset(mTitleGlow, glowPath);
        }
        mTitleGlow.SetTexture("_MainTex", fTitle.material.GetTexture("_MainTex"));
        mTitleGlow.SetFloat("_TextureWidth", fTitle.atlasWidth);
        mTitleGlow.SetFloat("_TextureHeight", fTitle.atlasHeight);
        mTitleGlow.SetFloat("_GradientScale", fTitle.atlasPadding + 1);
        mTitleGlow.EnableKeyword("GLOW_ON");
        mTitleGlow.SetColor("_GlowColor", new Color(0.94f, 0.54f, 0.24f, 0.55f));
        mTitleGlow.SetFloat("_GlowOuter", 0.35f);
        mTitleGlow.SetFloat("_GlowPower", 0.6f);
        mTitleGlow.EnableKeyword("UNDERLAY_ON");
        mTitleGlow.SetColor("_UnderlayColor", Palette.Outline);
        mTitleGlow.SetFloat("_UnderlayOffsetY", -0.6f);
        mTitleGlow.SetFloat("_UnderlayDilate", 0.4f);
        EditorUtility.SetDirty(mTitleGlow);
    }

    static TMP_FontAsset FontAsset(string ttfName)
    {
        string assetPath = $"{Fonts}/{ttfName} SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>($"{Fonts}/{ttfName}.ttf");
        if (font == null) throw new System.Exception($"[Ashbound] Missing font {ttfName}.ttf in {Fonts}");
        var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        fa.name = ttfName + " SDF";
        AssetDatabase.CreateAsset(fa, assetPath);
        fa.atlasTextures[0].name = ttfName + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = ttfName + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    // ------------------------------------------------------------------ data (from the design spreadsheet)

    struct P { public string n, c, skill, plays, asc; public bool open; }
    static P Pt(string n, string c, string skill, string plays, string asc, bool open = false) => new() { n = n, c = c, skill = skill, plays = plays, asc = asc, open = open };

    static List<ClassData> BuildData()
    {
        var list = new List<ClassData>
        {
            Class("Mage", "#8E6BEF", "Glass cannon · area control", "Mana: spells grow stronger the longer you avoid damage", "Arcane Orb", true,
                Pt("Fire", "#FF6B35", "Fireball", "Max damage, burn stacking", "Phoenix: once per run, revive in a fire nova; fireballs become meteors", true),
                Pt("Water", "#3DA5F4", "Tidal Wave", "Crowd control, knockback", "Leviathan: a giant water serpent circles you and devours enemies", true),
                Pt("Air", "#6FE3D1", "Chain Lightning", "Speed, mobility", "Storm Lord: constant lightning strikes all over the screen", true),
                Pt("Earth", "#B8A04A", "Stone Spikes", "Slow, tanky caster", "Titan: become a stone golem; every step causes a quake"),
                Pt("Dark", "#9B5DE5", "Shadow Bolt", "Chain reactions, life drain", "Lich: killed enemies rise as your skeleton army"),
                Pt("Light", "#FFD95A", "Holy Beam", "Healing, precision", "Seraph: wings plus a rotating ring of holy lasers")),
            Class("Berserker", "#E5484D", "Melee brawler", "Rage: lower HP means more damage and attack speed", "Spinning Axe", true,
                Pt("Bloodlust", "#E5484D", "Rage", "High risk, high reward", "Avatar of Rage: can't die for 10s at 0 HP; kills extend it", true),
                Pt("Thunder", "#6FE3D1", "Thunder Hammer", "Area stuns", "Thunder God: every hit calls lightning; hammer returns like a boomerang", true),
                Pt("Beast", "#E5484D", "Wolf Claws", "Speed melee, frenzy stacks", "Werewolf: transform, huge attack speed, howl that fears enemies"),
                Pt("Warlord", "#E5484D", "War Chains", "Group up and smash", "Conqueror: chained enemies fight for you"),
                Pt("Executioner", "#E5484D", "Great Axe", "Big single hits, executes", "Headsman: instantly kills any non-boss enemy under 25% HP")),
            Class("Archer", "#F2B33D", "Ranged · positioning", "Focus: standing still charges stronger shots; moving resets it", "Piercing Arrows", true,
                Pt("Sniper", "#F2B33D", "Piercing Shot", "Precision, positioning", "Deadeye: arrows pierce infinitely and mark enemies for double damage", true),
                Pt("Storm", "#6FE3D1", "Multishot", "Volume of fire", "Rain of Arrows: arrows constantly fall from the sky around you", true),
                Pt("Hunter", "#F2B33D", "Wolf & Traps", "Pet and trap control", "Beastmaster: a pack of wolves and a hawk fight alongside you"),
                Pt("Frost", "#3DA5F4", "Ice Arrows", "Control, shatter combos", "Winter Ranger: frozen enemies shatter into ice shards"),
                Pt("Spirit", "#9B5DE5", "Spectral Arrows", "Easy aim, magic hybrid", "Spirit Archer: a ghostly giant copy of you fires alongside you")),
            Class("Assassin", "#4CC38A", "Fast · risky · crits", "Shadowstep: dash with brief invisibility; stealth attacks crit", "Throwing Daggers", false,
                Pt("Venom", "#4CC38A", "Poison Daggers", "Hit and run, stack poison", "Plaguebringer: toxic cloud trail; poisoned enemies spread it"),
                Pt("Shadow", "#9B5DE5", "Shadowstep", "Stealth, burst crits", "Phantom: permanently half-invisible; every 5th hit is an execution"),
                Pt("Trickster", "#4CC38A", "Decoy Clone", "Distraction, chaos", "Mirror Master: up to 4 clones that copy your attacks"),
                Pt("Blood", "#E5484D", "Hemorrhage Blade", "Lifesteal duelist", "Vampire Lord: bat swarm form; overflow healing becomes a shield"),
                Pt("Wind", "#6FE3D1", "Fan of Knives", "Speed, many projectiles", "Tempest Blade: blades orbit you as a whirling storm")),
            Class("Tank (Knight)", "#4A8FE7", "Slow · durable", "Fortify: blocking builds a shockwave that releases automatically", "Guardian Shield", false,
                Pt("Paladin", "#FFD95A", "Holy Shield", "Sustain, holy damage", "Crusader: holy aura that burns enemies and heals you"),
                Pt("Bulwark", "#4A8FE7", "Shield Bash", "Pure defense", "Living Fortress: enemies touching you take damage; immune to knockback"),
                Pt("Thorns", "#B8A04A", "Bramble Armor", "Take hits on purpose", "Ironwood: roots erupt around you; reflected damage grows huge"),
                Pt("Juggernaut", "#4A8FE7", "Charge", "Fast, aggressive tank", "Unstoppable: charge leaves a shockwave trail; cooldown resets on kills"),
                Pt("Death Knight", "#9B5DE5", "Cursed Blade", "Dark tank, summons", "Dread Lord: frost aura + skeletal horse; your kills become shields")),
        };
        AssetDatabase.SaveAssets();
        return list;
    }

    static ClassData Class(string name, string color, string role, string mechanic, string weapon, bool unlocked, params P[] paths)
    {
        string file = name.Replace(" ", "").Replace("(", "_").Replace(")", "");
        var c = LoadOrCreate<ClassData>($"{Root}/Data/Classes/{file}.asset");
        c.weapons = new List<WeaponDef>();
        if (contentCache != null && classWeapons.TryGetValue(name, out var wl))
            foreach (var wn in wl) if (contentCache.byFile.TryGetValue(wn, out var cw)) c.weapons.Add(cw);
        c.defaultPathWeapon = contentCache != null && classDefaultPath.TryGetValue(name, out var dp) && contentCache.byFile.TryGetValue(dp, out var dpw) ? dpw : null;
        c.unlockHint = name == "Assassin" ? "Win 1 run (survive 15:00) to unlock" : name == "Tank (Knight)" ? "Win 2 runs to unlock" : "";
        c.className = name; c.portrait = art.TryGetValue(name, out var portraitArt) ? portraitArt : null; c.color = Hex(color); c.role = role; c.mechanic = mechanic; c.defaultWeapon = weapon; c.unlocked = unlocked;
        c.paths = new List<PathData>();
        foreach (var p in paths)
        {
            var d = LoadOrCreate<PathData>($"{Root}/Data/Paths/{file}_{p.n.Replace(" ", "")}.asset");
            d.heroArt = ArtFile($"InGame/Heroes/{file}_{p.n.Replace(" ", "")}.png", 220, bottomPivot: true);
            d.pathName = p.n; d.weapon = pathWeapons.TryGetValue(p.n, out var pw) ? pw : null; d.color = Hex(p.c); d.startingSkill = p.skill; d.playsLike = p.plays; d.ascension = p.asc; d.unlocked = unlocked && p.open;
            EditorUtility.SetDirty(d);
            c.paths.Add(d);
        }
        EditorUtility.SetDirty(c);
        return c;
    }

    static readonly Dictionary<string, string[]> classWeapons = new()
    {
        ["Mage"] = new[] { "ArcaneOrb", "MagicMissile", "FrostNova" },
        ["Berserker"] = new[] { "SpinningAxe", "ThrowingDaggers", "FrostNova" },
        ["Archer"] = new[] { "ThrowingDaggers", "MagicMissile", "FrostNova" },
        ["Assassin"] = new[] { "ThrowingDaggers", "SpinningAxe", "MagicMissile" },
        ["Tank (Knight)"] = new[] { "SpinningAxe", "FrostNova", "ArcaneOrb" },
    };
    static readonly Dictionary<string, string> classDefaultPath = new()
    {
        ["Mage"] = "Path_Fireball", ["Berserker"] = "Path_BloodAxes", ["Archer"] = "Path_PiercingShot",
        ["Assassin"] = "Path_ShadowDaggers", ["Tank (Knight)"] = "Path_GuardianShields",
    };

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (a) return a;
        a = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(a, path);
        return a;
    }


    // ------------------------------------------------------------------ world sprites + content

    static void LoadWorldSprites()
    {
        wBlob = WorldSprite("spr_blob.png"); wHead = WorldSprite("spr_head.png"); wCap = WorldSprite("spr_cap.png");
        wDot = WorldSprite("spr_dot.png"); wOrb = WorldSprite("spr_orb.png"); wBolt = WorldSprite("spr_bolt.png");
        wGem = WorldSprite("spr_gem.png"); wRing = WorldSprite("spr_ring.png"); wSoft = WorldSprite("spr_soft.png");
        wShadow = WorldSprite("spr_shadow.png"); wRock = WorldSprite("spr_rock.png");

        art.Clear();
        foreach (var (key, file, ppu) in new[] {
            ("Mage", "hero_mage", 220f), ("Berserker", "hero_berserker", 220f), ("Archer", "hero_archer", 220f),
            ("Assassin", "hero_assassin", 220f), ("Tank (Knight)", "hero_knight", 220f), ("Hollow", "enemy_hollow_walker", 256f) })
        {
            var sp = ArtSprite(file, ppu);
            if (sp != null) art[key] = sp;
        }

        foreach (var (key, file) in new[] { ("Boss0", "boss_ash_warden"), ("Boss1", "boss_rotting_matriarch"), ("Boss2", "boss_hollow_king") })
        {
            var sp = ArtFile($"InGame/Bosses/{file}.png", 256f, bottomPivot: true);
            if (sp != null) art[key] = sp;
        }
        menuBg = ArtFile("UI/menu_background.png", 100);
        groundTile = ArtFile("World/ground_tile.png", 200, tiled: true);
        gemArt = ArtFile("InGame/Icons/pickup_xp_gem.png", 300);

        string matPath = Root + "/Art/World/Sprite Unlit.mat";
        mUnlit = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mUnlit == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            mUnlit = new Material(shader);
            AssetDatabase.CreateAsset(mUnlit, matPath);
        }
    }

    static Sprite WorldSprite(string file)
    {
        string path = $"{Root}/Art/World/{file}";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) throw new System.Exception($"[Ashbound] Missing sprite file {path}");
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 128;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Character art from ChatGPT, trimmed into Art/InGame. Pivot at the feet.
    static Sprite ArtSprite(string file, float ppu)
    {
        string path = $"{Root}/Art/InGame/{file}.png";
        if (!System.IO.File.Exists(path)) return null;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu;
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        ti.SetTextureSettings(settings);
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    class Content { public Dictionary<string, WeaponDef> byFile = new(); public WeaponDef fallback; public List<Object> weaponPool = new(); public List<Object> passives = new(); }

    static LevelBonus B(float dmg = 0, int count = 0, float cd = 0, float area = 0, int pierce = 0, bool explode = false)
        => new() { damage = dmg, count = count, cooldown = cd, area = area, pierce = pierce, explode = explode };

    static WeaponDef Wpn(string file, string name, string awakeName, string color, WeaponKind kind,
        float dmg, float cd, float speed, float area, float life, float knock, int count, int pierce, bool explode,
        LevelBonus[] levels, LevelBonus awakening, params string[] text)
    {
        var w = LoadOrCreate<WeaponDef>($"{Root}/Data/Weapons/{file}.asset");
        w.weaponName = name; w.awakeningName = awakeName; w.color = Hex(color); w.kind = kind;
        w.damage = dmg; w.cooldown = cd; w.speed = speed; w.area = area; w.lifetime = life; w.knockback = knock;
        w.count = count; w.pierce = pierce; w.explode = explode;
        w.levels = levels; w.awakening = awakening; w.levelText = text;
        w.isEvolution = false; w.evolvePassive = null; w.evolvesInto = null;
        w.style = WeaponStyle.Default; w.effect = null; w.awakenIcon = null; w.awakenProjectile = null;
        if (contentBuilding != null) contentBuilding.byFile[file] = w;
        if (weaponArt.TryGetValue(file, out var wa)) { w.icon = Icon(wa.icon); w.projectile = Proj(wa.proj); }
        EditorUtility.SetDirty(w);
        return w;
    }

    static Content contentBuilding;

    static PassiveDef Pas(string name, string color, PassiveStat stat, float perLevel, string desc)
    {
        var p = LoadOrCreate<PassiveDef>($"{Root}/Data/Passives/{name}.asset");
        p.passiveName = name; p.color = Hex(color); p.stat = stat; p.perLevel = perLevel; p.description = desc;
        p.icon = Icon("icon_" + name.ToLowerInvariant());
        EditorUtility.SetDirty(p);
        return p;
    }

    static Content BuildContent()
    {
        foreach (var dir in new[] { "Weapons", "Passives" })
            if (!AssetDatabase.IsValidFolder($"{Root}/Data/{dir}")) AssetDatabase.CreateFolder($"{Root}/Data", dir);

        var c = new Content(); contentBuilding = c;
        pathWeapons.Clear();

        // ---- Path skills (launch Paths)
        pathWeapons["Fire"] = Wpn("Path_Fireball", "Fireball", "Inferno Core", "#FF6B35", WeaponKind.Projectile,
            14, 1.1f, 9, 1f, 1.6f, 1.5f, 1, 0, true,
            new[] { B(dmg: .25f), B(count: 1), B(area: .3f), B(dmg: .15f, count: 1) }, B(dmg: .5f, count: 2, area: .3f),
            "Hurls a fireball at the nearest enemy that explodes on impact", "+25% damage", "+1 fireball", "Explosions 30% bigger", "+1 fireball, +15% damage",
            "Inferno Core: +2 fireballs, +50% damage, bigger explosions");
        pathWeapons["Water"] = Wpn("Path_TidalWave", "Tidal Wave", "Maelstrom", "#3DA5F4", WeaponKind.Nova,
            10, 2.4f, 0, 1f, 0, 9f, 1, 0, false,
            new[] { B(area: .3f), B(dmg: .25f), B(cd: .2f), B(dmg: .2f, area: .3f) }, B(dmg: .5f, cd: .3f, area: .4f),
            "A wave bursts out around you and shoves enemies back", "Wave 30% wider", "+25% damage", "Waves 20% more often", "+20% damage, 30% wider",
            "Maelstrom: huge, frequent waves with +50% damage");
        pathWeapons["Air"] = Wpn("Path_ChainLightning", "Chain Lightning", "Thunderstorm Heart", "#6FE3D1", WeaponKind.Projectile,
            10, 0.8f, 16, 0.8f, 1.2f, 0.5f, 1, 2, false,
            new[] { B(pierce: 2), B(count: 1), B(dmg: .25f), B(count: 1, pierce: 2) }, B(dmg: .4f, count: 2, pierce: 4),
            "A fast bolt that passes through 3 enemies", "Passes through 2 more", "+1 bolt", "+25% damage", "+1 bolt, passes through 2 more",
            "Thunderstorm Heart: +2 bolts that rip through everything");
        pathWeapons["Bloodlust"] = Wpn("Path_BloodAxes", "Blood Axes", "Crimson Rampage", "#E5484D", WeaponKind.Orbit,
            13, 0, 6, 1f, 0, 3f, 2, 0, false,
            new[] { B(count: 1), B(dmg: .25f), B(area: .25f), B(count: 1) }, B(dmg: .5f, count: 2, area: .2f),
            "Two axes spin around you, cutting through the Hollow", "+1 axe", "+25% damage", "Axes orbit 25% wider", "+1 axe",
            "Crimson Rampage: +2 axes, +50% damage");
        pathWeapons["Thunder"] = Wpn("Path_ThunderHammer", "Thunder Hammer", "Storm Echo", "#6FE3D1", WeaponKind.Nova,
            18, 2.0f, 0, 0.9f, 0, 4f, 1, 0, false,
            new[] { B(dmg: .25f), B(area: .25f), B(cd: .2f), B(dmg: .25f, area: .2f) }, B(dmg: .5f, cd: .3f, explode: true),
            "Slams the ground, hitting everything near you", "+25% damage", "Slam 25% wider", "Slams 20% more often", "+25% damage, 20% wider",
            "Storm Echo: faster slams that set off a second blast");
        pathWeapons["Sniper"] = Wpn("Path_PiercingShot", "Piercing Shot", "Railshot", "#F2B33D", WeaponKind.Projectile,
            24, 1.4f, 18, 1f, 1.5f, 2f, 1, 3, false,
            new[] { B(dmg: .25f), B(pierce: 3), B(cd: .2f), B(dmg: .4f) }, B(dmg: .6f, count: 1, pierce: 20),
            "A heavy arrow that pierces 4 enemies", "+25% damage", "Pierces 3 more", "Shoots 20% faster", "+40% damage",
            "Railshot: +1 arrow that pierces the whole screen, +60% damage");
        pathWeapons["Storm"] = Wpn("Path_Multishot", "Multishot", "Arrow Storm", "#F2B33D", WeaponKind.Projectile,
            8, 1.0f, 12, 0.9f, 1.2f, 1f, 3, 0, false,
            new[] { B(count: 2), B(dmg: .2f), B(cd: .15f), B(count: 2) }, B(dmg: .3f, count: 4, cd: .25f),
            "Fires a spread of 3 arrows", "+2 arrows", "+20% damage", "Fires 15% faster", "+2 arrows",
            "Arrow Storm: +4 arrows, much faster volleys");
        Wpn("Path_ShadowDaggers", "Shadow Daggers", "Night Reaper", "#4CC38A", WeaponKind.Projectile,
            9, 0.55f, 16, 0.8f, 1f, 0.5f, 2, 1, false,
            new[] { B(count: 1), B(dmg: .25f), B(cd: .15f, pierce: 1), B(count: 1, dmg: .2f) }, B(dmg: .5f, count: 3, pierce: 3),
            "Flings shadow daggers at the nearest enemies", "+1 dagger", "+25% damage", "Throws 15% faster, pierces 1 more", "+1 dagger, +20% damage",
            "Night Reaper: +3 daggers that cut through whole packs");
        Wpn("Path_GuardianShields", "Guardian Shields", "Aegis Wall", "#4A8FE7", WeaponKind.Orbit,
            12, 0, 3.5f, 1.1f, 0, 6f, 2, 0, false,
            new[] { B(count: 1), B(dmg: .25f), B(area: .2f), B(count: 1) }, B(dmg: .6f, count: 2, area: .3f),
            "Two shields circle you and slam enemies away", "+1 shield", "+25% damage", "Orbit 20% wider", "+1 shield",
            "Aegis Wall: +2 shields, +60% damage, wider orbit");
        c.fallback = pathWeapons["Fire"];

        // ---- weapons found in level-ups
        c.weaponPool.Add(Wpn("ArcaneOrb", "Arcane Orb", "Black Hole", "#8E6BEF", WeaponKind.Orbit,
            9, 0, 4, 1f, 0, 1f, 1, 0, false,
            new[] { B(count: 1), B(dmg: .25f), B(count: 1), B(area: .3f) }, B(dmg: .5f, count: 2, area: .5f),
            "An orb of arcane energy circles you", "+1 orb", "+25% damage", "+1 orb", "Orbs 30% bigger", "Black Hole: +2 huge orbs, +50% damage"));
        c.weaponPool.Add(Wpn("MagicMissile", "Magic Missile", "Starfall", "#B9A6FF", WeaponKind.Projectile,
            10, 1.2f, 10, 0.8f, 2f, 0.5f, 2, 0, false,
            new[] { B(count: 1), B(dmg: .25f), B(cd: .2f), B(count: 2) }, B(dmg: .3f, count: 3, explode: true),
            "Fires 2 missiles at the nearest enemy", "+1 missile", "+25% damage", "Fires 20% faster", "+2 missiles", "Starfall: +3 missiles that explode"));
        c.weaponPool.Add(Wpn("FrostNova", "Frost Nova", "Glacier", "#7CC4F8", WeaponKind.Nova,
            7, 3f, 0, 1.1f, 0, 2f, 1, 0, false,
            new[] { B(area: .25f), B(dmg: .3f), B(cd: .2f), B(area: .25f) }, B(dmg: .5f, cd: .3f, area: .3f, explode: true),
            "A burst of frost hits everything around you", "25% wider", "+30% damage", "20% more often", "25% wider", "Glacier: bigger, faster novas with an icy blast"));
        c.weaponPool.Add(Wpn("SpinningAxe", "Spinning Axe", "Crimson Whirlwind", "#C9C4BA", WeaponKind.Orbit,
            12, 0, 6, 1f, 0, 3f, 1, 0, false,
            new[] { B(dmg: .25f), B(count: 1), B(area: .25f), B(count: 1) }, B(dmg: .5f, count: 2),
            "A heavy axe whirls around you", "+25% damage", "+1 axe", "Orbit 25% wider", "+1 axe", "Crimson Whirlwind: +2 axes, +50% damage"));
        c.weaponPool.Add(Wpn("ThrowingDaggers", "Throwing Daggers", "Phantom Blades", "#4CC38A", WeaponKind.Projectile,
            6, 0.6f, 15, 0.7f, 0.9f, 0.5f, 1, 1, false,
            new[] { B(count: 1), B(cd: .2f), B(pierce: 1, count: 1), B(dmg: .3f) }, B(dmg: .4f, count: 3, pierce: 3),
            "Quickly throws daggers that pass through 2 enemies", "+1 dagger", "Throws 20% faster", "+1 dagger, pierces 1 more", "+30% damage",
            "Phantom Blades: +3 daggers that slice through crowds"));

        // ---- passives
        var passiveByName = new Dictionary<string, PassiveDef>();
        c.passives.Add(Pas("Might", "#E5484D", PassiveStat.Damage, 0.1f, "+10% damage for all weapons"));
        c.passives.Add(Pas("Haste", "#6FE3D1", PassiveStat.Cooldown, 0.08f, "Weapons fire 8% faster"));
        c.passives.Add(Pas("Swiftness", "#4CC38A", PassiveStat.MoveSpeed, 0.1f, "+10% movement speed"));
        c.passives.Add(Pas("Vitality", "#FF8A8A", PassiveStat.MaxHealth, 20f, "+20 max health"));
        c.passives.Add(Pas("Reach", "#8E6BEF", PassiveStat.Area, 0.1f, "+10% weapon area"));
        c.passives.Add(Pas("Magnet", "#3DA5F4", PassiveStat.Pickup, 0.3f, "+30% XP pickup range"));
        foreach (PassiveDef pd in c.passives) passiveByName[pd.passiveName] = pd;

        // ---- evolutions: class weapon at Lv 5 + the matching passive = one crazy weapon
        WeaponDef Evo(string file, string name, string color, WeaponKind kind, float dmg, float cd, float speed, float area, float life, float knock, int count, int pierce, bool explode, string text)
        {
            var e = Wpn(file, name, name, color, kind, dmg, cd, speed, area, life, knock, count, pierce, explode,
                new[] { B(), B(), B(), B() }, B(), text, text, text, text, text, text);
            e.isEvolution = true; EditorUtility.SetDirty(e);
            return e;
        }
        void Pair(string baseFile, string passive, WeaponDef evo)
        {
            var w = c.byFile[baseFile]; w.evolvePassive = passiveByName[passive]; w.evolvesInto = evo; EditorUtility.SetDirty(w);
        }
        Pair("ArcaneOrb", "Reach", Evo("Evo_BlackHole", "Black Hole", "#9B5DE5", WeaponKind.Nova, 30, 3.2f, 0, 1.2f, 0, 0f, 1, 0, false,
            "EVOLVED: opens on the biggest crowd, pulls everything in, then explodes"));
        Pair("MagicMissile", "Haste", Evo("Evo_Starfall", "Starfall", "#C9B8FF", WeaponKind.Projectile, 26, 1.5f, 0, 1f, 0, 3f, 6, 0, false,
            "EVOLVED: volleys of 6 stars fall on enemies all over the screen"));
        Pair("FrostNova", "Vitality", Evo("Evo_Glacier", "Glacier", "#9BD8FF", WeaponKind.Nova, 26, 2.0f, 0, 1.4f, 0, 3f, 1, 0, false,
            "EVOLVED: ice spikes erupt around you and slow enemies by 75%"));
        Pair("SpinningAxe", "Might", Evo("Evo_CrimsonWhirlwind", "Crimson Whirlwind", "#E5484D", WeaponKind.Orbit, 22, 0, 9, 1.0f, 0, 5f, 4, 0, false,
            "EVOLVED: a giant red tornado full of axes surrounds you"));
        Pair("ThrowingDaggers", "Swiftness", Evo("Evo_PhantomBlades", "Phantom Blades", "#4CC38A", WeaponKind.Projectile, 16, 0.9f, 12, 1f, 1.1f, 0.5f, 3, 0, false,
            "EVOLVED: 3 ghost blades boomerang back to you, 2 more circle you"));
        // ---- looks: styles, impact art, awakened icons
        void Look(string file, WeaponStyle style, string effect = null, string awakenIcon = null, string awakenProj = null)
        {
            if (!c.byFile.TryGetValue(file, out var w)) return;
            w.style = style;
            w.effect = effect != null ? FxArtFile(effect) : null;
            w.awakenIcon = awakenIcon != null ? Icon(awakenIcon) : null;
            w.awakenProjectile = awakenProj != null ? FxArtFile(awakenProj) : null;
            EditorUtility.SetDirty(w);
        }
        Look("Path_Fireball", WeaponStyle.Default, "fx_fire_burst", "awk_inferno_core");
        Look("Path_TidalWave", WeaponStyle.Wave, null, "awk_maelstrom");
        Look("Path_ChainLightning", WeaponStyle.Chain, null, "awk_thunderstorm_heart");
        Look("Path_BloodAxes", WeaponStyle.Default, null, "awk_crimson_rampage");
        Look("Path_ThunderHammer", WeaponStyle.Hammer, "fx_fire_burst", "awk_storm_echo");
        Look("Path_PiercingShot", WeaponStyle.Default, null, "awk_railshot", "fx_golden_arrow");
        Look("Path_Multishot", WeaponStyle.Default, null, "awk_arrow_storm", "fx_golden_arrow");
        Look("Path_ShadowDaggers", WeaponStyle.Default, null, "awk_night_reaper");
        Look("Path_GuardianShields", WeaponStyle.Default, null, "awk_aegis_wall");
        Look("FrostNova", WeaponStyle.Frost, "fx_frost_burst");
        Look("Evo_BlackHole", WeaponStyle.BlackHole);
        Look("Evo_Starfall", WeaponStyle.Starfall, "fx_star_burst");
        Look("Evo_Glacier", WeaponStyle.Glacier, "fx_frost_burst");
        Look("Evo_CrimsonWhirlwind", WeaponStyle.Whirlwind);
        Look("Evo_PhantomBlades", WeaponStyle.PhantomBlades);
        contentBuilding = null;

        AssetDatabase.SaveAssets();
        return c;
    }

    // ------------------------------------------------------------------ gameplay prefabs

    class Gameplay { public GameObject player, enemy, gem, projectile, vfx, orb, rock, patch, pickup, damageNumber, deathPop; }

    static SpriteRenderer Spr(Transform parent, string name, Sprite sprite, Color color, int order, Vector2 pos = default, float scale = 1f)
    {
        var go = new GameObject(name);
        if (parent) go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * scale;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite; r.color = color; r.sortingOrder = order; r.sharedMaterial = mUnlit;
        return r;
    }

    static GameObject SaveGameplay(GameObject go)
    {
        string dir = Root + "/Prefabs/Gameplay";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(Root + "/Prefabs", "Gameplay");
        var asset = PrefabUtility.SaveAsPrefabAsset(go, $"{dir}/{go.name}.prefab");
        Object.DestroyImmediate(go);
        return asset;
    }

    static Gameplay BuildGameplayPrefabs()
    {
        var g = new Gameplay();
        var ember = Palette.Ember;

        // Player
        var player = new GameObject("Player");
        var prb = player.AddComponent<Rigidbody2D>();
        prb.bodyType = RigidbodyType2D.Kinematic; prb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var pcol = player.AddComponent<CircleCollider2D>(); pcol.radius = 0.3f; pcol.offset = new Vector2(0, 0.3f);
        player.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = 1;
        Spr(player.transform, "Shadow", wShadow, Color.white, -1, new Vector2(0, 0.02f), 0.9f);
        var visual = new GameObject("Visual").transform; visual.SetParent(player.transform, false);
        var stats = player.AddComponent<PlayerStats>();
        var pc = player.AddComponent<PlayerController>();
        Set(pc, "visual", visual);
        var ringSprite = ArtFile("World/spr_ring_thin.png", 256);
        if (ringSprite != null)
        {
            var ringR = Spr(player.transform, "PickupRing", ringSprite, new Color(0.55f, 0.8f, 1f, 0.22f), -3, Vector2.zero, 1f);
            Set(stats, "pickupRing", ringR.transform);
        }
        if (art.TryGetValue("Mage", out var heroArt))
        {
            var hero = Spr(visual, "Hero", heroArt, Color.white, 2);
            SetArr(stats, "flashRenderers", new List<Object> { hero });
            Set(pc, "heroSprite", hero);
        }
        else
        {
            var body = Spr(visual, "Body", wBlob, Hex("#8E6BEF"), 2, new Vector2(0, 0.42f), 0.8f);
            var head = Spr(visual, "Head", wHead, Color.white, 3, new Vector2(0.04f, 0.98f), 0.72f);
            var cap = Spr(visual, "Hood", wCap, Hex("#8E6BEF"), 4, new Vector2(0.04f, 1.14f), 0.74f);
            SetArr(stats, "flashRenderers", new List<Object> { body, head, cap });
            SetArr(pc, "classTinted", new List<Object> { body, cap });
        }
        g.player = SaveGameplay(player);

        // Enemy
        var enemy = new GameObject("Enemy");
        var erb = enemy.AddComponent<Rigidbody2D>();
        erb.gravityScale = 0f; erb.freezeRotation = true; erb.mass = 1f;
        var ecol = enemy.AddComponent<CircleCollider2D>(); ecol.radius = 0.3f; ecol.offset = new Vector2(0, 0.3f);
        enemy.AddComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder = 0;
        Spr(enemy.transform, "Shadow", wShadow, Color.white, -1, new Vector2(0, 0.02f), 0.85f);
        var ev = new GameObject("Visual").transform; ev.SetParent(enemy.transform, false);
        SpriteRenderer ebody;
        if (art.TryGetValue("Hollow", out var hollowArt)) ebody = Spr(ev, "Body", hollowArt, Color.white, 2);
        else ebody = Spr(ev, "Body", wBlob, Palette.Hollow, 2, new Vector2(0, 0.42f), 0.75f);
        if (hollowArt == null) foreach (var x in new[] { -0.06f, 0.16f })
        {
            Spr(ev, "EyeGlow", wSoft, new Color(ember.r, ember.g, ember.b, 0.6f), 3, new Vector2(x, 0.56f), 0.28f);
            Spr(ev, "Eye", wDot, ember, 4, new Vector2(x, 0.56f), 0.13f);
        }
        var en = enemy.AddComponent<Enemy>();
        Set(en, "body", ebody); Set(en, "visual", ev);
        g.enemy = SaveGameplay(enemy);

        // XP gem
        var gem = Spr(null, "XPGem", gemArt != null ? gemArt : wGem, Color.white, -5);
        var gemC = gem.gameObject.AddComponent<XPGem>();
        Set(gemC, "sprite", gem);
        SetArr(gemC, "tierSprites", new List<Object> { gemArt != null ? gemArt : wGem,
            ArtFile("InGame/Icons/pickup_xp_gem_green.png", 300) ?? wGem, ArtFile("InGame/Icons/pickup_xp_gem_red.png", 300) ?? wGem,
            ArtFile("InGame/Icons/pickup_xp_gem_purple.png", 300) ?? wGem });
        g.gem = SaveGameplay(gem.gameObject);

        // Projectile
        var proj = Spr(null, "Projectile", wBolt, Color.white, 10);
        Spr(proj.transform, "Glow", wSoft, new Color(1, 1, 1, 0.35f), 9, Vector2.zero, 1.2f);
        Set(proj.gameObject.AddComponent<Projectile>(), "sprite", proj);
        g.projectile = SaveGameplay(proj.gameObject);

        // VFX ring
        var vfx = Spr(null, "Vfx", wRing, Color.white, 15);
        Set(vfx.gameObject.AddComponent<Vfx>(), "sprite", vfx);
        g.vfx = SaveGameplay(vfx.gameObject);

        // Orb (orbit weapons)
        var orb = Spr(null, "Orb", wOrb, Color.white, 8);
        g.orb = SaveGameplay(orb.gameObject);

        // Pickups (meat, medkit, magnet...): root moves/bobs, child holds the icon at a world-friendly size
        var pick = new GameObject("Pickup");
        Spr(pick.transform, "Shadow", wShadow, Color.white, -6, new Vector2(0, -0.28f), 0.6f);
        var pickSpr = Spr(pick.transform, "Icon", null, Color.white, -4, Vector2.zero, 0.26f);
        Set(pick.AddComponent<Pickup>(), "sprite", pickSpr);
        g.pickup = SaveGameplay(pick);

        // Damage number (world-space TMP with an outline material)
        var dn = new GameObject("DamageNumber");
        var dnt = dn.AddComponent<TextMeshPro>();
        dnt.font = fBlack; dnt.fontSize = 4f; dnt.alignment = TextAlignmentOptions.Center; dnt.text = "12";
        dnt.textWrappingMode = TextWrappingModes.NoWrap; dnt.rectTransform.sizeDelta = new Vector2(4, 1.2f);
        string olPath = $"{Fonts}/Nunito-Black Outline.mat";
        var ol = AssetDatabase.LoadAssetAtPath<Material>(olPath);
        if (ol == null) { ol = new Material(fBlack.material); AssetDatabase.CreateAsset(ol, olPath); }
        ol.shader = fBlack.material.shader; ol.CopyPropertiesFromMaterial(fBlack.material);
        ol.EnableKeyword("OUTLINE_ON"); ol.SetFloat("_OutlineWidth", 0.28f); ol.SetColor("_OutlineColor", Palette.Outline); ol.SetFloat("_FaceDilate", 0.1f);
        EditorUtility.SetDirty(ol);
        dnt.fontSharedMaterial = ol;
        dn.GetComponent<MeshRenderer>().sortingOrder = 30;
        Set(dn.AddComponent<DamageNumber>(), "text", dnt);
        g.damageNumber = SaveGameplay(dn);

        // Death pop (squash + fade copy of the enemy sprite)
        var dp = Spr(null, "DeathPop", wBlob, Color.white, 5);
        Set(dp.gameObject.AddComponent<DeathPop>(), "sprite", dp);
        g.deathPop = SaveGameplay(dp.gameObject);

        // Decor
        g.rock = SaveGameplay(Spr(null, "Rock", wRock, Color.white, -20).gameObject);
        g.patch = SaveGameplay(Spr(null, "GroundPatch", wSoft, new Color(0.11f, 0.10f, 0.14f, 0.55f), -30).gameObject);
        return g;
    }

    // ------------------------------------------------------------------ prefabs

    class Prefabs { public GameObject primary, secondary, panel, classButton, pathCard, upgradeCard; }

    static Prefabs BuildPrefabs()
    {
        var p = new Prefabs();

        var prim = MakeButton(null, "Button_Primary", "Button", true, new Vector2(450, 90), 36);
        p.primary = Save(prim.gameObject);
        var sec = MakeButton(null, "Button_Secondary", "Button", false, new Vector2(450, 78), 30);
        p.secondary = Save(sec.gameObject);

        var panel = Node("Panel", null); panel.sizeDelta = new Vector2(400, 300);
        Img(panel, sPanel, Palette.Panel);
        p.panel = Save(panel.gameObject);

        // Class button: dot + label + selection ring
        var cb = MakeButton(null, "ClassButton", "Class", false, new Vector2(300, 84), 27);
        var cbLabel = cb.GetComponentInChildren<TextMeshProUGUI>();
        cbLabel.rectTransform.offsetMin = new Vector2(66, 0);
        var dot = Node("Dot", cb.transform); Place(dot, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(27, 0), new Vector2(27, 27));
        Img(dot, sCircle, Color.white);
        var ring = Node("Ring", cb.transform); Stretch(ring, -3, -3, -3, -3); Img(ring, sRing, Color.white).enabled = false;
        p.classButton = Save(cb.gameObject);

        p.pathCard = Save(MakePathCard());
        p.upgradeCard = Save(MakeUpgradeCard());
        return p;
    }

    static GameObject Save(GameObject go)
    {
        var asset = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{go.name}.prefab");
        Object.DestroyImmediate(go);
        return asset;
    }

    static GameObject Inst(GameObject prefab, Transform parent, string name = null)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        if (name != null) go.name = name;
        return go;
    }

    static Button MakeButton(Transform parent, string name, string label, bool primary, Vector2 size, float fontSize)
    {
        var rt = Node(name, parent); rt.sizeDelta = size;
        var img = Img(rt, sPanel, primary ? Palette.Accent : Palette.Panel);
        img.raycastTarget = true;
        var shadow = rt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = Palette.Outline; shadow.effectDistance = new Vector2(0, -8);
        var b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cols = b.colors;
        cols.normalColor = new Color(0.92f, 0.92f, 0.92f);
        cols.highlightedColor = Color.white;
        cols.selectedColor = new Color(0.92f, 0.92f, 0.92f);
        cols.pressedColor = new Color(0.72f, 0.72f, 0.72f);
        cols.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
        b.colors = cols;
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size.x; le.preferredHeight = size.y;
        var t = Txt(rt, "Label", label, primary ? fBlack : fBlack, fontSize, primary ? Palette.Outline : Palette.Text, TextAlignmentOptions.MidlineLeft);
        Stretch(t.rectTransform, 33, 18, 0, 0);
        return b;
    }

    static GameObject MakePathCard()
    {
        var root = Node("PathCard", null); root.sizeDelta = new Vector2(300, 196);
        var bg = Img(root, sPanel, Palette.Panel); bg.raycastTarget = true;
        var btn = root.gameObject.AddComponent<Button>(); btn.targetGraphic = bg;
        var cols = btn.colors; cols.normalColor = new Color(0.92f, 0.92f, 0.92f); cols.highlightedColor = Color.white; cols.selectedColor = cols.normalColor; btn.colors = cols;
        var group = root.gameObject.AddComponent<CanvasGroup>();
        var v = root.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(24, 24, 21, 18); v.spacing = 6;
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;

        var head = Node("Header", root);
        var h = head.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 12; h.childAlignment = TextAnchor.MiddleLeft; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        var dot = Node("Dot", head); Img(dot, sCircle, Color.white); Fixed(dot, 21, 21);
        var name = Txt(head, "Name", "Fire", fBlack, 27, Palette.Text);
        var skill = Txt(root, "Skill", "Fireball", fBlack, 21, Palette.Text);
        var plays = Txt(root, "Plays", "Max damage, burn stacking", fBold, 19, Palette.TextMuted);
        var locked = Txt(root, "Locked", "Locked · unlock by achievement", fBlack, 17, Palette.Accent);

        var ring = Node("SelectionRing", root); Stretch(ring, -4, -4, -4, -4);
        ring.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Img(ring, sRing, Color.white);

        var card = root.gameObject.AddComponent<PathCard>();
        Set(card, "nameText", name); Set(card, "skillText", skill); Set(card, "playsText", plays);
        Set(card, "dot", dot.GetComponent<Image>()); Set(card, "selectionRing", ring.GetComponent<Image>());
        Set(card, "lockedLabel", locked.gameObject); Set(card, "button", btn); Set(card, "group", group);
        return root.gameObject;
    }

    static GameObject MakeUpgradeCard()
    {
        var root = Node("UpgradeCard", null); root.sizeDelta = new Vector2(450, 600);
        Fixed(root, 450, 600);

        var glow = Node("Glow", root); Place(glow, Center, Center, Center, Vector2.zero, new Vector2(700, 860));
        var glowImg = Img(glow, sGlow, new Color(1f, 0.85f, 0.35f, 0.35f));
        var frame = Node("Frame", root); Stretch(frame, 0, 0, 0, 0);
        var frameImg = Img(frame, sPanel, Palette.Panel); frameImg.raycastTarget = true;
        var shadow = frame.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0.05f, 0.05f, 0.07f); shadow.effectDistance = new Vector2(0, -12);
        var gold = Node("GoldRing", root); Stretch(gold, -3, -3, -3, -3);
        var goldImg = Img(gold, sRing, Palette.Gold);

        var btn = root.gameObject.AddComponent<Button>(); btn.targetGraphic = frameImg;
        var cols = btn.colors; cols.normalColor = new Color(0.94f, 0.94f, 0.94f); cols.highlightedColor = Color.white; cols.selectedColor = cols.normalColor; btn.colors = cols;

        var content = Node("Content", root); Stretch(content, 0, 0, 0, 0);
        var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(33, 33, 39, 33); v.spacing = 18; v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = false; v.childForceExpandHeight = false;

        var tag = Node("Tag", content); Fixed(tag, 220, 38);
        var tagBgNode = Node("Bg", tag); Stretch(tagBgNode, 0, 0, 0, 0);
        var tagBg = Img(tagBgNode, sFill, Palette.Rock, 2f);
        var tagText = Txt(tag, "Text", "WEAPON", fBlack, 19, Palette.Text, TextAlignmentOptions.Center);
        Stretch(tagText.rectTransform, 8, 8, 0, 0);
        tagText.characterSpacing = 6; tagText.textWrappingMode = TextWrappingModes.NoWrap;

        var iconBox = Node("IconBox", content); Fixed(iconBox, 138, 138);
        Img(iconBox, sPanel, Palette.Void);
        var icon = Node("Icon", iconBox); Place(icon, Center, Center, Center, Vector2.zero, new Vector2(78, 78));
        var iconImg = Img(icon, sCircle, Color.white);

        var name = Txt(content, "Name", "Inferno Core", fTitle, 39, Palette.Text, TextAlignmentOptions.Center);
        name.textWrappingMode = TextWrappingModes.NoWrap; name.enableAutoSizing = true; name.fontSizeMin = 22; name.fontSizeMax = 39;
        name.gameObject.AddComponent<LayoutElement>().preferredWidth = 384;
        var level = Txt(content, "Level", "Lv 5 » Awakened", fBold, 22, Palette.TextMuted, TextAlignmentOptions.Center);

        var pipRow = Node("Pips", content);
        var ph = pipRow.gameObject.AddComponent<HorizontalLayoutGroup>(); ph.spacing = 8; ph.childAlignment = TextAnchor.MiddleCenter;
        ph.childControlWidth = true; ph.childControlHeight = true; ph.childForceExpandWidth = false; ph.childForceExpandHeight = false;
        var pips = new List<Object>();
        for (int i = 0; i < 6; i++)
        {
            var pip = Node("Pip" + (i + 1), pipRow); Fixed(pip, i == 5 ? 33 : 24, 12);
            pips.Add(Img(pip, sFill, Palette.Rock, 4f));
        }

        var effect = Txt(content, "Effect", "Effect text", fBlack, 25, Palette.Text, TextAlignmentOptions.Center);
        effect.lineSpacing = 8;

        var card = root.gameObject.AddComponent<UpgradeCard>();
        Set(card, "frame", frameImg); Set(card, "icon", iconImg); Set(card, "tagBg", tagBg); Set(card, "goldRing", goldImg); Set(card, "glow", glowImg);
        Set(card, "tagText", tagText); Set(card, "nameText", name); Set(card, "levelText", level); Set(card, "effectText", effect);
        SetArr(card, "pips", pips); Set(card, "button", btn);
        return root.gameObject;
    }

    // ------------------------------------------------------------------ scenes

    static Scene NewScene(Color background)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0, 0, -10);
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 5.4f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = background;

        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        return scene;
    }

    static RectTransform MakeCanvas(string name, int sortOrder = 0)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = 5;
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = sortOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return (RectTransform)go.transform;
    }

    static void BuildMainMenu()
    {
        var scene = NewScene(Palette.Void);
        var canvas = MakeCanvas("Canvas");

        var bg = Node("Background", canvas); Stretch(bg, 0, 0, 0, 0); Img(bg, null, Palette.Void);
        if (menuBg != null)
        {
            var bgArt = Node("BackgroundArt", canvas); Stretch(bgArt, 0, 0, 0, 0);
            Img(bgArt, menuBg, Color.white);
            var fit = bgArt.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = menuBg.rect.width / menuBg.rect.height;
            var shade = Node("LeftShade", canvas); Place(shade, Vector2.zero, new Vector2(0.75f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var fade = ArtFile("UI/ui_fade_right.png", 100);
            Img(shade, fade, new Color(0.07f, 0.06f, 0.09f, 0.75f));
        }
        if (menuBg == null) {
        var ground = Node("Ground", canvas); Place(ground, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 390)); Img(ground, null, Palette.Ground);
        var edge = Node("GroundEdge", canvas); Place(edge, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 390), new Vector2(0, 9)); Img(edge, null, Palette.Outline);

        foreach (var (x, y, s) in new[] { (210f, 300f, 26f), (480f, 465f, 18f), (1470f, 540f, 22f), (1680f, 240f, 18f), (1140f, 870f, 15f), (900f, 180f, 14f) })
            Ember(canvas, new Vector2(x, y), s);

        // Hollow enemies on the horizon
        Hollow(canvas, new Vector2(1290, 135), new Vector2(105, 138));
        Hollow(canvas, new Vector2(1434, 135), new Vector2(135, 192));
        Hollow(canvas, new Vector2(1608, 135), new Vector2(90, 117));
        }

        var title = Txt(canvas, "Title", "ASHBOUND", fTitle, 144, Palette.Accent);
        title.fontSharedMaterial = mTitleGlow; title.characterSpacing = 8; title.textWrappingMode = TextWrappingModes.NoWrap;
        Place(title.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(140, -120), new Vector2(1100, 180));
        var sub = Txt(canvas, "Subtitle", "Recover the Ember Crown. Survive the night.", fBold, 33, Palette.TextMuted);
        Place(sub.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(148, -300), new Vector2(1000, 50));

        var menu = Node("MenuButtons", canvas); Place(menu, TopLeft, TopLeft, TopLeft, new Vector2(144, -400), new Vector2(450, 580));
        var v = menu.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = 14;
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        var play = Btn(Inst(prefabsCache.primary, menu, "Play"), "Play");
        var heroes = Btn(Inst(prefabsCache.secondary, menu, "Heroes"), "Heroes");
        var unlocks = Btn(Inst(prefabsCache.secondary, menu, "Unlocks"), "Unlocks");
        var upgrades = Btn(Inst(prefabsCache.secondary, menu, "Upgrades"), "Upgrades");
        var settings = Btn(Inst(prefabsCache.secondary, menu, "Settings"), "Settings");
        var quit = Btn(Inst(prefabsCache.secondary, menu, "Quit"), "Quit");

        var stats = Node("TopRight", canvas); Place(stats, TopRight, TopRight, TopRight, new Vector2(-72, -60), new Vector2(700, 84));
        var sh = stats.gameObject.AddComponent<HorizontalLayoutGroup>(); sh.spacing = 18; sh.childAlignment = TextAnchor.MiddleRight;
        sh.childControlWidth = true; sh.childControlHeight = true; sh.childForceExpandWidth = false; sh.childForceExpandHeight = true;
        var crownVal = StatPill(stats, "CrownShards", "Crown shards", "0 / 3", Palette.Gold);
        var goldVal = StatPill(stats, "Gold", "Gold", "0", Amber);

        var ver = Txt(canvas, "Version", "Prototype v0.1", fBold, 21, Palette.TextMuted);
        Place(ver.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 24), new Vector2(400, 32));
        ver.alignment = TextAlignmentOptions.MidlineRight;

        var mm = canvas.gameObject.AddComponent<MainMenu>();
        UnityEventTools.AddPersistentListener(play.onClick, new UnityAction(mm.Play));
        UnityEventTools.AddPersistentListener(quit.onClick, new UnityAction(mm.Quit));
        UnityEventTools.AddPersistentListener(heroes.onClick, new UnityAction(mm.OpenHeroes));
        UnityEventTools.AddPersistentListener(unlocks.onClick, new UnityAction(mm.OpenUnlocks));
        UnityEventTools.AddPersistentListener(upgrades.onClick, new UnityAction(mm.OpenUpgrades));
        UnityEventTools.AddPersistentListener(settings.onClick, new UnityAction(mm.OpenSettings));
        Set(mm, "goldText", goldVal); Set(mm, "crownText", crownVal);

        // ---- Settings
        var (setRoot, setPanel) = MenuOverlay(canvas, "SettingsPanel", "SETTINGS", new Vector2(760, 920));
        var music = LabeledSlider(setPanel, "Music");
        var sfx = LabeledSlider(setPanel, "Sound effects");
        var fsBtn = Btn(Inst(prefabsCache.secondary, setPanel, "Fullscreen"), "Fullscreen: On");
        var resRow = Row(setPanel, "Resolution", 12);
        var resPrev = Btn(Inst(prefabsCache.secondary, resRow, "Prev"), "<", 78, 72);
        var resLbl = Txt(resRow, "Label", "1920 x 1080", fBlack, 26, Palette.Text, TextAlignmentOptions.Center);
        resLbl.gameObject.AddComponent<LayoutElement>().preferredWidth = 300;
        var resNext = Btn(Inst(prefabsCache.secondary, resRow, "Next"), ">", 78, 72);
        foreach (var b in new[] { resPrev, resNext }) { var l = b.GetComponentInChildren<TextMeshProUGUI>(); l.alignment = TextAlignmentOptions.Center; Stretch(l.rectTransform, 0, 0, 0, 0); }
        var dmgBtn = Btn(Inst(prefabsCache.secondary, setPanel, "DamageNumbers"), "Damage numbers: On");
        var shakeBtn = Btn(Inst(prefabsCache.secondary, setPanel, "ScreenShake"), "Screen shake: On");
        var resetBtn = Btn(Inst(prefabsCache.secondary, setPanel, "Reset"), "Reset progress");
        var setClose = Btn(Inst(prefabsCache.primary, setPanel, "Close"), "Close");
        var sp = setRoot.gameObject.AddComponent<SettingsPanel>();
        Set(sp, "musicSlider", music); Set(sp, "sfxSlider", sfx); Set(sp, "fullscreenButton", fsBtn); Set(sp, "resPrev", resPrev); Set(sp, "resNext", resNext);
        Set(sp, "damageNumbersButton", dmgBtn); Set(sp, "shakeButton", shakeBtn);
        Set(sp, "resetButton", resetBtn); Set(sp, "closeButton", setClose); Set(sp, "menu", mm);
        Set(sp, "fullscreenLabel", fsBtn.GetComponentInChildren<TextMeshProUGUI>()); Set(sp, "resolutionLabel", resLbl); Set(sp, "resetLabel", resetBtn.GetComponentInChildren<TextMeshProUGUI>());

        // ---- Upgrades
        var (upRoot, upPanel) = MenuOverlay(canvas, "UpgradesPanel", "UPGRADES", new Vector2(980, 900));
        var upGold = Txt(upPanel, "Gold", "0 gold", fBlack, 28, Amber, TextAlignmentOptions.Center);
        var upList = Node("List", upPanel);
        var ulv = upList.gameObject.AddComponent<VerticalLayoutGroup>(); ulv.spacing = 12;
        ulv.childControlWidth = true; ulv.childControlHeight = true; ulv.childForceExpandWidth = true; ulv.childForceExpandHeight = false;
        var upClose = Btn(Inst(prefabsCache.primary, upPanel, "Close"), "Close");
        var up = upRoot.gameObject.AddComponent<UpgradesPanel>();
        Set(up, "list", upList); Set(up, "goldText", upGold); Set(up, "closeButton", upClose); Set(up, "menu", mm);
        Set(up, "font", fBold); Set(up, "boldFont", fBlack); Set(up, "panelSprite", sPanel); Set(up, "fillSprite", sFill);
        SetArr(up, "icons", new List<Object> { Icon("icon_vitality"), Icon("icon_might"), Icon("icon_swiftness"), Icon("icon_magnet"), Icon("pickup_gold"), Icon("pickup_chest") });

        // ---- Heroes
        var (heRoot, hePanel) = MenuOverlay(canvas, "HeroesPanel", "HEROES", new Vector2(1700, 820));
        var heRow = Row(hePanel, "Columns", 18);
        var cols = new List<Object>();
        for (int i = 0; i < 5; i++)
        {
            var col = Node("Column" + (i + 1), heRow); Img(col, sFill, Palette.Void, 1.5f); Fixed(col, 300, 520);
            var t = Txt(col, "Text", "", fBold, 20, Palette.Text, TextAlignmentOptions.TopLeft); Stretch(t.rectTransform, 18, 18, 18, 18);
            t.textWrappingMode = TextWrappingModes.Normal; t.lineSpacing = 4;
            cols.Add(t);
        }
        var heSum = Txt(hePanel, "Summary", "", fBlack, 24, Palette.TextMuted, TextAlignmentOptions.Center);
        var heClose = Btn(Inst(prefabsCache.primary, hePanel, "Close"), "Close", 300, 84);
        var hp = heRoot.gameObject.AddComponent<HeroesPanel>();
        SetArr(hp, "classes", classesCache.ConvertAll(c => (Object)c)); SetArr(hp, "columns", cols);
        Set(hp, "summary", heSum); Set(hp, "closeButton", heClose); Set(hp, "menu", mm);

        // ---- Unlocks
        var (unRoot, unPanel) = MenuOverlay(canvas, "UnlocksPanel", "UNLOCKS", new Vector2(1640, 900));
        var unRow = Row(unPanel, "Columns", 18);
        var unCols = new List<Object>();
        for (int i = 0; i < 3; i++)
        {
            var col = Node("Column" + (i + 1), unRow); Img(col, sFill, Palette.Void, 1.5f); Fixed(col, 500, 640);
            var t = Txt(col, "Text", "", fBold, 21, Palette.Text, TextAlignmentOptions.TopLeft); Stretch(t.rectTransform, 22, 22, 20, 20);
            t.textWrappingMode = TextWrappingModes.Normal; t.lineSpacing = 6; t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = 21;
            unCols.Add(t);
        }
        var unClose = Btn(Inst(prefabsCache.primary, unPanel, "Close"), "Close", 300, 84);
        var unp = unRoot.gameObject.AddComponent<UnlocksPanel>();
        SetArr(unp, "classes", classesCache.ConvertAll(c => (Object)c)); SetArr(unp, "stages", stagesCache.ConvertAll(c => (Object)c));
        SetArr(unp, "columns", unCols); Set(unp, "closeButton", unClose); Set(unp, "menu", mm);
        Set(mm, "unlocksPanel", unRoot.gameObject); unRoot.gameObject.SetActive(false);

        Set(mm, "settingsPanel", setRoot.gameObject); Set(mm, "upgradesPanel", upRoot.gameObject); Set(mm, "heroesPanel", heRoot.gameObject);
        setRoot.gameObject.SetActive(false); upRoot.gameObject.SetActive(false); heRoot.gameObject.SetActive(false);

        EditorSceneManager.SaveScene(scene, MainMenuPath);
    }

    static Prefabs prefabsCache;

    static (RectTransform root, RectTransform panel) MenuOverlay(Transform canvas, string name, string title, Vector2 size)
    {
        var root = Node(name, canvas); Stretch(root, 0, 0, 0, 0);
        Img(root, null, new Color(0.04f, 0.04f, 0.06f, 0.88f)).raycastTarget = true;
        var panel = Node("Panel", root); Place(panel, Center, Center, Center, Vector2.zero, size); Img(panel, sPanel, Palette.Panel);
        var v = panel.gameObject.AddComponent<VerticalLayoutGroup>(); v.padding = new RectOffset(48, 48, 36, 36); v.spacing = 20; v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        var t = Txt(panel, "Title", title, fTitle, 56, Palette.Accent, TextAlignmentOptions.Center); t.fontSharedMaterial = mTitleGlow;
        return (root, panel);
    }

    static Slider LabeledSlider(Transform parent, string label)
    {
        var row = Node(label + "Row", parent);
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>(); h.spacing = 24; h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        var l = Txt(row, "Label", label, fBlack, 28, Palette.Text); l.gameObject.AddComponent<LayoutElement>().preferredWidth = 240;
        var sl = Node("Slider", row); Fixed(sl, 380, 48);
        // Invisible hit area over the whole slider: a click anywhere on the track jumps the value there (not only by dragging the knob).
        var hit = sl.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); hit.raycastTarget = true;
        var bg = Node("Background", sl); Place(bg, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Center, Vector2.zero, new Vector2(0, 18)); Img(bg, sFill, Palette.Outline, 3f);
        var fa = Node("Fill Area", sl); Place(fa, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Center, Vector2.zero, new Vector2(-20, 18));
        var fill = Node("Fill", fa); Place(fill, Vector2.zero, new Vector2(0, 1), Center, Vector2.zero, new Vector2(10, 0)); Img(fill, sFill, Palette.Accent, 3f);
        var ha = Node("Handle Slide Area", sl); Stretch(ha, 10, 10, 0, 0);
        var handle = Node("Handle", ha); Place(handle, Vector2.zero, new Vector2(0, 1), Center, Vector2.zero, new Vector2(36, 0));
        var hImg = Img(handle, sCircle, Palette.Text); hImg.raycastTarget = true;
        var s = sl.gameObject.AddComponent<Slider>();
        s.fillRect = fill; s.handleRect = handle; s.targetGraphic = hImg; s.direction = Slider.Direction.LeftToRight; s.minValue = 0; s.maxValue = 1; s.value = 0.5f;
        return s;
    }

    static void BuildClassSelect(List<ClassData> classes, Prefabs prefabs)
    {
        var scene = NewScene(Palette.Void);
        var canvas = MakeCanvas("Canvas");
        var bg = Node("Background", canvas); Stretch(bg, 0, 0, 0, 0); Img(bg, null, Palette.Void);

        // Header
        var header = Node("Header", canvas); Place(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(-120, 84));
        var hh = header.gameObject.AddComponent<HorizontalLayoutGroup>(); hh.spacing = 30; hh.childAlignment = TextAnchor.MiddleLeft;
        hh.childControlWidth = true; hh.childControlHeight = true; hh.childForceExpandWidth = false; hh.childForceExpandHeight = false;
        var back = Btn(Inst(prefabs.secondary, header, "BackButton"), "Back", 165, 72);
        var title = Txt(header, "Title", "Choose your hero", fTitle, 51, Palette.Text);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        var spacer = Node("Spacer", header); spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var stPrev = Btn(Inst(prefabs.secondary, header, "StagePrev"), "<", 72, 72);
        var stBox = Node("Stage", header); Img(stBox, sPanel, Palette.Panel);
        stBox.gameObject.AddComponent<LayoutElement>().preferredWidth = 470;
        var stv = stBox.gameObject.AddComponent<VerticalLayoutGroup>(); stv.padding = new RectOffset(24, 24, 8, 8); stv.spacing = 0;
        stv.childControlWidth = true; stv.childControlHeight = true; stv.childForceExpandWidth = true; stv.childForceExpandHeight = false;
        var stName = Txt(stBox, "Name", "Stage 1 · Ashen Fields", fBlack, 24, Palette.Text, TextAlignmentOptions.Center);
        var stInfo = Txt(stBox, "Info", "", fBold, 17, Palette.TextMuted, TextAlignmentOptions.Center);
        var stNext = Btn(Inst(prefabs.secondary, header, "StageNext"), ">", 72, 72);
        foreach (var b in new[] { stPrev, stNext })
        { var l = b.GetComponentInChildren<TextMeshProUGUI>(); l.alignment = TextAlignmentOptions.Center; Stretch(l.rectTransform, 0, 0, 0, 0); }

        // Body: class list | hero panel | paths
        var body = Node("Body", canvas); Stretch(body, 60, 60, 162, 54);
        var bh = body.gameObject.AddComponent<HorizontalLayoutGroup>(); bh.spacing = 36;
        bh.childControlWidth = true; bh.childControlHeight = true; bh.childForceExpandWidth = false; bh.childForceExpandHeight = true;

        var classList = Node("ClassList", body);
        classList.gameObject.AddComponent<LayoutElement>().preferredWidth = 300;
        var cv = classList.gameObject.AddComponent<VerticalLayoutGroup>(); cv.spacing = 15;
        cv.childControlWidth = true; cv.childControlHeight = true; cv.childForceExpandWidth = true; cv.childForceExpandHeight = false;

        var hero = Node("HeroPanel", body); Img(hero, sPanel, Palette.Panel);
        hero.gameObject.AddComponent<LayoutElement>().preferredWidth = 495;
        var hv = hero.gameObject.AddComponent<VerticalLayoutGroup>(); hv.padding = new RectOffset(36, 36, 36, 36); hv.spacing = 18; hv.childAlignment = TextAnchor.UpperCenter;
        hv.childControlWidth = true; hv.childControlHeight = true; hv.childForceExpandWidth = true; hv.childForceExpandHeight = false;
        var portraitWrap = Node("PortraitWrap", hero); portraitWrap.gameObject.AddComponent<LayoutElement>().preferredHeight = 300;
        var pbg = Node("PortraitBg", portraitWrap); Place(pbg, Center, Center, Center, Vector2.zero, new Vector2(300, 300)); Img(pbg, sCircle, Palette.Ground);
        var portrait = Node("Portrait", pbg); Place(portrait, Center, Center, Center, new Vector2(0, -6), new Vector2(240, 240));
        var portraitImg = Img(portrait, sCircle, Color.white);
        portraitImg.preserveAspect = true;
        var cname = Txt(hero, "ClassName", "Mage", fTitle, 45, Palette.Text, TextAlignmentOptions.Center);
        var role = Txt(hero, "Role", "Role", fBold, 22, Palette.TextMuted, TextAlignmentOptions.Center);
        var mech = InfoBox(hero, "MechanicBox", "CLASS MECHANIC");
        var weap = InfoBox(hero, "WeaponBox", "DEFAULT WEAPON");

        var right = Node("PathColumn", body); right.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var rv = right.gameObject.AddComponent<VerticalLayoutGroup>(); rv.spacing = 21;
        rv.childControlWidth = true; rv.childControlHeight = true; rv.childForceExpandWidth = true; rv.childForceExpandHeight = false;
        var lbl = Txt(right, "Label", "CHOOSE A PATH", fBlack, 21, Palette.TextMuted); lbl.characterSpacing = 6;
        var grid = Node("PathGrid", right);
        grid.gameObject.AddComponent<LayoutElement>().preferredHeight = 410;
        var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(298, 196); gl.spacing = new Vector2(18, 18);
        gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;
        var fill = Node("Spacer", right); fill.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;

        var bar = Node("BottomBar", right); Img(bar, sPanel, Palette.Panel);
        var barH = bar.gameObject.AddComponent<HorizontalLayoutGroup>(); barH.padding = new RectOffset(30, 24, 21, 21); barH.spacing = 24; barH.childAlignment = TextAnchor.MiddleLeft;
        barH.childControlWidth = true; barH.childControlHeight = true; barH.childForceExpandWidth = false; barH.childForceExpandHeight = false;
        var ascCol = Node("Ascension", bar); ascCol.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var av = ascCol.gameObject.AddComponent<VerticalLayoutGroup>(); av.spacing = 4; av.childControlWidth = true; av.childControlHeight = true; av.childForceExpandHeight = false;
        var ascLbl = Txt(ascCol, "Label", "ASCENSION AT MAX LEVEL", fBlack, 18, Palette.TextMuted); ascLbl.characterSpacing = 6;
        var ascText = Txt(ascCol, "Text", "Ascension", fBlack, 24, Palette.Text);
        var start = Btn(Inst(prefabs.primary, bar, "StartButton"), "Start run", 300, 84);

        var screen = canvas.gameObject.AddComponent<ClassSelectScreen>();
        SetArr(screen, "classes", classes.ConvertAll(c => (Object)c));
        Set(screen, "classList", classList); Set(screen, "classButtonPrefab", prefabs.classButton.GetComponent<Button>());
        Set(screen, "pathGrid", grid); Set(screen, "pathCardPrefab", prefabs.pathCard.GetComponent<PathCard>());
        Set(screen, "portrait", portraitImg);
        Set(screen, "nameText", cname); Set(screen, "roleText", role); Set(screen, "mechanicText", mech); Set(screen, "weaponText", weap);
        Set(screen, "ascensionText", ascText); Set(screen, "startLabel", start.GetComponentInChildren<TextMeshProUGUI>());
        Set(screen, "startButton", start); Set(screen, "backButton", back);
        SetArr(screen, "stages", stagesCache.ConvertAll(x => (Object)x));
        Set(screen, "stagePrev", stPrev); Set(screen, "stageNext", stNext); Set(screen, "stageName", stName); Set(screen, "stageInfo", stInfo);

        EditorSceneManager.SaveScene(scene, ClassSelectPath);
    }

    static void BuildGame(Prefabs prefabs, Gameplay gp, Content content)
    {
        var scene = NewScene(Palette.Ground);
        var cam = Camera.main;
        cam.orthographicSize = 5f;
        var follow = cam.gameObject.AddComponent<CameraFollow>();

        // World
        var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(gp.player);
        follow.target = playerGo.transform;
        SpriteRenderer groundR = null;
        var rocks = new GameObject("Decor Rocks").AddComponent<DecorScatter>();
        Set(rocks, "prefab", gp.rock.GetComponent<SpriteRenderer>());
        if (groundTile != null)
        {
            var g = Spr(null, "Ground", groundTile, Color.white, -40); groundR = g;
            g.drawMode = SpriteDrawMode.Tiled;
            float tile = groundTile.rect.width / groundTile.pixelsPerUnit;
            g.size = new Vector2(tile * 9, tile * 6);
            var tiler = g.gameObject.AddComponent<GroundTiler>();
            var tso = new SerializedObject(tiler); tso.FindProperty("tileSize").floatValue = tile; tso.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(rocks.gameObject); // the painted ground already has pebbles
        }
        else
        {
            var patches = new GameObject("Decor Ground Patches").AddComponent<DecorScatter>();
            Set(patches, "prefab", gp.patch.GetComponent<SpriteRenderer>());
            var pso = new SerializedObject(patches);
            pso.FindProperty("count").intValue = 26; pso.FindProperty("seed").intValue = 2;
            pso.FindProperty("scaleRange").vector2Value = new Vector2(3f, 6f);
            pso.ApplyModifiedPropertiesWithoutUndo();
        }
        var spawner = new GameObject("Enemy Spawner").AddComponent<EnemySpawner>();
        Set(spawner, "enemyPrefab", gp.enemy.GetComponent<Enemy>());
        var sso = new SerializedObject(spawner);
        var bossProp = sso.FindProperty("bosses");
        float[] bossScale = { 1.9f, 2.0f, 2.2f };
        for (int i = 0; i < bossProp.arraySize && i < 3; i++)
        {
            if (!art.TryGetValue("Boss" + i, out var bossArt)) continue;
            var b = bossProp.GetArrayElementAtIndex(i);
            b.FindPropertyRelative("sprite").objectReferenceValue = bossArt;
            b.FindPropertyRelative("tint").colorValue = Color.white;
            b.FindPropertyRelative("scale").floatValue = bossScale[i];
        }
        sso.ApplyModifiedPropertiesWithoutUndo();

        var canvas = MakeCanvas("HUD Canvas", 10);
        var vig = Node("HurtVignette", canvas); Stretch(vig, 0, 0, 0, 0);
        var vigImg = Img(vig, ImportSprite("ui_vignette.png", 0), new Color(1, 1, 1, 0));

        // XP bar
        var xp = Node("XPBar", canvas); Place(xp, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 24)); Img(xp, null, Palette.Outline);
        var xpFill = Node("Fill", xp); Stretch(xpFill, 0, 0, 0, 0);
        var xpFillImg = FilledImg(xpFill, Palette.Accent);

        // Player panel (top-left)
        var player = Node("PlayerPanel", canvas); Place(player, TopLeft, TopLeft, TopLeft, new Vector2(30, -45), new Vector2(390, 108));
        Img(player, sPanel, new Color(0.11f, 0.106f, 0.133f, 0.9f));
        var badge = Node("LevelBadge", player); Place(badge, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(15, 0), new Vector2(72, 72));
        var badgeImg = Img(badge, sCircle, Hex("#8E6BEF"));
        var lvl = Txt(badge, "Level", "1", fBlack, 27, Palette.Outline, TextAlignmentOptions.Center); Stretch(lvl.rectTransform, 0, 0, 0, 0);
        var heroText = Txt(player, "Hero", "Mage · Fire", fBlack, 21, Palette.Text);
        Place(heroText.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(105, -12), new Vector2(270, 30));
        var hpBg = Node("HPBar", player); Place(hpBg, TopLeft, TopLeft, TopLeft, new Vector2(105, -45), new Vector2(270, 21)); Img(hpBg, sFill, Palette.Outline, 3f);
        var hpFill = Node("Fill", hpBg); Stretch(hpFill, 3, 3, 3, 3);
        var hpFillImg = FilledImg(hpFill, Palette.Health);
        var hpText = Txt(player, "HPText", "120 / 120 HP", fBold, 18, Palette.TextMuted);
        Place(hpText.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(105, -70), new Vector2(270, 26));

        // Timer + boss warning (top-center)
        var timer = Txt(canvas, "Timer", "00:00", fTitle, 60, Palette.Text, TextAlignmentOptions.Center);
        timer.fontSharedMaterial = mTitleGlow;
        Place(timer.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(400, 72));
        var boss = Node("BossWarning", canvas); Place(boss, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(560, 54));
        Img(boss, sPanel, new Color(0.35f, 0.12f, 0.14f, 0.95f));
        var bossRing = Node("Ring", boss); Stretch(bossRing, 0, 0, 0, 0); Img(bossRing, sRing, Palette.Health);
        var bossText = Txt(boss, "Text", "The Ash Warden arrives in 0:18", fBlack, 22, Hex("#FFB3B5"), TextAlignmentOptions.Center);
        Stretch(bossText.rectTransform, 12, 12, 0, 0);

        // Boss health bar
        var bb = Node("BossBar", canvas); Place(bb, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -116), new Vector2(900, 70));
        Img(bb, sPanel, new Color(0.11f, 0.106f, 0.133f, 0.95f));
        var bbName = Txt(bb, "Name", "The Ash Warden", fTitle, 26, Hex("#FFB3B5"), TextAlignmentOptions.Center);
        Place(bbName.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(-24, 34));
        var bbBg = Node("Bar", bb); Place(bbBg, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(-36, 20)); Img(bbBg, sFill, Palette.Outline, 3f);
        var bbFill = Node("Fill", bbBg); Stretch(bbFill, 3, 3, 3, 3);
        var bbFillImg = FilledImg(bbFill, Palette.Health); bbFillImg.fillAmount = 1f;

        // Stats + pause (top-right)
        var pauseBtn = Btn(Inst(prefabs.secondary, canvas, "PauseButton"), "", 78, 78);
        Place((RectTransform)pauseBtn.transform, TopRight, TopRight, TopRight, new Vector2(-30, -45), new Vector2(78, 78));
        foreach (var x in new[] { -10f, 10f })
        {
            var bar = Node("Bar", pauseBtn.transform); Place(bar, Center, Center, Center, new Vector2(x, 0), new Vector2(9, 30)); Img(bar, sSquare, Palette.Text);
        }
        var stats = Node("Stats", canvas); Place(stats, TopRight, TopRight, TopRight, new Vector2(-126, -45), new Vector2(260, 84));
        Img(stats, sPanel, new Color(0.11f, 0.106f, 0.133f, 0.9f));
        var kills = Txt(stats, "Kills", "0 kills", fBlack, 22, Palette.Text, TextAlignmentOptions.MidlineRight);
        Place(kills.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(-42, 32));
        var gold = Txt(stats, "Gold", "0 gold", fBlack, 22, Amber, TextAlignmentOptions.MidlineRight);
        Place(gold.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(-42, 32));

        // x2 speed (shown after the first run)
        var speedBtn = Btn(Inst(prefabs.secondary, canvas, "SpeedButton"), "x1 speed", 210, 66);
        Place((RectTransform)speedBtn.transform, TopRight, TopRight, TopRight, new Vector2(-30, -144), new Vector2(210, 66));
        var speedLbl = speedBtn.GetComponentInChildren<TextMeshProUGUI>(); speedLbl.fontSize = 26;
        speedLbl.alignment = TextAlignmentOptions.Center; Stretch(speedLbl.rectTransform, 0, 0, 0, 0);

        // Off-screen pickup trackers
        var tracker = Node("PickupTrackers", canvas); Stretch(tracker, 0, 0, 0, 0);
        var trk = tracker.gameObject.AddComponent<PickupTracker>();
        Set(trk, "container", tracker); Set(trk, "backgroundSprite", sCircle); Set(trk, "arrowSprite", ImportSprite("ui_arrow.png", 0));

        // Loadout (bottom-left)
        var loadout = Node("Loadout", canvas); Place(loadout, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 30), new Vector2(10, 10));
        Img(loadout, sPanel, new Color(0.11f, 0.106f, 0.133f, 0.9f));
        var lf = loadout.gameObject.AddComponent<ContentSizeFitter>(); lf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; lf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var lh = loadout.gameObject.AddComponent<HorizontalLayoutGroup>(); lh.padding = new RectOffset(21, 21, 18, 18); lh.spacing = 21; lh.childAlignment = TextAnchor.LowerLeft;
        lh.childControlWidth = true; lh.childControlHeight = true; lh.childForceExpandWidth = false; lh.childForceExpandHeight = false;

        var pathCol = Node("PathSkill", loadout);
        var pcv = pathCol.gameObject.AddComponent<VerticalLayoutGroup>(); pcv.spacing = 6; pcv.childAlignment = TextAnchor.UpperCenter; pcv.childControlWidth = true; pcv.childControlHeight = true; pcv.childForceExpandWidth = false; pcv.childForceExpandHeight = false;
        var pathSlot = MakeSlot(pathCol, "PathSlot", 114, 114, 5, Palette.Accent);
        var pathLabel = Txt(pathCol, "SkillName", "Fireball", fBlack, 17, Palette.Gold, TextAlignmentOptions.Center);

        var slotsCol = Node("Slots", loadout);
        var scv = slotsCol.gameObject.AddComponent<VerticalLayoutGroup>(); scv.spacing = 12; scv.childControlWidth = true; scv.childControlHeight = true; scv.childForceExpandWidth = false; scv.childForceExpandHeight = false;
        var wRow = Row(slotsCol, "Weapons", 12);
        var pRow = Row(slotsCol, "Passives", 12);
        var weapons = new List<Object>(); var passives = new List<Object>();
        for (int i = 0; i < 5; i++) weapons.Add(MakeSlot(wRow, "Weapon" + (i + 1), 78, 78, 5, Palette.Health));
        for (int i = 0; i < 5; i++) passives.Add(MakeSlot(pRow, "Passive" + (i + 1), 78, 60, 5, Palette.XPGem));

        // Level-up overlay
        var lvlUp = Node("LevelUpScreen", canvas); Stretch(lvlUp, 0, 0, 0, 0);
        var dim = Img(lvlUp, null, new Color(0.04f, 0.04f, 0.06f, 0.85f)); dim.raycastTarget = true;
        var luv = lvlUp.gameObject.AddComponent<VerticalLayoutGroup>(); luv.padding = new RectOffset(0, 0, 78, 0); luv.spacing = 30; luv.childAlignment = TextAnchor.UpperCenter;
        luv.childControlWidth = true; luv.childControlHeight = true; luv.childForceExpandWidth = false; luv.childForceExpandHeight = false;
        var luTitle = Txt(lvlUp, "Title", "LEVEL UP", fTitle, 84, Palette.Accent, TextAlignmentOptions.Center); luTitle.fontSharedMaterial = mTitleGlow;
        var luSub = Txt(lvlUp, "Subtitle", "Level 15 · choose one", fBold, 27, Palette.TextMuted, TextAlignmentOptions.Center);
        var cardRow = Row(lvlUp, "CardRow", 42);
        var cards = new List<Object>();
        for (int i = 0; i < 3; i++) cards.Add(Inst(prefabs.upgradeCard, cardRow, "UpgradeCard" + (i + 1)).GetComponent<UpgradeCard>());
        var luBtns = Row(lvlUp, "Buttons", 21);
        var rerollB = Btn(Inst(prefabs.secondary, luBtns, "Reroll"), "Reroll (2 left)", 300, 78);
        var banishB = Btn(Inst(prefabs.secondary, luBtns, "Banish"), "Banish (1 left)", 300, 78);
        var levelUp = lvlUp.gameObject.AddComponent<LevelUpScreen>();
        SetArr(levelUp, "cards", cards); Set(levelUp, "subtitle", luSub);
        Set(levelUp, "rerollButton", rerollB); Set(levelUp, "banishButton", banishB);
        Set(levelUp, "rerollLabel", rerollB.GetComponentInChildren<TextMeshProUGUI>()); Set(levelUp, "banishLabel", banishB.GetComponentInChildren<TextMeshProUGUI>());

        // Pause overlay
        var pause = Node("PauseOverlay", canvas); Stretch(pause, 0, 0, 0, 0);
        Img(pause, null, new Color(0.04f, 0.04f, 0.06f, 0.8f)).raycastTarget = true;
        var pp = Node("Panel", pause); Place(pp, Center, Center, Center, Vector2.zero, new Vector2(1500, 960)); Img(pp, sPanel, Palette.Panel);
        var ppv = pp.gameObject.AddComponent<VerticalLayoutGroup>(); ppv.padding = new RectOffset(42, 42, 30, 36); ppv.spacing = 16; ppv.childAlignment = TextAnchor.UpperCenter;
        ppv.childControlWidth = true; ppv.childControlHeight = true; ppv.childForceExpandWidth = true; ppv.childForceExpandHeight = false;
        var pTitle = Txt(pp, "Title", "PAUSED", fTitle, 60, Palette.Accent, TextAlignmentOptions.Center); pTitle.fontSharedMaterial = mTitleGlow;
        var pStats = Txt(pp, "Stats", "Level 1", fBold, 22, Palette.TextMuted, TextAlignmentOptions.Center);
        var lbl = Txt(pp, "LoadoutLabel", "YOUR BUILD", fBlack, 19, Palette.TextMuted); lbl.characterSpacing = 6;
        foreach (var (t, mh) in new[] { (pTitle, 78f), (pStats, 32f), (lbl, 26f) }) t.gameObject.AddComponent<LayoutElement>().minHeight = mh;
        var grid = Node("Loadout", pp); { var gle = grid.gameObject.AddComponent<LayoutElement>(); gle.minHeight = gle.preferredHeight = 336; }
        var pgl = grid.gameObject.AddComponent<GridLayoutGroup>();
        pgl.cellSize = new Vector2(340, 100); pgl.spacing = new Vector2(12, 12); pgl.childAlignment = TextAnchor.UpperCenter;
        pgl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; pgl.constraintCount = 4;
        var musicS = LabeledSlider(pp, "Music");
        var sfxS = LabeledSlider(pp, "Sound effects");
        var pBtnRow = Row(pp, "Buttons", 24);
        var resume = Btn(Inst(prefabs.primary, pBtnRow, "Resume"), "Resume", 340, 84);
        var restart = Btn(Inst(prefabs.secondary, pBtnRow, "Restart"), "Restart run", 340, 84);
        var toMenu = Btn(Inst(prefabs.secondary, pBtnRow, "MainMenu"), "Main menu", 340, 84);
        foreach (var b in new[] { resume, restart, toMenu }) { var le = b.GetComponent<LayoutElement>(); le.minHeight = 84; le.minWidth = 340; }
        pp.sizeDelta = new Vector2(1480, 900);

        // Components
        var hud = canvas.gameObject.AddComponent<HUD>();
        Set(hud, "xpFill", xpFillImg); Set(hud, "hpFill", hpFillImg);
        Set(hud, "levelText", lvl); Set(hud, "hpText", hpText); Set(hud, "timerText", timer); Set(hud, "killsText", kills); Set(hud, "goldText", gold); Set(hud, "heroText", heroText);
        Set(hud, "levelBadge", badgeImg); Set(hud, "bossWarning", boss.gameObject); Set(hud, "bossWarningText", bossText);
        Set(hud, "pathSlot", pathSlot); Set(hud, "pathSlotLabel", pathLabel);
        SetArr(hud, "weaponSlots", weapons); SetArr(hud, "passiveSlots", passives);
        Set(hud, "speedButton", speedBtn); Set(hud, "speedLabel", speedLbl);
        Set(hud, "bossBar", bb.gameObject); Set(hud, "bossBarFill", bbFillImg); Set(hud, "bossBarName", bbName);

        var pm = canvas.gameObject.AddComponent<PauseMenu>();
        Set(pm, "overlay", pause.gameObject); Set(pm, "levelUpScreen", lvlUp.gameObject);
        Set(pm, "loadoutGrid", grid); Set(pm, "font", fBold); Set(pm, "boldFont", fBlack); Set(pm, "cardSprite", sPanel); Set(pm, "ringSprite", sRing);
        Set(pm, "statsText", pStats); Set(pm, "musicSlider", musicS); Set(pm, "sfxSlider", sfxS); Set(pm, "restartButton", restart);
        Set(pm, "pauseButton", pauseBtn); Set(pm, "resumeButton", resume); Set(pm, "menuButton", toMenu);

        // Game over overlay
        var over = Node("GameOverScreen", canvas); Stretch(over, 0, 0, 0, 0);
        Img(over, null, new Color(0.04f, 0.04f, 0.06f, 0.88f)).raycastTarget = true;
        var op = Node("Panel", over); Place(op, Center, Center, Center, Vector2.zero, new Vector2(760, 600)); Img(op, sPanel, Palette.Panel);
        var opv = op.gameObject.AddComponent<VerticalLayoutGroup>(); opv.padding = new RectOffset(48, 48, 45, 45); opv.spacing = 24; opv.childAlignment = TextAnchor.UpperCenter;
        opv.childControlWidth = true; opv.childControlHeight = true; opv.childForceExpandWidth = true; opv.childForceExpandHeight = false;
        var overTitle = Txt(op, "Title", "YOU FELL", fTitle, 72, Palette.Health, TextAlignmentOptions.Center); overTitle.fontSharedMaterial = mTitleGlow;
        var overStats = Txt(op, "Stats", "Survived 00:00", fBold, 27, Palette.Text, TextAlignmentOptions.Center); overStats.lineSpacing = 20;
        var overRewards = Txt(op, "Rewards", "", fBlack, 24, Palette.Gold, TextAlignmentOptions.Center);
        var retry = Btn(Inst(prefabs.primary, op, "Retry"), "Try again");
        var overMenu = Btn(Inst(prefabs.secondary, op, "MainMenu"), "Main menu");
        var gos = over.gameObject.AddComponent<GameOverScreen>();
        Set(gos, "title", overTitle); Set(gos, "stats", overStats); Set(gos, "retryButton", retry); Set(gos, "menuButton", overMenu); Set(gos, "rewards", overRewards);
        Set(pm, "gameOverScreen", over.gameObject);

        // Game over icon + colored frame (crown shard on victory, skull on defeat)
        var overIcon = Node("Icon", op); overIcon.SetSiblingIndex(0); Fixed(overIcon, 110, 110);
        var overIconImg = Img(overIcon, Icon("pickup_crown_shard"), Color.white); overIconImg.preserveAspect = true;
        var overRing = Node("Ring", op); Stretch(overRing, -4, -4, -4, -4); overRing.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var overRingImg = Img(overRing, sRing, Palette.Gold);
        op.sizeDelta = new Vector2(760, 700);
        Set(gos, "icon", overIconImg); Set(gos, "frameRing", overRingImg);
        Set(gos, "winIcon", Icon("pickup_crown_shard")); Set(gos, "loseIcon", Icon("icon_skull"));

        // Boss intro card (slides across)
        var intro = Node("BossIntro", canvas); Place(intro, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Center, new Vector2(0, 170), new Vector2(1300, 190));
        Img(intro, sPanel, new Color(0.12f, 0.07f, 0.09f, 0.95f));
        var introRing = Node("Ring", intro); Stretch(introRing, -3, -3, -3, -3); Img(introRing, sRing, Palette.Health);
        var introLbl = Txt(intro, "Label", "BOSS", fBlack, 24, Hex("#FFB3B5"), TextAlignmentOptions.Center); introLbl.characterSpacing = 18;
        Place(introLbl.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(0, 32));
        var introName = Txt(intro, "Name", "The Ash Warden", fTitle, 66, Palette.Text, TextAlignmentOptions.Center); introName.fontSharedMaterial = mTitleGlow;
        Place(introName.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Center, new Vector2(0, -4), new Vector2(-40, 84));
        var introTitle = Txt(intro, "Title", "Keeper of the Ember Gate", fBold, 26, Palette.TextMuted, TextAlignmentOptions.Center);
        Place(introTitle.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(0, 34));
        Set(hud, "bossIntro", intro); Set(hud, "bossIntroName", introName); Set(hud, "bossIntroTitle", introTitle);
        Set(hud, "hurtVignette", vigImg);

        // Boss chest opening
        var chestRoot = Node("ChestPopup", canvas); Stretch(chestRoot, 0, 0, 0, 0);
        Img(chestRoot, null, new Color(0.04f, 0.04f, 0.06f, 0.82f)).raycastTarget = true;
        var chestGroup = chestRoot.gameObject.AddComponent<CanvasGroup>();
        var chestGlow = Node("Glow", chestRoot); Place(chestGlow, Center, Center, Center, new Vector2(0, 40), new Vector2(720, 720));
        Img(chestGlow, sGlow, new Color(1f, 0.85f, 0.35f, 0.75f));
        var chestImg = Node("Chest", chestRoot); Place(chestImg, Center, Center, Center, new Vector2(0, 40), new Vector2(300, 300));
        Img(chestImg, Icon("pickup_chest"), Color.white).preserveAspect = true;
        var chestTitle = Txt(chestRoot, "Title", "TREASURE!", fTitle, 72, Palette.Gold, TextAlignmentOptions.Center); chestTitle.fontSharedMaterial = mTitleGlow;
        Place(chestTitle.rectTransform, Center, Center, Center, new Vector2(0, 300), new Vector2(1000, 100));
        var chestReward = Txt(chestRoot, "Reward", "+25 gold", fBlack, 40, Palette.Text, TextAlignmentOptions.Center);
        Place(chestReward.rectTransform, Center, Center, Center, new Vector2(0, -230), new Vector2(1000, 120));
        var chest = chestRoot.gameObject.AddComponent<ChestPopup>();
        Set(chest, "group", chestGroup); Set(chest, "chest", chestImg); Set(chest, "glow", chestGlow); Set(chest, "title", chestTitle); Set(chest, "reward", chestReward);
        Set(hud, "chestPopup", chest); Set(pm, "chestPopup", chestRoot.gameObject);

        // First-run tips (last = drawn above every overlay)
        var tip = Node("TipBanner", canvas); Place(tip, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(200, 14), new Vector2(1000, 76));
        Img(tip, sPanel, new Color(0.11f, 0.106f, 0.133f, 0.96f));
        var tipRing = Node("Ring", tip); Stretch(tipRing, -2, -2, -2, -2); Img(tipRing, sRing, Palette.Accent);
        tip.gameObject.AddComponent<CanvasGroup>();
        var tipText = Txt(tip, "Text", "Tip", fBlack, 23, Palette.Text, TextAlignmentOptions.Center); Stretch(tipText.rectTransform, 24, 24, 6, 6);
        tipText.enableAutoSizing = true; tipText.fontSizeMin = 14; tipText.fontSizeMax = 23;
        Set(hud, "tipBanner", tip); Set(hud, "tipText", tipText);

        // Run manager
        var run = new GameObject("Run Manager").AddComponent<RunManager>();
        Set(run, "player", playerGo.GetComponent<PlayerController>()); Set(run, "spawner", spawner);
        Set(run, "hud", hud); Set(run, "levelUp", levelUp); Set(run, "gameOver", gos);
        Set(run, "projectilePrefab", gp.projectile.GetComponent<Projectile>()); Set(run, "gemPrefab", gp.gem.GetComponent<XPGem>());
        Set(run, "vfxPrefab", gp.vfx.GetComponent<Vfx>()); Set(run, "orbPrefab", gp.orb.GetComponent<SpriteRenderer>());
        Set(run, "fallbackPathWeapon", content.fallback);
        SetArr(run, "weaponPool", content.weaponPool); SetArr(run, "passivePool", content.passives);
        if (groundR != null) Set(run, "ground", groundR);
        Set(run, "pickupPrefab", gp.pickup.GetComponent<Pickup>());
        Set(run, "damageNumberPrefab", gp.damageNumber.GetComponent<DamageNumber>());
        Set(run, "deathPopPrefab", gp.deathPop.GetComponent<DeathPop>());
        SetArr(run, "pickupSprites", new List<Object> { Icon("pickup_meat"), Icon("pickup_medkit"), Icon("icon_magnet"), Icon("pickup_gold_bag"), Icon("pickup_chest") });
        SetArr(run, "classes", classesCache.ConvertAll(c => (Object)c));
        SetArr(run, "stages", stagesCache.ConvertAll(c => (Object)c));
        var rso = new SerializedObject(run);
        var fxp = rso.FindProperty("fxArt");
        foreach (var (field, file) in new[] {
            ("lightning", "fx_lightning"), ("blackHole", "fx_black_hole"), ("fallingStar", "fx_falling_star"), ("iceSpikes", "fx_ice_spikes"),
            ("tornado", "fx_tornado"), ("ghostDagger", "fx_ghost_dagger"), ("frostBurst", "fx_frost_burst"), ("starBurst", "fx_star_burst"),
            ("wave", "fx_wave"), ("waveRing", "fx_water_ring"), ("crater", "fx_crater"), ("fireBurst", "fx_fire_burst"), ("goldenArrow", "fx_golden_arrow"),
            ("firePatch", "fx_fire_patch"), ("poison", "fx_poison"), ("bossBullet", "fx_boss_bullet"), ("arenaRing", "fx_arena_ring") })
            fxp.FindPropertyRelative(field).objectReferenceValue = Fresh(FxArtFile(file));
        rso.ApplyModifiedPropertiesWithoutUndo();

        lvlUp.gameObject.SetActive(false);
        pause.gameObject.SetActive(false);
        over.gameObject.SetActive(false);
        EditorSceneManager.SaveScene(scene, GamePath);
    }

    // Placeholder world art drawn as UI behind the HUD, until real sprites and gameplay exist.
    static void BuildPlaceholderWorld()
    {
        var world = MakeCanvas("Placeholder World (delete when gameplay exists)", -10);
        world.GetComponent<GraphicRaycaster>().enabled = false;
        var rnd = new System.Random(7);
        float R() => (float)rnd.NextDouble();

        for (int i = 0; i < 7; i++)
        {
            var rock = Node("Rock", world);
            Place(rock, Center, Center, Center, new Vector2(R() * 1700 - 850, R() * 860 - 430), new Vector2(60 + R() * 50, 40 + R() * 30));
            Img(rock, sPanel, Palette.Rock, 2f);
        }
        for (int i = 0; i < 16; i++)
        {
            var gem = Node("XPGem", world);
            Place(gem, Center, Center, Center, new Vector2(R() * 1200 - 600, R() * 700 - 350), new Vector2(18, 18));
            gem.localRotation = Quaternion.Euler(0, 0, 45);
            Img(gem, sFill, Palette.XPGem, 4f);
        }
        for (int i = 0; i < 34; i++)
        {
            float a = i / 34f * Mathf.PI * 2 + R() * 0.3f;
            float r = 230 + R() * 480;
            bool big = i % 9 == 0;
            float s = big ? 84 : 51 + R() * 12;
            Hollow(world, new Vector2(Mathf.Cos(a) * r * 1.35f + 960 - s / 2, Mathf.Sin(a) * r * 0.75f + 540 - s / 2), new Vector2(s, s * 1.15f));
        }
        // Hero (placeholder): body, head, hood
        var body = Node("Hero", world); Place(body, Center, Center, Center, new Vector2(0, -15), new Vector2(90, 87)); Img(body, sPanel, Hex("#8E6BEF"), 1.6f);
        var head = Node("Head", world); Place(head, Center, Center, Center, new Vector2(0, 45), new Vector2(72, 72)); Img(head, sCircle, Hex("#E8B48A"));
        foreach (var (x, y) in new[] { (-90f, 45f), (105f, 90f), (30f, -120f) })
        {
            var orb = Node("Orb", world); Place(orb, Center, Center, Center, new Vector2(x, y), new Vector2(39, 39)); Img(orb, sCircle, Hex("#FF6B35"));
            Ember(world, new Vector2(960 + x, 540 + y), 70, Hex("#FF6B35"));
        }
    }

    // ------------------------------------------------------------------ small builders

    static void Hollow(Transform parent, Vector2 bottomLeft, Vector2 size)
    {
        var e = Node("Hollow", parent); Place(e, Vector2.zero, Vector2.zero, Vector2.zero, bottomLeft, size);
        Img(e, sPanel, Palette.Hollow, size.x < 80 ? 2f : 1.4f);
        float eye = Mathf.Max(8, size.x / 6.5f);
        foreach (var fx in new[] { 0.3f, 0.66f })
        {
            var g = Node("EyeGlow", e); Place(g, new Vector2(fx, 0.68f), new Vector2(fx, 0.68f), Center, Vector2.zero, Vector2.one * eye * 3f);
            Img(g, sGlow, new Color(1f, 0.54f, 0.24f, 0.6f));
            var d = Node("Eye", e); Place(d, new Vector2(fx, 0.68f), new Vector2(fx, 0.68f), Center, Vector2.zero, Vector2.one * eye);
            Img(d, sCircle, Palette.Ember);
        }
    }

    static void Ember(Transform parent, Vector2 bottomLeftPos, float size, Color? color = null)
    {
        var c = color ?? Palette.Ember;
        var g = Node("EmberGlow", parent); Place(g, Vector2.zero, Vector2.zero, Center, bottomLeftPos, Vector2.one * size * 3f);
        Img(g, sGlow, new Color(c.r, c.g, c.b, 0.45f));
        var d = Node("Ember", parent); Place(d, Vector2.zero, Vector2.zero, Center, bottomLeftPos, Vector2.one * size * 0.5f);
        Img(d, sCircle, c);
    }

    static TextMeshProUGUI StatPill(Transform parent, string name, string label, string value, Color valueColor)
    {
        var pill = Node(name, parent); Img(pill, sPanel, Palette.Panel);
        var h = pill.gameObject.AddComponent<HorizontalLayoutGroup>(); h.padding = new RectOffset(27, 27, 15, 15); h.spacing = 15; h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        Txt(pill, "Label", label, fBlack, 22, Palette.TextMuted);
        return Txt(pill, "Value", value, fBlack, 30, valueColor);
    }

    static TextMeshProUGUI InfoBox(Transform parent, string name, string label)
    {
        var box = Node(name, parent); Img(box, sFill, Palette.Void, 1.5f);
        var v = box.gameObject.AddComponent<VerticalLayoutGroup>(); v.padding = new RectOffset(21, 21, 18, 18); v.spacing = 6;
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        var l = Txt(box, "Label", label, fBlack, 18, Palette.TextMuted); l.characterSpacing = 6;
        return Txt(box, "Value", "", fBold, 22, Palette.Text);
    }

    static SkillSlot MakeSlot(Transform parent, string name, float w, float h, int pipCount, Color typeColor)
    {
        var root = Node(name, parent);
        var v = root.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = 6; v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = false; v.childForceExpandHeight = false;
        var frame = Node("Frame", root); Fixed(frame, w, h);
        var frameImg = Img(frame, sPanel, Palette.Panel, w < 100 ? 1.8f : 1.4f);
        var icon = Node("Icon", frame); Place(icon, Center, Center, Center, Vector2.zero, Vector2.one * Mathf.Min(w, h) * 0.45f);
        var iconImg = Img(icon, sCircle, Color.white);
        var pipsRow = Row(root, "Pips", 3);
        var pips = new List<Object>();
        for (int i = 0; i < pipCount; i++) { var p = Node("Pip", pipsRow); Fixed(p, w > 100 ? 18 : 12, 7); pips.Add(Img(p, sFill, Palette.Rock, 6f)); }
        var slot = root.gameObject.AddComponent<SkillSlot>();
        Set(slot, "frame", frameImg); Set(slot, "icon", iconImg); SetArr(slot, "pips", pips);
        var border = Node("Border", frame); Stretch(border, -3, -3, -3, -3);
        var borderImg = Img(border, sRing, typeColor, w < 100 ? 1.8f : 1.4f);
        Set(slot, "border", borderImg);
        var sso = new SerializedObject(slot); sso.FindProperty("typeColor").colorValue = typeColor; sso.ApplyModifiedPropertiesWithoutUndo();
        return slot;
    }

    static RectTransform Row(Transform parent, string name, float spacing)
    {
        var r = Node(name, parent);
        var h = r.gameObject.AddComponent<HorizontalLayoutGroup>(); h.spacing = spacing; h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        return r;
    }

    static Button Btn(GameObject instance, string label, float w = -1, float h = -1)
    {
        var b = instance.GetComponent<Button>();
        var t = instance.GetComponentInChildren<TextMeshProUGUI>();
        if (t) t.text = label;
        if (w > 0)
        {
            var le = instance.GetComponent<LayoutElement>(); le.preferredWidth = w; le.preferredHeight = h;
            ((RectTransform)instance.transform).sizeDelta = new Vector2(w, h);
        }
        return b;
    }

    static Image FilledImg(RectTransform rt, Color c)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sSquare; img.color = c; img.raycastTarget = false;
        img.type = Image.Type.Filled; img.fillMethod = Image.FillMethod.Horizontal; img.fillOrigin = 0; img.fillAmount = 0.5f;
        return img;
    }

    // ------------------------------------------------------------------ UI primitives

    static readonly Vector2 Center = new(0.5f, 0.5f), TopLeft = new(0, 1), TopRight = new(1, 1);

    static RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = 5 };
        var rt = (RectTransform)go.transform;
        if (parent) rt.SetParent(parent, false);
        return rt;
    }

    static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = Center;
        rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
    }

    static void Fixed(RectTransform rt, float w, float h)
    {
        rt.sizeDelta = new Vector2(w, h);
        if (!rt.TryGetComponent(out LayoutElement le)) le = rt.gameObject.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = w; le.minHeight = le.preferredHeight = h;
    }

    static Image Img(RectTransform rt, Sprite s, Color c, float ppu = 1.3f)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = s; img.color = c; img.raycastTarget = false;
        if (s && s.border != Vector4.zero) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = ppu; }
        return img;
    }

    static TextMeshProUGUI Txt(Transform parent, string name, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        var rt = Node(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field) ?? throw new System.Exception($"[Ashbound] {target.GetType().Name} has no field '{field}'");
        p.objectReferenceValue = Fresh(value);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Opening a new scene unloads assets nothing references yet; reload them by instance ID.
    static Object Fresh(Object o)
    {
        if (o != null || ReferenceEquals(o, null)) return o;
        return EditorUtility.EntityIdToObject(o.GetEntityId());
    }

    static void SetArr(Object target, string field, IList<Object> values)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field) ?? throw new System.Exception($"[Ashbound] {target.GetType().Name} has no field '{field}'");
        p.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = Fresh(values[i]);
        so.ApplyModifiedPropertiesWithoutUndo();
        for (int i = 0; i < values.Count; i++)
            if (p.GetArrayElementAtIndex(i).objectReferenceValue == null)
                Debug.LogError($"[Ashbound] {target.GetType().Name}.{field}[{i}] is empty after build");
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}
