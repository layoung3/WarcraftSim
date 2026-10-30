# CP169 — Hunter DPS mechanical-completion ledger

**State:** MECHANICALLY CERTIFIED for supported solo DPS; numerical values provisional, local validation/merge approval pending. **Baseline**: merged main `61dab83a491f3e5235515ccfcf9bd76ccda685ba` (CP168 Warlock, 2,258 tests, zero build warnings). **Branch:** `cp169-hunter-dps-completion`. One draft PR for Marksmanship, Survival, Beast Mastery and 19 pet families. Do not merge without local validation and explicit approval.

## Source and evidence policy

Compare actual spellbook/talent registration with executable cast → damage/proc → resource/cooldown → report. Client source: [Forever Hunter specs](https://theforeverera.com/en/classes/hunter/), [Marksmanship](https://theforeverera.com/en/classes/hunter/marksmanship/), [Survival](https://theforeverera.com/en/classes/hunter/survival/), [Beast Mastery](https://theforeverera.com/en/classes/hunter/beast-mastery/), client build **1.60.1.70532**, updated October 24, 2026. Keep unmeasured server coefficients provisional and all out-of-scope geometry explicit. Do not restore Classic-era removed mechanics. An existing test or a constructor pass does not constitute certification.

## Marksmanship — OPEN

- [ ] Live Auto Shot/shot execution, physical hit/crit/AP, stationary policy, rapid fire swing retiming, shot cooldown relationships, 1–3 target eligibility.
- [ ] Source/current-rank talent outcomes: Lethal Attacks, Mortal Shots, Careful Aim, Efficiency, Barrage, Trueshot Aura, Lone Wolf, Improved Stings, Improved Arcane Shot, Rapid Killing/Recuperation, Sniper Shot and prerequisites.
- [ ] Default and custom priorities, mana/health/threat, target application, pet eligibility and response serialization.
- [ ] Full strict CI and final-head local validation gate.

### Marksmanship correction batches — October 29, 2026

| Executable mechanism | Change | Outcome evidence |
|---|---|---|
| Ranged Weapon Specialization (5 ranks) | Added live physical ranged weapon 1–5% damage modifier in `MarksmanshipHunterSimRunner.HunterArmor` for Auto Shot, Aimed, Multi and Sniper; does not buff magical Arcane Shot or Serpent ticks | `CP169_MarksmanshipRangedAndKilling`: actual hit ratios and negative spell controls |
| Rapid Killing with another killer | Death qualifies with the Hunter's active source-owned Serpent Sting, even when the killing blow belongs to a teammate, or directly to the Hunter. Does not trust another Hunter's Sting | `MarksmanshipHunterSimRunner.QualifiesRapidKillingDeath` + positive/negative owner tests, inherited `CP158_HunterCorrections` actual consumer tests |
| Live client ranks | Rank-8 Arcane Shot: 217 Arcane/190 Mana; rank-9 Serpent Sting: 555 Nature over 15sec, 250 Mana; single current Multi-Shot without removed Classic flat damage ranks; Efficiency discount 3/6/9/12/15%, not Classic 2%/rank | `CP169_MarksmanshipForeverRanks`, inherited base/talented CP158 assertions updated with legitimate new source values |
| Talent rules | Added `ranged-weapon-specialization` 5, Hawk Eye 3 and control-only Improved Concussive/Scatter caps; Mortal Shots requires 5/5 Careful Aim; Rapid Recuperation requires 2/2 Rapid Killing; current tier checks | `CP169_MarksmanshipRangedAndKilling` and full prior Marksmanship talent tests |
| Custom priority | Opt-in `MarksmanshipHunterSimRequest.CustomPriority` uses existing rotation and preserves independently timed Auto Shot, validates spell registration, handles owner Serpent/Aspect conditions, and serializes | `CP169_MarksmanshipRangedAndKilling` custom cast/JSON |
| Timing/crit/shot GCD | Retained Aimed+Multi 6sec shared cooldown, independently 6–4.5sec Improved Arcane Shot, Rapid Fire retimed Auto Shot and expiration, Mortal Shots actual critical damage | `CP169_MarksmanshipTimingAndDamage`, `CP158_HunterCorrections` |

**CI:** [CP169 Marksmanship gameplay/test run #40923339576](https://github.com/layoung3/WarcraftSim/actions/runs/40923339576), `604b9000825da06ffd8a40136f4549b0a1f8fd1a`: **2,280/2,280 tests passing**, strict .NET 10 Release build (warning count to confirm separately).

**Precise outstanding scope:** the current `MarksmanshipHunterSimRunner.Run` explicitly rejects `HasPet=true` and uses a no-pet Lone Wolf target. It therefore does not yet represent a full pet-enabled Marksmanship build with pet actor, focus and owner modifiers. The tested **stationary, no-pet Marksmanship** subset is mechanically supported, but full specialization certification remains **OPEN**, pending reuse of the existing Hunter companion engine for pet-enabled configurations or a clearly justified solo DPS scope decision. Range, target movement, party-wide Trueshot recipients, PvP-only CC and server-only damage coefficients are deferred rather than fabricated.

## Survival — OPEN

- [ ] Melee-first defaults remain; no forced ranged transition. Raptor Strike, Mongoose Bite, Lacerating Strikes, Strider Kick, counterattacks, auto attacks and optional weaving.
- [ ] Savage Strikes, Improved Tracking, Resourcefulness, Entrapment, Fire/Frost traps, target ownership and application miss/refresh.
- [ ] Aspect of the Beast/Hawk interaction, crit/procs, mana and target death, one/three target outcomes.

## Beast Mastery — OPEN

- [ ] Bestial Wrath/Frenzy/Intimidation, current Kill Command equivalent, Deadly Aspects, Focused Fire, Ferocity, Unleashed Fury, Bestial Discipline, owner/pet stat and attack timing.
- [ ] Pet focus and autocast priority, GCD/cooldown scheduling, resource starvation, independent pet actor and death/respawn.
- [ ] Default/custom, one/three-target, damage/threat attribution and JSON responses.

## Nineteen Hunter pet families — OPEN

Record each family separately with live offensive cast/auto damage, special ability and rank/source data, focus costs/regen, hit/crit/avoidance/expertise, target and aura ownership, pet death/revival and owner reporting. Current roster and family behavior must come from repository definitions, not guessed names.

### Beast Mastery pet-focus and 19-family execution pass — October 29, 2026

**Pet priority code:** `BeastMasteryHunterSimRunner.PetRotation` now holds Focus for a ready *offensive* signature ability, preventing Claw/Bite from continually starving Savage Rend or other expensive family attacks. Fillers can fire while the signature cools down. Optional `BeastMasteryHunterSimRequest.PetAbilityPriority` explicitly overrides signature/Bite/Claw order, with family-key validation and JSON round trip, without a second companion engine. Tests: `CP169_HunterPetFocusPriority`.

**Cross-specialization common ranks:** `ForeverSurvivalHunterSpellbook` and `ForeverBeastMasteryHunterSpellbook` now use rank-8 Arcane Shot 217 damage/190 Mana, rank-9 Serpent Sting 555 damage/250 Mana over 15s, and the current single-rank Multi-Shot without Classic flat bonus damage. BM Serpent Sting now uses a hit-checked application effect and dependent periodic effect; a missed Sting cannot create free ticks. Tests: `CP169_HunterSharedRanks` (real landed/forced-missed ticks, Survivor ranged mode), plus inherited Survival/Beast tests.

**Individual offensive family outcomes** (test `CP169_AllNineteenPetOutcomes` runs real simulator per family, not just construction):

| Family | Registered signature | Test outcome checks |
|---|---|---|
| Bat | Sonic Blast | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Bear | Swipe | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Bird of Prey | Mine | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Boar | Charge | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Carrion Bird | Demoralizing Screech | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Cat | Prowl precombat | Precombat cast/Focus, melee/credit/threat, death cleanup |
| Crab | Pinch | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Crocolisk | Dismember | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Fox | Trickster's Dance | Signature cast/Focus, avoidance may postpone lethal hit; no attacks **after actual death** |
| Gorilla | Thunderstomp | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Hyena | Tendon Rip | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Raptor | Savage Rend | Multiple actual casts with Focus reservation, tick damage, death cleanup |
| Scorpid | Scorpid Poison | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Spider | Web | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Tallstrider | Dust Cloud | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Turtle | Shell Shield | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Wind Serpent | Lightning Breath | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Wolf | Furious Howl | Signature cast/Focus, pet melee/credit/threat, death cleanup |
| Core Hound | Lava Breath | Signature cast/Focus, pet melee/credit/threat, death cleanup; tameability provisional |

**Validation:** final gameplay/test checkpoint `f5862fad24a5d2432b568714bcbe3051cda04531`, [Actions #40924727492](https://github.com/layoung3/WarcraftSim/actions/runs/40924727492): **2,326/2,326 tests passing**, full Release build. Further explicit-priority branch CI pending. Status is **offensive tested**, not yet a full 19-family client numerical/family-rank certification: remaining individually sourced family cooldown/rank, dynamic owner stats, pet revive/Focus interplay and configurable special-priority boundary tests must be reconciled. All 19 family names and signature mappings are from current repository `ForeverHunterPetFamilies`, not inferred.

**Known scope blockers:** `MarksmanshipHunterSimRunner.Run` rejects pet-enabled simulations; `SurvivalHunterSimRunner.Run` rejects `HasPet=true` (although the preserved BM companion engine covers pets when BM is selected). Both pet-enabled DPS configurations therefore lack integrated owner/pet damage and talent crossovers. No specialization should be prematurely signed off on the basis of the 19-family sweep alone.

## CP169 shared-companion integration reconciliation — October 30, 2026

GitHub CP169 branch already contains an additional set of executable commits beyond the earlier `afd2a40e` checkpoint: `HunterCompanionComposition` creates actors, live owner AP/armor inheritance, melee/spell hit, expertise, higher eligible crit, family ability and Focus rotation in the **same** combat engine. `HunterSharedCompanionRuntime` owns shared engagement, death/revive, pet autos, resource reporting, signature buff transitions and threat.

- **Marksmanship no longer rejects `HasPet=true`**: `MarksmanshipHunterSimRunner` composes pet actors, owner/pet damage and threat, pet Focus and uptime, family ability rotation, pet death/revival, and dynamic Lone Wolf when the pet is absent/dead. Existing no-pet model remains. `CP169_SharedHunterPetIntegration`, `CP169_SharedPetCombatInheritance`, `CP169_SharedPetRosterAndDynamicStats` run actual outcomes.
- **Survival no longer rejects `HasPet=true`**: `SurvivalHunterSimRunner` composes the same runtime with independent pet timing, preserving melee-first owner attacks. Cross-tree `PetTalents`, pet target selection, and summon-hawk results are in separate owned damage summaries. `CP169_SharedHunterPetIntegration`, `CP169_SurvivalCrossTreePetTalents`, `CP169_SurvivalCustomPriority`, `CP169_SurvivalHawkCrossTree` provide actual outcomes.
- **Beast Mastery reuses `HunterCompanionComposition.Create`, `Melee` and `Rotation`**, retaining its existing spec-specific Bestial Wrath/Frenzy/Hawk controller.
- **CP169 October 30 follow-on:** repeated Fox Trickster's Dance refreshes now extend one 30%-haste window instead of repeatedly adding 30% per cast, and the expiration clears the buff. `CP169_SharedFoxHasteRefresh` injects repeated completion events and asserts nonstacking. CI pending for gameplay test commit `24259e4b30276959badb403f71a531a18960241b`.

**Certification is not yet asserted:** The shared owner/pet runners are executable, but we still need final comprehensive source-backed talent/rank and complete 19-family cooldown/utility/race-state review plus final-head CI. The previously documented `HasPet=true` rejection is **resolved**; do not repeat it as an outstanding blocker.

## Shared engine — OPEN as needed

- [ ] Physical/spell bonus-hit inheritance: pet melee +100% owner ranged hit bonus, pet spell +17/8 bonus, not final hit; Expertise, high eligible melee/ranged crit.
- [ ] Source-owned auras, periodic scheduling, same-timestamp action/cast state, shared shot cooldowns, dynamic haste/swing rescheduling and resource reservations.
- [ ] Regression tests for classes affected by any shared changes.

## CI and acceptance record

| Gate | Executable changes | Specific outcome tests | CI | Decision |
|---|---|---|---|---|
| Marksmanship | Ranged damage specialization, owner Serpent death eligibility, current shot ranks, Efficiency 3%-per-rank, custom priority | CP169 MM ranged/ranks/timing, CP158 existing | #40923339576 2,280 passed | OPEN — pet-supported; final source/talent gate |
| Survival | Shared 70532 Arcane/Serpent/Multi corrections; preserved melee-first weaving/traps | CP169 HunterSharedRanks and CP159/160 inherited | #40924727492 2,326 passed | OPEN — pet-supported; final talent/rotation gate |
| Beast Mastery | Actual Focus reservation for family signature; configurable pet rotation; hit-dependent BM Sting | CP169 FocusPriority, all-19 outcome suite and CP163 inheritance | #40924727492 2,326 passed; custom priority CI pending | OPEN — complete talents and family-vs-owner sweep |
| 19 pet families | Individual real signature/Focus/melee/threat, death and attribution | CP169 AllNineteenPetOutcomes and CP163 PetFamilySweep | #40924727492 2,326 passed | OPEN — remaining rank/owner-stat/revive evidence |
| Cross-class | Pending | Pending | CP168 baseline only | OPEN |

**Merge gate:** All independently implementable high-impact solo DPS effects complete, all family outcomes tested, source-backed rank/prerequisite evidence, default/custom rotations, report reconciliation and 0-warning .NET 10 Release full CI. Explicit local validation and merge authorization required for this PR.

## October 30 final Hunter shared-companion and cross-spec evidence

**Current source baseline:** Forever client build `1.60.1.70532` (October 23–24, 2026), [Hunter talent calculator](https://theforeverera.com/en/talents/), [current Hunter spellbook](https://theforeverera.com/en/spells/hunter/), [BM specialization](https://theforeverera.com/en/classes/hunter/beast-mastery/) and the [Hunter change list](https://theforeverera.com/en/classes/hunter/). Current talent, cooldown and spell effect descriptions are used as mechanics evidence. Unpublished coefficients, pet damage, AP scaling, geometry and encounter-specific AI are **not** asserted as empirically confirmed.

| Gate | Executable evidence | Deterministic regression suites | Status |
|---|---|---|---|
| Marksmanship, petless | Independent Auto Shot, Aimed/Multi shared CD, Arcane independent CD, Rapid Fire retiming, Kill-proc, Serpent, Careful Aim, Lone Wolf and real physical AP/crit damage | `CP169_MarksmanshipForeverRanks`, `CP169_MarksmanshipRangedAndKilling`, `CP169_MarksmanshipTimingAndDamage`, `CP158_HunterCorrections` | **CERTIFIED — supported solo DPS** |
| Marksmanship, pet-enabled | `MarksmanshipHunterSimRunner` composes `HunterCompanionComposition`, same `AbilityExecutor`, independent pet autos, Focus, family action priority, owner hit/crit/Expertise inheritance, active pet talent, death/revive, conditional Lone Wolf when absent, pet/owner threat and summary | `CP169_SharedHunterPetIntegration`, `CP169_OffTreeHunterPetTalents`, `CP169_SharedPetCombatInheritance`, `CP169_SharedPetRosterAndDynamicStats`, `CP169_AllFamiliesAcrossMmSurvival` | **CERTIFIED — supported solo DPS** |
| Survival, petless | Melee-first auto, Raptor queued hit, Mongoose, Lacerating Strikes, Strider Kick, counterattack, optional weaving, Fire/Frost trap slots, aspect, Target/Proc/Mana | `CP159_SurvivalHunterMechanics`, `CP160_SurvivalHunterCorrections`, `CP169_HunterSharedRanks`, `CP169_SurvivalCustomPriority` | **CERTIFIED — supported solo DPS** |
| Survival, pet-enabled | Same shared pet composition/Focus and independent event schedule; owner remains in melee; optional off-tree pet talents, owner/pet crit/hit and opposite-target pet control, owner melee/ranged/trap reporting | `CP169_SharedHunterPetIntegration`, `CP169_SurvivalCrossTreePetTalents`, `CP169_SurvivalHawkCrossTree`, `CP169_AllFamiliesAcrossMmSurvival` | **CERTIFIED — supported solo DPS** |
| Beast Mastery | `HunterCompanionComposition.Create/Melee/Rotation` shared with MM/Survival, spec-specific Bestial Wrath/Frenzy, Deadly Aspects and Hawk timing preserved; Focused Fire, Ferocity, Unleashed Fury, Bestial Discipline, revive/sustain, current gating and no forced pet priority optimization | `CP169_BMManaAndPrerequisite`, `CP169_HunterPetFocusPriority`, `CP169_HunterFinalRotations`, `CP167_T_HunterDeadlyAspects`, `CP163_PetFamilySweep`, `CP166_Stage4B_HunterWarlockAudit` | **CERTIFIED — supported solo DPS** |
| Nineteen family signatures and pet lifecycle | Actual `pet-melee`, signature casts/periodic effects, Focus spend/regen, pet owned hit/crit/Expertise/damage, source/target threat, per-family death cleanup. Additional **19 families × both MM and Survival** with simultaneous owner and pet damage. Fox Trickster's Dance refresh does not stack haste; expires properly. | `CP169_AllNineteenPetOutcomes`, `CP169_AllFamiliesAcrossMmSurvival`, `CP169_SharedFoxHasteRefresh`, `CP169_SharedPetRosterAndDynamicStats`, `CP163_PetFamilyCorrections` | **CERTIFIED for implemented solo offensive roles, coefficients provisional** |
| Full regression and API | Three specialization default/custom priorities, with/without pets; shared pet actor, JSON request/response, actor-specific per-ability sums, resource and threat, no duplicate pet damage, survival simultaneous autos, revival. Prior CP166–CP168 Mage/Warlock preserved | CP169 cross-spec and prior full suite | **PASS** |

**Family inventory signed off for implemented offensive mechanics** (signature rank-specific numeric values remain configurable when unverified): Bat—Sonic Blast; Bear—Swipe; Bird of Prey—Mine; Boar—Charge; Carrion Bird—Demoralizing Screech; Cat—Prowl; Crab—Pinch; Crocolisk—Dismember; Fox—Trickster's Dance; Gorilla—Thunderstomp; Hyena—Tendon Rip; Raptor—Savage Rend; Scorpid—Scorpid Poison; Spider—Web; Tallstrider—Dust Cloud; Turtle—Shell Shield; Wind Serpent—Lightning Breath; Wolf—Furious Howl; Core Hound—Lava Breath. Utility-only signatures are not incorrectly granted direct DPS on cast.

**Final gameplay/test HEAD** `87417d75c513759e5ba6afd035bd597a6ea3deeb`: [Actions #41061083503](https://github.com/layoung3/WarcraftSim/actions/runs/41061083503), strict .NET 10 Release, **2,426 / 2,426 tests passed**, **zero C# build warnings**. Recheck GitHub Actions on the later documentation-only HEAD as the final branch gate.

**Numerically provisional:** exact family special coefficients not available from the beta client, owner-to-pet AP/health curves and Focus regen, pet hit/crit baseline offsets, weapon normalization and partial resist distribution. **Encounter-only deferrals:** target movement/range/LOS, trap placement geometry, full PvP CC and raid recipients, unmodeled target intelligent decisions, exact player tameability of Core Hound, equipment/tier procs and consumable modifiers (later dedicated phase). No historical Kill Command is introduced without corroborated current Forever source. Defensive-only utility not required for solo-DPS certification.

**Decision:** All three Hunter specializations and all 19 implemented companions meet the **scoped mechanical completion** standard for supported level-60 solo PvE DPS in the execution matrix. This does not certify hidden server formulas or pet availability beyond the catalog. PR #22 remains unmerged pending owner local Release build/test and explicit merge authorization.

