# CP169 — Hunter DPS mechanical-completion ledger

**State:** OPEN / not certified. **Baseline**: merged main `61dab83a491f3e5235515ccfcf9bd76ccda685ba` (CP168 Warlock, 2,258 tests, zero build warnings). **Branch:** `cp169-hunter-dps-completion`. One draft PR for Marksmanship, Survival, Beast Mastery and 19 pet families. Do not merge without local validation and explicit approval.

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

## Shared engine — OPEN as needed

- [ ] Physical/spell bonus-hit inheritance: pet melee +100% owner ranged hit bonus, pet spell +17/8 bonus, not final hit; Expertise, high eligible melee/ranged crit.
- [ ] Source-owned auras, periodic scheduling, same-timestamp action/cast state, shared shot cooldowns, dynamic haste/swing rescheduling and resource reservations.
- [ ] Regression tests for classes affected by any shared changes.

## CI and acceptance record

| Gate | Executable changes | Specific outcome tests | CI | Decision |
|---|---|---|---|---|
| Marksmanship | Ranged damage specialization, owner Serpent death eligibility, current shot ranks, Efficiency 3%-per-rank, custom priority | CP169 MM ranged/ranks/timing, CP158 existing | #40923339576 2,280 passed | OPEN — pet-enabled runner not supported |
| Survival | Shared 70532 Arcane/Serpent/Multi corrections; preserved melee-first weaving/traps | CP169 HunterSharedRanks and CP159/160 inherited | #40924727492 2,326 passed | OPEN — pet-enabled runner not supported; remaining talent/rotation gate |
| Beast Mastery | Actual Focus reservation for family signature; configurable pet rotation; hit-dependent BM Sting | CP169 FocusPriority, all-19 outcome suite and CP163 inheritance | #40924727492 2,326 passed; custom priority CI pending | OPEN — complete talents and family-vs-owner sweep |
| 19 pet families | Individual real signature/Focus/melee/threat, death and attribution | CP169 AllNineteenPetOutcomes and CP163 PetFamilySweep | #40924727492 2,326 passed | OPEN — remaining rank/owner-stat/revive evidence |
| Cross-class | Pending | Pending | CP168 baseline only | OPEN |

**Merge gate:** All independently implementable high-impact solo DPS effects complete, all family outcomes tested, source-backed rank/prerequisite evidence, default/custom rotations, report reconciliation and 0-warning .NET 10 Release full CI. Explicit local validation and merge authorization required for this PR.
