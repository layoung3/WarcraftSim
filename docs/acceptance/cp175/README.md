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
