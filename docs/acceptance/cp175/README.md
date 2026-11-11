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

## Current completion milestone (2026-11-09)

The current milestone completes report reconciliation and responsive workflows for the supported stationary four-class scope, adds exact historical artwork for 21/46 modifier tiles, corrects Hunter off-tree stat/resource/damage inheritance, and reconciles the supported Hunter precombat sidebar with backend source contributions. See the final milestone section of `docs/audits/CP175-implementation-ledger.md` and `docs/audits/CP175-modifier-icon-provenance.md` for numerical boundaries and unresolved source mappings.

Required acceptance includes the full strict .NET 10 Release build and xUnit suite, all Python importer tests, Node UI/report/event contracts, 77 actual Chrome build import/simulate/export/replay fixtures plus nine caster/Hunter routes, 24 responsive view/width combinations, and reversible real-sidebar Hunter talent changes. Full reports remain in `cp174-level60-full-simulation-reports`; screenshots, including `cp175-viewport-*` and `cp175-hunter-preview-talents.png`, remain in `cp174-ui-viewport-captures`.

A green prior commit is not acceptance for this milestone. Record the final exact SHA, successful Actions run and counts in the verification receipt before independent review. Source-provisional inputs, unsupported movement/control/AI and remaining explicitly labeled off-tree effects are not promoted to verified live-game mechanics by these tests. PR #28 stays unmerged and no deployment is performed.

### Final local verification receipt (2026-11-09)

Frozen source passed strict .NET 10 Release with zero warnings/errors; **3,148/3,148 xUnit tests**, zero skipped (including 70 new Hunter regression cases); **66/66 Python tests**; Node UI/event/report contracts; all 21 pinned artwork checks; 77 real Chrome build import/simulation/export/replay fixtures and nine caster/Hunter routes; all 24 responsive view/width combinations; and actual Hunter Lightning Reflexes rank 5 -> 0 -> 5 sidebar changes with exact restoration. Browser stat refresh failures: zero. Expected validation-rejection responses remain asserted; no unexpected browser error is accepted. Independent read-only review found no remaining confirmed issues after the aspect, catalog-preview and authored-buff corrections.

Full reports are retained locally in the completion acceptance directory and screenshots in the existing capture directory; generated files and binary artwork are not committed. The final pushed SHA, Actions run URL and remote gate result must be recorded in the PR verification receipt. This local receipt does not substitute for exact-head CI.


## Verification update (2026-11-10)

Later dated entries in [the implementation ledger](../../audits/CP175-implementation-ledger.md) supersede the historical publication status above. The latest verified published checkpoint is `f6443d6389181fd9fd1e44c4c0db9ca9568833b7`, accepted by [Actions 42915003855](https://github.com/layoung3/WarcraftSim/actions/runs/42915003855). It passes 4,951 .NET and 126 Python cases, the existing core browsers, Elemental 29 / Balance 28 / Shadow 32 / Enhancement 20 / Rogue 36 browser gates, and existing HTTP regression suites. All six artifacts uploaded. Independent review of all 31 Rogue screenshots and the associated reports confirms the requested desktop/mobile capture coverage and request/export/replay integrity, within that restricted Assassination workflow.

The next local group repairs existing Feral finisher execution and startup validation, and preserves fractional Rogue Energy in result cards. It passes strict Release with zero warnings/errors, 5,061 .NET cases, 126 Python cases and 15 Node suites. [Feral causal evidence and reproducible HTTP checks](../../audits/CP175-feral-workflow.md) describe the bounded policy and exact before/after controls. Its new exact-head Actions/browser gate is still required; the older success does not certify these changes. Feral is not yet admitted to the unified builder, and its AP/form/preparation limitations remain open. Historical source alignment is not current-client certification. The broader project is unfinished; PR #28 remains draft and unmerged, with no deployment or additional spending.


### Feral workflow accepted; preparation follow-up locally verified

The next published checkpoint, `bc2c08724f66031502d4ad297dc7e9306c42da55`, passed [Actions 42920413317](https://github.com/layoung3/WarcraftSim/actions/runs/42920413317): 5,061 .NET and 126 Python tests, existing core/class browsers including Rogue 37, and the existing HTTP gates. Independent artifact review confirms precise 110 / 27.5 / 17.5 Energy on desktop/mobile with unchanged gameplay and complete coverage. All six artifacts remain preserved.

The subsequent [Feral preparation and Predatory Strikes milestone](../../audits/CP175-feral-precombat.md) is locally verified with 5,307 .NET tests, 126 Python cases, 15 Node suites, the existing HTTP gates and 272 complete causal comparisons. Preservation-only extraction was verified separately before the talent correction. This adds a runner-level preview, not a Feral UI or strict-admission route. Its exact-head Actions gate remains pending; unresolved AP/form/baseline limitations and historical source confidence remain explicit. No overall completion, merge or deployment is claimed.


### Historical/manual fixed-Cat backend follow-up (2026-11-11)

Head `71086dd29bcc80fa6548dd2691558e85a4902253` passed [Actions 42924768837](https://github.com/layoung3/WarcraftSim/actions/runs/42924768837), including 5,307 .NET and 126 Python tests, the existing core/class browsers and HTTP gates. All six artifacts uploaded.

The subsequent [historical/manual Feral backend stage](../../audits/CP175-feral-historical.md) is locally verified with 5,716 .NET tests, 126 Python cases, 15 Node suites, all existing HTTP gates, both 272-check preservation/causal sets and 977 new strict requests. Its genuine 36-point partial allocation retains 15 unspent points. Historical attributes/resources and explicitly manual pre-talent Cat AP have distinct ownership; unsupported controls fail closed. This is backend integration only, with no Feral UI or default preset yet. Inherited provisional effects, missing AP consumers and other source limits remain visible. Its new exact-head Actions/browser gate is still pending, and the overall project remains unfinished.


### Partial Feral unified workflow (2026-11-11)

The preceding backend head `1eb93116334886f64d1f27c712b5dd625f49d94a` passed [Actions 42932143422](https://github.com/layoung3/WarcraftSim/actions/runs/42932143422): 5,716 .NET and 126 Python tests, existing core/class browsers and HTTP gates, with all six artifacts uploaded. That result accepts the backend checkpoint only.

The new unified Druid/Feral workflow adds a preparation-only preview, genuine fourteen-key **36-point partial preset with 15 unspent**, explicit manual Cat AP, strict import/export, complete reports, comparison and replay. AP starts blank; zero is valid. Historical resources and manual AP remain separate, and observed/reviewed input modes and unsupported mechanics are not silently substituted. The [illustrative importable fixture](cp175-feral-historical-manual-cat.json) uses Night Elf, manual AP 800, 10 seconds and seed 144001. It is not a player observation, optimized build or complete 51-point allocation.

Final local verification passes strict Release with zero warnings/errors, 5,834 .NET tests, 126 importer Python tests, 22 identity-oracle tests, 19 Node suites, all prior HTTP gates, 40 new Feral UI requests/eight full reports, Feral 56/81, both 272 preservation/causal sets and 977 historical strict requests. All 37 frozen source paths match after testing. The first final round's obsolete absent-preset assertion and all earlier evidence are preserved; the new exact partial-preset assertion retains full coverage.

The Actions browser harness adds 34 named gates, 40 rejection cases and measured desktop/mobile captures. Its exact-published-head browser result and actual screenshot review are still pending; local model tests do not certify pixels. See [the implementation ledger](../../audits/CP175-implementation-ledger.md) for source, resource, comparison and unsupported-mechanics boundaries. This supersedes the prior backend-only UI status, not its historical source limits. No merge, deployment or overall project completion is claimed.

### Comparison import correction after first Feral UI publication

Head `d0ad14e2ef8f46a9b231e9993ca1503755e6bf03` failed [Actions 42939570850](https://github.com/layoung3/WarcraftSim/actions/runs/42939570850): the unchanged comparison browser test found that rejecting an invalid file erased a previous valid selection. The new Feral browser stages were not reached. The correction preserves accepted reports on hard rejection and prevents stale asynchronous file reads from overwriting newer selections. Invalid candidates are never committed, and reports with integrity warnings still suppress numerical deltas. The original browser assertion and strict validators are unchanged.

Final-r3 passes the complete local bundle: 5,834 .NET tests, Python 126 plus identity 22, all 19 Node suites including 41 transaction cases, every existing HTTP gate, Feral UI 40/eight reports, Feral 56/81, both 272 comparison sets and historical 977. All 38 frozen source hashes match. Failed evidence remains preserved. A new exact-head browser run and actual screenshot review are still required before this UI milestone is accepted.

### Exact browser inventory follow-up

Head `988845117e82fe4d94c8fa587b1b3af6395c523a` passed all 11 comparison browser workflows, then failed [Actions 42941968657](https://github.com/layoung3/WarcraftSim/actions/runs/42941968657) on an obsolete Balance-only selector assertion. The test now strictly requires `balance, feral` and still requires Balance as the default. A 36-entrypoint/48-file browser audit found no other blocking stale inventory assumption; 37 new local inventory checks execute the real selector and browser assertions. No production code changed. Final-r4 reran the complete local bundle successfully, including 5,834 .NET tests, Python 126+22, all 19 Node suites and every HTTP gate, with all 65 source hashes unchanged. The failed run is preserved and is not acceptance. A new exact-head browser run and actual screenshot review remain pending.

### Initial race identity follow-up

Head `a361e25b4c5e7d1222dce7793437ca7a8c68effa` passed every existing class browser suite, then failed [Actions 42944848428](https://github.com/layoung3/WarcraftSim/actions/runs/42944848428) on Feral's initial Skyborne selector/sidebar label mismatch. The one-line display correction preserves canonical keys, defaults and unavailable-baseline rejection. The numerical fixture now explicitly selects Night Elf instead of assuming it was the default. Tests using the actual race catalog add 330 identity/setup checks and cover blank/invalid inputs and explicit supported-race recovery. Final-r5 passes all local gates, including 5,834 .NET tests, Python 126+22, 19 Node suites and all HTTP regressions, with 66 source hashes unchanged. The failed run and observations are preserved. All 34 Feral browser gates and actual screenshot acceptance still require a new exact-head run.

### Rejected-import readiness follow-up

Head `39e9dc1ec87e20971cb1c9470fb25d9ac8538906` passed the first 15 Feral browser gates, including actual simulation, full report, replay and roundtrip, before [Actions 42947747882](https://github.com/layoung3/WarcraftSim/actions/runs/42947747882) caught a premature test export during legitimate rollback refresh. The test now waits for preparation matching the exact restored request; invalid drafts remain blocked, and local rejection may retain its current receipt. Runtime, strict validation and native click assertions are unchanged. Final-r6 passes all local gates and all 66 source hashes, with production/.NET tests unchanged. Failed evidence and both causal regression failures are preserved. Full exact-head browser and screenshot acceptance remain pending.

### Cross-class dynamic-control restoration follow-up

Head `aafa90e13211a2f225acf8060ee2d159fc805e99` passed 19 Feral browser gates and all 40 strict rejection exports, then [Actions 42950819895](https://github.com/layoung3/WarcraftSim/actions/runs/42950819895) found a real foreign-import rollback defect. A removed dynamic dropdown was restored before its class UI was rebuilt, aborting restoration. The fix rebuilds those controls before strict final raw-value replay. Presence-aware tests reproduce the old failure and cover 18 distinct valid/invalid drafts across all eight classes; the original browser assertions remain unchanged. Final-r7 passes the full local bundle and all 66 source hashes. The failed evidence is preserved. Full exact-head browser and screenshot acceptance still remain pending.

### Specific Feral comparison diagnostics

Head `c432380247ab3c93fba2a664ca87680c3a6bb28e` passed 29 Feral browser gates in [Actions 42956995971](https://github.com/layoung3/WarcraftSim/actions/runs/42956995971), including restored cross-class drafts, before a generic encounter warning failed the position-specific assertion. Position was retained and deltas correctly suppressed. The fix names changed Feral scenario fields and values while preserving comparison safety and the original browser assertions. Final-r8 passes the full local bundle and all 66 source hashes; ten retained complete comparisons differ only in additive diagnostic text. Nine available comparison screenshots had no independent visual blocker. Remaining native gates and full ready/results desktop/mobile coverage are still pending.

### Responsive Feral identity coverage

Head `066646c0a1b068a051b478b3408a7cf79edd6028` passed 33 Feral browser gates in [Actions 42961289868](https://github.com/layoung3/WarcraftSim/actions/runs/42961289868), including complete desktop captures, before the mobile recipe required an intentionally hidden desktop card. The visible header already supplies mobile identity. The test-only correction explicitly verifies/captures that header and all stats/scope while preserving desktop capture, strict geometry and hidden-region rejection. Source-backed 390/790/791 and negative regressions pass. Final-r9 passes every local gate and all 66 source hashes. All 24 available Feral screenshots had no independent desktop issue; full native mobile coverage and final pixel acceptance remain pending.

### Accepted partial fixed-Cat UI checkpoint

Exact head `437355ed2025474265e25ccc1711e4147b4e17ea` passes [Actions 42964941307](https://github.com/layoung3/WarcraftSim/actions/runs/42964941307), all 34 Feral browser gates and 40 strict rejected imports. Independent review reconciles six complete reports and inspects all 67 actual desktop/mobile Feral PNGs, including explicit mobile identity and every final content region. Source/data ownership, comparison suppression, rollback, resource precision and 36 allocated/15 unspent disclosure pass. Evidence and all earlier failed attempts remain preserved. This accepts only the historical-provisional partial fixed-Cat integration, not complete Feral mechanics or project completion. The minor shared “Hits” label for damage-event counts is tracked for the next grouped correction; source-backed Furor transition work continues separately within the existing runner.

### Furor transition correction and damage-event reporting

The existing Feral runner now applies the retained historical Furor rank formula to subsequent form changes, separately from authored startup resources. Actual signed/capped resource events, failed/no-op history and shared RNG behavior are covered by 215 new tests. Across 42 complete HTTP cases, 22 unaffected responses remain exact and 20 changed cases reconcile to the source formula. The shared report label is now “Damage events,” with explicit miss/zero-damage/tick semantics and unchanged numerical counts. Combined local verification passes strict Release, all 6,049 .NET tests, Python 126+22, 19 Node suites and every HTTP regression; all 1,070 frozen inputs match. Exact-head CI and fresh label-note desktop/mobile pixels are still required. This does not expand strict fixed-Cat UI admission beyond inactive Furor0/5 or certify current-client/tick fidelity, full Feral mechanics or overall completion.

### Accepted Furor/reporting checkpoint

Exact head `9fa45e4ac12cbba12b77688d9f2a5b2aedaa408d` passes [Actions 42969821423](https://github.com/layoung3/WarcraftSim/actions/runs/42969821423), all 6,049 .NET tests and 34 Feral browser gates. Fresh targeted desktop/mobile pixels verify every changed Damage events/note tile, with full native capture receipts and six unchanged fixed-Cat raw responses. The prior full UI review and direct-transition Furor numerical evidence remain distinct. The shared label follow-up is closed; strict fixed-Cat admission and historical-provisional limits remain unchanged. Bounded Combat Rogue backend integration is next, preserving Assassination and legacy behavior before any separate UI admission.

### Partial Combat Rogue backend stage

Eleven historical Combat keys now support legal 31 allocated/20 unspent and 30/21 variants through the existing runner, strict validation and preparation preview. The exact historical Precision cap 3 bridge leaves legacy cap 5 and Assassination unchanged. Admission is fixed sword/sword, single-target, no incoming attacks or utility, with owned request/map validation and existing baseline provenance. Lightning Reflexes, Puncturing Wounds and Endurance are inactive; Endurance's source 30/60% versus legacy 10/20% mismatch and provisional off-hand/proc/formula semantics remain disclosed. The source is retained decoded historical content, not raw-byte or current-client certification. Existing model binding may discard unknown nested typed fields; unsupported shared-envelope latency controls reject. There is no Combat UI/importable preset, `default-itemized` or simulation-discovery exposure.

Final-local-r3 passes strict Release, all 6,163 .NET tests, Python 126+22, importer smoke, 19 Node suites and every prior HTTP gate. New Combat captures pass both 119-request standalone mode and 310-request mode with all 191 frozen requests. The same 37 oracle tests (32 synthetic plus five retained-real) pass against each mode, not 74 distinct tests. All 836 retained request/response body hashes, 1,045 frozen source inputs and ten binaries match. Independent source, receipt and documentation review found no remaining scoped blocker; the implementation ledger records the exact final receipt/hash.

Earlier `green-r5`/`final-local-r1` HTTP evidence used a weaker identity oracle and is retained as history, not final parity proof. Corrected full-replay `final-local-r2` predates the standalone retained-test fix; that test-tool failure is preserved separately from runtime evidence. The final oracle restricts GUID normalization to five actual timeline event fields, preserves literal identity anchors and type distinctions, and uses exact Decimal values. Only the explicitly expected `sourceCommit` metadata stamp may differ; checkout provenance remains exact. Published head is still `9fa45e4`; this stage needs a new exact-head Actions run after publication and is not overall completion.

The workflow is unchanged, so run the new local gates explicitly against a locally running API. These standalone commands capture 119 requests and run 37 oracle tests without needing the untracked baseline. Use a fresh output directory and the API's actual build stamp, not a guessed published identity:

```sh
python3 tools/rogue-combat-historical-http.py --base http://127.0.0.1:5142 --out /tmp/cp175-combat-http --source-commit "$CP174_SOURCE_COMMIT"
python3 tools/test_rogue_combat_identity.py --retained /tmp/cp175-combat-http -v
```

When the retained pre-change corpus is available, add `--baseline .task-evidence/rogue-combat-backend-20261111/baseline` to the HTTP invocation for 310 requests including all 191 frozen replays, then run the same 37 oracle tests against that fresh output directory. The baseline is local evidence, not an installed prerequisite or a claim that new Combat HTTP/identity checks already run in CI. Detailed scope and evidence history are in the implementation ledger.

### Accepted Combat backend checkpoint

Exact head `1cd4afde349d8093bbd8e08b8d4235ca08599529` passes [Actions 42977153618](https://github.com/layoung3/WarcraftSim/actions/runs/42977153618), all 6,163 .NET tests and all seven existing browser acceptance summaries, including Assassination 37 and Feral 34 plus 40 rejected imports. The reports archive is checksum/CRC verified; six artifacts match the head by metadata, but only that archive was downloaded. No new pixel inspection is claimed for this frontend-unchanged backend stage; prior accepted pixel evidence remains. Combat's local 119/310-request and 37-test proofs are not new CI jobs. This accepts only the bounded historical-provisional backend with its existing limitations, not Combat UI/default exposure or overall completion. Separate Combat UI integration is in progress and not yet accepted. The PR remains draft and unmerged.

### Combat UI integration, acceptance pending

The local implementation adds Combat selection and its eleven-key 31 allocated/20 unspent default, with legal 30/21 and other underallocations. Strict Precision remains capped at 3 while legacy cap 5 and Assassination remain unchanged. Current flat-preview preparation gates Run/Save/Export; invalid or unavailable inputs stay editable, and rejected imports restore exact raw drafts. Dwarf is the initial sorted race; Human fixtures select Human explicitly. Reports use execution-owned preparation and reject invalid candidates before replacing A/B. Full parsed-JavaScript replay and exact-Decimal HTTP comparisons are separate proofs. Inactive effects, provisional mechanics, decoded historical source confidence and fixed-sword/single-target limits remain visible; there is no new engine, formula change or complete/optimized-build claim.

Current-source final-local-r2 passes strict Release, all 6,169 .NET tests, Python 126+22 plus eight stage-regression cases and 38 identity cases in each HTTP mode, 19 prior Node suites plus five Combat suites, importer smoke and every prior HTTP gate. New evidence includes 50 UI calls/106 exact-Decimal proofs, two actual production-projected reports with full parsed replays, 133 report checks and 194 A/B rejection checks. The accepted-backend replay preserves 306 of 310 complete responses, with exactly two default, one note and one source-stamp transition; seven discovery contracts pass. All 1,060 frozen source inputs and ten binaries match, and independent review finds no code/evidence blocker. The ledger records the exact final receipt/hash. R1 and the clean-checkout `FileNotFoundError` remain history; only the pure stage test changed to remove its untracked-file dependency, without altering runtime or full HTTP comparisons.

The 29 native Combat gates and 27 rejected builds are mandatory harness coverage, not a current browser pass. Workflow configuration/permissions are unchanged; the existing capture script adds Combat checks without removing prior class gates. Publication, a new exact-head complete CI run, actual runtime within the existing 20-minute job budget and changed desktop/mobile pixel review are still required. The accepted backend checkpoint does not accept this UI stage or complete the assignment.

### Combat UI capture follow-up

Head `b0699615b496f691441002add43a1fb630322b15` fails [Actions 42984473427](https://github.com/layoung3/WarcraftSim/actions/runs/42984473427) at an obsolete Assassination mobile scope selector, before Feral/Combat browser execution. All 6,169 .NET tests, comparison/Elemental/Balance/Shadow/Enhancement browser summaries and Rogue HTTP 53 passed, but full Rogue/Feral/Combat acceptance did not. All 28 available Rogue PNGs were inspected without another blocking layout finding; there are no Combat/Feral images, and the noted small truncation/wrapping issues are not claimed fixed.

The local correction captures the same six scope paragraphs, preserves the strict helper, and displays truthful selected-preset availability without changing raw capability/report data. New native desktop/mobile captures require the corrected note and all eleven expanded Combat talent rows, with complete text and closed-state restoration. A single Combat-only `overflow-wrap:anywhere` rule is preventative for long source tokens, not a claimed observed-overflow fix. Capture caps and deadlines are unchanged.

Final-local-r3 passes strict Release, all 6,169 .NET tests and the complete prior/new Python, Node and HTTP gates; Combat support now has 133 offline checks. All 1,061 source inputs, ten binaries and eight correction inputs match after normal shutdown. The retained heartbeat-delay warning caused no failed/skipped gate; the ledger records the immutable receipt/hash. Publication, new exact-head complete CI, native browser coverage, actual runtime and corrected pixel inspection remain pending. Failed evidence is preserved; this is not UI acceptance or overall completion.

### Green CI, Combat caption acceptance blocked

Head `b967b237e86e8b03ed713373d7590d8c26209ec8` passes [Actions 42990679994](https://github.com/layoung3/WarcraftSim/actions/runs/42990679994), all 6,169 .NET tests and all eight browser suites, including Combat's 29 gates. Combat takes 148.285 seconds; the job takes 13m34.339s, within existing limits. All eleven expanded talent rows are captured at both sizes and closed again.

Visual review of 117 files finds one blocker: the correct Combat 31/20 allocation is preceded by stale Assassination 38/13/twelve-key caption text. Green CI therefore does not accept the UI. A narrow caption correction preserves the separate current allocation and all historical/provisional caveats, with production-renderer and native capture assertions for 31/30/zero/early/reset states.

Fresh final-local-r4-r2 passes the zero-warning strict rebuild, all 6,169 .NET tests, every prior Python/Node/HTTP gate and 158 Combat support checks. All 810 Combat/stage/report requests, 1,541 body hashes, 1,061 source inputs, ten binaries and five changed inputs verify. The initial R4 path-enumeration failure occurred before build/tests and remains separate from this successful run; the ledger records the immutable final receipt/hash. Corrected-source publication, new exact-head CI/runtime checks and corrected desktop/mobile allocation pixels remain pending. Prior evidence is retained; no complete/optimized build or overall completion is claimed.

### Early Mage navigation follow-up

Head `218e2c1535c0d70924ea57591854e2ea28c4b66f` fails [Actions 42996250943](https://github.com/layoung3/WarcraftSim/actions/runs/42996250943) before Combat: a Talents click reaches a `DIV` and leaves Results active. Strict build, 6,169 .NET tests, four owner tests and nine early routes passed. Three early reports and 16 reviewed screenshots are preserved, but none captures the failed frame or Combat. The precise transient remains unresolved; this is not a proven Combat regression.

A three-file test-tool correction waits for pending sidebar preparation at that Mage boundary, uses the unchanged strict single-dispatch helper and preserves the original navigation postcondition. Ten actual-source causal tests use modeled delivery/geometry, not a native browser. Product code, the Combat caption and both strict shared helpers are unchanged.

Final-local-r5 passes the zero-warning rebuild, all 6,169 .NET tests/334 focused tests, all prior gates, ten mandatory navigation checks and 158 Combat support checks. All 810 indexed request statuses, 1,541 body hashes, 1,062 source copies, ten binaries and three changed tools verify; the ledger records the immutable receipt/hash. The prior native trigger remains unresolved. Publication, new exact-head CI/runtime and corrected Combat pixels remain pending; this does not accept the UI or complete the assignment.

### Accepted bounded Combat UI checkpoint

Exact head `fa53137e8194e7c84dd84fdae74c54f3ef265cae` passes [Actions 42999863967](https://github.com/layoung3/WarcraftSim/actions/runs/42999863967), all 6,169 .NET tests and all eight browser suites. Combat's 29 gates include 27 rejected builds, 25 preserved report rejections, complete report/replay checks and the corrected allocation caption. Combat takes 145.328 seconds; the job takes 12m30.778s within unchanged limits. Pixel review freshly inspects 22 images and carries 99 previously inspected Combat images by exact byte identity; all expanded details remain covered and no scoped visual blocker remains. The earlier Mage transient is not explained, but the full current native run passes.

This accepts only the historical-provisional partial Combat UI: 31 allocated/20 unspent, 30/21 without Adrenaline Rush, legal underallocations and visible inactive/unsupported/source limitations. It is not full Rogue mechanics, an optimized build, current-client certification or assignment completion. The next bounded Rogue mechanics stage is in progress; this acceptance record travels with its next cohesive commit. The PR remains draft and unmerged, and all prior evidence is preserved.

### Bounded Rogue damage corrections: local pass

The [source and verification audit](../../audits/CP175-rogue-subtlety-source-corrections.md) bounds the next stage to Ghostly Strike's 125%/180% explicit main-hand coefficient and Hemorrhage's 15% live own-target/own-Rogue Rupture bonus. Legacy call compatibility, unrelated dagger gates, direct Hemorrhage damage/defaults, strict admission and UI remain unchanged. Historical decoded source is not current-client certification; strict Subtlety and a complete or optimized build remain unavailable.

Four 120-second direct HTTP defaults at 80 ms do exercise Rupture and legitimately gain 43.2 damage/0.36 DPS each; exactly 40 causal fields change, including descriptions, with all unrelated fields preserved. Earlier no-Rupture defaults and zero-latency goldens are separate evidence.

Final local review passes the zero-warning rebuild, 210 focused/all 6,379 .NET tests, 25 retained Python checks and 24 clean checks with one optional skip. Standalone smoke passes 127 requests/91 causal fixtures; separate old/new comparison passes 127 complete responses, and the existing corpus preserves 810 raw responses plus two separately parsed reports. All 1,066 source inputs and ten binary paths/four assemblies match frozen manifests. The ledger records immutable receipt SHA-256 `c5d48da72cb6eb16e7df3780c0e2fe08d617afe3c31c35763355a397f164b901` and independent review. These are parent-stamped working-candidate results, not a clean new-head build. Publication and new exact-head complete CI/browser acceptance remain pending; no local browser run, strict Subtlety acceptance or overall completion is claimed.

### Accepted bounded Rogue damage checkpoint

Exact head `5580292a315ffe66baf16534982186cf1dabb55d` passes [Actions 43008077207](https://github.com/layoung3/WarcraftSim/actions/runs/43008077207), with checkout merge `52ff66137361fc5e371d44a3a3dc1970665debfe` sharing tree `add89ebbf1cbf865cf98812afe6e26fc144c6805`. All 6,379 .NET tests, including 210 new cases, and all eight browser suites pass: comparison 11, Elemental 29, Balance 28, Shadow 32, Enhancement 20, Assassination 37, Feral 34 and Combat 29. Combat takes 147.495 seconds; the job takes 799.373398 seconds within unchanged limits.

Independent review verifies all 51 named successful report stamps and a 127-request/91-causal-fixture corrected HTTP gate. All 127 complete CI responses match the frozen corrected local capture after only the verified identity/default-ID/provenance allowances, including the four corrected long-default Rupture cases. The ledger records acceptance receipt SHA-256 `1815b926c66eb685c2aa83fd0b7efe87d4518a1b249dc82bc6a1b566b2b485e9` and its independent evidence. No fresh PNG review is claimed: UI assets are unchanged and the earlier accepted Combat visual proof remains separate. This additive acceptance preserves the historical local-candidate entry and audit. It accepts only the two historical/provisional damage corrections, not strict Subtlety, full Rogue mechanics or assignment completion. The PR remains draft and unmerged.

### Bounded Cutthroat correction: local execution pass, acceptance pending

The [Cutthroat source and contract audit](../../audits/CP175-rogue-cutthroat-source-corrections.md) records three literal production changes in two files: cap 3 to 5, chance 20% to 3% per rank, and opportunity 12 to 10 seconds. Existing positive saved ranks 1–3 intentionally change on rerun, without remapping or a legacy mode. Otherwise-valid ranks 4/5 become accepted through legacy direct/preparation paths and both Assassination HTTP previews. Strict shared/Combat-preview/import/UI admission continues to reject every Cutthroat key, including zero; Subtlety HTTP preview remains unavailable. Defaults, tuning, schemas, rotation, Core, strict catalogs and UI remain unchanged; decoded historical source does not certify current-client behavior or every preserved lifecycle convention.

Frozen local execution passes the zero-warning strict rebuild and all 6,505 .NET tests. The identical 126 focused cases turn 42 intended failures green and retain 84 passing controls. Python/importer, 19 earlier plus five Combat Node suites and all preserved HTTP gates pass. New full HTTP proof covers 382 old/new cases: 308 unchanged complete responses under narrow allowances; 40 causal cases with 39 literal-probability first causes and one wholly unchanged case; and 34 intended 400-to-200 admissions, comprising 16 complete legal-rank-zero control comparisons and 18 trace proofs. Separate same-phase replay preserves all 382 complete envelopes. Prior damage 127 envelopes and 810 existing raw responses plus two separately parsed reports remain exact.

The 58 corrected trace analyses include one zero-proc short fixture; their aggregate 3,303 Backstabs/282 opportunities/272 non-Stealth Ambushes are not a direct comparison with the old 40-trace totals. Separate 63-request/15-trace smoke passes in 8.537734415 seconds and supplies positive proof for every rank 1–5, without replacing the full local proof. The ledger records immutable local receipt SHA-256 `788a4f62929ba2619d6bddf25db5cc338d33153eb4fa1baa1f12bc62e0ff6b7a`, 40 linked records, passing independent pre-hook review, 1,213 frozen execution inputs and ten binary paths. The API is stopped. Assemblies still name accepted parent `5580292a`; this is a source-stamped working candidate, not a clean final-head build.

Failed setup/capture attempts remain preserved, including the first capture's missing original client bytes and the second capture's offline Decimal serialization failure followed by separate analysis and replay. The later eight-line CI hook has its own passing exact-command receipt, SHA-256 `aa51f35f00cbf257dcfbd7d7da9d96d279e58cfafeb15337f5d1b7667f19e847`: 41 pure tests and 63 corrected responses/15 traces, with smoke time 10.981801662 seconds, separate from the initial 8.537734415-second smoke. The full suite and full HTTP aggregate remain linked pre-hook results, not rerun or relabeled; the rest of the driver was not run locally. Final execution-input/tool/binary/historical-receipt checks pass and the API is stopped. Independent final post-hook/prose review, publication and new exact-head complete CI/browser acceptance remain pending. No fresh pixels, complete or optimized build, broader admission or overall completion are claimed.

### Accepted bounded Cutthroat checkpoint

Exact head `765c6c43267443ebf2ea12e66c40699626d94791` passes [Actions 43019623997](https://github.com/layoung3/WarcraftSim/actions/runs/43019623997). Checkout `ed4b728457e940fee87939085b13718363d376b4` shares tree `1b533254875755e6a2fe9dbd2afe9c4b5045adb7`. The zero-warning/error build, all 6,505 .NET tests and all eight existing browser suites pass. New Cutthroat smoke passes 63 requests/15 traces; all 63 complete envelopes and the prior damage gate's 127 envelopes match their corrected local captures under only verified narrow allowances. Combat takes 146.768 seconds and the job 806.454651 seconds within unchanged limits.

Acceptance receipt SHA-256 is `a89507ce44304dc09b4c4761ae8123dd30b80b4d7a6fdbcd7177cae5ea329cad`; the ledger links the retained archive and independent review. This accepts only historical/provisional cap 5, 3% per rank and a 10-second opportunity. It explicitly excludes the old blanket report-admission statement: Assassination's generic report inspector is consistency-only, distinct from strict execution/build import and Combat's source-confirmed allowlist. The narrow audit wording correction and separate hardening follow-up are included with the next cohesive stage. Prior local entries remain history; unchanged UI does not imply fresh pixel review. No strict Subtlety, full Rogue or overall completion is claimed.

### Dirty Deeds Energy correction: local pass, acceptance pending

The [Dirty Deeds audit](../../audits/CP175-rogue-dirty-deeds-source-corrections.md) confines production changes to one shared reduction and the two existing Garrote/Cheap Shot costs. At missing/0/1/2, Energy costs are **50/50/40/30** and **60/60/50/40** respectively. Historical decoded tooltip evidence establishes the absolute 10/rank reduction at cap 2, not independent certification of the local base costs or current-client behavior. Garrote executes in all three legacy runners; Cheap Shot has no runner priority and its execution proof is in-process only. Defaults, tuning, priorities, strict admission, UI and saved reports stay unchanged.

From the clean 6,505-test baseline, the identical 137 focused cases turn 41 intended numerical failures green and preserve 96 controls. Corrected strict Release has zero warnings/errors, all **6,642 .NET tests pass**, and the full Python/importer/24 Node-suite aggregate passes. The ledger records full pre-hook receipt SHA-256 `75e8d26233ec9533f4c2708961e3deb322e91f4db7d9e4868dcaf57ecbef5286`. Earlier focused and HTTP harness failures remain preserved and distinct from valid product-red evidence.

The 313-case old/new matrix has 143 complete unchanged envelopes, 157 independently reconstructed resolver/trace causal proofs, eight bounded arithmetic/structure proofs and five malformed-binding diagnostics. All 165 causal cases identify 93 literal-spend or 72 affordability first causes. Twenty-four isolated cases change only five exact cost-related fields. Corrected replay has 308 complete equalities plus five diagnostic-only comparisons. Broad poison/encounter RNG is not independently reconstructed; those cases have complete seeded replay. Damage deltas are 72 positive, 79 zero and 14 negative, so this is not a blanket DPS increase. All 127 damage, 382 Cutthroat, 63 smoke and 810 existing raw envelopes plus two parsed reports remain preserved. Separate smoke passes 41 requests/34 traces/68 Garrotes in 5.845515523 seconds; the API stops normally at 13:26:02Z.

Focused interrupted-completion controls prove executor no-spend/no-aura behavior, not successful-opener observer metrics or Cheap Shot CP semantics. HTTP files initially named `source-death` are incoming-damage/actor-health-preservation controls because the runner preserves health; actual source-death rejection is focused executor evidence. The fourteen-case reader receipt remains unchanged, with no report-inspector hardening or saved-report migration.

The initial 1,094-path candidate freeze mistakenly excluded all tracked docs. A separate 122-file supplement, including 92 historical JSON inputs, verifies unchanged clean-baseline bytes **after .NET/during HTTP**, with post-HTTP and before/after aggregate checks. It is not retroactive pre-build coverage; the audit preserves the exact limitation and original receipts. Assemblies still identify the accepted parent, so this is a source-stamped working candidate, not a clean final-head build.

The later eight-line capture-driver append has separate passing exact-command receipt SHA-256 `c70374cadc0ee592fbc2dc03afdefef572fa311da5c0500c70eb65f78414423d`: 43 pure tests and 41 requests/34 traces/68 Garrotes in 6.058382428 seconds. All 41 complete envelopes match the earlier smoke. The final 1,216-input manifest includes historical JSON and excludes exactly four owned prose files; source/binary/anchor checks pass and the API is stopped. Only the appended commands ran, not the whole driver, browser prefix or a rebuild. All aggregate results above remain pre-hook; none is relabeled as a post-hook rerun, and the original manifest timing limitation remains explicit. Final independent hook/prose review, publication and new exact-head Actions/browser acceptance remain pending. No local browser run, fresh pixels, new crowd-control/enemy-AI support, strict Subtlety, complete Rogue, current-client certification or overall completion is claimed.

### Accepted bounded Dirty Deeds checkpoint

Exact head `c1c599199d2628c1943f3252b24861ac5a740329` passes [Actions 43031237502](https://github.com/layoung3/WarcraftSim/actions/runs/43031237502); checkout `80e91476bcb5f944905d61d6bf77ff9cfbfe70f2` shares tree `3812ede4f8e5ddc404f6dfce80f9127d9abb2669`. The zero-warning/error build, all **6,642 .NET tests** and all eight native browser suites pass. All **231 complete local/CI response comparisons** pass: Dirty Deeds 41, Cutthroat 63 and prior damage 127. Dirty Deeds smoke has 34 traces/68 Garrotes in 6.380206361 seconds; Combat takes 147.769 seconds and the job 860.1520442 seconds within unchanged limits.

Acceptance receipt SHA-256 is `9176f73285e4fde91a7a55fe32b93983a93a81e50540be7a8f20d6518ca93858`; the ledger records verified artifacts and independent review. This accepts only the historical/provisional 10 Energy/rank correction, preserving the prior local-manifest timing limitation, in-process-only Cheap Shot proof and separate report-reader gap. No fresh PNG review is claimed for that unchanged UI. Earlier local pending entries remain history; the PR stays draft/unmerged and this is not full Rogue or overall completion.

### Historical Assassination report admission: local pass, acceptance pending

The [report-validation audit](../../audits/CP175-rogue-assassination-report-validation.md) defines the separately reviewed follow-up: enforce the twelve-key historical rank/row/parent contract, exact saved/full/typed maps including supported zeros, and bounded identity/allocation receipts at report admission. Legacy raw APIs, strict simulation/build import and A/B comparison retain their distinct boundaries. Optional descriptive/source metadata gains no new rejection or comparison rule; valid v1 38/36/other legal allocations, null visual provenance, numeric-string encounter controls and explicit 17.25/zero/custom tuning remain supported. This is internal consistency checking, not server attestation or blanket report integrity.

Corrected r2 passes independent source review, **19 positive/311 negative controls, 35 pure DOM transaction scenarios**, nine existing frontend suites and a 60-original retained-report sweep with unchanged comparisons/metrics. Six files in that sweep are from a failed-CI cohort, not six acceptance runs. Old source admitted 301 invalid cases and already rejected ten. Initial r1 missed direct/nested array selected-key coercion; six added type controls and one strict string check resolve it. R1 evidence remains superseded, not hidden or relabeled.

The accepted-head baseline freezes all 1,220 tracked paths, including all 126 docs and 92 docs JSON inputs. Final r2 freezes 1,223 inputs with only three named prose exclusions, plus 14,337 external anchors; all 92 docs JSON remain included. Its strict rebuild has zero warnings/errors and all **6,642 .NET tests**, the full Python/importer aggregate and **24 existing Node suites** pass, with API/Core/Data DLL bytes unchanged from baseline. Raw preservation passes 308 complete plus five diagnostic Dirty Deeds cases, 127 damage, 382/63 Cutthroat and 810 older raw responses plus two separately parsed reports; the separate 41-case Dirty Deeds smoke also matches completely. Only the existing narrow identity/source-stamp allowances apply; raw and parsed proofs remain distinct.

Local receipt SHA-256 is `9dfb620af7ebc4c12feb9755b60837ae00373e34290b5a21cc9d5293f43b217b`. Source/binary/anchor and retained baseline/r1/r2 snapshot checks pass; the API stopped at 14:30:03Z. R1 ran its strict rebuild and 6,642 .NET tests but no aggregate non-HTTP/API/browser gates; it remains superseded by corrected r2. Final independent review, publication, exact-final-head Actions/native browser acceptance and fresh error-state pixels remain pending. Browser additions are syntax checked only, and injected no-file/read-failure cases are not native OS chooser/filesystem evidence. No final report-reader acceptance or local browser pass is claimed. The earlier accepted probe remains historical evidence of the original defect.

### Accepted historical Assassination report-admission checkpoint

Exact head `6fae3e8a00f0d9f5f33ffc78a024555d91e460d5` passes [Actions 43042933479](https://github.com/layoung3/WarcraftSim/actions/runs/43042933479); workflow checkout `d018a3fba6183ae15a8ee3fd5a93d11c6384a916` has the same tree `bf479ed05831ecece7156d6e5cccd60f23a29288`. The strict build has zero warnings/errors, all **6,642 .NET tests** and all eight native browser suites pass, including **66 Rogue cases** and independently checked encounter-editor coverage. Independent artifact review verifies **231 complete local/CI raw-response comparisons** and **51 reconstructed complete exported combatSummary objects**, spanning the named positive reports rather than only Combat-spec Rogue reports. These are separate raw and parsed proofs, not 282 raw comparisons.

Acceptance receipt SHA-256 is `7fcf064a2bca50e6c4874bc445e110609566a9e606861d7fb14250c8f89bf18c`; its path and independent review are recorded in the ledger. Six actual fresh desktop/mobile images were inspected for rejection readability and preserved comparison, including three mobile preserved-results tiles. This is not a claim to have inspected all 422 images. The job took 854.9828417 seconds and Combat 148.486 seconds within unchanged limits, with no required gate skipped.

This accepts the bounded historical talent/allocation-consistency guard. Assassination reports now reject unsupported keys, including Elusiveness at zero, in all three rank maps; Combat retains its separate allowlist. The older weak-reader probe and pending entries remain historical evidence, not current behavior. Genuine older reports, supported zeros, legal unspent allocations, optional provenance, explicit tuning and existing comparison decisions remain supported. The guard does not attest to report authenticity or provenance. The six inspected images belong to this report stage, not to the following mechanics stage. PR 28 remains draft/unmerged; no full Rogue or overall completion is claimed.

### Elusiveness cooldown correction: released scope, baseline rebuilt

The [Elusiveness source and contract audit](../../audits/CP175-rogue-elusiveness-source-corrections.md) records the reviewed one-product-file correction: subtract the historical **45/90 seconds** from both Vanish and Blind at unchanged cap 2, preserving their provisional local bases. Missing/0/1/2 cooldowns are intended to become **300/300/255/210** and **180/180/135/90 seconds**. Vanish is in all three legacy DPS priorities and can enable a real later damaging opener; Blind is genuinely in the utility-on priority. Readiness is measured from the actual successful start, not simulation origin, and no uniform DPS improvement or new enemy-control behavior is promised.

The accepted report-stage head above is the baseline. The initial workers stopped after its snapshot, without a product/test/tool edit or rebuild result. On resumption, all **1,226 source files, 14,428 external inputs and 165 preserved prebuild binary files** matched their retained/live records. A fresh strict accepted-head rebuild then passed with zero warnings/errors in **48.80 seconds**; actual rebuilt binaries and informational versions were retained. This is baseline build evidence, not a new focused/full-test pass or corrected-mechanics acceptance.

Valid old-code numerical failures and complete old HTTP replay/causal-contract review remain pending before production editing. Corrected focused results, full local regression, independent final review, publication and exact-final-head CI/native artifact acceptance also remain pending. Only this README, the ledger and the new audit may be excluded from future execution manifests; historical JSON and original report fixtures remain covered. No new pixel review is claimed or required by this mechanics-only scope unless UI changes arise. No defaults, tuning, strict/report admission, routes, priorities, Core, processor, frontend or source data are changed by the approved correction.

### Elusiveness correction: focused and raw HTTP pass, aggregate pending

The earlier baseline entry remains history. After independent old-evidence approval and coordinator release, the product change is exactly **30→45** and **15→45** in the two existing Elusiveness cooldown expressions. CP147 changes only 240/150 to 210/90, retaining Dirty Tricks' 20-Energy assertion. The identical **105 focused tests** move from **68 passing controls and 37 intended old-code failures** to **105 passing**, with strict Release zero warnings/errors. Actual isolated scheduling moves the second Vanish from 272.25 to 257.25 at rank one and 242.25 to 212.25 at rank two, from the same first start 2.25; rank zero stays 302.25. This is not a full-suite result.

Corrected pre-hook receipt SHA-256 is `dfde15b9a70ebeb4579a5efeaa665663f123e49b4c808a11a504abff20324810`. Each corrected 110-case capture/replay has 44 trace proofs. Replay passes **105 complete exact envelopes plus five typed-binding diagnostics**; old/new comparison has **76 complete unchanged envelopes, 29 cooldown-causal cases and five diagnostics**. All six all-spec/rank window cases demonstrate a real later Vanish and 400-damage Ambush; repeated fixtures exercise four natural Vanishes. The 29 damage deltas are **16 positive and 13 negative**, not a universal DPS improvement. Broad RNG evidence remains bounded arithmetic/lifecycle plus full seeded replay, not independent reconstruction of every proc or hidden scheduler count.

Earlier invalid focused fixtures and checker omissions remain retained. The stronger old checker requires missing spend/effect/aura events to fail. Corrected r4 then fixes a verification-only ordering assumption: an already queued owned auto can deal damage after Vanish completes but before its queued aura notifications, breaking Stealth before a later opener in the low-Energy case. No engine or priority change was made, and no raw response was recaptured. Separate parsed-report tests preserve 21 positives/four comparison controls and reject 24 Elusiveness-zero/positive mutations; they are not raw-wire equality evidence.

The standalone 19-request smoke passes 11 traces, two duplicate comparisons and missing/zero equality in **0.687490398 seconds capture plus 15.252151439 seconds analysis**; 37 self-contained pure tests also pass. The hook is still unapplied in this receipt. Retained baseline/candidate binaries, selected inputs and separately frozen three-path prose checks pass; the API stopped normally at **16:11:40Z**. These are parent-stamped pre-hook results, not a clean final-head build. Full aggregate/preservation, any approved hook with a new freeze, final independent review, publication and exact-final-head CI/native artifact acceptance remain pending. No fresh pixels, broader mechanics or overall completion are claimed.

### Elusiveness correction: final local post-hook pass

Final local receipt `.task-evidence/rogue-elusiveness-corrections-20261111/final-local-aggregate-receipt-r2.json`, SHA-256 **`2886f53f28478136fe32a53756a419417d17f52807afa38e70c46ed22b98151d`**, records a fresh zero-warning/error strict Release build, **6,747/6,747 .NET tests and 105/105 focused cases**, with none failed/skipped. All 25 listed Node suites, Python/importer suites, importer smoke, syntax and whitespace checks pass. The optional retained-input check is skipped only in the damage suite's clean mode; its retained mode passes all 25 checks.

The exact reviewed driver append is now applied and executed under the sole-owner API, with **37 pure tests, 19 responses and 11 traces** in **15.332995991 seconds**, including two duplicate equalities and one missing/zero equality. The full driver/browser prefix did not run locally. A new complete final freeze and aggregate include that append; earlier pre-hook results remain separate history. Final full/replay captures each pass 110 cases/44 traces; corrected replay has **105 complete equalities plus five diagnostics**, and old/new has **76 complete unchanged responses, 29 causal cases and five diagnostics**, retaining the 16 positive/13 negative damage split.

Preservation passes 810 earlier raw responses plus two separately parsed reports, 127 damage, 382/63 Cutthroat, and 313/41 Dirty Deeds cases. The 313-case Dirty Deeds gate is **308 complete equalities plus five diagnostics**. All 60 retained original report inputs and parsed metrics/comparison outcomes remain unchanged, including six artifacts from a failed historical CI cohort. Original baseline copies, **1,227 final source inputs, 26,206 external inputs, eight execution tools**, separately frozen three-path prose and live/retained binaries pass custody checks. The API stopped normally at **16:47:34Z**; final offline checks also passed. The initial receipt recorder undercounted smoke traces using planning flags; its failure remains retained, with no failed project execution or recapture hidden by r2.

This closes local post-hook execution only. Assemblies still name accepted parent `6fae3e8` and the API uses its explicit local Elusiveness stamp. Final independent publication/prose review, publication and exact-final-head CI/native artifact acceptance remain pending. No local browser run, new pixel review, current-client/base-cooldown certification, broader mechanics or overall completion is claimed. PR 28 remains draft/unmerged.
