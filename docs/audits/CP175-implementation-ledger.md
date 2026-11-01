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
