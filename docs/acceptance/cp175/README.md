# CP175 — Importable level-60 Solo DPS acceptance builds

These are **`warcraftsim.solo-dps-build` schema v1 files** for the actual `/solo-dps.html` Import control (not typed API requests). They use repository-curated itemized talent ranks, existing catalog item IDs and the CP175 Warrior 31 Fury / 20 Arms executable fixture. All are historical-provisional; source client hotfixes and full equipment slots are not presumed.

## How to use
Open `/solo-dps.html`, choose **Import**, select a JSON file, then open **Rotation** to inspect/reorder its custom priority. The Encounter tab shows duration, seed, companion and relevant class controls. Click **Simulate**, export the report, and use **Replay same seed** to check deterministic behavior. The page must reject invalid itemized ranks rather than repairing them.

## Files and checks

| File | Starting preset | Main executable assertions |
| --- | --- | --- |
| `cp175-warrior-custom-rotation.json` | Warrior Fury, 31 Fury / 20 Arms | Bloodthirst and Whirlwind; 100 starting Rage, Berserker Stance |
| `cp175-mage-custom-rotation.json` | Mage Arcane, 36 Arcane / 15 Fire | Arcane Power, Presence of Mind, Arcane Wrath / Arcane Blast; staff 19102 and approved enchant/oil IDs |
| `cp175-warlock-custom-rotation.json` | Warlock Destruction, 49 Destruction / 2 Affliction | Incinerate and Immolate / Conflagrate, optional Succubus |
| `cp175-hunter-custom-rotation.json` | Hunter Marksmanship, 51 Marksmanship | Aimed Shot, Serpent Sting, Arcane Shot; Cat companion / Hawk Aspect |
| `cp175-warrior-mixed-tree.json` | Warrior **Arms** start, 31 Fury / 20 Arms | **Off-preset Bloodthirst** with custom priority, actual attack events |
| `cp175-warlock-mixed-tree.json` | Warlock **Affliction** start, 49 Destruction / 2 Affliction | **Off-preset Incinerate** with custom priority, actual spell events |

The Warrior mixed-tree allocation is copied from `CP175_WarriorMixedTreeCapstones` (full tier and prerequisite validation). The Mage, Warlock and Hunter ranks are taken from `WarcraftSim.Api/wwwroot/itemized-presets/`; their historical-only talent entries grant no invented execution bonuses.

**Known bounds:** Full ability/cooldown/source certification across all class-tree mixes remains incomplete. Warrior's historical race profile is provisional; temporary stat effects are preserved only for their supported source-backed duration and existing gear semantics. Hunter pet path and Warlock demon availability remain typed-runner controlled. No item IDs have been inserted into otherwise empty slots. A build must not be declared browser-certified solely because this JSON parses.

## E1/E2 executable-condition additions

- `cp175-warrior-scripted-execute.json`: 28-second time-driven boss HP (100% on pull, 20% at 12 seconds, 0% finish marker), with a custom Execute <=20% condition. Chrome checks completed Execute casts occur after the configured threshold, not from actual training-boss HP.
- `cp175-warlock-conditional-maintenance.json`: timed HP milestones plus Immolate target-aura-missing/owner-only maintenance and Incinerate mana >=20% rules; custom execution is evaluated in the existing Core condition evaluator.

Both remain schema-v1 importable builds and are covered by the same browser import/export/combat/replay harness as the original six. The engine supports stepwise scripted boss HP; OR groups, remaining-time predicates and full add lifecycles are not represented as implemented here.
