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


## F3B3 Frost client 70707 provisional actual Chrome builds

- `cp175-f3b3-frost-dps.json`: repeated Frostbolt with learned Frost precision, improved cast time, Shatter/Frostbite, and no unselected consumables
- `cp175-f3b3-frost-fingers-proc.json`: 120s deterministic proc-oriented Ice Lance/Frostbolt rotation with actual source-owned Fingers of Frost aura, charges, damage events and replay
- `cp175-f3b3-frost-barrier.json`: explicit Ice Barrier cast and AbsorbApplied event; backend scripted incoming-hit tests establish actual shield depletion and conditional Frost damage rather than claiming a browser incoming hit
- `cp175-f3b3-fire-start-frost-hybrid.json`: Fire-starting specialization with *legal 51-point Frost-heavy ranked selection*, source-backed Frost spells and learned capstone Ice Barrier, real combat export and deterministic replay

These files follow the existing Solo DPS import schema and are exercised in real Chrome; screenshots are retained in the workflow's `cp174-ui-viewport-captures` artifact and raw reports in `cp174-level60-full-simulation-reports`.


## F3B4 — four 51-point cross-tree level-60 Mage executions

- `cp175-f3b4-arcane-fire.json`: Arcane 36 / Fire 15 / Frost 0 — Arcane Power and Presence of Mind alongside Fireball and Accelerant/Ignite
- `cp175-f3b4-arcane-frost.json`: Arcane 36 / Fire 0 / Frost 15 — learned Arcane Power plus Ice Lance/Frostbolt with Ice Shards/Frostbite
- `cp175-f3b4-fire-frost.json`: Arcane 0 / Fire 36 / Frost 15 — Combustion, Frostfire Bolt, Ignite, Ice Shards; capstone remains legally ranked
- `cp175-f3b4-three-trees.json`: Arcane 16 / Fire 20 / Frost 15 — direct Frostfire Bolt and Fire/Frost/Arcane passives and proc-granting talents

The real Chrome workflow validates talent ranks, typed requests, actual combat events, build and report export, import and seeded replay. Removing an enabling talent and importing a stale custom action are separately tested, not described as successful browser combat. The starting specialization is a rotation preset, not a class permission gate.


## F3C0 — Warlock current 70753 source-alignment and real Chrome fixtures

All four are genuine 51-point schema-v1 level-60 imports. Source coefficients remain provisional, with some November 2 adjustments announced before client-verified tooltip refresh.

- `cp175-f3c0-affliction-wrack.json`: actual Corruption/Bane and Wrack six-tick channel receipts, Improved Drains damage mask, no implicit demon or potion
- `cp175-f3c0-demonology-felhunter.json`: actual Felhunter damage events, owner/pet separation, pet-health Stamina inheritance and selected Demonology passives
- `cp175-f3c0-destruction-shadow-flame.json`: learned Conflagrate and Incinerate, landed Shadow and Flame aura and ranked school damage
- `cp175-f3c0-affliction-start-destruction.json`: Affliction preset but legally learned Destruction spells and independent demon damage

The browser checks source-facing not-implemented Curse of Exhaustion warning, real selected ranks, serialized request, combat events, export/import and deterministic replay. Controlled scripted pet incoming-health mechanics are separately tested in xUnit. Screenshots and raw reports are retained by the established GitHub Actions artifacts.

## F3D3B — Beast Mastery and pet final mechanics browser fixtures

Four separate, importable 51-point Hunter builds were added to the existing real Chrome pipeline:

- `cp175-f3d3b-beast-wrath-frenzy.json`: Beast Mastery / active Cat; actual owner Bestial Wrath cast, pet-owned 18-second aura, Frenzy proc after eligible pet crit and owner/pet damage separation
- `cp175-f3d3b-wind-serpent-focus.json`: Beast Mastery / Wind Serpent; real Lightning Breath damage, its sourced 50 Focus expenditure, independent pet melee and correct owner attribution
- `cp175-f3d3b-survival-bm.json`: legal 44 Survival / 7 Beast Mastery; melee-first Raptor Strike alongside an active Raptor benefiting from learned 2/2 Focused Fire
- `cp175-f3d3b-petless-lone-wolf.json`: Marksmanship / Lone Wolf with companion explicitly disabled; no pet attacks or pet damage, and actual petless owner multiplier receipt

For each file the browser harness imports a genuine build, asserts selected talent ranks and typed request serialization, runs combat, exports and reimports the build, saves the full JSON combat report, captures talent/rotation screenshots, and checks deterministic same-seed replay. The standard artifacts remain `cp174-ui-viewport-captures` (PNG) and `cp174-level60-full-simulation-reports` (JSON). The exact-final-head workflow must succeed before declaring these four fixtures accepted.

Controlled dynamic AP/Armor inheritance, tagged pet debuff dispels, scripted pet death/revive and timing comparisons are independent xUnit backend evidence and **not** claimed as direct Chrome battle observations. Numerical coefficients and the Core Hound's tameability remain provisional.
