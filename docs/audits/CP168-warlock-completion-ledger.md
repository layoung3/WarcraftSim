# CP168 — Warlock DPS mechanical completion ledger

**Status:** OPEN — not certified. **Base:** `main` at `50a5aebc06961fb43995ceeaba0e9afa8ebdb2cf` (CP167 Mage merged). **Branch:** `cp168-warlock-dps-completion`.

This ledger is the authoritative CP168-specific review checklist, to be filled with implementation evidence. Inherited CP166/CP167 tests do not by themselves certify a specialization. Keep every implementation batch in the same branch and pull request. Never merge without owner local validation and explicit approval.

## Evidence requirements per checked row

Record: current Forever build and tooltip/client reference, talent rank and prerequisite, exact executable registration → cast/proc → outcome path, deterministic positive/negative/edge-case tests, full-suite CI status, and any numerically provisional or encounter-dependent assumptions. Avoid restoring removed Dark Pact. For affected engine behavior, add reusable regression tests instead of creating parallel Warlock-only engines.

## Affliction — OPEN

- [ ] Full current client spellbook/tree inventory and prerequisites; distinguish movement/range-only utility from DPS.
- [ ] Corruption, Immolate, Bane of Agony and Bane of Doom: hit/application, tick progression, source-owned refresh, independent targets, expiration boundaries and damage aggregation.
- [ ] Curse/Bane exclusive categories: owner, target, replacement, self/other caster, simultaneous effects.
- [ ] Drain Life, Drain Soul, Wrack, Siphon Life, Soul Siphon: channel tick ownership, interruption, mana/health/shards, target conditions and attribution.
- [ ] Nightfall/Shadow Trance: **runner already constructs `WarlockNightfallProcessor` and exposes proc/consume metrics** (`WarlockSimRunner.cs` lines 185–197, 265, 313–314); CP167 inventory's `Missing from executable catalog` label is stale. Trace actual proc mask, duration, consumption, deterministic tests and current client ranks before marking complete.
- [ ] Amplify Curse: buff consumption, cooldown, Agony snapshot/refresh and misses.
- [ ] Pandemic, Malevolence, Soul Harvest, Improved Drains, Improved Life Tap and Shadow Mastery rank effects.
- [ ] Grim Reach: *CP167 inventory identifies registration without executable route*; classify range-only behavior honestly, no fake DPS benefit.
- [ ] Default + custom rotations and 1/2/3-target controlled runs, including competing casters.

## Demonology — OPEN

- [ ] Imp / Voidwalker / Succubus / Incubus / Felhunter / Infernal / Doomguard: offensive ability use, actual hits and damage attribution, mana/energy, inheritance, haste, scaling, owner talent modifiers and spell-school rules.
- [ ] Pet death/revival, replacement cast windows, temporary summon expiry and owner buff removal/reapplication.
- [ ] Master Summoner and Fel Domination: real cast costs/durations, cooldown, lost uptime and consumption rules.
- [ ] Demonic Pact and Demonic Sacrifice: before/after replacement and buff ownership; Soul Link on active/dead demon.
- [ ] Demonic Brand: charges, eligible schools, owner/pet proc masks, target and time boundaries.
- [ ] Decimation: threshold-before-hit, rank 1/2 cooldown, cast-time, damage, shard-free window, expiration/misses.
- [ ] Demonic Energies, Health Funnel and pet-resource/health sustain, with effective damage and survivability checks.
- [ ] Rank/prerequisite outcomes including Improved Imp/Voidwalker/Sayaad/Felhunter, Unholy Power, Fel Vitality, Demonic Knowledge and Master Demonologist.
- [ ] Demonic Embrace and Demonic Aegis: *CP167 inventory lists missing executable routes*; verify whether they affect supported DPS or incoming-damage sustain and implement as warranted.

## Destruction — OPEN

- [ ] Shadow Bolt/Improved Shadow Bolt, Immolate/Conflagrate, Incinerate, Searing Pain, Soul Fire and Shadowburn per-rank outcomes.
- [ ] Fire and Brimstone, Shadow and Flame, Intensity, Aftermath, Pyroclasm, Ruin and other live current talents: rank/prerequisites and deterministic proc tests.
- [ ] Bane of Havoc secondary-target selection and owner attribution.
- [ ] Rain of Fire/Hellfire: actual target multiplicity, tick roll rules, caster self-damage excluded from outgoing DPS, threat and attribution.
- [ ] Soul Shard spend/return semantics, specifically Shadowburn death windows and exact expiry/same-timestamp ordering.
- [ ] Cast start/completion and critical/periodic reporting, no double counted events.
- [ ] Default + custom priority runs, changing target health and two-target cases.

## Shared engine review — only when concretely needed

- [ ] Source-target aura identity, expiration and refresh scheduling.
- [ ] Application outcome vs tick outcome, eligible direct/periodic procs, event ordering.
- [ ] Resource reservation at cast start/completion and conditional shard refund.
- [ ] Companion stat update transitions and correct outgoing vs self-damage summaries.
- [ ] Channel pushback/interrupt/cancel and damage/threat reconciliation.

## Acceptance record (unfilled)

| Area | Executable evidence | Deterministic tests | Latest CI | Remaining blockers | Gate |
|---|---|---|---|---|---|
| Affliction | Pending | Pending | Pending | Complete inventory and behavior audit | OPEN |
| Demonology + seven demons | Pending | Pending | Pending | Offensive and lifecycle certification | OPEN |
| Destruction | Pending | Pending | Pending | All DPS talents/spells and self-damage accounting | OPEN |
| Cross-engine regression | Pending | Pending | Pending | Root-cause audit as issues are found | OPEN |

**Merge gate:** full .NET 10 Release build and tests, zero warnings/failures, source-backed status updates in `CP167-class-completeness-inventory.md` and `CP166-coverage-matrix.md`, owner local validation, explicit owner approval. The numerical-provisional status of unobserved server coefficients must remain explicit.
