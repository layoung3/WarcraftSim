# CP169 — Hunter DPS mechanical-completion ledger

**State:** OPEN / not certified. **Baseline**: merged main `61dab83a491f3e5235515ccfcf9bd76ccda685ba` (CP168 Warlock, 2,258 tests, zero build warnings). **Branch:** `cp169-hunter-dps-completion`. One draft PR for Marksmanship, Survival, Beast Mastery and 19 pet families. Do not merge without local validation and explicit approval.

## Source and evidence policy

Compare actual spellbook/talent registration with executable cast → damage/proc → resource/cooldown → report. Client source: [Forever Hunter specs](https://theforeverera.com/en/classes/hunter/), [Marksmanship](https://theforeverera.com/en/classes/hunter/marksmanship/), [Survival](https://theforeverera.com/en/classes/hunter/survival/), [Beast Mastery](https://theforeverera.com/en/classes/hunter/beast-mastery/), client build **1.60.1.70532**, updated October 24, 2026. Keep unmeasured server coefficients provisional and all out-of-scope geometry explicit. Do not restore Classic-era removed mechanics. An existing test or a constructor pass does not constitute certification.

## Marksmanship — OPEN

- [ ] Live Auto Shot/shot execution, physical hit/crit/AP, stationary policy, rapid fire swing retiming, shot cooldown relationships, 1–3 target eligibility.
- [ ] Source/current-rank talent outcomes: Lethal Attacks, Mortal Shots, Careful Aim, Efficiency, Barrage, Trueshot Aura, Lone Wolf, Improved Stings, Improved Arcane Shot, Rapid Killing/Recuperation, Sniper Shot and prerequisites.
- [ ] Default and custom priorities, mana/health/threat, target application, pet eligibility and response serialization.
- [ ] Full strict CI and final-head local validation gate.

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
