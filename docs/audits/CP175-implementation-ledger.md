# CP175 implementation ledger — checkpoint 1

Branch: `cp175-shared-character-simulation`, draft PR #28.
Base: CP174 merged into main at `ad92d8b35fb3145f6816d86953c15852a7c2d283`.

This ledger explicitly separates functioning code from proposals; an endpoint returning capability metadata does not imply that its class adapter executes those features.

## Implemented and tested in this checkpoint

1. **Stage A structural model:** `ForeverSharedCharacterConfiguration` combines race, class, level, full equipment with enchants, itemized ranks, external modifiers and target upkeep, combat inventory, racial active, encounter, rotation and class-specific JSON. `POST /api/forever/shared/validate-envelope` enforces *structural* gates only and explicitly returns `readyForUnifiedSimulation=false`. It does **not** replace the runtime-specific talent and equipment validation.
2. **Capability discovery:** `GET /api/forever/shared/capabilities` advertises honest Mage, Warlock, Hunter, Warrior current capabilities and restrictions. The frontend consumes this metadata for existing class encounter controls. Warrior is shown but disabled in the character dropdown until its full typed adaptation is certified.
3. **Shared item policy:** `CombatUseItemsOptions.RestorationPolicy` defaults to `conservative-no-overcap` for currently shared gem/rune/rage/healing items. The predicate uses **maximum restoration from actual ability definitions**, not duplicate guessed numbers. Health emergency exception requires explicit opt-in. `legacy-half-mean` remains explicit; all charges and shared cooldown groups still go through the engine. `Remaining` inventory counts are now available in the processor. Regression tests exercise each boundary and ensure no phantom items. **Important**: Mage's older dedicated Major Mana Potion and some other pre-existing caster potion processors have not yet moved into this policy; do not assert universal no-overcap compliance.
4. **Dedicated Rotation tab:** Existing executable caster priority inspector/order, supported thresholds, reset and custom actions moved intact from Encounter to a separate top-level Rotation tab. Hunter currently has limited per-runner custom priorities without the shared preview contract; capability metadata keeps those controls from claiming parity.
5. **Live stats:** Manual Refresh removed. Backend `generate-level60` is invoked through a 180ms debounce and token-guarded stale response rejection. Unreviewed race/class baselines are blocked using the actual coverage catalog; the user must explicitly select Historical baseline to preview provisional values. On accepted changes, stats and attribution update without JavaScript stat formulas.
6. **Statistical Results:** `DistributionSummary.Percentile99` extends existing proven Warrior batch statistics. All class stats use population SD and sorted linear interpolation at `p*(N-1)`, disclosed in result JSON/UI. `POST /api/forever/shared/simulate-iterations` accepts 1–100 iterations for strict Mage/Warlock/Hunter typed builds, executes independent seed = `unchecked(baseSeed+i)` with separate engine state, and re-runs the actual median-nearest representative seed with timeline. The same endpoint can route an explicit Warrior typed configuration to the existing `ForeverWarriorSimRunner` batch, preserving rage/tank and gear behavior. Warrior batch now retains actual sample DPS values and seeds.
7. **Results UI:** A compact readable summary includes mean DPS, population SD, mean±SD, median, P95, P99, minimum, maximum, iterations and duration. Single-run mode is explicitly marked as deterministic debugging and does **not** imply statistical accuracy. JSON export includes per-seed observations and representative response; batch replay validates the exact seed sequence and sampled DPS. The pre-existing ability table and raw timeline remain.

## Verified but still incomplete or unsupported

- **Full Warrior character-builder adapter — not done.** Its support catalog, equipment bridge, true talent tree and stance/rotation settings are still separate. Backend seeded statistics work using the typed Warrior request; the disabled Warrior character option is not a functional builder.
- **Arbitrary cross-tree execution — not done.** Shared envelope structural validation cannot certify a 21/21/9 build's combat behavior. Current typed Hunter runners can reject selected modeled cross-spec talents. Keep explicit "not executable" capabilities until a shared talent executor is proved.
- **One unified modifier catalog — not done.** Warrior's support catalog remains separate. No data migration is claimed. The existing CP173 food selections do contribute backend stats for supported caster builds, but food/scroll/oil combinations still need a four-class stacking/effect census.
- **All consumables under one scheduler — not done.** Mage dedicated Major Mana Potion (1350–2250), other caster potion processors, and unsupported Healthstones are not generalized. A max-restore policy must also be applied to those processors before declaring universal no-overcap. Sapper/Dynamite cooldown relationship is still provisional; inventory/cooldown availability is actual engine logic.
- **DPS survival separation — not done for every class.** Warrior solo tank already uses fixed-duration nonlethal scoring, whereas other classes spend actual health and may die. The new structural envelope refuses unimplemented nonlethal/survival policies, rather than silently granting free healing.
- **Rotation UI all four classes — not done.** Existing Mage/Warlock rotation controls run; Hunter/Warrior visual policy editors and class-specific rotation descriptions still need native adapter integration and accessibility validation.
- **Detailed results sections — partial.** Statistical metrics and ability breakdown work. Critical/miss resource views, buff/debuff uptime charts, procs, cooldowns and readable class-specific event timeline controls need dedicated tabs/tables; raw JSON remains accessible under Advanced.
- **Icon coverage — unchanged from CP174.** A source-labelled talent/icon cache exists, equipment has provisional exact-key mappings and neutral fallbacks; no icon images were newly approved for external modifier tiles or generic consumables. The external catalog has source spell/item IDs but does not have a verified per-tile icon mapping yet. Do not infer art solely from a name or unverified ID.
- **Warrior-specific Encounter controls — partial metadata only.** No shared editable stance, starting Rage, sacrificial demon, Hunter starting Aspect, or totem builder has been certified across the four classes.

## Gates for further development on this SAME draft PR

**B:** Reconcile support/item catalogs and 1350–2250 Mana Potion + Healthstone policies, no-overcap for *all* supported restore items; test actual food stacking/durations and charge limits across all classes.

**C:** Adapter mapping and rank validation for arbitrary cross-tree legal builds across all three trees. Verify learned abilities affect real combat and custom rotations invalidate when talents removed.

**D:** Extend separate Rotation tab with ability artwork, supported conditions and key/mouse reordering for Hunter/Warrior; class-derived Encounter sections (starting Aspect/demon/rage/stance) must run in backend.

**E:** Make detailed Results readable beyond current compact statistics: damage/casts/crit/miss/regen/uptime, consumable and pet contribution charts; preserve full source and provisional warnings.

**F:** Expand only source-licensed icon cache with exact spell/item icon identifiers; report verified/provisional/unresolved counts; retain neutral fallback.

**G:** Native four-class character-builder adapter incl. Warrior equipment/stat/talent/support parity and true DPS nonlethal/health risk semantics where requested. Then full browser and cross-class acceptance.

All new work remains on PR #28. Never merge without explicit owner approval.

## Stage B1 source checkpoint (pending CI acceptance)
Prepared Major Mana Potion (1350–2250 mana) and five non-improved Healthstones (120/300/600/960/1440 fixed health) use the shared item scheduler and source catalog; potion and conjured-stone cooldown categories are separate. Mage legacy explicit potion fields are adapted; the old separate Mage spellbook/rotation executable potion was removed. The four classes' existing shared item processors can access eligible items. UI now exposes the inventory. This is not yet certified by a new exact-head CI or browser suite. Improved Healthstones, further modifier unification and cross-tree execution remain pending.

## Stage B2-A — shared modifier bridge (CI qualification required)
- Warrior now accepts the same `ForeverExternalModifierSelection` as Mage, Warlock and Hunter, using the CP173 source-labelled definitions for new buffs/consumables/debuffs, without changing existing Warrior support presets.
- The Warrior bridge normalizes CP173's melee attack-power stat key at the class adapter boundary and rejects legacy/CP173 duplicates and known cross-catalog exclusive-group conflicts. Existing Warrior-only support records stay in the versioned legacy catalog until independently verified common sources replace them.
- The existing shared target-debuff aura processor now runs on Warrior, so selected CP173 debuffs follow initial-only or maintained actual durations instead of static indefinite armor reduction; pre-pull legacy Warrior debuffs retain existing semantics.
- Food, flasks, elixirs and other CP173 stat modifiers continue using the same contributor pipeline. Permanent equipment enchant execution remains separately owned by the equipment pipeline. No claim is made that arbitrary Warrior equipment, all scrolls or unverified temporary enhancements are migrated.

## B2-B — static shared food/enhancement validation (pending exact-head CI)
- Shared modifier application now rejects a second application of a pre-existing enabled source-labelled modifier on an already materialized character profile; disabling/unselecting still removes it at source.
- Numerical tests cover existing source-backed Well Fed effects on Mage/Hunter, the same backend duration gate (900 seconds), Forever-compatible multiple elixir categories, same-food/same-elixir exclusion and distinct permanent/temporary weapon enchant slots (item 19102, permanent 1219580, oil 25122). No scrolls were added without executable sourced records; no temporal reapplication is implied.

## B2-C — shared item quantitative parity (pending CI)
- Added deterministic item tests for one vs. two prepared stackable Major Mana Potions with 120-second shared cooldown, cross-family healing-potion cooldown exclusion, exactly reconciled mana/healing event counts, and typed Warlock potion execution without Life Tap or assumed external healing. Previous tests cover class-wide fixed Healthstone, gem/rune one-use limits, engineering and Warrior rage items. These tests do not assert unmeasured Hunter mana behavior or universally certified server cooldown policies.

## C-A — Warlock target-priority cross-tree execution (pending CI)
- Removed unsupported preset-name restrictions on the already executable Affliction/Destruction additional-target priority editors. These commands now gate by actual learned rank, supported action and configured target rather than specialization label.
- The existing source-owned DoT spread policy may operate on off-tree configurations; no new effects or damage formulas were fabricated.
- Numerical acceptance requires actual off-preset Siphon Life and Incinerate execution on an additional enemy, and rejection after removing the required talent. This does **not** certify unrelated Hunter typed-runner cross-tree ability registration, Warrior protection actions or all 51-point allocations.

## C-B — Warrior cross-tree active capstones (pending exact-head CI)
- In tree mode, a learned Mortal Strike or Bloodthirst is now registered via its existing factory even if the initializing Warrior preset differs; default and saved custom priority eligibility consult ranks. Legacy preset behavior is unchanged.
- A concrete 31 Fury / 20 Arms level-60 allocation on an Arms starting preset must execute Bloodthirst and lose the action when the talent is removed. The separate Battle Stance-only Spearing Strike, Sweeping Strikes, and Protection active interactions are **not** implied certified by this change.

## C-C — saved Warrior custom priority diagnostics (pending CI)
- In Warrior tree mode, enabled saved custom priority actions gated by unallocated talents are now reported explicitly in the simulation's modeling notes, while existing editable saved rows and skip semantics remain intact. This avoids silently implying execution for an unlearned action; the validated available-action editor for Warrior is still a future unified frontend stage.

## B2-D — Warrior real-health shared modifiers (CI qualification pending)
- Warrior solo and integrated actor HP now add the resolved additive `health` stat after the existing unbuffed/manual or race+Stamina base. This connects Flask of the Titans and Lesser Fortitude to actual actor maximum HP, not merely a stat preview.
- Deterministic regression verifies baseline 5000, Flask 6200 and Elixir 5120 actual simulated maximum HP, plus no request mutation and removing a flask restores the baseline. No fictional healing is introduced.

## D1 implementation checkpoint (CI pending)
Added authoritative Warrior builder preparation endpoint, source-backed Warrior talent metadata and ranked validation, equipment/enchants conversion, selectable initial stance, and unified capability advertisement. Warrior remains on the existing seeded runner; historical race baseline is explicit. The frontend and full acceptance work are subsequent D1/D2 stages.


## CP175 D1–D6 — unified Warrior, four-class Encounter/Rotation and browser acceptance

Stage D1/D2/G code is on the **existing** CP175 branch and draft PR #28. No new PR or protected-branch mutation.

- **D1 / Warrior:** `ForeverWarriorBuilderController` provides defaults, sourced three-tree talent metadata, exact tree-rank validation, source-owned equipment normalization, permanent enchant and duration-eligible static temporary enhancement bridge, Warrior stat preview, and real typed request preparation. The unified page enables Warrior, but retains the existing `ForeverWarriorSimRunner` for combat and seeded batch statistics. The starting race baseline is deliberately **historical-provisional** and must be explicitly enabled. No 51-point Warrior starter preset was fabricated: the unallocated tree is legal and ready for owner selection/import.
- **D2 / Encounter:** Executable Warrior initial stance aura, starting Rage, target Armor, optional stance dancing and boss attacks; Orc Blood Fury/Troll Berserking bridge through the runner's already validated `MajorCooldowns` rather than invented racial formulas. Mage Armor, Warlock Life Tap (with **no default external healing**) and optional demon sacrifice, Hunter Aspect and pet special/engagement settings map into the existing typed requests; unsupported settings reject rather than silently claim effects.
- **D3 / Rotation:** All four classes expose an action selector and ordered rows. Warrior custom rules use the existing `WarriorRotationRules` adapter, including Minimum Rage, target-health prerequisites persisted with saved rules, and server-backed preview; Mage/Warlock retain native priority conditions; Hunter uses each typed runner's available `CustomPriority` field. Mouse drag and existing accessible row Up/Down controls reorder priorities. Native talent/action validation is authoritative. Hunter's **full automatic resolved-priority inspector and per-action conditional editor remain provisional**, not falsely certified by static action discovery.
- **D4 / Cross-tree:** The prior C-A/C-B source-covered fixtures remain real executable comparisons: a legal 31 Fury/20 Arms Warrior initialized from Arms executes learned Bloodthirst, and an Affliction-starting Warlock executes off-preset Incinerate with valid Destruction ranks. Full class-wide arbitrary cross-tree coverage, especially Hunter typed runner registration, remains a separate gate.
- **D5 / Importable builds:** Six real `warcraftsim.solo-dps-build` v1 JSON files plus a guide are committed under `docs/acceptance/cp175/`. The automated Chrome loop imports each through the actual file input, verifies class, race, selected talents and equipment, checks custom priority order, re-exports and compares build fields, simulates and checks actual cast-completion events, exports combat reports, and replays the same seed. It captures six rotation screenshots as CI artifacts.
- **D6 / Evidence:** The six-file browser gate passed at commit `451b26f7220c600029809f1e55f2672a6f40048e`, GitHub Actions **#41283967753**, with 2,685 xUnit tests, 54 Python tests, a zero-warning Release build and existing nine-spec browser route checks. Recorded browser combat confirmations: Warrior `bloodthirst`, Mage `arcane-blast`, Warlock `incinerate`, Hunter `aimed-shot`, Warrior mixed `bloodthirst`, Warlock mixed `incinerate`. Screenshot artifact `cp174-ui-viewport-captures`, ID 13074218966. The newer race-control and this documentation commit require independent **exact-final-head CI** before a final merge gate could even be considered.

Remaining limitations: Legacy Warrior support catalog and the source-backend gear bridge are not completely unified with the shared native character builder; unreviewed item/enchant procs, complete Hunter action preview and conditional editors, all cross-tree mechanics, additional class-specific support such as Shaman totems, and reliable tank/survival scenario policy separation are **not certified**. The browser screenshot and build import tests are evidence of the explicit tested paths, not of every possible 51-point distribution. PR #28 stays draft and unmerged until owner acceptance.


## CP175-F1B — first reviewed accessory data import (partial, 2026-11-01)

- Existing PR #28 and branch retained. This stage adds **7 new unique combat-catalog item IDs**, not the requested 40–60; the completion gate is **not met**. Prior catalog was 62 unique IDs; nominal post-import catalog is 69 IDs before resolver-specific eligibility filters. No inspect-only items count toward this delta.
- New independently reviewed published tooltip IDs: 21712 (Amulet of the Fallen God, neck; 70291), 23057 (Gem of Trapped Innocents, neck; 70532), 23058 (Life Channeling Necklace, neck; 70532), 23041 (Slayer's Crest, trinket; 70634), 23046 (The Restrained Essence of Sapphiron, trinket; 70425), 23047 (Eye of the Dead, trinket; 70425), and 23067 (Ring of the Cryptstalker, Hunter unique-equipped ring; 70338). Exact individual HTTPS references are attached to each item. These are **historical-build source observations**, not retested 70707 values.
- The unchanged CP171 embedded reviewed manifest passes through the existing snapshot importer and server-side shared equipment resolver; this does not create a second source of combat stats. Verified static numerical fields are retained. Known unsupported trinket on-use mechanics are explicitly linked as non-modeled. Cryptstalker ring has the existing set key and unique/class restriction; unmodeled set bonuses must not execute.
- **Quarantined during this batch:** item 23040 (Glyph of Deflection) was briefly staged then removed because its block-chance/block-value static properties lack a supported executable representation for this batch. Mark of the Champion variant 23207 was not imported: its conditional target-family bonuses cannot be represented safely as unconditional spell power. A lack of verified weapon damage/speed prevented promotion of weapon search results.
- No browser screenshots, numeric equipment-switch simulation comparisons, four-class replay proof or local full suite are claimed. New focused C# catalog contract tests are included; exact-final-head CI remains a separate acceptance requirement.


## CP175-F1B continuation — CI repair, CP172 item ownership and current-build data (2026-11-01)

- Fixed xUnit2031 via `Assert.Single(items, predicate)` (commit 845151c). Earlier CI then exposed a second startup blocker: reviewed ID 23041 duplicated CP172's independently executable Slayer's Crest. Removed the reviewed duplicate and preserved CP172's existing 64 AP + modeled item ability; the F1B historical +48 AP entry was not promoted or allowed to replace CP172. The **prior report of seven genuinely available accessories was premature**. Six of the originally staged IDs are distinct.
- Added **seven further individually published build 1.60.1.70707 items**, namely 288927 Quick Strike Ring, 288928 Heavy Dark Iron Ring, 288929 Band of Sulfuras, 288931 Ring of Spell Power, 21583 Cloak of Clarity, 21710 Cloak of the Fallen God and 23053 Stormrage's Talisman of Seething. Numeric values are retained exactly from published client-derived tooltips/catalog; never inferred from item level. The four low-level newly added rings display no required-level constraint; this is recorded as requiredLevel 0, not as level-60 equipment.
- Compared to starting 62 base+reviewed unique IDs, the reviewed manifest grows by **13 net IDs** (6 distinct historical accessories plus 7 70707 entries), before CP172's separate 4 registered items. This is not 20–30 **additional** IDs within this continuation. Source-specific usable filters and actual browser selection need exact-head verification; no screenshot or deterministic gear-switch numerical claim has been established.
- New automated pre-import check: `python tools/ForeverEquipmentImportCli/reviewed_intake_audit.py` scans all three registered item collections for duplicate IDs, missing slots/stat/effect evidence, HTTPS review attribution and reviewed weapon damage/speed completeness. New candidate workflow: (1) use published Forever catalog for candidate ID and inventory lookup; (2) acquire pinned wago Item/ItemSparse tables with CP171 downloader; (3) normalize with CP171 bulk_normalize and SHA-bound reviewed values; (4) collect each numeric item tooltip and its build URL; (5) keep no-value records quarantined; (6) populate reviewed manifest through existing pipeline; (7) run intake audit, strict build/xUnit/Python and Chrome selection tests; (8) compare runtime /api/forever/equipment/coverage and export receipts. Manual item tooltip review remains necessary until a permitted bulk final-value export exists.
- Item effects: historical 23046/23047 continue to disclose non-executing on-use semantics. The CP172 Slayer's Crest modeled effect remains in force; known uncertain hotfixes are not silently overridden.
- **Incomplete:** exact-final-head passing CI, browser screenshots, full four-class replay and numerical equipment-switch comparisons, and 20–30 additional source-reviewed DPS items. This is partial delivery; PR #28 remains draft and unmerged.


## CP175-F1C — automatic approved DB2 batch promotion (2026-11-01)

**Scope/results:** Functional batch-promotion plumbing added, but no new runtime item IDs in this iteration; previous 79 registered IDs remain the baseline. **CP175-F1C is not complete** until a genuinely sourced bulk dataset produces 25+ selectable records and four-class browser/combat receipts. Do not claim metadata-only candidates are available in the picker.

The repository already contains a pinned wago.tools client CSV downloader (`download_client_csv.py`) and a strict DB2 + reviewed-values normalizer (`bulk_normalize.py`). ItemSparse stat IDs, item levels and formula candidates **cannot** substitute for attested final numeric combat stats; the normalizer expressly quarantines them. The public AHledger open-data page advertises a catalog JSON endpoint with attribution and Bind on Forever provides build 1.60.1.70707 listings, but the current work did not retrieve and validate an independently licensed bulk **resolved-values** export. No inferred Classic or item-level damage/armor has been promoted.

New tool: `tools/ForeverEquipmentImportCli/promote_approved_bulk.py`, invoked only after the existing CP171 normalizer has emitted both a checksum-approved JSON batch and its matching receipt. It deterministically merges verified items into the **existing** embedded CP171 reviewed manifest, preserves sets and explicit overrides, rejects collisions with CP172 effect-owned items, and writes actionable per-ID rejection reasons. An exact same-source rerun is idempotent; `--write` is required to actually change the manifest. The existing `reviewed_intake_audit.py` continues checking cross-catalog ownership. Regression tests: `test_promote_approved_bulk.py`.

Typical commands, after obtaining the pinned `1.60.1.70707` client CSV files and a separately reviewed final numeric values JSON + checksum-bound `approval.json`:

```bash
python tools/ForeverEquipmentImportCli/bulk_normalize.py --help
# Supply the required --directory, --build, --source-url, --imported-at,
# --catalog-version, --resolved-values and --approval parameters and paths.
# Capture --output reviewed-batch.json and --report reviewed-batch-report.json.
python tools/ForeverEquipmentImportCli/promote_approved_bulk.py --batch reviewed-batch.json --report reviewed-batch-report.json
python tools/ForeverEquipmentImportCli/promote_approved_bulk.py --batch reviewed-batch.json --report reviewed-batch-report.json --write --output-report promotion-receipt.json
python tools/ForeverEquipmentImportCli/reviewed_intake_audit.py
python -m unittest discover -s tools/ForeverEquipmentImportCli -p 'test_*.py'
dotnet build WarcraftSim.slnx -c Release -warnaserror
dotnet test WarcraftSim.slnx -c Release --no-build
```

The build-pinned review attestation and source-table digests must be retained together with the normalized batch for independent review; a `--write` run should only be committed after rechecking import report, conflicts, runtime /api/forever/equipment/coverage and actual class eligibility. Browser screenshots/seeded simulation receipts **not obtained** in this stage.


## CP175-F1D — source feasibility gate (blocked, November 1, 2026)

**Decision: stop equipment bulk-source work and proceed to CP175-F2.**
See [the evidence report](CP175-F1D-bulk-equipment-source-feasibility.md)
for source endpoints, public usage conditions, attempted access, returned/documented
fields, unresolved client-value gaps, existing snapshot comparison and reopening criteria.

- Starting branch `cp175-shared-character-simulation` at `11afabf48d96900efcf2937f4c7178f51c3c2a29`,
  CI #41365649457 succeeded; existing PR #28 stays draft/unmerged.
- **Bind on Forever:** published build 70707 item pages include useful
  individually reviewed static stats, but published terms prohibit bulk
  copying/scraping and no authorized bulk resolved-values export/API
  was identified. No automated scraping or mirroring performed.
- **AHledger:** permits attributed API use within rate/cache limits,
  with item metadata and build-70707 catalog data. Its documented
  single-item schema does not contain stat/damage/speed values; actual
  class-filtered catalog JSON response could not be read under research
  access/robots restrictions. Cannot treat unknown fields as approved stats.
- **Existing CP171 evidence:** Wago CSV joins previously produced 19,267
  metadata-only candidates but no independently approved numeric batch;
  pinned tooltip observations are too small and some absent from CSV.
  Reuse `bulk_normalize.py` and `promote_approved_bulk.py` when a valid
  source becomes available; do not create another ingestion framework.
- **F1D results:** 0 candidate rows actually submitted for import,
  0 accepted, 0 importer rejections; registered ID count 79 before and
  after. No new equipment screenshots or numerical combat comparisons,
  because no new usable records were promoted.
- The source feasibility conclusion is **not** a claim no viable feed
  exists in the world: it means neither inspected provider supplied a
  validated, accessible, *permitted* resolved-value bulk feed on this run.
  Specific next evidence needed: approved bulk export/permission or
  documented API response with full build-pinned combat-value schema.
- **Acceptance status:** F1D followed the explicitly authorized blocked path.
  F1 remains an acknowledged equipment-coverage limitation; CP175-F2 is
  the recommended next implementation checkpoint. Do not merge PR #28.


## CP175-F2A — explicit consumable inventory and four-class modifier integration (2026-11-02)

**Scope:** Existing CP173 static modifiers and CP175 shared item scheduler, not new items or a separate system. Draft PR #28 on existing CP175 branch, unmerged. The previous F1 data-source limitation remains open and unmodified.

| Capability | Before F2A | After F2A implementation |
| --- | --- | --- |
| Mage prepared Mana Ruby default | `MageSimRequest.CombatItems.ManaRubyCharges=1`, potentially executing without builder selection | Zero by default. Only explicit prepared stock can execute |
| Major Mana Potion | Explicit shared charges supported; legacy Mage Boolean compatibility remains | Still opt-in; zero inventory never executes; existing shared potion cooldown retained |
| Five Healthstone ranks | Each unique, shared conjured-stone group, finite stock and source-backed healing | Preserved; each now also supports explicit **disabled while stocked** state |
| Runes, gem hierarchy, engineering explosives | Existing shared scheduler and cooldown/health policy | Preserved, with disabled inventory excluded from the ability registrations before combat |
| Mighty Rage Potion | Backend supported for Warrior, missing from Solo DPS combat-item controls | Visible for Warrior; absent for Mage, Warlock and Hunter (unsupported class) |
| Combat-use quantity/toggle | Toggling off erased stock; quantity >0 always meant enabled | Toggle preserves stock and records `combatItems.disabledItemKeys`. Distinguishes **enabled**, **disabled but stocked** and **no inventory**. Typed requests/export/import serialize the same state |
| Food, flask, elixir | 5 existing Well Fed foods, 3 flasks and 6 elixirs with server-enforced exclusive groups | Kept source-defined values and stacking; no new effect groups or speculative modifiers |
| External character buffs | Arcane Intellect, Power Word: Fortitude, Battle Shout and Blessing of Might; CP173 shared source conversion | Retained, selectable through existing UI and existing backend stat contributors; source duplicate prevention unchanged |
| Target debuffs | Faerie Fire and Curse of Elements, initial-only or maintained modes | Existing target-side auras retained; selected configuration now clearly reported with results |
| Configured effects reporting | Saved build and resolved stat contributions existed, but summary lacked an explicit selected-effects listing | Results show names of configured food/buffs/debuffs, and exported report retains selected keys (not falsely labeled as combat casts) |
| Warrior item reporting | Shared processor executed items, but Warrior response lacked consumed-item counts | Representative run now exposes `combatItemUses`, populates use receipts from the processor, and Solo DPS displays total consumed items |

**Engine rule:** `CombatUseItemsOptions.DisabledItemKeys` accepts only distinct known item keys. `CombatUseItemProcessor` does not register explicitly disabled stocked items. Stocked zero or absent items remain unavailable, unselected items do not materialize, personal and shared cooldowns remain executable engine rules, and successful casts decrement finite per-run inventory. This state is serialized as part of the existing `combatItems` envelope; no redundant consumable engine was created.

**Regression evidence committed:** extended `CP175_SharedRestoration.cs` to cover four-class disabled Healthstone, three-class disabled potion, invalid disabled-key rejection and no implicit Mage gem. Extended `CP175_WarriorSharedModifiers.cs` to compare Warrior active versus disabled Dense Dynamite and deterministic replay with use receipts. Existing CP173/CP174 stacking, rune, shared-cooldown and modifier tests remain in the full suite. `solo-dps-ui-contract.test.cjs` verifies independent inventory/enabled state and Warrior-only Rage Potion; `solo-dps-browser-interactions.cjs` exercises real Chrome inventory preservation and lack of a Mage default gem.

**Outstanding F2A acceptance:** representative four-class numeric food/buff DPS comparisons, a dedicated four-class screenshots set, and full owner import/export checks for *disabled stocked* inventory have not been separately captured during this checkpoint. Existing six CP175 acceptance fixtures remain unchanged. Exact-final-head CI must pass before this stage can be marked certified. The source-backed stacking restrictions, potion/rune priorities, and spell effects were not modified.


### CP175-F2A executable acceptance receipts (November 2)

- **Functional CI through `ba9a9817`:** strict Release, 2,733 full xUnit tests, CP173 modifier tests, Python suite, frontend DOM and real Chrome flow passed. The real Chrome test now captures `cp175-f2a-mage-disabled-stock.png` and `cp175-f2a-hunter-enabled-inventory.png` under the established `cp174-ui-viewport-captures` artifact ([run #41396676969](https://github.com/layoung3/WarcraftSim/actions/runs/41396676969), artifact ID 13118844325). This does not certify every consumable for every class.
- **Further Chrome acceptance added after that run:** export the disabled-but-stocked Mage build as actual JSON, verify `denseDynamiteCharges=3` and `disabledItemKeys` includes `dense-dynamite`, reimport via the actual file input, and ensure the disabled toggle and inventory both survive. This newer evidence requires exact-head CI.
- **Actual controlled CP173 same-settings numeric baselines preserved in [run #41396136248](https://github.com/layoung3/WarcraftSim/actions/runs/41396136248):** Mage Greater Arcane Elixir DPS **300.8009 → 313.3699**; Warlock Greater Arcane Elixir DPS **208.6653 → 217.2492**; Hunter Mongoose owner damage **15,471.16 → 16,354.11**, pet damage **5,651.39 → 5,783.56**. These are engine fixtures with fixed settings, not live-server measurements.
- **New controlled Warrior comparison:** `ExplicitBattleShoutIncreasesSeededWarriorDamage` tests selected Battle Shout versus no external buff at the same seed and asserts increased DPS plus deterministic replay. A dedicated detailed-logger CI step was added to publish the numeric Warrior values. The new `WarriorReportsActualConsumableUsesAndDisabledStockDoesNotExecute` test confirms one real executable Dense Dynamite use, zero when stocked-but-disabled, and equal replay output. Do not invent a numerical Warrior delta before exact CI logs are collected.
- **Test limitations:** Consumable generation remains the existing class-aware executor with provisional Forever server cooldown assumptions. Existing food, flasks, elixirs and target debuff definitions were preserved (no speculative expansion); the selected modifier labels are displayed with results, while actual combat item activation is counted from engine receipts. The older F1 equipment coverage remains open.


## CP175-F2B1 — source-backed enchant breadth and separate confidence/implementation indicators (November 2, 2026)

**Policy change (owner-authorized):** A documented, attributable historical or datamined numeric value may be simulated provisionally. It is **not** equivalent to confirmed build-specific data. Preserve source URL/build and any unresolved proc or set bonus; do not turn unknown values into invented bonuses, and do not bypass licensing. This applies to equipment, enchants, food/consumables, external modifiers, effects and future talent metadata.

### New permanent enchant batch

**31 existing registered enchants → 54** (23 new unique SpellIDs; existing starter ID 25080 Superior Agility was recognized and **not duplicated**). All 23 new records have dataConfidence=`provisional`, build label `1.60.1.70707`, independent source URL, and application date `2026-11-02`. Numeric bonuses come from [Method's October 31 Forever Beta enchant guide](https://www.method.gg/wow-forever/list-of-all-consumables-and-enchants-in-wow-forever) with individual linked Wowhead Forever spell IDs. These observations are **not independently verified** against current live server values. The existing legacy executable 13898 Fiery Weapon effect and all CP172 mechanics are preserved.

| Applicable slot | Before | After | Newly registered |
| --- | ---: | ---: | ---: |
| Neck | 2 | 4 | 2 |
| Back/cloak | 2 | 3 | 1 |
| Chest | 2 | 3 | 1 |
| Wrist | 10 | 13 | 3 |
| Hands | 4 | 6 | 2 |
| Feet | 2 | 3 | 1 |
| Main-hand (includes two-handed) | 9 | 17 | 8 |
| Off-hand (weapons, shields, held) | 5 | 15 | 10 |

A single weapon enchant may appear in main-hand *and* off-hand counts. Source item coverage remains limited: the two shield-only and three held-off-hand-only new enchant definitions are correctly typed but **cannot be called broadly selectable** when the current equipment catalog does not offer a compatible approved item. The **18 other new enchants** have compatible currently registered armor, accessories or weapons. Recheck class/race restrictions and item source status in the live picker before claiming every choice is usable for every class. The older F1 equipment source blocker remains open.

### Metadata and mechanics

- Catalog classes now store separate `dataConfidence` and `implementationStatus` fields; existing equipment/set metadata, external modifier projection and CP172 executable-effect status are extended without adding competing registries.
- Browser `sourceStatusInfo` and `attachSourceIndicators` render a subtle information marker for provisional/unknown data and a separate warning marker for partially or nonimplemented mechanics. Accessible hover, focus, click/tap and Escape dismissal are supported in details; buttons in option lists retain native tooltips and keyboard selection. Historical Frostfire set items illustrate **both** warnings simultaneously: individual stats execute, unimplemented set bonuses do not.
- The new enchant definitions are mechanically static-only; 22 are marked fully implemented for their known static stats, while `1248665` (+4 Defense) is partially implemented because full server-side avoidance conversion is not calibrated. This does **not** disable the +4 Defense attribute.
- New target flags `requiresShield` and `requiresHeldInOffHand` join the existing `requiresWeapon`/`requiresTwoHandedWeapon` validation. Shared and Warrior resolution reuse the same server check. The compatible-enchants endpoint now also enforces it. No additional item databases or proc models were introduced.
- Existing item data-confidence fallback is based on attribution/build. A published tooltip does not become independently verified merely because it is current-build. Existing historical and source-specific limitations remain visible and do not automatically prohibit safe static stats.
- Recorded potential source mismatch: starter enchant spell `20026` is labeled Major Health (+100 Health), while Method's beta table labels that spell Major Stamina (+10 Stamina). It was **not silently overridden** during F2B1. An independent current-client reconciliation is required.

### Tests and evidence

The expanded `CP175_F2B1ProvisionalEnchants` suite checks 23 IDs/values, source status, serialization, four-class raw stat deltas, two-hand/weapon/shield/held compatibility, and preservation of prior executable effects. `CP175_F2B1EnchantCombatReceipts` uses real seeded Warrior, Mage, Warlock and Hunter runners with controlled enchanted-versus-unenchanted comparisons and deterministic replay. Mage Arcane Mind increases the +9 Intellect enchant's total Mana contribution from base 135 to 148.5. Real Chrome checks select a new two-hand enchant and inspect its source indicator, then show both confidence and missing-set-effect popups on Frostfire Robe. Screenshots are stored with the existing CI `cp174-ui-viewport-captures` artifact; earlier six CP175 build imports and replay checks remain mandatory.

**Acceptance limitations:** Exact-final-head strict .NET build/full xUnit/Python/DOM/Chrome must succeed before claiming full completion; existing limited equipment means shield/held enchant classes lack tested selectable target items, and some Forever gameplay effects remain provisional. No new bulk equipment, talent, consumable or combat effect mechanics were added.


## CP175-F2B2 — source-backed food, elixirs, flask, buff, debuff and potions (November 2, 2026)

**Scope:** existing CP173 external modifier catalog / character stat contributor, CP175 shared `CombatUseItemProcessor`, and F2B1 confidence indicators. No duplicate catalog or class-specific item executor.

### Before/after distinct selectable definitions

| Category | Before | New | After |
| --- | ---: | ---: | ---: |
| Persistent food / Well Fed | 5 | 11 | 16 |
| Elixirs | 6 | 12 | 18 |
| Flasks | 3 | 1 | 4 |
| External buffs | 4 | 1 | 5 |
| External target debuffs | 2 | 1 | 3 |
| Combat-use item abilities | 16 | 2 | 18 |
| **Total** | **36** | **28** | **64** |

Numbers above count *distinct catalog keys*, not classes multiplied by ability slots. The 26 new modifiers have nonempty source-backed static numeric effects and use the existing selectable `/api/forever/character/modifiers` categories. The 2 new potions are finite-stock executable abilities in the shared combat item catalog and explicit Solo DPS controls. Neither adds unimplemented proc damage. All 28 new definitions are classified **provisional**; none is claimed independently verified by an observed 70707 server fight. `Curse of Recklessness`, `Greater Mageblood Elixir`, `Flask of Chromatic Resistance` and `Mark of the Wild` explicitly disclose **partially implemented** mechanics (respectively the target's +90 melee AP/control immunity, Hunter-specific MP5 resource handling, and unverified full resistance-mitigation mechanics). Other new entries are stat-only or direct-resource **fully implemented for the known numeric portion**, not a promise of universal game completeness.

**Provenance:** Published [Method WoW Forever Beta consumables and enchants reference](https://www.method.gg/wow-forever/list-of-all-consumables-and-enchants-in-wow-forever), October 31, 2026, compiled during the `1.60.1.70707` beta. The individual `ForeverExternalModifierDefinition.SourceUrl` points to either that guide or a named ForeverDB client-derived item page (for example [Swiftstrike Steak](https://foreverdb.net/item/250072) from an earlier pinned build, [Baked Salmon](https://foreverdb.net/item/13935), [Elixir of Cunning](https://foreverdb.net/item/250328), [Greater Mageblood Elixir](https://foreverdb.net/item/250341), and [Flask of Chromatic Resistance](https://foreverdb.net/item/13513)). These may represent **different historical client snapshots**, and the 70707 label indicates the guide's beta era rather than independent same-build attestation for every row. No item ID or numeric effect is derived from ItemSparse item level formulas. Known source changes need only data corrections.

**Current-build individual spell tooltips:** [Mark of the Wild Rank 7](https://www.wowhead.com/forever/spell=9885/mark-of-the-wild) shows +385 Armor, +16 all attributes, +27 magic resistances for 60 minutes. [Curse of Recklessness 1225841](https://www.wowhead.com/forever/spell=1225841/curse-of-recklessness) reports -640 enemy Armor, +90 enemy melee AP and cannot-flee/Fear/Horror limitations over 120 seconds. Only -640 Armor is applied to the target. Its other mechanics are **not** simulated or granted, and the UI shows a separate partial-mechanics warning.

**New potions:** [Superior Mana Potion item 13443](https://www.wowhead.com/forever/item=13443/superior-mana-potion) restores 900–1500 Mana, and [Superior Healing Potion item 3928](https://www.wowhead.com/forever/item=3928/superior-healing-potion) restores 700–900 Health. Both published Forever tooltips indicate two-minute cooldowns. Actual engine abilities inherit the existing `combat-potion` shared 120-second lockout, conditional deficit policies, finite stock, per-run decrement and explicit `DisabledItemKeys`. They are absent at zero inventory. All four UI classes can configure legal Health consumables; Mana potions are usable only when the actor actually has Mana. Potion effect sources and statuses appear through F2B1's reusable indicator.

**Stacking policy:** Inherited CP174 documented conservative Forever behavior persists: one Well Fed food; one flask; known overlapping Agility/Strength/Intellect elixir families exclude each other; independent named elixirs may coexist. This **does not adopt an unsupported TBC 1 Battle + 1 Guardian system**, despite conflicting beta guide language. The single external curse category prohibits simultaneous Elements/Recklessness, and Warlock's own Elements cannot be combined with external selected Recklessness. No automatic buffs or phantom food/potions.

**Validation:** `CP175_F2B2ModifierExpansion` checks all new modifier entries, provenance and category uniqueness, stacking, Mage/Warlock Baked Salmon combat output, Hunter Swiftstrike output, Warrior physical food, partial curse, deterministic replay and Mage MP5 regeneration. `CP175_SharedRestoration` now tests each new potion, zero/disabled stock across Mage/Warlock/Hunter/Warrior where appropriate, actual healing/mana events, finite charges and shared potion cooldown. The existing real Chrome script additionally verifies new food/curse markers, new food stat preview, Superior Mana Potion stock and toggle, and F2B2 screenshots while retaining the established four-class export/replay fixtures. Final-head CI remains the certification gate.

**Known limitations:** The new food entries model only the published combat stats, not their +5% experience gain from kills. Food consumption occurs pre-pull and is represented as selected static modifiers, not a simulated eating animation. Some raid encounter/resistance benefits are not covered by the single-target DPS environment. Partial target AP and control-immunity effects for Recklessness are intentionally nonexecuting. Historical data remains provisional and may change. Original F1 equipment breadth is out of scope and still open.


## CP175-F3A — Warrior 70707 talent accuracy and executable cross-tree corrections (November 2, 2026)

**Ownership/coordination:** Existing `cp175-shared-character-simulation` branch / draft PR #28 only. Checked all open PRs before development: #28 was the only open PR; no concurrent Warrior `class-updates` PR was found. No merging or cherry-picking of class-update work.

**Source:** [WoW Forever DB Warrior talent calculator, 70707](https://wowfdb.com/talents/warrior/), independently cross-referenced with [Forever Guides client 70707](https://theforeverguides.com/tools/talents/warrior/) and published [Wowhead Warrior talent changes](https://www.wowhead.com/forever/spells/talents/warrior). Client-derived descriptions are **provisional** (not in-game verified). The old API reported a 70338 preview; the Warrior-specific metadata now exposes a provisional 70707 reference, source build and mechanically separate implementation status. This does not imply every combat formula is current-server certified.

**Per-talent coverage matrix:** See [CP175-F3A-warrior-talent-coverage.md](CP175-F3A-warrior-talent-coverage.md). 52 definitions:
| Tree | Definitions | Previously declared fully modeled | Previously declared partial | Not executable |
|---|---:|---:|---:|---:|
| Arms | 17 | 11 | 4 | 2 |
| Fury | 17 | 6 | 7 | 4 |
| Protection | 18 | 9 | 5 | 4 |
| Total | 52 | 26 | 16 | 10 |

These are **catalog-declared** statuses and are not counted as 26 independently certified talents. This stage changes one source-backed prerequisite rule and restores off-preset action access for several already implemented mechanics. No new custom combat engine or unknown formula is added.

**Corrected source prerequisite:** 70707 Impale requires **1 rank in Deep Wounds** plus the normal Arms tier points; the old runtime demanded maxed 3/3 Deep Wounds. F3A corrects both server rank validation and `enginePrerequisiteRank` sent to the frontend. It does not relax Deep Wounds' own 3/3 Improved Rend prerequisite. This allows a legal Impale allocation to apply the existing critical-damage engine behavior.

**Executable capability corrections:** a Fury template can equip selected Arms Spearing Strike and Sweeping Strikes even when Arms is not its starting preset, and a shield-equipped Arms/Fury template can register legally allocated Protection Shield Slam/Concussion Blow (Last Stand when learned). The existing Battle/Defensive Stance, Rage, shield, cooldown, nearby-target and rank gates remain active. Automatic Fury priority includes learned Spearing/Sweeping only when appropriate; custom priorities may use legal off-tree actions and reject unallocated off-preset capstones. Off-tree Bloodthirst remains supported. Existing Fury Overpower custom rotations with explicitly enabled stance dancing remain legal.

**Focused seeded evidence:** `CP175_F3AWarriorTalentCertification.cs` tests the 70707 one-rank prerequisite, all tree counts and source metadata, Fury-preset Spearing damage, Fury-preset Sweeping echo onto a real secondary target, Arms-preset Protection Shield Slam real damage and shield gating, unlearned off-tree fail-closed behavior, and automatic priority visibility. Numeric values and replay are emitted by a dedicated CI step, not assumed from tooltip text. Earlier multi-tree Bloodthirst regressions remain intact.

**Browser acceptance:** existing ten real Chrome import/simulation/replay fixtures must still pass. New `cp175-f3a-fury-offtree-spearing.json` and `cp175-f3a-arms-mortal-strike.json` are full legal 51-point browser builds; the acceptance runner imports/exports them, captures talent/rotation screenshots, verifies exact talent ranks and actual completed ability casts and replays the same seed.

**Known F3A limits:** A fully browser-exercised Protection-focused shield build remains blocked by the sparse approved item catalog: do not fake a shield to satisfy an acceptance screenshot. The manual-shield runner certifies Shield Slam execution independently; it is not a substitute for frontend equipment eligibility. Control-dependent talents (Vanguard, root/disarm, Iron Will and crowd-control components), Shield Slam dispel and bonus threat, full Fury Gore Drinker and weapon-specialization live proc tables, and some out-of-combat Rage mechanics remain partial/unsupported as shown in the matrix. No encounter scheduling, other classes, equipment ingestion, or rotations architecture were changed.

**Final gate:** exact-head strict Release build, full xUnit, Python, frontend/DOM, real Chrome acceptance, and deterministic seeds must all pass before marking this checkpoint certified. Keep PR #28 draft/unmerged.


### F3A seeded combat receipts and browser acceptance (CI #41495888214)

A full strict Release/xUnit/Python/Chrome run **passed** on commit `8ca9d1b5` with 2,765 xUnit tests. Source-checked numerical Warrior tests used real seeded combat events (not inferred tooltip damage):

| Selected legal build | Recorded actual effect | Seeded mean DPS |
| --- | ---: | ---: |
| Fury preset + learned Arms Spearing Strike | 115.56 direct Spearing damage | 142.7050 |
| Fury preset + learned Arms Sweeping Strikes; nearby add | 2,122.51 echoed secondary-target damage | 312.2922 |
| Arms preset + learned Protection Shield Slam; manual equipped-shield configuration | 1,154.82 Shield Slam damage | 218.9834 |

The exact same build/seed replay matched in the focused runner tests. These are individual simulation receipts, **not** no-talent-versus-talent DPS deltas, and the manually configured shield cannot be counted as a selectable current Forever shield in the Solo DPS picker.

The real Chrome run [#41495888214](https://github.com/layoung3/WarcraftSim/actions/runs/41495888214) successfully imported, simulated, exported and replayed `cp175-f3a-fury-offtree-spearing.json` and `cp175-f3a-arms-mortal-strike.json`, together with the ten existing CP175 acceptance fixtures. F3A screenshots include the Warrior provisional talent indicator and both build priority panels in the established `cp174-ui-viewport-captures` artifact. Protection browser acceptance remains blocked by the actual usable shield inventory; this limitation is not concealed by a manual-shield unit test.

The later accessibility/DOM test tweak requires its own exact-final-head GitHub Actions result; do not present an in-progress or failed exact-head workflow as passing.


## CP175-F3A2 — Seven bounded executable Warrior talent corrections (November 2, 2026)

Source and statuses: build 1.60.1.70707 [WoW Forever DB Warrior talents](https://wowfdb.com/talents/warrior/) and the retained [F3A talent-by-talent matrix](CP175-F3A-warrior-talent-coverage.md). Current-client derived numbers remain provisional, not independently proven from live combat. No overlapping Warrior class-update PR was open at checkpoint start; branch `cp175-shared-character-simulation`, draft PR #28 retained.

| Effect | Before F3A2 | After code and evidence scope |
| --- | --- | --- |
| Improved Berserker Rage | Metadata only | Explicitly enabled legal Berserker Rage gains real 5/10 Rage on cast, using existing policy and resource event engine. Root/snare cleanse remains unsupported. |
| Enrage | Conditional physical bonus, no observable direct talent proc | Preserves 30% on incoming hit, rank*2% physical, 12-second refresh; new sourced Enrage proc event arms Gore Drinker. |
| Gore Drinker | Three charges after spell casts but Enrage proc disconnected | Real Enrage proc and eligible casts prime three landed melee heals of 0.5/1% of max Health; periodic damage excluded. |
| Death Wish | +20% physical done only | Active 30s also raises incoming damage by 5% in the real boss-auto and scripted-damage paths; Fear immunity remains unsupported. |
| Blood Craze | Six tick healing but trigger not observable | A sourced proc is emitted for actual critical or >20% max-health incoming hit; six healing ticks and refresh generation remain engine scheduled. |
| Impale | F3A talent catalog said 1 Deep Wounds rank but runner/factory still rejected it | Runner and direct-ability factory now accept 1 rank, preserving rank-specific critical bonus, stance rules and tier legality. |
| Weaponmaster (Sword) | Sword extra attack could trigger from shield abilities carrying weapon metadata | No extra sword attacks from Shield Bash or Shield Slam. Other weapon specializations remain as previously modeled. |

**Honest categorical delta:** F3A 26 fully modeled / 16 partial / 10 unmodeled → F3A2 **27 fully modeled / 16 partial / 9 unmodeled**. Two classification moves only (Gore Drinker partial→modeled, Improved Berserker Rage unavailable→partial); five other changes refine execution while keeping original status. This is not an assertion of independent current-server accuracy.

**New tests:** `CP175_F3A2WarriorTalentMechanics.cs` compares 0/1/2 ranks for legal Berserker Rage; denies wrong stance; verifies real incoming Death Wish damage risk and expiration, a true Enrage proc that arms Gore Drinker's three melee heals, Blood Craze six source-driven healing ticks and forbidden low-damage triggers, and 500 excluded shield-action hits from Sword Weaponmaster. Existing Warrior/CP175 full suite remains mandatory, including the corrected Impale unit and shared spellbook tests. The dedicated detailed CI step prints numeric receipts. The core scheduler and other classes are unchanged.

**Frontend:** An explicit opt-in Berserker Rage checkbox in the *existing* Warrior stance settings controls whether the scheduled utility is available, with correct typed request and build export/import preservation. The new `cp175-f3a2-fury-improved-berserker-rage.json` Chrome fixture checks the selected 2/2 talent, policy, +10 actual Rage event, export and deterministic replay. Existing Arms/Fury/off-tree fixtures remain. Shield-dependent Protection is tested in controlled backend mode, not misrepresented as picker-exercised.

**Remaining limits:** Movement impairments/cleansing, fear immunity, target control, full Weaponmaster mixed-hand conversion, rage behavior outside combat and shield equipment breadth remain uncertified. Full current-head CI (strict Release, xUnit, Python, DOM, Chrome) must pass before marking this slice accepted; do not treat pending workflow as success.


## CP175-F3B0 Mage CI and source-alignment continuation (2026-11-02)

The CP139 175% Arcane Blast surcharge assertion and CP174 Arcane Resilience historical-art rank mismatch caused four F3B0 CI failures. Fixes preserve the pinned historical talent visual data, explicitly mark the 70707 3-rank cap as separate from old 2-rank artwork, and use the client-derived 100% **base** Mana surcharge per active stack, 12% Intellect-derived Spell Damage per stack and fixed 18-second initial window. These 70707 numbers are **provisional**: https://wowfdb.com/talents/mage/ and https://wowfdb.com/classes/mage/changes/.

The 53-row Mage tree coverage audit (18 Arcane / 17 Fire / 18 Frost) is at [CP175-F3B-mage-talent-coverage.md](CP175-F3B-mage-talent-coverage.md). Conservative baseline: 16 full / 27 partial / 10 not modeled, classified by mechanics not tooltips. F3B0 does not implement the future Fire, Frost or other Arcane gaps. Detailed exact-head CI and Chrome acceptance must be recorded only after verified, not from in-progress workflow.


## CP175-F3B1 Arcane checkpoint (source audit 2026-11-02)

Current [Forever DB Mage 70707](https://wowfdb.com/talents/mage/) removes Missile Barrage proc eligibility from Arcane Missiles channel ticks; it preserves 20/40% Arcane Blast and 10/20% direct-spell chances. The 0.5s faster-tick tooltip conflicts with a reported removed speed aura in the 70707 client; timing is still provisional and noted as partial. Clearcasting requires one roll per spell execution (not one roll for every AoE enemy). Arcane Subtlety now reduces threat from actually dealt Arcane damage by 15/30% without changing DPS; resistance penetration remains unmodeled. This first bounded implementation reuses the current Mage specialization and threat resolver. Other Arcane work, including complete Mana Shield integration, is tracked separately until event-level CI certification.


### F3B1 bounded Arcane mechanic execution and evidence (November 2, 2026)

**Source baseline:** [Forever DB Mage 70707](https://wowfdb.com/talents/mage/), [Arcane Shielding rank effects](https://foreverdb.net/de/spell/11252), and [Mana Shield Rank 6 Forever spell](https://www.wowhead.com/forever/spell=10193/mana-shield). These are client-derived/provisional rather than live-server combat logs.

- **Missile Barrage:** 70707 removed proc eligibility from Arcane Missiles channel ticks. Existing landed Arcane Blast (20/40%) and eligible direct cast (10/20%) chances remain. A 15-second source-owned proc aura is now observable and removed upon the first Arcane Missiles channel; channel cancellation clears pending state. Historical 70630 missile-hit proc-chance tuning is retained only for serialization compatibility, not executed. The 70707 tooltip's 0.5s missiles conflict with datamined removed speed-aura evidence; existing five-tick 0.5s channel is explicitly **provisional** until live test, and Missile Barrage remains partial.
- **Arcane Concentration:** one proc roll per qualifying cast execution (including multiple affected targets); no unconditional Mana discount. Actual Clearcasting aura/zero-cost damage spell behavior remains. Exact low-rank suppression formula is unavailable, and neither spell-rank selection nor outside-combat proc qualification can be universally certified, so this remains partial.
- **Arcane Shielding:** preserves rank-dependent Mage Armor resistance and executes Mana Shield rank 6 in explicitly selected custom priorities (140 Mana cast, 570 physical absorb, 60s; 2 Mana per damage absorbed at rank 0, 1.66 at rank 1, 1.34 at rank 2). The existing AbsorbManager performs actual absorption, while a minimal Mage-only scripted-incoming adapter bounds absorption by available Mana, spends actual Mana and emits resource receipts. Magic bypasses Mana Shield; other absorbs retain their own behavior. Scripted physical only, not a second absorb engine; broad encounter damage, shield dispel and early recast remain partial.
- **Arcane Subtlety:** migrated from historical-only to a legal typed 2-rank talent. The actual Arcane-school damage threat resolver reduces threat 15/30% (not DPS) at ranks 1/2, including Fire-started legal cross-tree Arcane Missiles. All-school target resistance reduction remains **unmodeled**, so the talent becomes partial rather than fully modeled. Existing owner itemized export now carries its newly executable ranks through the typed runner.
- **Coverage:** Arcane 7 full / 8 partial / 3 unmodeled; Mage total 16 full / 28 partial / 9 unmodeled. Every classification is about the current engine's bounded mechanics, not independent live-server certification.

**Tests:** `CP175_F3B1ArcaneTalentMechanics.cs` covers 70707 no-channel-hit proc, landed Arcane Blast proc and aura consumption, rank 0/1/2 Arcane threat deltas, Fire-started cross-tree threat, rank-scaled Mana Shield physical absorption/consumption, magical bypass, finite Mana, explicit Fire-started defensive ability, and deterministic Clearcasting/aura/free-cast replay. Legacy CP167 and actual owner build tests were updated for the new 70707 behavior and previously historical Arcane Subtlety. No other class combat logic was rewritten.

**Browser:** `docs/acceptance/cp175/cp175-f3b1-arcane-mana-shield.json` and `cp175-f3b1-fire-arcane-subtlety.json` use actual schema-v1 51-point imports. The existing real Chrome harness asserts explicit shield activation (not a fabricated scripted hit), Fire-started Arcane Missiles and Arcane Subtlety selection, full export/import and seed replay. Screenshot/report retention uses the established CI artifacts.

**CI qualification:** Exact final-head .NET 10 Release, all xUnit/Python, JavaScript/DOM, real Chrome and replay must complete successfully before F3B1 is called certified. No PR merge.


## CP175-F3B2 — Mage Fire mechanical corrections (November 2, 2026)

**Baseline**: [Forever DB 70707 Mage talent calculator](https://wowfdb.com/talents/mage/), [Forever client change log](https://wowfdb.com/classes/mage/changes/), and published [Forever beta dev notes](https://www.bluetracker.gg/wow/topic/eu-en/631316-wow-forever-beta-development-notes-updated-26-october/). Current client-derived mechanics remain **provisional** until independently checked in live combat. The original F3B1 Mage status was 16 full / 28 partial / 9 unmodeled; F3B2 moves Impact **unmodeled → partial**, resulting in **16 full / 29 partial / 8 unmodeled** (Fire 5 full / 10 partial / 2 unmodeled; Arcane and Frost unchanged).

**Functional corrections (no new spell or DoT engine):**

1. Fire Power now counts only five source-owned active effect categories: Ignite, Improved Scorch, Fireball DoT, Pyroblast DoT and Impact. Rank 1/2/3 increases Fire damage by 1/2/3% per active category, capped at four (=12% at rank 3). Excludes Flamestrike and Frostfire as contributing auras. Frostfire Bolt DOES receive the bonus despite not contributing a stack. Ignite's reactive ticks are excluded from reapplication of these bonuses, preventing double dipping. Personal source and target boundaries hold for direct hits and existing periodic damage.
2. Impact now connects its legitimate 3/7/10% rank effects from Forever client talent #11103 to the existing aura engine. A landed eligible direct Fire hit rolls once per ability execution and applies a source-owned 2s stun marker. Stationary training bosses do not have enemy action/movement AI, so stun impairment and low-rank suppression remain **unimplemented** (Impact stays partial). No free DPS bonus or invented control is claimed.
3. Heating Up preserves existing 20s, up to three 25%-cast-reduction stacks; a landed Fire Blast grants one and a critical Fire Blast two, while periodic crits and misses are excluded. The simulator now emits a real named aura on gain/refresh and removes it on non-PoM Pyroblast consumption. Existing PoM priority behavior is preserved.
4. Combustion's 180s active cooldown and third-direct-Fire-crit expiration remain with explicit landed positive-damage filtering, tracked hit/crit numerical metrics, and actual buff remove events. Periodic damage and resisted/zero damage do not advance stacks.
5. Ignite retains four-second two-tick **rolling** behavior and proper per-Mage/per-target source ownership. Ranks 1 and 5 produce 8% and 40% of landed direct Fire crit damage. Fireball ticks currently have no independently confirmed crit mask and remain noncritical; Pyroblast periodic damage may crit in the existing engine, but periodic criticals never independently retrigger Ignite. Exact server rollover remains provisional.
6. Selected Fire trainer spells Fire Blast, Scorch and Flamestrike can be used from legal Arcane/Frost starting presets; Combustion, Pyroblast and Blast Wave remain rank-gated. No unlearned action is fabricated.

**Tests**: `CP175_F3B2MageFireMechanics.cs` provides real rank-0/1/2/3 Fire Power second-hit deltas, target/source and max-four-effect count, off-preset Arcane-start Scorch execution, rank-1/5 Ignite total tick-pool ratios, periodic-crit exclusion, Impact 3/7/10 fixed-seed proc counters, zero/hit/crit Heating Up aura/cast events, three-crit Combustion removal, invalid rank/ability rejection, and numerical report accounting. Older CP167 tests remain intact, with the isolated Improved Scorch vulnerability test explicitly disabling Fire Power so the corrected interaction is tested separately.

**Browser**: `cp175-f3b2-fire-combustion-pyroblast.json`, `cp175-f3b2-fire-scorch-power.json`, `cp175-f3b2-arcane-start-fire-mixed.json` are schema-v1 complete 51-point imported builds. Real Chrome harness asserts selected Fire ranks, source/partial indicators, actual Fire effects, Combustion and Pyroblast/Heating Up activity, Arcane-start off-tree legal execution, report/export and same-seed replay, and captures screenshots. Rare Ignite/Impact procs are evaluated in deterministic backend tests rather than falsely guaranteed in browser.

**Open**: Fire Ward reflection, range improvements from Flame Throwing and Improved Fire Blast, true Impact stun/control, precise Ignite rollover/snapshot edge cases, multi-target Combustion nuances, partial Fire Power interactions with unsupported enemy behavior, AoE geometry, and uncertain DoT critical masks. Remain partial/unmodeled as matrix states. **Acceptance:** require exact-final-HEAD successful strict .NET Release/xUnit/Python/DOM/Chrome/seed replay before certification; never report in-progress CI green.


## CP175-F3B3 Frost mechanical checkpoint (2026-11-02)

**Sources:** [Forever DB 70707 Frost talents](https://wowfdb.com/classes/mage/frost/talents/), [spell ranks](https://wowfdb.com/classes/mage/frost/spells/), [client changes](https://wowfdb.com/classes/mage/changes/), [Frostbite rank effects](https://wowforevertalents.com/mage/talents/frostbite/) and [Improved Frost Nova trait](https://www.wowhead.com/forever/spell=11165/improved-frost-nova). Client-derived, **provisional**; not confirmed by live-server boss logs. No new Mage engine/scheduler.

**Executable corrections:** Ice Lance Frozen damage 3x→4x (300% increased) and Fire Blast category-19 8s shared cooldown; Winter's Chill owner-target 15s debuff and 20% per-stack next-Cone amplifier (7% SP per stack retained), landed Cone consumes only its target; Ice Barrier 819+10% Frost SP absorb (plus Frost Warding 10% per rank), +15% timed Frost damage and +5% extra while shield intact; Frostbite 5/10/15% landed Chill freeze proc creates source-owned five-second Frozen state used for subsequent Shatter and Ice Lance; Improved Frost Nova rank1/2 reduces the real cooldown 2/4s; Fingers of Frost adds source-owned 15s proc aura and removes on last charge/expiry. Blizzard existing 8x1s 1168 damage and rank-based 15/25/40% slow are tested, not rewritten without current proc-mask evidence. Frostfire dual-school resistance selection remains unsupported.

**Coverage:** Frost **4 full / 11 partial / 3 unmodeled → 5 full / 12 partial / 1 unmodeled** (Improved Frost Nova fully executed; Frostbite moves none→partial). Mage aggregate **16/29/8 → 17 full / 30 partial / 6 unmodeled**. Arcane and Fire classifications preserved. Missing Arctic Reach geometry, armor-triggered FoF/Frostbite, boss immune/AI/root mechanics and unresolved Frost Blast rank remain explicit; do not interpret source metadata or a freeze aura as live boss control.

**Testing and Chrome:** `CP175_F3B3MageFrostMechanics.cs`, existing CP167 Frost and target ownership suites, and `docs/acceptance/cp175/cp175-f3b3-*.json` actual browser imports/exports/replays provide bounded event, damage, cooldown, absorption, and selected-talent evidence. Distinguish backend scripted incoming hits from browser scenarios with no enemy hits. Gate any completed/certified claim on exact-final-head Actions passing full strict .NET10, xUnit, Python, JavaScript/DOM and real Chrome.


## CP175-F3B4 cross-tree checkpoint and gap significance (2026-11-02)

Current implementation and talent-source census: 53 published Mage talent definitions; 47 executable typed keys; six historical-fallback nonexecuting selectors. Source confidence is always separate from mechanical capability. The Mage gap taxonomy and 31 remaining partial/not-modeled talent rows are recorded in [CP175-F3B-mage-talent-coverage.md](CP175-F3B-mage-talent-coverage.md). After reclassifying five supported-but-provisional mechanics, the conservative total is **22 full / 25 partial / 6 unmodeled**. Category flags A/B/C/D/E are not standalone badges; user-facing UI still communicates source confidence and real missing mechanics separately.

F3B4 work fixes Cold Snap shared Ice Lance cooldown, cross-tree trainer and opted-in reactive default priorities, no-effect stale custom actions after talent removal and imported build preflight, and Arcane Mind character-stat preview; adds rank-specific four-mix and negative event regressions with actual Chrome export/replay acceptance. All unsupported movement, immunity, range and resistance behaviors are retained as limitations. Final CI must be verified on the exact final HEAD.


## CP175-F3C0 Warlock coverage and source alignment (2026-11-03)

The [52-row Warlock talent matrix](CP175-F3C-warlock-talent-coverage.md) audits current 70753 definitions (17 Affliction / 19 Demonology / 16 Destruction). Baseline **30 fully modeled / 17 partial / 5 unmodeled**, with A/B/C/D/E gaps and independent provisional source confidence (novel announced changes are not yet 70753 tooltip-verified). Bounded combat updates: announced Wrack tick/coefficient, corrected Improved Drains mask, Shadow and Flame new damage scaling, two Health per effective Stamina for the seven demons and explicit nonexecuting Curse of Exhaustion metadata. Full CI and real Chrome acceptance must pass at the final exact HEAD before F3C0 is reported complete. No new branch/PR or merge.


## F3C1 — Affliction functional integration (2026-11-03)

- **Trained Drain Mana:** source: [Forever rank-4 spell 11704](https://raiddex.com/en/wow-forever/spells/drain-mana-rank-4), cross-checked with [Forever spell changes](https://foreverdiff.com/spells/drain-mana/). At level 60, 310 Mana cost, 5-second channel, one tick per second, 136 target Mana transferred per tick before talents. Earlier ranks are 42/68/99 per tick (IDs 5138/6226/11703); 11704 is rank 4. The existing channel scheduler runs the spell, and a dedicated event processor transfers finite target Mana without inventing a boss Mana pool. Opt-in `targetStartingMana` is a controlled encounter fixture; ordinary Solo DPS has no target Mana and cannot recover Mana from Drain Mana. No full enemy resource AI or geometry is claimed. Improved Drains 7/13/20% now applies to transfer.
- **Bane of Agony:** November 2 notes explicitly identify dynamically updated damage. The existing 12 × 2-second, three-stage ramp and target-owned periodic engine are retained, with its spell-power coefficient read dynamically from the actor on each tick. DoT shape and coefficient remain provisional until in-game numerical confirmation; no new scheduler was created. Existing source-owned refresh/independent target behavior remains.
- **Drain Soul:** November 2 notes specify that Soul Shards can be generated upon dealing damage rather than on a kill and that the chance was significantly reduced. Removed the earlier guaranteed-kill path; shard chance stays a user-provided test-fixture parameter until a verified value exists. Valid positive channel damage, eligibility and available inventory space still gate actual generation. Duration-driven boss scripts do not imply death.
- **Nightfall:** eligible landed Corruption periodic or Drain Soul/Life/Wrack channel damage can proc; direct damage or incorrect delivery cannot. Existing 2/4% ranks, 10-second provisional lifetime, instant Shadow Bolt and aura consumption remain.
- **Cross-tree:** Drain Mana remains trained and works regardless of starting specialization; Improved Drains is rank-scoped. Existing off-tree ability/priority validation is reused.
- **Coverage:** Affliction 11 mechanically full / 4 partially modeled / 2 not modeled (was 10 / 5 / 2); only Improved Drains changes principal status. Encounter-dependent missing target Mana is flagged C, source-dependent coefficients D. No Demonology or Destruction reclassification. This is NOT a claim of complete Affliction utility or server-verified numerical tuning.
- **Regression/CI:** CP175_F3C1AfflictionMechanics provides resource receipts, depleted/no-target Mana, rank scaling, shard eligibility, Nightfall delivery exclusion, live Agony coefficient and seeded replay. Running the full suites, browser screenshots, and exact-head CI remain acceptance gates; do not mark passed without actual results.

Sources: [November 2 changes discussed November 3](https://us.forums.blizzard.com/en/wow/t/latest-warlock-changes/2399400), [Forever rank 4](https://raiddex.com/en/wow-forever/spells/drain-mana-rank-4), [Forever spell comparison](https://foreverdiff.com/spells/drain-mana/). Provisional client release 1.60.1.70753; published notes are not live-server validation.

### F3C1 post-CI regression follow-up

The legacy CP167 guarantee-of-shard-on-kill assertions are superseded by the November 2 damaging-tick trigger and the earlier official progressive chance description. Corrected old assertions to reject unsupported guaranteed kill drops and recognize the new event key; the source confidence and encounter dependence remain explicit. F3C0 coverage baseline regression was updated from 17 to 16 partial implementations after Improved Drains became mechanically covered.

## CP175-F3C2 — Demonology and companion execution checkpoint

Existing PR #28 and `cp175-shared-character-simulation`, no new branch. Implemented (not just documented): owner Stamina-to-pet Health live delta and reversal with Demonic Embrace/Fel Vitality/family multipliers; corrected Imp Firebolt and Sayaad Lash inherited spell-power talent scaling; Unholy Power restricted to five permanent demons; rank-5 Imp Fire Shield cast and incoming-hit retaliatory damage; rank-6 Voidwalker Sacrifice with source-backed absorption, improved talent rank scaling and pet death/lifecycle. Existing CompanionActorFactory, AbilityExecutor, AuraManager, AbsorbManager, rotations, threat routing, and pet lifecycle remain authoritative. Added F3C2 event/numeric/regression tests. The existing Solo DPS boss still has no enemy AI, so no claim of automatic pet control, radius targeting or unsupported magic resistance. Report exact-head Actions and F3C2-specific browser evidence separately.

## CP175-F3C3 — Destruction correction checkpoint

**Existing** branch `cp175-shared-character-simulation`, draft PR #28; not merged. Combat implementation: corrected Bane's missing Immolate cast-time reduction, and added Aftermath/Pyroclasm source-owned on-hit CC aura/event receipts using the existing AuraManager and seeded combat events. Pyroclasm channels perform one probability roll per real target per channel execution, never one full-duration chance roll per tick. No damage formula, speculative AI control or range geometry added. Existing Conflagrate source-owned Immolate gating, landed-only consumption/retention and Shadow and Flame 4% rank buffs remain intact, with additional exact regression tests and cross-tree replay. These control effects are encounter-dependent because the stationary boss cannot move or cast. Pyroclasm moves from not modeled to partial; no other tree classification changes. Final exact-head CI and Chrome artifacts must be attached separately once verified.

## CP175-F3C4 — Warlock cross-tree integration and core Solo DPS checkpoint

Branch `cp175-shared-character-simulation` / existing draft PR #28. Starting exact HEAD `b71046e07846d24a1c43120934df1e00fe84f47e` with successful CI #41673937656. Continue in-place; no new branch or merge.

**Mechanics delivered:** corrected live owner Demonic Knowledge stat and spell scaling to avoid re-snapshotting active temporary spell-power, and reverse exactly once on pet loss; expanded opt-in talent-aware automatic Warlock priorities to include legally selected off-tree DPS actions before filler; propagated channel execution IDs through finite Drain Mana and damage-based Drain Soul shard events for precise cancel/receipt tests. Source-backed Demonic Pact allows Felhunter pre-pull sacrifice to coexist with active Imp; resummoning Felhunter, or summoning any pet without Pact, cancels it (confirmed by Blizzard's official class deep dive and current tooltip). No new pet engine and no unsupported formulas.

**Acceptance:** four *actual* 51-point class-preset builds in `docs/acceptance/cp175/cp175-f3c4-*.json`; xUnit `Cp175F3C4WarlockIntegratedCombat` checks actual owner/pet damage, typed serialization, negative talent removal, automatic mixed priorities, Pact positive/negative, live Agony SP during/after temporary buffs, channel cancellation and execution-specific resource receipts, deterministic replay. The real Chrome acceptance script imports all four builds, verifies named damage and pet actions, captures talent/rotation screenshots, exports complete reports, replays deterministically, verifies 15s Talisman-to-Imp stat propagation, and deallocates Conflagrate on 17/17/17 build to ensure it disappears from the custom priority. Existing F3C2 Felhunter/Imp browser receipt remains part of shared acceptance.

**Coverage:** 32 fully modeled / 16 partial / 4 not modeled, unchanged. Current partial/unmodeled counts are not a claim of 20 missing DPS mechanics. Audit now classifies all 20 residual nodes by A–E and separates core stationary Solo DPS from defensive, movement, range, CC, enemy-AI and source-calibration restrictions. Exact final HEAD/CI and screenshots/reports to be recorded in PR acceptance comment after actual Actions success.

## CP175-F3D0 — Hunter 70753 talent baseline, pet census and functional corrections (2026-11-03)

**Existing branch/PR:** `cp175-shared-character-simulation`, draft PR #28. Start `93b6cefe1a30e9d27b5cd2d5a33eb6db55f087e5`, CI #41690789865 success (2,946 xUnit, 60 Python). **No new branch/PR or merge.** This checkpoint audits Hunter and makes bounded changes; it does not certify all Hunter mechanics.

**Current sources:** [client-derived Hunter talents, build 1.60.1.70753](https://foreverchanges.pro/talents/hunter); [Nov 3 beta notes](https://www.bluetracker.gg/wow/topic/us-en/2360696-wow-forever-beta-development-notes-updated-november-2/); [Blizzard Hunter class deep dive](https://news.blizzard.com/en-us/article/24301515/world-of-warcraft-forever-class-deep-dives-hunter-and-druid). Latest 70753 talent count is **50 = 16 BM / 16 MM / 18 Survival**, not a presumed old 51-node tree. Full matrix with source confidence, rank caps, row positions, typed/UI mapping and specific A–E gaps in `docs/audits/CP175-F3D-hunter-talent-coverage.md`.

**Changes to actual combat (not metadata-only):**
- Marksmanship and cross-spec trained Aimed Shot +350 rank-6 bonus (was +166), Arcane Shot rank-8 280 +15% live ranged AP (was 217 without scaling), Serpent Sting rank-9 135/tick +5% live ranged AP per tick (was 111 without scaling), Sniper Shot rank-3 +590 (was 295).
- Barrage now 5/10/15% on Aimed Shot, Multi-Shot and Volley; *Sniper Shot is intentionally excluded*, correcting a second double-qualification issue. Trueshot Aura owner rank-5 default 100 ranged AP rather than old 50.
- Survival Lightning Reflexes +3% of Agility per rank in its modeled owner melee/ranged AP path, replacing obsolete 2%; secondary crit/dodge conversion remains partial until level-60 Forever coefficients verified.
- The four in-combat Survival traps now participate in **one shared 30sec Fire/Frost cooldown group**, with existing rank-modified `Survivalist's Discipline` duration, instead of separate cooldowns.
- Beast Mastery Pathfinding is moved to tier 3 (10 earlier points). Hunter pets keep independent Hunter-specific inheritance; no Warlock demon coefficient was reused.
- In pet-enabled Marksmanship, **Lone Wolf checks the existing pet-active aura** rather than only pet actor existence: before engagement, after loss/dismissal, and during active combat are distinct. Focused Fire likewise only grants while the pet is genuinely active; no phantom active-pet bonuses.

**Focused tests:** `Cp175F3D0HunterSourceAndCombatCertification` checks all 50 talent rows and source caps, new numerical base/coefficient effects, rank 0/1/2/3 Barrage and Sniper exclusion, same group cooldown on four traps, Lightning Reflexes melee/ranged impact, Lone Wolf without active pet and all 19 pet families. Existing CP169 exhaustive per-family combat, Focus, threat, death, secondary-target and same-seed replay tests remain.

**Browser acceptance plan:** four existing-frontend Hunter builds (MM + pet, melee-first Survival, BM + pet, and no-pet Lone Wolf MM), all itemized/51 points, actual owner/pet damage and cooldown receipts, build export/import, full combat JSON reports and deterministic replay, plus accessible provisional/implementation indicators. Browser captures must be provided as real GitHub Actions artifacts; never substitute screenshots taken from a fake rendered UI.

**Current limitations to carry forward:** Source spell IDs have not been reconciled for all 50 talents; the matrix intentionally marks them unverified rather than guessing. Raid-wide Trueshot Aura, some Hunter survival control, source Agility→crit/dodge, Scatter Shot's utility strike, Lacerating Strikes weaker-refresh priority, broad pet behavior and spatial geometry belong in F3D1–F3D4. Exact-head CI and artifacts to be appended after successful Actions verification.

## CP175-F3D1 — Marksmanship combat completion checkpoint (2026-11-03)

Starting verified `a2ea207d46b02ae8d3f3015de0b303ab5e375e8c` (CI #41696331021). Continue **existing** `cp175-shared-character-simulation` branch and **draft PR #28** without merge.

**Combat changes:** Implement client spell 19503 Scatter Shot through the existing Marksmanship ability executor: independent 30s cooldown, 8%-base-Mana Efficiency discount, 50% physical ranged weapon and live attack-power scaling, landed-only 4s damage-break disorient marker; actual cast stops the existing Auto Shot timer. A new bounded processor handles only the non-damage state transitions. The unmodeled 15-yard range and enemy disorientation AI remain explicit. The actual source-owned Trueshot Aura 20906 now adds/removes +100 owner RAP through the existing live Hunter equipment/stat inheritance callback, preventing a permanent precombat snapshot; party distribution and pre-pull Mana cost are not claimed. Rapid Killing's existing 2-minute/20% rank-2 kill/Sting bonus, Rapid Recuperation's 15s conditional regeneration, and existing Aimed/Multi/Arcane/Sniper timing remain, with additional direct receipts and negative eligibility coverage. The new optional Scatter action is only in learned custom priorities; talent removal scrubs stale actions.

**Mechanical classifications:** Marksmanship 9 full / 5 partial / 2 not modeled; all Hunter 27 full / 20 partial / 3 unmodeled. Scatter Shot's direct-damage Category A gap is resolved. The detailed source/rank/remaining B–E matrix is updated in `docs/audits/CP175-F3D-hunter-talent-coverage.md`.

**Browser and CI acceptance:** four new actual 51-ranked talent builds—Marksmanship DPS with pet; Scatter/utility with legal learned rank; BM preset with MM off-tree lethal crit and active pet; petless Marksmanship/Lone Wolf—are added under `docs/acceptance/cp175`. Existing full Chrome export/simulate/reimport/seeded replay acceptance must pass before certification. Future F3D4 remains responsible for arbitrary three-tree legal Hunter allocation support and a genuine fully cross-tree active talent runtime.

## CP175-F3D2 — Hunter Survival combat mechanical correction (2026-11-03)

**Starting HEAD:** `139fa9c612925dfd797cbd8201af6596f44f2057`; successful exact-head CI #41699845816. Continue existing `cp175-shared-character-simulation` branch and draft PR #28. No new branch or merge.

**Code, not documentation only:** `SurvivalHunterAgilityStats` uses approved Hunter level-60 derived stats to apply Lightning Reflexes' 3/6/9/12/15% *incremental* Agility through actual melee/ranged AP, live critical-roll stats and Hunter pet inheritance, including temporary stat delta and reversal. A configurable agility-to-dodge coefficient remains an *opt-in, source-uncertain numerical fixture*, not an invented live rate. Mongoose Bite/Strider Kick/Counterattack and Aimed/Multi damage now read live owner AP rather than baking once-only bonus into the spellbook. Trap AP uses current owner RAP; shared Fire/Frost cooldowns and existing trap activation processor remain. The old global `_bleedGeneration` canceled unrelated targets' Lacerating Strikes ticks; replaced with a per-target generation ledger and target-owned 21s aura, reusing the existing event scheduler. Nov 2 notes explicitly require **same-Hunter weaker reapplications to overwrite**, so no stronger-snapshot preservation or rollover mechanism is added.

**Regressions:** `Cp175F3D2SurvivalMechanicalCompletion` includes ranks 0–5 AP/crit exact calculations, temporary Agility modifier apply/expiry, missing/explicit provisional dodge, 40%-of-Mongoose 7-tick bleed, weaker/equal reapplication, two-target isolation, illegal talent removal, real melee proc/cost execution, unified trap category and legal Survival/BM pet ownership with same-seed replay. Prior Hunter/Warlock/Mage/Warrior tests must remain green. Source confidence stays separate from mechanical status.

**Updated matrix:** Survival 9 full / 9 partial / 0 not, all Hunter 28 full / 19 partial / 3 not; unresolved B/C/D/E include Agility-to-dodge and actual enemy incoming attacks. Explicit Category A DPS rows are now zero *only in currently supported Solo DPS scope*. F3D4 remains the planned arbitrary legal mixed-tree Hunter framework, which must not be falsely claimed complete through Survival-only tests.

**Verification:** Strict Release/xUnit/Python/JS/Chrome/real export-replay and artifact IDs to be appended to existing PR #28 only after exact-head CI success.


### F3D2 bounded cross-tree and browser additions

The F3D2 follow-up retains existing per-spec runners. The connected itemized catalog exposes BM allocations from Survival and first-tier Improved Tracking from Marksmanship, with strict rank/tree checks. Survival routes BM ranks into the existing PetTalents handler, and Marksmanship routes only tracked Improved Tracking into a separate existing Survival-rank owner; deeper unsupported Survival talents remain rejected. Four 51-point real Chrome fixtures are `cp175-f3d2-melee-survival`, `cp175-f3d2-lacerating-strikes`, `cp175-f3d2-survival-beast-mastery` (44/7) and `cp175-f3d2-marksmanship-survival` (5 Survival/46 MM). Each requires actual combat, source and rank receipts, export/reimport and deterministic replay, plus browser Counterattack deallocation and priority scrubbing. This does not claim F3D4's arbitrary three-tree Hunter engine.


## CP175-F3D3 — Bounded BM pet lifecycle and 19-family replay implementation (2026-11-03)

Continued the existing `cp175-shared-character-simulation` branch and draft PR #28, without creating a new branch, PR or merge. Starting HEAD `81bb8197a2a83827a6290369775ce56f7f580ed5` had successful Actions #41711813210 (2,989 xUnit / 60 Python; F3D2).

**Functional corrections:** the BM Fox haste state now refreshes without stacking or early stale-expiration removal; actual Fox ability cooldown remains the sourced 180 seconds. On BM pet death, transient Frenzy and Fox haste and Bestial Wrath eligibility are cleared, accompanying auras are removed, and old haste expiry events become idempotent. This prevents pet bonuses from leaking across a dead-to-revived actor lifecycle.

**Regression changes:** `CP169_AllNineteenPetOutcomes.cs` now runs deterministic dual-seed-matched combat/Focus/timeline receipts for every registered family, checks a sourced 180-second Fox signature cooldown and independent auto-attacks between casts, and checks Fox aura removal on lethal incoming damage. Existing 19-family family-specific signature, resource expenditure, damage ownership and postmortem no-attacks tests remain intact.

**Scope and evidence:** this checkpoint has not implemented Improved Mend Pet's missing dispel component, newly certified external numerical coefficients, comprehensive 19-family rank tuning, four new Chrome acceptance builds, or arbitrary cross-tree F3D4 integration. Beast Mastery's conservative 10/5/1 mechanical matrix remains unchanged. The CI history contains a strict-build xUnit2031 correction and subsequent failed early tests built on unsupported assumptions about configurable Fox cooldown and hit-avoidance timing; those tests were corrected to reflect actual family rules. Completion requires exact-final-head CI and remaining F3D3 browser/functional acceptance, not just this ledger entry.

## CP175-F3D3B — Beast Mastery mechanics and real Chrome completion batch (2026-11-03)

**Starting point:** F3D3 draft PR #28 HEAD `f68b44da003caa45964a1b8a2a56f5e26e3c6be0`, exact-head CI #41721190494 success (3,010 xUnit); same branch, no new PR or merge.

**Functional combat fixes:** BM's Intimidation next-hit guarantee now expires at ten seconds (and is consumed at its first eligible hit), including explicit removal on pet death and dismissal. Both pet loss paths clear Frenzy/Fox temporary haste and active Bestial Wrath eligibility; stale expiry events are idempotent. Improved Mend Pet is now connected to the real five healing ticks: a successful 15% (rank 1) or 50% (rank 2) chance removes one hostile tagged Curse/Disease/Magic/Poison aura through the existing AuraManager. Zero ranks and absent eligible debuffs do not consume proc RNG. Source: the sourced Forever client talent tooltips, not an assumed 15% per point.

**New numerical/event-level regression files:** `CP175_F3D3B_BeastMasteryNumerical.cs` (Bestial Wrath 1.50x pet direct hit with exact post-expiration baseline, Frenzy actual auto timestamp acceleration, dismissal cleanup); `CP175_F3D3B_MendPetDispel.cs` (rank chance and Mana values, hostile aura selection, failed proc, actual Mend Pet healing, Improved Revive Pet rank spell times/costs and real death→revive→pet melee events). Additional dynamic Hunter owner AP/Armor apply/remove and no-duplicate assertions extend the CP169 test; BM Intimidation expires without a landed hit in the existing 19-family suite.

**Four dedicated browser builds** under `docs/acceptance/cp175` are `cp175-f3d3b-beast-wrath-frenzy.json`, `cp175-f3d3b-wind-serpent-focus.json`, `cp175-f3d3b-survival-bm.json`, and `cp175-f3d3b-petless-lone-wolf.json`. These retain legal 51-point ranks and enter the established `tools/solo-dps-browser-interactions.cjs` import → typed request → simulation → report/export → same-seed replay path, with per-build combat receipts and screenshots. These are separately executed Chrome fixtures, not a claim that scripted enemy dispels were browser-tested.

**Coverage (pending final-head verification):** BM 11 fully modeled / 4 partially modeled / 1 not modeled, compared with 10/5/1 previously. Other 19-family source coefficients, encounter-wide utility/AI, pet Health dynamics and arbitrary F3D4 Hunter cross-tree combinations remain provisional or out of scope. No source-independent live-game certification claimed.

**Verification gate:** record only successful exact-final-head Actions and preserve build, tests, browser artifacts, and deterministic replay. A preliminary green workflow cannot certify subsequent commits.

### F3D3B follow-through: Spirit Bond execution, negative eligibility, Chrome receipts

The existing BM Spirit Bond healer did not schedule the initial periodic event. The runner now schedules that event only when rank > 0 (5-second ticks at rank 2, 10 seconds otherwise). A controlled injured-pet simulation verifies actual periodic pet healing and its absence after talent removal. The numerical tests now also compare 5-rank Unleashed Fury and 2-rank Focused Fire exact pet-hit damage ratios against their unlearned variants, verify real pet Focus regeneration at Bestial Discipline ranks 0/1/2, and ensure removing Bestial Wrath/Summon Hawk makes stale custom priorities fail closed.

[GitHub Actions #41724222394](https://github.com/layoung3/WarcraftSim/actions/runs/41724222394) provided four successful F3D3B Chrome import, typed request, combat, JSON export and seeded-replay receipts: BM+Cat (owner 11229.89, pet 6640.20); Wind Serpent (owner 12617.21, pet 6888.82); Survival/BM (owner 18077.26, pet 3575.07); petless Lone Wolf (owner 19947.35, pet 0). Corresponding screenshots and reports were archived under standard workflow artifacts. These are prior-head receipts and will be rechecked by exact-final-head CI; enemy pet debuff injection and temporary owner stat mutation remain backend fixture evidence.

## CP175-F3D4 — Hunter bounded three-tree execution and remaining integration gate (2026-11-04)

**Starting point:** Existing `cp175-shared-character-simulation` and draft PR #28 at F3D3B HEAD `54e0cc32ff8d073f26bb823d0570f01986021568`, successful CI #41725824520 (3,026 xUnit, 60 Python, four Chrome Hunter builds). No branch/PR creation or merge.

**Functional corrections in this continuation:** Marksmanship now executes legally ranked Beast Mastery pet talents from its own itemized allocation while retaining off-tree Survival Improved Tracking; Survival now executes Marksmanship Lethal Attacks on effective owner crit while preserving its learned BM pet talent effects; Beast Mastery now applies off-tree Survival Improved Tracking to qualified owner damage while retaining learned MM crit and active pet combat. Typed requests are copied before cross-tree rank normalization to prevent non-repeatable same-seed replay. Survival's Focused Fire now honors the pet-active aura and stops granting bonuses when the pet is alive but disengaged.

**Tests and acceptance fixtures:** `CP175_F3D4HunterCrossTree.cs` exercises valid MM/BM/SV allocations, three-tree variants rooted in each of the three specs, legal budget and prerequisite validation, actual owner damage/pet Focus and damage, deterministic replay on unchanged request objects, off-tree damage modifier removal and nonactive-pet conditional bonuses. `tools/solo-dps-browser-interactions.cjs` now includes six new `docs/acceptance/cp175/cp175-f3d4-*.json` level-60 builds, each verified for imported ranked selections, typed request, actual combat, report export, build roundtrip, full JSON report and seeded replay; a real three-tree Scatter Shot deallocation scrubs the learned-only priority action.

**Boundary:** Do not misstate this bounded integration as universal 51-point cross-tree gameplay parity. Most deep off-tree spells remain non-executable from unrelated primary runners; the engine intentionally fails closed on such selected modeled talents. Comprehensive preset switching without loss, deep mixed-actives integration, complete externally sourced pet coefficients and live-server accuracy remain incomplete. No new Hunter talent mechanical coverage status was upgraded solely for an integration test; the F3D3B baseline remains 29 full / 18 partial / 3 not modeled across 50 Hunter talents.

**CI acceptance:** Only quote the GitHub Actions result for the final commit SHA, not an earlier green head. Run strict Release, full xUnit, Python, frontend/DOM, Chrome, complete pet-family sweep, build/report export and deterministic replay; keep PR #28 draft and unmerged.


### F3D4 final off-tree eligibility correction (2026-11-04)

The Marksmanship runner now executes selected Beast Mastery **Deadly Aspects** as source-qualified landed Auto Shot haste: 2% per rank chance while Aspect of the Hawk is active, 30% actual swing haste for a timed 12 seconds, with aura expiry and no permanent stacking. It also retains the selected **Bestial Discipline** casting Spirit-regeneration fraction (25% per rank) in the owner Mana processor, in addition to existing pet Focus regeneration. Both have direct combat/resource tests in `CP175_F3D4HunterCrossTree.cs`. The MM/BM browser fixture is 44 MM / 7 BM with real 2/2 Focused Fire, rather than the initial 41/10 test allocation with a nonexecuting Deadly Aspects rank.

Strict unified itemized validation rejects selected Beast Mastery abilities that are not implemented in the chosen starting runner; Marksmanship currently rejects Improved Mend Pet, Summon Hawk, Improved Aspect of the Monkey and Pathfinding. Survival rejects incoming unified allocations of Deadly Aspects, Improved Mend Pet, Improved Aspect of the Monkey and Pathfinding, but preserves existing CP169 legacy typed `PetTalents` execution and Hawk family tests. These rejections explicitly identify **remaining cross-tree integration limitations**, not missing points or fabricated passives. Full arbitrary cross-tree gameplay remains incomplete.


## CP175-F3D4B — Cross-tree Hunter action execution and real-runner eligibility

**Starting accepted code:** existing branch `cp175-shared-character-simulation`, draft PR #28 at `eca46b452d291dc3b28a28b4d675cc0651734a0b`; Actions #41854007474 success, 3,036 xUnit, 60 Python and six F3D4 mixed-tree Chrome builds.

**Actual changes:** Survival and Beast Mastery now attach the already-defined Marksmanship Sniper Shot and Scatter Shot (real Mana, cast time, cooldown and disorient/Auto Shot stop), rather than recognizing the names without a spell. Marksmanship and Beast Mastery attach Survival Counterattack, Strider Kick and Deterrence using the existing Survival factory. Explicit melee engagement is required for melee attacks, and Counterattack remains gated by a real reactive opportunity (or an opted-in scheduled incoming parry fixture); Strider Kick applies its actual on-hit movement marker. Marksmanship now executes off-tree Summon Hawk via `HunterSharedHawkRuntime`, with two separate actors, Arcane Shot shared cooldown, independent reported Hawk damage. BM and Survival have learned Trueshot Aura, supported ranged weapon modifiers and Lone Wolf conditions connected to owner damage. Three runner-built priority entries are exposed through the *existing* `/api/forever/rotation/preview` endpoint; the frontend enables Hunter preview, uses it to validate imported/custom priorities, and reconciles removed learned actions. All of this uses the established engine—not a new Hunter runner or pet architecture.

**Evidence:** `CP175_F3D4B_HunterActionAccess.cs` covers legal action execution, Mana receipts, peer and pet/Hawk damage, no-rank rejection, melee stance and parry requirements, Strider aura, Deterrence, and deterministic replay. `CP175_F3D4B_HunterPriorityPreview.cs` proves preview entries come from selected combat runners and stale actions fail closed. Five dedicated `cp175-f3d4b-*.json` Chrome imports execute cross-preset actives, preserve itemized talent selections, create full JSON reports and screenshots and compare replay. A second actual-browser interaction unlearns Strider Kick from a BM-starting build and verifies its custom priority is removed. All previous F3D4 and CP169 19-family checks remain.

**Status discipline:** An earlier exact-head green run #41861459619 passed 3,049 xUnit, 60 Python and all five browser fixtures. That workflow does not certify subsequent off-tree Trueshot regression or final documentation commits; check the eventual exact-final-head Actions result before marking the checkpoint fully accepted.

**Boundaries:** Core cross-preset *talent active* accessibility is implemented and demonstrated for named talent-granted actions. Unsupported enemy behavior, pathfinding, proper range, broader passive interactions, provisional numeric damage and Core Hound tameability are **not** inferred as finished. Existing PR stays draft; do not merge main.


## CP175-F4A — Four-class bounded Solo DPS integration (2026-11-04)

**Starting accepted head:** `88ee09ca0053833659bff2489ee35dd26fd27720` / Actions #41885851165, strict Release, 3,051 xUnit and 60 Python. Continued only existing `cp175-shared-character-simulation` and draft PR #28. No new PR or merge.

### Four-class integration matrix

| Class | Existing legal talent/character execution | Mixed-tree actions and owner/pet effects | Current F4A executable evidence | Remaining boundary |
| --- | --- | --- | --- | --- |
| Warrior | `ForeverWarriorSimRunner` + actual rank-derived Arms/Fury/Protection talent map; race/equipment, rage and target armor integrated | Preset-independent trained Bloodthirst and off-tree Arms/weapon/rage passives; native auto attacks, stances and Rage | `cp175-f4a-warrior-arms-fury.json`: legal 51 ranks, real off-tree Bloodthirst receipts, custom priority, export/reimport and replay | Weaponmaster subtypes and queued dual-wield costs remain partial; incoming-damage procs/defense cannot be universally certified in stationary no-boss-attack cases |
| Mage | `MageAdaptiveRotation` + `MageSimRunner` itemized Arcane/Fire/Frost ranks and effective stat bridge | Fire/Frost projectile, Arcane Mana/crit and cooldown effects, selected mixed-tree spells | `cp175-f4a-mage-three-tree.json`: legal Arcane/Fire/Frost 16/20/15, real Frostfire Bolt damage and replay | Full enemy Frozen/control AI, spell projectile travel, uncertain historical rank formulas remain separate |
| Warlock | `WarlockAdaptiveRotation` + `WarlockSimRunner` across three actual trees, owner+independent demon actors and Health/Mana | Affliction DoTs, Demonology pet bonuses, Destruction direct damage, Life Tap, temporary auras | `cp175-f4a-warlock-demonology-three-tree.json`: legal 17/17/17, active Imp, actual damage owner and demon receipts, report/replay | Geometry, source-provisional periodic coefficients, unsupported historical effects and enemy AI remain |
| Hunter | Marksmanship/Survival/Beast Mastery typed runner-specific preferred rotations; shared pets, equipment, race, consumption | F3D4B learned active access under each preset; F4A extends off-tree Marksmanship Mortal Shots to Survival/BM owner ranged critical hits without leaking to pet damage | `CP175_F4A_HunterMortalShotsParity.cs`: 5-rank critical damage **2.30 vs 2.00**, exact paired critical receipts and unchanged pet damage with rank removed. `cp175-f4a-hunter-mortal-shots-pet.json`: legal 46 MM/5 Survival, real Sniper critical and pet action | More comprehensive off-tree Lightning Reflexes, Lacerating, deep Survival stat/proc parity; missing enemy movement/defense and pet family coefficients remain distinct |

### Functional correction and high-priority verification

The concrete F4A cross-tree combat discrepancy was **Mortal Shots**: in Survival- and Beast Mastery-starting Hunters, legally learned 5/5 Marksmanship Mortal Shots must boost qualifying ranged critical damage, while neither pet damage nor noncritical damage receives the owner-only critical multiplier. The correction is exercised through normal live damage/critical results; `CP175_F4A_HunterMortalShotsParity` compares real Sniper Shot critical receipts at 2.30 vs 2.00 with exactly matched seed, no duplicate effects and rank-removal reversal. All earlier F3D4B action, Focus and 19-family tests remain.

Warrior's 27/16/9, Mage's 22/25/6, Warlock's 32/16/4, and Hunter's 29/18/3 remain the **conservative mechanical classifications**, not source-certainty labels. No Warrior, Mage or Warlock core stationary DPS coefficient was silently retuned without a reproduced source-backed defect; their accepted mechanics are protected by the full existing numeric/event regression suites and the new real four-class Chrome scenarios. The observed Warrior browser validation required respecting the native Warrior report's ranked build and `meanDps` schema instead of incorrectly assuming caster-only report fields. This is a **test integration correction**, not a Warrior DPS formula change.

**Source provenance and stopping boundary:** supported Forever coefficients remain provisional unless independently confirmed on live server; no enemy spatial pathing, range, threat, control AI or raid simulation completeness is inferred. Several longer-term Category A cross-tree passive gaps (especially Hunter's deep Survival effects) warrant a separate continuation with real Agility source and executable conditions rather than invented constants. Browser evidence covers four representative selected scenarios, **not all** arbitrary cross-tree builds. The final-head GitHub Actions result is authoritative for acceptance; earlier green code heads do not certify later changes.


## CP175-F4B — Character builder and selected-state integrity (2026-11-04)

**Starting point:** PR #28, existing `cp175-shared-character-simulation` branch HEAD `5b9a88519b4f45de8e06cc08ca5845cfaedeaeae`, exact-head Actions #41900426316 success (3,053 xUnit, 60 Python, real Chrome). No new branch, PR, protected push or merge.

### Source-of-truth integrity matrix

| Configuration | Frontend selection / serialized field | Server validation and precombat stats | Real combat path | Export/import and verification |
| --- | --- | --- | --- | --- |
| Class / race | `classKey`, `race`; class change clears `cfg.equipment` and `cfg.effects` | `ForeverPlayableRaceCatalog.Validate`; explicitly selected reviewed/provisional level-60 baseline; racial passives | Four typed class adapters and racial stat processors | `saveConfig` / `applyConfig`; F4B Chrome class-transition check rejects stale mage gear, buffs and consumables |
| Starting preset / actual talents | `specialization`, itemized `talents.ranks`, exact tree totals | Talent catalog, itemized 51-point tier/prerequisite validation and typed class checks | Legal learned actives, class-specific passive stats and automatic/custom priorities | Export saves full ranks; F3A–F4A mixed-tree browser/replay suites retained; changing an edited preset requires explicit replacement confirmation |
| Equipment and enchants | `cfg.equipment.items[{slotKey,itemId,enchantSpellId,temporaryEnchantSpellId}]`; Warrior preparation receives the exact normalized loadout | `/api/forever/equipment/resolve`, slot/class/race and enchant-target checks; `ForeverEquipmentStatBridge` | Equipment stat contributions and only certified item/enchant effects | F4B adds validated **Unequip** action; removes item and both enchants, refreshes stat preview and exports empty slot; CP172/F2B1 actual enchant combat tests retained |
| Food / elixirs / external player buffs | `cfg.effects.consumables` and `cfg.effects.buffs` → typed `externalModifiers` | `ForeverExternalModifierCatalog.Resolve` enforces ID, level, exclusivity, duration; shared profile stat contributions | Supported strength/agility/AP/intellect/spell-power effects | F4B selected modifiers included verbatim in typed request, completed report and exported build; **new fix:** toggle or target-debuff mode change schedules backend preview recalculation |
| Target debuffs | `cfg.effects.targetDebuffs`, `targetDebuffMode` | Category and mutually exclusive group validation | Existing target-debuff combat processor; initial-only versus maintained | Selection and mode persist in build, request and report; no unselected application |
| Combat consumables | `request.combatItems` charges, `disabledItemKeys` and skill; `preparedCombatConsumables` in export | `CombatUseItemsOptions.Validate`, class and inventory limits, cooldown and threshold policy | `CombatUseItemProcessor` spends only selected enabled stock | `CP175_F4B_ConsumableEligibility.cs`: absent, stocked-disabled, enabled, insufficient mana deficit and same-seed receipt checks; potion not created from class defaults |
| Racial cooldowns | `racialActive`, selected race | Legal class/race baseline, racial eligibility | Existing `ForeverRacialActiveProcessor`; no unsupported free racial | Selected value exported and restored; F4B Orc Warlock/Hunter builds exercise real racial path, existing CP173 source comparisons retained |
| Hunter pet / Warlock demon | `companionEnabled`, `petFamily`, `demonType` | Family/demon keys and typed `petEnabled`, `hasPet`, `enablePet` | Independent pet Focus/family attacks or demon owner/demon damage, conditional owner passives | F4B Hunter Cat and Warlock Imp remain selected with attributed combat; petless/disabled regressions preserved |
| Final stats and reporting | `preview()` requests backend `generate-level60`; `body()` creates typed request | `effectiveStats` and contribution provenance from the existing calculator; temporary proc stats are not asserted permanently | Class runner combat state, resolved equipment, temporary events | Full exported report embeds build, typed request, resolved character, selected effects, server receipts and deterministic replay |

### Confirmed corrections, executed comparisons and boundaries

- **Stale displayed stats (confirmed):** external modifier tiles and target debuff mode changed stored configuration without scheduling new backend character-stat preview. F4B now invokes the existing debounced stat refresh on those state changes; a Chrome fixture disables/restores Arcane Intellect, confirms an actual displayed Intellect decrease and exact restoration with no duplicated buff, and saves a screenshot.
- **Unremovable equipment (confirmed):** item picker supported replacing but not directly unequipping the current item. F4B adds a server-validated `Unequip` action for the selected slot; a real Chrome Mage removes an enchanted/temporarily enhanced main-hand weapon, confirms empty displayed slot and refreshed stats, exports the revised build, and verifies the item and both enchants are absent while unrelated buff selections remain.
- **Controlled four-class stat reconciliation:** `CP175_F4B_CharacterStatIntegrity.cs` starts from source-labeled level-60 provisional baselines, applies one opt-in selection at a time, removes it and verifies exact restoration. Warrior 20 food Strength + 25 elixir Strength + 16 Mark of the Wild Strength = **+61 Strength**; Mage +22 Nightfin Soup and +35 Greater Arcane Elixir = **+57 Spell Power**, with +31 Arcane Intellect; Warlock +28 Baked Salmon and +35 elixir = **+63 Spell Power**, with +31 Arcane Intellect; Hunter +40 food ranged AP, +25 Mongoose Agility / +2 attack crit, and +139 Battle Shout **melee-specific AP** (not an invented generic AP). Exact Mage registered weapon/enchant equip→remove→reequip preserves numerical identity. Buff/debuff category and food exclusivity violations fail closed.
- **Combat inventory receipt coverage:** `CP175_F4B_ConsumableEligibility.cs` tests Mage stocked-but-disabled and zero-stock Major Mana Potion never cast; enabling a truly stocked eligible potion produces exactly one Mana-restoring event and deterministic replay; a fully supplied character below the required Mana deficit does not waste it. Additional external food or buffs cannot resurrect a disabled potion.
- **Four browser builds:** `cp175-f4b-warrior-arms-fury.json`, `cp175-f4b-mage-three-tree.json`, `cp175-f4b-warlock-demonology-three-tree.json`, `cp175-f4b-hunter-mortal-shots-pet.json` retain accepted F4A legal mixed-tree ranks and add individually named registered items, known compatible enchants, supported food/elixirs/buffs/target debuffs, and selected class/race/pet/demon. The existing Chrome harness checks exact exported modifier IDs, equipment/enchant identity, typed request, effective backend stats, actual actor damage, build import/export and seeded replay.

**Known limits:** Source-provisional Forever item values are not upgraded to verified; bulk equipment intake remains blocked, some equipment effect procs are intentionally unmodeled, the four selected Chrome builds do not prove every possible item or class/race combination, and encounter AI/control/range are not introduced. Preset replacement on a modified build requires explicit user confirmation; automatic preservation across *arbitrary* preset switches is not claimed. Detailed caller stat-preview mode can differ from transient combat stats, and numerical parity is asserted only where the existing derived conversion has supported source data. Existing Warrior, Mage, Warlock and Hunter talent mechanical completion counts remain unchanged.

**CI gate:** Exact-final-head workflow is required to certify all F4B fixtures; intermediate heads or previous F4A green runs are not final F4B acceptance.


## CP175-F4C/D continuation — report comparison deliverable (partial)
- Added `solo-dps-comparison.js` report-only snapshot and compare contract, with fail-closed schema imports and stable encounter comparison. It distinguishes mean seeded DPS from representative-run total/owner/pet/ability damage; missing attribution is not fabricated as zero.
- Results UI supports selecting the current completed run as A/B and importing exported full reports into either slot. Changing class or another build does not silently overwrite comparison slots. Incompatible class, seed, iteration count, duration, target/health/armor/resistance, scripted adds, buff/debuff/food and baseline assumptions suppress the numerical DPS delta.
- Added Node numerical/validity regression and actual Chrome CDP comparison interactions/screenshots to existing CI, without a new framework or combat scheduler changes.
- Release-era evidence remains provisional until reproducible client observations are supplied, measured, and reconciled. No in-game live-log certification is claimed.
- **Open:** independent four-class report damage reconciliation, complete cooldown/encounter event certification, icon completion, full four-class F4C/D acceptance and newly sourced released-game formulas. This is a partial stage, not checkpoint sign-off.

## CP175-F4C/D — report integrity and readable combat receipts (2026-11-09)

Starting remote HEAD: `8fa1b75dbbd54fd498523c548039179a81a252f8`, draft PR #28. Last accepted remote CI remains [42666027337](https://github.com/layoung3/WarcraftSim/actions/runs/42666027337); it does not certify this milestone.

Implemented corrections:
- Complete report imports validate nested schema, typed class/preset, finite metrics, seed samples and request/build/raw-response consistency. Boolean/blank/null values are not fabricated as numeric zero. Inconsistent owner/pet/Hawk, ability totals or DPS/duration block comparison deltas.
- Mage and Warrior export their known owner-only attribution; Hunter exports independent Hawk damage. A/B comparisons label each column's single-run or mean-DPS mode independently. Representative damage is never called mean damage.
- A compact readable event table exposes actual action timing, resource deltas and remaining totals, auras, encounter transitions and damage, with filters. Periodic scheduler events are not counted as additional damage. The view identifies representative-run events and discloses its first-200-row display limit; the full JSON is preserved.
- Twelve preset-level numerical tests independently reconcile total, ability and damage-event receipts, covering all four supported classes. Warrior incoming damage remains separate from owner damage.
- Windows Chrome acceptance exposed a real download race: file existence preceded complete JSON availability. The harness now waits for finalized, parseable downloads and fails explicitly after a bounded timeout; assertions were not removed.

Local evidence: strict .NET 10.0.401 Release build passed with zero warnings/errors; 3,075 xUnit tests passed, none skipped; 60 Python tests passed under installed Python 3.14; Node DOM, comparison and event contracts passed. Real Chrome completed nine caster/Hunter spec routes and 77 CP175 build import/simulate/export/replay fixtures, including Warrior, scripted encounters, four-class selected equipment/modifiers and negative learned-action checks. Zero unexpected browser errors and zero stat-refresh failures. Actual completed exports were independently self-compared by the report contract. This is local evidence, not a claim of new exact-head Actions acceptance.

Screenshot review: `cp175-f4c-resource-events.png` confirms readable resource changes including remaining Mana; matched/mismatched comparison screenshots and the existing fixture screenshots remain in the local capture collection. The local worktree contains `browser-final-local.log`, `tests-final-local.log`, `build-final-local.log`, and `acceptance-final-*` full reports. These generated files and downloaded copyrighted artwork are not committed.

### Actions cost and storage audit / execution gate

The repository is public and the only workflow uses standard `ubuntu-latest`, so standard compute is documented as free: https://docs.github.com/en/billing/concepts/product-billing/github-actions. The account's 1,802-minute warning cannot be attributed to WarcraftSim from this evidence. Account billing/budget details are inaccessible. Latest inspected run retained about 19.73 MiB, primarily 17.2 MiB screenshots and 1.96 MiB reports; existing screenshot/icon/census retention was 14 days and full reports 30 days.

No artifacts were deleted and no budgets, billing plans, permissions, required status checks or workflow topology changed. All existing upload names remain because documentation references them. Routine PR retention is reduced to 7 days; explicit manual acceptance runs retain 14-day screenshots/source receipts and 30-day full reports. The workflow diff changes retention only. No repository download-artifact consumer was found, but external consumers cannot be ruled out, so icon uploads were retained.

**Push and workflow execution are on hold** until account storage billing risk is resolved or the owner makes the necessary decision. Main metadata reports unprotected; the detailed protection API returns 403, so protection requirements are not asserted absent. No additional Actions run has been triggered for this milestone.

**Next active stage:** investigate selected off-tree Hunter Survival passive parity, particularly Lightning Reflexes in Marksmanship/Beast Mastery, using existing source-provisional formulas and paired regressions. Do not mark full owner-review readiness until that investigation, final source-tree tests and exact-final-head CI are complete. All existing limits on movement/control/AI, incomplete item catalogs and provisional Forever coefficients remain. No live-server observations were invented or coefficients promoted merely because Forever launched.

### F4D responsive workflow correction and cost-gate update (2026-11-09)

Actual Chrome device-metrics verification at 390, 768, 1440 and 1920 pixels found no document-width overflow, but reproduced a functional narrow-layout defect: CSS hid the sidebar footer and therefore both build import and export controls. The footer remains available on mobile/tablet while only advanced stat details collapse. The new `tools/solo-dps-viewport.test.cjs` checks all six views at all four widths (24 combinations), visible import/export, and a reachable Simulate control; it runs inside the existing browser capture step. This changes no required CI job or permission. Before-fix browser assertion failed at width 390; after-fix all combinations pass. Device metrics are used because headless command-line window widths can be cropped by browser minimum-window behavior. The screenshots use `cp175-viewport-{width}-{view}.png` in the existing capture directory.

The owner supplied account billing evidence showing an Actions budget of **$0**, with usage blocked at that budget and $0 paid spend. The no-paid-usage gate is therefore resolved for the planned cohesive milestone push and its existing workflow. Do not change the budget. Artifact billing attribution to this public repository remains unverified; reducing retention reduces stored volume, not a proven dollar saving. If GitHub blocks uploads or compute at quota, report the blocker rather than deleting artifacts or authorizing charges. Earlier hold statements above record the prior state, not a current prohibition. Final new exact-head CI is still pending.

## CP175 completion milestone: scope and remaining confidence limits (2026-11-09)

This section supersedes earlier checkpoint status statements for the supported stationary level-60 Solo DPS scope. Earlier sections remain historical receipts, not a current list of unfinished work. Final acceptance still requires the exact pushed head to pass Actions; local tests alone are not remote CI acceptance.

Implemented in this milestone:
- F4C: strict full-report imports and request/build/response reconciliation, meaningful A/B scenario compatibility, distinct mean-DPS versus representative-run damage labels, and readable filtered action/resource/aura/encounter/damage receipts. Actual cooldown casts are shown; guessed cooldown availability is not invented. The compact event view shows at most 200 rows and preserves the complete exported timeline.
- F4D: actual Chrome device-metrics tests cover all six views at 390, 768, 1440 and 1920 pixels. A reproduced narrow-layout bug hiding build import/export is fixed. Real browser acceptance is required, not silently skipped when Chrome is missing.
- Artwork: 21 of 46 modifier tiles now have exact ID-and-name-matched historical source artwork with a pinned manifest and checksum. The other 25 remain neutral: 15 lack catalog IDs, eight are absent from the pinned source, and two have conflicting names. No guessed IDs, artwork binaries or mechanic changes are committed. See `CP175-modifier-icon-provenance.md`.
- Hunter stationary mechanics: raw Agility is carried explicitly, Lightning Reflexes applies across starting presets, temporary stat deltas reverse exactly once, and Mongoose Agility no longer adds generic AP to RAP a second time. Off-tree hit, melee-ability crit, critical-damage, resource regeneration, and melee AP scaling have direct regressions. Owner-only Hawk/Beast aspect AP is excluded from companion inheritance even when equipment, racial or Trueshot callbacks occur.
- Hunter preview: displayed precombat stats include supported Lightning Reflexes, Lethal Attacks and Surefooted contributions without modifying the reusable input profile. Generic item/buff AP is source-attributed to RAP; conditional effects, active auras and companions remain combat outputs. A visible scope statement and actual Chrome rank removal/relearning test prevent claiming full transient combat parity.

Four-class review retains the existing Warrior Rage/Enrage/Gore Drinker/Death Wish and damage-attribution tests; Mage learned-action, Cold Snap, resource and Arcane Mind integrity; and Warlock owner/demon scaling, channel ownership and Demonic Pact coexistence receipts. No speculative new coefficient was introduced to make an audit count look complete.

Hunter's conservative classification is now 24 fully modeled, 23 partial and three unmodeled nodes. The lower full count corrects earlier overstatements: selected MM/BM Expose Prey, Lacerating Strikes, Clever Traps, Survival Tactics, Improved Wing Clip, Entrapment, Deflection and Survivalist remain explicitly limited rather than silently advertised as universally executable. Movement, enemy AI/control, full spatial trap behavior, unsupported utility, incomplete source catalogs and live-server coefficient certification remain outside this tested scope. Historical/provisional source confidence is independent of passing software tests.

All work remains on existing draft PR #28. No merge, deployment, budget change, artifact deletion, security permission change or additional permanent PR is authorized by these results.

### Final local verification receipt (2026-11-09)

Frozen source passed strict .NET 10 Release with zero warnings/errors; **3,148/3,148 xUnit tests**, zero skipped (including 70 new Hunter regression cases); **66/66 Python tests**; Node UI/event/report contracts; all 21 pinned artwork checks; 77 real Chrome build import/simulation/export/replay fixtures and nine caster/Hunter routes; all 24 responsive view/width combinations; and actual Hunter Lightning Reflexes rank 5 -> 0 -> 5 sidebar changes with exact restoration. Browser stat refresh failures: zero. Expected validation-rejection responses remain asserted; no unexpected browser error is accepted. Independent read-only review found no remaining confirmed issues after the aspect, catalog-preview and authored-buff corrections.

Full reports are retained locally in the completion acceptance directory and screenshots in the existing capture directory; generated files and binary artwork are not committed. The final pushed SHA, Actions run URL and remote gate result must be recorded in the PR verification receipt. This local receipt does not substitute for exact-head CI.


## Resumed integrity and cross-tree milestone (2026-11-09; local work in progress)

The last independently verified remote milestone is `c251ea3b423c460db7a1c1229aa2238e8682625c`, Actions run [42677632432](https://github.com/layoung3/WarcraftSim/actions/runs/42677632432). It is not final simulator-completeness certification. The owner explicitly authorized continuation on the existing draft PR #28, local API/browser tests, scoped evidence scripts and audit updates, and verified milestone pushes. No merge, deployment, additional permanent PR, paid usage, billing-limit change or historical-artifact deletion is authorized.

Implemented locally, with final combined acceptance still pending:

- Caster request isolation prevents repeated simulations from mutating caller profiles or inventory. Demonic Embrace no longer doubles its Stamina contribution. Snapshots preserve all current dictionary comparers, nested values and null-validation semantics; regression tests cover case-insensitive and case-sensitive inputs.
- Warrior shared equipment effects retain their AP/haste/physical contributions through policy rebuild and expiry. Physical actions retain physical mitigation. Handless Bloodthirst is eligible only for main-hand melee enchants; unknown handless Warrior attacks fail closed without changing other classes or item/set triggers. These effects retain their existing historical/provisional confidence.
- Warrior equipment preparation preserves original manual hand inputs, removes stale catalog-derived values when equipment is cleared, and retains intentional manual edits. Legacy prepared requests without recoverable original values fail closed with reset guidance. Explicit Protection shield, block, one-handed weapon and incoming-attack assumptions are labeled manual rather than inventing a catalog shield. Preview uses the same supported precombat defense calculations as the runner.
- Seven Warlock demons have explicit temporary/ritual assumptions, default off. Imported assumptions are strictly typed and preserved. Actual engine modeling warnings are visible in Results and after priority validation.
- Removing encounter adds remaps surviving actor references atomically and disables references to removed actors rather than silently attacking another target. Empty custom orders fail closed. Explicit zero armor survives import. Browser comparisons use actual independent A/B report file imports and retain old valid reports after invalid imports.
- Existing Survival event logic is shared with Marksmanship and Beast Mastery for Expose Prey, Mongoose/Lacerating, traps, Clever Traps, Survival Tactics and control markers. Melee/proximity and trap-trigger assumptions remain explicit. This supersedes the earlier blanket no-effect limitation for these core cross-tree actions, not the limitations for movement, enemy AI, unscripted incoming avoidance, defensive-only talents or provisional coefficients.
- Barrage-funded Arcane Missiles no longer consumes Clearcasting, which remains available for a subsequent eligible spell or expires naturally during the channel. Official source: [Blizzard November 2 notes](https://us.forums.blizzard.com/en/wow/t/2360696/1). Existing timing/cost tuning remains provisional.
- Acquisition tools enforce bounded retries/bytes/timeouts, Retry-After and cache policy, atomic writes and immutable source/build/hash receipts. Existing-only revalidation matched all 174 cached JPEGs byte-for-byte against their pinned sources. The 174 receipts total 127,313 bytes and contain public provenance, not credentials. Structural JPEG checks are not full codec decoding; provenance does not establish artwork licensing.

Evidence integration preserves all existing job names, permissions, artifact names and retention. Four additional browser suites run in the existing capture harness with isolated report subdirectories. The existing report artifact includes recursive JSON receipts, and the existing artwork artifact includes its bounded receipt sidecars. No separate upload job or paid service is introduced.

Local gates so far: the previous combined source passed 3,238 xUnit cases with zero strict-build warnings/errors; the subsequent Barrage fix passed 26 focused cases including nine additions. The resumed Python suite passes 106 cases, and Node UI/event/report/encounter/manual-assumption contracts pass. These are not yet a final aggregate test count or current-source browser pass. Newly added regressions and final browser runs must complete before publication.

### Remaining completeness gates and source boundaries

- Complete actual browser import/replay/encounter/comparison/preset/viewport checks and exact pushed-head Actions after the passing strict full .NET and Node/Python gates. Record failures as failures; no first-milestone evidence substitutes for this gate.
- Complete the source-supported core talent inventory. Mage armor-triggered Fingers of Frost/Frostbite requires a qualifying incoming melee/chill model; no assumption that every physical hit qualifies. Wand scheduling and projectile timing need source-backed semantics rather than invented coefficients. Missing live evidence remains explicit.
- Existing implementation inventory includes eight classes, 15 concrete runners and 25 spec/role labels. Unified Solo DPS currently exposes four classes. Balance and Elemental are the closest existing DPS adapters, followed by Rogue/Enhancement and Shadow/Feral Cat. Existing healing/tanking engines remain separate; Paladin metadata is not an implemented runner. These expanded workflows are not certified by four-class tests.
- Reuse the versioned importer and reviewed catalog. Identity/icon metadata is not combat-stat or effect certification. No additional combat-ready item batch has been independently verified. ForeverDB requires provider approval and an API key; no account, key request or provider outreach has occurred. TheWoWDB bounded sample requests returned HTTP 530/1033 and Retry-After 120; no outage bypass was attempted. AHledger returned identity metadata but not build-qualified stats/effects. Existing reviewed baselines remain provisional proxies, not live observations.

This remains a partial milestone. Full completeness, independent review and owner release approval are separate gates.


### Resumed aggregate verification and review corrections

The frozen implementation passes strict Release build with zero warnings/errors and **3,303/3,303 xUnit tests**, zero skipped, plus **106 Python tests** and Node contracts. The first aggregate run caught a real interaction: moving Lone Wolf to live resolution also scaled Dense Dynamite. The existing test was kept unchanged; eligibility is now restricted to registered Hunter class attacks/autos, including background traps, excluding consumables and equipment/enchant procs. Twelve additional item/enchant parity cases cover petless, active, delayed and dead pets. This is compatibility containment, not new live-game certification of item interactions.

Independent review also found and corrected missing petless Lone Wolf on imported MM Survival actions and Freezing Trap markers surviving pet/Hawk damage. Mongoose is 297 -> 356.4, Wing Clip 50 -> 60, explosive tick 85.8 -> 102.96, and immolation 205.4 -> 246.48 under the existing 1.20 model. Lacerating snapshots the landed hit once. Positive pet/Hawk direct or periodic damage removes the frozen marker and invalidates aura conditions without granting owner-only procs. Read-only re-review found these reported defects resolved.

Final-current-source browser acceptance and new exact-head Actions are still pending at this entry. The local API is explicitly labeled as c251ea3 plus uncommitted milestone work; these local reports must not be presented as committed-head CI receipts.


### Final local browser gate for this milestone

Current implementation passed the existing Chrome build import/simulate/export/replay suite, 24 responsive view/width combinations, live Hunter sidebar rank removal/restoration, both MM/BM cross-tree control suites, all 12 presets and seven Warlock demons, Protection manual assumptions/Shield Slam/preview/import parity, encounter deletion and explicit retarget recovery, zero-armor replay, and 11 independent comparison/build-import workflows. The Protection harness initially waited for the caster-only success label; it now asserts the exact Warrior success state, block value and scope without weakening the product checks. New screenshots honor the existing artifact directory.

Independent report inspection self-compared **125 completed full-report exports** with no integrity warnings: core80, Hunter10, Protection/companions21, encounter6, comparison8. One empty default-name Chrome download placeholder in the encounter directory is retained but is not a receipt and is excluded from this count. Final coverage manifests report zero unexpected browser exceptions. Mobile equipment, comparison and Protection screenshots were visually reviewed; historical/manual source disclosures remain visible.

Generated local evidence is under `acceptance-integrity-resumed/`, with final Hunter and Protection receipts in their `-r2` subdirectories. Local logs are `build-integrity-resumed.log`, `tests-integrity-resumed.log`, `python-integrity-resumed.log` and `browser-integrity-resumed-*.log`. These generated files are not committed, and their honest uncommitted-source metadata is preserved. New exact pushed-head CI is the next publication gate; this milestone does not close the expanded class/catalog completeness assignment.


### Exact-head milestone CI receipt

Commit `7096830c19b6f5e3b40f3d07241c4d2182246f38` is verified on the existing PR branch. [Actions run 42692098564](https://github.com/layoung3/WarcraftSim/actions/runs/42692098564), job 129414180652, completed successfully for that exact head. Logs confirm 3,303 passing xUnit tests, 106 Python tests, the extended real-browser suites and all six existing artifact uploads. Full report artifact: 13708245984 (3,008,800 bytes); screenshot artifact: 13708156062 (24,033,162 bytes); pinned icon/receipt artifact: 13707797171 (442,075 bytes). The existing seven-day PR retention and job/permission topology are unchanged. No merge, deployment, budget change or historical-artifact deletion occurred.

This proves the milestone's software gate, not complete current-game accuracy or completion of the expanded eight-class/catalog assignment. The next bounded stage prepares existing Balance/Elemental requests, sourced baseline caps and one normalized potion inventory while preserving old direct-preview numerics. Those classes remain outside the unified UI until their adapters and acceptance gates are complete.


## Existing Balance and Elemental input preparation (local stage)

The optional baseline inputs follow the established Mage pattern. Requests are isolated before preparation, including dictionary comparison semantics after baseline generation. No-baseline requests retain legacy resource numerics and potion policy. Prepared requests use the pinned Druid BaseMana 1244 or Shaman 1520, existing provisional attribute conversion, explicit flat resources, and Ancestral Knowledge exactly once before resource snapshots. At controlled Intellect 200, expected prepared Mana is 3944/4220; Shaman rank5 Ancestral Knowledge yields 4520. Missing observed maximum Health uses the explicitly labeled manual scenario cap and provisional post-baseline Stamina delta, not an invented observation.

Prepared potion use has one normalized shared inventory and scheduler. Equal positive aliases deduplicate; conflicting quantities reject; legacy-only inventory adapts once. The old default legacy charge remains for compatibility, so future shared adapters must explicitly clear it when no consumable is selected. This fixes the duplicate major-mana-potion ability registration on the new prepared path without silently rewriting old preview policy.

Focused strict Release verification passes 85 cases, including legacy Druid/Elemental potion and 4508-Mana/40%-start expectations, source notes, resource contributions, invalid caps, input reuse and comparer preservation. Event replay compares deterministic timings/outcomes, not freshly generated event identifiers. Independent review and the next combined aggregate gate remain pending. Solo DPS UI/capabilities, strict itemized presets, gear/effect adapters and new spell formulas are not enabled by this stage.


## Warrior Weaponmaster per-hand correction (local stage)

Read-only audit confirmed that mixed axe/non-axe weapons lost the modeled axe crit bonus, and all dual-wield maces lost the modeled armor ignore. Sword proc eligibility already followed the correct weapon hand and was not rewritten. Warrior-local adapters now add mixed-hand crit only to qualifying explicit-hand direct melee effects, and apply dual-wield mace armor ignore to the actual target's current armor without mutating it. Existing homogeneous crit, single-weapon behavior, other actors, handless/shield effects, spells and bleeds are unchanged.

Thirty-five new regressions cover ranks 0/1/5, reversed hands, no double bonus, Whirlwind, dynamic Sunder, separate add armor, exclusions, sword extras and deterministic replay. Formula receipts for raw 1000 against 3000 armor are 647.058824 normally, 653.983353 with rank 1 mace, and 683.229814 with rank 5 mace. The combined strict solution build has zero warnings/errors and all 3,380 tests pass before a final small caster threshold-precedence test addition. Independent review found no concrete defects in either local stage.

Weaponmaster remains partial/provisional. The repository's 70707 reference is reviewed web data, not an immutable numerical tooltip receipt or live-combat calibration. The current WoW Forever DB page identifies 70862 and must not be relabeled as 70707. Handless eligibility and existing single-weapon edge behavior remain explicitly unresolved. Sidebar aggregate crit is not a mixed-hand conditional crit percentage. No class, mode, scheduler or Core architecture was added.


### Final local gate for prepared casters and Weaponmaster

Final strict Release build: zero warnings/errors. Full xUnit suite: **3,385/3,385 passed**, zero skipped; Python: **106 passed**; Node UI/event/report/encounter contracts passed. This adds 82 regression cases over the previous exact-head milestone. Independent reviews found the scoped changes consistent with the stated compatibility boundaries.

Additional prepared-potion regressions establish shared-threshold precedence for equal aliases and legacy-threshold copying for legacy-only inventory. Review also caught Balance's legacy one-second cast/GCD floor affecting instant shared items. Resolved-baseline runs now preserve declared timing only for known combat-item catalog keys; class spells and no-baseline timing are unchanged. Event tests prove instant recovery, the existing microsecond executor guard, and correct behavior while Nature's Grace affects the following class spell. Resource, cooldown and input-lock checks remain in the executor.

Actual HTTP checks of both existing controllers succeeded with explicit supported hypothetical race selections, historical-provisional baselines and one shared potion use each. An unspecified race correctly failed closed instead of inventing a baseline. These checks do not enable a new unified UI workflow.

Current-source Chrome passed core build/import/simulation/export/replay, all 12 presets and seven demons, Protection, target removal/retargeting, zero armor, and 11 comparison workflows. **115 completed report exports** independently reconcile without integrity warnings (core 80, Protection 21, encounter 6, comparison 8). Evidence remains locally under `acceptance-caster-weaponmaster/` with honest uncommitted-source labels; generated reports are not committed. The next pushed-head Actions result remains a separate gate. The expanded class/catalog completion assignment remains open.


### Prepared-caster and Weaponmaster exact-head CI

Commit `df4565dab33594073a30bcab93b6ae72e2882d0d` is verified on the existing draft PR branch. [Actions run 42698736633](https://github.com/layoung3/WarcraftSim/actions/runs/42698736633) completed successfully for that exact head. Job 129437484683 confirms 3,385 passed xUnit cases, 106 Python cases, the extended browser acceptance suites and six successful artifact uploads. Exact-head evidence: [reports](https://github.com/layoung3/WarcraftSim/actions/runs/42698736633/artifacts/13710846246), [screenshots](https://github.com/layoung3/WarcraftSim/actions/runs/42698736633/artifacts/13710012745), [icons and provenance receipts](https://github.com/layoung3/WarcraftSim/actions/runs/42698736633/artifacts/13710941414). Existing retention, permissions and workflow topology remain unchanged. No merge, deployment, paid-service enablement or historical-artifact deletion occurred.

The following static gear stage is separate and still local. Empty AllowedClasses cannot be treated as verified unrestricted eligibility: existing model/importer defaults lose missing-versus-explicit provenance. New prepared adapters must fail closed using explicit source-backed class membership and full existing equipment validation, not a hardcoded second catalog. Known older-build records retain their own source versions, not a claim of current 70707 verification. Shaman weapon eligibility, broader catalogs, dynamic effects and unified UI exposure remain open gates.

## Prepared static caster adapter: local implementation and source gate

The local prepared-only adapter reuses shared static equipment, enchant and external-modifier resolution. It normalizes MP5 aliases, incorporates Ancestral Knowledge before derived spell crit, preserves instant class-spell timing and isolated request comparers, and rejects unsupported dynamic effects and target debuffs. Legacy/no-baseline paths and unified UI capability exposure are unchanged.

The initial local implementation passed a strict Release build, 3,456 xUnit cases and 106 Python cases. HTTP checks demonstrated deterministic static deltas and four rejection paths. These numerical checks do not establish item eligibility. A later source audit found that nonempty AllowedClasses lists are not sufficient proof: historical records 19102, 22436 and 22476 lack retained restriction-specific receipts. They predate the bulk normalizer, so their lists are not attributed to its newly discovered blank-mask defect. Existing records must remain unknown rather than being relabeled verified. Publication is held while additive restriction provenance and fail-closed prepared admission are implemented; real gear support is not claimed.

Read-only intake tracing also found that blank AllowableClass values default to -1, after which armor proficiency can turn unknown restrictions into nonempty lists. Unsupported negative masks escape the current quarantine prefix set. The bounded correction must preserve explicit source restrictions separately from proficiency, reject ambiguous new combat intake, and retain an explicit legacy compatibility boundary without inventing catalog facts.

Initial browser acceptance failed once at an obscured talent Reset button after Save. A single clean rerun passed all nine routes and 77 fixtures; no root cause was established and no product UI change was justified. Only failure diagnostics were expanded. Independent comparison, encounter and Protection/companion suites passed, and 115 completed full-report exports reconciled without integrity warnings (80 core, 8 comparison, 6 encounter, 21 Protection). Evidence is retained under acceptance-static-caster with honest precommit labels. These results precede the pending source-evidence gate and are not the final milestone acceptance.

### Evidence-gated correction and verified local contract

The specific equipment-safety correction preserves the earlier adapter and all historical catalog JSON. New optional classRestrictionEvidence records carry item identity, source kind, raw mask, source-only decoded classes, client build, source URL/digest, interpretation status and confidence. Missing evidence is unknown/pending, not a permanent class prohibition. Explicit supported positive masks can qualify through reviewed pinned intake; missing, zero and unproven all-bits sentinel semantics remain unknown metadata. Tests demonstrate later valid evidence admitting the same previously unknown item.

New public imports require consistent provisional DB2 evidence and reviewed Item/ItemSparse digest bindings. Embedded historical loading alone uses an internal compatibility entry point; no request/manifest flag can select it. Source restrictions are checked independently from armor proficiency. New Balance/Elemental catalog use rejects unknown receipts, conflicting lists, request-supplied fake receipts and unsupported effects. Weapon admission remains pending separate proficiency evidence. Existing Mage/Warlock/Hunter/Warrior and legacy preview behavior are preserved. Receipt consistency is not independent proof that a source or reviewer is truthful.

Strict Release build passes with zero warnings/errors; full xUnit suite passes **3,496/3,496**, zero skipped, and Python passes **126/126**. The existing Python-to-.NET smoke confirms exact receipt round trips, unknown metadata preservation/exclusion and unchanged legacy records. Independent code review resolved URL, digest-case, duplicate replay, boolean-ID and weapon-proficiency findings. No real catalog facts were invented or altered.

Actual HTTP acceptance covers both prepared casters with authored budgets/static modifiers, deterministic replay and rejection of the three unproven real items. A second isolated loopback API loads a clearly synthetic SHA-pinned armor manifest through the existing startup overlay, proving strict import to shared resolver to prepared simulation: +900 Mana, +140 Health and +75 Spell Power with selected modifiers/enchant. The synthetic fixture is not a real item or proof of game eligibility. Unknown metadata remains absent from combat selections; legacy rows remain pending in the new path. Both isolated processes are owned by the harness and never deploy software.

The existing capture script now includes these API receipts in its existing bounded report directory, with no workflow job, permission, artifact name or retention change. The local API evidence folder contains 32 files totaling about 1.43 MB before compression; generated receipts are not committed. Local source labels remain honest precommit labels. Real source eligibility, broader equipment coverage, dynamic caster effects, strict talents/rotation/encounter adapters and unified UI exposure remain open gates.

Final post-correction browser gates pass: all nine core routes, 77 fixtures, talent save/reset/load, 12 presets, seven demons, Protection assumptions, encounter deletion/retargeting/zero armor and 11 comparison workflows. Independently reconciled 115 completed reports without warnings (80 core, 8 comparison, 6 encounter, 21 Protection), under acceptance-restriction-final. The earlier intermittent obscured-click failure did not recur; its root cause remains unproven. Frontend contracts, Node syntax and whitespace checks also pass. Task-owned local APIs were stopped after acceptance. This is a verified local milestone, pending exact-head CI and still not completion of the expanded simulator assignment.

## Follow-up CI smoke alignment and Rogue input preparation

Exact-head run 42715064724 for 82125a344b88f3071b500b548e8c29f9ccb2f265 passed build/tests, browser gates and isolated synthetic prepared acceptance, then failed the old CLI smoke because that step attempted to newly import the historical embedded CP171 manifest. Its missing eligibility evidence is now correctly rejected. The corrected mandatory smoke explicitly verifies that rejection with no output, then imports the valid pinned synthetic fixture twice, compares both catalogs/reports byte-for-byte and checks one reviewed override. All earlier provenance/unknown assertions remain. Only the command body changes; job/check topology, permissions, artifact retention and real catalog data are unchanged. The exact replacement passed locally before publication.

The next bounded Rogue stage fixes caller mutation when disabling poisons by isolating the full request and preserving dictionary comparers. Optional existing baseline preparation retains no-baseline numerics, applies observed AP exactly once, and discloses supplied historical/provisional Health versus manual scenario fallback. Energy, rotations, tuning and UI scope are unchanged; prepared catalog/enchant selections remain unsupported rather than falsely resolving gear. Independent numerical tests prove 700 observed AP plus 200 authored contribution gives 900 effective AP and 190 normal-swing damage. Null equipment, item lists and entries return 400 without mutating caller data.

Final strict Release build has zero warnings/errors; **3,515/3,515 xUnit tests** pass, including 19 new Rogue cases. Python remains 126 passing cases, and the strengthened cross-language smoke passes. Independent review found no remaining scoped defect. Actual local HTTP acceptance exercised all three Rogue specs with deterministic prepared replay and malformed/catalog rejection; generated receipts are local and honestly precommit-stamped. These are existing backend workflow improvements, not unified UI availability or complete Rogue talent/equipment validation. Exact-head successful CI remains required before calling this milestone verified remotely.
