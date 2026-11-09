# CP175-F3D0 — Hunter talent coverage and Solo DPS combat-gap baseline

**Review:** November 3, 2026. **Ruleset:** WoW Forever Hunter client 1.60.1.70753, with November 3 announced balance updates. **Repository:** `layoung3/WarcraftSim`, branch `cp175-shared-character-simulation`; checkpoint starts at `93b6cefe1a30e9d27b5cd2d5a33eb6db55f087e5` (CI #41690789865, success). **Not a claim of server-certified combat values.**

**Primary current client:** [ForeverChanges Hunter talents, 1.60.1.70753](https://foreverchanges.pro/talents/hunter), updated November 3. [Spellbook comparison](https://foreverchanges.pro/spellbook/hunter).

**Developer notes:** [Blizzard Forever Hunter and Druid class deep dive](https://news.blizzard.com/en-us/article/24301515/world-of-warcraft-forever-class-deep-dives-hunter-and-druid) and [Beta Development Notes, November 2/3](https://www.bluetracker.gg/wow/topic/us-en/2360696-wow-forever-beta-development-notes-updated-november-2/).

**Historical comparison only:** repository `WarcraftSim.Api/Catalog/forever-talent-visual-reference-v1.json`, pinned historical WoWSims export. Icons and row-position references do not prove mechanics. Most numeric talent spell IDs were **not independently recovered**; leave blank rather than fabricate.

## Talent count and mechanical classification

There are **50 distinct Hunter talents** in the current client: 16 Beast Mastery, 16 Marksmanship and 18 Survival. The simulator already has many executed mechanics but **declaring a talent key in typed metadata is not proof it executes**. Coverage is conservative and identifies principal core combat, not universal raid/PvP support.

| Talent tree | Current talents | Fully modeled | Partially modeled | Not modeled |
|---|---:|---:|---:|---:|
| Beast Mastery | 16 | 11 | 4 | 1 |
| Marksmanship | 16 | 9 | 5 | 2 |
| Survival | 18 | 9 | 9 | 0 |
| **Total** | 50 | 29 | 18 | 3 |

**Core stationary Solo DPS versus total combat (after F3D2):** `0` talent rows carry an explicit **A (core DPS)** gap; the remaining partial/unmodeled rows mainly concern defensive, movement, trap targeting or enemy AI. Not every "not modeled" row is a DPS omission. Numerical source coefficients, pet family values and some inter-tree allocation/validation still require further certification.

**Gap categories:** A = core DPS/damage/resource/proc; B = defensive/healing/threat; C = encounter movement/range/control/pathing; D = source coefficient provisional; E = validation. Current row flags: A 0, B 6, C 22, D 18, E 3. A blank gap means implementation status is full for its principal supported functionality.

**Source ID handling:** The current source page exposes names, rank caps and tooltips, but the complete underlying numerical talent-spell ID mapping has not yet been authoritatively reconciled in this repository. `unverified` in the Source ID column is intentional, not a guessed spell ID. All rows are sourced to current 70753 tree and tagged **data confidence = provisional** pending live-server validation. Current per-row itemized frontend and native typed dictionaries exist; **cross-tree integration is partial and separate from simple catalog exposure**.

## Fifty current talents — exact row-by-row implementation matrix

| Talent tree | Engine/source key | Max ranks | Row | Source spell ID | Frontend / typed | Mechanical status | Gap | Actual implementation / remaining limitation |
|---|---|---:|---:|---|---|---|---|---|
| beast-mastery | `deadly-aspects` | 5 | 1 | unverified | typed + itemized | fully-modeled | D | 2%/rank chance of 30% auto-attack haste from eligible Aspect; cast/expiry implemented |
| beast-mastery | `endurance-training` | 5 | 1 | unverified | typed + itemized | fully-modeled | D | 3%/rank pet Health and Armor through shared actor factory |
| beast-mastery | `focused-fire` | 2 | 2 | unverified | typed + itemized | fully-modeled | D | 1%/rank owner and active-pet damage, conditional on active pet |
| beast-mastery | `improved-aspect-of-the-monkey` | 3 | 2 | unverified | typed + itemized | partially-modeled | B,E | Hunter/pet dodge bonuses; constrained incoming-defense scenarios |
| beast-mastery | `improved-revive-pet` | 2 | 2 | unverified | typed + itemized | partially-modeled | B,E | Mana/cast-time and revive execution; more pet-scaling recovery scenarios outstanding |
| beast-mastery | `pathfinding` | 2 | 3 | unverified | typed + itemized | not-modeled | C | Cheetah/Pack travel movement not supported; moved to tier 3 in 70753 |
| beast-mastery | `bestial-swiftness` | 1 | 3 | unverified | typed + itemized | partially-modeled | C | 30% pet movement represented by bounded engage time, not pathing |
| beast-mastery | `unleashed-fury` | 5 | 3 | unverified | typed + itemized | fully-modeled | D | 3%/rank damage on pet and eligible hawk actions |
| beast-mastery | `improved-mend-pet` | 2 | 4 | unverified | typed + itemized | fully-modeled | — | 10%/rank Mana reduction, plus exact 15% (rank 1)/50% (rank 2) cleanse chance on effective Mend Pet healing ticks; removes one tagged hostile Curse/Disease/Magic/Poison aura through shared AuraManager |
| beast-mastery | `ferocity` | 5 | 4 | unverified | typed + itemized | fully-modeled | D | 2%/rank pet and hawk critical-strike chance |
| beast-mastery | `summon-hawk` | 1 | 4 | unverified | typed + itemized | fully-modeled | C,D | Owned hawk summons, damage, 18-second lifetime and shared Arcane cooldown |
| beast-mastery | `spirit-bond` | 2 | 5 | unverified | typed + itemized | fully-modeled | D | Periodic owner/pet Health regeneration with active companion |
| beast-mastery | `intimidation` | 1 | 5 | unverified | typed + itemized | partially-modeled | C | Pet next successful attack crit/threat; actual enemy AI stun absent |
| beast-mastery | `bestial-discipline` | 2 | 5 | unverified | typed + itemized | fully-modeled | D | Pet Focus regeneration and owner casting Mana regeneration |
| beast-mastery | `frenzy` | 5 | 6 | unverified | typed + itemized | fully-modeled | D | On-critical pet attack speed proc and timed expiration |
| beast-mastery | `bestial-wrath` | 1 | 7 | unverified | typed + itemized | fully-modeled | C | Pet +50% damage for 18 sec; enemy fear/control assumptions excluded |
| marksmanship | `hawk-eye` | 3 | 1 | unverified | typed + itemized | not-modeled | C | 2 yards/rank ranged reach absent without spatial geometry |
| marksmanship | `improved-concussive-shot` | 5 | 1 | unverified | typed + itemized | not-modeled | C | 4%/rank Concussive Shot stun and enemy interruption absent |
| marksmanship | `lethal-attacks` | 5 | 1 | unverified | typed + itemized | fully-modeled | — | 1%/rank all owner attack critical chance; pet crit inherits owner |
| marksmanship | `improved-stings` | 3 | 2 | unverified | typed + itemized | partially-modeled | C | Serpent Sting modifier active; Viper/Scorpid utility not complete |
| marksmanship | `efficiency` | 5 | 2 | unverified | typed + itemized | fully-modeled | D | 3%/rank Shot/Sting Mana cost scaling; some cross-tree melee costs pending |
| marksmanship | `careful-aim` | 5 | 2 | unverified | typed + itemized | fully-modeled | D | 20%/rank Intellect contribution to owner Attack Power |
| marksmanship | `rapid-killing` | 2 | 3 | unverified | typed + itemized | partially-modeled | C,E | Cooldown reduction and valid kill-triggered next-shot buff; raid kill attribution limited |
| marksmanship | `improved-arcane-shot` | 5 | 3 | unverified | typed + itemized | fully-modeled | — | 0.3 sec/rank Arcane Shot independent cooldown reduction |
| marksmanship | `lone-wolf` | 1 | 3 | unverified | typed + itemized | fully-modeled | C | 20% owner attack damage only without active pet; includes before engage and after loss |
| marksmanship | `trueshot-aura` | 1 | 4 | 20906 | typed + itemized | partially-modeled | C,D | Rank-5 +100 live owner ranged AP, aura application/removal and Hunter-specific pet inheritance; raid distribution unmodeled |
| marksmanship | `mortal-shots` | 5 | 4 | unverified | typed + itemized | fully-modeled | D | 6%/rank ranged ability critical bonus |
| marksmanship | `rapid-recuperation` | 2 | 5 | unverified | typed + itemized | fully-modeled | C | Conditional 15-second casting Mana regeneration after Serpent Sting |
| marksmanship | `barrage` | 3 | 5 | unverified | typed + itemized | fully-modeled | — | 5/10/15% Aimed/Multi/Volley damage; excludes Sniper Shot |
| marksmanship | `scatter-shot` | 1 | 5 | **19503** | typed + itemized + custom action | partially-modeled | C | 8% base Mana, instant 30s CD, 50% ranged weapon damage with live RAP, landed four-second disorient, damage-break, Auto Shot stop; 15 yd positioning and enemy AI absent |
| marksmanship | `ranged-weapon-specialization` | 5 | 6 | unverified | typed + itemized | fully-modeled | — | 1%/rank physical ranged weapon damage |
| marksmanship | `sniper-shot` | 1 | 7 | unverified | typed + itemized | partially-modeled | C,D | 4sec cast/15sec cooldown and rank-3 +590 damage execute; range charges not modeled |
| survival | `improved-tracking` | 5 | 1 | unverified | typed + itemized | fully-modeled | C | 1%/rank damage vs explicit tracked creature type |
| survival | `deflection` | 5 | 1 | unverified | typed + itemized | partially-modeled | B | 1%/rank parry bonus lacks complete incoming physical attacks |
| survival | `entrapment` | 5 | 2 | unverified | typed + itemized | partially-modeled | C | Trap control marker, no enemy root/pathing |
| survival | `savage-strikes` | 2 | 2 | unverified | typed + itemized | fully-modeled | — | 2%/rank melee ability crit chance |
| survival | `survivalist` | 5 | 2 | unverified | typed + itemized | fully-modeled | — | 2%/rank maximum Health |
| survival | `improved-wing-clip` | 3 | 2 | unverified | typed + itemized | partially-modeled | C | Wing Clip damage and root proc; no movement simulation |
| survival | `clever-traps` | 2 | 3 | unverified | typed + itemized | fully-modeled | D | 15%/rank Fire trap damage and Frost duration |
| survival | `surefooted` | 3 | 3 | unverified | typed + itemized | partially-modeled | B,C | Hit chance modeled; movement-impair reduction absent |
| survival | `deterrence` | 1 | 3 | unverified | typed + itemized | partially-modeled | B | Defensive aura and cooldown; enemy attacks constrained |
| survival | `survival-tactics` | 2 | 4 | unverified | typed + itemized | partially-modeled | C | Trap hit effects modeled; Feign Death enemy threat/AI absent |
| survival | `predators-edge` | 5 | 4 | unverified | typed + itemized | fully-modeled | D | 6%/rank melee critical bonus and offhand damage |
| survival | `counterattack` | 1 | 4 | unverified | typed + itemized | partially-modeled | C | Parry-reactive attack and damage execute; root enemy-control absent |
| survival | `resourcefulness` | 2 | 5 | unverified | typed + itemized | fully-modeled | — | 30%/rank melee/trap Mana reduction and crit-gated regen |
| survival | `expose-prey` | 2 | 5 | unverified | typed + itemized | fully-modeled | C | 5%/rank marked-target proc opens Mongoose Bite |
| survival | `survivalists-discipline` | 2 | 5 | unverified | typed + itemized | fully-modeled | — | 20%/rank trap and Deterrence cooldown reduction |
| survival | `strider-kick` | 1 | 5 | unverified | typed + itemized | partially-modeled | C | Real melee strike; 30% movement speed buff not spatially modeled |
| survival | `lightning-reflexes` | 5 | 6 | unverified | typed + itemized | partially-modeled | B,D | 3/6/9/12/15% effective Agility drives 1 melee AP, 2 ranged AP, incremental crit/52.9 and temporary buff reversal; dodge coefficient unverified and optional |
| survival | `lacerating-strikes` | 1 | 7 | unverified | typed + itemized | fully-modeled | D | 40% landed Mongoose Bite damage as seven crit-eligible 3s ticks over 21s, same-owner weaker overwrites, target-specific refresh without global cancellation |

## November 3 source corrections compared with previous Hunter tuning

| Mechanic | Previous simulator | New 70753 source-supported behavior |
|---|---|---|
| Aimed Shot, level 60 rank 6 | +166 damage, 2s | **+350** bonus damage, still 2s |
| Sniper Shot rank 3 | +295 damage, 4s/15s | **+590** bonus, still 4s/15s; Barrage *does not* apply |
| Arcane Shot rank 8 | 217 base, no AP coefficient | **280 base + 15% live ranged Attack Power** |
| Serpent Sting rank 9 | 111/tick, no RAP coefficient | **675 over 15s = 135 per tick + 5% live RAP per tick** |
| Barrage | 3/7/10% | **5/10/15%** for Multi-Shot, Aimed Shot and Volley only |
| Trueshot Aura rank 5 | 50 RAP | **100 RAP** for owner; raid distribution unsupported |
| Lightning Reflexes | 2/4/6/8/10% effective Agility AP path | **3/6/9/12/15%** (secondary crit/dodge still partial) |
| Pathfinding | Tier 2 | **Tier 3** |
| Fire vs Frost traps | two cooldown categories | **one shared 30s trap cooldown**, subject to talent cooldown reductions |
| Aspect of the Hawk rank 7 | +120 RAP | unchanged; the Nov 3 +20/35/50/70/90/110 changes apply to ranks 1–6, not rank 7 |

All November 3 launch-announced hotfix values execute **provisionally**. The source does not authorize inventing omitted coefficients or enemy movement/AI.

## Nineteen implemented Hunter pet families

The catalog has **19 unique family identities**. Each uses the shared pet actor and family-specific ability plus Bite and/or Claw. Off-family role/attack-speed data and source numeric tuning remain separately provisional. Core Hound exists in the source catalog but tameability is not fully confirmed. Family damage/Armor/Health below are multiplicative previews, not measured in-game absolutes.

| Family | Base damage modifier | Base armor modifier | Base health modifier | Signature ability | Ordinary Focus attacks | Confidence / runtime |
|---|---:|---:|---:|---|---|---|
| `bat` | +7% | +0% | +0% | `sonic-blast` | Bite | provisional; engine actor + family ability |
| `bear` | -9% | +5% | +8% | `swipe` | Claw Bite | provisional; engine actor + family ability |
| `bird-of-prey` | +7% | +0% | +0% | `mine` | Claw  | provisional; engine actor + family ability |
| `boar` | -10% | +9% | +4% | `charge` | Bite | provisional; engine actor + family ability |
| `carrion-bird` | +0% | +5% | +0% | `demoralizing-screech` | Claw Bite | provisional; engine actor + family ability |
| `cat` | +10% | +0% | -2% | `prowl` | Claw Bite | provisional; engine actor + family ability |
| `crab` | -5% | +13% | -4% | `pinch` | Claw  | provisional; engine actor + family ability |
| `crocolisk` | +0% | +10% | -5% | `dismember` | Bite | provisional; engine actor + family ability |
| `fox` | +0% | +5% | +0% | `tricksters-dance` | Bite | provisional; engine actor + family ability |
| `gorilla` | +2% | +0% | +4% | `thunderstomp` | Bite | provisional; engine actor + family ability |
| `hyena` | +0% | +5% | +0% | `tendon-rip` | Bite | provisional; engine actor + family ability |
| `raptor` | +10% | +3% | -5% | `savage-rend` | Claw Bite | provisional; engine actor + family ability |
| `scorpid` | -6% | +10% | +0% | `scorpid-poison` | Claw  | provisional; engine actor + family ability |
| `spider` | +7% | +0% | +0% | `web` | Bite | provisional; engine actor + family ability |
| `tallstrider` | +0% | +0% | +5% | `dust-cloud` | Bite | provisional; engine actor + family ability |
| `turtle` | -10% | +13% | +0% | `shell-shield` | Bite | provisional; engine actor + family ability |
| `wind-serpent` | +7% | +0% | +0% | `lightning-breath` | Bite | provisional; engine actor + family ability |
| `wolf` | +0% | +5% | +0% | `furious-howl` | Bite | provisional; engine actor + family ability |
| `core-hound` | +1% | +0% | +5% | `lava-breath` | Bite | provisional; engine actor + family ability |

Existing CP169 `Cp169AllNineteenPetOutcomes`, pet Focus, spell/melee hit inheritance, target attribution, death/dismissal and pet-family sweep tests remain the authoritative combat baseline. The F3D0 regression suite adds source crosschecks, 19-family roster verification and targeted owner/pet scenarios. Pet family signatures with no damage are utility actions; a successful cast does not imply numeric damage.

## Priority for F3D1–F3D4

1. **F3D1 Marksmanship:** Recheck all 70753/launch announcement shot ranks and range, record sampled live hit/crit/auto timings; resolve Scatter Shot direct damage and Rapid Killing raid event ownership, and Trueshot Aura party sharing where in supported scope.
2. **F3D2 Survival:** Complete Lightning Reflexes 15% Agility contribution to crit/dodge using verified conversion; certify Lacerating Strikes' weaker-bleed overwrite behavior; finish melee/trap proc and shared-cooldown tests.
3. **F3D3 Beast Mastery and pets:** Audit full 19-family signature ranks, Focus, attack-speed innate passives and owner inheritance; finish Improved Mend Pet dispels/Intimidation control where supported.
4. **F3D4 Hunter cross-tree:** Validate arbitrary 51-point legal mixed allocations across all three runner types, active/petless transitions, stale talent removal, full browser export/import/replay, and absence of phantom pet effects.

**Acceptance boundary:** the matrix is a reasoned source-and-code classification as of this checkpoint; exact-final-head CI and real Chrome screenshots/report artifacts must be recorded separately after GitHub Actions reports success. Do not treat current 50-key catalog coverage as a fully implemented raid/PvP Hunter.


## F3D1 — Marksmanship mechanics (2026-11-03)

**Starting HEAD:** `a2ea207d46b02ae8d3f3015de0b303ab5e375e8c`, successful [CI #41696331021](https://github.com/layoung3/WarcraftSim/actions/runs/41696331021). Existing branch/PR #28; no merge.

**Sourced Scatter Shot:** [Spell ID 19503](https://raiddex.com/en/wow-forever/spells/scatter-shot) and [Forever client comparison](https://foreverdiff.com/spells/scatter-shot-19503/) corroborate one Marksmanship talent rank, 8% **base** Mana, instant cast, 30-second independent cooldown, 15-yard range, 50% ranged weapon damage, 4-second disorientation broken by subsequent damaging hits, and stopping Auto Shot. The existing `AbilityExecutor` resolves one ranged hit/miss/critical, 50% weapon base roll plus 50% normalized ranged AP contribution; `DependsOnEffectKey` gates the aura on landing. A minimal event processor stops Auto Shot on actual cast and removes the aura on later damage, without duplicating the combat engine. Explicit range, target AI disorientation and melee range are **not modeled**, so the talent is **partial**, not fully simulated. Barrage explicitly excludes Scatter Shot; ordinary physical ranged weapon specialization and Mortal Shots still qualify, and Rapid Killing's documented next Shot buff is eligible. Custom priority accepts learned Scatter; **automatic DPS priorities intentionally omit it**.

**Rapid Killing:** [Current rank-2 tooltip, 415405](https://www.wowhead.com/forever/spell=415405/rapid-killing) says 2-minute Rapid Fire cooldown reduction and 20% more damage to the next Shot within 20 seconds after an eligible kill or death while afflicted by your Serpent Sting. The existing handler already uses source-owned active Sting or Hunter last-hit and does not manufacture a kill for the duration-scripted boss. Tests separate owner, pet and foreign-owner kills, cooldown ranks, damage multiplier and consumed procs; pet kills without an active owner Sting are treated conservatively because explicit pet-kill attribution is not source-certified. Raid-party events and non-trivial level classification still need F3D4 encounter certification. Rapid Recuperation stays a conditional **15-second** in-casting Mana event stream, never a permanent regeneration multiplier.

**Trueshot Aura:** rank 5 spell [20906](https://wowforever.wclbox.com/en/fashu/hunter/hunter-trueshot-aura), current November 3 +100 Ranged AP hotfix is provisionally implemented through source-owned aura apply/remove. The initial owner stat no longer includes an unremovable fixed bonus: the existing `ForeverEquipmentHunterDynamicStats` callback adds/removes live RAP and refreshes Hunter-specific pet inheritance without changing shared pet rules. Normal Solo DPS starts with a precast raid aura; the 525-Mana out-of-combat cast is **not** simulated as a consumable, and no fabricated raid roster is introduced. Test fixtures cover applying once, unrelated equipment SP/AP buffs, removal once and numerical ranged damage.

**Existing ranged engine preserved:** 2s Aimed Shot and 6s Aimed/Multi shared cooldown, independent Arcane Shot, 4s/15s Sniper Shot, 70753 damage coefficients, Auto Shot independent scheduling, target-owned Sting ticks, and Rapid Fire haste rescheduling. Pet-active versus petless Lone Wolf remains driven by actual pet status. Scatter Shot's explicit stop-auto rule is its only new Auto Shot timing behavior; the simulator does not restart Auto Shot without a separately modeled start action.

**Mechanical coverage after scoped correction:** Marksmanship 9 full / 5 partial / 2 not modeled; all Hunter 27 / 20 / 3. Scatter Shot loses the A tag because its principal 50% direct damage now executes. Remaining *explicit Category A* Hunter gaps are Survival Lightning Reflexes secondary Agility conversions and Lacerating Strikes overwrite priority. Marksmanship's remaining gaps are range, enemy control, raid buffs, and raid kill credit, not a missing ordinary stationary DPS damage formula. Provisional client values do not by themselves force mechanical partial status.

**F3D1 acceptance:** `CP175_F3D1MarksmanshipMechanicalCompletion.cs` supplies exact spell/rank/Mana/weapon/AP contracts, landed/failed disorient and damage break, Auto Shot stop, owner/Sting kill attribution, Trueshot live-stat apply/expire and ranked cooldown behavior. Four legal itemized 51-point Chrome builds separately demonstrate MM damage, Scatter utility, cross-tree BM with MM ranks, and petless Lone Wolf, with full export/import/replay. **Actual GitHub Actions results and artifact IDs must be attached only after exact-head success**.

## CP175-F3D2 — Survival live Agility and per-target bleed closure

**November 3, 2026** · Starting `139fa9c612925dfd797cbd8201af6596f44f2057`, CI [#41699845816](https://github.com/layoung3/WarcraftSim/actions/runs/41699845816). Existing draft PR #28, no merge.

**Primary sources:** [Blizzard Nov 2/3 Forever beta notes](https://us.forums.blizzard.com/en/wow/t/wow-forever-beta-development-notes-%E2%80%93-updated-november-2/2360696) explicitly confirm **Lacerating Strikes always overwrites the same Hunter's prior bleed, even a weaker damage-per-second application**, and Lightning Reflexes 3/6/9/12/15% Agility. [Talent tooltip](https://talentsforever.com/hunter/talents/lacerating-strikes) specifies 40% of landed Mongoose Bite damage over 21s (seven 3s ticks); earlier beta notes permit bleed ticks to crit. No rollover is implied.

**Live stat flow:** Reuse the existing CP81 level-60 Hunter stat profile in `ForeverPrimaryPhysicalStatProfiles` (1 melee AP, 2 ranged AP per Agility; 52.9 Agility for +1% crit; 2 Armor/Agility). The `SurvivalHunterAgilityStats` adapter adds only Lightning Reflexes' **incremental** effective Agility, preventing a second base/equipment conversion. Equipment and timed source deltas pass through the existing `ForeverEquipmentHunterDynamicStats` callback, then add/revert 3% per rank of their Agility, derived AP, crit and armor. The same live stats drive Auto Shot, melee autos, Mongoose Bite, Counterattack, Strider Kick, Aimed/Multi, traps and pet owner inheritance. Existing base gear, enchant, food and modifier selection is not changed.

**Dodge boundary:** The client publishes Hunter crit conversion but not Agility-to-dodge as an authoritative server-derived number. Some Classic/guide estimates state ~26.5 Agility per 1% dodge, but this is **not a verified Forever level-60 conversion**. The optional `AgilityPerDodgePercentAt60` encounter field permits explicit, source-labelled controlled tests; null defaults to UNKNOWN, not a hidden Classic assumption or zero-dodge gameplay claim. The default stationary Solo DPS encounter has no genuine incoming attack/dodge table. Accordingly Lightning Reflexes remains **partially modeled** for defensive dodge and source validation, while its core Solo DPS Agility/AP/crit path is implemented.

**Lacerating Strikes:** The existing Survival reactive processor still schedules ticks on the shared `SimulationContext`; `SurvivalLaceratingBleedLedger` only scopes one generation per *target*, preventing an unrelated target's refresh from canceling ticks elsewhere. A landed Mongoose Bite with Lacerating learned applies a target-owned 21s refreshing aura and seven ticks totaling 40% of that hit before independent periodic critical rolls. Each new Mongoose application from the Hunter **unconditionally replaces** the previous same-target application, including weaker and equal strength. Old generation ticks are invalidated, the new application gets full duration, other targets retain theirs; no independent scheduler or carryover arithmetic is invented. Target death and absent talent remain guarded by the existing engine.

**Melee/traps:** Default priority retains Mongoose and Counterattack opportunity gates, Raptor Strike queued swing cost and stance. Four Fire/Frost traps retain the unified shared 30s cooldown reduced 20% per Survivalist's Discipline rank; Resourcefulness and Clever Traps remain on the engine's existing damage/resource path. Trap AP is read from current owner ranged AP rather than a prebuff-only number, while exact trap AP coefficient remains provisional.

**Coverage:** Survival **9 fully / 9 partial / 0 not** (was 8/10/0). Total Hunter **28 fully / 19 partial / 3 not**. Explicit Category A core Solo DPS gaps fall from two to zero within supported stationary encounter scope. Other incomplete B/C/D/E gaps (including confirmed dodge source uncertainty, enemies and movement) remain. These counts are not universal raid/PvP Hunter certification.

**Evidence gate:** `CP175_F3D2SurvivalMechanicalCompletion.cs` contains numerical rank, temporary Agility apply/expire, optional dodge, seven-tick bleed, same-target weaker refresh, independent target generations, next-swing mana and shared-trap cooldown, legal Survival/BM and deterministic replay checks. Exact final-head CI and Chrome reports are to be recorded only after GitHub Actions succeeds.


### F3D2 bounded mixed-tree integration

The unified Hunter itemized catalog now exposes executable **Beast Mastery** talents alongside Survival and the source-backed first-tier **Improved Tracking** passive alongside Marksmanship. Without constructing a second combat runner, the existing Survival runner separates legally selected BM ranks into its already-running `PetTalents` structure before validating actual 51-point budgets and tiers. A 44 Survival / 7 BM build can therefore execute Mongoose Bite/Lacerating Strikes and an active pet with Endurance Training and Focused Fire. Marksmanship accepts a 46 MM / 5 Survival Improved Tracking build on an explicitly marked tracked target; the tracked 5% bonus modifies actual owner hit results, not pet damage. Deallocation and invalid deep off-tree ranks still fail closed.

This is **scoped to tested functionality**, not arbitrary 51-point three-tree integration. Other Surv/MM and BM/Surv combinations and cross-tree ability grants remain the subject of CP175-F3D4. No artificial class-specific point grants were used in the browser tests.


## F3D3 bounded pet lifecycle and replay checkpoint (November 3, 2026)

The first F3D3 implementation batch reuses the existing BM pet actor, Focus resource, family spellbook and auto-attack processor. No new companion scheduler or catalog was introduced.

- **Fox Trickster's Dance:** BM's haste timer now guards a refreshed aura against duplicate +30% haste and against a stale expiry removing a newer effect. The 70753 source-backed signature cooldown is **180 seconds**, not the configurable fallback used by unknown family abilities; tests honor that fixed cooldown. The other Hunter specializations already had refreshed-Fox handling in their shared companion runtime.
- **Pet death and revival:** BM now terminates active Frenzy and Fox temporary haste, clears pending Bestial Wrath damage eligibility and removes these pet-only auras on death. A revived pet must not inherit the dead pet's transient bonuses. Expiration events are made idempotent so a stale death-era event cannot remove haste from a newer lifecycle.
- **Nineteen-family replay regression:** the preexisting CP169 actual-combat, signature cost, pet/owner damage reconciliation and lethal-death sweep is extended with the same-seed timeline, Focus regeneration and damage replay checks across all 19 family keys. Fox tests separately exercise source-defined cooldown and lethal aura cleanup.

**Mechanical matrix remains unchanged:** Beast Mastery **10 fully modeled / 5 partially modeled / 1 not modeled** until supported dispel, defense, healing and remaining talent interactions are directly certified. The 19-family numerical values, signature ranks and Core Hound tameability remain provisional. This work is not a claim of full F3D3 certification, arbitrary cross-tree execution, real server verification, or new browser acceptance. Consult PR #28's final-head Actions result for CI evidence; a green intermediate-head workflow is not final-head acceptance.

## F3D3B — Beast Mastery numerical and browser acceptance (November 3, 2026)

**Mechanical change since F3D3:** Improved Mend Pet's supported missing cleanse is implemented in `HunterMendPetDispel` and invoked by the real BM Mend Pet healing-tick handler. The sourced Forever rank chances are **15% at rank 1 and 50% at rank 2** (not 15% per rank), alongside 10%/20% Mana savings. The shared AuraManager owns removal and event emission; only an active hostile aura explicitly tagged `dispel:curse`, `dispel:disease`, `dispel:magic` or `dispel:poison` qualifies. One aura is removed per successful proc. Defensive/encounter debuffs without corresponding auras are *not* fabricated. Historical client evidence: [Forever Improved Mend Pet](https://theforeverera.com/en/talents/hunter/improved-mend-pet/) and [Forever armory talent](https://wowforeverarmory.com/talents/hunter/beast-mastery-improved-mend-pet). The exact cleanse rank values are independently testable, but live-server proc-rate validation remains provisional.

**Numerical and lifecycle evidence:** `CP175_F3D3B_BeastMasteryNumerical.cs` compares noncritical actual Pet melee hit damage with/without 18-second Bestial Wrath (expected 1.50 multiplier), tests timed expiration and immediate dismissal removal, and compares genuine pet melee timestamps with and without Frenzy. `CP175_F3D3B_MendPetDispel.cs` tests exact proc boundaries, eligible/ineligible tagged auras, one-aura removal, Mend Pet Mana and healing receipts, Improved Revive Pet rank cast time/cost and death→revive→attack receipts. Existing `CP169_SharedPetRosterAndDynamicStats.cs` now verifies temporary Hunter Attack Power/Armor inheritance returns to its precise baseline and does not duplicate pet bonuses. `CP169_AllNineteenPetOutcomes.cs` additionally certifies Intimidation arming expiration without a successful pet hit. Temporary pet-only Wrath/Frenzy/Fox/Intimidation effects are removed on both pet death and dismissal.

**Browser builds:** `docs/acceptance/cp175/cp175-f3d3b-beast-wrath-frenzy.json`, `cp175-f3d3b-wind-serpent-focus.json`, `cp175-f3d3b-survival-bm.json`, and `cp175-f3d3b-petless-lone-wolf.json` are separate legal 51-point importable level-60 builds. The existing Chrome harness imports each, verifies ranks and typed requests, runs combat, validates owner/pet damage and relevant Wrath/Frenzy/lightning-breath/Focus/Lone Wolf evidence, captures screenshots, exports build/report, and verifies seeded replay. Controlled enemy pet debuffs, scripted repeated pet deaths and owner stat mutation remain **backend fixture** evidence rather than misrepresented browser gameplay. Final-head CI must pass before these are labeled accepted.

**Updated supported Solo DPS mechanical matrix:** Beast Mastery **11 full / 4 partial / 1 not modeled**; overall Hunter **29 full / 18 partial / 3 not modeled**. Previous F3D3's 10/5/1 was the pre-cleanse checkpoint, not a contradiction. Improved Revive Pet remains conservative partial pending broader dynamic maximum-Health/replacement certification; Intimidation remains partial for enemy AI stun; Pathfinding remains not modeled without travel pathing. Source-provisional coefficients, 19-family signature-rank uncertainty, dynamic pet Health inheritance outside supported snapshot behavior, and Core Hound tameability remain explicitly unresolved. This is not independent live-server 19-family certification or arbitrary F3D4 cross-tree completion.

### F3D3B final numerical/negative additions

An additional functional defect was identified in BM Spirit Bond: although `BeastState` contained the 5s/10s periodic healing handler, nothing scheduled its first event. The runner now schedules that first event at rank-appropriate cadence when learned; removing Spirit Bond suppresses the periodic healing receipt entirely. Negative tests also isolate exact active-pet Unleashed Fury (1.15 at five ranks) and Focused Fire (1.02 at two ranks) multipliers, reject stale Bestial Wrath/Summon Hawk priorities when the granting talent is removed, and verify 0/1/2-rank Bestial Discipline Focus regeneration (5/5.5/6 Focus/sec in the controlled tuning fixture).

**Chrome evidence already observed:** [Actions #41724222394](https://github.com/layoung3/WarcraftSim/actions/runs/41724222394) passed all four F3D3B build imports, combat assertions, screenshot captures, build/report export and exact-seed replay. This earlier implementation-head success is not substituted for final-head CI after the Spirit Bond and negative eligibility commits.

## F3D4 — Hunter cross-tree execution integration (2026-11-04)

**Correction to the incoming baseline:** F3D3B's implemented Improved Mend Pet cleanse updated Beast Mastery from **10 full / 5 partial / 1 unmodeled** to **11 full / 4 partial / 1 unmodeled**. The reconciled 50-talent matrix is **Marksmanship 9/5/2; Survival 9/9/0; Beast Mastery 11/4/1; total 29/18/3**. Cross-tree test additions do not by themselves change individual talent completeness classifications.

**Actual integration work:**

- Marksmanship-starting requests now transfer learned BM ranks from the itemized typed allocation to the existing pet talent owner while maintaining Survival Improved Tracking support. BM family survival/Focus/passives remain handled by the CP169 shared companion systems; separate pet and owner damage remains intact.
- Survival-starting requests now execute **Marksmanship Lethal Attacks** (1%/rank crit on owner melee/ranged rolls, with pet crit inheriting effective owner crit), alongside previously supported BM off-tree pet modifiers. Their existing melee-first Raptor/Mongoose/Trap and Lacerating rules are not replaced.
- Beast Mastery-starting requests now execute **Survival Improved Tracking** (1%/rank qualifying tracked-target owner damage, not a phantom pet damage multiplier) alongside supported off-tree Marksmanship Lethal Attacks. The target is explicitly classified through the runner's TargetIsTracked setting.
- MM, Survival and BM normalize ranks on a deserialized copy of the typed request: repeated simulations no longer silently remove learned off-tree ranks from the caller's original build. Survival Focused Fire now requires the actual `hunter-pet-active` aura as well as a living pet, so an alive-but-disengaged pet does not grant owner damage.

**Strict validation:** The unified engine/itemized catalog includes only the newly executable off-tree keys in the corresponding runner, and rejects unsupported learned effects rather than silently treating them as historical filler. The same 51-point itemized totals, tier, prerequisite and rank limits apply across all three trees. Test fixtures include MM/BM 44/7; Survival/BM 44/7; MM/Survival 46/5; MM/Survival/BM 41/5/5; Survival/MM/BM 39/5/7; and BM/MM/Survival 41/5/5. The last three are distinct starting-spec three-tree simulations, not synthetic aggregate grants.

**What remains unsupported:** This is **not yet arbitrary legal cross-tree execution**. Most deeper off-tree Marksmanship skills remain unavailable in Survival/BM runners; deeper Survival actives/passives remain unavailable in MM/BM runners; and the specialization selector may still reset the allocation on a new preset load instead of preserving an arbitrary formerly legal build. A legal itemized allocation involving one of those unsupported learned abilities must fail closed rather than be described as executable. These are genuine cross-tree **Category A/E integration gaps**, separate from the already certified stationary single-specialization core DPS paths.

**Completeness assessment:** Within their supported primary-spec stationary Solo DPS paths, no additional previously unknown Category A numerical coefficient defect was established in Marksmanship, Survival or Beast Mastery by F3D4. All 19 registered families retain real engine combat, Focus and deterministic-replay testing. However the total number of *arbitrary cross-tree* unsupported combinations has not been exhaustively enumerated or certified as zero. Remaining B/C issues include effective enemy AI control, movement/pathing, source-uncertain avoidance, broader raid effects and defensive debuffs. Remaining D issues include live-unknown Hunter pet signature coefficients, source-provisional inheritance/attack tuning and Core Hound tameability; no live-server equivalence is claimed.

**Evidence files:** `CP175_F3D4HunterCrossTree.cs` tests exact 51-point validation, combat-event participation, owner/pet damage separation, reversible off-tree Focused Fire multiplier, no phantom inactive-pet Focused Fire, three-tree passive effects, same-seed replay, and negative incomplete allocations. Six separate `cp175-f3d4-*.json` importable browser fixtures are executed in `tools/solo-dps-browser-interactions.cjs`, including real Scatter Shot removal from a three-tree build. Browser screenshot/report artifacts belong to the exact final-head Actions run and must not be claimed complete before success.


### F3D4 final off-tree eligibility correction (2026-11-04)

The Marksmanship runner now executes selected Beast Mastery **Deadly Aspects** as source-qualified landed Auto Shot haste: 2% per rank chance while Aspect of the Hawk is active, 30% actual swing haste for a timed 12 seconds, with aura expiry and no permanent stacking. It also retains the selected **Bestial Discipline** casting Spirit-regeneration fraction (25% per rank) in the owner Mana processor, in addition to existing pet Focus regeneration. Both have direct combat/resource tests in `CP175_F3D4HunterCrossTree.cs`. The MM/BM browser fixture is 44 MM / 7 BM with real 2/2 Focused Fire, rather than the initial 41/10 test allocation with a nonexecuting Deadly Aspects rank.

Strict unified itemized validation rejects selected Beast Mastery abilities that are not implemented in the chosen starting runner; Marksmanship currently rejects Improved Mend Pet, Summon Hawk, Improved Aspect of the Monkey and Pathfinding. Survival rejects incoming unified allocations of Deadly Aspects, Improved Mend Pet, Improved Aspect of the Monkey and Pathfinding, but preserves existing CP169 legacy typed `PetTalents` execution and Hawk family tests. These rejections explicitly identify **remaining cross-tree integration limitations**, not missing points or fabricated passives. Full arbitrary cross-tree gameplay remains incomplete.


## CP175-F3D4B — Cross-preset learned active action eligibility (November 4, 2026)

**Starting checkpoint:** F3D4 HEAD `eca46b452d291dc3b28a28b4d675cc0651734a0b` / Actions #41854007474 passed 3,036 xUnit, 60 Python and Chrome. Work stays on existing PR #28; no merge.

This implementation connects previously preset-gated Hunter actives by reusing the existing Marksmanship/Survival spellbooks, HunterSharedHawkRuntime and shared ability/cooldown/AutoAttack/ReactiveOpportunity processors. Starting specialization keeps its preferred automatic rotation. These abilities remain gated by actual talents and their ordinary mechanics.

### Active talent action matrix

"Executable" means a legal rank allocation, suitable combat conditions and a real action receipt (not merely that the icon or name appears). The original primary-spec implementations are preserved.

| Learned talent / action | Marksmanship start | Survival start | Beast Mastery start | Required conditions / evidence |
| --- | --- | --- | --- | --- |
| Sniper Shot | Native | Executable off-tree | Executable off-tree | Learned talent and Trueshot prerequisite; four-second cast, 15-second cooldown, Mana expenditure; cross-preset F3D4B Chrome and xUnit receipts |
| Scatter Shot | Native | Executable off-tree | Executable off-tree | Learned rank, 8% base Mana, 30-second cooldown, real landed damage/disorient and Auto Shot stop; control AI and 15-yard geometry remain unsupported |
| Strider Kick | Executable off-tree | Native | Executable off-tree | Learned rank, explicit melee engagement, weapon hit and temporary Strider movement aura; Chrome and backend negative stance tests |
| Counterattack | Executable off-tree | Native | Executable off-tree | Learned Counterattack + Deterrence, melee engagement and reactive parry opportunity; explicit scripted incoming parry only in deterministic encounter fixtures, never an unconditional grant |
| Deterrence | Executable off-tree | Native | Executable off-tree | Learned rank, native self defensive aura/cooldown; only explicitly prioritized in off-tree DPS fixtures |
| Bestial Wrath | Executable off-tree | Executable off-tree | Native | Learned BM prerequisite chain, eligible pet, actual pet aura and temporary damage; CP169/F3D3B lifecycle regressions |
| Intimidation | Executable off-tree | Executable off-tree | Native | Learned BM prerequisite chain, living/active pet, next-hit window and threat; F3D3B expiration regression |
| Summon Hawk | Executable off-tree | Executable off-tree | Native | Learned rank, two distinct summoned Hawk actors, Mana and Arcane Shot shared cooldown; separate owner/pet/Hawk damage attribution and source-owned expiry |
| Trueshot Aura (passive/automatic aura) | Native | Executable off-tree | Executable off-tree | Learned Marksmanship rank, actual source-owned 100 RAP aura; separate live owner/pet inheritance; no automatic utility DPS cast |
 
**Direct negative evidence:** Unlearned Sniper/Scatter fail stale custom priorities in both non-MM runners; unlearned Summon Hawk fails MM custom priority; unlearned Deterrence fails MM/BM; Counterattack does not cast without a real parry window; melee-only actions do not cast without melee stance; talent UI removal scrubs an imported BM-starting Strider Kick priority. The new Hunter rotation preview delegates to each actual combat runner and rejects stale imported custom actions.

**Browser fixtures:** `cp175-f3d4b-survival-sniper-scatter.json`, `cp175-f3d4b-beast-mastery-sniper-scatter.json`, `cp175-f3d4b-marksmanship-summon-hawk.json`, `cp175-f3d4b-marksmanship-survival-melee.json`, and `cp175-f3d4b-beast-mastery-survival-melee.json`. Each is a legal 51-point build with actual casts, Mana cost (where required), pet/Hawk damage where appropriate, full report, import/export, screenshots and same-seed replay. The earlier six F3D4 three-tree fixtures and 19-family pet suite are retained.

**Important limits:** Action access is different from every *passive* being universally complete. More extensive cross-tree passive parity (especially deeply invested Survival proc/bleed/trap effects within ranged-primary runners), full enemy AI control, spatial movement and range, and independently verified Forever damage coefficients remain separate limitations. No blanket claim of complete universal Hunter gameplay or live-beta numerical accuracy. The conservative mechanical talent count stays **Marksmanship 9 full/5 partial/2 not; Survival 9/9/0; Beast Mastery 11/4/1; total 29/18/3** until actual missing mechanical effects are certified. The matrix concerns active access, not promotion of every partial talent.


## F4A — Off-tree Mortal Shots critical damage parity (November 4, 2026)

The F4A four-class pass identified a stationary Solo DPS parity issue in **legally selected Marksmanship Mortal Shots on Survival- and Beast Mastery-starting Hunter builds**. The actual owner ranged critical result now includes the learned 6% per rank critical damage bonus through each runner's existing combat roll resolver; pet critical damage does not inherit the owner-only Mortal Shots effect. It is **not** applied as a blanket hit multiplier or duplicated on normal hits.

`WarcraftSim.Tests/Consolidated/CP175_F4A_HunterMortalShotsParity.cs` uses the existing legal F3D4B cross-preset itemized builds with 5/5 Mortal Shots, 100% controlled owner critical chance, real Sniper Shot impacts and fixed seed. Removing the learned rank changes only the owner critical hit multiplier: source-backed 5 ranks = **2.30x**; zero ranks = **2.00x**, with paired landed critical amounts compared to a 0.000001 tolerance and unchanged pet damage. Exact replay of the removed-rank build must match the original timeline.

The dedicated Hunter F4A Chrome build has 46 Marksmanship / 5 Survival, active Cat, actual Sniper Shot critical receipt and independent pet damage and seeded replay. The 19 registered families remain covered by their previous exact-seed sweeps. This certifies **this specific off-tree ranged critical path**, not generalized cross-preset stat parity.

**Known further core integration work:** Survival's Lightning Reflexes Agility-derived melee/ranged AP/critical conversion uses the existing Survival-specific effective-stat path; other starting presets currently lack an equivalent sourced Agility input in every numeric-only request. Lacerating Strikes and wider defensive/target-control procs also need targeted legal cross-tree fixtures. Those gaps remain open rather than deriving an unsupported Agility value or upgrading 29/18/3 Hunter mechanical coverage counts. Movement, enemy AI, range, threat and pet coefficient provenance remain outside F4A's stationary scope.

## Completion correction: Hunter off-tree stats and source ownership (2026-11-09)

This supersedes the F4A Lightning Reflexes limitation above for supported level-60 profiles. Raw Agility is explicit (zero when unavailable, never inferred from AP); rank 5 adds 15% Agility with the existing sourced conversions. For raw 220 Agility the increment is 33 melee AP, 66 RAP and 33/52.9 crit percentage points. MM/BM off-tree paths now execute this increment and reverse temporary deltas once. Mongoose's 25 Agility contributes 50 RAP, not 75. With a temporary 40 Agility and rank 5, derived Agility/melee AP is 46, armor 92 and RAP 92, without double-applying ordinary deltas.

Surefooted hit, Savage Strikes melee-ability crit, Predator's Edge eligible melee critical damage and Resourcefulness crit-gated Mana regeneration now execute in the tested off-tree paths. Ordinary autos/pets do not inherit owner-only melee ability critical bonuses. Newly created Strider Kick/Counterattack receive live AP scaling. Owner-only Hawk/Beast AP is excluded from companion AP during equipment/racial/Trueshot changes and after expiry.

Precombat display uses the same supported coefficients while preserving the original reusable profile. Catalog equipment is resolved from raw inputs, and selected external plus authored profile buffs contribute generic RAP exactly once. Real Chrome removes and relearns five Lightning Reflexes ranks, proving displayed Agility/AP/crit/armor reversal. Conditional auras and companion values are explicitly excluded from this static display scope.

The current conservative node count is **24 fully modeled / 23 partial / 3 unmodeled**. This lowers earlier overstated full classifications for Survival-only effects rather than certifying arbitrary cross-tree execution. MM/BM selections of Expose Prey, Lacerating Strikes, Clever Traps, Survival Tactics, Improved Wing Clip, Entrapment, Deflection and Survivalist retain explicit limitations. Unmeasured dodge, spatial geometry/control, unsupported enemy behavior and provisional coefficients remain unchanged. New `CP175_Hunter*` regressions pass 70 cases locally; final exact-head CI must be recorded separately.
