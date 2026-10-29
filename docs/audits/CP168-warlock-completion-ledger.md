# CP168 — Warlock DPS mechanical completion ledger

**Status:** OPEN — not certified. **Base:** `main` at `50a5aebc06961fb43995ceeaba0e9afa8ebdb2cf` (CP167 Mage merged). **Branch:** `cp168-warlock-dps-completion`.

This ledger is the authoritative CP168-specific review checklist, to be filled with implementation evidence. Inherited CP166/CP167 tests do not by themselves certify a specialization. Keep every implementation batch in the same branch and pull request. Never merge without owner local validation and explicit approval.

## Evidence requirements per checked row

Record: current Forever build and tooltip/client reference, talent rank and prerequisite, exact executable registration → cast/proc → outcome path, deterministic positive/negative/edge-case tests, full-suite CI status, and any numerically provisional or encounter-dependent assumptions. Avoid restoring removed Dark Pact. For affected engine behavior, add reusable regression tests instead of creating parallel Warlock-only engines.

## Affliction — OPEN

**CP168 batch G — effective healing threat and mixed target effects:** Extended existing `WarlockDemonThreat` with `HealingThreatResolver`, using the shared provisional 0.5 effective-healing threat coefficient distributed among living enemies and honoring owner-only Suppression. `CP168_AfflictionSuppressionThreat` now tests rank-zero vs rank-five healing threat split on two live enemies; full .NET CI passed the fixed fixture at `0c3d230d0819b4a04e9eef6cc7f6b2a35f86b27a`. Added opt-in `AfflictionAdditionalTargetPriority` fixed-target debuff selections so real three-target simulations can maintain different Curse/Bane combinations (including Elements/Agony primary, Recklessness/Doom secondary, Corruption/Agony tertiary), preserving default single-target and the `SpreadAfflictionDots` fallback. `CP168_AfflictionIndependentTargetDebuffs` covers actual aura application, exclusivity, API serialization and invalid target/spell rejection. Commits `51bb97800e33ab323a45b98e8fa1979cebf9ca61` / `28aa656d3695dfb52bbc4279cfd1b37a59a1e6d4`; final CI for latest test pending. **Encounter-dependent enemy movement, Curse of Exhaustion slow and Curse of Weakness mitigation are deferred from DPS scope.**

**CP168 batch E — complete Wrack Shadow-DoT mask:** `Warlock70425DamageModifiers.Multiplier` previously granted Wrack's +10% periodic Shadow vulnerability only to Corruption and Agony. Current 70532 [Wrack tooltip](https://theforeverera.com/en/talents/warlock/wrack/) describes other Shadow DoTs, and this now includes Siphon Life and Bane of Doom without affecting direct spells, pet damage, or unrelated schools. `CP168_AfflictionWrackVulnerability` exercises all four supported Shadow DoTs, negative categories, and a live Siphon Life damage tick with Wrack active. Commits `6bb948ed6d6059b35ea99b5b21417b6057af83ce` / `13ba4be707169b84af96676cfd11144b8b9d75a0`. CI pending.

**CP168 batch F — Suppression threat:** Client 70532 [Suppression](https://theforeverera.com/en/talents/warlock/suppression/) grants +1% hit and -4% threat per rank (max 5). Hit already worked; the owner's outgoing threat did not. `WarlockSimRunner` now sets rank-derived suppression threat reduction, and shared `WarlockDemonThreat` applies it to owner outgoing damage, independently of Searing Pain's Demonic Brand multiplier, without reducing pet threat. `CP168_AfflictionSuppressionThreat` covers ranks 0/1/3/5, pet non-interference, and full simulated Shadow Bolt damage versus threat. Commits `70f1f3eb3ff8a46cdbb5fa2a403f61aa9731eb81` / `b9b3c8cae4880d675bf18c583388bf5d2f15f262`. CI pending.

**CP168 batch D — Soul Siphon and client source cross-check:** Current 70532 source describes +4/8/12% per other owned Affliction effect, capped at three; the current [Blizzard deep dive](https://worldofwarcraft.blizzard.com/en-us/news/24310968/world-of-warcraft-forever-explorando-a-fundo-as-classes-ladino-e-bruxo) and [70532 talent record](https://theforeverera.com/en/talents/warlock/soul-siphon/) support the effect mask. The runtime counted Elements but omitted Recklessness, despite both being Affliction curses; `WarlockSimRunner.WarlockRollResolver` now includes the latter. `CP168_AfflictionSoulSiphonCurse` compares first landed Drain Life ticks at rank zero and rank three for both own Curses, requiring precisely +12% for one active effect. Added at `76ef414066ad2e0ccc6b42441ac746b66397a966` / `c237790c49746cecfd7cb5545084a8e646212aa2`, CI pending. **No claim that other Affliction ranks are all certified.**

**CP168 batch C — multi-target execution and shared owner-aware rotation conditions:** New opt-in `SpreadAfflictionDots` (default false) expands generated and custom Affliction priority entries for Corruption, Agony, Doom, Siphon Life and both supported Curses across each configured enemy, using existing `RotationTargetSelector.Fixed` (not a separate actor/companion engine). Bane of Havoc is intentionally excluded from spreading because it has a unique secondary-victim slot. Added `RotationConditionDefinition.RequireSourceOwnership` (default false) and owner-filtered target-aura evaluation/next-expiry scheduling, so another Warlock's Corruption does not suppress the local caster's refresh. Existing generic shared-buff conditions remain unchanged. `CP168_AfflictionMultiTargetRotation` covers 1/2/3 targets, per-target AuraApplied and actual damage, baseline opt-out, misses with Suppression disabled, and JSON serialization; `CP168_SourceOwnedAuraConditions` checks mixed-caster suppression and expiry scheduling. GitHub Actions run of initial batch failed 1/2092 tests because Suppression invalidated the 0-hit test fixture; the fixture was repaired at `e2fc661709c91bc0b4b5bc79b09b297b8a0d5fe0`; subsequent full CI must pass before any gate can close. **Not a full Affliction certification:** owner-target refresh at equal timestamps, drain-resource outcomes, complete talent rank matrix, default and custom resource-starvation, and threat reconciliation still require outcome evidence.

**CP168 implementation batch B (2026-10-29):** `WarlockAmplifyCurseProcessor` now clears the source-owned `warlock-amplified-agony` marker whenever its Agony is removed (including replacement by another Bane, expiration or dispel), while ignoring stale removal events when an owner Agony remains active. `CP168_AfflictionAmplifyOwnership.cs` supplies controlled three-target, two-caster coverage and a stale-removal guard. Commits `44e463934a57191fba81ab4ecc6ee12484aeee50` / `d802f5f901c4ecc1b5ec49d404a9350fd4679b71`. **These tests validate one interaction, not full multi-target rotation mechanics.**

**CP168 implementation batch A (2026-10-29):** fixed early Shadow Trance aura removal leaving an internal instant-Shadow-Bolt state active in `WarlockNightfallProcessor.cs`. The code now invalidates the proc on source-owned `AuraRemoved`, even before its nominal expiry. Added `CP168_AfflictionNightfallRemoval.cs`: deterministic forced-100%-proc setup, 3s→instant cast-time transition, early aura removal, 3s restored timing and zero false consumption. CI pending at commit `c0c95cba01b06f4b4e6630bf0f1ccbfb68af45dc`. **This does not certify Affliction**; full multi-target, Bane, curse, channel, economy and talent outcome gates remain open.

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
