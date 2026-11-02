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
