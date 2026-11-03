# CP175-F3D0 — Hunter talent coverage and Solo DPS combat-gap baseline

**Review:** November 3, 2026. **Ruleset:** WoW Forever Hunter client 1.60.1.70753, with November 3 announced balance updates. **Repository:** `layoung3/WarcraftSim`, branch `cp175-shared-character-simulation`; checkpoint starts at `93b6cefe1a30e9d27b5cd2d5a33eb6db55f087e5` (CI #41690789865, success). **Not a claim of server-certified combat values.**

**Primary current client:** [ForeverChanges Hunter talents, 1.60.1.70753](https://foreverchanges.pro/talents/hunter), updated November 3. [Spellbook comparison](https://foreverchanges.pro/spellbook/hunter).

**Developer notes:** [Blizzard Forever Hunter and Druid class deep dive](https://news.blizzard.com/en-us/article/24301515/world-of-warcraft-forever-class-deep-dives-hunter-and-druid) and [Beta Development Notes, November 2/3](https://www.bluetracker.gg/wow/topic/us-en/2360696-wow-forever-beta-development-notes-updated-november-2/).

**Historical comparison only:** repository `WarcraftSim.Api/Catalog/forever-talent-visual-reference-v1.json`, pinned historical WoWSims export. Icons and row-position references do not prove mechanics. Most numeric talent spell IDs were **not independently recovered**; leave blank rather than fabricate.

## Talent count and mechanical classification

There are **50 distinct Hunter talents** in the current client: 16 Beast Mastery, 16 Marksmanship and 18 Survival. The simulator already has many executed mechanics but **declaring a talent key in typed metadata is not proof it executes**. Coverage is conservative and identifies principal core combat, not universal raid/PvP support.

| Talent tree | Current talents | Fully modeled | Partially modeled | Not modeled |
|---|---:|---:|---:|---:|
| Beast Mastery | 16 | 10 | 5 | 1 |
| Marksmanship | 16 | 9 | 5 | 2 |
| Survival | 18 | 9 | 9 | 0 |
| **Total** | 50 | 28 | 19 | 3 |

**Core stationary Solo DPS versus total combat (after F3D2):** `0` talent rows carry an explicit **A (core DPS)** gap; the remaining partial/unmodeled rows mainly concern defensive, movement, trap targeting or enemy AI. Not every "not modeled" row is a DPS omission. Numerical source coefficients, pet family values and some inter-tree allocation/validation still require further certification.

**Gap categories:** A = core DPS/damage/resource/proc; B = defensive/healing/threat; C = encounter movement/range/control/pathing; D = source coefficient provisional; E = validation. Current row flags: A 0, B 7, C 22, D 18, E 3. A blank gap means implementation status is full for its principal supported functionality.

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
| beast-mastery | `improved-mend-pet` | 2 | 4 | unverified | typed + itemized | partially-modeled | B | 10%/rank Mend Pet Mana reduction; 15%/rank dispel chance absent |
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
