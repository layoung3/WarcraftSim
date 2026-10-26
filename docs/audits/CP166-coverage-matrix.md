# CP166 — Mechanical and Formula Audit Coverage Matrix

**Scope:** current WoW Forever level-60 simulator. **Updated:** 2026-10-26.
**Stage 1 baseline:** `f2f8a2e1f0a378fb07852f295915c020110a30b3`.
**Stage 1 merge:** `8f7faf1dc4c38d52410782dce4864e98ba166b3a`.
**Stage 2 baseline:** `8f7faf1dc4c38d52410782dce4864e98ba166b3a`.
**Stage 2 validation:** source and deterministic-test additions only; full .NET 10 build/xUnit must be run by the repository owner before a subsystem is considered completely validated.

The matrix is intentionally conservative: a formula matching another formula in this repository is **not** independent verification. Status definitions are the four categories required by the user:

- **Audited — verified**: executable implementation, appropriate independent Forever evidence **and** executed regression coverage.
- **Audited — provisional**: executable path reviewed, but numerical evidence and/or execution validation remains incomplete.
- **Audited — defects remaining**: specific independent defects still unresolved in the listed scope.
- **Not audited**: no meaningful end-to-end examination yet.

## Evidence register

| ID | Evidence | Quality / limits |
|---|---|---|
| F1 | [ForeverDB current beta client stats](https://foreverdb.net/stats), build `1.60.1.70450`, extracted 2026-10-19 | Client-derived for AP/stat, rating conversions, Defense tooltip, block value and 75% Armor cap. Explicitly labels the **armor-reduction curve speculative** and identifies server-only variables. |
| F2 | [Blizzard Oct 8 development notes via ForeverDB](https://www.foreverdb.app/en/forever/blue-tracker/us/30395251) | Official: incoming Rage uses expected creature health, includes crit/crushing, ignores absorbs and actual equipped Armor; assumed mitigation varies 20–40% with level; no published per-level expected-health/Armor curve. |
| F3 | [WoW Forever beta Warrior deep dive](https://www.publicnow.com/view/3B932506BC82DF22044E0CD4EA54093F908286F3) | Official high-level explanation of normalized outgoing/incoming Rage; follow later Oct 8 updates for refinements. |
| F4 | [Normalized Rage combat-log research](https://github.com/magey/forever-warrior/issues/3) and [WoW forums 60-model](https://us.forums.blizzard.com/en/wow/t/what-does-rage-actually-look-like-at-60-%E2%80%94-current-math-and-sims/2366159) | Community, controlled low-level observations for 3.46 MH / 4.50 2H / 1.73 OH; not a proof of every level-60 coefficient, extra-attack trigger or crit interaction. |
| F5 | [Forever beta glancing known-issues context](https://www.wofwforever.com/en/guides/wow-forever-weapon-skill-scaling/) | Secondary account of official known issues: glancing damage penalty against higher-level enemies and caster glancing outcomes not reliable. |
| R | Current repository executable source, Stage 2 baseline SHA above | Primary evidence for **implemented behavior**, never an independent confirmation of Forever's numerical rules. |
| U | Earlier Formula Watch discussions | Research leads only unless original log/build and timestamp are linked. Older findings may predate Oct 8 changes. |

## Stage 1 — completed correction, local validation still required

| Subsystem | Executable source reviewed | Mechanic and correction | Sources | Regression tests | Status / open questions |
|---|---|---|---|---|---|
| Damage-event mitigation metadata | `Core/Simulation/Engine/AbilityExecutor.cs`, `ScriptedEncounterDamageProcessor.cs`, `EncounterBossMeleeProcessor.cs` | Ability-generated damage and avoided-hit events now carry the declared mitigation type. | R, F2 | `CP166_SharedDamageMitigationEvents.cs` | **Audited — provisional** until full suite executes. |
| Incoming-Rage resource event metadata | `Core/Simulation/Engine/DamageTakenResourceGenerationProcessor.cs` | Incoming-Rage event now carries resource name and balance captured at emission. | R, F2 | `CP166_SharedDamageMitigationEvents.cs` | **Audited — provisional** until full suite executes. |

## Stage 2 — shared combat mathematics

| Subsystem | Source inspected (project-relative) | Reviewed mechanics / findings | Verification | Corrections / tests | Status and remaining unknowns |
|---|---|---|---|---|---|
| Primary STR/AGI/AP/RAP/Armor conversion | `Data/Forever/Combat/ForeverPrimaryPhysicalStatContributor.cs`, `ForeverPrimaryPhysicalStatProfiles.cs`, `Core/Simulation/Building/BaseStatsCharacterSimulationRuntimeCalculator.cs` | Class-dependent primary-stat conversions and +2 Armor/AGI; applied after equipment/talents/buffs, not double-added by builder itself. | F1, R | Existing `Forever_Stats_Formulas_01/02.cs`; new `CP166_Stage2_CombatMathematics.cs`. | **Audited — provisional**: full nine-class end-to-end stat builder and final build not executed here. |
| INT, spell crit, derived spell stats | `ForeverSpellStatConversionContributor.cs`, `ForeverPrimarySpellStatProfiles.cs`, `ForeverSpellStatConversions.cs` | Level-60 client table and separate spell crit/hit/haste rating conversions; missing levels throw rather than interpolate. | F1, R | Existing Forever stats tests; Stage 2 level-60 assertions. | **Audited — provisional**: baseline crit and Spirit coefficients server-side/unverified. |
| Rating and block conversion | `ForeverPhysicalStatConversions.cs`, `ForeverPhysicalStatConversionContributor.cs` | Hit 10, crit 14, dodge 12, parry 15, block 5, Strength 20 per Block Value; flat across levels. | F1, R | Stage 2 direct math tests. | **Audited — provisional** until test execution. |
| Melee attack tables: white, dual and special | `ForeverPhysicalCombatRulesetFactory.cs`, `ForeverMeleeAttackProfiles.cs`, `RulesetCombatRollResolver.cs`, `OrderedCombatRollTable.cs` | White 95% equal-level; dual-white 76%; specials 95%; ordered mutually exclusive miss/dodge/parry/glance/block/crit/crush. Glancing disabled for specials. | F1, R | `Combat_Resolution_01/02.cs`; Stage 2 attack-table and mutually-exclusive outcome tests. | **Audited — provisional**: exact spell/weapon level penalties and some positional combinations not fully corroborated. |
| Weapon Skill, Defense and creature level | `ForeverWeaponSkillCombatRollAdjustmentProvider.cs`, `ForeverDefenseCombatRollAdjustmentProvider.cs`, `ForeverCreatureLevelCombatRollAdjustmentProvider.cs`, `ForeverCombatRollContextAdjustmentProvider.cs` | Source skill, level differential and target Defense adjustments combine once; extra Defense contributes 0.04 percentage points per skill. | F1, F5, R | `Forever_Stats_Formulas_01.cs`; Stage 2 Defense invariant. | **Audited — provisional**: skill-dependent miss/glancing slope needs independent logs; do not trust currently buggy higher-level glancing penalty. |
| Glancing and crushing | `OrderedCombatRollTable.cs`, `ForeverMeleeAttackProfiles.cs`, `RulesetCombatRollResolver.cs` | Single-roll outcomes and absent glancing penalty in unverified situations; creature crushing rate remains null rather than fabricated. | F5, R | Existing glancing/crushing tests and Stage 2 ordering invariant. | **Audited — provisional**: higher-level glancing multiplier/caster mechanics and crushing chance remain unknown. |
| Main/off-hand swings and attack-power scaling | `ForeverAutoAttackFactory.cs`, `AutoAttackProcessor.cs`, `ForeverMeleeAttackProfiles.cs` | 14 AP = 1 weapon DPS, base 50% off-hand factor; swing-specific AP coefficient, off-hand hand-only bonuses. | F1, R | `AutoAttack_Foundation_01.cs`, `AutoAttack_NextSwing_01.cs`; Stage 2 math tests. | **Audited — provisional**: off-hand talent/positional interactions across each class need Stage 4. |
| Instant-attack weapon normalization | `ForeverNormalizedWeaponSpeeds.cs`, `ForeverAutoAttackFactory.cs`, specific Warrior/Hunter/Rogue spellbooks | Normalized AP speed distinct from real swing speed; constants dagger 1.7, 1H 2.4, 2H 3.3, ranged 2.8. | R, U | Stage 2 normalized-speed constants; existing class tests. | **Audited — provisional**: Forever-specific verification for every instant ability incomplete. |
| Armor formula, level and mitigation cap | `RulesetDamageMitigationResolver.cs`, class-specific `RogueSimRunner.cs`, `FeralDruidSimRunner.cs`, `MarksmanshipHunterSimRunner.cs`, `SurvivalHunterSimRunner.cs`, `BeastMasteryHunterSimRunner.cs` | Shared rational formula uses **attacker level**. Custom resolvers did not enforce client 75% cap. Flat armor floors and selected armor-reduction talents reviewed. | F1 (75% cap only), R, U | **Fixed:** `ForeverArmorMitigationMath.cs` and five class-specific resolvers; Stage 2 direct and Hunter/Bear high-armor tests. | **Audited — provisional**: exact `400+85×attackerLevel` curve is marked speculative by current client-data reference; heterogeneous class denominator tuning remains provisional. |
| Flat armor reduction and penetration | `ForeverSupportEffectCatalog.cs`, `ForeverWarriorSimRunner.cs`, `RogueSimRunner.cs`, `FeralDruidSimRunner.cs` | Existing flat reductions and percent penetration paths have nonnegative floors, but some class modules apply custom order/multipliers. | R, U | No global rewrite; class tests preserved. | **Audited — provisional**: full stacking, independent flat/percent precedence and ranking remain unverified. |
| Magical resistance and partial resist | `RulesetDamageMitigationResolver.cs`, `ForeverSpellCombatRulesetFactory.cs`, per-class spell resolvers | Shared `ResistanceAverage` provides a mean mitigation fraction, not a validated discrete partial-resist outcome table. | R | Existing combat resolution tests only. | **Audited — defects remaining**: resist roll distribution and real per-school resistance behavior not yet established; do not invent probabilities. |
| Hit, crit probability boundary and outcome exclusivity | `RulesetCombatRollResolver.cs`, `OrderedCombatRollTable.cs`, `ForeverSpellCriticalDeliveryAdjustmentProvider.cs` | Chances clamped, single roll table collision rules and direct vs periodic crit stats; compared with source. | F1, R | Stage 2 ordered-table tests; existing `Combat_Resolution_01/02.cs`. | **Audited — provisional**: Forever per-action modifiers and resist/miss policies need Stage 3/4. |
| Incoming Rage (Warrior) | `ForeverWarriorDamageTakenRageFactory.cs`, `DamageTakenResourceGenerationProcessor.cs`, `ForeverWarriorSimRunner.cs` | Expected creature health, assumed armor, absorb exclusion, block handling; fixed event classification in Stage 1. | F2, F3, R | `Warrior_Talents_Stances_01.cs`, `CP166_SharedDamageMitigationEvents.cs`. | **Audited — provisional**: exact level-dependent health and armor calibration unpublished. |
| Incoming Rage (Bear) | `FeralDruidSimRunner.cs`, new `ForeverDruidDamageTakenRageFactory.cs` | **Defect:** flat 5 Rage per damaging boss hit, independent of incoming damage size; replaced with normalized shared damage-taken processor gated by Bear form. | F2, R | **Fixed:** `CP166_Stage2_DruidIncomingRage.cs`. | **Audited — provisional**: request defaults 10,000 reference health and 35% assumed armor are explicitly approximations, NOT client-verified level-60 constants. Natural Reaction bonus remains separate. |
| Outgoing Rage | `ForeverWarriorAutoAttackFactory.cs`, `AutoAttackProcessor.cs`, Bear-specific `FeralDruidSimRunner.cs` | 1H speed×3.46; 2H speed×4.5; OH speed×1.73, with crit bonus/hand policy separated; Bear outgoing Rage separately provisional. | F3, F4, R | `Warrior_Talents_Stances_01.cs`; Stage 2 outgoing Rage base invariant. | **Audited — provisional**: rank/critical bonus and level-60 carryover conflicting or absent in logs; no coefficients changed. |
| Multi-component cost atomicity | `ResourceAvailabilityCalculator.cs`, `AbilityExecutor.cs`, `AutoAttackProcessor.cs`, `AbilityResourceCost.cs` | **Defect:** same resource used in two costs was validated individually, prematurely advertised as available and could be only partially spent. Next-swing replacement path already rejected duplicates. | R | **Fixed:** aggregate same-key costs in readiness, cast start and cast completion; `CP166_Stage2_ResourceMath.cs`. | **Audited — provisional** until tests execute. |
| Mana/Energy/Focus regeneration, caps and timing | `ResourceState.cs`, `ResourceAvailabilityCalculator.cs`, `FiveSecondManaRegenerationProcessor.cs`, Hunter/Warlock companion runners, Rogue/Feral runtime | Generic passive accrual linear and clamped; 5-second Mana rules and class timing remain separate; arbitrary event boundaries and caps checked. | R | Stage 2 fractional/cap tests, `MageWarlock_ResourceManagement_CP132_1.cs`, `CP163_PetFamilyCorrections.cs`. | **Audited — provisional**: level-60 natural rates and casting interruption rules require source/Stage 3 audit. |
| Refunds / additional resource consumption | `AutoAttackProcessor.cs`, `AbilityExecutor.cs`, `ResourceManipulator.cs` | Next-swing refund percentage rules and Execute-style additional consumption reviewed, but class-specific proc refunds deferred. | R | Existing `AutoAttack_NextSwing_01.cs`; new atomic cost tests. | **Audited — provisional**: proc-specific rounding and eligibility require class audits. |
| Haste and swing/cast timing interaction | `ForeverAutoAttackTimingProvider.cs`, `ForeverAbilityTimingProvider.cs`, `AutoAttackProcessor.cs` | Haste uses separate timing providers; real swing interval distinct from normalized AP weapon speed. | F1, R | `Forever_Spells_Timing_01.cs`, Hunter timing tests. | **Audited — provisional**: channels, pushback, GCD and same-timestamp proc ordering to Stage 3. |
| Encounter/result numeric accounting | `SimulationContext.cs`, `SimulationEngine.cs`, `ActorCombatSummary.cs`, `DamageTakenResourceGenerationProcessor.cs` | Rage and resource emission, mitigation raw/final reconciliation; no unproven global changes. | R | `CP166_SharedDamageMitigationEvents.cs`, Stage 2 math/cost tests. | **Audited — provisional** pending complete suite and exported run reconciliation. |

## Stage 3 — shared spells, heals, procs (not started)

| System | Source inspection and regression tests | Verification/discrepancies | Status |
|---|---|---|---|
| Direct spells, hit/crit, coefficients and resistance distributions | Pending | Pending | **Not audited** |
| Periodic damage/healing, application miss and snapshot dynamics | Pending | Pending | **Not audited** |
| Cast/GCD/channel/haste/pushback/interruption | Pending | Pending | **Not audited** |
| Procs, cooldowns and owner/target auras | Pending | Pending | **Not audited** |
| Healing/overhealing, absorbs and healing threat | Pending | Pending | **Not audited** |

## Stage 4 — class specializations (not started)

| Class / specializations | Meaningful complete per-spec executable audit | Status |
|---|---|---|
| Warrior — Arms, Fury, Protection | Pending | **Not audited** |
| Hunter — Marksmanship, Survival, Beast Mastery | Pending | **Not audited** |
| Rogue — Assassination, Combat, Subtlety | Pending | **Not audited** |
| Mage — Arcane, Fire, Frost | Pending | **Not audited** |
| Warlock — Affliction, Demonology, Destruction | Pending | **Not audited** |
| Priest — Discipline, Holy, Shadow | Pending | **Not audited** |
| Shaman — Enhancement, Elemental, Restoration | Pending | **Not audited** |
| Druid — Balance, Feral Cat, Feral Bear, Restoration | Pending | **Not audited** |

## Stage 5 — companions and already implemented modifiers (not started)

| System | Status | Required later |
|---|---|---|
| 19 Hunter pet families and Warlock demon archetypes | **Not audited** | Inheritance, family abilities, uptime, death, threat, replacement, sacrifice, target eligibility |
| Static raid buffs / boss debuffs / consumables | **Not audited** | Selected effect-component stacking, exclusivity and duration |
| Existing enchants / equipment / on-use / racials / engineering effects | **Not audited** | Active effect eligibility, proc frequency, duration, threat and source version |

## Validation / release gate

1. Pull merged Stage 2 `main`, run `dotnet build`, then full `dotnet test`.
2. Validate resource and high-armor tests, Bear incoming-Rage events, plus representative Warrior/Hunter/Rogue/Feral exports.
3. Keep all provisional values marked as such; **do not advance to Stage 3** until Stage 2 tests and user review pass.
4. Stage 2 is **not an assertion of complete numerical verification**: the unknown server Rage curve, weapon-skill/glancing behavior, resistance distribution, and some class-dependent physical formulas remain open evidence dependencies.
