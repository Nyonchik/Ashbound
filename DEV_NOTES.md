# Ashbound dev notes (Nov 16, 2026)

## Done this session (built and play-tested in Unity, NOT committed yet)
- Evolution rule: weapon Lv 5 + its paired passive at Lv 5 (passive stays in its slot). Cards show "Evolves with X Lv 5"; passive cards show what they evolve.
- Evolved weapons have unique behavior and art (new art in Art/InGame/Effects, fx_*.png):
  - Black Hole: opens on the biggest crowd, pulls ~2s, explodes
  - Starfall: volleys of 6 stars on random on-screen enemies
  - Glacier: rings of ice spikes, 75% slow (bosses capped at ~40%)
  - Crimson Whirlwind: one giant tornado with axes inside
  - Phantom Blades: 3 boomerang daggers + 2 circling daggers
- Chain Lightning: real bolt that hits one enemy and jumps to neighbours (-15% dmg/jump). Awakened (Thunderstorm Heart): gold bolts + sky strikes.
- Path skill looks: Tidal Wave = rolling waves (awakened: 2 waves); Thunder Hammer = crater + lightning (awakened: double slam); Fireball explosion art; Frost Nova burst + slow; Railshot/Arrow Storm golden arrows.
- Awakened icons for all 9 path skills (Icons/awk_*.png), shown in HUD/cards/pause.
- Bosses (BossAI.cs, BossBullet.cs, Hazard.cs): Ash Warden charges + fire trail; Matriarch summons minions + poison puddles; Hollow King bullet rings + teleport. Harder stages = faster attacks + borrowed moves.
  Boss arena ring (player locked inside), boss health bar with name.
- UI: level-up cards colored by type (red weapon, blue passive, orange path, gold evolution/awakening); loadout slots with colored borders.
- Pause: button FIXED (scene-saved click listener was lost; now wired in code). Pause screen shows full build as cards, music/SFX sliders, Restart run, Main menu.
- Upgrades panel: Buy buttons FIXED (image wasn't clickable).
- Volume sliders (main-menu Settings + pause screen): a single click anywhere on the track now sets the value (no dragging needed). Tested.
- Editor-only test keys: F2 level-up, F3 max weapons+passives, F4 die, F5 skip to next boss.

## Finished (evening)
- Balance: boss contact damage 16/20/28 (was 25/30/40); Matriarch summons 3(+tier) every 7s; arena ring alpha 0.42.
- Pause panel layout fixed (title can't squash). Restart run tested. Bigger see-through tornado.
- macOS app rebuilt: Builds/Ashbound.app. Local git commit made (not pushed).

## Still open
- Buy-button fix not click-tested (didn't want to spend your real gold); root cause fixed.
- Arena ring art is thick - could regenerate a thinner ring.
- Ideas: more boss variety per stage, skill UI polish pass.
- git-lfs isn't installed on the Mac: brew install git-lfs before pushing.

## Done (late evening)
- Stage enemies + ground: Hollow Forest (stump walker, wolf, mossy brute, forest floor) and Ruined Capital (undead soldier, ghoul, brick golem, cobblestones).
- Boss spawn kills every regular mob (they drop XP) and pauses normal spawns until the boss dies. Boss intro card with name + title. Thin arena ring art.
- Hit feedback: red screen edge on hits, pulsing red + heartbeat sound under 30% HP.
- Boss chest opening animation (TREASURE! +25 gold +1 level up). Chest on the ground is bigger.
- Victory/defeat screen: crown shard or skull icon, gold or red frame.
- Settings: Damage numbers on/off, Screen shake on/off. Main menu: Unlocks screen (heroes, stages, paths, crown shards, x2 speed).
- First-run tips (move, level-up, drops, evolve, boss, pause), each shown once per save.
- Tidal Wave is now a real ring of rushing water expanding from the hero (+ trailing ripple).
- Drops much rarer: ~0.26% per kill (meat > gold bag > magnet > medkit) and at least 15s apart; bosses drop meat instead of a medkit.
- Upgrades buy verified (bought Magnet Lv 1 for 50 gold).
- Editor-only test keys: F2 level-up, F3 max weapons+passives, F4 die, F5 next boss, C drop a chest.

## Ideas for later
- More boss variety per stage; a skill UI polish pass; balance after a few real runs (drop rates, boss damage).

## Note
Test runs with F5 counted as real progress in the save (Berserker Beast path unlocked, stage 1 shows cleared). Settings > Reset progress clears it.
