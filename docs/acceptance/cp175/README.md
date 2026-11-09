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

## F3D4 — Hunter mixed-tree engine/browser fixtures

Six additional source-provisional level-60 51-point imports are connected to the existing real Chrome acceptance pipeline:

- `cp175-f3d4-marksmanship-beast-mastery.json`: Marksmanship start, 44 MM + 7 BM, Wind Serpent with independent auto, signature Focus, sourced ranged shots and BM pet bonuses
- `cp175-f3d4-survival-beast-mastery.json`: Survival start, 44 Survival + 7 BM, melee-first Raptor Strike/Lacerating and Raptor pet with learned Focused Fire
- `cp175-f3d4-marksmanship-survival.json`: Marksmanship start, 46 MM + 5 Survival Improved Tracking, owner ranged attack and active pet combat
- `cp175-f3d4-three-trees.json`: Marksmanship start, 41 MM + 5 Survival + 5 BM, learned Scatter Shot, tracked-target bonus, pet Endurance Training, talent-removal UI regression
- `cp175-f3d4-survival-three-trees.json`: Survival start, 39 Survival + 5 MM Lethal Attacks + 7 BM, actual melee-first combat and ranked effective crit alongside BM pet
- `cp175-f3d4-beast-mastery-three-trees.json`: BM start, 41 BM + 5 MM Lethal Attacks + 5 Survival Improved Tracking; real owner and pet attacks

The browser checks imported ranks, complete typed serialization, active owner and pet event receipts, pet Focus spending, exported build/report, screenshots and exact-seed replay. The negative browser interaction removes learned Scatter Shot from the three-tree allocation and verifies its priority is scrubbed. The generated screenshots are in the standard `cp174-ui-viewport-captures` CI artifact, and JSON reports in `cp174-level60-full-simulation-reports`.

**Important boundary:** These fixtures prove representative legal cross-tree subsets, **not arbitrary deep mixed-tree Hunter gameplay**. Unsupported active talents must fail strict validation rather than be silently selected. Controlled pet absence and conditional damage tests in xUnit are backend-only evidence, not browser simulations of enemy AI.


### F3D4 final off-tree eligibility correction (2026-11-04)

The Marksmanship runner now executes selected Beast Mastery **Deadly Aspects** as source-qualified landed Auto Shot haste: 2% per rank chance while Aspect of the Hawk is active, 30% actual swing haste for a timed 12 seconds, with aura expiry and no permanent stacking. It also retains the selected **Bestial Discipline** casting Spirit-regeneration fraction (25% per rank) in the owner Mana processor, in addition to existing pet Focus regeneration. Both have direct combat/resource tests in `CP175_F3D4HunterCrossTree.cs`. The MM/BM browser fixture is 44 MM / 7 BM with real 2/2 Focused Fire, rather than the initial 41/10 test allocation with a nonexecuting Deadly Aspects rank.

Strict unified itemized validation rejects selected Beast Mastery abilities that are not implemented in the chosen starting runner; Marksmanship currently rejects Improved Mend Pet, Summon Hawk, Improved Aspect of the Monkey and Pathfinding. Survival rejects incoming unified allocations of Deadly Aspects, Improved Mend Pet, Improved Aspect of the Monkey and Pathfinding, but preserves existing CP169 legacy typed `PetTalents` execution and Hawk family tests. These rejections explicitly identify **remaining cross-tree integration limitations**, not missing points or fabricated passives. Full arbitrary cross-tree gameplay remains incomplete.


## F3D4B — Off-tree Hunter talent-granted action acceptance

The existing Chrome harness now includes five additional legal 51-point level-60 Hunter builds:

- `cp175-f3d4b-survival-sniper-scatter.json` — Survival starting preset, learned Sniper Shot + Scatter Shot, timed casts and real Mana expenditure, independently acting Cat
- `cp175-f3d4b-beast-mastery-sniper-scatter.json` — Beast Mastery start with learned MM shots, pet melee/Focus and owner-ranged damage
- `cp175-f3d4b-marksmanship-summon-hawk.json` — Marksmanship start with legal BM Summon Hawk, two separate Hawk actors, Arcane shared cooldown, owner/pet/Hawk attribution
- `cp175-f3d4b-marksmanship-survival-melee.json` — Marksmanship start with learned Strider Kick, Counterattack and Deterrence; explicit melee engagement and opt-in scripted parry; no pet
- `cp175-f3d4b-beast-mastery-survival-melee.json` — Beast Mastery start with the same learned Survival melee actives, legal pet, real pet damage and parry requirement

All are run via `tools/solo-dps-browser-interactions.cjs` with real Chrome, actual itemized talent selection, typed request checks, damage/aura/Mana receipts, build export/reimport, full JSON report and deterministic seeded replay. A separate browser check removes the learned Strider Kick rank from BM and confirms its old custom priority is scrubbed. Reports and screenshots are preserved under the existing `cp174-level60-full-simulation-reports` and `cp174-ui-viewport-captures` Actions artifacts.

The explicit `hunterMelee` and `hunterParryEvery` imported encounter fields apply only to the supported melee-active fixtures. The latter is a transparent deterministic incoming-parry scenario; it does **not** claim automatic enemy AI or spatial movement. Deterrence is supported only if learned and intentionally selected in a custom DPS priority. The frontend uses the actual Hunter runner-backed priority preview for strict custom/import action eligibility. Existing F3D0–F3D4 and 19-pet-family acceptance fixtures remain intact.


## F4A — Four-class mixed-tree integration browser acceptance

Four separate itemized level-60 **51-point** builds are executed through `/solo-dps.html` and the existing `tools/solo-dps-browser-interactions.cjs` pipeline:

- `cp175-f4a-warrior-arms-fury.json`: Arms starting preset with real Fury Bloodthirst, 51 legal rank points, custom Berserker Stance/Rage priority and Warrior-native full JSON report fields
- `cp175-f4a-mage-three-tree.json`: Arcane starting preset, legal Arcane 16 / Fire 20 / Frost 15, actual Frostfire Bolt damage with cross-tree selected spell passive ranks
- `cp175-f4a-warlock-demonology-three-tree.json`: Demonology starting preset, legal 17 / 17 / 17, real owner spell and independently attributed active Imp demon damage
- `cp175-f4a-hunter-mortal-shots-pet.json`: Survival starting preset with Marksmanship Mortal Shots and Sniper Shot, actual ranged critical impacts alongside independent Cat damage; numerical critical multiplier checked separately in xUnit

Each build must pass rank-derived totals, actual action event receipts, exported build reimport, full JSON report and exact-seed replay; saved screenshots and reports are uploaded into the existing `cp174-ui-viewport-captures` and `cp174-level60-full-simulation-reports` workflow artifacts. Warrior's report carries native `meanDps` and ranked build fields, while the three typed caster/Hunter reports carry `dps` and `talentAllocation`; the browser assertions respect that difference without weakening the common 51-point requirement.

**Limits:** These are representative mixed-tree stationary Solo DPS builds. The code does not prove all support/control/defensive abilities, complete deep off-tree Hunter Survival passive parity, all source coefficients, raid behavior or enemy AI. Earlier CP175 browser fixtures, 19 Hunter pet-family runs and Warlock demon regressions remain part of CI. Claim final acceptance only for a green exact-final-head Actions workflow.


## CP175-F4B — Four-class selected-character stat integrity

The following new importable, legal 51-point builds add actual user-selected source-provisional equipment/enchant, food, buff and target-debuff configuration to accepted F4A talent allocations:

- `cp175-f4b-warrior-arms-fury.json`: Human Arms/Fury, source-catalog main-hand and helmet, compatible strength weapon enchant, Smoked Desert Dumplings, Greater Strength Elixir, Mark of the Wild, Faerie Fire
- `cp175-f4b-mage-three-tree.json`: Human Arcane/Fire/Frost, registered spellcaster weapon with permanent enchant and temporary weapon effect, Nightfin Soup, Greater Arcane Elixir, Arcane Intellect, Curse of the Elements, explicitly stocked Major Mana Potion
- `cp175-f4b-warlock-demonology-three-tree.json`: Orc Demonology three-tree with Imp, approved armor and compatible wrist enchant, Baked Salmon, Greater Arcane Elixir, Arcane Intellect, Curse of the Elements, explicitly stocked Superior Mana Potion and eligible selected racial
- `cp175-f4b-hunter-mortal-shots-pet.json`: Orc mixed-tree Survival, Cat, registered chest and enchanted necklace, Poached Sunscale Salmon, Mongoose Elixir, Battle Shout, Faerie Fire and eligible selected racial

Each run uses the existing real Chrome import→server validation→backend stat preview→typed request→combat→full report/export→build reimport→same-seed replay pipeline, checking selected ID fidelity rather than assuming catalog presence means executed effect. Full JSON reports and screenshots are collected in the existing `cp174-level60-full-simulation-reports` and `cp174-ui-viewport-captures` CI artifacts.

A separate browser transition imports the Mage build, toggles selected Arcane Intellect off/on, requires actual backend displayed Intellect to decrease/restore without stacking, then directly unequips its enchanted main-hand. The exported build must have **no** remaining item or enchant in that slot and must retain unrelated selected buffs. Changing class to Warlock in that same session must clear previous Mage items, food, buffs, target debuffs and combat inventory. Backend numerical fixtures verify exact food/buff stat deltas and disabled/absent/eligible potion combat-event behavior.

**Limitations:** These are representative approved catalog cases, not a claim that the Forever catalog, transient effect geometry, all offensive procs, race/class sources or every possible item permutation is complete. Only existing reviewed or source-provisional coefficients and modeled effect status apply. Final acceptance requires the exact-current-head Actions result.


## F4C/D implementation — report comparisons (partial checkpoint)
The Results tab now has a two-slot report comparison workflow. Run a build and choose **Use current run** for A, change the build and run again for B, or import previously exported `warcraftsim.solo-dps-report` v1 JSON independently into either slot. The comparison module does not modify the simulation, hidden inventory, or saved builds. It rejects build JSON passed off as a report.

The view checks class, duration, iteration count, seed, boss HP, known target defenses, scripted encounter and add configuration, external buffs/food/target debuffs, and reviewed-versus-historical baseline mode before displaying a DPS delta. Mismatched inputs show side-by-side values **without** a numerical DPS delta. Multi-run DPS is the mean, while reported total damage and ability sources remain the representative run; these are never mislabeled as mean damage. Unknown owner/companion contribution is reported unavailable, not zero. A matched single-seed result is not a statistically definitive DPS ranking.

`node tools/solo-dps-comparison.test.cjs` covers matched and mismatched cases, independently seeded distributions, sample-vs-mean labeling, effect assumptions, report integrity and invalid import rejection. The existing CDP browser harness adds comparison interactions and screenshots in `/tmp/cp174-ui-captures/`. **This is not a declaration that all F4C/D combat, encounter, artwork or live-release validation stages are complete.**

### Owner-supplied released-client observations
For future formula checks, supply a small reproducible observation batch: exact Forever client build, UTC timestamp, character class/race/level, stats, equipment/enchant IDs, exact itemized talent ranks, active buffs and target debuffs, target level and available armor/resistance evidence, spell or ability rank, event outcome (hit/crit/miss/resist) and damage observed, sample count, and measurement conditions. Store the original observations separately from any inferred coefficient; compare against a deterministic simulator fixture and record provenance/confidence before promoting a value. Do not assume the public release itself verifies beta coefficients. Existing verifier utilities under `tools/ForeverEquipmentImportCli/` apply only to their supported observation formats, not arbitrary combat logs.

## F4C/D local report acceptance and cost gate (2026-11-09)

The report milestone adds strict comparison integrity, independent owner/pet/Hawk and ability reconciliation, and a readable filtered combat-event table. Unknown historical attribution remains explicitly unavailable. Cast timestamps are actual receipts, not a fabricated cooldown-availability trace. Resource totals preserve zero; periodic scheduler ticks do not double-count damage. The browser export reader now waits for complete JSON rather than file existence alone.

Local verification: zero-warning strict .NET 10 Release build; 3,075/3,075 xUnit tests; 60/60 Python tests; Node comparison, event and DOM contracts; nine spec routes and 77 imported Chrome fixtures with export/replay, no unexpected browser errors or stat-preview failures. Twelve new numerical cases cover all supported class presets. Source-backed local artwork preparation retrieved 138 pinned talent icons and the existing item-icon census; unresolved mappings remain neutral rather than guessed.

Evidence filenames include `cp175-f4c-resource-events.png`, `cp175-f4c-matched-comparison.png`, `cp175-f4c-mismatched-comparison.png`, and the existing CP175 fixture screenshots. Full local JSON reports remain in the development worktree's `acceptance-final-*` directories. No generated reports, source receipts or copyrighted image binaries were deleted or committed. The existing CI artifact names remain unchanged; PR retention is 7 days and manual acceptance retention remains 14/30 days.

This is not final certification: the last verified remote HEAD/CI is still `8fa1b75dbbd54fd498523c548039179a81a252f8` / Actions `42666027337`. Pushes and CI triggers are held while storage-cost risk is unresolved. Hunter off-tree passive parity review is continuing. Do not merge or deploy; do not call the project ready for independent review before the final code tree and exact-final-head Actions are accepted. See the implementation ledger for current scope, cost evidence and source limitations.

### Responsive acceptance and current execution gate

The real Chrome viewport test covers equipment, talents, effects, encounter, rotation and results at 390/768/1440/1920 widths. A reproduced hidden build-transfer control defect is corrected: Import/Export remain visible in narrow layouts. No document overflow or clipped Simulate control was found under actual device metrics. Screenshots are `cp175-viewport-{width}-{view}.png` in the standard capture collection.

Owner account evidence confirms a $0 Actions budget that blocks paid usage. The planned cohesive milestone push is no longer held for cost approval, but no budget change, artifact deletion or paid usage is authorized. Exact-final-head CI remains a required pending gate; the existing older green result does not certify newer local changes.
