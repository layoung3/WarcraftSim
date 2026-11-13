# CP175 Stage A — Existing-system audit and migration contracts

**Base**: merged CP174 PR #27, `main` commit `ad92d8b35fb3145f6816d86953c15852a7c2d283` (2026-11-01). New work is isolated in the CP175 branch. This is a development audit, not complete four-class certification.

## Verified authoritative systems (reuse rather than rewrite)

| Concern | Existing owner | Semantics that MUST survive |
|---|---|---|
| Simulation event queue, casts, cooldowns, resources | `WarcraftSim.Core/Simulation/Engine` | Owns combat events/auras; never recompute damage or resources in JavaScript |
| Seeded batch statistics | `SimulationBatchRunner`, `SimulationBatchSummary`, `DistributionSummary` | Iterations use `unchecked(baseSeed + index)`, representative seed is closest to the median. Current distribution uses **population** variance (divide by N) and linearly interpolated order statistic at `p*(N-1)`; P99 is not yet exposed |
| Warrior DPS/tank | `ForeverWarriorSimRunner` | **Already supports seeded iterations** and per-ability results; tank solo preview scores incoming damage without draining finite HP, while integrated Warrior+Priest retains actual death/TTD. Preserve these distinct policies |
| Mage/Warlock/Hunter | Specialized typed runners | Engine behavior and pet/demon actors must not be rewritten merely to unify configuration |
| Combat consumables | `CombatUseItemsOptions` + `CombatUseItemProcessor` + `ForeverCombatItemCatalog` | Real, shared inventory with class eligibility, cooldowns, health safety and actual ability effects. Existing heuristic accepts about half of mean recovery: **NOT** the requested maximum-restoration no-overcap rule |
| Static buffs and debuffs | `ForeverExternalModifierCatalog` + `ForeverExternalTargetDebuffProcessor` | Explicit source-only catalog; no TBC Battle/Guardian exclusivity; maintained/initial-only debuff mode. Warrior currently uses **different support catalog** `ForeverSupportEffectCatalog`; no verified equivalence yet |
| Temporary weapon enhancements | CP172 equipment resolver / `temporaryEnchantSpellId` | Distinct from permanent enchant; existing engine owns execution |
| Rank-derived talents | `ForeverSoloTalentController.ValidateItemized`, `ForeverItemizedPresetCatalog` | New frontend requires full ranked selection for Mage/Warlock/Hunter. Warrior has its own complete `ForeverWarriorTalentCatalog` and `TalentMode=tree`. Arbitrary cross-tree ability execution is not certified for typed Hunter |
| Rotation | `MageAdaptiveRotation`, `WarlockAdaptiveRotation`, `WarriorRotationRules`, Hunter per-spec rotation methods | Caster preview endpoint currently exposes only Mage+Warlock. Warrior owns richer automatic/custom/maintenance policies. A shared UI must surface class-specific capabilities without pretending every condition is available |
| Existing frontend | `wwwroot/solo-dps.html/.js/.css` | Single-run Mage/Warlock/Hunter, tabs for talents/modifiers/encounter/results; rotation is nested inside encounter; stat refresh is manual; results lack statistical iterations |
| Level-60 snapshot | `ForeverCharacterController.GenerateLevel60` | Keep baseline provenance and separate ready vs resolved profiles; do not double-count stats in runners |

## Major discrepancies to resolve in implementation stages

1. **Warrior vs caster character input:** Warrior has `WarriorSimRequest` with gear mode, custom rotation rules, multi-iteration batch, tank policies and a *different* support catalog. Do not naively bind this to the Mage/Warlock `ForeverLevel60SimulationRequest` payload. First establish a class-agnostic **validated envelope** and adapters; only declare execute support after actual mapping tests.
2. **DPS benchmarking survival:** Warrior's solo tank intentionally scores lethal bursts without depleting finite HP. Warlock Life Tap and rune costs currently use genuine Health; running through death would require a new explicitly named nonlethal benchmark policy. Do not silently heal or disable deaths in existing APIs.
3. **Stats methodology:** Batch runner already has mean, population standard deviation, median, P95 and other percentiles. Add P99 with exactly the same interpolation. Do not present a one-run estimate as a distribution; do not invent sample standard deviation. Document population vs sample convention clearly.
4. **Food and external support:** CP173 selected food is applied through real resolved stats to caster pipelines, but Warrior maps support effects through its separate catalogue. Unifying requires a source-key reconciliation matrix to avoid duplicate or unsupported stat application.
5. **Available consumables:** Warrior has Rage Potion and Engineering, Hunter has Mana/Engineering, Warlock uses Life Tap but should retain mana consumables; Mage has its own legacy Major Mana Potion separate from `CombatUseItemsOptions`. Never introduce an extra hidden potion.
6. **Item value policy:** The current gem eligibility threshold is based on *at least half of expected recovery*, whereas the CP175 conservative requirement is *missing resource >= maximum restoration*. Change this via an explicit policy with backward-compatible legacy default only if required; test all item tiers, no-overcap and cooldown competition.
7. **Itemized arbitrary cross-tree:** Unmodeled historical-only talents can consume points but must never acquire imaginary combat effects. A starting preset must never become a specialization lock. For cases where typed runners cannot execute selected modeled ranks, fail with a concrete capability error until the shared adapter is corrected.

## Incremental acceptance gates

- **Stage A:** audit, capability metadata, schema/validation contracts, batch statistics parity tests. No behavioral changes to existing class runners.
- **Stage B:** one authoritative cross-class modifier/inventory adapter with compatibility tests; no duplicated class-specific item scheduler.
- **Stage C:** reconcile rank applicability across Mage, Warlock, Hunter and Warrior. Full tree/prerequisite validation and cross-tree effects must be measured in real combat.
- **Stage D:** separate Rotation tab and backend-owned class Encounter capability metadata, with real mutations/keyboard tests.
- **Stage E:** repeated seeded runs, distribution and P99, representative timeline, meaningful Results tables/charts, JSON receipt.
- **Stage F:** debounced/stale-safe live stat refresh and evidence-backed icon coverage census.
- **Stage G:** integrate Warrior fully only after stats/gear/talents/rotation/consumes agree with its existing runner; run four-class real browser submissions.

No stage is complete solely because its files compile; local or CI tests and actual browser interactions are required. PR stays draft/unmerged until owner acceptance.
