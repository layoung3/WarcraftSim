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

## Affliction — mechanical DPS completion decision (2026-10-29)

**Gate: CERTIFIED for the currently supported level-60 solo PvE DPS model, with numerically provisional server values and explicit scope exclusions below.** Latest gameplay/test commit `7e4579881dc99c8484be9ab135eaa96e2b582470`: GitHub Actions [run #40906779476](https://github.com/layoung3/WarcraftSim/actions/runs/40906779476) passed the .NET 10 Release build with **zero C# warnings**, **2,136/2,136 tests passing**. The subsequent ledger-only commit requires its own CI status check; it changes no gameplay.

**Live source:** Forever client build 1.60.1.70532 (October 22–24, 2026) talent/spell entries at [Forever Warlock talents](https://theforeverera.com/en/talents/warlock/), [spells](https://theforeverera.com/en/spells/warlock/), [Improved Drains](https://theforeverera.com/en/talents/warlock/improved-drains/), [Pandemic](https://theforeverera.com/en/talents/warlock/pandemic/), [Wrack](https://theforeverera.com/en/talents/warlock/wrack/) and [Suppression](https://theforeverera.com/en/talents/warlock/suppression/). These establish client tooltip/rank intent; actual server probability/partial resistance/snapshot curves remain provisional.

| Supported behavior | Executable path and outcome evidence | Decision |
|---|---|---|
| Corruption, Agony ramp, Doom expiration and Siphon Life | `ForeverWarlockDpsSpellbook` → `AbilityExecutor` → `AuraManager` / periodic scheduler → `WarlockRollResolver`; `CP167_AW_AgonyRankAndRamp`, `CP167_AC_WarlockDoomCoil`, `CP168_AfflictionMultiTargetRotation`, `CP168_AfflictionIndependentTargetDebuffs` | **Complete / numeric provisional** |
| Two/three independent enemy targets, mixed Curses/Banes and other-caster ownership | `WarlockAdaptiveRotation` uses shared `RotationTargetSelector.Fixed` with opt-in `SpreadAfflictionDots` or `AfflictionAdditionalTargetPriority`; owner-aware `RotationConditionEvaluator`; `CP168_SourceOwnedAuraConditions`, `CP168_AfflictionMultiTargetRotation`, `CP168_AfflictionIndependentTargetDebuffs`, `CP167_AX_CurseAndBaneOwnership` | **Complete** |
| Elements/Recklessness, Curse/Bane exclusivity and suppression | `WarlockCurseOwnershipProcessor`, `WarlockBaneOfHavocProcessor`, `WarlockRollResolver`; actual armor reduction/target damage in `CP167_AX`, `CP166_Stage4B_HunterWarlockAudit`; `CP168_AfflictionSuppressionThreat` ranks 0/1/3/5 and healing threat | **Complete for DPS / magic resistance numerical provisional** |
| Nightfall/Shadow Trance proc, instant bolt, early removal and no phantom consumption | `WarlockNightfallProcessor` and `CP167_Z_WarlockNightfall` + `CP168_AfflictionNightfallRemoval` | **Complete / aura lifetime provisional** |
| Drain Life/Soul/Wrack, damage-driven healing, Soul Siphon, interruption, shards and Harvest | `WarlockIncomingCastInterruptionProcessor`, `WarlockDrainSoulShardProcessor`, `WarlockSoulHarvestProcessor`, `DamageToSelfHealingProcessor`; `CP167_AE_ChannelsAndTalents`, `CP167_AN_SoulHarvest`, `CP167_AQ_DpsProcChannelPushback`, `CP168_AfflictionSoulSiphonCurse` | **Complete / nonlethal shard chance provisional** |
| Improved Drains ranks 0–3 | `ForeverWarlockDpsSpellbook` exact 7/13/20% on Drain Life, Drain Soul, Wrack; `CP168_AfflictionImprovedDrains` checks actual tick damage all ranks | **Complete** |
| Pandemic ranks 0–3 and eligible critical effects | `WarlockRollResolver` exact 33/67/100% crit bonus on Corruption, Agony, Doom, Siphon Life, Drain Life, Drain Soul, Wrack; `CP168_AfflictionPandemicCrits` tests seven actual DoT/channel paths | **Complete** |
| Wrack other-Shadow-DoT vulnerability | `Warlock70425DamageModifiers` + `CP168_AfflictionWrackVulnerability` positive/negative masks and actual Siphon Life damage | **Complete** |
| Shadow Mastery on all supported owner Shadow attacks, including Drain Soul/Death Coil | `ForeverWarlockDpsSpellbook`, `WarlockRollResolver`; `CP168_AfflictionShadowMasteryCoverage` damage/healing rank 0 vs 5 | **Complete** |
| Amplify Curse and other periodic talents | `WarlockAmplifyCurseProcessor`, `CP167_AM_AmplifyCurse`, `CP168_AfflictionAmplifyOwnership`; `CP167_V_MageWarlockTalentCertification` covers Corruption, Malevolence and other ranks | **Complete for Agony DPS portion** |
| Talent cap/prerequisites and usable presets | `ForeverWarlockTalentBuild` requires Siphon Life before Wrack; `CP168_AfflictionWrackPrerequisite`, `CP156_DefaultTalentPresets`, `CP157_MarksmanshipAndRosterSmoke`, plus inherited Nightfall/Siphon validation | **Complete within modeled talent set** |
| Resource/threat/damage summaries and API serialization | `WarlockSimRunner`, `WarlockDemonThreat`, shared `HealingThreatResolver`; CP167 drain/finisher and CP168 multi-target/suppression test suites assert results and JSON. Owner and pet threat kept distinct. | **Complete for solo model** |

**Numerically provisional / deferred (not hidden defects):** Agony stage weights pending live log validation; unpublished periodic crit/partial resistance and snapshotted-versus-dynamic server coefficients; Bane of Doom conditional Doomguard spawn on qualifying death (chance unknown); source-specific resistance reduction distribution for Elements; Drain Soul nonlethal shard probability (configurable); Nightfall Shadow Trance duration; geometry/range for Grim Reach and other PvP-control or stand-alone defensive Curse/ward effects; equipment-dependent Spellstone and other temporary enchants (explicitly reserved for the later equipment/modifier project). Cross-player raid debuff distribution and pet/raid AI remain out of this solo certification. **Bane of Havoc belongs to Destruction talent coverage and remains in that future certification gate.**

**Result:** No currently identified high-impact, independently correctable *Affliction solo DPS* defect remains in the reviewed executable paths. This is a scoped mechanical certification, not a claim that unpublished Forever server formulas or every defensive/raid interaction is numerically validated.

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

### CP168 Demonology implementation and execution evidence — October 29, 2026

**Source baseline:** the [Forever 70532 Warlock tree](https://theforeverera.com/en/talents/warlock/), [Demonic Brand](https://theforeverera.com/en/talents/warlock/demonic-brand/), [Demonic Pact](https://theforeverera.com/en/talents/warlock/demonic-pact/), [Soul Link](https://theforeverera.com/en/talents/warlock/soul-link/), [Demonic Knowledge](https://theforeverera.com/en/talents/warlock/demonic-knowledge/), [Master Demonologist](https://theforeverera.com/en/talents/warlock/master-demonologist/), [Improved Health Funnel](https://theforeverera.com/en/talents/warlock/improved-health-funnel/), [Demonic Embrace](https://theforeverera.com/en/talents/warlock/demonic-embrace/) and [Demonic Aegis](https://theforeverera.com/en/talents/warlock/demonic-aegis/) entries (build 1.60.1.70532, dated October 24), plus Blizzard's [Warlock deep dive](https://worldofwarcraft.blizzard.com/en-us/news/24310968/world-of-warcraft-forever-explorando-a-fundo-as-classes-ladino-e-bruxo). These ground *tooltip intent*, not hidden server probabilities. Pet family scaling coefficients remain explicitly provisional.

**Source → execution → outcome:**

| Subsystem | Executable correction / existing path | Deterministic evidence | Status |
|---|---|---|---|
| Brand target and pet ownership | `WarlockDemonicBrandProcessor` validates live branded aura *and* active companion before spending 2/4/6 charges; no stale despawn/dispel proc | `CP168_DemonologyBrandLifecycle`, `CP167_H_WarlockBrandAndShadowburn`, `CP167_BB_SevenDemonBrandOwnership` | Tested / damage coefficient provisional |
| Soul Link | `ScriptedCasterIncomingDamageProcessor` optional transfer hook → `WarlockSimRunner` 30% post-mitigation redirect → target demon `ActorDied` event → existing `WarlockDemonCompanion.Leave` cleanup. Only five permanent families eligible; owner/pet damage +3% uses active-permanent gate | `CP168_DemonologySoulLinkTransfers` including all five, Infernal/Doomguard exclusion, lethal transfer; `CP167_BJ_WarlockDpsTransitions` owner bonus removed after death | Tested / solo prebuff assumption |
| Master Demonologist | `WarlockRollResolver` 2% per-rank Fire (Imp) and Shadow (Sayaad) damage bonuses; scripted owner/pet incoming hits apply 2%/rank physical (Voidwalker) or magic (Felhunter) reduction; recipient mitigation applies after Soul Link redirect | `CP168_DemonologyMasterDemonologist`, `CP168_DemonologySoulLinkTransfers`, inherited talent damage comparisons | Tested |
| Demonic Knowledge | `WarlockSimRunner` starts without an unengaged pet's spell-power bonus; `WarlockDemonCompanion` updates live owner spell definitions on engagement/death. Pet spell power and owner hit/crit inheritance retained | `CP168_DemonologyKnowledgeTransitions`, `CP168_DemonologyCritInheritance`, inherited pet hit tests | Tested / pet spell-power transfer coefficient provisional |
| Crit, damage and report attribution | `WarlockRollResolver` reads owner crit for demon spell attacks; Warlock-only physical creature rule binds owner-derived crit stat. Per-pet event source retains actual family/target; separate owner/pet/threat totals | `CP168_DemonologyCritInheritance`, `CP168_SevenDemonOffensiveCertification`, `WarlockPostCp165CombatCorrections` | Tested |
| Fel Vitality, Unholy Power, improved pet attacks | Shared `CompanionActorFactory` health/Mana inheritance, live roll multipliers and existing demon spellbook rank scaling | `CP164_WarlockDemonCompanions`, `CP166_Stage4B_HunterWarlockAudit`, `CP167_AO_SevenDemonSmoke` | Tested / generic coefficient provisional |
| Demonic Pact and Sacrifice | `WarlockSacrificeRules`: 2-hour family aura persists on different-family summon with Pact, removed when same family returns; original pet death/replacement updates damage buffs | `CP167_BJ_WarlockDpsTransitions`, `CP165_WarlockPrePullSacrifice`, `CP164_WarlockDemonCompanions` | Tested |
| Decimation and Soul Fire | `WarlockDecimationProcessor`: correct below-35%-before-hit proc from Shadow Bolt/Searing Pain, rank 45/90% cooldown and 20/40% cast-time, shard-free 10s window, cast-start cost reservation | `CP167_S_WarlockDecimation` threshold, rank, JSON, cast/shard results | Tested / server edge policy provisional |
| Demonic Energies, Health Funnel | `WarlockDemonicEnergiesProcessor`: source-owned damage heals active pet, Life Tap Mana passes through pet cap. `WarlockHealthFunnelProcessor`: real channel ticks and HP transfer. `WarlockDemonThreat` now applies Improved Health Funnel's 50/100% **threat** reduction in addition to existing 20/40% healing and 15/30% cost bonuses | `CP167_AV_DemonicEnergies`, `CP167_AU_HealthFunnelPetDps`, `CP168_DemonologyHealthFunnelThreat` | Tested / base healing-threat coefficient provisional |
| Felhunter Devour Magic | `WarlockDemonCompanion` purges actual target aura before granting 618 base effective pet healing, boosted by Improved Felhunter 10/20/30%; never heals on an absent aura. Base spell cost 215 Mana | `CP168_FelhunterDevourMagicHealing` actual purge, raw/effective heal, over-heal, cost, negative condition; [client spell record](https://questing.wiki/wow-forever/spells/19736/) | Tested |
| Demonic Embrace and Aegis | `ForeverWarlockTalentBuild` accepts correct 5/2 ranks and tier constraints. `WarlockSimRunner` increases runtime Stamina and derived max HP for Embrace; assumes 10 HP / Stamina at level 60 pending explicit Forever evidence | `CP168_DemonologyTalentStaminaAndRanks`; Aegis buff itself deferred as standalone Demon Skin/Armor defense | Embrace tested / health conversion provisional; Aegis defensively deferred |
| Summoning and cooldowns | `WarlockReplacementSummonProcessor` uses real owner cast, 10s baseline reduced by Master Summoner and Fel Domination, Mana/shard costs and lost pet uptime. Expiry stops temporary summons | `CP167_AS_DemonSummonTiming`, `CP164_WarlockDemonCompanions`, `CP168_SevenDemonOffensiveCertification` | Tested |

**Family-by-family evidence** (not just construction; actual positive Damage, actor-origin, pet hit/crit, special resource/cooldown, threat, death and expiration where applicable):

| Demon | Real offensive path | Evidence | Status |
|---|---|---|---|
| Imp | Firebolt spell, inherited crit/hit/SP, pet Mana spend; no generic melee | `CP168_SevenDemonOffensiveCertification`, `CP168_DemonologyCritInheritance`, `CP164_WarlockDemonCompanions` | Offensive-tested |
| Voidwalker | Creature melee, Torment/Suffering threat and rank scaling; physical Master mitigation | `CP168_SevenDemonOffensiveCertification`, `CP168_DemonologyMasterDemonologist`, `CP167_BB_SevenDemonBrandOwnership` | Offensive-tested |
| Succubus | Shadow Lash and melee, Sayaad damage talents, shared crit/inheritance | Same 7-demon/crit suites; `CP164_WarlockDemonCompanions` | Offensive-tested |
| Incubus | Independent Sayaad actor, Shadow Lash and melee, crit, target-owned Brand | Same 7-demon/crit suites; `CP167_BB_SevenDemonBrandOwnership` | Offensive-tested |
| Felhunter | Shadow Bite/melee, magic Master mitigation, Devour Magic heal, Spell Lock when enabled | 7-demon/crit/Master/Devour suites; `CP167_BB_SevenDemonBrandOwnership` | Offensive-tested |
| Infernal | Temporary melee and Fire Immolation periodic attack; no post-despawn/late Brand/Soul Link | 7-demon suite, `CP167_AO_SevenDemonSmoke`, `CP167_BB_SevenDemonBrandOwnership` | Offensive-tested |
| Doomguard | Temporary melee and Shadow Doom Bolt; Mana/crit; no post-despawn/Soul Link | 7-demon suite, `CP167_AO_SevenDemonSmoke`, `CP167_BB_SevenDemonBrandOwnership` | Offensive-tested |

**Explicit DPS certification boundaries:** owner Soul Link is treated as an already-applied pet prebuff for the solo simulator (not a separate raid utility GCD); full group recipients for raid Pact, positional pet control, Seduction/Spell Lock encounter AI, Felhunter Tainted Blood AP reduction, Voidwalker Consume Shadows/Sacrifice defenses, Demon Skin/Armor and Demonic Aegis defensive effects, pet raid aggro AI, temporary summon geometry and unsupported server-only coefficients are deferred. The base healing-threat coefficient, owner/pet SP-transfer curves, Demonic Embrace HP conversion and Brand proc damage coefficient remain provisional. No removed Classic-only demon talents reintroduced.


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
| Affliction | Runtime spell/talent, owner/target, threat and resource paths above | CP167/CP168 outcome suites, including 1–3 targets | #40906779476: 2,136 passed, 0 warnings | Numeric/server-only and encounter deferred as above | **CERTIFIED — supported solo DPS** |
| Demonology + seven demons | Pending | Pending | Pending | Offensive and lifecycle certification | OPEN |
| Destruction | Pending | Pending | Pending | All DPS talents/spells and self-damage accounting | OPEN |
| Cross-engine regression | Pending | Pending | Pending | Root-cause audit as issues are found | OPEN |

**Merge gate:** full .NET 10 Release build and tests, zero warnings/failures, source-backed status updates in `CP167-class-completeness-inventory.md` and `CP166-coverage-matrix.md`, owner local validation, explicit owner approval. The numerical-provisional status of unobserved server coefficients must remain explicit.
