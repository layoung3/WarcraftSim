# CP166 — Mechanical and Formula Audit Coverage Matrix

**Scope:** current WoW Forever level-60 simulator. **Updated:** 2026-10-27.
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
| F6 | [Blizzard Forever beta notes, October 26](https://www.bluetracker.gg/wow/topic/us-en/2360696-wow-forever-beta-development-notes-updated-october-26/) | Official: Divine Aegis requires **non-periodic critical healing**; Inner Focus does not enhance periodic crits; channel effects are treated distinctly from aura periodics in named mechanics; Devouring Plague may crit; Impact cannot proc from periodic Flamestrike. |
| F7 | [Forever Prayer of Mending tooltip](https://www.wowhead.com/forever/spell=401859/prayer-of-mending) | Client-derived: triggers on damage or incoming non-periodic healing and jumps with a five-charge limit; validates the existing generic trigger gate but not smart-jump proximity simulation. |
| F8 | [Forever spell/heal coefficient overview](https://www.warcrafttavern.com/forever/guides/stats/) | Secondary contextual evidence for coefficient application and downranking; does NOT resolve server snapshot/partial resist tables or per-rank values. |
| F9 | [Official WoW Forever beta Priest updates, October 15](https://www.bluetracker.gg/wow/topic/us-en/2360696-wow-forever-beta-development-notes-updated-october-15/) | Official: Divine Aegis clarification; stronger Power Word: Shield replacement rules; Priest Renew multi-caster bug fixed in beta. |
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

## Stage 3 — shared spells, healing, periodic effects, procs and auras

**Stage 3 baseline:** `3a2a497cc3ce9f0191afa008ae3d4a8a8423a426` (PR #12; Stage 2 locally validated by user).
**Validation:** Source review, static patch/delimiter checks and added tests. The model environment has no .NET SDK, so the full suite must be run locally; **do not elevate to Audited — verified before that run**. Review covers the reusable *shared* execution paths and representative existing class integrations, not all per-talent mechanics (Stage 4).

| System or subsystem | Executable source inspected | Behavior reviewed / discrepancy found | Independent source | Corrected implementation and tests | Status / remaining unknowns |
|---|---|---|---|---|---|
| Direct spell cast → hit → crit → coefficient → mitigation → absorb → attribution | `Core/Simulation/Engine/AbilityExecutor.cs`, `RulesetCombatRollResolver.cs`, `RulesetDamageMitigationResolver.cs`, `SimulationContext.cs`; `Data/Forever/Combat/ForeverSpellCombatRulesetFactory.cs`, `ForeverSpellCriticalDeliveryAdjustmentProvider.cs` | Direct spells resolve a class/Forever hit and crit table then effect damage scaling, mitigation and absorption; applied damage emits raw/mitigated/absorbed/actual event. Spell coefficients and crit modifiers already have per-effect scopes. | F6, F8, R | No unsupported numerical rewrites. `CP166_Stage3_CrossClassIntegration.cs`, existing `Forever_Spells_Timing_01.cs`, `Combat_Resolution_01/02.cs`. | **Audited — provisional**: per-school partial-resist distributions, exact server coefficient/downranking curves and full talent interactions need logs/client evidence and Stage 4. |
| Standalone DoT application, missed applications and aura-owned periodic ticks | `AbilityExecutor.cs`, `PeriodicEffectScheduler.cs`, `AuraManager.cs`, `AuraInstance.cs`, Warlock/Mage/Shaman/Hunter spellbook examples | **Defect:** standalone DoTs with CanMiss applied unconditionally and individual ticks then rerolled miss. Dependent DoTs already had prerequisite gates (e.g. Serpent Sting). | F6 confirms periodic crits, R; once-per-application miss is an engine rule compatible with existing dependent-hit spellbooks; no verified separate Forever tick-resist distribution. | **Fixed:** one initial application roll (without extra crit) for standalone damage-over-time; tick rolls independently crit without repeated miss/dodge/parry; retains dependent impact eligibility. `CP166_Stage3_PeriodicAndCasting.cs`, `CP166_Stage3_TargetDependencies.cs`, `CP166_Stage3_CrossClassIntegration.cs`. | **Audited — provisional**: per-school periodic partial resists and any special spell-specific behavior need empirical evidence. |
| Direct-impact spell and dependent DoT composition | `Data/Forever/Warlock/ForeverWarlockDpsSpellbook.cs`, `Data/Forever/Mage/ForeverMageDpsSpellbook.cs`, `Data/Forever/Shaman/ForeverElementalShamanDps.cs`, `Core/Simulation/Engine/AbilityExecutor.cs` | **Defect:** Immolate, Frostfire Bolt and Flame Shock had distinct direct and periodic effects, but no landed-impact dependency; DoT could be applied after the associated direct component missed. | R (independent per-target hit outcomes); client/source evidence for precise per-spell resist policies remains incomplete | **Fixed:** each existing DoT now requires its own landed direct component; `CP166_Stage3_SpellbookDependencyContracts.cs`, `CP166_Stage3_TargetDependencies.cs` and class run tests. | **Audited — provisional**: dynamic snapshot/refresh and class-specific exceptions remain for Stage 4. |
| Multi-target effect dependencies | `AbilityExecutor.cs`, `AbilityExecutionState.cs`, `SimulationContext.cs`, `EffectTargetResolver.cs` | **Defect:** effect results were stored only by effect key, so last target's direct hit could incorrectly determine another target's DoT eligibility. | R | **Fixed:** per-execution/per-effect/per-target resolution records, backward-compatible effect-wide fallback only when no target-specific result exists. `CP166_Stage3_TargetDependencies.cs`. | **Audited — provisional** until tests execute; later per-class multi-target proc eligibility still needs Stage 4. |
| HoTs, effective healing, critical eligibility and dynamic healing modifiers | `AbilityExecutor.cs`, `HealingReceivedModifiers.cs`, `AuraManager.cs`, Priest/Resto Druid/Resto Shaman executors | HoT application and critical tick rolls reviewed. **Defects:** active HoTs could reroll misses; healing received multiplier rejected bonuses greater than 1 even though engine supports debuffs/beneficial effects. | F6 (periodic critical healing), R | **Fixed:** HoT ticks cannot reroll miss; beneficial healing-received multipliers above 1 supported; negative remains rejected. `CP166_Stage3_PeriodicAndCasting.cs`, `CP166_Stage3_HealingAndProcs.cs`, `CP166_Stage3_CrossClassIntegration.cs`. | **Audited — provisional**: per-spell snapshot versus dynamic scaling and haste tick policy unverified. |
| DoT/HoT refresh, source ownership, expiration and stacking | `AuraManager.cs`, `AuraInstance.cs`, `PeriodicEffectScheduler.cs` | Same-key auras from different casters coexist; same-source refresh changes runtime ID so old ticks and expiration events are stale; Stack honors MaxStacks; half-open aura expiration unless explicit boundary tick. | F9 (Renew ownership issue), R | Existing `Ability_Lifecycle_02.cs`; new `CP166_Stage3_AurasAbsorbsThreat.cs`. | **Audited — provisional**: generic mutually-exclusive buff component policies and dynamic stats beyond healing-received modifiers remain only partially represented; existing modifier audit is Stage 5. |
| Hard casts, channels, pushback, GCD and cancellation | `AbilityExecutor.cs`, `AbilityExecutionState.cs`, `SimulationContext.cs`, `BaseAbilityTimingProvider.cs`, `CastDamagePushbackProcessor.cs`, `ForeverAbilityTimingProvider.cs`, `SimulationEngine.cs` | Completion queue normally reschedules pushed casts; channels have separate tick events; effects are credited after cast completion. **Defect:** duplicate same-execution cast completion could spend resource/reapply effect and inflate cast summary. | F6 (Penance and channel distinctions), R | **Fixed:** completion execution claimed once; duplicate completions discarded from queue before summary. `CP166_Stage3_PeriodicAndCasting.cs` plus prior `Ability_Lifecycle_01/02.cs`, `CP154_CastPushback.cs`, `CP161_SharedLatencyAndSurvivalDefaults.cs`. | **Audited — provisional**: precise Forever channel interrupt/haste behavior and latency on multiple simultaneous actions still have build-specific unknowns. |
| Shared direct-critical rolling DoT proc | `CriticalStrikeRollingDamageProcessor.cs`, `CriticalStrikeRollingDamageDefinition.cs`, `PeriodicEffectScheduler.cs`, `AbilityExecutor.cs` | Only direct critical damage with eligible resolution and hand triggers; rolled damage pool carries only remaining pre-mitigation damage, avoiding feedback from periodic crits. | R | Existing Warrior `Forever_Abilities_01.cs`, `Warrior_Talents_Stances_01.cs`. | **Audited — provisional**: proc rate/eligibility of each talent and interaction with weapon-specific bonus mechanics is Stage 4. |
| Prayer of Mending healing-received integration | `Simulation/Healing/PrayerOfMendingProcessor.cs`, `Simulation/Engine/HealingReceivedModifiers.cs`, `AuraManager.cs` | **Defect:** triggered PoM heal bypassed live healing-received multipliers (e.g. healing-reduction auras), unlike ordinary direct and periodic heals. | F7 (proc trigger), R (shared modifier semantics) | **Fixed:** PoM uses `HealingReceivedModifiers.Apply` before `Heal`, and events now report modified raw/effective/overheal amounts; `CP166_Stage3_HealingAndProcs.cs`. | **Audited — provisional**: jump targeting/proximity and exact absorb/proc coefficient policies still need client evidence. |
| Divine Aegis and healing-trigger proc boundaries | `Simulation/Healing/DivineAegisProcessor.cs`, `PrayerOfMendingProcessor.cs`, `AlternatingHealManaRefundProcessor.cs` | **Defect:** Divine Aegis triggered from periodic critical heals despite explicit Forever non-periodic condition. Prayer of Mending already triggers on damage or non-periodic healing; recursion excluded for its own heal events. | F6, F7, F9, R | **Fixed:** Divine Aegis excludes `CombatEffectDeliveryType.Periodic` while allowing direct and channel ticks. `CP166_Stage3_HealingAndProcs.cs` and existing `Healing_PriestMechanics_CP126.cs`. | **Audited — provisional**: Divine Aegis shield cap/overheal semantics and numerical talent ranks should be independently verified in Stage 4; Prayer of Mending proximity remains approximated. |
| General proc dispatch, chains and internal cooldowns | `SimulationEngine.cs`, `SimulationContext.cs`, `CriticalStrikeRollingDamageProcessor.cs`, `PrayerOfMendingProcessor.cs`, `ReactiveAbilityOpportunityProcessor.cs`; representative class proc processors | Event queue sequence and shared processor dispatch reviewed. Direct versus periodic delivery metadata is available for suppression; event count limit protects against runaway cycles; class-specific ICD and multi-target proc policies live in specialization processors. | F6, R | Existing `Mechanics_Foundation_01.cs`, `CP144_ClassMechanics.cs`; new Divine Aegis and dependency tests. | **Audited — provisional**: there is no independently verified universal proc-eligibility/ICD coefficient model for all class abilities; Stage 4 must audit opt-in proc implementations individually. |
| Absorb application/consumption, effective heal, overheal and healing threat | `AbsorbManager.cs`, `HealingReceivedModifiers.cs`, `HealingThreatResolver.cs`, `SimulationContext.cs`, `HealerPriorityProcessor.cs` | Absorbs consume before health damage and attribute to original caster; effective heal separates overheal; generic healing threat splits a configurable fraction across living enemies, using actual effective heal. | R, F8 | `CP166_Stage3_AurasAbsorbsThreat.cs`, `CP166_Stage3_HealingAndProcs.cs`, `CP166_Stage3_CrossClassIntegration.cs`; existing `Healing_HolyPriest_CP125.cs`. | **Audited — provisional**: threat coefficient, absorb stacking priority, smart targeting in moving encounters and precise overheal-related shield policies remain unverified. |
| Cross-class executable impact | `MageSimRunner.cs`, `WarlockSimRunner.cs`, `ShadowPriestSimRunner.cs`, `ElementalShamanSimRunner.cs`, `BalanceDruidSimRunner.cs`, `HolyPriestPreviewRunner.cs`, `RestorationShamanSimRunner.cs`, `RestorationDruidSimRunner.cs` | Representative spellbooks follow generic event pipelines with separate custom roll adapters; no specialization rotations or numeric coefficients changed here. | R | `CP166_Stage3_CrossClassIntegration.cs`; existing class integration suites. | **Audited — provisional**: smoke coverage is NOT completion of individual specializations; full per-spec audit remains Stage 4. |

## Stage 4A — Warrior and Rogue (incremental correction checkpoint; not complete)

**Source baseline:** `08016902a730279918829f52a237b4a0ff8d9da9` (Stage 3 repair merged). This entry records a partial Stage 4A implementation, **not** six verified specializations. Owner's Stage 3 .NET build and test suite passed; Stage 4A .NET tests have not yet been executed in this connector environment.

**Confirmed, source-backed correction:** Rogue `RogueCombatProcessor` gave Garrote its combo point on cast completion even when its missable periodic effect failed application. Move award to successful `AuraApplied` only. At the Adrenaline Rush aura boundary, settle accrued Energy regeneration before switching regeneration rate, avoiding old time being credited at the new rate. Warrior `WarriorRageTalentProcessor` and `WarriorWeaponTalentProcessor` resource-change events now include resource name and post-gain total, following Stage 1/2 reporting conventions. The Unbridled Wrath 2-Rage two-handed rule is **obsolete**: official October 5 beta notes remove it, confirmed by current build comparison; all weapons still award one Rage on an eligible proc (current proc mask remains provisional).

| Specialization | Executable source reviewed in this checkpoint | Corrections / evidence | Remaining audit / verification | Status |
|---|---|---|---|---|
| Warrior Arms | `ForeverWarriorSimRunner.cs`, `ForeverWarriorTalentCatalog.cs`, `WarriorRageTalentProcessor.cs`, `WarriorSweepingStrikesProcessor.cs`, `WarriorWeaponTalentProcessor.cs` | Corrected shared Rage event metadata. Reviewed Rend/Deep Wounds and Sweeping Strikes connections; no comprehensive revalidation of their coefficients. | Full ability/talent inventory, stance interactions, bleed snapshots, weapon procs, execute and independent numeric confirmation. | **Audited — defects remaining** |
| Warrior Fury | `ForeverWarriorSimRunner.cs`, `WarriorRageTalentProcessor.cs`, `WarriorWeaponTalentProcessor.cs`, `ForeverWarriorTalentCatalog.cs` | Corrected resource-event metadata; explicitly preserved one-Rage Unbridled Wrath after Oct 5 rules revision. `CP166_Stage4A_WarriorRogueRegression.cs` includes a deterministic proc check. | Detailed Bloodthirst/Flurry/replacement swing/extra-attack interactions and broader outcome regressions. | **Audited — defects remaining** |
| Warrior Protection | `WarriorProtectionAbilityFactory.cs`, `WarriorProtectionProcessor.cs`, `WarriorIncomingTalentProcessor.cs`, `ForeverWarriorSimRunner.cs` | Inspected Revenge, shield cooldown, Last Stand, mitigation and projected-health wiring. Existing fixed-duration tank model deliberately unchanged. | Full talent/rank and threat verification, shield timing, boss hit outcomes, tank window calculations and more regression tests. | **Audited — defects remaining** |
| Rogue Assassination | `ForeverRogueTalentBuild.cs`, `ForeverRogueSpellbook.cs`, `RogueCombatProcessor.cs`, `RoguePoisonProcessor.cs` | Corrected Garrote points on a miss; Rupture retains separate combo-point damage snapshots by target. New outcome tests check failed Garrote application and target-swapped Rupture scaling. | Poison hit/proc tables, multi-hit Mutilate, Seal Fate, finisher miss rules, charges, bleed refresh/snapshot policy, remaining talents. | **Audited — defects remaining** |
| Rogue Combat | `RogueCombatProcessor.cs`, `RogueSimRunner.cs`, `RogueThreatAndDefenseProcessor.cs`, `ForeverRogueSpellbook.cs` | Corrected shared energy regeneration boundary accounting; Hack and Slash now copies actual AP-scaled weapon-swing damage and off-hand modifiers rather than unscaled weapon base damage. Added extra-attack damage regression. Existing Cleave/Adrenaline Rush pathways inspected. | Energy timing test, extra-attack proc masks and recursion, Blade Flurry threat/hit table, weapon talent effects, cooldown resets. | **Audited — defects remaining** |
| Rogue Subtlety | `ForeverRogueSpellbook.cs`, `RogueCombatProcessor.cs`, `ForeverRogueTalentBuild.cs` | Reviewed stealth/opener/Premeditation and positional gates; successful Garrote application now owns its combo point. | Openers/stealth failure behavior, Cold Blood and Premeditation targeting, Preparation reset contracts, tier prerequisites. | **Audited — defects remaining** |

**Regression file:** `WarcraftSim.Tests/Consolidated/CP166_Stage4A_WarriorRogueRegression.cs`. Source-level review only; full Stage 4A .NET 10 build, unit tests and fresh Simulation Lab exports are outstanding. The numeric validity of most Warrior/Rogue modifiers remains **provisional**; do not import Classic-era or early Forever beta values without contemporaneous independent evidence.

**Open Stage 4A gate:** enumerate every ability/talent into coverage evidence; complete comprehensive mechanics tests for all six specs and address all independent defects. Stage 4B must remain blocked.

### Stage 4A Warrior talent-definition-to-runtime index (52 catalog entries)

This index is generated from the **actual Warrior catalog** on the Stage 4A branch and checked for literal talent IDs in the named runtime files. A source-key match shows an implementation reference, **not proof of gameplay correctness**. Indirect factories with no literal key may still connect talents. The final status column is therefore conservative. The three stance trees share one `ForeverWarriorSimRunner`; formulas and source imports remain subject to the independent Forever evidence register.

| Tree | Talent (rank cap) | Catalog declaration | Direct executable references | Verification / limitation |
|---|---|---|---|---|
| arms | `improved-heroic-strike` (3) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| arms | `deflection` (5) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| arms | `improved-rend` (3) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| arms | `improved-charge` (2) | not-modeled | No direct literal-key reference in sampled runtime files | Not executable in solo model |
| arms | `improved-tactical-mastery` (5) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| arms | `improved-overpower` (2) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| arms | `anger-management` (1) | partial | `ForeverWarriorSimRunner.cs`, `WarriorRageTalentProcessor.cs` | Mechanic / numeric audit remains open |
| arms | `deep-wounds` (3) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| arms | `spearing-strike` (1) | modeled | `ForeverWarriorSimRunner.cs`, `WarriorRotationRules.cs` | Connected or indirect; outcome and client verification still required |
| arms | `two-handed-weapon-specialization` (3) | partial | `ForeverWarriorSimRunner.cs` | Mechanic / numeric audit remains open |
| arms | `impale` (2) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| arms | `bloodthrill` (5) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| arms | `sweeping-strikes` (1) | partial | `ForeverWarriorSimRunner.cs`, `WarriorSweepingStrikesProcessor.cs`, `WarriorRotationRules.cs` | Mechanic / numeric audit remains open |
| arms | `weaponmaster` (5) | partial | `ForeverWarriorSimRunner.cs`, `WarriorWeaponTalentProcessor.cs` | Mechanic / numeric audit remains open |
| arms | `improved-slam` (2) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| arms | `improved-hamstring` (3) | not-modeled | No direct literal-key reference in sampled runtime files | Not executable in solo model |
| arms | `mortal-strike` (1) | modeled | `ForeverWarriorSimRunner.cs`, `WarriorRotationRules.cs` | Connected or indirect; outcome and client verification still required |
| fury | `booming-voice` (5) | partial | `WarriorRuntimeActionProcessor.cs` | Mechanic / numeric audit remains open |
| fury | `cruelty` (5) | modeled | No direct literal-key reference in sampled runtime files | Connected or indirect; outcome and client verification still required |
| fury | `lingering-rage` (5) | not-modeled | No direct literal-key reference in sampled runtime files | Not executable in solo model |
| fury | `unbridled-wrath` (5) | partial | `ForeverWarriorSimRunner.cs`, `WarriorRageTalentProcessor.cs` | Mechanic / numeric audit remains open |
| fury | `furious-precision` (3) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| fury | `piercing-howl` (1) | not-modeled | No direct literal-key reference in sampled runtime files | Not executable in solo model |
| fury | `blood-craze` (3) | partial | `WarriorIncomingTalentProcessor.cs` | Mechanic / numeric audit remains open |
| fury | `dual-wield-specialization` (5) | partial | `ForeverWarriorSimRunner.cs` | Mechanic / numeric audit remains open |
| fury | `raging-blows` (1) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| fury | `enrage` (5) | partial | `WarriorIncomingTalentProcessor.cs`, `WarriorWeaponTalentProcessor.cs` | Mechanic / numeric audit remains open |
| fury | `improved-execute` (2) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| fury | `improved-berserker-rage` (2) | not-modeled | No direct literal-key reference in sampled runtime files | Not executable in solo model |
| fury | `death-wish` (1) | partial | `WarriorRuntimeActionProcessor.cs`, `WarriorWeaponTalentProcessor.cs` | Mechanic / numeric audit remains open |
| fury | `improved-intercept` (2) | not-modeled | No direct literal-key reference in sampled runtime files | Not executable in solo model |
| fury | `flurry` (5) | modeled | `WarriorWeaponTalentProcessor.cs` | Connected or indirect; outcome and client verification still required |
| fury | `gore-drinker` (2) | partial | `WarriorWeaponTalentProcessor.cs` | Mechanic / numeric audit remains open |
| fury | `bloodthirst` (1) | modeled | `ForeverWarriorSimRunner.cs`, `WarriorWeaponTalentProcessor.cs`, `WarriorRotationRules.cs` | Connected or indirect; outcome and client verification still required |
| protection | `improved-bloodrage` (2) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| protection | `shield-specialization` (5) | modeled | `ForeverWarriorSimRunner.cs`, `WarriorIncomingTalentProcessor.cs` | Connected or indirect; outcome and client verification still required |
| protection | `iron-will` (5) | not-modeled | No direct literal-key reference in sampled runtime files | Not executable in solo model |
| protection | `anticipation` (5) | modeled | `ForeverWarriorSimRunner.cs` | Connected or indirect; outcome and client verification still required |
| protection | `improved-revenge` (3) | modeled | `WarriorProtectionAbilityFactory.cs` | Connected or indirect; outcome and client verification still required |
| protection | `improved-thunder-clap` (3) | partial | `WarriorRuntimeActionProcessor.cs` | Mechanic / numeric audit remains open |
| protection | `last-stand` (1) | modeled | `WarriorRuntimeActionProcessor.cs`, `WarriorProtectionAbilityFactory.cs`, `WarriorProtectionProcessor.cs` | Connected or indirect; outcome and client verification still required |
| protection | `master-of-defense` (2) | modeled | `WarriorIncomingTalentProcessor.cs` | Connected or indirect; outcome and client verification still required |
| protection | `improved-disarm` (3) | not-modeled | No direct literal-key reference in sampled runtime files | Not executable in solo model |
| protection | `defiance` (3) | modeled | `WarriorDamageThreatProcessor.cs` | Connected or indirect; outcome and client verification still required |
| protection | `improved-sunder-armor` (3) | modeled | `WarriorRuntimeActionProcessor.cs` | Connected or indirect; outcome and client verification still required |
| protection | `vanguard` (1) | not-modeled | No direct literal-key reference in sampled runtime files | Not executable in solo model |
| protection | `improved-shield-bash` (2) | not-modeled | No direct literal-key reference in sampled runtime files | Not executable in solo model |
| protection | `improved-shield-wall` (2) | modeled | `WarriorRuntimeActionProcessor.cs`, `WarriorProtectionAbilityFactory.cs` | Connected or indirect; outcome and client verification still required |
| protection | `concussion-blow` (1) | partial | `WarriorProtectionAbilityFactory.cs`, `WarriorRotationRules.cs` | Mechanic / numeric audit remains open |
| protection | `focused-rage` (3) | partial | `ForeverWarriorSimRunner.cs`, `WarriorProtectionAbilityFactory.cs` | Mechanic / numeric audit remains open |
| protection | `bastion` (5) | partial | `ForeverWarriorSimRunner.cs` | Mechanic / numeric audit remains open |
| protection | `shield-slam` (1) | partial | `ForeverWarriorSimRunner.cs`, `WarriorProtectionAbilityFactory.cs`, `WarriorRotationRules.cs` | Mechanic / numeric audit remains open |

**Warrior ability and cross-system inventory:** `ForeverMortalStrikeFactory`, `ForeverOverpowerFactory`, `ForeverExecuteFactory`, `ForeverRendFactory`, `ForeverDeepWoundsFactory`, `ForeverSlamFactory`, `ForeverSpearingStrikeFactory`, `ForeverBloodthirstFactory`, `ForeverWhirlwindFactory`, `ForeverHeroicStrikeFactory`, `ForeverCleaveFactory`, `ForeverWarriorStanceFactory`, `ForeverWarriorAutoAttackFactory`, `WarriorProtectionAbilityFactory`, `WarriorRuntimeActionProcessor`, `WarriorDamageThreatProcessor`, `WarriorIncomingTalentProcessor`, `WarriorWeaponTalentProcessor`, `WarriorSweepingStrikesProcessor`, `WarriorStanceDanceProcessor`, and `WarriorTankSpikeProcessor`. These represent actual factory and event pathways, **not all independently proven combat effects**. Encounter AI-dependent crowd control and server-only armor/rage curves remain explicit dependencies.

### Stage 4A Rogue talent-definition-to-runtime index (42 supported keys)

These 42 keys are **the explicitly supported Rogue talent subset**, not an assertion that Forever's full tree is implemented. The code references were traced against `ForeverRogueSpellbook`, `RogueSimRunner`, `RogueCombatProcessor`, `RoguePoisonProcessor` and `RogueThreatAndDefenseProcessor`. Registration, name-matching and even simulation execution are not independent validation of a talent's effect.

| Tree | Talent (rank cap) | Executable reference(s) | Verification / limitation |
|---|---|---|---|
| assassination | `malice` (5) | `RogueSimRunner.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `ruthlessness` (3) | `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `improved-slice-and-dice` (3) | `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `vile-poisons` (5) | `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `improved-poisons` (5) | `RoguePoisonProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `vigor` (2) | `RogueSimRunner.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `seal-fate` (5) | `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `mutilate` (1) | `ForeverRogueSpellbook.cs`, `RogueSimRunner.cs`, `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `venom` (1) | `ForeverRogueSpellbook.cs`, `RogueSimRunner.cs`, `RogueCombatProcessor.cs`, `RoguePoisonProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `improved-eviscerate` (3) | `ForeverRogueSpellbook.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `precision` (5) | `RogueSimRunner.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `dual-wield-specialization` (5) | `ForeverRogueSpellbook.cs`, `RogueSimRunner.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `aggression` (3) | `ForeverRogueSpellbook.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `blade-flurry` (1) | `ForeverRogueSpellbook.cs`, `RogueSimRunner.cs`, `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `adrenaline-rush` (1) | `ForeverRogueSpellbook.cs`, `RogueSimRunner.cs`, `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| subtlety | `opportunity` (2) | `ForeverRogueSpellbook.cs` | Connected reference; independent behavior and coefficients require verification |
| subtlety | `hemorrhage` (1) | `ForeverRogueSpellbook.cs`, `RogueSimRunner.cs`, `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| subtlety | `thousand-cuts` (1) | `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `cold-blood` (1) | `ForeverRogueSpellbook.cs`, `RogueSimRunner.cs`, `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `flawless-execution` (1) | `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| subtlety | `ghostly-strike` (1) | `ForeverRogueSpellbook.cs`, `RogueSimRunner.cs`, `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| subtlety | `premeditation` (1) | `ForeverRogueSpellbook.cs`, `RogueSimRunner.cs`, `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| subtlety | `cutthroat` (3) | `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| subtlety | `improved-ambush` (3) | `ForeverRogueSpellbook.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `puncturing-wounds` (3) | `ForeverRogueSpellbook.cs`, `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `lethality` (5) | `RogueSimRunner.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `improved-garrote` (3) | `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `improved-kidney-shot` (3) | `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `improved-expose-armor` (2) | `ForeverRogueSpellbook.cs`, `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `quick-recovery` (2) | `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| assassination | `fleet-footed` (2) | `RogueThreatAndDefenseProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `deflection` (5) | `RogueThreatAndDefenseProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `riposte` (1) | `ForeverRogueSpellbook.cs`, `RogueSimRunner.cs`, `RogueCombatProcessor.cs`, `RogueThreatAndDefenseProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `hack-and-slash` (5) | `RogueSimRunner.cs`, `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `weapon-expertise` (2) | `RogueSimRunner.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `endurance` (2) | `ForeverRogueSpellbook.cs` | Connected reference; independent behavior and coefficients require verification |
| combat | `lightning-reflexes` (5) | `RogueThreatAndDefenseProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| subtlety | `elusiveness` (2) | `ForeverRogueSpellbook.cs` | Connected reference; independent behavior and coefficients require verification |
| subtlety | `master-of-subtlety` (3) | `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |
| subtlety | `dirty-deeds` (2) | `ForeverRogueSpellbook.cs`, `RogueSimRunner.cs` | Connected reference; independent behavior and coefficients require verification |
| subtlety | `dirty-tricks` (2) | `ForeverRogueSpellbook.cs` | Connected reference; independent behavior and coefficients require verification |
| subtlety | `preparation` (1) | `ForeverRogueSpellbook.cs`, `RogueSimRunner.cs`, `RogueCombatProcessor.cs` | Connected reference; independent behavior and coefficients require verification |

**Rogue ability and mechanic inventory:** `ForeverRogueSpellbook` defines Sinister Strike, Backstab, Eviscerate, Rupture, Ambush, Garrote, Cheap Shot, Vanish, Expose Armor, Kidney Shot, Riposte, Premeditation, Ghostly Strike, Cold Blood, Slice and Dice, Mutilate (both hands), Venom, Hemorrhage, Adrenaline Rush, Blade Flurry, plus Kick, Gouge, Blind, Feint, Evasion, Sprint, Distract, Preparation and Sap. `RoguePoisonProcessor` adds weapon-local Instant, Deadly, Wound, Crippling and Mind-numbing poison execution. `RogueCombatProcessor` owns Energy changes, combo points, finishers, stealth/opener state and Hack and Slash. `RogueThreatAndDefenseProcessor` owns Feint threat and incoming scripted avoidance. The subset omits other real Forever talents; these are **unimplemented**, not silently verified.

**Independent client confirmations introduced in this continuation:** Opportunity is 2 ranks of 5% each and affects Backstab, Garrote, Ambush and Mutilate (<https://theforeverera.com/en/talents/rogue/opportunity/> and official Blizzard Rogue deep dive); Vigor is 5/10 maximum Energy at ranks 1/2 (<https://theforeverera.com/en/talents/rogue/vigor/>); Improved Poisons provides 2% application and 10% charge preservation per rank (<https://theforeverera.com/en/talents/rogue/improved-poisons/>); Hack and Slash mace armor ignore is 3% per rank (<https://theforeverera.com/en/talents/rogue/hack-and-slash/>). These values replace outdated provisional defaults and have new source-oriented regressions. Other configurable tuning values remain provisional.

**Connected stage 3 consistency corrections:** Rogue periodic melee damage must not reroll target dodge/parry after application; Deadly Poison ticks now resolve through the shared damage/absorb/event engine rather than direct `TakeDamage` calls. Rogue finisher effects must spend combo points when hit/aura outcome lands, not before damage multipliers resolve. Detailed live encounter and cross-class regression validation is still outstanding.

### Stage 4A executable correction register (continuation)

| Defect / correction | Affected spec and source | Evidence and regression | Remaining limitation |
|---|---|---|---|
| Eviscerate consumed combo points at cast completion before its own damage multiplier evaluated; missed finishers also spent points | Rogue Assassination/Combat/Subtlety — `RogueCombatProcessor.cs` | Consumption moved to landed hit event; aura finishers (`rupture`, `slice-and-dice`, `venom`, `kidney-shot`, `expose-armor`) defer spending until applied; pending cast ID and original enemy target retained for self auras. `Eviscerate_OnlyConsumesComboPointsOnLandedDamage`, `Rupture_FailedApplicationCannotConsumeComboPoints`. | Exact Forever refunded Energy/CP rules on individual finisher miss outcomes need separate client confirmation; broader envenom-equivalent behavior remains provisional. |
| Established bleed tick rerolled melee dodge/parry despite Stage 3's already-landed application | Rogue Assassination/Subtlety — `RogueSimRunner.RogueRollResolver` | Dodge/parry eligibility now excludes periodic delivery; `Garrote_AppliedBleedTicksDoNotRerollTargetDodge`. | The original DoT application still rolls hit; whether every bleed can critically tick is spell-specific Forever data dependent. |
| Deadly Poison tick bypassed damage absorb and shared damage event handling | Rogue all three — `RoguePoisonProcessor.cs` | Tick now calls `AbilityExecutor.ExecuteBackgroundDirectDamage` with periodic delivery and no new application miss; threat and absorb attribution share standard engine semantics. | Deadly Poison spell resistance, spell crit eligibility and exact snapshot policy remain open. |
| Mutilate, Backstab, Ambush, Garrote Opportunity effect used obsolete Classic five-rank/4%-per-rank assumptions or was unwired | Rogue Assassination/Subtlety — `ForeverRogueTalentBuild.cs`, `ForeverRogueSpellbook.cs` | Forever build 70532 Opportunity has two ranks of 5% each and affects all four named abilities. Default Subtlety preset reduced to 2 ranks. `Opportunity_CurrentForeverRanksScaleEveryNamedAbility`, `Opportunity_RejectsObsoleteFivePointClassicRank`. | Other talent caps and row prerequisites still require exhaustive client-by-client validation. |
| Vigor, Improved Poisons charge preservation, Hack and Slash mace armor bypass used stale coefficients | Rogue Assassination/Combat — `RogueSimRunner.cs`, `ForeverRogueTuning.cs` | Forever client tooltips: Vigor +5/+10 Energy; Improved Poisons 10% preserved charges per rank; Hack and Slash mace 3% armor ignore per rank. `Vigor_MaxEnergyMatchesCurrentForeverTooltip`, `PoisonTalentsAndWeaponSpecializationUseCurrentClientRankValues`. | Mace crit/proc interaction and poison stack independence require more outcomes. |
| Sweeping Strikes copied echo bypassed secondary target's absorption shields and omitted mitigation reporting | Warrior Arms — `WarriorSweepingStrikesProcessor.cs` | Secondary echo now goes through `AbsorbManager.ApplyDamage`; `SweepingStrikes_EchoConsumesShieldBeforeSecondaryHealth`. | Secondary melee-roll/armor semantics are still provisional; no invented new hit table. |
| Shield Block could leave permanent +75% block stat after its second charge was consumed | Warrior Protection — `WarriorProtectionProcessor.cs` | Separate `_blockBonusActive` state ensures stat cleanup at zero charges; `Protection_ShieldBlockRemovesTemporaryStatAfterSecondBlockedSwing` verifies recast. | Exact recharge / charge mechanics versus live server remain to be checked. |
| Warrior/Rogue default executable and API serialization coverage | Arms/Fury/Protection/Assassination/Combat/Subtlety — `ForeverWarriorSimRunner`, `RogueSimRunner` | `Warrior_AllThreePresetsProduceExecutableAndSerializableRuns`, `Rogue_AllThreePresetsProduceExecutableAndSerializableRuns`. | These are smoke regressions, NOT evidence that every active ability/talent was audited. |

**Stage 4A validation gate:** Additional tests committed, but neither local .NET 10 build nor xUnit has been executed for the **Stage 4A branch**. No GitHub Actions runs were found for this branch head. The previously confirmed clean Stage 3 result does not validate these new changes. Current supported data sources include Forever beta build 70532 tooltips from The Forever Era and Blizzard's October Rogue deep dive. All server-only damage, proc mask, avoidance and bleed rules not independently confirmed remain provisional. A full, independently auditable scan of source paths and coverage statuses above **does not make the six specializations verified**; the remaining implemented mechanics and runtime API outcomes require further review. Stage 4B blocked.

### Stage 4A subsequent executable findings and unresolved gates

| Specialization/system | New source-backed correction | Outcome regression | Remaining independently implementable audit |
|---|---|---|---|
| Warrior Arms — Mortal Strike | `ForeverMortalStrikeFactory` now applies a real 0.50 healing-received aura to hit target instead of a marker-only aura; `AbilityEffectDefinition.HealingReceivedMultiplier` defaults to 1 and is transferred by `AbilityExecutor.ApplyExplicitAura`. | `Arms_MortalStrikeAppliesRealHealingReductionOnlyWhenHitLands` | Multi-source Mortal Strike/healing-reduction stacking and missed impact cross-target behavior need tests; no standalone universal healing-reduction stacking policy has been proven. |
| Warrior Protection — Taunt | `WarriorProtectionAbilityFactory` now creates baseline Defensive Stance Taunt independent of shield, with 8-second cooldown, 3-second forced targeting, and match-highest-threat operation; custom Protection priority whitelist includes Taunt. | `Protection_TauntMatchesHighestThreatWithoutShieldAndUsesCurrentCooldown` (competing tank is a registered actor) | Challenging Shout and encounter threat-swap behavior, boss AI selection and Taunt immunity are not fully implemented; exact conditions require encounter support. |
| Rogue Assassination — Seal Fate | A missed Seal Fate proc on first Mutilate critical no longer prevents second crit from its own proc chance; successful bonus remains limited to one per execution. | `Assassination_SealFateCanProcFromSecondMutilateCritAfterFirstFails` | Multi-target proc assignment, talent-specific critical eligibility, and charge limitations still need wider coverage. |
| Rogue all — physical avoidance | Melee dodge and frontal parry are now reported distinctly rather than as a generic miss, while periodic ticks continue to skip repeated avoidance after application. | `Rogue_PhysicalAvoidanceReportsDodgeAndParrySeparately` | Shared attack-table order, glancing and physical special/white split remain partially modeled; do not claim server-perfect avoidance curves. |

**Further Fury/Protection outcomes:** `Fury_BloodthirstRankUsesCurrentFortyFivePercentAttackPowerCoefficient` checks rank-4 Bloodthirst coefficient, cooldown and movement aura dependency; `Fury_WhirlwindDualWieldHasIndependentWeaponHitsAndSharedCost` verifies dual-hand AoE definitions/cost; `Protection_DefensiveStanceThreatAndDefianceAreAppliedOnce` checks the 1.30 defensive stance threat multiplier and multiplicative 5% per rank Defiance on an actual target ThreatTable. These are contract-level/outcome regressions, not complete numerical validation.

**Scope boundary:** The current Stage 4A branch contains genuine implementation changes and a source-derived talent inventory, but the **entire** set of 52 Warrior talent outcomes and 42 Rogue supported talent outcomes has not been independently run and compared to Forever evidence. Catalog entries explicitly marked `not-modeled` remain unimplemented; encounter AI-only effects are not faked. The audit therefore remains open as **Audited — defects remaining** for all six specializations and PR #15 remains draft pending complete verification and .NET build/test execution.

## Stage 4A finalization gate — CI validated; local owner validation required

**Finalization evidence (October 27, 2026):** [GitHub Actions .NET 10 Release workflow run 40542978017](https://github.com/layoung3/WarcraftSim/actions/runs/40542978017) completed **successfully**: `dotnet restore WarcraftSim.slnx`, `dotnet build WarcraftSim.slnx --no-restore --configuration Release -warnaserror` (**0 warnings**), and `dotnet test WarcraftSim.slnx --no-build --configuration Release` (**1,625/1,625 tests passing**). This supersedes earlier Stage 4A notes describing unexecuted tests or an unavailable CI environment. The repository owner's separate local .NET validation is still required **before merge**.

**Finalization changes following the earlier correction registers:**

- Source and test compilation repairs: nullable Warrior Rage event source; added the missing Rogue test namespace; fixture-specific unmitigated Mortal Strike test resolver and opt-in boss attacks for fixed-duration Protection tests
- Fixed the two remaining Mutilate Opportunity modifiers from 4% to 5% per rank, bringing both hands into alignment with the client tooltip; corrected the Simulation Lab `wwwroot/default-talents/rogue-subtlety.json` rank from 5 to 2 and the obsolete CP142 Vigor expectation from 120 to 110 Energy at rank 2
- Last Stand is now a shield-independent Protection talent; its duration/health handling executes even with no shield. Shield Wall, Shield Block, Shield Slam and shield bash remain shield-gated where applicable
- Added `challenging-shout` to all Warrior spec ability registers and optional custom priorities: **5 Rage, 600s cooldown, 6s forced targeting** of active nearby-enemy candidates, with no permanent threat change. The solo runner assumes its explicitly active scripted enemies are nearby; exact 10-yard geometry remains **encounter-dependent**, not simulated
- Added coverage for shield-independent Last Stand and multi-enemy Challenging Shout without changing the encounter AI, threat formula, or fixed-duration tank behavior
- GitHub Actions `.github/workflows/dotnet-10.yml` now provides a reusable PR build/test gate and uses read-only repository permission

**Current-phase six-specialization disposition:**

| Specialization | Mechanical surfaces examined and represented by existing + Stage 4A regressions | CI-validated result | Deferred scope / uncertainties |
|---|---|---|---|
| **Warrior Arms** | Mortal Strike damage/heal debuff; Rend and rolling Deep Wounds application/tick definitions; Overpower, Execute, Spearing Strike, Improved Slam, stances, Sweeping Strikes/secondary absorbs, weapon talents, Rage procs and threat | **Audited — provisional; validation-ready** | Rend coefficient and server hit tables; Sweeping Strikes secondary roll/mitigation exactness, Improved Charge/Hamstring utility and distance conditions |
| **Warrior Fury** | Bloodthirst AP scaling and movement trigger, Whirlwind per-hand/per-target hit definitions, dual wielding, Flurry, Enrage, Death Wish, Recklessness, Bloodrage, execute interactions, unbridled Rage, Gore Drinker and Weaponmaster extra attacks | **Audited — provisional; validation-ready** | Server proc mask/normalization, rage-income coefficients, fight-specific mobility and control talents |
| **Warrior Protection** | Shield Slam/Block/Bash, Revenge opportunity, Last Stand without shield, Shield Wall, avoidance, incoming mitigation, defensive talent/resource/proc paths, threat and Taunt/Challenging Shout, fixed-duration survivability windows | **Audited — provisional; validation-ready** | Exact incoming Rage/defense/Crush models; boss taunt immunity, location geometry, interrupted control/crowd-control policies |
| **Rogue Assassination** | Mutilate two-hand generation/Seal Fate, Garrote/Rupture, Eviscerate, Venom, Ruthlessness, Quick Recovery, poison charges/stacks/ticks, application dependencies, Vigor and talent rank restrictions | **Audited — provisional; validation-ready** | Poison resistance, tick critical eligibility, snapshot rules and unimplemented real Forever talents outside the supported subset |
| **Rogue Combat** | Sinister Strike, Riposte, Adrenaline Rush Energy boundary, Blade Flurry/extra damage, Hack and Slash/Weapon Expertise, dual wielding, finishers, defense, utility and threat accounting | **Audited — provisional; validation-ready** | Provisional poison/weapon proc masks, server armor-penetration curve and multi-actor positioning |
| **Rogue Subtlety** | Ambush/Backstab/Garrote/cheap-shot stealth gates, Vanish, Preparation/Premeditation, Opportunity 2-rank modifiers, Hemorrhage, positional conditions, stealth/defensive cooldowns, target-owned combo points | **Audited — provisional; validation-ready** | Stealth detection, target movement, PvP crowd-control/AI and unsupported real talents not declared by the model |

The 52-row Warrior talent catalog and 42-row *currently supported* Rogue talent subset remain indexed above. A literal code reference is not evidence of combat correctness; measured client behaviors are separated from provisional server values in the tables above. Catalog entries deliberately marked `not-modeled` are not misclassified as implemented. The supported solo class pathways have no **currently known high-severity independently correctable defects** after the corrections and a green suite; this is not a guarantee that undiscovered defects or future server changes do not exist.

**Completion decision:** Stage 4A is **mechanically complete for the current supported solo-simulation development scope, ready for repository-owner local build and full test validation**, not for immediate merge. PR #15 may be marked ready for review, but **do not merge until the owner confirms local validation**. Stage 4B remains blocked. No unrelated raid AI or gear/frontend work was started.

### Stage 4B final companion formula correction — October 15–20, 2026

**Authoritative mechanic sources:** [October 20 AidanZMoon reply to forever-bugs #394](https://github.com/ClassicWoWCommunity/forever-bugs/issues/394#issuecomment-6302980919) explicitly provides **bonuses, not final attack hit rates**: Hunter pet melee inherits **1× Hunter ranged bonus hit**; Hunter pet spell inherits **17/8× Hunter ranged bonus hit**; Warlock demon spell inherits **1× owner spell bonus hit**; Warlock demon melee inherits **8/17× owner spell bonus hit**. The originating issue contains level-29 player logs from a bugged build and the reply states the behavior **should be corrected**; no independent post-fix level-60 combat logs were supplied. The official [October 15 Blizzard beta notes (posted October 16)](https://us.forums.blizzard.com/en/wow/t/wow-forever-beta-development-notes-%E2%80%93-updated-october-15/2360696) explicitly confirm **Hunter pets benefit from Expertise** and inherit **the higher of Hunter melee or ranged critical chance**. Those are independently verified intended rules; the precise server-side avoidance/expertise conversion remains provisional.

| Class / family | Source-verified inheritance | Existing combat implementation after correction | Controlled override | Remaining independent questions |
|---|---|---|---|---|
| Hunter pet physical / 19 families | Pet melee **bonus hit = Hunter ranged bonus hit** | `BeastRolls`: uses owner actor's current `ranged-hit-bonus-percent` plus provisional level-aware creature physical base miss; independent physical dodge/parry and critical outcomes remain enabled. Pet-family physical attacks and bleed applications use physical resolution. | Nullable legacy `ForeverBeastMasteryHunterTuning.PetHitPercent` explicitly sets FINAL hit when non-null; null is ordinary inheritance default | Creature melee +level coefficient (currently 5 skill/level × 0.04 percentage points), target defenses and any alternate physical cap |
| Hunter pet magical special / family Lightning Breath | Pet spell **bonus hit = Hunter ranged bonus hit × 17/8** | `BeastRolls` uses separate target-level spell base miss; magical family direct-damage effects now use `CombatResolutionTypes.Spell` (previously incorrectly `Melee`), and physical dodge/parry is not rolled. No invented server resistance value. | Same explicit legacy final-hit override | Exact raid boss spell floor/caps beyond existing 0–100 clamp; resist chance remains separate |
| Warlock demon spells / Imp, Voidwalker, Succubus, Incubus, Felhunter, Infernal, Doomguard | Demon spell **bonus hit = owner's spell bonus hit** | `WarlockRollResolver` reads owner effective `spell-hit-percent`, `spell-hit-chance-percent` and shared `hit-chance-percent` at effect resolution; adds bonus to existing +level spell base miss, not fixed 85% | Nullable `DemonSpellHitPercent` remains explicit FINAL override | Level-60 resist table, spell floor/caps and item stat aggregation fidelity |
| Warlock demon creature melee / eligible auto-attacking demons | Demon physical **bonus hit = owner spell bonus hit × 8/17** | Existing `RulesetCombatRollResolver` creature single-roll miss/dodge/parry/crit table now has actual base 95% and a source hit-bonus stat, plus the existing creature-level adjustment provider. The current owner's effective stats are read on every attack. | Nullable `PetMeleeHitPercent` explicitly sets FINAL hit for controlled test fixtures; null inherits normally | Whether exact creature +3-level miss curve differs from the current provisional context provider |
| Hunter pet Expertise | A pet must benefit from owner's Expertise | `HunterExpertisePercent` on the owner actor reduces eligible dodge and parry **only**, clamped at zero. `BeastRolls` reads owner dodge/parry reduction stats at each hit. Does not change pet spells or physical miss percentages. | Owner Expertise = 0 or 100 percentage points tested; base `PetAvoidancePercent` retains explicit encounter control | Mapping item Expertise rating/skill to percentage-point avoidance reduction on live server remains unavailable |
| Hunter pet critical strike | Pet inherits **higher owner ranged/melee crit** | Owner's effective ranged crit includes Lethal Attacks; owner-actor melee/ranged crit stats are read at each pet roll. One max operation is followed by pet-specific Ferocity and configurable pet-only critical bonuses **once**; no double stacking. | Controlled owner melee or ranged 100% crit tests | Unverified pet crit suppression vs raid bosses and stat-sheet visual bugs |

**Regression evidence:** `WarcraftSim.Tests/Consolidated/CP166_Stage4B_PetFormulaInheritance.cs` verifies production-runner melee and spell misses under zero inherited hit, capped inherited owner-hit cases, old explicit final-hit overrides, physical Expertise suppression vs magical spell-miss isolation, either higher Hunter critical source, all seven demon rosters and Hunter/Warlock damage accounting. It also explicitly checks the 17/8 and 8/17 numerical conversion ratios. Existing CP163/164/166 family and damage tests are preserved. As the client has no post-correction combat logs, do **not** recast intended ratios as fully measured observed hit rates.

**Baseline compatibility:** No second pet engine, no rotation changes, no Stage 4C work. Previous Stage 4B family specials, spell and damage-accounting corrections are unchanged apart from correcting their attack-table eligibility. All remaining exact hit tables and coefficients remain provisional, as in Formula Watch.

## Stage 4B — Hunter and Warlock executable audit (October 27, 2026)

**Baseline:** `e424959b91d9077c1cc4eb6e14b79de89d3b8af7` (merged Stage 4A). **Working PR:** [#16](https://github.com/layoung3/WarcraftSim/pull/16) / `cp166-stage4b-hunter-warlock-audit`. Preserve Stage 1–4A behavior; Stage 4C blocked.

**Evidence grading:** Official Blizzard [Hunter deep dive](https://worldofwarcraft.blizzard.com/en-us/news/24301515) (September 30) confirms baseline Aimed Shot, independent Arcane cooldown, fire/frost trap cooldown split, pet family unique abilities and updated Hunter talents. Official Blizzard [Warlock deep dive](https://worldofwarcraft.blizzard.com/en-us/news/24310968) (October 7) confirms new Bane effects, Soul Link moved to row five, Demonic Pact sacrifice-family behavior and revised Master Demonologist. Client-derived [Improved Shadow Bolt build 70532](https://bindonforever.com/spell/17793) confirms 4–20% own Shadow damage vulnerability after a Shadow Bolt critical for 12 seconds, with **no charges**; [Bane of Havoc client spell](https://www.wowhead.com/forever/spell=1225228/bane-of-havoc) confirms 15% of damage to other targets for five minutes, one target at a time. Exact mitigation curves, pet proc masks, some ranks and spell coefficients are not server-verified. Source-key appearance is evidence of **connectivity**, not proof of hit, damage or cooldown behavior.

### Specialization execution surfaces and results

| Specialization | Executable mechanics inspected | Current status / boundaries |
|---|---|---|
| Hunter Marksmanship | `MarksmanshipHunterSimRunner`, `ForeverMarksmanshipHunterSpellbook`, reactive damage/critical/proc logic, stationary Auto Shot, shared Aimed/Multi, independent Arcane, 4s Sniper, Rapid Fire, Rapid Killing, Rapid Recuperation, Serpent Sting, no-pet Lone Wolf and AP stat conversions | **Audited — provisional**: Rapid Fire now reschedules already-active weapon autos at haste gain and removal; exact haste stacking and hit/crit coefficient questions remain server-dependent |
| Hunter Survival | `SurvivalHunterSimRunner`, `ForeverSurvivalHunterSpellbook`, Raptor queued swing, Mongoose opportunity, Strider Kick, Lacerating Strikes, fire/frost trap separation, arming/proximity, Entrapment DR, melee/ranged weaving travel and input latency | **Audited — provisional**: melee-first/default interface preserved; stationary target-selection, trap overlap and source hit models are provisional |
| Hunter Beast Mastery | `BeastMasteryHunterSimRunner`, `ForeverBeastMasteryHunterSpellbook`, stat inheritance, auto and Focus, family abilities, Frenzy, Bestial Wrath, Intimidation, Mend/Revive, Hawk and group threat | **Audited — provisional**: revived pets now resume scripted incoming-hit chain; four offensive family DoTs require a landed application rather than guaranteed damage |
| Warlock Affliction | `WarlockSimRunner`, `ForeverWarlockDpsSpellbook`, Corruption, Bane of Agony, Wrack channel, Pandemic, Malevolence, improved drain/tap, Shadow Mastery, hit and crit attribution | **Audited — provisional**: Stage 3 application/tick separation retained; snapshot, resistant DoTs and user-target range remain provisional |
| Warlock Demonology | `WarlockSimRunner`, `WarlockDemonCompanion`, Demonology stat/talent scaling, Soul Link, Demonic Knowledge, Unholy Power, Sacrifice, Pact, Master Demonologist, replacement, summoning and expiry | **Audited — provisional**: keeps different-family sacrifice across Pact resummons; pet inheritance, mastery ranks, demon spell masks pending server verification |
| Warlock Destruction | `WarlockSimRunner`, `WarlockConflagrateConsumptionProcessor`, Fire/Shadow direct damage, Immolate dependency, Conflagrate consume/retain, Ruin, Incinerate, Agonizing Flames, Bane, Improved Shadow Bolt, Bane of Havoc | **Audited — provisional**: missing Improved Shadow Bolt and Bane of Havoc behavior corrected through source-owned aura processors; cross-target Bane selection is opt-in, not the single-target default rotation |

### Supported talent-by-executable-reference inventory

The following records are generated from the actual branch catalog entries and evaluated against spellbook/runner processors. Unsupported real-tree talents remain outside these supported catalogs; do not treat them as implemented merely because other tree nodes exist.

#### Hunter Marksmanship (12 supported keys)

| Talent | Rank cap | Executable refs | Classification |
|---|---:|---|---|
| `lethal-attacks` | 5 | `ForeverMarksmanshipHunter.cs`, `ForeverBeastMasteryHunter.cs`, `MarksmanshipHunterSimRunner.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `efficiency` | 5 | `ForeverMarksmanshipHunter.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `careful-aim` | 5 | `ForeverMarksmanshipHunter.cs`, `MarksmanshipHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `lone-wolf` | 1 | `ForeverMarksmanshipHunter.cs`, `MarksmanshipHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `improved-stings` | 3 | `ForeverMarksmanshipHunter.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `barrage` | 3 | `ForeverMarksmanshipHunter.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `mortal-shots` | 5 | `ForeverMarksmanshipHunter.cs`, `MarksmanshipHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `trueshot-aura` | 1 | `ForeverMarksmanshipHunter.cs`, `MarksmanshipHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `sniper-shot` | 1 | `ForeverMarksmanshipHunter.cs`, `MarksmanshipHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `improved-arcane-shot` | 5 | `ForeverMarksmanshipHunter.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `rapid-killing` | 2 | `ForeverMarksmanshipHunter.cs`, `MarksmanshipHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `rapid-recuperation` | 2 | `ForeverMarksmanshipHunter.cs`, `MarksmanshipHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |

#### Hunter Survival (18 supported keys)

| Talent | Rank cap | Executable refs | Classification |
|---|---:|---|---|
| `improved-tracking` | 5 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `deflection` | 5 | `ForeverSurvivalHunter.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `entrapment` | 5 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `savage-strikes` | 2 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `survivalist` | 5 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `improved-wing-clip` | 3 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `clever-traps` | 2 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `surefooted` | 3 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `deterrence` | 1 | `ForeverSurvivalHunter.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `survival-tactics` | 2 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `predators-edge` | 5 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `counterattack` | 1 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `resourcefulness` | 2 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `expose-prey` | 2 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `survivalists-discipline` | 2 | `ForeverSurvivalHunter.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `strider-kick` | 1 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `lightning-reflexes` | 5 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `lacerating-strikes` | 1 | `ForeverSurvivalHunter.cs`, `SurvivalHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |

#### Hunter Beast Mastery (18 supported keys)

| Talent | Rank cap | Executable refs | Classification |
|---|---:|---|---|
| `deadly-aspects` | 5 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `endurance-training` | 5 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `focused-fire` | 2 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `improved-aspect-of-the-monkey` | 3 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `pathfinding` | 2 | `ForeverBeastMasteryHunter.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `improved-revive-pet` | 2 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `bestial-swiftness` | 1 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `unleashed-fury` | 5 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `improved-mend-pet` | 2 | `ForeverBeastMasteryHunter.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `ferocity` | 5 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `summon-hawk` | 1 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `spirit-bond` | 2 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `intimidation` | 1 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `bestial-discipline` | 2 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `frenzy` | 5 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `bestial-wrath` | 1 | `ForeverBeastMasteryHunter.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `lethal-attacks` | 5 | `ForeverMarksmanshipHunter.cs`, `ForeverBeastMasteryHunter.cs`, `MarksmanshipHunterSimRunner.cs`, `BeastMasteryHunterSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `improved-concussive-shot` | 5 | `ForeverBeastMasteryHunter.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |

#### Warlock Affliction/Demonology/Destruction (27 supported keys)

| Talent | Rank cap | Executable refs | Classification |
|---|---:|---|---|
| `improved-corruption` | 5 | `ForeverWarlockDpsSpellbook.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `grim-reach` | 2 | No direct use | Encounter-dependent: range cannot affect fixed-position solo |
| `suppression` | 5 | `WarlockSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `improved-life-tap` | 2 | `ForeverWarlockDpsSpellbook.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `improved-drains` | 3 | `ForeverWarlockDpsSpellbook.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `pandemic` | 3 | `WarlockSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `malevolence` | 5 | `WarlockSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `shadow-mastery` | 5 | `ForeverWarlockDpsSpellbook.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `wrack` | 1 | `WarlockSimRunner.cs`, `ForeverWarlockDpsSpellbook.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `unholy-power` | 5 | `WarlockSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `demonic-sacrifice` | 1 | `WarlockSimRunner.cs`, `WarlockDemonCompanion.cs`, `WarlockPrePullSacrificeProcessor.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `soul-link` | 1 | `WarlockSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `fel-vitality` | 3 | `WarlockSimRunner.cs`, `WarlockDemonCompanion.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `demonic-knowledge` | 3 | `WarlockSimRunner.cs`, `WarlockDemonCompanion.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `demonic-pact` | 1 | `WarlockDemonCompanion.cs`, `WarlockPrePullSacrificeProcessor.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `master-demonologist` | 5 | `WarlockSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `improved-felhunter` | 3 | `WarlockDemonCompanion.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `improved-imp` | 3 | `WarlockDemonCompanion.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `improved-sayaad` | 3 | `WarlockDemonCompanion.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `bane` | 5 | `ForeverWarlockDpsSpellbook.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `cataclysm` | 3 | `ForeverWarlockDpsSpellbook.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `ruin` | 5 | `WarlockSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `improved-shadow-bolt` | 5 | `WarlockSimRunner.cs`, `WarlockImprovedShadowBoltProcessor.cs` | Corrected: owner-only 12s Shadow vulnerability on critical Shadow Bolt |
| `incinerate` | 1 | `WarlockSimRunner.cs`, `ForeverWarlockDpsSpellbook.cs`, `Warlock70425DamageModifiers.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `bane-of-havoc` | 1 | `ForeverWarlockDpsSpellbook.cs`, `WarlockBaneOfHavocProcessor.cs` | Corrected: executable spell and target-owned cross-target transfer (no target AI) |
| `agonizing-flames` | 3 | `WarlockSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |
| `shadow-and-flame` | 5 | `WarlockSimRunner.cs` | Connected source reference; independent numbers/trigger masks still provisional unless evidenced below |

### Hunter pet-family executable roster (19)

The common pet actor, damage, Focus and threat pipeline remains `CompanionActorFactory` and the class's normal `AbilityExecutor`. Each row maps one catalog family to its distinct spellbook special and `BeastState` execution path. Do **not** interpret merely registering an ability as proving live-server numerical exactness.

| Pet family | Signature special | Applied combat/utility path | Coverage |
|---|---|---|---|
| `bat` | `pet-sonic-blast` | Signature direct ability and optional debuff | **Executable; numerical provisional** |
| `bear` | `pet-swipe` | 3-target physical strike | **Executable; numerical provisional** |
| `bird-of-prey` | `pet-mine` | Signature direct ability and optional debuff | **Executable; numerical provisional** |
| `boar` | `pet-charge` | Signature direct ability and optional debuff | **Executable; numerical provisional** |
| `carrion-bird` | `pet-demoralizing-screech` | Explicit timed utility aura/haste/defense | **Executable; numerical provisional** |
| `cat` | `pet-prowl` | Explicit timed utility aura/haste/defense | **Executable; numerical provisional** |
| `crab` | `pet-pinch` | Signature direct ability and optional debuff | **Executable; numerical provisional** |
| `crocolisk` | `pet-dismember` | Signature direct ability and optional debuff | **Executable; numerical provisional** |
| `fox` | `pet-tricksters-dance` | Explicit timed utility aura/haste/defense | **Executable; numerical provisional** |
| `gorilla` | `pet-thunderstomp` | up-to-9-target area damage | **Executable; numerical provisional** |
| `hyena` | `pet-tendon-rip` | Application hit + scheduled periodic ticks | **Executable; numerical provisional** |
| `raptor` | `pet-savage-rend` | Application hit + scheduled periodic ticks | **Executable; numerical provisional** |
| `scorpid` | `pet-scorpid-poison` | Application hit + scheduled periodic ticks | **Executable; numerical provisional** |
| `spider` | `pet-web` | Application hit + scheduled periodic ticks | **Executable; numerical provisional** |
| `tallstrider` | `pet-dust-cloud` | Explicit timed utility aura/haste/defense | **Executable; numerical provisional** |
| `turtle` | `pet-shell-shield` | Explicit timed utility aura/haste/defense | **Executable; numerical provisional** |
| `wind-serpent` | `pet-lightning-breath` | Signature direct ability and optional debuff | **Executable; numerical provisional** |
| `wolf` | `pet-furious-howl` | Explicit timed utility aura/haste/defense | **Executable; numerical provisional** |
| `core-hound` | `pet-lava-breath` | Signature direct ability and optional debuff | **Executable; numerical provisional** |

### Warlock demon executable roster (7)

| Demon | Signature action | Attack mode | Lifecycle / coverage |
|---|---|---|---|
| `imp` | `demon-firebolt` | Spellcaster without generic auto | Persistent until death/sacrifice/replacement; **executed, provisional scaling** |
| `voidwalker` | `demon-torment` | Melee auto + spell/utility | Persistent until death/sacrifice/replacement; **executed, provisional scaling** |
| `succubus` | `demon-lash-of-pain` | Melee auto + spell/utility | Persistent until death/sacrifice/replacement; **executed, provisional scaling** |
| `incubus` | `demon-lash-of-pain` | Melee auto + spell/utility | Persistent until death/sacrifice/replacement; **executed, provisional scaling** |
| `felhunter` | `demon-shadow-bite` | Melee auto + spell/utility | Persistent until death/sacrifice/replacement; **executed, provisional scaling** |
| `infernal` | `demon-immolation` | Melee auto + spell/utility | Temporary expiration and cast cancellation; **executed, provisional scaling** |
| `doomguard` | `demon-doom-bolt` | Melee auto + spell/utility | Temporary expiration and cast cancellation; **executed, provisional scaling** |

### Corrections, integration tests, and explicitly deferred work

- **Marksmanship:** retime the currently scheduled Auto Shot when Rapid Fire applies/expires rather than changing a stat that affects only later swings. `MarksmanshipRapidFireRetimesAlreadyActiveAutoShotsAndExpires` compares identical auto-only timed runs.
- **Beast Mastery:** after Revive Pet, restart the scripted incoming-hit chain lost at death; `BeastMasteryRevivedPetsResumeScriptedIncomingAttacks` guards repeatability. Hunter family signature DoTs `savage-rend`, `tendon-rip`, `scorpid-poison` and `web` now roll one real application hit, with later ticks inheriting success under Stage 3; four parameterized tests force 0% pet hit and verify no periodic damage.
- **Companion breadth:** `AllNineteenPetFamiliesHaveIndependentExecutableSpecialAbilities` executes all 19 normal pet ability paths with actual focus and combat events; `EveryWarlockDemonUsesItsOwnAbilityManaAndActivityState` covers seven demons; prior CP163 and CP164 sweeps also verify all families, damage and threats under 1- and 3-target scenarios.
- **Warlock Destruction:** `WarlockImprovedShadowBoltProcessor` triggers exclusively from own landed critical Shadow Bolt, refreshes a 12-second target-owned aura, and increases only the Warlock's own Shadow damage 4% per rank. It is **not** Classic charge-based raid-wide vulnerability. `WarlockBaneOfHavocProcessor` applies a one-target-per-owner aura and echoes 15% of the Warlock's actual damage to **other** targets, never self/pet damage and never recursive echoes. The spell definition retains configurable 5%-base-mana assumption; no automatic second-target targeting is invented. Regression tests cover source isolation, expiry, actual critical casts, Bane target swap and echo.
- **Warlock full spec:** `WarlockImmolateApplicationMissPreventsPeriodicDamage` forces missed application; `DemonicPactPreservesDifferentSacrificeButCancelsSameFamily` keeps existing same-family Pact rules across Succubus/Incubus equivalence; `AllSixHunterAndWarlockSpecsExecuteAndSerializeWithSharedEngine` runs actual public response/summary paths.
- **Existing suite:** All earlier shared Stage 1–4A, Hunter CP158–163 and Warlock CP164–165 tests are preserved. Do not weaken existing tests.
- **Deliberately encounter-dependent:** detection/movement, trap geometry, Hunter weaving optimization, utility crowd-control and dispels; multi-target Warlock Bane selection and dynamic raid targeting; boss immunities; demon spell control decisions without configured target conditions. `grim-reach` is range-only in the fixed-position solo runner, not a DPS boost.
- **Provisional server data:** outgoing/incoming creature hit/crit tables, partial resists, AP and spell-power coefficients, pet scaling/family bonuses, periodic snapshot vs dynamic tick rules, cooldown hotfixes, pet focus regeneration and timing, Soul Link mitigation, demon resistance, Hunter sting damage. Preserve Formula Watch unknowns.

**Pet attack-table follow-up:** physical Bite/Claw/Swipe-style family specials and physical bleed application attempts now declare dodge/parry eligibility in `ForeverBeastMasteryHunterSpellbook`, matching `BeastRolls` rather than bypassing attack avoidance while auto-melees remained avoidable. Magic specials remain spell-like; periodic ticks retain Stage 3 application inheritance and never reroll dodge. `PhysicalPetFamilySpecialsUsePetMeleeAvoidance` exercises direct Bear Swipe and Raptor bleed application with forced 100% pet avoidance.

**Specialization scope boundary:** Affliction's `grim-reach` affects distance only; no stationary solo damage effect is invented. Unsupported abilities, including an unregistered Drain Life/Unstable Affliction and Demonic Brand, are explicitly outside the currently implemented subset rather than claimed as verified or silently substituted from Classic rules. Existing Shadow Bolt/Incinerate/Immolate/Conflagrate, pet stats and temporary summon restrictions remain in the default supported simulation and regressions.

**Further damage-attribution correction:** Bane of Havoc copies already-resolved effective damage. `WarlockRollResolver` now explicitly returns an unchanged landed hit for `bane-of-havoc-echo`, so the copied amount is not multiplied a second time by own Shadow Vulnerability, Demonic Sacrifice, or school-specific damage bonuses. The echo remains noncritical, cannot hit the original enemy, and cannot recursively retrigger Havoc. This restores conservative damage accounting across target-specific shadow modifiers.

**Validation-ready release gate:** [GitHub Actions run 40558284292](https://github.com/layoung3/WarcraftSim/actions/runs/40558284292) for commit `b3f4ebf816aadd6903c336e87558806a04c349a8` succeeded: `.NET 10` Release `-warnaserror` build **zero warnings** and **1,650/1,650 xUnit tests passed**. This included the full existing cross-class suite and new controlled 19-family, 7-demon, all-six-spec, poison/periodic, pet avoidance, sacrifice and spell-proc cases. The final documentation-only matrix update has no runtime behavior change. **Stage 4B is mechanically complete for the currently supported solo implementations, but numerically provisional; repository-owner local validation is REQUIRED before PR #16 can merge.** Stage 4C must not start automatically.

## Stage 4 — class specializations (Stage 4A validation-ready; other classes not started)

| Class / specializations | Meaningful complete per-spec executable audit | Status |
|---|---|---|
| Warrior — Arms, Fury, Protection | Stage 4A six-spec executable audit and CI completed; server-only data remains provisional; local owner validation pending | **Audited — provisional** |
| Hunter — Marksmanship, Survival, Beast Mastery | Stage 4B mechanical source and companion execution review, owner-local validation outstanding; current supported formulas provisional | **Audited — provisional** |
| Rogue — Assassination, Combat, Subtlety | Stage 4A partial correction checkpoint above; complete inventory and independent validation outstanding | **Audited — defects remaining** |
| Mage — Arcane, Fire, Frost | Pending | **Not audited** |
| Warlock — Affliction, Demonology, Destruction | Stage 4B owner/companion execution review and corrected talent pathways, owner-local validation outstanding; numerical unknowns provisional | **Audited — provisional** |
| Priest — Discipline, Holy, Shadow | Pending | **Not audited** |
| Shaman — Enhancement, Elemental, Restoration | Pending | **Not audited** |
| Druid — Balance, Feral Cat, Feral Bear, Restoration | Pending | **Not audited** |

## Stage 5 — companions and already implemented modifiers (not started)

| System | Status | Required later |
|---|---|---|
| 19 Hunter pet families and 7 Warlock demons | **Audited — provisional** (Stage 4B) | Current executable families, special abilities, Focus/Mana, lifecycle and damage attribution tested; exact server coefficients, encounter-specific targeting and raid interactions remain provisional |
| Static raid buffs / boss debuffs / consumables | **Not audited** | Selected effect-component stacking, exclusivity and duration |
| Existing enchants / equipment / on-use / racials / engineering effects | **Not audited** | Active effect eligibility, proc frequency, duration, threat and source version |

## Validation / release gate

1. **Stage 2 validation:** User confirmed `dotnet build` and full `dotnet test` passed after PR #12; Stage 3 authorized.
2. **Stage 3 validated:** user confirmed clean local `dotnet build` and the full test suite passing after PR #14, before authorizing Stage 4A. New Stage 4A tests are NOT validated by the older run.
3. Review DoT application/tick traces, dependent AoE target results, duplicate completion counts, Divine Aegis proc sourcing, Priest/Restoration healer heal-overheal and mana, and representative class-run logs.
4. Stage 4A CI is green; obtain repository owner confirmation of the branch's local full build/test before merging PR #15 or proceeding to Stage 4B.
5. Stage 4A is validation-ready; do **not** start Stage 4B until PR #15 is locally validated and merged to main. Remaining server-data unknowns (resistance distribution, per-rank snapshots, haste/tick policy, threat/proc coefficients) are explicitly retained as provisional audit dependencies rather than guessed values.
6. The full CP166 global audit remains open: Stage 4 specialization review and Stage 5 pets/modifier review are still required before consumable, gear, frontend or raid work.

### Stage 3 local regression repair (October 27, 2026)

User's first local Stage 3 run compiled and executed 1,592 tests: 1,587 passed and five failed. Four regressions traced to the new per-target effect-result lookup: enemy-targeted **single-target** impact results were no longer accessible to a subsequent **self-targeted** reactive effect (dodge/block/glancing/crushing and Bloodthirst's self buff). The repair preserves the original effect-wide result only for a proven single-target prerequisite; multi-target dependencies still require the same target's own result. The fifth failure was a stale validator restriction rejecting multi-target dependencies, despite the new per-target result storage. That restriction has been removed. These are execution/validation fixes, **not** changes to combat coefficient formulas or test expectations. The repository owner has since confirmed clean local `dotnet build` and full `dotnet test`; Stage 3 validation gate is satisfied.

---

## Stage 4C — Mage and Priest executable audit (October 27, 2026)

**Starting main:** `36ec636ab303b41265ba6fc3a6a254da5a4d28a9` (Stage 4B locally validated and merged). **Single PR:** [#17](https://github.com/layoung3/WarcraftSim/pull/17), branch `cp166-stage4c-mage-priest-audit`. Do not merge before repository-owner local validation. Stage 4D, rotation tuning, equipment/consumable databases, unrelated frontend/raid systems are excluded.

**Evidence/authority.** Official [Priest and Warrior deep dive](https://news.blizzard.com/en-us/article/24301514/world-of-warcraft-forever-class-deep-dives-priest-and-warrior), official [October 26 beta notes](https://www.bluetracker.gg/wow/topic/us-en/2360696-wow-forever-beta-development-notes-updated-october-26/) and newer client [build 1.60.1.70532](https://theforeverera.com/en/talents/). Client tooltips establish intended values, **not proof** of unavailable server coefficients/partial resists. Newer 70532 Shadow Word: Death **18s** and Vampiric Embrace **30s** cooldowns supersede contradictory earlier September preview documentation. [Shadow Affinity removal](https://theforeverera.com/en/talents/priest/shadow-affinity/) is current client data, not an assumption drawn from Classic.

### Executable specialization and baseline spell inventory

| Specialization | Supported executable abilities (per actual spellbooks) | Mechanics exercised and stage-specific corrections | Scope-qualified status |
|---|---|---|---|
| Mage Arcane | Arcane Blast, Arcane Wrath, Arcane Missiles channel; cross-tree Fireball, Frostbolt, Frostfire Bolt, utility Evocation and optional mana potion/item system | Arcane damage/crit, Intellect AP-to-SP stacks, cost increases/expiry, Missile Barrage proc/free faster channel, **new Arcane Concentration Clearcasting** and Arcane Meditation prerequisite; global Arcane Instability and corrected Arcane Mind crit | **Audited — provisional**; exact spirit/regen and server spell partial-resists unverified; Arcane Power, Presence of Mind, Shifting Power not registered in current Mage spellbook |
| Mage Fire | Fireball, Fire Blast, Pyroblast, Frostfire Bolt hit+DoT, reactive Ignite; configurable cross-tree spells | Heating Up cast reductions, Ignite triggered critical periodic events, Fire Power target effects, Critical Mass, three-rank Incineration named spell eligibility, Frostfire DoT impact dependency and correct separate periodic SP coefficient | **Audited — provisional**; Conserved rolling Ignite now modeled; exact server rounding provisional; Scorch, Living Bomb, Combustion not registered in the existing supported model |
| Mage Frost | Frostbolt, Frost Blast, Ice Lance, Frostfire Bolt, **new verified Cone of Cold** single-target stationary effect | Fingers of Frost charges, Shatter, Piercing Ice, Frost Channeling mana/threat, current Winter's Chill 5% + 7%-SP per stack applied to the next landed Cone and consumed on hit, target-only resistance assumptions | **Audited — provisional**; Cone range/cone/movement slows, Blizzard, Frost Nova, Cold Snap, Icy Veins and other crowd control are not modeled as full encounter AI; stationary boss assumed in Cone |
| Priest Discipline | Current shared `HolyPriestPreviewRunner` with Flash Heal, Heal, Greater Heal, Renew, Power Word: Shield, Binding Heal, Prayer of Healing, talent-gated Penance/PoM | Separate spell effect, healing received modifiers, Spirit 5SR/Meditation, Mana efficiency and Mental Strength, Soul Warding shield, **Penance immediate first tick plus 1s and 2s ticks**, Divine Aegis nonperiodic crit absorb, mitigation and threat | **Audited — provisional**; Power Infusion, Inner Focus, offensive Penance and raid-specific target policy not represented as runnable spells |
| Priest Holy | Same 9-spell healing toolkit, specialization-specific fixed-scripted incoming-damage priority and optional additional members | Direct/critical/periodic healing, HoT refresh, spiritual healing/guidance, effective vs overheal, absorption, 5-target group healing, Prayer of Mending source-owned bounce and overheal/proc triggers | **Audited — provisional**; Circle of Healing, Holy Nova, area radius, other healing talents or detailed healer AI not in current spellbook |
| Priest Shadow | Shadow Word: Pain, Devouring Plague, Mind Blast, Mind Flay channel, Shadow Word: Death and talented Vampiric Embrace | Shadowform crit/cost, personal Shadow Weaving, progressive Improved Mind Flay, backlashes based on **10% maximum Priest health** when victim survives, Devouring Plague health recovery, real damage/periodic/channel attribution, new shared enemy threat reporting | **Audited — provisional**; unsupported dark inspiration/Spirit Tap kill simulation, distance-only Shadow Reach, missing actual enemy resistance, and unmodeled raid/aura interaction remain documented |

**Executable ability totals:** 14 Mage definitions (including the passive Ignite reaction, Evocation and mana potion), 9 Priest healing definitions, 6 Shadow damage/aura definitions; **29 spell definitions total** in the spellbooks after adding Cone of Cold. Some definitions require talent or custom-priority activation, and not all can execute in a default solo rotation. Three Mage, two Priest healer variants and one Shadow Priest simulation use the same shared Core caster and reporting model, not separate engines.

### Talent keys and connection status — 52 supported entries

#### Mage Arcane/Fire/Frost (23 supported keys)

| Key | Spec/tree | Rank cap | Actual runner/effect path | Coverage and remaining constraint |
|---|---|---:|---|---|
| `arcane-focus` | arcane | 5 | `MageSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `arcane-impact` | arcane | 3 | `MageSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `arcane-concentration` | arcane | 5 | `MageSimRunner.cs` | Corrected: seeded 2%-per-rank damage proc; next damaging spell free, 15s, 1s ICD; actual resource timeline tests |
| `arcane-meditation` | arcane | 3 | `MageSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `arcane-mind` | arcane | 5 | `MageSimRunner.cs` | Corrected: +20% of base Arcane critical bonus/rank; +2% Intelligence/rank retained |
| `arcane-instability` | arcane | 3 | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Corrected: all spell schools +1% damage and +1% crit/rank |
| `arcane-blast` | arcane | 1 | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs`, `MageAdaptiveRotation.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `missile-barrage` | arcane | 2 | `MageSimRunner.cs`, `MageAdaptiveRotation.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `arcane-wrath` | arcane | 1 | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs`, `MageAdaptiveRotation.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `improved-fireball` | fire | 5 | `ForeverMageDpsSpellbook.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `ignite` | fire | 5 | `MageSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `incineration` | fire | 3 | `MageSimRunner.cs` | Corrected: 3 ranks, +2% each on Fire Blast, Ice Lance, Arcane Blast; Scorch itself unsupported |
| `pyroblast` | fire | 1 | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs`, `MageAdaptiveRotation.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `heating-up` | fire | 1 | `MageSimRunner.cs`, `MageAdaptiveRotation.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `fire-power` | fire | 3 | `MageSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `critical-mass` | fire | 5 | `MageSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `elemental-precision` | frost | 3 | `MageSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `ice-shards` | frost | 5 | `MageSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `frost-channeling` | frost | 3 | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Corrected: Frost Mana -5%/rank and spell threat -10%/rank |
| `shatter` | frost | 5 | `MageSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `ice-lance` | frost | 1 | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs`, `MageAdaptiveRotation.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `fingers-of-frost` | frost | 2 | `MageSimRunner.cs`, `MageAdaptiveRotation.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `winters-chill` | frost | 5 | `MageSimRunner.cs` | Connected in Stage4C to Frostbolt, Frost Blast, empowered Ice Lance and next Cone of Cold; no geometry/slow AI |

#### Priest Discipline/Holy shared healing (16 supported keys)

| Key | Spec/tree | Rank cap | Actual runner/effect path | Coverage and remaining constraint |
|---|---|---:|---|---|
| `twin-disciplines` | discipline | 5 | `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `mental-agility` | discipline | 3 | `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `meditation` | discipline | 3 | `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `mental-strength` | discipline | 5 | `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `soul-warding` | discipline | 1 | `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `penance` | discipline | 1 | `ForeverPriestHealingSpellbook.cs`, `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `divine-aegis` | discipline | 3 | `HolyPriestPreviewRunner.cs` | Stage 3 invariant: nonperiodic critical healing only, shared absorb manager |
| `improved-renew` | holy | 3 | `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `improved-power-word-shield` | discipline | 3 | `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `divine-fury` | holy | 5 | `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `improved-healing` | holy | 3 | `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `binding-heal` | holy | 1 | `ForeverPriestHealingSpellbook.cs`, `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `spiritual-guidance` | holy | 5 | `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `spiritual-healing` | holy | 3 | `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `litany-of-light` | holy | 2 | `HolyPriestPreviewRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `prayer-of-mending` | holy | 1 | `ForeverPriestHealingSpellbook.cs`, `HolyPriestPreviewRunner.cs` | Stage 3 invariant: damage/nonperiodic healing proc, 5 charges and caster ownership |

#### Priest Shadow (13 supported keys)

| Key | Spec/tree | Rank cap | Actual runner/effect path | Coverage and remaining constraint |
|---|---|---:|---|---|
| `shadow-focus` | shadow | 5 | `ShadowPriestSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `spirit-tap` | shadow | 5 | No direct runtime effect | Encounter-dependent: kill trigger, cannot accrue on living level-63 dummy |
| `improved-shadow-word-pain` | shadow | 2 | `ForeverPriestShadowSpellbook.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `shadow-reach` | shadow | 2 | No direct runtime effect | Encounter-dependent: distance-only in stationary solo runner |
| `mind-flay` | shadow | 1 | `ForeverPriestShadowSpellbook.cs`, `ShadowPriestSimRunner.cs`, `ShadowPriestProgressiveMindFlayProcessor.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `improved-mind-flay` | shadow | 2 | `ShadowPriestSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `improved-mind-blast` | shadow | 5 | `ForeverPriestShadowSpellbook.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `shadow-weaving` | shadow | 3 | `ForeverPriestShadowReactiveProcessor.cs` | Corrected: 3 ranks, 33/67/100% personal 2% Shadow stacking effect |
| `vampiric-embrace` | shadow | 1 | `ForeverPriestShadowSpellbook.cs`, `ForeverPriestShadowReactiveProcessor.cs`, `ShadowPriestSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `devouring-contagion` | shadow | 2 | `ForeverPriestShadowSpellbook.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `early-demise` | shadow | 2 | `ForeverPriestShadowSpellbook.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `darkness` | shadow | 5 | `ShadowPriestSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |
| `shadowform` | shadow | 1 | `ForeverPriestShadowSpellbook.cs`, `ShadowPriestSimRunner.cs` | Definition and execution reference inspected; numerical evidence provisional |

### Specific defects corrected and cross-class compatibility

1. **Mage Arcane:** `MageSpecializationProcessor` now consumes a 15-second Clearcasting proc at successful cast completion instead of ignoring Arcane Concentration entirely. It switches the next damage spell's resource cost to zero at cast start, restores current Arcane Blast stack cost or Missile Barrage cost after use, and excludes periodic/aura self-feedback. `MageSimResponse` exposes generated and consumed Clearcasting counts. `arcane-meditation` requires allocated Arcane Concentration. Critical Arcane Mind now adds 20% of the BASE critical bonus per rank, while Arcane Instability applies to all schools and crit.
2. **Mage Fire:** Incineration cap is now 3 ranks, applied to named Fire Blast, Ice Lance and Arcane Blast effects (Scorch unsupported). **Frostfire periodic effects** now gain only the .08 periodic SP coefficient from temporary Arcane Blast spell-damage stacks, instead of erroneously using the .857 direct impact coefficient. Stage 3 successful-impact dependency remains unchanged.
3. **Mage Frost:** Frost Channeling -5% Frost spell Mana cost/rank and -10% frost threat/rank now reach actual spell/resource/threat paths; verified rank-5 Cone of Cold 555 mana, 10s CD and 325–355 base damage is available as optional custom action, without inventing geometry. Rank 5 Winter's Chill's 100% proc chance applies only to landed Frostbolt, Frost Blast and Fingers-empowered Ice Lance (rank-scaled chance), raising damage of the next landed Cone by 5% and +7% Spell Power per stack (max 5), then consuming.
4. **Priest Shadow:** Shadow Weaving is now a **three-rank** 33/67/100%-proc self-buff, not five obsolete ranks; Shadow Affinity (removed in 70532) deleted from both supported validator and default Simulation Lab preset. Shadow Word: Death's nonlethal backlash now uses 10% of the caster's maximum health rather than its damage dealt; existing 18s SW:D and 30s VE 70532 cooldowns preserved. Damage on enemy now records shared threat, excluding caster self-backlash.
5. **Priest healing:** the Core channel scheduler gained a **default-off** `ChannelTickAtStart` property; Penance now ticks at channel start and after 1s/2s, for the correct 2-second channel, without changing Arcane Missiles, Mind Flay, Wrack or Tranquility schedules. Prayer of Mending and Divine Aegis Stage 3 corrections, effective healing/overhealing and absorb-before-health ordering preserved.
6. **Other classes:** No speculative global spell-crit, resist, stacking or snapshot changes. The sole Core change is an opt-in channel tick-start flag, so previous channels retain their event intervals. Class-specific new threat consumers use the existing shared `ThreatManager`.

**Executed regressions:** Existing Mage CP132/139, Priest CP125/126/127/129, Stage3 healing/periodic, Stage4A/4B suites remain unchanged except current Shadow talent point expectation. New `CP166_Stage4C_MagePriestAudit.cs` covers all six spec response/serialization paths, Mage actual damage/threat, shadow priest periodic/channel/resource/health, effective and overheal accounting, Clearcasting zero-cost casts and proc statistics, Arcane Mind coefficient, Frost Channeling rank cost/threat, Arcane Instability across schools, Winter's Chill/Cone spell damage, correct Penance 0s/1s/2s ticks, critical/heal interactions and SW:D exact max-health backlash.

**Obsolete talent correction:** Client build 70532 [removed Piercing Ice](https://theforeverera.com/en/talents/mage/piercing-ice/). Its three-rank validator entry, default Mage/Frost ranks, Simulation Lab preset and phantom +6% Frost damage multiplier have been removed; unspent points remain explicit declared filler. The outdated Classic talent cannot inflate current Forever Frost damage.

**Ignite close-crit correction and evidence:** [Blizzard's September 24 Forever Mage changes, mapped to build 70532](https://bindonforever.com/spell/11119) explicitly require per-Mage Ignite ownership, conservation of closely timed critical damage and no repeated % damage bonus on the periodic ticks. `MageIgniteRollingProcessor` now carries the `RollingDamageRemaining` balance through the existing shared `AuraManager` and `PeriodicEffectScheduler`; preexisting paid ticks are never replayed, target/caster auras are isolated, and the per-crit new pool is `8% × Ignite rank × already-resolved critical damage`. The old mutable periodic base / premature refresh path is removed. `TwoCloseFireCritsPreserveAllUnpaidIgniteDamage` deterministically verifies that two 100-damage crits produce **80 Ignite damage** across the appropriate 2 or 3 ticks depending on overlap, and that the victim's HP and Mage attribution reconcile. Exact undocumented server rounding and tick quantization remain provisional.

**Final CI-ready evidence:** [GitHub Actions run 40571278450](https://github.com/layoung3/WarcraftSim/actions/runs/40571278450) compiled **.NET 10 Release with `-warnaserror`, zero warnings, and 1,680/1,680 tests passed**, including the new six-spec checks, Cone of Cold, Frostfire coefficient, old class integrations and Ignite conservation. A subsequent whitespace-only Frost preset cleanup does not change execution. Repository-owner local build/full suite remains mandatory **before merging PR #17 into main**.

**Still provisional and separately tracked:** Ignite roll-over/snapshot, dynamic school resistance and discrete partial resists, Frostfire dual-school target resistance, proper travel time, unknown level-60 spell rank/calibration, cooldown hotfixes without logged evidence, real movement/interrupt/pushback on channeled heals, area geometry, dynamic group priorities, Spirit Tap kill proc in boss-alive encounter, unsupported full trees and PvP control. The current Mage spellbook has no Arcane Power/Combustion/Cold Snap or full AoE rotation; the existing Priest healing book has no Inner Focus/Power Infusion or Holy Nova/Circle of Healing. Those are explicitly UNSUPPORTED abilities rather than verified functionality.

**Final gate:** GitHub Actions `.github/workflows/dotnet-10.yml` must pass .NET 10 Release `-warnaserror` build and all xUnit tests at the final PR head. Owner-local repeat build and tests required before merge. Do not advance to Stage4D automatically.


---

## Stage 4D — Shaman and Druid seven-spec execution and source audit (October 27, 2026)

**Baseline:** `c290361058d9264c12613deb9c6a5a7fd35524ad` (merged, owner-locally-validated Stage 4C). **One branch/PR:** `cp166-stage4d-shaman-druid-audit`, [PR #18](https://github.com/layoung3/WarcraftSim/pull/18). Keep open and **unmerged pending owner validation**. Source of truth is the current `main` ancestry and exact changed branch paths.

**Independent Forever source, as of build 1.60.1.70532:** [Shaman talent and spell data](https://theforeverera.com/en/talents/shaman/), [Shaman current rank spells](https://theforeverera.com/en/spells/shaman/), [Restoration Druid talents](https://theforeverera.com/en/talents/druid/), [Healing Stream rank 5](https://theforeverera.com/en/spells/shaman/healing-stream-totem/), [Mana Tide rank 4](https://theforeverera.com/en/spells/shaman/mana-tide-totem/), [Ancestral Healing](https://theforeverera.com/en/talents/shaman/ancestral-healing/), [Healing Way](https://theforeverera.com/en/talents/shaman/healing-way/), [official October Shaman beta notes](https://www.bluetracker.gg/wow/topic/us-en/2360696-wow-forever-beta-development-notes-updated-october-26/). These are client/tooltips and official public notes **not server-side empirical proc/coefficient logs**; affected inputs without client proof remain explicitly provisional. Never substitute Classic or later expansion values.

### Executable spell/ability inventory — 58 spellbook definitions, 7 actual paths

Counts denote registered `AbilityDefinition` actions, including passive/proc definitions and talent-gated spells, **not** count of all default rotation casts, nor proof every rank executes.

| Execution path | Actual ability/action keys exposed | Stage 4D mechanics and caveats | Status |
|---|---|---|---|
| **Shaman Elemental** | `lightning-bolt`, `lightning-bolt-rank7`, `chain-lightning`, `flame-shock`, `lava-burst`, `elemental-mastery`, `searing-totem`, `mana-spring-totem`, `lightning-overload-bolt`, `lightning-overload-chain` (10) | Direct crit/hit and spell school, Flame Shock direct-landed dependent DoT, Clearcasting/resource debits, Overload triggered damage, mana/totem uptime, multi-target chain; Lightning Overload 3/3 Alacrity prerequisite and non-consumption of Clearcasting corrected | **Audited — provisional**; Shock varieties absent, target-by-target Chain Lightning falloff/source eligibility not independently measured, geometry absent |
| **Shaman Enhancement** | `earth-shock`, `lightning-bolt`, `maelstrom-lightning-bolt`, `stormstrike`, `rage-of-the-farseer` (5) **plus** distinct `main-hand-auto`, optional `off-hand-auto`, and `windfury-weapon-extra` / `windfury-totem-extra` background attacks | Actual optional MH/OH dual white streams, penalty only on autos, 50% offhand damage and independently attributed output; Flurry and Maelstrom each hand, Windfury additional melee rolls without recursive procs, only one personal Windfury provider, Warrior opt-in Windfury Totem preserved | **Audited — provisional**; Flametongue/Frostbrand/Rockbiter imbues, Lava Lash, Shamanistic Rage and full group totem deployment absent; offhand imbue not modeled |
| **Shaman Restoration** | `healing-wave`, `lesser-healing-wave`, `chain-heal`, `riptide`, `water-shield`, `nature-swiftness`, `mana-tide-totem`, `healing-stream-totem` (8) | Updated level60 rank bases/mana, 8/17/25% Healing Way, 8/17/25% Ancestral Healing Armor, Chain Heal bounce/falloff and target eligibility, Riptide direct+HoT, Water Shield charges and resource events, Mana Tide flat 290 pulse, 300s group-wide Healing Stream; shared healing/absorb/threat accounting, 75% Armor boundary | **Audited — provisional**; shielding ICD is tunable estimate, absence of group placement/dispels/other totems, direct spell-power coefficients pending logs |
| **Druid Balance** | `wrath`, `starfire`, `moonfire`, `insect-swarm`, `hurricane`, `faerie-fire` (6) | Moonkin/crit, Nature's Grace/Eclipse/Omen, spell Hit, Wrath and Starfire, DoT aura timing, Hurricane 10s channel/secondary spell rolls, Faerie Fire and Thorns scripted encounter; usual damage attribution | **Audited — provisional**; Eclipse/haste precision, Nature's Grace server snapshot, radius/movement, final coefficients unverified; limited cooldowns |
| **Druid Feral Cat** | `claw`, `shred`, `rake`, `rip`, `ferocious-bite`, `pounce`, `ravage`, `cower`, `shifting-power`, `faerie-fire`, `berserk` and shared form utility (part of 22 Feral definitions) | Combo-point generation/finisher spend, positional Prowl opener, Cat energy, Shred reductions, Rake/Rip source-owned bleeds, Omen procs, Shifting Power, Feral threat; Furor full-rank energy retained during Bear interlude | **Audited — provisional**; Tiger's Fury absent, proper combo point target ownership/swap timing, energy tick cadence/form AP inheritance and bleed snapshot exact server behavior need future data |
| **Druid Feral Bear** | `maul`, `swipe`, `lacerate`, `enrage`, `growl`, `challenging-roar`, `feral-charge`, `demoralizing-roar`, `bash`, `primal-bite`, `frenzied-regeneration`, `berserk` and shared Cat/Bear spellbook (22 total combined) | Fixed-duration preserved, shared normalized incoming Rage and dodge/Armor mitigation, 75% armor cap, model projected burst/damage windows, Bear threat, Rage from auto/Enrage/reactive critical and accurate post-resource totals, active form gates/utility taunts | **Audited — provisional**; actual survival instincts/Mangle absence, server Rage input curves provisional, no healer or movement AI, form transition AP/armor snapshot nuance unresolved |
| **Druid Restoration** | `healing-touch`, `rejuvenation`, `regrowth`, `tranquility`, `natures-swiftness`, `wild-growth`, `swiftmend` (7) | Same shared direct/periodic heal core, current Living Spirit→Wild Growth and Naturalist→Nature's Swiftness prerequisites, non-consuming full-HoT Swiftmend, Nature's Focus pushback, Wild Growth target pulse, Tranquility channel, 5SR/effective heal/overheal/threat | **Audited — provisional**; Lifebloom, Tree of Life, Regrowth radius, actual group smart targeting geometry and final coefficients unmodeled |

**Spellbook total:** 10+5+8+6+22+7 = **58** definitions across six spellbooks; Feral Cat/Bear share a single 22-definition spellbook. **Talent total:** 107 *catalog entries across six talent validators* (94 distinct talent string keys; some shared across specializations), see each key and rank cap below. Supported/registered is not synonymous with fully executable.

### Stage 4D follow-up — verified Enhancement haste and regen abilities

Independent build-70532 client evidence: [Rage of the Farseer](https://theforeverera.com/en/spells/shaman/rage-of-the-farseer/) is an instant self ability with **30% attack-speed haste for 25 seconds, 180-second cooldown and no resource cost**. The [talent prerequisite](https://theforeverera.com/en/talents/shaman/rage-of-the-farseer/) is Mental Quickness 2/2. This 70532 release no longer grants casting haste. Previously the catalog admitted the rank but no executable action used it. `ForeverEnhancementShamanSpellbook` now supplies a level-40 Aura ability; the reactive processor adjusts the live actor haste stat and uses `AutoAttackProcessor.RescheduleForHasteChange` on aura application and removal, including active offhand weapons and separate Flurry effects. The default solo rotation uses it when talented, unless `UseRageOfTheFarseer=false` in a test/configured run. `RageOfTheFarseerHastesActiveWeaponSwingsAndExpiresAfter25Seconds` and its talent prerequisite/definition companion test exercise actual attack outcomes, aura expiry and isolation from disabled usage.

[Improved Stormstrike](https://theforeverera.com/en/talents/shaman/improved-stormstrike/) rank 1/2 has **50/100% chance** on Stormstrike to restore **50% Spirit-derived Mana regen during spellcasting for 15s**; on a Shaman's incoming Dodge or Parry the same chance resets Stormstrike's cooldown. This is distinct from outgoing attacks being avoided. Previously the validator accepted ranks but no combat code read them. The reactive processor now applies an actual 15-second refreshable source-owned aura on eligible Stormstrike completions, resets only Stormstrike's ordinary cooldown upon own Dodge/Parry, and exposes proc/reset counts. An **optional, default-null** callback to the existing `FiveSecondManaRegenerationProcessor` adjusts in-five-second-rule Spirit regeneration at aura boundaries; when the aura expires, the previous rate resumes without changing any other caster. Rank prerequisites Stormstrike 1/1 and Mental Quickness 2/2 are enforced. The new deterministic tests verify mana difference, refresh, isolated expiration, rank-gated resets, and no interference with global cooldown or other Shaman skills. The shared Mana-regeneration callback is opt-in; all prior classes still run through the old path and are cross-class tested by the full existing xUnit suite.

These are **verified tooltip mechanics and executable results**, not independent proof of Spirit regeneration base coefficient, hit-table normalization, parry frequency or actual server proc RNG distribution. Maelstrom partial-stack cast-time/Mana behavior is now connected for the existing Lightning Bolt; independent imbue/totem deployment remains an ability-completeness gap.

**Maelstrom Weapon partial-stack correction:** [build 70532 talent tooltip](https://theforeverera.com/en/talents/shaman/maelstrom-weapon/) states **4% cast-time and Mana cost reduction per talent rank per stack**, maximum 5 stacks for 30 seconds, for the Shaman's next Lightning Bolt. Previously the simulator provided only a separate five-stack instant spell and ignored partial stacks on normal Lightning Bolt. `EnhancementMaelstromTimingProvider` now snapshots reduced cast duration at cast start; the reactive processor independently adjusts normal Bolt Mana costs, preserves the active cast's reserved Mana discount if another melee hit grants stacks midcast, consumes only the reserved stacks after the cast, and expires partial stacks after 30s using an internal event. The previous five-stack instant `maelstrom-lightning-bolt` path remains intact. `MaelstromPartialStacksReduceNormalLightningCastTimeAndMana` parameterizes ranks 1 and 5 at 3/5 stacks; `EnhancementMaelstromDiscountsRealLightningBoltResourceAndCastEvents` compares actual spell cast timestamps and Mana debits against the zero-talent control. Chance-to-gain-stack and attack eligibility remain configurable/provisional where server data is unpublished.

### Corrected cross-specialization defects and regression boundaries

- **Elemental:** registered obsolete Call of Thunder rejected; Lightning Overload requires actual Elemental Alacrity rank 3. Triggered Overload spell events no longer expend a paid spell's Clearcasting opportunity. Flame Shock application/tick and Chain Lightning remain on Stage 3 hit-dependent and per-target framework.
- **Enhancement:** opt-in off-hand weapon properties produce real separate offhand stream and 19 percentage-point extra white-miss penalty while preserving normal special-hit rolls; actual +offhand damage applies once through shared factory. Both hands can trigger Flurry and Maelstrom. Windfury extra melee rolls remain nonrecursive and prevent personal Totem+Weapon double gains. Shaman mitigation now shares Stage 2 **75% Armor cap**.
- **Restoration Shaman:** 70532 Healing Wave rank 10 base 1615/Mana 620, Lesser Healing Wave rank 6 base 820/Mana 380, Chain Heal rank 3 base 490/Mana 405, Riptide rank 3 841 initial/161×5 HoT/Mana 385; provisional healing-power scaling held unchanged. Healing Way rank 1/2/3 = 8/17/25% for Healing Wave only. Ancestral Healing crit aura 8/17/25% armor, with incoming Armor cap; Water Shield 3 charges, real caster hits and critical heals/spells, 2% of maximum Mana per charge, provisional 3.5s ICD; Mana Tide rank4 uses fixed 290 mana per 3s tick (4 pulses, one Water slot, 60 Mana cast); Healing Stream rank5 80 Mana/300s/11 heal every 2s to **all eligible living group members**, independent overheal+crit, not just most injured.
- **Balance:** Hurricane cleave and Nature's Grace/Eclipse event reporting audited with existing CP149/152/155 suites; no class-wide unverified changes.
- **Feral Cat/Bear:** Full Furor preserves the accumulated Cat Energy through Bear/Dire Bear while its regeneration is stopped there; previous code erased it. Feral Enrage, critical Rage, Bear auto Rage, Shifting Power Energy and Frenzied Regeneration Rage now emit correct gained/spent `ResourceChanged` and `ResourceTotalAfter` data. Solo Bear remains alive in fixed-duration damage-window testing.
- **Restoration Druid:** full-rank client prerequisites for Wild Growth (3/3 Living Spirit) and Nature's Swiftness (5/5 Naturalist) enforced. Existing Stage 3 HoT, Swiftmend, Tranquility and group healing pipelines are preserved.

**Executable test ledger:** `WarcraftSim.Tests/Consolidated/CP166_Stage4D_ShamanDruidAudit.cs` checks all seven spec response paths and serialization, Elemental Overload/flame periodic and talent validation, real Enhance off-hand/white damage/Windfury mutual exclusion and nonrecursion, Water Shield per-application charges/ICD and caster hits, Healing Stream all-group pulses and zero-effective-overheal fixture, Resto Shaman sourced rank and tick policy, Balance Hurricane multi-target channel, Feral Cat bleed/energy/combos, direct Furor transitions, fixed-duration Bear threat/Armor/Rage, and Restoration Druid periodic/effective/overheal output. Previously audited `CP144_ClassMechanics.cs`, `CP148_ManaTideBoundaries.cs`, `CP149_DruidConnectedMechanics.cs`, `CP146_ConnectedMechanics.cs`, and `CP155_DruidExportCorrections.cs` are preserved with source-backed updated prerequisites and flat Mana Tide fixture. Shared Warrior Totem integration still tested by `Shaman_EnhancementWindfury_CP130.cs` and `Warrior_WindfuryBloodthirst_CP130_2.cs`.

**Provisional numerical mechanics:** 70532 client verifies listed rank base amounts, cooldowns, prerequisites and healing; these do **not** verify all level-63 hit/avoidance tables, MH/OH glancing under current beta known issues, per-tick coefficient, resistance, multi-target area geometry, totem casting range, server internal cooldowns, healing proc masks, Windfury source proc tuning, melee speed normalization and actual logs. Keep numeric knobs explicit and do not optimize rotation throughput.

**Missing high-impact ability inventory for later completeness phase (not Stage4E; Rage of the Farseer and Improved Stormstrike now connected):** Elemental **Earth Shock/Frost Shock and true Shock shared CD**, Elemental Mastery/AoE shock alternates; Enhancement **Flametongue/Frostbrand/Rockbiter imbues, Lava Lash, Shamanistic Rage, independent dual imbues and coordinated party totems**; Restoration Shaman **Earth Shield, full Healing Stream/Mana Tide raid proximity and remaining totem family**; Balance **optional Starfall and full multi-target radius/movement**; Feral Cat **Tiger's Fury, target-swapped combo-point model**; Feral Bear **Mangle, Survival Instincts, full incoming healer AI**; Restoration Druid **Lifebloom, Tree of Life and geometry-aware Wild Growth/Tranquility targeting**. Presence of a key in the original user request is not evidence it has an existing implementation.

**Boundary:** No gear catalogs, raid AI, Stage4E, rotation retuning or frontend redesign. No speculative global combat-roll/resistance changes. Current solo and fixed-duration tank encounters preserved.

### Per-catalog registered talent trace (static; 107 entries)

#### Elemental Shaman: 27 registered talent keys

Registry: `WarcraftSim.Data/Forever/Shaman/ForeverElementalShamanDps.cs`. Runtime-reference column is a *static execution trace*; a reference is not independent proof of its value or proc eligibility. Dashes signify no direct modeled combat effect found outside validator/catalog.

| Talent key | Cap | Direct runtime references | Audit classification / caveat |
|---|---:|---|---|
| `convection` | 5 | `ForeverElementalShamanDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `concussion` | 5 | `ForeverElementalShamanDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `call-of-flame` | 3 | `ForeverElementalShamanDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `elemental-alacrity` | 3 | `ElementalShamanSimRunner.cs`, `ForeverElementalShamanDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `lightning-overload` | 3 | `ElementalShamanSimRunner.cs`, `ForeverElementalShamanDps.cs` | Requires 3/3 Elemental Alacrity; reactive Overload must not consume Clearcasting |
| `elemental-fury` | 5 | `ElementalShamanSimRunner.cs`, `ForeverElementalShamanDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `lava-burst` | 1 | `ForeverElementalShamanDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `elemental-mastery` | 1 | `ForeverElementalShamanDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `elemental-focus` | 1 | `ElementalShamanSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `elemental-devastation` | 3 | — | Registered only / passive not executable in the existing solo model |
| `reverberation` | 5 | `ForeverElementalShamanDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `elemental-precision` | 3 | — | Registered only / passive not executable in the existing solo model |
| `tidal-mastery` | 5 | — | Registered only / passive not executable in the existing solo model |
| `tidal-focus` | 5 | `ElementalShamanSimRunner.cs` | Healing spell Mana -1%/rank; hit +1%/rank per 70532 tooltip |
| `mindfulness` | 3 | `ElementalShamanSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `totemic-focus` | 5 | `ForeverElementalShamanDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `restorative-totems` | 5 | — | Healing Stream +10% healing/rank; Mana Spring +5%/rank only in Elemental |
| `natural-grace` | 3 | — | Registered only / passive not executable in the existing solo model |
| `improved-healing-wave` | 5 | — | Registered only / passive not executable in the existing solo model |
| `thundering-strikes` | 5 | `ElementalShamanSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `ancestral-knowledge` | 5 | `ElementalShamanSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `improved-ghost-wolf` | 2 | — | Registered only / passive not executable in the existing solo model |
| `shamanistic-focus` | 1 | `ForeverElementalShamanDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `elemental-warding` | 3 | — | Registered only / passive not executable in the existing solo model |
| `eye-of-the-storm` | 3 | — | Registered only / passive not executable in the existing solo model |
| `elemental-reach` | 2 | — | Registered only / passive not executable in the existing solo model |
| `trial-by-fire` | 2 | — | Registered only / passive not executable in the existing solo model |

#### Enhancement Shaman: 10 registered talent keys

Registry: `WarcraftSim.Data/Forever/Shaman/ForeverEnhancementShamanTalents.cs`. Runtime-reference column is a *static execution trace*; a reference is not independent proof of its value or proc eligibility. Dashes signify no direct modeled combat effect found outside validator/catalog.

| Talent key | Cap | Direct runtime references | Audit classification / caveat |
|---|---:|---|---|
| `thundering-strikes` | 5 | `EnhancementShamanSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `mental-dexterity` | 3 | `EnhancementShamanSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `elemental-weapons` | 3 | `EnhancementShamanSimRunner.cs` | Windfury bonus AP modifier only; other imbue behaviors unmodeled |
| `shamanistic-focus` | 1 | `ForeverEnhancementShamanSpellbook.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `flurry` | 5 | `ForeverEnhancementShamanReactiveProcessor.cs` | Both equipped white weapon hands use shared haste/charges |
| `stormstrike` | 1 | `EnhancementShamanSimRunner.cs`, `ForeverEnhancementShamanSpellbook.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `mental-quickness` | 2 | `EnhancementShamanSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `improved-stormstrike` | 2 | `ForeverEnhancementShamanReactiveProcessor.cs`, `EnhancementShamanSimRunner.cs`, `FiveSecondManaRegenerationProcessor.cs` | **Corrected:** 50/100% chance on Stormstrike to retain 50% Spirit Mana regeneration during 5SR for 15s; 50/100% incoming dodge/parry cooldown reset; direct execution and expiration tests. |
| `maelstrom-weapon` | 5 | `EnhancementShamanSimRunner.cs`, `EnhancementMaelstromTimingProvider.cs`, `ForeverEnhancementShamanReactiveProcessor.cs`, `ForeverEnhancementShamanSpellbook.cs` | **Corrected:** 4% per rank per stack reduced **normal** Lightning Bolt cast time and Mana (20%/stack at max rank), up to 5 stacks; 30s partial-stack expiry, per-cast snapshot/consumption, original 5-stack instant Bolt preserved. Extra Windfury attacks remain proc-ineligible. |
| `rage-of-the-farseer` | 1 | `ForeverEnhancementShamanSpellbook.cs`, `ForeverEnhancementShamanReactiveProcessor.cs`, `EnhancementShamanSimRunner.cs` | **Corrected:** self aura 30% weapon attack-speed bonus for 25s, 180s cooldown, both active weapon streams retimed on apply/remove; requires Mental Quickness 2/2. |

#### Restoration Shaman: 15 registered talent keys

Registry: `WarcraftSim.Data/Forever/Shaman/ForeverRestorationShaman.cs`. Runtime-reference column is a *static execution trace*; a reference is not independent proof of its value or proc eligibility. Dashes signify no direct modeled combat effect found outside validator/catalog.

| Talent key | Cap | Direct runtime references | Audit classification / caveat |
|---|---:|---|---|
| `tidal-focus` | 5 | `ForeverRestorationShaman.cs` | Healing spell Mana -1%/rank; hit +1%/rank per 70532 tooltip |
| `totemic-focus` | 5 | `ForeverRestorationShaman.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `mindfulness` | 3 | `RestorationShamanSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `natural-grace` | 3 | `RestorationShamanSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `improved-reincarnation` | 2 | `RestorationShamanSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `ancestral-healing` | 3 | `RestorationShamanSimRunner.cs`, `RestorationShamanCombatProcessor.cs` | Corrected armor to 8/17/25%; independent crit heal gate; 75% Armor ceiling |
| `purification` | 5 | `RestorationShamanCombatProcessor.cs`, `ForeverRestorationShaman.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `healing-focus` | 3 | — | Registered only / passive not executable in the existing solo model |
| `nature-swiftness` | 1 | `ForeverRestorationShaman.cs` | Shaman instant Nature healing present; aura expiration/cast restoration checked |
| `tidal-mastery` | 5 | `RestorationShamanSimRunner.cs`, `RestorationShamanCombatProcessor.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `healing-way` | 3 | `ForeverRestorationShaman.cs` | Corrected to 8/17/25% Healing Wave based on client 70532 |
| `water-shield` | 1 | `ForeverRestorationShaman.cs` | Incoming caster hits or spell criticals consume 3 charges; 2% max Mana; 3.5s provisional ICD |
| `mana-tide-totem` | 1 | `ForeverRestorationShaman.cs` | Rank4 60 Mana cost, 4 x 290 Mana pulses every 3s, 12s Water slot |
| `restorative-totems` | 5 | `RestorationShamanCombatProcessor.cs` | Healing Stream +10% healing/rank; Mana Spring +5%/rank only in Elemental |
| `riptide` | 1 | `ForeverRestorationShaman.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |

#### Balance Druid: 20 registered talent keys

Registry: `WarcraftSim.Data/Forever/Druid/ForeverBalanceDruidDps.cs`. Runtime-reference column is a *static execution trace*; a reference is not independent proof of its value or proc eligibility. Dashes signify no direct modeled combat effect found outside validator/catalog.

| Talent key | Cap | Direct runtime references | Audit classification / caveat |
|---|---:|---|---|
| `improved-wrath` | 5 | `BalanceDruidSimRunner.cs`, `ForeverBalanceDruidDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `genesis` | 5 | `ForeverBalanceDruidDps.cs` | Different Balance-vs-Restoration specialization effects; talent catalog is class-specific |
| `improved-moonfire` | 2 | `ForeverBalanceDruidDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `moonglow` | 3 | `ForeverBalanceDruidDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `natures-majesty` | 2 | `BalanceDruidSimRunner.cs`, `ForeverBalanceDruidDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `natures-reach` | 2 | `BalanceDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `natures-splendor` | 1 | `ForeverBalanceDruidDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `insect-swarm` | 1 | `BalanceAdaptiveRotation.cs`, `ForeverBalanceDruidDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `vengeance` | 5 | `BalanceDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `eclipse` | 3 | `BalanceDruidSimRunner.cs`, `ForeverBalanceDruidDps.cs` | Balance Wrath creates/Starfire consumes charges, same-source state |
| `moonfury` | 5 | `ForeverBalanceDruidDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `moonkin-form` | 1 | `BalanceDruidSimRunner.cs`, `ForeverBalanceDruidDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `natures-grace` | 1 | `BalanceDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `starlight-wrath` | 5 | — | Registered only / passive not executable in the existing solo model |
| `improved-starfire` | 5 | `BalanceDruidSimRunner.cs`, `ForeverBalanceDruidDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `naturalist` | 5 | `ForeverBalanceDruidDps.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `subtlety` | 3 | — | Registered only / passive not executable in the existing solo model |
| `reflection` | 3 | `BalanceDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `natures-focus` | 5 | — | Restoration Druid cast pushback reduction in scripted caster-pressure fixture |
| `furor` | 5 | — | Full rank preserves Energy in Bear (regen off) across form changes |

#### Feral Cat and Bear Druid (shared tree): 21 registered talent keys

Registry: `WarcraftSim.Data/Forever/Druid/ForeverFeralDruidSpellbook.cs`. Runtime-reference column is a *static execution trace*; a reference is not independent proof of its value or proc eligibility. Dashes signify no direct modeled combat effect found outside validator/catalog.

| Talent key | Cap | Direct runtime references | Audit classification / caveat |
|---|---:|---|---|
| `ferocity` | 5 | `ForeverFeralDruidSpellbook.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `shredding-attacks` | 3 | `ForeverFeralDruidSpellbook.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `savage-fury` | 2 | `ForeverFeralDruidSpellbook.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `sharpened-claws` | 2 | `FeralDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `predatory-strikes` | 3 | `FeralDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `furor` | 5 | `FeralDruidSimRunner.cs` | Full rank preserves Energy in Bear (regen off) across form changes |
| `shifting-power` | 1 | `ForeverFeralDruidSpellbook.cs` | Cat Energy return and cooldown; base Mana cost explicit |
| `primal-bite` | 1 | `FeralDruidSimRunner.cs`, `ForeverFeralDruidSpellbook.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `thick-hide` | 3 | `FeralDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `feral-instinct` | 3 | `ForeverFeralDruidSpellbook.cs` | Swipe damage modifier, not extra Bear threat |
| `improved-shifting-power` | 2 | `ForeverFeralDruidSpellbook.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `blood-frenzy` | 2 | `FeralDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `feral-swiftness` | 2 | `FeralDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `brutal-impact` | 2 | `ForeverFeralDruidSpellbook.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `rend-and-tear` | 3 | `FeralDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `natural-reaction` | 3 | `FeralDruidSimRunner.cs` | Bear avoidance incoming Rage/mitigation interaction, provisional coefficients |
| `leader-of-the-pack` | 1 | `FeralDruidSimRunner.cs`, `ForeverFeralDruidSpellbook.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `natural-instinct` | 2 | `FeralDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `berserk` | 1 | `FeralDruidSimRunner.cs`, `ForeverFeralDruidSpellbook.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `feral-charge` | 1 | `ForeverFeralDruidSpellbook.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `heart-of-the-wild` | 5 | `FeralDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |

#### Restoration Druid: 14 registered talent keys

Registry: `WarcraftSim.Data/Forever/Druid/ForeverRestorationDruid.cs`. Runtime-reference column is a *static execution trace*; a reference is not independent proof of its value or proc eligibility. Dashes signify no direct modeled combat effect found outside validator/catalog.

| Talent key | Cap | Direct runtime references | Audit classification / caveat |
|---|---:|---|---|
| `improved-rejuvenation` | 3 | `ForeverRestorationDruid.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `gift-of-nature` | 5 | `RestorationDruidSimRunner.cs`, `ForeverRestorationDruid.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `genesis` | 5 | `ForeverRestorationDruid.cs` | Different Balance-vs-Restoration specialization effects; talent catalog is class-specific |
| `living-spirit` | 3 | `RestorationDruidSimRunner.cs`, `ForeverRestorationDruid.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `improved-tranquility` | 2 | `RestorationDruidSimRunner.cs`, `ForeverRestorationDruid.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `swiftmend` | 1 | `ForeverRestorationDruid.cs` | Full source-owned HoT duration healing, without consuming HoT |
| `wild-growth` | 1 | `ForeverRestorationDruid.cs` | Requires 3/3 Living Spirit; periodic group heal is executable, radius and group movement omitted |
| `gift-of-the-earthmother` | 1 | `ForeverRestorationDruid.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `natures-swiftness` | 1 | `ForeverRestorationDruid.cs` | Requires 5/5 Naturalist; instant next eligible healing cast consumes proc |
| `naturalist` | 5 | `ForeverRestorationDruid.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `subtlety` | 3 | `RestorationDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `improved-regrowth` | 5 | `RestorationDruidSimRunner.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |
| `natures-focus` | 5 | `RestorationDruidSimRunner.cs` | Restoration Druid cast pushback reduction in scripted caster-pressure fixture |
| `tranquil-spirit` | 5 | `ForeverRestorationDruid.cs` | Runtime path connected; ranks/coefficients provisional except individually evidenced |


**Validation gate:** the branch's .NET 10 Release `-warnaserror` workflow must pass all inherited and new xUnit tests at the final SHA. The repository owner must repeat local build and test before PR #18 merges. Documented unverified server-only numbers are not a reason for indefinite blocking.


---

## Stage 4E — Final eight-class reconciliation and CP167 implementation inventory (October 28, 2026)

**Baseline main:** `d82378578695581dfcb3adb8935dffe418d37db2` (Stage 4D merged and owner-validated). **Branch/PR:** `cp166-stage4e-cross-class-reconciliation`, [PR #19](https://github.com/layoung3/WarcraftSim/pull/19). This section **supersedes outdated stage-status assertions** elsewhere in this historical matrix; it does not erase the original Stage 1–4D evidence trail. Do not merge PR #19 until local user confirmation. No CP167 implementation or equipment/raid architecture begins here.

### Eight-class audit reconciliation — status at the CP166 closure gate

These are **superseding present-tense statuses**; earlier text describing Stage 1/2 as awaiting tests, Stage 4A as partial, or Stage 4B/4C/4D as unmerged was correct *when written* but is stale after the user's locally validated merges. The Stage 4E branch starts from the fully merged Stage 4D SHA. No class becomes numerically verified merely because it runs.

| Class | Solo specializations and connected core mechanics reconciled | Executable evidence | Status and remaining boundary |
|---|---|---|---|
| Warrior | Arms/Fury/Protection; normalized MH/OH, Rage, stance, extra attacks, mitigation and 75%-capped Armor, tank fixed duration and threat/taunt | Stage 4A and prior Stage 1/2 class tests; Stage 4E default runner | **Audited — provisional:** level-60 Rage expectation, glancing, crushing and full boss AI still external |
| Rogue | Assassination/Combat/Subtlety; per-target CP, finisher hit gating, dual wield and poison hit/DoT, threat, energy cost and cooldowns | Stage 4A tests + Stage 4E combat-default serialization | **Audited — provisional:** poison resistance/snapshot and enemy movement; supported utility already implemented |
| Hunter | Marksmanship/Survival/Beast Mastery; distinct ranged and melee tables, haste retiming, DoTs, 19 pet family abilities, owner pet hit 1×/17/8, crit/expertise, death/revive, damage/threat | Stages 4B and 4E paired threat regressions | **Audited — provisional:** pet boss tables, AoE Volley, aspect support and full encounter geometry |
| Warlock | Affliction/Demonology/Destruction; hit-dependent DoTs, channels, per-school damage, Demonic Pact/Sacrifice/Havoc, seven demon roles, owner hit 1×/8/17, attribution/threat | Stage 4B pet spell/melee comparisons, Stage 4E Demonology default | **Audited — provisional:** missing Affliction health-drains and demon brand chain tracked in CP167 |
| Mage | Arcane/Fire/Frost; damage/periodic procs, Ignite roll conservation, Clearcasting, Frost threat and channel tick effects | Stage 4C tests + Stage 4E Fire default | **Audited — provisional:** burst cooldowns and AoE are P1; resistance and spell-pushback formula external |
| Priest | Discipline/Holy/Shadow; effective-vs-overheal, absorb, Divine Aegis, Penance instant+two ticks, Shadow DoTs, threat and source healing | Stage 3/4C healing and Shadow default tests; earlier paired healer tests | **Audited — provisional:** Inner Focus/Power Infusion missing P1, group radius & server coefficients |
| Shaman | Elemental/Enhancement/Restoration; spell/Rage-like weapon procs, dual wield, totem, Mana, healer crit/HoT, 5SR, Windfury no recursion, damage & heal threat | Stage 4D tests, Stage 4E Elemental+Enhancement and cross-healer Natural Grace comparisons | **Audited — provisional:** missing proper multi-Shock, totem ownership/imbue group and server ICDs |
| Druid | Balance/Feral Cat/Feral Bear/Restoration; form Mana/Energy/Rage, bleeds, HoTs, Eclipse, Bear mitigation, resources, projected health, threat | Stage 4D tests + Stage 4E Balance and Feral default | **Audited — provisional:** Innervate/Barkskin, target-switch CP behavior, exact HoT snapshots and fight geometry |

**Important historical contradictions resolved for future work:** The older Stage 4A matrix's “not audited / defects remaining” rows were a *checkpoint*, not the final post-merge Warrior/Rogue status. The Stage 4B pet-section's “must rerun local test” was fulfilled before its merge. Stage 4C's originally suspected Classic spell abilities are NOT automatically in CP167: inspect actual 70532 client entries; Stage 4D's proposed Tiger's Fury, Mangle, Lifebloom, Tree of Life, Earth Shield and Lava Lash are excluded until current independent client evidence supports them. The Mage/Priest and Shaman/Druid default configurations remain synthetic stat sheets, **not evidence-backed level-60 BiS profiles**. No legacy test suite was disabled during Stage 4E.

### Reconciliation: outcomes versus independently corroborated formulas

| Subsystem | Reconciled cross-class behavior and evidence | Evidence type | Remaining limits / classification |
|---|---|---|---|
| Physical attack tables | Warrior uses the ordered ruleset white/special table; Rogue and Hunter/Shaman/Feral use class roll adapters, with independent melee/ranged hit, dual-wield penalties, dodge/parry/glance qualifiers. Separate physical normalization and 75% Armor ceiling from Stage 2 remain intact. | Current source + earlier Stage 2–4D regressions | **Implemented/provisional:** level-63 glancing and crush percentages, skill/Expertise conversion, boss defense and armor curve remain server-side unknowns; no universal replacement table invented. |
| Spell hit / school resist | Mage, Warlock, Priest, Balance, Elemental and Hunter/Warlock companions retain distinct Fire/Shadow/Nature/Frost effect schools and caster hit modifiers. Stage 3 does one harmful DoT application hit; ticks never reroll misses; periodic critical eligibility is spell-specific. | Earlier Stage 3 and 4B client-proven application/hit rules | **Implemented/provisional:** discrete partial resist tables, dual school targeting, client-specific spell hit floor/cap not independently measured. |
| Extra swings / haste | Warrior/Rogue, Enhancement Windfury, Hunter autos and pets retain source/hand-specific extra attacks and nonrecursive proc gating. Farseer, Flurry and Rapid Fire re-time active swings, not only future starts. | Prior Stage 4A–4D executable regressions | Proc masks, overlapping haste multiplicative policy and latency exactness numerically provisional. |
| Resource costs / regeneration | Shared atomic multi-resource cost checks and post-delta resource event totals preserved. Mana (5SR); Rogue/Cat Energy; Warrior/Bear Rage; Hunter Focus; target-owned Rogue points and Shaman partial Maelstrom rules tested in prior phases. | Shared C# contracts and prior tests | Base level-60 Mana/Focus regen, periodic boundaries, Warrior/Bear server expected-health Rage curves provisional. Cat combo target-switch ownership is a **partial P1** dependency for CP167. |
| Healing / tanking | Priest, Restoration Druid/Shaman all use common effective-vs-overhealing accounting, live healing received effects and absorb-first ordering. Bear/Warrior tank trajectories continue for fixed durations even at projected death. Source attribution separate from mitigation; shields are not automatically healing. | Stage 3–4D deterministic regressions | Target geometry, encounter healing AI, defensive stacking under unverified server coefficients deferred. |
| Companions | Hunter 19 pet families and 7 Warlock demons reuse shared executor and companion actor setup, with owner-hit BONUS inheritance (1x, 17/8, 1x, 8/17), Expertise as avoidance, higher hunter melee/ranged critical, death/revive/sacrifice/pet resource contracts. | October 20 bug report, October 15 official notes, Stage 4B physical/spell tests | Unpublished boss-level partial resists, pet focus/attack tables and dynamic raid targeting remain provisional. |
| **Damage threat (Stage 4E correction)** | Warrior, Rogue, Beast Mastery, Warlock, Mage, Shadow Priest and Feral had damage threat. **Elemental, Enhancement, Balance, MM, Survival omitted all outgoing damage threat**. Those five now use `SoloDamageThreatResolver` through existing `ThreatManager` (one source-owned effective damage event/target; friendly and failed damage ignored). Elemental Overload copies explicitly do **not** generate threat; Balance Druid Subtlety and Shaman Natural Grace use source-specific reductions. | Current source, cross-spec comparisons, current [Druid Subtlety](https://theforeverera.com/en/talents/druid/subtlety/) and [Shaman Natural Grace](https://theforeverera.com/en/talents/shaman/natural-grace/) tooltips | **Corrected/verified executable**; encounter-specific enemy AI threat choice and real raid modifiers deferred. |
| **Restoration Shaman Natural Grace (Stage 4E correction)** | The healing-runner threat used **10%/rank** while the Elemental threat and 70532 talent use **5%/rank**. Corrected Resto Shaman to 5/10/15% reduction, with paired effective healing/threat outcome tests. | Source / independent [70532 Natural Grace](https://theforeverera.com/en/talents/shaman/natural-grace/) | **Corrected/verified for configured threat**; exact live boss healing threat coefficient still provisional. |

**Regression ledger:** `WarcraftSim.Tests/Consolidated/CP166_Stage4E_CrossClassReconciliation.cs` compares the five previously threat-silent real runners, Nature/Arcane threat rank deltas, suppresses Elemental Overload copies only, compares Elemental vs Restoration Natural Grace, tests physical/periodic friendly and avoided threat for eight source keys and runs all eight class defaults with owner/pet damage and JSON reconciliation. Prior cross-class tests (`CP166_Stage2_CombatMathematics`, `CP166_Stage3_CrossClassIntegration`, `CP166_Stage4A_WarriorRogueRegression`, `CP166_Stage4B_PetFormulaInheritance`, `CP166_Stage4C_MagePriestAudit`, `CP166_Stage4D_ShamanDruidAudit`) remain enabled. No broad speculative shared hit-table changes were made.

### CP167 — Missing or disconnected ability and talent inventory

This inventory is **derived from current supported code and [Forever Era build 1.60.1.70532 spellbooks](https://theforeverera.com/en/spells/)**, cross-checked against [current talent catalogs](https://theforeverera.com/en/talents/). It is the **remaining active work list**, not a historical Classic-era wish list. "Absent" means no executable definition in the current Shaman/Druid/Mage/etc spellbook and runner; "Partial" means a limited existing path must be extended. Utility auras and their numerical roles may require follow-up evidence. Scope is **level-60 solo throughput and fixed scripted encounters**; P0 denotes an entirely unusable specialization or core resource system and is empty because all eight class defaults execute.

| Class / specialization | Ability or talent | Actual implementation status | Current Forever evidence | Combat role / why absence matters | Dependencies / acceptance tests | Priority |
|---|---|---|---|---|---|---|
| Hunter / Marksmanship / Survival | **Aspect of the Hawk** | Absent spell and sustained RAP aura | [70532 client](https://theforeverera.com/en/spells/hunter/) | +120 RAP at level 60; all ranged attacks understate damage | Buff aura, owner ranged AP/stat refresh, exclusive aspect slot | **P1** |
| Hunter / Marksmanship / Survival | **Volley** | Absent channeled AoE | [70532 client](https://theforeverera.com/en/spells/hunter/) | 6s sustained ranged-area damage cannot be evaluated | Core channel target propagation, ranged hit and AoE accounting | **P1** |
| Warlock / Affliction / shared | **Drain Life** | Absent damage/heal channel | [70532 client](https://theforeverera.com/en/spells/warlock/) | Affliction health recovery and channel filler incomplete | Shared channel damage-to-heal transfer, event attribution, interruption | **P1** |
| Warlock / Affliction | **Siphon Life** | Absent talent-gated life-drain DoT | [70532 client](https://theforeverera.com/en/spells/warlock/) | High-uptime periodic damage and self-healing missing | Source-owned DoT, 3s ticks, dynamic healing transfer, hit dependency | **P1** |
| Warlock / Demonology / shared | **Searing Pain** | Absent high-threat direct Fire spell | [70532 client](https://theforeverera.com/en/spells/warlock/) | Missing the trigger for Demonic Brand and a direct casting option | Spellbook rank/cast/crit, threat modifier, priority opt-in | **P1** |
| Warlock / Demonology | **Demonic Brand** | Current talent not in executable subset | [70532 client](https://theforeverera.com/en/spells/warlock/) | Pet damage school/next-hit overrides and threat are missing | Searing Pain first, brand charges, pet effect school and source attribution | **P1** |
| Warlock / Destruction | **Shadowburn** | Absent instant Shadow finisher | [70532 client](https://theforeverera.com/en/spells/warlock/) | Material timed burst, execute/death shard interaction missing | Cooldown, damage/shard conditional, target death within 8s | **P1** |
| Warlock / Destruction / shared | **Rain of Fire** | Absent 8s channeled AoE | [70532 client](https://theforeverera.com/en/spells/warlock/) | Fire AoE encounter throughput unmodeled | Multi-enemy periodic channel, source hit/proc masks, interruption | **P1** |
| Mage / Arcane | **Presence of Mind** | Absent instant-next-cast cooldown | [70532 client](https://theforeverera.com/en/spells/mage/) | Major cast-time modifier needed by Arcane Power build | One-time cast snapshot, resource/cooldown bookkeeping | **P1** |
| Mage / Arcane | **Arcane Power** | Mentioned in model notes only; absent executable talent/ability | [70532 client](https://theforeverera.com/en/spells/mage/) | Major Arcane damage/Mana cooldown entirely omitted | Presence of Mind prerequisite, 15s aura, cast Mana multiplier | **P1** |
| Mage / Fire | **Combustion** | Mentioned in notes only; no executable action | [70532 client](https://theforeverera.com/en/spells/mage/) | Fire crit cooldown and proc consumption missing | Source aura, eligible direct Fire crit counting, expiration | **P1** |
| Mage / Fire | **Scorch** | Recognized in Incineration conditional only; no spell | [70532 client](https://theforeverera.com/en/spells/mage/) | Fast Fire filler and fire-talent proc source missing | Spell rank coefficients, incineration and damage/threat integration | **P1** |
| Mage / Fire / shared | **Flamestrike** | Absent direct plus periodic AoE | [70532 client](https://theforeverera.com/en/spells/mage/) | AoE damage and periodic application accounting missing | Multi-target impact dependency, ground effect ticking | **P1** |
| Mage / Frost | **Blizzard** | Absent channeled AoE | [70532 client](https://theforeverera.com/en/spells/mage/) | Frost AoE sustained damage cannot be simulated | 8s channel/periodic resolution, per-target event accounting | **P1** |
| Mage / Arcane / shared | **Arcane Explosion** | Absent instant Arcane AoE | [70532 client](https://theforeverera.com/en/spells/mage/) | Arcane multitarget option absent | Instant AoE effect resolution and scaling | **P1** |
| Mage / Frost | **Cold Snap** | Absent Frost cooldown reset action | [70532 client](https://theforeverera.com/en/spells/mage/) | Important frost cooldown reuse impossible | Existing AbilityState.ResetCooldown, Frost eligibility exceptions | **P1** |
| Priest / Discipline | **Inner Focus** | Current talent not registered as executable cooldown | [70532 client](https://theforeverera.com/en/spells/priest/) | Free next spell and non-periodic +25% crit modifier missing | Cast-start cost snapshot, non-periodic crit gate, cooldown | **P1** |
| Priest / Discipline | **Power Infusion** | Current talent not registered as executable cooldown | [70532 client](https://theforeverera.com/en/spells/priest/) | +20% spell damage/healing for 15s omitted | Ally/self aura and current damage/heal scaling; 3m CD | **P1** |
| Shaman / Elemental | **Earth Shock** | Only in Enhancement spellbook; absent Elemental | [70532 client](https://theforeverera.com/en/spells/shaman/) | Core instant direct Shock damage option missing | Shared Shock cooldown group and source-verified rank | **P1** |
| Shaman / Enhancement | **Flametongue Weapon** | Absent weapon imbue | [70532 client](https://theforeverera.com/en/spells/shaman/) | Per-hand speed-scaled additional Fire damage omitted | Shared per-hand imbue state and on-hit ownership | **P1** |
| Shaman / Enhancement / Bear-threat comparisons | **Rockbiter Weapon** | Absent weapon imbue | [70532 client](https://theforeverera.com/en/spells/shaman/) | Per-hand bonus AP and extra melee threat unmodeled | Hand-specific AP/attack threat and imbue exclusivity | **P1** |
| Shaman / Elemental / Enhancement | **Fire Nova** | Absent totem-centered fire AoE spell | [70532 client](https://theforeverera.com/en/spells/shaman/) | Current Forever new Fire Nova mechanic is unavailable | Existing Fire totem ownership, 0.143 SP coefficient, target list | **P1** |
| Shaman / Enhancement / shared | **Lightning Shield** | Absent reactive damage aura | [70532 client](https://theforeverera.com/en/spells/shaman/) | Long-duration reactive lightning charge/crit/proc absent | Incoming hit trigger, charges, source ownership | **P1** |
| Shaman / All | **Call of the Elements** | Absent configured multi-totem deployment | [70532 client](https://theforeverera.com/en/spells/shaman/) | Grouped buffs and Mana/cooldown cannot be evaluated | Totem slot ownership, shared buff state, independent per-totem cost | **P1** |
| Shaman / Enhancement / Warrior group | **Windfury Totem deployment** | Partial: static personal/Warrior opt-in proc, no deployable group aura | [70532 client](https://theforeverera.com/en/spells/shaman/) | Missing actual uptime, slot and party targeting; static toggle cannot compare full buffs | Existing nonrecursive proc reuse, air slot, eligibility, radius proxy | **P1** |
| Druid / Balance / Restoration | **Innervate** | Absent major Mana recovery cooldown | [70532 client](https://theforeverera.com/en/spells/druid/) | Long encounter Mana sustain and healer support understated | 5SR override, +400% Mana regeneration for 20s, self/ally target | **P1** |
| Druid / Cat / Bear / Restoration | **Barkskin** | Absent survival/interrupt suppression cooldown | [70532 client](https://theforeverera.com/en/spells/druid/) | Defensive mitigation and caster pushback interactions omitted | 20% damage mitigation aura, cooldown, form eligibility | **P1** |
| Druid / Feral Cat | **Combo-point target ownership on swaps** | Partial: solo target-wide combo-point state; multienemy swaps not demonstrated | [70532 client](https://theforeverera.com/en/spells/druid/) | Switching targets risks incorrect finisher ownership and damage in add encounters | Target-owned generator/finisher state, controlled 2-target events | **P1** |
| Warrior / All | **Berserker Rage** | Absent utility cooldown | [70532 client](https://theforeverera.com/en/spells/warrior/) | Encounter-limited fear immunity and Rage reaction | Control and Rage trigger event gate | **P2** |
| Hunter / Survival / shared | **Aspect of the Beast** | Absent aspect utility | [70532 client](https://theforeverera.com/en/spells/hunter/) | Melee AP aspect option and aspect exclusivity unmodeled | Exclusive aspect state and melee AP conversion | **P2** |
| Hunter / All | **Tranquilizing Shot** | Absent encounter dispel shot | [70532 client](https://theforeverera.com/en/spells/hunter/) | Boss Enrage dispel cannot be evaluated | Opt-in target enrage flag and remove-aura effect | **P2** |
| Warlock / All | **Curse of the Elements** | Absent support curse/debuff (current rank supported) | [70532 client](https://theforeverera.com/en/spells/warlock/) | Raid magic-school buff/debuff comparison incomplete | Curse exclusivity, school damage/resistance, 2nd actor buffs | **P2** |
| Priest / Holy / shared | **Holy Nova** | Absent AoE heal and damage | [70532 client](https://theforeverera.com/en/spells/priest/) | Group emergency heal alternative missing | Dual offensive/healing multi-target, threat policies | **P2** |
| Shaman / Elemental / shared | **Frost Shock** | Absent cold shock | [70532 client](https://theforeverera.com/en/spells/shaman/) | Situational slowing/secondary damage and cooldown unavailable | Shared Shock CD, Frost hit and control aura | **P2** |
| Shaman / Enhancement | **Frostbrand Weapon** | Absent Frost imbue | [70532 client](https://theforeverera.com/en/spells/shaman/) | Minor imbue damage and slow chance comparisons unavailable | Per-hand proc, Frost target aura, slow encounter deferral | **P2** |
| Shaman / All | **Totemic Recall** | Absent partial Mana refund ability | [70532 client](https://theforeverera.com/en/spells/shaman/) | Unrecoverable totem-mana/slot expenses on swaps | Slot-owned active totem costs, refund 25% of Mana cost | **P2** |
| Druid / All | **Rebirth** | Absent conditional battle resurrection | [70532 client](https://theforeverera.com/en/spells/druid/) | Group wipe recovery cannot be tested | Ally death/revive event, cooldown; solo throughput not affected | **P2** |
| Mage / Frost | **Frost Nova** | Absent PvE control effect | [70532 client](https://theforeverera.com/en/spells/mage/) | Primarily root/proc/encounter geometry instead of stationary boss DPS | Root immunity and range/CC events | **P3** |

### CP167 priority counts by class

| Class | P0 simulation-blocking | P1 high impact | P2 moderate | P3 deferred |
|---|---:|---:|---:|---:|
| Warrior | 0 | 0 | 1 | 0 |
| Rogue | 0 | 0 | 0 | 0 |
| Hunter | 0 | 2 | 2 | 0 |
| Warlock | 0 | 6 | 1 | 0 |
| Mage | 0 | 8 | 0 | 1 |
| Priest | 0 | 2 | 1 | 0 |
| Shaman | 0 | 7 | 3 | 0 |
| Druid | 0 | 3 | 1 | 0 |
| **Total** | **0** | **28** | **9** | **1** |

**Impact clarification:** Existing Rogue openers, utility and poisoned finishers, Warrior Taunt/Sunders/Shouts, Hunter Rapid Fire, Warlock Demonic Pact/Havoc, and existing Druid Wild Growth/Shifting Power are **not missing**. Zero P0 is a statement about current **runnable class rotations**, not proof every class is level-60 complete. Detailed P1 counts are **planned independent work units**, not time estimates or commitments to implement all at once.

### Evidence correction: do not implement removed or uncorroborated Classic-era abilities

Earlier Stage 4C/4D text mentioned possible Tiger's Fury, Lava Lash, Shamanistic Rage, Earth Shield, Mangle, Lifebloom, Tree of Life, Starfall, Survival Instincts, Living Bomb, Mage Shifting Power, Circle of Healing and Unstable Affliction. **The current 70532 spellbooks do not independently corroborate these names**; they are **excluded from actionable CP167 counts pending new contemporary client evidence**, not scheduled based on older expansions. [Tiger's Fury was explicitly removed in Forever](https://theforeverera.com/en/spells/druid/tigers-fury/) and replaced by the already modeled Druid Shifting Power; it should **not** be implemented. The existence of a concept in a historical Stage 4 audit does not overrule the newer client catalog.

### Dependency-ordered CP167 implementation groups (not begun)

1. **CP167-A — Shared reusable contracts and tests, minimal scope:** controlled cast-time-next-spell, instant/charged on-use buffs, source-owned curse/debuff exclusivity, per-hand imbue and optional totem slot lifetimes, portable damage-to-heal channel effects and configurable multi-target area definition. Reuse existing `AbilityExecutor`, `AuraManager`, `AutoAttackProcessor`, `ThreatManager`, `HealerPriorityProcessor` and existing pet system. Do not build raid/AI architecture.
2. **CP167-B — Missing primary solo modifiers and single-target mechanics:** Hunter Aspect Hawk; Elemental Earth Shock + shared Shock cooldown; Shaman Lightning Shield / Rockbiter / Flametongue, group Windfury Totem state; Warlock Drain Life + Siphon Life, Mage Scorch, Druid Innervate. Include zero-hit, CD, source-specific buff, proc/crit and resource conservation tests.
3. **CP167-C — Major combat cooldowns and sustained mitigation:** Mage Presence of Mind then Arcane Power (talent prerequisite order), Combustion, Cold Snap; Priest Inner Focus and Power Infusion; Druid Barkskin. Execute controlled durations, overlapping proc and mana-cost tests, no rotation optimization.
4. **CP167-D — Area, totem and multi-target effects:** Hunter Volley; Warlock Rain of Fire; Mage Flamestrike, Blizzard, Arcane Explosion; Shaman Fire Nova and Call of the Elements. Use fixed explicit targets and stage-3 target-specific dependencies rather than dynamic movement or a raid engine.
5. **CP167-E — Warlock companion trigger chain and extra finishers:** Searing Pain **before** Demonic Brand (charges/pet attack school/threat), Shadowburn; preserve demon inheritance, and ensure no extra pet engine. Druid Cat target-owned combo points can be validated here with 2-target controlled runs.
6. **CP167-F — P2 and opt-in encounter utility:** Shaman Frost Shock, Frostbrand, Totemic Recall; Hunter Aspect Beast and Tranquilizing Shot; Priest Holy Nova; Druid Rebirth; Warrior Berserker Rage; Warlock Curse Elements; Frost Nova controls. Defer P3 movement/CC until encounter-state support is explicitly required.

**Ordering rationale:** shared source-owned resource/periodic/target semantics before class abilities; high-impact missing primary throughput before timed cooldowns and AoE; standalone pet trigger chain only after Searing Pain; P2 positioning and raid group dependencies last. Do **not** start CP167 in this PR. Do not begin gear, enchant/consumable databases, BiS, rotation optimization or raid/frontend rework before ability completeness.


### CP167 continuous PR #20 execution ledger (in progress, not release-certified)

All CP167 changes remain on `cp167-a-direct-spells-shared-shock-cooldown` / draft [PR #20](https://github.com/layoung3/WarcraftSim/pull/20); do not merge until the user approves after local validation. Coverage status means **an executable path exists**, not that every client tooltip, target interaction, and throughput coefficient is verified. The earlier 38-row inventory is retained unchanged as the baseline. Discoveries beyond those rows will be appended during the required eight-class final scan.

| Original P1 | Executable status on PR #20 | Tests and remaining limitation |
|---|---|---|
| Mage Scorch | **Implemented, provisional numeric coefficient**: rank-7 current Fire damage/cost, opt-in rotation, existing Incineration and Heating Up integration | `CP167_A_DirectSpells`; source checks and runtime damage accounting; exact spell scaling remains provisional |
| Elemental Earth Shock | **Implemented, provisional numeric coefficient**: Nature direct hit, cost/talent multipliers, shared Flame Shock cooldown | `CP167_A_DirectSpells`; spellbook and runtime; high-threat modifier and PvE interrupt behavior not invented |
| Mage Cold Snap | **Implemented, initial limited Frost scope**: talent-gated 10-minute cooldown, existing Frost Blast/Cone of Cold cooldown reset | `CP167_B_MageCooldowns`; only current cooldown-bearing Frost actions are targeted; future Frost ability registrations require expansion |
| Mage Combustion | **Implemented, provisional exact proc policy**: talent-gated on-use buff, direct landed Fire hits grant stacking crit benefit, stops on third direct critical hit | `CP167_B_MageCooldowns`; tests require three critical Fire strikes, aura consumption, accounting. Client proc details should be rechecked before final certification |

| Druid Innervate | **Implemented, provisional level-60 base Mana**: trainer cooldown with 5%-of-base-Mana cost, 20s source-owned +400% spirit regeneration and five-second-rule override in Balance/Restoration | `CP167_C_DruidCooldowns`; comparative Mana and healer execution; ally-targeting and Feral integration remain for final review |
| Druid Barkskin | **Implemented, partial cross-form coverage**: 60s cooldown, 15s physical-only damage mitigation and prevented hard-cast pushback; Balance/Restoration solo encounters | `CP167_C_DruidCooldowns`; scripted hit comparison; Feral form eligibility and all scripted damage classes still require review |
| Warlock Drain Life | **Implemented, provisional spell-power coefficient**: 5s Shadow channel, 1s ticks; healing derives from resolved effective damage | `CP167_D_WarlockLifeAndFinishers`; channel damage/heal attribution and hit dependencies |
| Warlock Siphon Life | **Implemented, provisional spell-power coefficient**: talent-gated 30s Shadow DoT, 3s ticks with source-owned healing | `CP167_D_WarlockLifeAndFinishers`; talent gate and resolved-damage healing; refresh/snapshot and fully verified numeric scaling remain open |
| Warlock Searing Pain | **Implemented, provisional threat multiplier**: current rank-6 1.5s Fire spell, direct damage/threat; opt-in custom priority | `CP167_D_WarlockLifeAndFinishers`; positive threat differential verified; 2x modifier inherited from documented legacy behavior, not independent Forever proof; Demonic Brand remains absent |
| Warlock Shadowburn | **Implemented, provisional shard inventory model**: 15s instant Shadow finisher, talent gate, finite shard consumption and conditional 8s kill refund for configured qualifying enemies | `CP167_D_WarlockLifeAndFinishers`; shard conservation and damage; delayed-death refund and target qualification need additional acceptance tests |

| Mage Arcane Explosion | **Implemented, partial numeric/geometry coverage**: rank-6 390 Mana, 232–252 base Arcane damage, instant, per-enemy hits for a fixed configured enemy set | `CP167_E_MageAoE`; 3-target damage and reporting; 10-yard radius and SP scaling unverified/unmodeled |
| Mage Blizzard | **Implemented, partial numeric/geometry coverage**: rank-6 1400 Mana before Frost Channeling, 1168 base Frost damage over an 8-second channel, 1s damage ticks, per-target hits | `CP167_E_MageAoE`; channel event accounting and full suite; ground targeting, slow and SP coefficient remain pending |

| Mage Presence of Mind | **Implemented with opt-in rotation:** 180s cooldown, aura until an eligible under-10s hard cast starts; consumed once by the next eligible Mage damaging spell. Same cast-start timing pipeline as Heating Up; no added engine architecture | `CP167_F_MageArcaneCooldowns`; exact one instant hard cast and subsequent normal cast; utility non-damage Mage casts not yet covered |
| Mage Arcane Power | **Implemented with current 70532 damage and cost rules:** 180s CD, 15s +30% damage / +30% Mana, composes with Arcane Blast stack costs, Missile Barrage and Clearcasting free casts | `CP167_F_MageArcaneCooldowns`; comparative damage/cost, expiration and Clearcasting. Current automatic rotation remains opt-in, as CP166 baselines must not change |
| Priest Inner Focus | **Implemented, talent-gated:** 180s cooldown; next completed Mana spell costs zero and gets +25 percentage points of crit if a direct crit-capable effect; cancelled cast retains pending buff | `CP167_G_PriestMajorCooldowns`; healer and cross-tree Shadow execution; delivery-specific channel/periodic crit policy retained |
| Priest Power Infusion | **Implemented, self-target PvE approximation:** 180s cooldown, 15s +20% direct/periodic spell damage and normal healing-roll scaling; current 20%-base-Mana cost (base Mana provisional) | `CP167_G_PriestMajorCooldowns`; Discipline Penance prerequisite; other ally recipients and separately triggered group-heal effects remain partial |
| Warlock Demonic Brand | **Implemented, target/owner gated and numeric provisional:** Searing Pain must land; 10s target aura, rank 1/2/3 = 2/4/6 pet triggers; independent damage procs via existing executor. Searing Pain threat reduction 17/33/50%; tanking Voidwalker Shadow proc high threat, non-tanking Imp Fire proc ordinary threat | `CP167_H_WarlockBrandAndShadowburn`; rank, miss, school and charge tests. Exact pet-school mapping and 65–68 +7.8% SP proc need in-game confirmation |

**Next verification:** Warlock Shadowburn's kill-window regression was rewritten to schedule its first hit and a later death without permitting a second cast to refresh the window. Extra Brand threat follows the official rule that tanking pets, not Imp-style non-tank pets, receive the Shadow threat bonus.

**Additional confirmed client coverage gaps discovered beyond original matrix:** Warlock **Decimation** (Soul Fire cooldown and conditional follow-up on Shadow Bolt / Searing Pain under 35% health) is currently absent from the executable talent subset; Mage baseline **Magic Attunement** is current but not in the modeled baseline ability catalog. These require standalone scope, additional current-client evidence and execution tests, not historical spell imports.

### CP167 October 28 continuation — Hunter, imbues, shields, totems and AoE

**Validation:** The cumulative Release `-warnaserror` build and complete xUnit suite passed on code SHA `2069f0baabf22387033639a5fa26f5f788a8fe3a` (CI run `40713115474`, **1,784 passed, zero warnings/errors**). This is GitHub Actions validation, not owner local acceptance of this checkpoint.

| Original item | Executable status | Current evidence, regression, remaining boundary |
|---|---|---|
| Hunter Aspect of the Hawk | **Partial P1**: Rank 7, 120 Mana, source-owned exclusive Hawk aura grants +120 live ranged AP; MM Auto Shot/Aimed/Multi/Sniper dynamically reflect it | 70532 Hawk tooltip; `CP167_I_HunterAspectVolley`; other Hunter runners, other-aspect switching and pet inheritance require separate work |
| Hunter Volley | **Partial P1**: 490 Mana, 6s channel, six 112 Arcane hits per eligible enemy, Barrage 3/7/10% applied | 70532 Volley/Barrage; `CP167_I_HunterAspectVolley`; explicit target-set approximation on MM only, radius/other specializations pending |
| Mage Flamestrike | **Partial P1**: 990 Mana, 3s, direct 417 Fire plus 83 every 2s for 8s per target; per-target direct-hit application dependency; Improved Flamestrike 5% crit per rank | 70532; `CP167_J_AreaSpells`; global one-ground-effect exclusivity and SP coefficients not validated |
| Warlock Rain of Fire | **Partial P1**: 1,185 Mana, 8s channel with 110 Fire per second across explicit enemies | 70532; `CP167_J_AreaSpells`; ground effect geometry and precise coefficients unresolved |
| Shaman Flametongue Weapon | **Partial P1**: 155 Mana, 60-min per-hand aura, live auto-attack Fire proc via shared executor; Elemental Weapons 5/10/15% damage | 70532; `CP167_K_ShamanWeaponImbues`; exact weapon-speed mapping provisional, totem interaction/crit policy pending |
| Shaman Rockbiter Weapon | **Partial P1**: 150 Mana, per-hand +554 AP with 7/13/20% Elemental Weapons talent; MH and OH damage isolated | 70532; `CP167_K_ShamanWeaponImbues`; extra threat configurable but defaults to no speculative bonus until actual coefficient confirmed |
| Shaman Lightning Shield | **Partial P1**: 370 Mana, 600s aura, three reactive charges of 178 Nature; timed incoming hits in Enhancement and Elemental | 70532; `CP167_L_ShamanLightningShield`; configurable provisional server ICD, spell-power coefficient and Restoration integration pending |
| Shaman Fire Nova | **Partial P1**: 375 Mana, 10s cooldown, 239 base Fire with provisional .143 SP coefficient and explicit target set; requires source-owned active Searing Fire totem | 70532; `CP167_M_ShamanFireNovaAndWindfury`; shared four-slot lifetime/geometry and other Fire totems not yet modeled |
| Shaman Windfury Totem deployment | **Partial P1**: optional real 250 Mana deployment, 1s GCD, 300s owner aura; existing nonrecursive 20%-chance 246-AP extra swing gated by uptime | 70532; `CP167_M_ShamanFireNovaAndWindfury`; old static option preserved, party/raid radius, shared Air slot and replacement pending |
| Shaman Frostbrand Weapon | **Partial P2 newly executable**: per-hand 160 Mana, 158 Frost proc and 8s slow aura; proc chance configurable, no enemy movement modeling | 70532; `CP167_K_ShamanWeaponImbues`; proc chance/slow behavior within boss AI provisional |

**Original P1 progress:** **26/28 have at least one executable path** on this draft branch, including partial implementations and numeric approximations. Two P1 entries remain without a complete executable path: **Call of the Elements** (requires configured four-slot totems, proper aggregate Mana and ownership) and **Feral Cat target-owned combo points on switches**. Neither may be marked complete or bypassed. The original 38-row inventory stays the baseline; these statuses supersede only the corresponding rows.

**Additional/current abilities discovered beyond the original matrix:** Hunter **Deadly Aspects**, Shaman **Call of the Ancestors**, **Call of the Spirits** and **Totemic Projection**, in addition to earlier Warlock **Decimation** and Mage **Magic Attunement**. Verify their class/talent tiers and executable PvE relevance rather than importing old expansion counterparts.

**Known incomplete cross-spec functionality:** Hawk/Volley were first integrated in Marksmanship only; Enhancement imbues/shield and Elemental shield/nova do not imply all three Shaman roles have full shared spellbook coverage. Frostbrand proc chance, Searing Fire totem slot proxy, Flamestrike ground exclusivity, pet-stat propagation, Stormstrike/imbue cross-procs, and Shaman Rockbiter threat remain open. The full eight-class spell/talent-to-executable reconciliation, all class defaults and any newly discovered high-impact omissions remain required before CP167 completion.

### CP167 continuation — four-slot lifecycle, target-owned combos and cross-spec Hunter (2026-10-28)

**Code and test changes are on the same draft PR #20, unmerged.** The 38 original CP167 rows remain an immutable historical input; this ledger is a newer implementation-status overlay.

| Workstream | Current implementation | Remaining acceptance gates |
|---|---|---|
| Four-slot Shaman totems (P1) | **Executable, partial:** owner-scoped Earth/Fire/Water/Air slot ledger, actual Mana-costed individual abilities, replacement and expiration, optional Windfury aura proc gating, source Fire totem for Fire Nova, 25% of live active totem costs returned by Totemic Recall | Restoration still owns separate Water slot logic; actual totem destruction/radius/secondary party owner requires review. Totem costs/ranks other than checked items need current client verification |
| Call of the Elements (P1) | **Executable, partial:** 3-second cast; selected one-per-slot effects applied through the normal AbilityExecutor, one combined Mana cost, now current 1.5s GCD; independent loadouts for Call of Ancestors (level 30) and Spirits (40) | Verify alternate setup combinations, cancellation, totem group ownership, all class roles, overwriting effects without accidental buff stacking, real Totem Bar behavior |
| Feral Cat combo points (P1) | **Executable, partial:** per-target CP dictionary, explicit enemy-switch schedule, target-specific Rip hit/snapshot, return to previous target, target death clears target points; failed finisher restores target CP and refunded **80% Energy (numeric provisional)** | Complete in-client Miss Power discount and snapshot magnitude verification; multiple simultaneous Rip/finisher edge cases and definitive target despawn handling |
| Hunter Hawk/Beast/Volley across 3 specs | **Executable, partial:** MM, BM and Survival register the shared abilities. Dynamic ranged AP on BM/MM physical shots; Survival separates melee/ranged live AP and remains melee-first. One player aspect suppresses the other | Verify all baseline shots/specials and pet owner inheritance, complete Deadly Aspects Beast melee haste, pets do not inherit temporary player-only AP, and current client pet-specific scaling |
| Stoneskin/Strength of Earth | **Executable, partial:** mutually exclusive owner Earth slot, +106 live Shaman AP from Strength, Stoneskin -30 on scripted physical hits; live per-hand Rockbiter stats refreshed when Strength state changes | Party coverage and physical vs magic incoming rules, real 30yd range, all tooltips and other Earth totems |
| Additional Shaman calls | **Executable, partial:** Ancestors and Spirits have independently configured totem lists using the same lifecycle and combined-Mana semantics | Current trainer levels verified as 30/40, contrary to early official blog's swapped text; Restoration party deployments remain open |

**Counts (strict, non-overstated):**

- Original P1 entries with **at least one executable code path:** **28 / 28**. This includes the two formerly missing functional paths (Call of the Elements and Feral target-owned combo points).
- Original P1 items **certified fully mechanically complete against current client spellbook/talent data:** **0 signed off** in this initial eight-class completeness certification. This is a *review/certification status*, not a claim that zero mechanics work.
- Original P1 items **still awaiting some provisional-value, partial-interaction or full-certification review:** **28 / 28** under the expanded acceptance gate. This is not equivalent to 28 known functional defects.
- **New current-client omissions tracked beyond the original 38:** at least **6** tracked items/families in `docs/audits/CP167-class-completeness-inventory.md`. Of these, Decimation, BM Beast-path Deadly Aspects, and Ancestors/Spirits now have initial executable paths; Magic Attunement's dependent effects and Totemic Projection remain encounter-dependent or missing.
- **Independently implementable known next-pass items:** Exact Feral finisher energy miss refund, Mage/Priest ally support, Restoration and Hunter multi-actor/raid group effects, Decimation cast-trigger edge cases, and class-specific talent gaps from the full client comparison.
- **Encounter/server-data limited:** ranges, geometry, target movement, dynamic totem placement and destruction, ground spell position, unverified proc probabilities, and some class-specific damage/heal coefficients. Do not fill these with invented effects or claim certified accuracy.

**CP167 subsequent executable reconciliation (Oct 28):** Warlock Decimation now has rank-specific Soul Fire cooldowns, a genuine shard cost and an execute-window cast/shard/damage processor (`CP167_S_WarlockDecimation`). Hunter Beast Mastery Deadly Aspects now supports owner Beast-mode melee autos and actual swing haste/rescheduling (`CP167_T_HunterDeadlyAspects`), without allowing pet swings to trigger the owner talent. Restoration Shaman has **opt-in** shared Earth/Fire/Water/Air slots, all three programmed Calls, Totemic Recall and existing Water healing/Mana pulses using the same `ShamanTotemLifecycleProcessor` (`CP167_U_RestorationShamanTotems`). Current 70532 client ranks Call of the Ancestors at **30** and Call of the Spirits at **40**; these conflict with the October 7 news prose. Sources and remaining limitations are recorded in `CP167-class-completeness-inventory.md`. **No original P1 has been elevated from executable/partial to fully client-certified solely by these focused tests.**

**First-pass eight-class runtime-path inventory:** [CP167 class completeness worklist](CP167-class-completeness-inventory.md), covering Warrior (Arms/Fury/Protection), Rogue (Assassination/Combat/Subtlety), Hunter (MM/Survival/BM), Warlock (Affliction/Demonology/Destruction), Mage (Arcane/Fire/Frost), Priest (Disc/Holy/Shadow), Shaman (Elemental/Enhancement/Restoration), and Druid (Balance/Feral Cat/Feral Bear/Restoration). All 25 specs remain **in audit** until a side-by-side full level-60 current spellbook and every rank/talent comparison is completed.

**Validation:** CP166 regressions and earlier CP167 outcome suites remain enabled. Latest CI/build/test evidence must be read from final head, and owner's local validation must follow; no new branch or PR, no merge without explicit authorization.

### CP167 group 1 — Mage/Warlock exhaustive source inventory continuation (October 28, 2026)

- Original P1 inventory remains **28/28 with at least one executable path**, but **0/28 separately signed off as fully client-certified** under the expanded final gate. This is an evidence/certification measure, not a claim that 28 mechanics are dysfunctional.
- Group-1 source inventory in `CP167-class-completeness-inventory.md`: all **106 known current Mage/Warlock talents** and **131 Mage/Warlock class-and-demon spell entries** enumerated with highest available rank, source link and initial runtime references. These are code-path classifications only; **0/6 Mage/Warlock specializations independently certified**.
- Independently implemented this batch: **Mage Master of Elements, Improved Scorch, Improved Frostbolt/Frost Blast, Blast Wave, Pyroblast Rank 8 DoT, Heating Up + Presence of Mind ordering, Mage talent spellbook gating; Warlock Improved Bane of Agony, Nightfall, Conflagrate talent gating, Death Coil, Bane of Doom and owner Bane exclusions**.
- Explicit unverified mechanics: exact Pyroblast periodic power coefficient, Nightfall aura lifetime, current Bane of Doom Doomguard summon chance, AoE geometry, cross-owner Scorch and raid-wide effects, additional rank/crit/school masks, talents absent from executable dictionaries and the other 19 spec-group comparisons.
- **Client evidence baseline:** Mage talent 70532 / spellbook 70658, Warlock talent 70532 / spellbook 70630. Source citation links, per-spec gaps and tests are in the Group 1 appendix. No entire spec should be marked certified from these incremental results. Full Release CI and owner local tests are still required at each final-head checkpoint.

### CP166 completion and formula watch handoff

No **confirmed high-severity independently correctable shared defect remains identified** within the reconciled supported solo scope after this correction pass. This does not claim universal mathematical equivalence among distinct class-specific hit tables. Formula Watch remains authoritative for **unverified** level-60 spell partial resists, physical miss/glance and boss armor curve, Rage normalization, pet attack-table level differences, snapshots and proc masks. Zero P0 and remaining P1 entries make a **CP167 ability-completeness phase necessary** before trusting cross-spec DPS comparisons or gear optimizations. No Stage 1–4D effects were intentionally altered except explicit cross-class threat consistency and current Natural Grace rank correction.

**Release gate:** .NET 10 Release `-warnaserror` build and complete xUnit suite on final branch head, local build and full tests by owner, then merge PR #19. Leave unmerged until owner confirms. No CP167 work starts automatically.

### CP167 Group 1 continued — Curse of the Elements and Burning Soul

- Warlock level-60 rank-4 Curse of the Elements: 200 Mana, 300-second target-owned aura, +10% magical damage in live Warlock/pet damage resolver, with deterministic direct and periodic tests in `CP167_AD_CurseAndFireThreat`; -75 resist component, cross-class raid buff, dispels and curse exclusivity remain out of scope/unfinished.
- Mage Burning Soul 1–3: +rank support and tier gate, -10/20/30% Fire threat (including Frostfire Bolt) wired to live threat generation, outcome test added. Fire pushback resistance remains missing; do not call talent certified.
- Existing draft PR #20 only; no merge or class-group certification. Prior 28 P1 gates remain intact. Final-head CI required.


### CP167 continuation — Mage/Warlock solo PvE executable certification expansion (2026-10-28)

**Scope**: existing draft PR #20 only; no new class group, equipment, raid AI, geometry or merge. Retains 106 talent/131 spell registry and original 28-P1 audit rows. These are **outcome-tested features, not six-class certification**; all six specialization gates stay open pending the remaining independenty correctable list in `CP167-class-completeness-inventory.md`.

| Mechanic / source build 70532 | Executable path | Regression / confidence and next gate |
|---|---|---|
| Mage Improved Fire Blast 2 ranks | `ForeverMageDpsSpellbook` reduced cooldown 8→7→6s | `CP167_AE_ChannelsAndTalents`; rank cap verified; target geometry/range open |
| Mage Burning Soul 3 ranks | `MageDamageThreatResolver` Fire threat -10/-20/-30%, shared `CastDamagePushbackProcessor` Fire hardcasts with 23/47/70% resist on real incoming damage | `CP167_AG_InboundDefense`, `CP167_AD_CurseAndFireThreat`; requires explicit incoming damage, no invented boss AI |
| Mage Ice Barrier rank4 | Talent-gated `ForeverMageDpsSpellbook` absorb 811 for 60s/480 Mana/30s CD via `AbsorbManager`; Cold Snap reset | `CP167_AF_IceBarrier`, `CP167_AG_InboundDefense`; full absorb event reporting; no magical coefficient speculation |
| Mage Ice Block rank1 | 15 Mana, 10s self immunity, 300s CD, forbids Mage spell casts during aura, Cold Snap reset; explicit incoming attack is immune without consuming shields | `CP167_AK_IceBlock`; broader debuff dispel, pet/raid visibility unverified |
| Mage Magic Absorption 2 ranks | Live full magical resist 1/2% maximum Mana refund and 1s ICD in `MageMagicAbsorptionProcessor` | `CP167_AI_MagicAbsorption`; magical resistance roll adjustment still missing; avoids made-up resist odds |
| Warlock Drain Soul rank4 | Shared channel 290 Mana/15s/5 Shadow ticks (84 base each), nontrivial kill guaranteed shard or explicitly configured nonlethal chance | `WarlockDrainSoulShardProcessor`, `CP167_AE_ChannelsAndTalents`; server proc probability unknown/zero by default |
| Warlock Hellfire rank3 | Shared channel 1300 Mana, 206 Fire/sec to caster and eligible enemies, event-accounted | `CP167_AE_ChannelsAndTalents`, `CP167_AL_SelfDamageAccounting`; self-damage counted taken, not offensive damage; radius unresolved |
| Warlock Soul Siphon 3 ranks | Owner-target live Affliction auras, +4/8/12% per up to three other effects for Drain Life/Drain Soul/Wrack | `CP167_AE_ChannelsAndTalents`; further target switch proc audit needed |
| Warlock Improved Voidwalker 3 ranks | Shared pet `WarlockDemonCompanion` Torment/Suffering threat 10/20/30% higher | `CP167_AH_Voidwalker`; Consume Shadows/Sacrifice still missing |
| Warlock Fire and Brimstone 3 ranks | Conflagrate direct crit +8/17/25%, owned talent prerequisite | `CP167_AG_InboundDefense`; encounter-specific targets open |
| Warlock Molten Skin 5 ranks | Explicit incoming damage and Hellfire self-harm -2% per talent rank | `CP167_AG_InboundDefense`; magical resistance/mitigation still provisional |
| Warlock Aftermath 5 ranks | Immolate only initial hit +10/20/30/40/50%, DoT unchanged | `CP167_AJ_Aftermath`; Conflagrate daze on immune boss not a DPS multiplier |
| Warlock Amplify Curse rank1 | 3min cooldown 30s self aura; next landed Bane of Agony gains source-owned 50% damage tick target aura, buff consumed; ordinary Bane refresh clears amplification | `WarlockAmplifyCurseProcessor`, `CP167_AM_AmplifyCurse`; Curse of Weakness/Exhaustion effects remain encounter-dependent |

No complete signoff is asserted. Remaining high-impact independent tasks include Mage Arcane Shielding, Mana Shield, Amplify/Dampen Magic, Frost improved Blizzard/Impact controllable targets; Warlock Fel Concentration, Intensity, Fel Domination, Master Summoner, Demonic Aegis, Soul Harvest, Health Funnel, curse exclusivity, seven-demon comprehensive verification, and relevant aura/mitigation cross-class interactions. Do not infer completion merely from a catalog name or a generic AuraApplied event.


#### CP167 Group 1 added full-resist, armor and Soul Harvest follow-up

- **Mage Armor:** Current Forever 70532 rank 3 costs 490 Mana; executes 30-minute owner buff with +15 all magical school resistance; after application FiveSecondManaRegenerationProcessor actually retains half ordinary Spirit regeneration within the five-second rule. `MageArmorResistanceProcessor` applies Arcane Shielding rank1/2 bonus +25/+50% to existing resistance stat without inventing a resistance hit table. `CP167_AP_MageArmor` tests resource, buff and live stats. **Partial**: Arcane Shielding Mana Shield drain and numeric resist mitigation not certified.
- **Warlock Soul Harvest:** level-60 rank1/2 grants 10s +50/+100% Spirit regen on an eligible Drain Soul kill, permitting 50/100% casting regeneration. Reuses shared five-second Mana processor through non-static optional multiplier delegates; `WarlockSoulHarvestProcessor` and `CP167_AN_SoulHarvest` test owner AuraApplied and ending Mana. Trigger never occurs from trivial target. **Mechanically implemented for configured eligible kills; no invented server proc chance.**
- **Seven demons**: `CP167_AO_SevenDemonSmoke` tests each existing companion family using the existing actor engine and verifies configured Infernal/Doomguard expiration. This is a smoke verification, **not complete per-family source/rank/talent certification**.
- **Current CI**: final-head validation will be recorded on PR #20. Every source-backed completion label remains scope-limited; six class specs not yet certified and PR remains draft.


### CP167 DPS-only checkpoint — rank/proc, channels and owner demon summons

**Revised gate:** 19 DPS specializations require mechanical audit/certification; the six tank/healer role-specific certifications are explicitly deferred, not deleted. Mage Arcane/Fire/Frost and Warlock Affliction/Demonology/Destruction remain in scope on existing draft PR #20. Existing CP166 and tank/heal tests remain unchanged.

| Current Forever 70532 mechanic | Executor / regression evidence | Mechanical/numerical status |
|---|---|---|
| Mage Fingers of Frost + Shatter | `MageSimRunner`, `ForeverMageTalentBuild`, `CP167_AQ_DpsProcChannelPushback`, `CP167_AR_FrostDpsOwnership` | **Corrected:** FoF fixed 15% chance regardless of rank, grants 1/2 charges for 15s, next eligible spell consumes; Shatter 3 ranks +17/33/50% crit on spells treating target Frozen; actual default Frost talent preset corrected. External/other Chill procs need final comparison. |
| Improved Blizzard + Permafrost | Target-owned aura and live FoF trigger on resolved Blizzard channel tick; `CP167_AR_FrostDpsOwnership` | **Executable / encounter-limited:** 15/25/40% slow 1.5s, extension 11/22/33%; no fabricated raid-boss movement benefits. |
| Warlock Fel Concentration and Intensity | `WarlockIncomingCastInterruptionProcessor`, shared AbilityExecutor pushback/cancellation and explicit incoming damage; `CP167_AQ_DpsProcChannelPushback` | **Executable / source-backed rank chance:** 23/47/70% protection, channels actually stop remaining ticks on failed protection and hardcasts incur pushback; no fake boss attack schedule. |
| Master Summoner + Fel Domination | `ForeverWarlockSummonSpellbook`, `WarlockReplacementSummonProcessor`, unchanged companion actor, `CP167_AS_DemonSummonTiming` | **Executable opt-in:** owner cast, casting lock, Mana + Soul Shard spending, five permanent demons, 300s Fel Domination cooldown, pet engagement after successful cast; base Mana numerical input provisional; permanent summon is not confused with Infernal/Doomguard temporary summon mechanics. |
| Impact and Pyroclasm | 70532 talent descriptions | **Encounter-control only / deferred from solo-boss DPS signoff**: 2/3-second stuns have no verified damage bonus, so no phantom DPS multiplier. Add-control mechanics may be implemented when supported target immunity/interrupt conditions exist. |

Certification **0/6 Mage/Warlock DPS specs and 0/19 total DPS specs signed off**; this is a validated functional correction, not a final class certificate. The established six-step post-DPS roadmap is recorded in `CP167-class-completeness-inventory.md`: item cache/database, modifiers, class-based solo UI, Phase 1 BiS optimization, role-specific tank/heal completion, then guild/raid/logs.


### CP167 Mage/Warlock DPS continuation — landed procs, pet sustain and Affliction rank evidence

**Source snapshot:** WoW Forever client 1.60.1.70532 (The Forever Era), retaining previously audited CP166 shared mechanics. These are DPS-focused fixes, not 6/6 specialization certificates or external server-coefficient calibration.

| Subsystem | Executable path and observed outcome | Tests; limits |
|---|---|---|
| Fire Mage Heating Up / Pyroblast | Resolved landed Fire Blast grants the extra stack; a crit grants another. A miss never gains a stack. 20s charge lifetime and actual Pyro cast time preserved; no phantom proc on AbilityCastCompleted. | `MageSimRunner`, `CP167_AT_HeatingUpLandedDamage`. Future exact same-timestamp PoM and other spell overlap combinations still under review. |
| Demonology Health Funnel / survivability | Real 10s channel (153 HP restored/sec, 79 caster HP/sec rank7). Improved Health Funnel 1/2 grants 20/40% more transferred health and 15/30% cheaper health cost. Pet actor supports opt-in actual damage before/after channel; demonstrable increased pet survival/DPS uptime, never new pet engine. | `WarlockHealthFunnelProcessor`, `WarlockDemonCompanion`, `CP167_AU_HealthFunnelPetDps`. Boss movement/range and reduced healing threat unverified. |
| Demonic Energies | Rank1/2 owner nonphysical spell damage heals active pet 8/15% of effective damage; Life Tap Mana gains transfer 50/100% to active demon, clamped to pet cap. Disabled if pet dead/missing. | `WarlockDemonicEnergiesProcessor`, `CP167_AV_DemonicEnergies`. Original generic spell-effect `ResourceChanged` omitted ResourceKey/ResourceTotalAfter: fixed in shared `AbilityExecutor`, proven by tests and full suite. |
| Bane of Agony rank6 | Current 70532 215 Mana / 552 base Shadow over 24s vs erroneous old 290 Mana and 1080 base. Existing 12 two-second ticks get target-owning early/middle/late rising damage instead of flat damage. | `ForeverWarlockAgonyRamp`, `WarlockRollResolver`, `CP167_AW_AgonyRankAndRamp`; Classic 0.5/1/1.5 tick distribution is a **provisional numerical proxy** for Forever's confirmed ramping wording, not server-log verified. |
| Curse of Recklessness rank4 | 115 Mana, 120s, -505 Armor against owner target, affecting real pet physical mitigation; does not remove Banes; replaces owner's Curse of Elements, reverses debuff on expiration/replacement. | `WarlockCurseOwnershipProcessor`, `CP167_AX_CurseAndBaneOwnership`; shared/debuff stacking of other players and full raid aura geometry deferred. |

**Removed/unsupported:** Dark Pact is absent from the current Forever talent tree; it must not be implemented from Classic solely to raise Affliction coverage. All former 25-spec and new 19-DPS-spec status definitions remain subordinate to the current DPS-only certification: 0/6 Mage/Warlock and 0/19 total DPS specs signed off until remaining executable issues are closed. Full tank/heal role signoff deferred with code/tests preserved.

**Validated functional commit:** `46e13b86ebeb5294010beb8c527694e166e7233f` [GitHub Actions run 40762849715](https://github.com/layoung3/WarcraftSim/actions/runs/40762849715), strict Release 0 build warnings and 1,986 passed. Later documentation commits require final-head CI check before calling current HEAD validated.


### CP167 Group 1 DPS mechanical closure evidence — 2026-10-29

The supported solo DPS certification now distinguishes verified current Forever client spell behavior from unknown server coefficients, and does **not** require unrelated standalone defensive or raid/encounter features. Existing CP166 tests, source pages and all tank/heal implementations remain preserved. No Mage/Warlock DPS spec is signed off yet; 19 DPS specs remain on the larger roadmap, Hunter follows only after Mage/Warlock gate.

| Current source mechanic | Actual correction | Deterministic execution evidence / unresolved |
|---|---|---|
| Mage Arcane Blast R5 | [Forever 70532](https://theforeverera.com/en/spells/mage/arcane-blast/): 15% base Mana instead of stale fixed 305, retains four 175%-cost stack levels / 15s state; 1376 simulated level60 baseMana remains provisional | `ForeverMageDpsSpellbook`, `CP167_AY`: spell definition and first/second cast resource events; stack and expiration tracked |
| Mage Missile Barrage | [Forever rank2](https://theforeverera.com/en/talents/mage/missile-barrage/): Frost Blast is an eligible spell. Arcane Mage custom rotation can cast baseline Frost Blast; proc rank gates unchanged | `MageSimRunner.MageSpecializationProcessor`, `MageAdaptiveRotation`, `CP167_AY` |
| Mage Improved Channeling | [Forever 70532](https://theforeverera.com/en/talents/mage/improved-channeling/): 20% per rank reduction to Arcane Missiles interruption, 14% per rank hardcast pushback for Arcane Blast/Wrath | `MageImprovedChannelingProcessor`, `CP167_BA`: real incoming hit, channel cancellation, retained ticks, rank5 guaranteed protection. No fabricated boss AI |
| Mage Arcane Power + Clearcasting | In-progress spell costs are no longer rewritten by aura expiration/stack refresh; newly procced Clearcasting at the same timestamp as an already-started hardcast applies to the **next** spell, not the paid current one | `MageSimRunner`, `CP167_BC`, `CP167_BG`, preserved `CP166_Stage4C_MagePriestAudit` and `CP167_F_MageArcaneCooldowns` assertions |
| Mage Improved Cone of Cold | [Forever ranks1–3](https://theforeverera.com/en/talents/mage/improved-cone-of-cold/): +12/23/35% resolved Cone damage, rank cap/tier | `ForeverMageTalentBuild`, `ForeverMageDpsSpellbook`, `CP167_BD` |
| Mage Fireball rank12 | [Forever 70532/70634](https://theforeverera.com/en/spells/mage/fireball/): 410 Mana, 425–541 direct (sim average 483) and 60 Fire damage over 8s with landed impact requirement | `ForeverMageDpsSpellbook`, `CP167_BE`: 4×15 tick, miss no application. Periodic SP coefficient is unverified and not borrowed from direct. Periodic critical mask conservatively disabled pending explicit Forever evidence |
| Mage Fire Power | [Forever 70532](https://theforeverera.com/en/talents/mage/fire-power/): count *active source-owned target Fire damage effects*, not recently cast spells on all targets. Improved Scorch is not a separate damaging Fire effect | `MageRollResolver`, `CP167_BF`, unchanged `CP167_V` Improved Scorch outcome assertions; no double counted target vulnerability |
| Mage Shatter | Charge-consumption is associated with actual spell key; nonperiodic resolution only so a previous frozen treatment cannot grant unrelated DoT ticks critical benefit | `MageSpecializationProcessor.IsFrozenSpellDamage`, `CP167_AQ/AR` |
| Warlock Shadow and Flame | [Forever 70532](https://theforeverera.com/en/talents/warlock/shadow-and-flame/): Conflagrate applies 20s +2% Shadow per rank; Shadowburn applies 20s +2% Fire per rank and 20% per rank immediate Soul Shard refund; existing Immolate retention preserved | `WarlockShadowAndFlameProcessor`, `WarlockRollResolver`, `CP167_AZ`: landed normal + repeat 15s cooldown Shadowburn, no proc on miss, live aura only for *later* spells, exact resource totals |
| Warlock Demonic Brand | Source-owned brand proc school and damage attribution run through existing seven companion actors: Imp/Infernal Fire, other five Shadow | `CP167_BB`: seven family offense/owner/target smoke outcomes; defensive/pet-specific server coefficients remain provisional |

**Milestone gate:** 0/6 certified until meaningful Mage Arcane/Fire/Frost and Warlock Affliction/Demonology/Destruction remaining DPS blockers are resolved, even if tests pass. Current CI requires the final branch HEAD to pass the existing strict .NET 10 Release job. Hunter, equipment and frontend work has not begun here.


### CP167 Mage client 1.60.1.70685 October 28 talent migration and Warlock DPS transitions

**Primary Mage hotfix evidence:** [WOWF.IO direct 70658→70685 comparison](https://wowf.io/en/news/mage-talent-hotfix-oct-28) and [updated 70685 calculator](https://wowf.io/en/talents/mage). 70532 was authoritative for the earlier inventory but is **not** the latest talent layout. Mage historical 54 talents become **53 current** with removed Arcane Focus; Warlock's 52 talent entries are unchanged by this client hotfix.

| Delta | Executable code | Outcomes / limitations |
|---|---|---|
| Arcane Focus removed, Improved Channeling row1/col2, Arcane Resilience row1/col3, Barrage row2/col4 | `ForeverMageTalentBuild`: no obsolete hit bonus, migrated old `arcane-focus` rows without allocating alternative effect; position mapping and 5-prior-point Barrage gate, Arcane Resilience rank cap | `CP167_BH` migration/position/tier; Arcane Resilience is purely defensive and deferred |
| Improved Fireball -> Accelerant | Canonical `accelerant` rank1–5 reduces Fireball/Frostfire Bolt AND Arcane Wrath cast time .1–.5 sec. Previous `improved-fireball` key accepted on imported JSON and canonicalized on validation. | `CP167_BH` 0/2/5 ranks; Frost Blast is not Accelerant eligible |
| Improved Fire Blast moved | New Fire row3/col4, requires 10 earlier Fire points; damage path/CD retained | `CP167_BH` + original `CP167_AE`, no invented range benefit |
| Incineration 3->5, Critical Mass 5->3 | Rank-valid crit effects in `MageRollResolver`: Fire Blast, Ice Lance, Arcane Blast, Scorch +2%/rank up to10%; Fire all-spell Critical Mass +2%/rank up to6%; Combustion requires **3/3** Critical Mass | `CP167_BH/BI` actual deterministic critical differences; existing CP166 rank boundary assertions updated to 70685 without weakening the current cap |
| Elemental Precision -> Precision | Three Frost ranks, +2/4/6% hit to every Mage school, obsolete Arcane Focus hit removed | `CP167_BH/BI` miss-vs-landed Arcane, Frost and Fire experiments, serialized legacy mapping |
| Frost Blast rank3 numeric conflict | Spell 1326055; level48, 90 Mana, base1.5s cast, 30yd, 9s 40% slow. 70532 lists 73–81; 70630/70634-derived lists 80–88, newer SP ratio reports 27.1%, older 34.3%, historic simulator uses 42.9%. | `ForeverMageTuning.FrostBlastDamage` and `FrostBlastSpellPowerCoefficient` separately configurable, no unverified 70685 value chosen. `CP167_BI` verifies alternate numeric inputs and landed-hit Chill + Permafrost extension; exact 70685 rank3 payload needs client export |
| Missile Barrage trigger / October22 faster ticks | `MageSpecializationProcessor`: 15s proc duration, successful landed direct hit required, Frost Blast eligible; 5 missiles in 2.5s with .5s spacing and zero cost at channel start. | `CP167_BI` outcomes for miss/hit, expiration and fast ticks; older 70532 faster-tick removal report conflicts with 70685 tooltip, retain current explicitly documented accelerated path |
| Warlock transition evidence | `WarlockSacrificeRules`, existing companion, Soul Link bonus and shard resource; no independent pet engine | `CP167_BJ`: Demonic Pact preserves different-family Sacrifice but cancels same-family replacement; owner damage Soul Link bonus removed on pet death; zero-shard Shadowburn unavailable and misses cannot earn death refund |

**Certification standard:** currently Mage Arcane/Fire/Frost and Warlock Affliction/Demonology/Destruction remain **mechanically partial until source-backed target/owner and proc intersections pass final tests**; numerical uncertainty in Frost Blast coefficients/geometry is separately provisional, not an independent blocker. Do not certify other DPS classes or start Hunter before six-spec gate. Retain original tank/heal functionality and all previous CP166 regressions.


## CP168 closeout — Warlock-only class completion evidence (2026-10-29)

This section **supplements**, rather than rewrites, CP166 Stage 1–4E shared-engine audit findings. The Warlock-specific CP168 executable completeness and numerical scope now live in [CP168-warlock-completion-ledger.md](CP168-warlock-completion-ledger.md).

| Area | CP168 executable reconciliation | Regression evidence | Status |
|---|---|---|---|
| Source-target aura ownership | Existing `AuraManager` and target-specific scheduling retained; Warlock own-Immolate cast prerequisite, multi-target DoTs and Havoc fixed-target selection; no additional aura engine | `CP168_DestructionSpellbookAndCastGates`, `CP168_DestructionSecondaryTargetOwnership`, `CP168_DestructionAoEHavocIntegration`, Affliction owner tests | **Class-verified / server timing provisional** |
| Shared cast/resource pipeline | Baseline Soul Fire registered for all 3 Warlock specs, one shard reserved/consumed; Cataclysm discount applies to all supported Destruction spells; Shadowburn exact death-window refund and repeated/dead target protection | `CP168_DestructionCostCompleteness`, `CP168_DestructionShadowburnDeathWindow`, `CP167_S_WarlockDecimation` | **Class-verified / server numeric provisional** |
| Periodic/channel/target accounting | Immolate/Conflagrate owned applications and retention, 1–3 target Rain/Hellfire, no self-damage in outgoing DPS or threat, interruption/pushback via shared engine | `CP168_DestructionAoEHavocIntegration`, `CP168_DestructionCritAndDamageTalentOutcomes`, `CP167_AQ_DpsProcChannelPushback`, `CP167_J_AreaSpells` | **Class-verified / area geometry deferred** |
| Procs and crit/damage masks | Improved Shadow Bolt own target; Shadow and Flame, Fire and Brimstone, Agonizing Flames, Ruin, Aftermath; Demonic Brand and Nightfall unchanged beyond CP168 reconciliation | `CP168_DestructionImprovedShadowBoltOwnership`, `CP168_DestructionRanksAndProcs`, `CP167_AZ_ShadowAndFlame`, CP168 Demonology suite | **Class-verified** |
| Cross-specialization reporting | Default and custom Warlock rotations for Affliction, Demonology, Destruction and all 7 demons retained, owner/pet damage, Mana/shard and threat totals reconcile; Mage/CP166/CP167 tests retained | `CP168_FinalWarlockCrossSpecRegression` and complete GitHub Actions suite | **Class-verified** |

Latest gameplay/test HEAD `443e33ac8f13f99f6e074835cd7f0d7935d34a1b` ran [GitHub Actions #40919895748](https://github.com/layoung3/WarcraftSim/actions/runs/40919895748): **2,258/2,258 tests passing, zero build warnings**. Shared numerical/encounter-only caveats from CP166 remain intact. The class scope is level-60 **solo PvE DPS**, not exhaustive defensive/raid mechanics.


## CP169 Hunter cross-engine completion closeout — October 30, 2026

The existing CP166 shared-engine mechanisms have been reused and regressed, not replaced by a class-specific combat engine. [CP169 ledger](CP169-hunter-completion-ledger.md) is the authoritative class-level completion record.

| Shared system | Hunter executable evidence | State |
|---|---|---|
| Companion actor and lifecycle | `HunterCompanionComposition` and `HunterSharedCompanionRuntime` compose MM/Survival in the existing `AbilityExecutor`, `AutoAttackProcessor`, `AuraManager`, `ThreatManager` and `CompanionActorFactory`; BM shares actor/ability/rotation composition | **Verified for supported solo DPS** |
| Physical/spell inheritance and AP/crit | Pet melee +100% Hunter ranged hit bonus, pet spell hit +17/8 owner bonus, Expertise applies to physical avoidance, higher qualified melee/ranged crit; dynamic owner stat deltas without dropping pet-only buffs | **Verified; hidden coefficients provisional** |
| Owner/pet scheduling and resources | Independent owner Auto Shot or melee stream with pet swing and shared event clock; pet Focus regeneration, signature priority reservation, cooldown, revive/reengage, no post-death attacks | **Verified via cross-spec 19-family suite** |
| Target/proc/aura handling | Serpent Sting dependent hit and 5 ticks, source-owned Hunter special procs, family periodic effects and refresh, Fox haste refresh nonstacking and expiry, no duplicated threats or damage | **Verified; geometry deferred** |
| Regression and serialization | All 3 specs default/custom and pet/no-pet, each of 19 families through MM and Survival, prior CP166/CP167 Mage and CP168 Warlock suites | **Full strict CI 2,426 passing, zero build warnings** |

GitHub Actions [#41061083503](https://github.com/layoung3/WarcraftSim/actions/runs/41061083503) confirms last gameplay/test head; final documentation-only HEAD CI must also pass. No equipment/raid implementation included in CP169.
