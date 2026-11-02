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

## E1/E2 advanced full-duration acceptance (180 seconds)

- `cp175-warrior-180s-scripted-add-groups.json`: 180s scripted boss health 100% → 75% at 60s → 35% at 120s → 20% at 150s → finish marker 0%; two independent adds (30–125s, 90–160s), the first with an add-health milestone at +45 seconds relative to spawning. The real Warrior Execute cast is checked after the 150-second 20%-HP milestone. The fixture equips 16 actual locally reviewed/curated catalog selections, not invented item stats.
- `cp175-warlock-180s-or-add-target.json`: same 180s boss milestone series; one add 25–120s with scripted 40%-HP milestone 50 seconds after spawn. The owner-only Immolate refresh rule targets that specific add, using a 3s aura-expiry window. Incinerate is conditional on mana >=20% **AND** either boss HP <=35% **OR** encounter remaining <=50s. The fixture equips 13 reviewed/curated records, including Plagueheart pieces, and preserves class-specific set/equipment validation.

Both are tested through the real browser import/export, full combat event timeline, condition serialization and exact-seed replay. Failure to observe timed add-state receipts or legal class-specific casts is a failing acceptance, not a documentation-only note. User-editable add groups and relative/absolute timing share the same backend scheduler.

## F3B0 — Mage Arcane baseline (70707 provisional)

- `cp175-f3b0-mage-arcane-stacks-resilience.json` uses a complete 51-point Mage Arcane build with 3/3 Arcane Resilience and 5/5 Arcane Concentration gating Arcane Meditation. It opts out of Arcane Power/Presence of Mind for isolated repeated Arcane Blast casts; no selected potion or Mana Ruby is fabricated.
- The real Chrome harness checks the three-rank talent tile and provisional indicator, server-projected armor increase/decrease on rank selection and restored preview, rejection of illegal Arcane Concentration removal, build roundtrip, actual scaled Mana cost and damage events, report export and exact-seed replay.
- Screenshot: `cp175-f3b0-mage-arcane-source-armor.png` in the existing CI viewport-capture artifact. Numeric zero-to-four stack receipts additionally live in `CP175_F3B0Mage70707SourceCorrections`.


## F3B1 — Arcane mechanics (client 70707 provisional)

- `cp175-f3b1-arcane-mana-shield.json`: 51-point Arcane build, explicit `mana-shield` defensive priority followed by Arcane Blast, no hidden consumable inventory. Browser proves shield cast and real AbsorbApplied; scripted physical Mana spending is verified by controlled backend tests, not misrepresented as browser combat.
- `cp175-f3b1-fire-arcane-subtlety.json`: Fire-starting 51-point build with 2/2 Arcane Subtlety, explicit Arcane Missiles channel. Browser checks talent ranks, typed request, real channel, build export and deterministic seeded replay. All-school resistance penetration remains unavailable.
- Screenshots in standard `cp174-ui-viewport-captures` workflow artifact; raw reports in `cp174-level60-full-simulation-reports`.

- `cp175-f3b1-mage-missile-barrage.json`: 51-point Arcane build using an existing source-aura-active condition to cast Arcane Missiles only when Missile Barrage actually procs. The browser checks landed Arcane Blast, 70707 Missile Barrage aura application, first channel consumption, Arcane Missiles damage/free Mana receipt and deterministic replay. Client 70707 channel cadence remains provisional.


## F3B2 — Fire mechanics (provisional client 70707)

- `cp175-f3b2-fire-combustion-pyroblast.json`: legal 51-point Fire build, real Combustion and Fire Blast, landed Heating Up aura and consuming Pyroblast, tested build/report export and exact-seed replay. Ignite's exact critical roll and Impact proc are independently verified in deterministic xUnit rather than fabricated by the browser.
- `cp175-f3b2-fire-scorch-power.json`: Fire-focused repeated Scorch with 3/3 Improved Scorch, 3/3 Fire Power and 3/3 Impact; real Scorch effect application. xUnit compares exact first/second impact multipliers with and without Fire Power.
- `cp175-f3b2-arcane-start-fire-mixed.json`: Arcane starting preset with an entirely legal Fire-focused 51-point allocation, real Fire Blast/Combustion/Scorch and passive Fire Power effects. Talent prerequisites and typed cross-tree action availability are enforced by the same backend.
- The browser screenshots and reproducible full reports are retained in the established `cp174-ui-viewport-captures` and `cp174-level60-full-simulation-reports` CI artifacts.
