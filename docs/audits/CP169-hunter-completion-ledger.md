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

## Shared engine — OPEN as needed

- [ ] Physical/spell bonus-hit inheritance: pet melee +100% owner ranged hit bonus, pet spell +17/8 bonus, not final hit; Expertise, high eligible melee/ranged crit.
- [ ] Source-owned auras, periodic scheduling, same-timestamp action/cast state, shared shot cooldowns, dynamic haste/swing rescheduling and resource reservations.
- [ ] Regression tests for classes affected by any shared changes.

## CI and acceptance record

| Gate | Executable changes | Specific outcome tests | CI | Decision |
|---|---|---|---|---|
| Marksmanship | Pending | Pending | CP168 baseline only | OPEN |
| Survival | Pending | Pending | CP168 baseline only | OPEN |
| Beast Mastery | Pending | Pending | CP168 baseline only | OPEN |
| 19 pet families | Pending | Pending | CP168 baseline only | OPEN |
| Cross-class | Pending | Pending | CP168 baseline only | OPEN |

**Merge gate:** All independently implementable high-impact solo DPS effects complete, all family outcomes tested, source-backed rank/prerequisite evidence, default/custom rotations, report reconciliation and 0-warning .NET 10 Release full CI. Explicit local validation and merge authorization required for this PR.
