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

**CI validation:** PR #16 runs the pre-existing .NET 10 Release `-warnaserror` build and full xUnit suite. The previous complete audit/matrix commit `854d6027b0ff282dda89b7da05baab689c2f858d` passed [Actions run 40557817095](https://github.com/layoung3/WarcraftSim/actions/runs/40557817095). The final additional physical avoidance regression must also pass at PR head before review. No merge or Stage 4C before repository-owner local validation.

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
