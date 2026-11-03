# CP175-F3C0 — Warlock source and talent-mechanics certification baseline

**As reviewed:** 2026-11-03, current published client tree **1.60.1.70753**. Source: [ForeverChanges tree](https://foreverchanges.pro/talents/warlock), cross-check [pinned 70707 talent source](https://forevermastery.com/en/tools/talents/warlock/). **Important distinction:** [November 2 Blizzard development-note changes](https://us.forums.blizzard.com/en/wow/t/warlock-design-that-needs-fixing-before-launch/2399235/17) announce Wrack, Shadow and Flame and pet Health scaling adjustments that some client-derived tooltips still show at their *older* values; simulator implementation uses those published upcoming values **provisionally**. Neither source alone proves live-server damage.

**Definitions:** 52 current talents: Affliction 17, Demonology 19, Destruction 16. Current typed `ForeverWarlockTalentBuild` contains **52 selectable keys**; **51 have at least some existing source/handler evidence** and `curse-of-exhaustion` is the newly recognized nonexecuting source key. In addition, some selectable keys (e.g. Demonic Aegis, Grim Reach, Destructive Reach, Pyroclasm) have **no** runtime effects; an API entry never establishes execution.

**Count by principal Solo DPS implementation (conservative):** 30 fully modeled, 17 partially modeled, 5 not modeled. Full means the identified principal effect actually executes in the supported runner, **not** an assertion of complete raid/PvP/targeting semantics.

| Tree | Fully modeled | Partially modeled | Not modeled | Total |
|---|---:|---:|---:|---:|
| affliction | 11 | 4 | 2 | 17 |
| demonology | 8 | 10 | 1 | 19 |
| destruction | 12 | 2 | 2 | 16 |
| **Total** | **31** | **16** | **5** | **52** |

## Practical gap categories

**A** — missing core Solo DPS/DoT/resource/pet-damage, **B** — defensive/healing/threat, **C** — encounter/targeting/geometry, **D** — specific source uncertainty, **E** — further event/browser validation. Tags may overlap. **All 52 entries carry provisional source confidence**, including mechanically full ones; D is a *specific outstanding uncertainty*, not merely that all values come from beta.

| Flag | Count |
|---|---:|
| A | 1 |
| B | 13 |
| C | 17 |
| D | 7 |
| E | 10 |

## All current Warlock talent rows

**Talent ID:** the current talent/spell numeric ID was not established from the retrieved source for every row; no IDs are fabricated. Refer to linked calculator for client spell records. **Row:** 1-based current tier from the 70753 calculator. **Prerequisite:** only confirmed explicitly enforced engine dependencies are named; an unlisted client-specific prerequisite is not assumed absent. **Typed/Frontend:** all rows have a typed talent rank and are exposed in the current catalog with provisional data and independent implementation status. Some 70753 ranks and spell identifiers have not been individually confirmed by combat logs.

| Tree | Current talent key | Max rank | Row | Explicit dependency (where known) | Typed/UI | Mechanical status | Gap flags | Handler and inspected evidence | Remaining limitation |
|---|---|---:|---:|---|---|---|---|---|---|
| affliction | `improved-life-tap` | 2 | 1 | — | Yes | fully-modeled | — | ForeverWarlockDpsSpellbook; CP167_D_WarlockLifeAndFinishers | Life Tap gain and rank-cost receipt |
| affliction | `suppression` | 5 | 1 | — | Yes | fully-modeled | — | WarlockSimRunner/WarlockDemonThreat; CP168_AfflictionSuppressionThreat | Spell hit and owner threat, not pet threat |
| affliction | `improved-corruption` | 5 | 1 | — | Yes | fully-modeled | — | ForeverWarlockDpsSpellbook; CP168_AfflictionPandemicCrits | Corruption cast time and tick scaling |
| affliction | `grim-reach` | 2 | 2 | — | Yes | not-modeled | C | No range/position engine | Spell range geometry absent |
| affliction | `soul-harvest` | 2 | 2 | — | Yes | partially-modeled | C,E | WarlockSoulHarvestProcessor; CP168 source regression | Kill-trigger regen executes; eligibility edge cases |
| affliction | `improved-drains` | 3 | 2 | — | Yes | fully-modeled | C,D | Drain Mana channel and resource processor; CP175_F3C1AfflictionMechanics | Only explicit finite target Mana; source tuning provisional |
| affliction | `improved-bane-of-agony` | 2 | 3 | — | Yes | fully-modeled | D | ForeverWarlockDpsSpellbook; CP167_AW_AgonyRankAndRamp | Agony tick damage and ramp; dynamic source provisional |
| affliction | `fel-concentration` | 3 | 3 | — | Yes | partially-modeled | B,E | WarlockIncomingCastInterruptionProcessor; CP167_AQ | Channel interruption/pushback modeled with encounter limitations |
| affliction | `amplify-curse` | 1 | 3 | — | Yes | partially-modeled | B,C | WarlockAmplifyCurseProcessor; CP168_AfflictionAmplifyOwnership | Agony amplification works, Weakness/Exhaustion absent |
| affliction | `pandemic` | 3 | 3 | — | Yes | fully-modeled | D | WarlockSimRunner crit resolver; CP168_AfflictionPandemicCrits | Ranked Affliction periodic crit bonus executes |
| affliction | `malevolence` | 5 | 4 | — | Yes | fully-modeled | — | WarlockSimRunner crit resolver; CP168 | Shadow critical effect chance executes |
| affliction | `nightfall` | 2 | 4 | — | Yes | fully-modeled | — | WarlockNightfallProcessor; CP168_AfflictionNightfallRemoval | Source-specific damage procs and instant Shadow Bolt consumption |
| affliction | `curse-of-exhaustion` | 1 | 4 | — | Yes | not-modeled | C | No combat action; typed rank only | No executable 30% movement slow; current rank selectable with warning |
| affliction | `siphon-life` | 1 | 5 | — | Yes | partially-modeled | B,E | ForeverWarlockDpsSpellbook; DamageToSelfHealingProcessor | Target-owned periodic damage and owner healing execute; edge context limited |
| affliction | `soul-siphon` | 3 | 5 | — | Yes | fully-modeled | — | WarlockSimRunner; CP168_AfflictionSoulSiphonCurse | Target-affliction effect count, drain damage cap |
| affliction | `shadow-mastery` | 5 | 6 | — | Yes | fully-modeled | — | ForeverWarlockDpsSpellbook; CP168_AfflictionShadowMasteryCoverage | Shadow damage and drain scaling |
| affliction | `wrack` | 1 | 7 | siphon-life 1/1 | Yes | fully-modeled | D | ForeverWarlockDpsSpellbook; CP175_F3C0WarlockSourceAndCombatCorrections | Six channel ticks and DoT vulnerability; announced 86 + 0.286 SP per tick |
| demonology | `improved-health-funnel` | 2 | 1 | — | Yes | partially-modeled | B,E | WarlockHealthFunnelProcessor; CP168_DemonologyHealthFunnelThreat | Pet healing, owner cost/threat, incomplete emergency contexts |
| demonology | `improved-imp` | 3 | 1 | — | Yes | partially-modeled | B,C | ForeverWarlockDemonSpellbook; CP168_SevenDemonOffensiveCertification | Imp Firebolt damage works; Fire Shield absent |
| demonology | `demonic-embrace` | 5 | 1 | — | Yes | fully-modeled | D | WarlockSimRunner; CP168_DemonologyTalentStaminaAndRanks | Owner effective Stamina and health; demon inherits effective Stamina |
| demonology | `unholy-power` | 5 | 1 | — | Yes | fully-modeled | — | WarlockSimRunner pet-roll resolver; CP168_SevenDemonOffensiveCertification | Ranked pet damage for eligible permanent demons |
| demonology | `demonic-aegis` | 2 | 2 | — | Yes | not-modeled | B | No Demon Skin/Armor runtime implementation | Demon Armor/Skin effectiveness unimplemented |
| demonology | `improved-voidwalker` | 3 | 2 | — | Yes | partially-modeled | B,C | WarlockDemonCompanion; CP167_AH_Voidwalker | Torment/Suffering work; Consume Shadows/Sacrifice/AI incomplete |
| demonology | `fel-vitality` | 3 | 2 | — | Yes | fully-modeled | D | WarlockSimRunner/WarlockDemonCompanion; CP168_DemonologyTalentStaminaAndRanks | Owner Mana, eligible pet maximum Health/Mana rank scaling |
| demonology | `demonic-energies` | 2 | 2 | — | Yes | fully-modeled | — | WarlockDemonicEnergiesProcessor; CP167_AV_DemonicEnergies | Actual spell-damage pet healing and Life Tap Mana |
| demonology | `improved-sayaad` | 3 | 3 | — | Yes | partially-modeled | C,E | ForeverWarlockDemonSpellbook; CP164 | Lash rank scales; Seduction/Soothing Kiss effects incomplete |
| demonology | `demonic-sacrifice` | 1 | 3 | — | Yes | fully-modeled | — | WarlockPrePullSacrificeProcessor; CP165 | Pre-pull family buffs and cancellation/extension |
| demonology | `master-summoner` | 2 | 3 | — | Yes | partially-modeled | C,E | ForeverWarlockSummonSpellbook; CP168 | Replacement summon cost/time modeled; general summoning absent |
| demonology | `decimation` | 2 | 4 | — | Yes | fully-modeled | — | WarlockDecimationProcessor; CP168_DemonologyKnowledgeTransitions | Execute-window Soul Fire cooldown, shard and cast effects |
| demonology | `fel-domination` | 1 | 4 | master-summoner 2/2 | Yes | partially-modeled | C,E | WarlockReplacementSummonProcessor; CP168 | Replacement summon discount modeled; broad summon unavailable |
| demonology | `demonic-brand` | 3 | 4 | — | Yes | partially-modeled | B,C | WarlockDemonicBrandProcessor; CP168_DemonologyBrandLifecycle | Source-owned Searing Pain pet attacks/threat; enemy targeting partial |
| demonology | `improved-felhunter` | 3 | 5 | — | Yes | partially-modeled | B,C | ForeverWarlockDemonSpellbook; CP168_FelhunterDevourMagicHealing | Devour healing/Spell Lock CD; Tainted Blood/Paranoia missing |
| demonology | `soul-link` | 1 | 5 | demonic-sacrifice 1/1 | Yes | partially-modeled | B,C | WarlockSimRunner damage redirect; CP168_DemonologySoulLinkTransfers | Damage redirect and owner/pet damage; incoming scenarios limited |
| demonology | `demonic-knowledge` | 3 | 5 | — | Yes | fully-modeled | D | Warlock70425DamageModifiers/WarlockDemonCompanion; CP168_DemonologyKnowledgeTransitions | Active-pet owner and demon spell damage |
| demonology | `master-demonologist` | 5 | 6 | — | Yes | partially-modeled | B,C | WarlockSimRunner and pet roll resolver; CP168_DemonologyMasterDemonologist | Pet-family owner/pet offense/mitigation; full encounter partial |
| demonology | `demonic-pact` | 1 | 7 | soul-link 1/1 | Yes | fully-modeled | — | WarlockPrePullSacrificeProcessor; CP165 | Demonic Sacrifice continues after qualified replacement |
| destruction | `destructive-reach` | 2 | 1 | — | Yes | not-modeled | C | No spell range/geometry engine | No spell range geometry |
| destruction | `improved-shadow-bolt` | 5 | 1 | — | Yes | fully-modeled | — | WarlockImprovedShadowBoltProcessor; CP168_DestructionImprovedShadowBoltOwnership | Landed crit 12-second target-owned Shadow debuff |
| destruction | `bane` | 5 | 1 | — | Yes | fully-modeled | — | ForeverWarlockDpsSpellbook; CP168_DestructionSpellbookAndCastGates | Ranked cast-time reductions across named Destruction spells |
| destruction | `molten-skin` | 5 | 2 | — | Yes | fully-modeled | B,E | Warlock incoming damage processor; CP168 | Rank-based incoming mitigation for scripted encounters |
| destruction | `cataclysm` | 3 | 2 | — | Yes | fully-modeled | — | ForeverWarlockDpsSpellbook; CP168_DestructionCostCompleteness | Ranked Destruction Mana reduction |
| destruction | `aftermath` | 5 | 2 | — | Yes | partially-modeled | C | ForeverWarlockDpsSpellbook; CP168 | Immolate impact bonus executes; Conflagrate Daze missing |
| destruction | `ruin` | 5 | 3 | — | Yes | fully-modeled | — | WarlockSimRunner crit resolver; CP168_DestructionCritAndDamageTalentOutcomes | Destruction critical bonus scaling |
| destruction | `shadowburn` | 1 | 3 | — | Yes | fully-modeled | — | WarlockShadowburnShardProcessor; CP168_DestructionShadowburnDeathWindow | Actual Soul Shard cost and 8-second kill refund |
| destruction | `intensity` | 3 | 4 | — | Yes | partially-modeled | B,E | WarlockIncomingCastInterruptionProcessor; CP168 | Pushback protection in supported incoming attacks |
| destruction | `agonizing-flames` | 3 | 4 | — | Yes | fully-modeled | — | Warlock70425DamageModifiers; CP168_DestructionCritAndDamageTalentOutcomes | Searing Pain crit and Destruction damage |
| destruction | `conflagrate` | 1 | 4 | — | Yes | fully-modeled | — | WarlockConflagrateConsumptionProcessor; CP168_DestructionRanksAndProcs | Talent gating, landed hit and target-owner Immolate consumption |
| destruction | `pyroclasm` | 2 | 5 | intensity 3/3 | Yes | not-modeled | C | No stun-on-damage proc runtime | AoE/Soul Fire stun proc and enemy control unimplemented |
| destruction | `bane-of-havoc` | 1 | 5 | — | Yes | fully-modeled | C | WarlockBaneOfHavocProcessor; CP168_DestructionAoEHavocIntegration | 15% explicit other-target damage echo, no spatial geometry |
| destruction | `fire-and-brimstone` | 3 | 5 | conflagrate 1/1 | Yes | fully-modeled | — | WarlockSimRunner crit resolver; CP168 | Conflagrate crit chance per rank |
| destruction | `shadow-and-flame` | 5 | 6 | — | Yes | fully-modeled | D | WarlockShadowAndFlameProcessor; CP175_F3C0 | Announced 4%/rank school buffs, landed aura, retention/refund |
| destruction | `incinerate` | 1 | 7 | bane-of-havoc 1/1 | Yes | fully-modeled | — | ForeverWarlockDpsSpellbook; CP168_DestructionSpellbookAndCastGates | Learned Incinerate and own-Immolate 25% extra damage |

## Bounded implementation receipts / data provenance

1. **Wrack (A correction):** The announcement gives 86 Shadow damage per second at level 60 and +0.286 of spell power per tick. Previously the runtime used 115 + 0.12 of spell power. The new tuning values apply in the *existing* six-tick channel; actual positive hit and mana/resource event regression is under `CP175_F3C0WarlockSourceAndCombatCorrections.cs`.
2. **Improved Drains (A correction):** The October 30 client changed this to include **Drain Mana**, replacing **Wrack**. Existing Drain Life / Drain Soul effects retain actual 7/13/20% ranks. F3C0 removes a fictitious Wrack multiplier; Drain Mana is **not modeled** and must not appear as a selected executable action.
3. **Shadow and Flame (A correction):** Nov 2's announced 4/8/12/16/20% Fire/Shadow boost replaces 2/4/6/8/10%. Aura generation still requires landed Conflagrate/Shadowburn; its separate Immolate retention/shard refunds are preserved. Values are marked **announcement-provisional**.
4. **Seven demons (B correction):** Summoned demon maximum health uses 2 Health per owner effective Stamina rather than no such inheritance in the current implementation. `Fel Vitality` and family scaling still apply through the shared `CompanionActorFactory`. Extra protection and conditional owner/buff recomputation require targeted F3C2 certification.
5. **Curse of Exhaustion (C gap, not fabricated):** Now recognized as a rank-one current Affliction talent, consumes an itemized point at its proper tier, and displays **not implemented**. Its 30%/12s movement slow does not execute in the stationary model; a stale `curse-of-exhaustion` custom priority still fails.
6. **Previously supported mechanics:** Nightfall, Pandemic, Soul Link, Decimation, Demonic Sacrifice, Bane of Havoc, Conflagrate, demon damage and seven demon families have existing CP168 numerical/event integration evidence. These are not rewritten during F3C0.

## Next checkpoint ordering

**F3C1 Affliction:** Full Drain Mana integration if an acceptable source is established; dynamic Bane of Agony tick re-evaluation, Drain Soul shard-generation chance after announced reduction and low-rank Nightfall trigger exclusion. Preserve DoT owner/target identity.

**F3C2 Demonology:** Improved Health Funnel, Imp Fire Shield, Demonic Aegis, Voidwalker and Felhunter utility; validate live timed owner-Stamina changes to pet Health, additional threat/mitigation against scripted encounter hits, demon replacement and five-permanent-vs-two-temporary eligibility.

**F3C3 Destruction:** Pyroclasm missing stun proc (only when actual control/interruption semantics exist), Aftermath daze, eligible Shadow and Flame target/school restrictions and Conflagrate conditions. Keep range and spatial targeting a category C limitation without falsifying core DPS counts.

## Limits and current evidence gate

**Source confidence ≠ executable status**: an announced number may be run provisionally and still have mechanically full modeled behavior. Do not claim 52 talents independently in-game-verified, nor claim source-derived runtime handlers execute in unrepresented PvP/raid situations. This is a baseline not a Warlock class completion gate.

Browser captures and exact-final-head Actions links belong in the PR completion comment **only after the final workflow finishes**.


## F3C1 — Affliction functional integration (2026-11-03)

- **Trained Drain Mana:** source: [Forever rank-4 spell 11704](https://raiddex.com/en/wow-forever/spells/drain-mana-rank-4), cross-checked with [Forever spell changes](https://foreverdiff.com/spells/drain-mana/). At level 60, 310 Mana cost, 5-second channel, one tick per second, 136 target Mana transferred per tick before talents. Earlier ranks are 42/68/99 per tick (IDs 5138/6226/11703); 11704 is rank 4. The existing channel scheduler runs the spell, and a dedicated event processor transfers finite target Mana without inventing a boss Mana pool. Opt-in `targetStartingMana` is a controlled encounter fixture; ordinary Solo DPS has no target Mana and cannot recover Mana from Drain Mana. No full enemy resource AI or geometry is claimed. Improved Drains 7/13/20% now applies to transfer.
- **Bane of Agony:** November 2 notes explicitly identify dynamically updated damage. The existing 12 × 2-second, three-stage ramp and target-owned periodic engine are retained, with its spell-power coefficient read dynamically from the actor on each tick. DoT shape and coefficient remain provisional until in-game numerical confirmation; no new scheduler was created. Existing source-owned refresh/independent target behavior remains.
- **Drain Soul:** November 2 notes specify that Soul Shards can be generated upon dealing damage rather than on a kill and that the chance was significantly reduced. Removed the earlier guaranteed-kill path; shard chance stays a user-provided test-fixture parameter until a verified value exists. Valid positive channel damage, eligibility and available inventory space still gate actual generation. Duration-driven boss scripts do not imply death.
- **Nightfall:** eligible landed Corruption periodic or Drain Soul/Life/Wrack channel damage can proc; direct damage or incorrect delivery cannot. Existing 2/4% ranks, 10-second provisional lifetime, instant Shadow Bolt and aura consumption remain.
- **Cross-tree:** Drain Mana remains trained and works regardless of starting specialization; Improved Drains is rank-scoped. Existing off-tree ability/priority validation is reused.
- **Coverage:** Affliction 11 mechanically full / 4 partially modeled / 2 not modeled (was 10 / 5 / 2); only Improved Drains changes principal status. Encounter-dependent missing target Mana is flagged C, source-dependent coefficients D. No Demonology or Destruction reclassification. This is NOT a claim of complete Affliction utility or server-verified numerical tuning.
- **Regression/CI:** CP175_F3C1AfflictionMechanics provides resource receipts, depleted/no-target Mana, rank scaling, shard eligibility, Nightfall delivery exclusion, live Agony coefficient and seeded replay. Running the full suites, browser screenshots, and exact-head CI remain acceptance gates; do not mark passed without actual results.

Sources: [November 2 changes discussed November 3](https://us.forums.blizzard.com/en/wow/t/latest-warlock-changes/2399400), [Forever rank 4](https://raiddex.com/en/wow-forever/spells/drain-mana-rank-4), [Forever spell comparison](https://foreverdiff.com/spells/drain-mana/). Provisional client release 1.60.1.70753; published notes are not live-server validation.
