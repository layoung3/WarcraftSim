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
