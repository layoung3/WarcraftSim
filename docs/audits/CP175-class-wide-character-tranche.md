# CP175 — Class-wide character execution: Shaman, Rogue and shared inputs

## Legacy Rogue compatibility repair — November 13 qualification

The published tranche at `a2aa5f45ba0fff11b4585f5623f616dcf8837303` passed
8,318 .NET tests,82 class-wide HTTP checks and31 native class-wide browser checks
in workflow43293109074, then failed legacy Combat report admission. That workflow
failed; it is not pending or accepted. The retained actual report contains all31
historical request fields and five added inactive DTO fields, rejected by the old
exact-key list. Local production-function execution reproduced that error.

The bounded repair shares legacy Rogue request/tuning guards in the existing
frontend assumptions module. It admits only absent/exact inactive defaults for
the five additions, deeply validates complete modifier/item objects, and enforces
all52 historical tuning fields and bounds for both legacy specs. The sole nullable
compatibility field accepts only absence/null; every non-null value is rejected.
Genuine old-build HTTP execution also reproduced a second defect: server binding
materialized that null in preparation while the submitted tuning omitted it.
Only a fresh outgoing tuning copy is adapted; saved inputs remain unchanged.

Comparison projections ignore only validated inactive serialization distinctions.
Combat keeps exact owned-request/duplicate receipts and strict source policy.
Historical Assassination keeps advisory source metadata with an explicit visible
source/reference difference caveat. Full-response replay identity is unchanged;
no generic default stripping, missing/null normalization or numeric tolerance was
added to replay. Existing required fields, rank/weapon restrictions and negative
fixtures remain enforced.

The [qualification receipt and complete old-build reports](../acceptance/cp175/rogue-legacy-compat/README.md)
record exact local commands and source hashes: strict Release,8,318 .NET tests,
22 Node entry points (including936 compatibility cases and52 A/B checks),
359 Python passes/3 optional skips, existing importer execution and real HTTP
acceptance. Native Chromium remains locally blocked by its socket operation.
The original implicit importer build attempt is recorded as failed, separately
from the passing explicit serial build and actual importer smoke. Publication
and the complete exact-head Actions outcome belong in the existing PR #28
delivery receipt; local checks alone do not mark the tranche accepted.

## Baseline and ownership

This implementation continues `cp175-shared-character-simulation` and draft PR #28 from `b21d567c97a44ebc5527953b259c1ad3caa6bbc8`, with successful [workflow 43254621489](https://github.com/layoung3/WarcraftSim/actions/runs/43254621489) and 8,113 .NET tests. The previous Shaman core checkpoint and older ledger entries are history. Rasta's recorded repository-write pause remains in effect; no historical patch was reapplied. The coordinator reconciled the current checkout and remote before editing. Workers used disjoint files and detached validation copies; only the coordinator publishes.

The local environment provides .NET SDK10.0.404, Python3.12.14, Node24.22.1, repository reads/writes and actual local API execution. Serial MSBuild is required because this environment denies the parallel named-pipe path. Local Chromium cannot launch because a required socket operation is denied. Local HTTP and JavaScript checks are therefore distinct from the native Chromium acceptance in the existing CI job. No new branch, PR, deployment, release, merge, force-push, paid service or artifact deletion is part of this pass.

## Shared contract and visible workflow

`/class-build.html`, linked from the existing Solo DPS planner, uses `/api/forever/shared/class-builds`. It does not change the meaning of old typed/historical exports. The new `warcraftsim.class-build` version1 envelope carries the existing shared character configuration: class, race, level, naked-baseline evidence, itemized ranks, equipment, enchants, modifiers, inventory, encounter, policy and class assumptions.

The three questions remain separate:

1. **Legality:** canonical talent keys, rank caps, actual parent ranks, earlier-tier investment and point budget. Legal partial allocations and unspent points are accepted. Missing effects do not make a rank map illegal.
2. **Execution coverage:** explicit implemented, partial or unimplemented effects and applicable scenarios. Selected missing effects require acknowledgment before a disclosed partial run. Pending equipment remains editable/exportable, with execution blocked by its actual evidence problem.
3. **Source confidence:** current published client records, official dated overrides and historical/provisional values retain their own provenance. Passing code tests does not certify live-server numerical values.

**Apply talent preset** replaces only itemized ranks and the provenance label. Race, gear, permanent/temporary enchants, inventory, modifiers, encounter, class settings and custom priority survive. A custom priority is revalidated; it is not replaced. Automatic policy is regenerated from effective talents and weapons. **Reset entire character** is a separately named, confirmed operation.

The editor shows all three trees, current caps and prerequisites, source links, per-node coverage, prepared statistics and actual priority entries. Verified local historical artwork is displayed only when the existing receipt-based icon endpoint advertises the asset; unmatched nodes have a visible text fallback. Artwork does not upgrade mechanical confidence.

Build import requires explicit version/identity/ranks, rejects unknown and case-aliased duplicate fields, and validates before replacing state. Server-defaulted transport fields are returned canonically without repairing ranks or removing pending items. Malformed null collections, invalid ranks and invalid reports preserve the existing build/report. Reports contain the original build, prepared snapshot, exact adapter request, coverage, source stamp and complete simulation result.

Report import checks its duplicated preparation/coverage/request snapshots and damage consistency. Browser-exported decimal numbers are compared at their exact IEEE754 transport representation, not with an arbitrary tolerance. Deterministic replay separately compares every browser-decoded simulation field and the relationships among generated event IDs; .NET formula tests retain decimal arithmetic. An imported report is explicitly unverified until replay, rather than being treated as an authenticated combat receipt.

## Shaman: all three trees in one engine

`UseClassWideTalents=true` routes to `ShamanClassWideSimRunner` regardless of preset origin. The current catalog has **50 nodes:16 Elemental,18 Enhancement,16 Restoration**. All50 have explicit bounded effect consumers and applicable-scenario descriptions. This is not evidence for every legal combination, raid geometry or a complete healing/tanking workflow.

The common spellbook connects lightning, Shocks, fire totems, imbues, melee, healing and utility. Current rank changes affect preparation, actions, costs, procs, cooldowns and automatic priorities. Examples include:

- Elemental Clearcasting, Overload, Lightning Mastery, actual pushback resistance, shared Shock cooldowns and range gates.
- Enhancement Mental Dexterity/Quickness, live AP/SP, actual hand imbues, Stormstrike, current Maelstrom costs/cast reservations, Flurry/Farseer scheduling and actual avoidance-triggered resets.
- Restoration cast/cost/hit/threat adjustments, exact Mindfulness ranks, Water Shield, Mana Tide, Nature's Swiftness across damage/healing, Ancestral Healing armor, Riptide and Chain Heal.
- Focused Earthbind/Earthbound, Stoneclaw health/interception, Ghost Wolf, Windwall/Grounding, Frost Shock slow, Flametongue Totem, Totemic Call relocation and Reincarnation scenarios.

Utility uses explicit one-dimensional movement, susceptible targets and incoming-attack controls. Default bosses remain control immune. Ghost Wolf weapon attacks, complete party geometry and full healer/tank UIs remain separate scope. Existing Restoration code and shared processors are reused. The shared Fire Totem lifetime behavior is explicitly opt-in so the accepted legacy55-second event contract stays unchanged.

**Trial by Fire:** the new mode executes both Fire Nova changes and real Magma duration/extra ticks. The historical Elemental/Enhancement workflows retain their correctly disclosed unavailable Magma component. Current published spellbook data supports Fire Nova226–252, midpoint239; the new mode uses239 and records the older historical midpoint245 disagreement. A provisional coefficient is not a missing mechanic.

Reviewed software starters are42-point Elemental+Restoration,48-point Enhancement+Restoration, and31-point Restoration focused effects. They deliberately retain9,3 and20 unspent points. They are sourced legal witness allocations, not claims of community popularity, optimality or a manufactured point total.

## Rogue: Assassination, Combat and Subtlety

The current54-node allocation uses one `RogueSimRunner` class-wide path, including the strict shared Subtlety workflow. Actual weapons and positions determine Mutilate/Backstab/Ambush eligibility. Preset origin does not select a spellbook or priority.

The pass connects the November rework, including Shank, Flash of Steel, Boot Knife, Composure/Poised, Venom, current Mutilate, Ghostly Strike, Hemorrhage and Death Mark. Existing resource, combo-point, poison, cooldown, defense, aura and live-stat systems are reused. Incoming attacks now reduce real Health in opt-in class-wide mode, permitting actual healing-item consumption. Default legacy behavior and golden fingerprints are retained.

Current per-node coverage is **50 implemented,2 partial,2 unimplemented**. Master of Deception's detection and Camouflage's stealth movement/re-entry remain unimplemented. Heightened Senses executes incoming avoidance but lacks its detection component. Quick Getaway executes the additional Sap target but lacks its movement component. These gaps remain legal, visible and disclosed; they are not called inactive.

Three reviewed legal starters allocate actual ranks:31/20/0,10/31/10 and10/10/31. Their30-second automatic witnesses execute Mutilate; Blade Flurry/Adrenaline Rush; and Hemorrhage/Death Mark respectively. Neither these witnesses nor the74 new cases certify every legal build.

## Shared equipment and statistics

`ForeverSharedCharacterInputs` feeds the existing catalog resolver, modifier pipeline and dynamic equipment processor. Raw stats enter once; each runner applies its own talent multipliers and derived conversions afterward. Gear/enchant removal is reversible. Manual budgets, naked observations and catalog-derived contributions are separate, and catalog items cannot override their server-owned stats.

Four narrowly pinned published-client records provide usable representative armor without changing the older ItemSparse gate:

| Item | Explicit class mask | Recorded required level | Supported path |
|---|---|---:|---|
|18452 Sergeant Major's Leather Armsplints|Rogue|58|Class-wide Rogue, supported wrist enchants|
|18434 First Sergeant's Dragonhide Armguards|Druid|58|Prepared Balance runner, supported wrist enchants|
|19829 Zandalar Augur's Belt|Shaman|0|Class-wide/ prepared Shaman|
|19830 Zandalar Augur's Bracers|Shaman|0|Class-wide/ prepared Shaman, supported wrist enchants|

Recorded level0 is preserved, not replaced by an invented60. The Shaman two-piece4MP5 bonus contributes once. Unsupported higher set thresholds remain gated. Source confidence is provisional. Acquisition/faction availability is not inferred from names or Classic records.

The new provenance kind, `published-client-item-record`, requires exact positive masks, classification, no fallback marker, pinned record hashes and the full source-file/build reference. Missing masks are not unrestricted. The **79 pre-existing items** remain pending in these newly strict paths when their restriction evidence is insufficient. Weapon proficiency and dynamic-effect eligibility remain independent questions.

Talisman of Ephemeral Power's metadata now agrees with its already modeled runtime; coefficients and timing are unchanged. Its class restriction evidence is still pending in these new paths. Supported existing equipment-effect witnesses exercise activation, expiry and live-stat consumption without a duplicate precombat contribution.

Selected self modifiers apply once. The explicitly supported target subset is Faerie Fire, consumed from live target Armor with actual expiry/maintenance. Other target modifiers remain blocked until their complete consumers exist. Rogue additional cleave targets retain their own Armor.

## Current inventory and completion backlog

The API and UI expose all nine classes and all27 tree names. Prior talent-audit counts are historical, not a current70888 completeness claim. The current tranche reconciles the Shaman/Rogue trees; other classes retain their existing workflows and a per-tree completion backlog.

| Class / all trees | Engine and UI now | Shared inputs / items | Rotation / batch | Remaining integration or evidence |
|---|---|---|---|---|
|Warrior:Arms,Fury,Protection|Existing unified typed engine, stance/weapon/protection processors|Existing gear/support bridge, modifiers, dynamic effects and combat items|Existing live priority and bounded batches|Reconcile current all-tree source and remaining declared effects; maintain real stance/shield gates and separate tank workflow|
|Paladin:Holy,Protection,Retribution|Metadata/reference only; no executable class engine|Not connected|Not connected|Next class: implement all three trees and the established shared contract|
|Hunter:Beast Mastery,Marksmanship,Survival|Existing three unified typed workflows and pet integration|Existing catalog/enchants/modifiers/effects/items|Existing priorities and batches; stale capability flags corrected|Current source reconciliation and remaining range/control/defensive/pet interaction evidence|
|Rogue:Assassination,Combat,Subtlety|Current class-wide shared editor/engine|New published wrist/enchant path, manual inputs, modifiers, effects/items|Live automatic/custom priority and paired comparisons|Four explicitly incomplete talent components; broader weapon evidence and interaction coverage|
|Priest:Discipline,Holy,Shadow|Restricted Shadow UI; healing/cooldown code exists separately|Unified gear/modifiers/items incomplete|Existing Shadow policy; new class-wide/batch contract missing|Reuse healing/defensive code for legal off-tree DPS allocations; current-node reconciliation|
|Shaman:Elemental,Enhancement,Restoration|Current class-wide engine; focused Restoration/utility scenarios|Published waist/wrist/enchants/set, manual inputs, modifiers/effects/items|Live automatic/custom policy and paired comparisons|Broader interaction/source evidence, other target debuffs and racial actives; full healer/tank UI separate|
|Mage:Arcane,Fire,Frost|Existing three unified typed workflows|Existing catalog/enchants/modifiers/effects/items|Existing priorities and batches; stale capability flags corrected|Reconcile current all-tree source and remaining declared defensive/utility effects|
|Warlock:Affliction,Demonology,Destruction|Existing three unified typed workflows and demons|Existing catalog/enchants/modifiers/effects/items|Existing priorities and batches; stale capability flags corrected|Reconcile current all-tree source and remaining pet/defensive/control consumers|
|Druid:Balance,Feral,Restoration|Restricted Balance/Feral UI; Restoration runner separate|Prepared Balance now supports sourced wrist/enchant; strict UI and fixed Cat catalog integration missing|Existing typed policies; class-wide comparisons missing|Complete Cat AP consumers, form/weapon prerequisites and Restoration cross-tree integration|

Missing code, missing adapters and missing evidence are separate API fields. The inventory never infers complete effect execution from a named node or a few passing presets.

## Comparison and replay foundations

Each completed adapter supports2–40 paired comparison seeds and2–40 separate validation seeds, at most160 actual runs, with cancellation propagated to the engine. The baseline seed is constrained to a nonnegative range that leaves room for both cohorts and avoids the negative-seed alias in `System.Random`. Validation seeds begin at baseline+1,000,003. Every run prepares a fresh isolated build.

Comparisons require matching effective target mitigation/Health, duration, survival and relevant encounter settings. Omitted versus explicit equivalent defaults and decimal spellings compare semantically. Samples, distributions and paired DPS differences are retained. The uncertainty interval uses the Student-t0.975 quantile with N−1 degrees of freedom and sample standard deviation of paired differences, including small-sample uncertainty. [NIST table](https://itl.nist.gov/div898/handbook/eda/section3/eda3672.htm).

This compares chosen builds/policies under their disclosed model. It is not a full optimizer, live-server certification or universal optimality claim.

## Source references

- [Official November2 development notes](https://us.forums.blizzard.com/en/wow/t/wow-forever-beta-development-notes-%E2%80%93-updated-november-2/2360696).
- [Official November10 tuning announcement](https://us.forums.blizzard.com/en/wow/t/class-tuning-incoming-november-10/2404092).
- [Current Shaman tree](https://theforeverera.com/en/talents/shaman/) and [spellbook](https://theforeverera.com/en/spells/shaman/), build1.60.1.70888.
- [Current Rogue tree](https://theforeverera.com/en/talents/rogue/), build1.60.1.70888; raw HTML SHA256 `09fe460693c5b48ab708e229c38deb8078950f9021af0a658e4c617e66350ead`.
- [Current Mutilate record](https://foreverdb.net/spell/1241584) and [Death Mark record](https://www.wowhead.com/forever/spell=1325758/death-mark).
- [Published item chunk](https://theforeverera.com/data/items/chunk-9.json), build1.60.1.70888; SHA256 `079016994c9ee6f4076dcf4ecf3cbd8ccedb33567c99242e12b0ed703eafe7a3`.

## Acceptance

Worker-isolated verification passed: Rogue2,026 tests (74 new), shared inputs292 tests, Shaman894 tests (62 new), each with strict Release compilation and no skips. These counts overlap surrounding suites and are not added together as a full-suite total.

Final integrated local verification passed a forced strict Release rebuild with zero warnings/errors and **8,318 .NET tests, zero failures/skips**, including **205 added cases** over the accepted baseline. All **33 Node suite invocations** and **82 real HTTP checks** passed. The intended Python entry points executed **362 cases: 359 passes and three optional historical-retained-input skips**, with no errors/failures. The reviewed-intake audit for the 79 older registered IDs and actual .NET importer integration smoke passed separately; new published-client records retain their own validation. The HTTP run retains six complete build/report witnesses, source/coverage snapshots, malformed-import/report rejection, origin-independent deterministic replay and reversible gear inputs. The six exact build files are linked from the [acceptance README](../acceptance/cp175/README.md#class-wide-character-acceptance--current-tranche).

An actual 45-second Elemental+Restoration Lightning Bolt comparison adds sourced Shaman waist/wrist items 19829/19830 and wrist enchant 1220624 at 30 yards. Eight training seeds improve by a paired mean **35.68 DPS**, with an approximate 95% interval **29.75–41.61**; eight distinct validation seeds improve by **36.02 DPS**, interval **31.85–40.19**. All16 paired differences are positive. Full chosen inputs and samples are retained in `class-build-http/elemental-restoration-gear-comparison.json`. This demonstrates the tested equipment path under its disclosed assumptions, not universally optimal gearing.

The first integrated full-suite run exposed one stale catalog-art-count assertion after the four intentional additions. The corrected test preserves the original 20 identities and checks the four new records explicitly; the final full-suite result above supersedes that failed attempt. The final exact-published-head CI/native artifact verification belongs in the coordinator's delivery receipt and existing draft PR #28. The CI browser job now exercises current three-tree allocation, talent-only preservation, actual gear/enchant effects, invalid-import rollback, build/report files, complete replay, paired comparisons and desktop/mobile rendering. Local Chromium remains unavailable, and merely adding those tests is not a browser pass.
