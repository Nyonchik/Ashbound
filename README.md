# Ashbound

A Survivor.io-style roguelite made in **Unity 6** (6000.6.4f1). Pick a hero, survive 15 minutes of
swarming enemies, level up weapons and passives, and beat three bosses per stage.

## Features

- **5 hero classes**, each with its own mechanic: Mage (Mana), Berserker (Rage), Archer (Focus),
  Assassin (Shadowstep dash), Tank (Fortify)
- **26 Paths** with unique skills, looks, and awakened (level 6) versions
- **Weapon evolutions**: weapon Lv 5 + its paired passive Lv 5 evolves it (Black Hole, Starfall,
  Glacier, Crimson Whirlwind, Phantom Blades, ...)
- **3 stages, 9 bosses**: Ashen Fields, Hollow Forest, Ruined Capital, with telegraphed boss attacks
- **Map events**: swarms, encirclements, treasure goblin, cursed shrines, breakable props with loot
- **Meta-progression**: equipment (6 slots, rarities, merging), store with chests and gems,
  hero levels 1-20 with talents, Ascension A1-A10, daily quests, achievements
- **Modes**: daily challenge (seeded), endless mode
- **Accessibility**: key rebinding, screen-shake strength, colorblind-friendly damage numbers, HUD size

## Running it

1. Install Unity **6000.6.4f1** (Unity 6) through Unity Hub.
2. Clone this repo and open the folder in Unity Hub (`Add project from disk`).
3. Open `Assets/_Ashbound/Scenes/MainMenu.unity` and press Play.

Scenes: `MainMenu` -> `ClassSelect` -> `Game`.

## Project layout

```
Assets/_Ashbound/
  Scripts/   Core (progression, saves), Gameplay, UI (built at runtime), Data, Editor tools
  Art/       Characters, enemies, bosses, effects, icons, UI
  Data/      ScriptableObject game data
  Prefabs/  Scenes/  Shaders/  Fonts/
  Resources/Audio/   Music and SFX
```

Development notes are in [`DEV_NOTES.md`](DEV_NOTES.md). Editor-only shortcuts: `G` god mode,
`B` jump to the next stage's boss set.

## Status

Work in progress. Phase 4 (settings, accessibility, first-run tutorial) is still being tested.

## License

Code is released under the [MIT License](LICENSE).
Fonts: Cinzel and Nunito, under the SIL Open Font License (see `Assets/_Ashbound/Fonts/*-OFL.txt`).
