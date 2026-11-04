# CP175-F3B0 — Mage 70707 talent-by-talent implementation coverage

**Snapshot:** November 2, 2026; client **1.60.1.70707**. Source [Forever DB Mage calculator](https://wowfdb.com/talents/mage/) and [client change history](https://wowfdb.com/classes/mage/changes/). Source is published/client-derived and **provisional; not in-game mechanically verified**. Status describes the bounded solo engine, not universal PvP/raid completeness.

**Coverage:** 53 definitions: Arcane 18, Fire 17, Frost 18. **22 fully modeled, 25 partial, 6 not modeled** (conservative inspected-engine classification, not 53 independent combat attestations). Declared typed-rank keys: 47 (Arcane 15, Fire 15, Frost 17). Six remaining historical-only tree nodes cannot grant executable effects.

**Legend:** *Full* = principal modeled damage/stat/cooldown effect reachable in current solo engine and supported by established test paths, with tuning still provisional; *Partial* = some effect runs but meaningful documented components are unavailable; *None* = no mechanics execute. *Typed* = current `ForeverMageTalentBuild` accepts key (does not prove player-facing itemized validation or runtime execution). *Event/Numeric* = code path and existing/focused tests inspected, not a new individual test of every talent. Cross-tree acceptance still requires F3B4 beyond F3B0.

| Tree | Talent ID | Cap | Tier | Explicit prerequisite | Typed | Status | Runtime evidence and remaining limitation |
|---|---|---:|---:|---|---|---|---|
| arcane | `wand-specialization` | 2 | 1 | — | No | None | Wand auto-attack bonus absent |
| arcane | `improved-channeling` | 5 | 1 | — | Yes | Partial | Pushback mechanics present; outgoing threat/complete interrupt matrix unverified |
| arcane | `arcane-resilience` | 3 | 1 | — | Yes | Partial | Armor scales by 25% Intellect per rank in preview and combat; incoming physical mitigation not generally certified |
| arcane | `arcane-subtlety` | 2 | 2 | — | Yes | Partial | Arcane school threat reduction is implemented at 15/30%; all-school resistance reduction remains unavailable |
| arcane | `magic-absorption` | 2 | 2 | — | Yes | Partial | Resistance/Mana refund on explicit full resist; general resist distribution unresolved |
| arcane | `arcane-concentration` | 5 | 2 | — | Yes | Partial | Clearcasting proc and consumption execute; low-rank and out-of-combat exclusions not universally certified |
| arcane | `missile-barrage` | 2 | 2 | — | Yes | Partial | Proc executes; Arcane Missiles channel-hit trigger and modern 70707 missile cadence require reconciliation |
| arcane | `arcane-geometry` | 2 | 3 | — | No | None | Spell range geometry not modeled |
| arcane | `arcane-impact` | 3 | 3 | — | Yes | Full | Arcane spell critical chance rank-scaled |
| arcane | `arcane-blast` | 1 | 3 | — | Yes | Full | 70707 provisional additive cost/SP stacks and 18-second timer; spell rank/coefficient provisional |
| arcane | `arcane-shielding` | 2 | 4 | — | Yes | Partial | Mage Armor resistance and Mana Shield physical absorption/Mana drain execute under scripted incoming hits; broader encounter and early recast remain unsupported |
| arcane | `improved-counterspell` | 2 | 4 | — | No | None | Enemy silence/interrupt not modeled |
| arcane | `arcane-meditation` | 3 | 4 | arcane-concentration 5/5 | Yes | Full | Rank-scaled casting regeneration with corrected prerequisite |
| arcane | `arcane-wrath` | 1 | 4 | arcane-blast 1/1 | Yes | Partial | Damage/Mana/cast execute; projectile and source rank validation pending |
| arcane | `presence-of-mind` | 1 | 5 | — | Yes | Full | Selected cooldown can make next supported spell instant |
| arcane | `arcane-mind` | 5 | 5 | — | Yes | Full | Intellect and Arcane crit-damage scaling execute |
| arcane | `arcane-instability` | 3 | 6 | — | Yes | Full | Ranked spell damage and critical chance execute |
| arcane | `arcane-power` | 1 | 7 | presence-of-mind 1/1 | Yes | Full | Opt-in damage/Mana cooldown executes |
| fire | `incineration` | 5 | 1 | — | Yes | Full | Named spell critical chance rank-scaled |
| fire | `accelerant` | 5 | 1 | — | Yes | Full | Cast time for Fireball/Frostfire/Arcane Wrath rank-scaled |
| fire | `ignite` | 5 | 2 | — | Yes | Full | Rank-scaled critical pool, rolling 4s two-tick source-owned Ignite, periodic criticals excluded, no percentage bonus double-dip; live rollover remains provisional |
| fire | `flame-throwing` | 2 | 2 | — | No | None | Fire range increase not modeled |
| fire | `impact` | 3 | 2 | — | Yes | Partial | 3/7/10% Fire direct hit proc applies source-owned 2-second stun aura; no boss AI interruption or low-rank proc scaling |
| fire | `burning-soul` | 3 | 3 | — | Yes | Partial | Fire pushback and threat implemented; source spell eligibility still needs certification |
| fire | `improved-flamestrike` | 3 | 3 | — | Yes | Partial | Damage/crit effects execute; real AoE geometry absent |
| fire | `pyroblast` | 1 | 3 | — | Yes | Full | Talent-gated direct/periodic damage; current-rank client details provisional |
| fire | `improved-fire-blast` | 2 | 3 | — | Yes | Partial | Cooldown reduction executes; added range is unsupported |
| fire | `improved-scorch` | 3 | 4 | — | Yes | Full | Landed Scorch vulnerability per target and stacks execute |
| fire | `improved-fire-ward` | 2 | 4 | — | No | None | Fire Ward mechanics not modeled |
| fire | `heating-up` | 1 | 4 | pyroblast 1/1 | Yes | Full | 20s aura from eligible direct crit, landed Fire Blast one extra stack, PoM precedence and subsequent Pyro consumption; low-rank/target-control edge cases remain provisional |
| fire | `master-of-elements` | 3 | 4 | — | Yes | Full | Fire/Frost critical base mana refunds execute |
| fire | `critical-mass` | 3 | 5 | — | Yes | Full | Fire critical chance rank-scaled |
| fire | `blast-wave` | 1 | 5 | — | Yes | Partial | AoE damage executes; Daze/geometry unsupported |
| fire | `fire-power` | 3 | 6 | — | Yes | Full | Source-owned Ignite, Improved Scorch, Fireball, Pyroblast and Impact effects counted once; Frostfire benefits but contributes no stack; capped at 12% |
| fire | `combustion` | 1 | 7 | critical-mass 3/3 | Yes | Partial | 180s cooldown, event-level 10% Fire crit per landed hit, three non-periodic crits consume aura, resisted/zero-damage excluded; AoE interactions provisional |
| frost | `frost-warding` | 2 | 1 | — | Yes | Partial | Ice Barrier absorb increases; ward negation/armor contributions incomplete |
| frost | `improved-frostbolt` | 5 | 1 | — | Yes | Full | Frostbolt cast-time decrease executes |
| frost | `precision` | 3 | 1 | — | Yes | Full | Hit across every spell school rank-scaled |
| frost | `ice-shards` | 5 | 2 | — | Yes | Full | Frost critical damage bonus executes |
| frost | `frostbite` | 3 | 2 | — | Yes | Partial | Source-backed 5/10/15% landed Chill proc emits 5s target-owned Frozen aura; Shatter/Ice Lance benefit from actual aura. Movement, immunity and low-rank penalties unsupported |
| frost | `improved-frost-nova` | 2 | 2 | — | Yes | Full | Frost Nova cooldown 25/23/21 seconds from 0/1/2 ranks; existing target geometry is outside this talent |
| frost | `permafrost` | 3 | 2 | — | Yes | Partial | Chill duration extension executes; movement slow cannot affect stationary target |
| frost | `frost-channeling` | 3 | 3 | — | Yes | Full | Frost cost and threat modifiers execute |
| frost | `improved-blizzard` | 3 | 3 | — | Yes | Partial | Slow aura emits; moving targets and control not represented |
| frost | `ice-lance` | 1 | 3 | — | Yes | Partial | 70707 +300% vs frozen (4x) and category-19 8s shared cooldown with Fire Blast are executable; spell rank, enemy immunity and range remain provisional |
| frost | `arctic-reach` | 2 | 4 | — | No | None | Range geometry absent |
| frost | `ice-block` | 1 | 4 | — | Yes | Partial | Invulnerability/lockout scripted; other control conditions not fully certified |
| frost | `shatter` | 3 | 4 | — | Yes | Partial | Frozen critical chance executes for FoF; external Frozen sources limited |
| frost | `improved-cone-of-cold` | 3 | 5 | — | Yes | Partial | Frost Cone damage scaling executes; geometric coverage limited |
| frost | `cold-snap` | 1 | 5 | — | Yes | Full | Existing cooldown resets; not all current Frost abilities included |
| frost | `fingers-of-frost` | 2 | 5 | ice-lance 1/1 | Yes | Partial | 15s real source aura, 15% landed Chill proc, one/two rank-owned charges consumed by actual casts; Armor-triggered procs unavailable |
| frost | `winters-chill` | 5 | 6 | — | Yes | Partial | 70707 +20% damage per stack and +7% SP per stack to next target-owned Cone; aura application and landed consume execute. Resist/target immunity not certified |
| frost | `ice-barrier` | 1 | 7 | cold-snap 1/1 | Yes | Partial | 70707 819+10% Frost SP absorption with Frost Warding rank modifier; +15% Frost damage timed plus 5% while intact; full encounter pushback/immunity provisional |

## Mechanical and data provenance

- Engine coverage mapping: `WarcraftSim.Api/Simulation/Mage70707TalentCoverageCatalog.cs`, `ForeverMageTalentBuild`, `ForeverMageDpsSpellbook`, `MageSimRunner`, `MageAdaptiveRotation` and focused CP167/CP175 tests.
- Every row's *data confidence* is provisional client-derived 70707. Historical CP174 art/icon records remain linked separately with their pinned version, and mismatched old rank descriptions must never be treated as 70707 rank values.
- For 43 typed keys, rank selection reaches the typed Mage request; whether a proc, aura or cooldown actually fires is stated in the notes. For ten historical-only keys, itemized selection may consume talent points but no mechanics execute.
- Frontend source indicators distinguish provisional formula confidence from partial/unavailable effect coverage.
- Cross-tree support is rank-driven for modeled passive talents and permitted active abilities; lack of in-combat evidence for every hybrid precludes stronger universal claims.
- Itemized API validates actual earlier-row point expenditure and explicit prerequisites. Typed compatibility API retains aggregate-point support for older imports; do not treat synthetic totals as independent evidence of legal itemization.

## Source conflicts and known F3B1–F3B4 follow-ups

1. **Arcane (F3B1):** Missile Barrage live tooltip/channels conflict, low-rank Clearcasting suppression, Arcane Shielding Mana Shield, Arcane Subtlety resistance/threat, and missing Counterspell/geometry mechanics.
2. **Fire (F3B2):** Fire Power’s eligible effect list differs from the current runner; source says Frostfire benefits but does not count as an effect. Ignite precise rollover, Heating Up Fire Blast stacks, and stun/range/ward effects remain uncertified.
3. **Frost (F3B3):** Ice Lance 300% *increased* Frozen damage (versus current 3x multiplier) and shared 8s Fire Blast cooldown; Winter's Chill 20% damage taken per stack (current old 5% Cone bonus); Frost Warding/Ice Barrier revised coefficients, Armor-triggered Fingers charges and missing Frozen target controls.
4. **Cross-tree (F3B4):** full Fire/Frost/Arcane mixed-tree browser simulation with trained spells and negative removal gates.

**Not claimed:** Full in-game accuracy, all modern rank texts, universal control/PvP mechanics, all cross-tree permutations or all 53 individually verified in combat. The 70707 Arcane Resilience cap change is shown separately from the pinned 2-rank historical art data.


## CP175-F3B4 — Missing-behavior significance audit (2026-11-02)

**Scope:** 53 published client 1.60.1.70707 talent definitions, 47 current typed/runnable keys (Arcane 15, Fire 15, Frost 17), and six historical-fallback itemized nodes (three Arcane, two Fire, one Frost) that spend points **without** providing a combat effect. These six fallback identities have not been independently extracted from an authoritative pinned 70707 client tree, so their exact row positions remain provisional. Removed `arcane-focus`, `improved-fireball` and `elemental-precision` remain rejected/migrated as previously designed, never silently granted as new ranks.

**Source:** [Blizzard Mage deep dive](https://worldofwarcraft.blizzard.com/en-us/news/24302097), [Forever DB Mage tree](https://wowfdb.com/talents/mage/) and [70707 changes](https://wowfdb.com/classes/mage/changes/). Mechanics are executable under the stationary single-target / explicitly configured adds engine; *verified against the simulator* does not mean independently live-game verified.

The F3B4 status correction separates a provisional **number** from a missing **mechanic**. Ignite's rolling direct-crit-to-DoT behavior, learned Pyroblast's direct/periodic events, Heating Up's eligible aura/cast consumption, Fire Power's five target categories, and Cold Snap's timed Frost/shared cooldown reset are executable and have dedicated numerical or event-level tests; they move **partial → fully modeled**. Their client coefficients/edge masks remain provisional, independently flagged. This leaves **22 fully modeled / 25 partial / 6 not modeled**, versus the F3B3 baseline **17 / 30 / 6**. Fire becomes **9 / 6 / 2**, Frost **6 / 11 / 1**, Arcane **7 / 8 / 3**. No other class classification changes.

### Practical gap inventory

A = core Solo DPS effect gap; B = defensive/threat/resistance gap; C = encounter/targeting limitation; D = unverified source value or eligibility; E = missing direct validation. Rows may carry multiple flags, and counts below therefore overlap. 

| Category among incomplete talents | Before F3B4 | After F3B4 |
|---|---:|---:|
| A — core DPS | 5 | 4 |
| B — defensive / threat | 10 | 10 |
| C — encounter / targeting | 21 | 21 |
| D — source-specific uncertainty | 12 | 8 |
| E — test coverage | 8 | 8 |

Source-confidence D remains **provisional for all 53 talent definitions**, including the 22 fully executable talents. The D counts in this *remaining-gaps table* describe only partial/unmodeled rows with an additional source-specific uncertainty; data confidence is separately tracked for every row.

| Tree | Talent | Engine status | Gap categories | Actual remaining limitation |
|---|---|---|---|---|
| arcane | `wand-specialization` | not-modeled | A, E | Wand auto-attack bonus absent |
| arcane | `improved-channeling` | partially-modeled | B, E | Pushback mechanics present; outgoing threat/complete interrupt matrix unverified |
| arcane | `arcane-resilience` | partially-modeled | B, C | Armor scales by 25% Intellect per rank in preview and combat; incoming physical mitigation not generally certified |
| arcane | `arcane-subtlety` | partially-modeled | B, C | Arcane spell threat reduced by 15/30%; resistance reduction unmodeled |
| arcane | `magic-absorption` | partially-modeled | B, E | Resistance/Mana refund on explicit full resist; general resist distribution unresolved |
| arcane | `arcane-concentration` | partially-modeled | A, D, E | Clearcasting proc and consumption execute; low-rank and out-of-combat exclusions not universally certified |
| arcane | `missile-barrage` | partially-modeled | D, E | Proc executes; Arcane Missiles channel-hit trigger and modern 70707 missile cadence require reconciliation |
| arcane | `arcane-geometry` | not-modeled | C | Spell range geometry not modeled |
| arcane | `arcane-shielding` | partially-modeled | B, C | Mage Armor resistance and scripted physical Mana Shield efficiency execute; full encounter and manual recast remain unsupported |
| arcane | `improved-counterspell` | not-modeled | C | Enemy silence/interrupt not modeled |
| arcane | `arcane-wrath` | partially-modeled | A, D, E | Damage/Mana/cast execute; projectile and source rank validation pending |
| fire | `flame-throwing` | not-modeled | C | Fire range increase not modeled |
| fire | `impact` | partially-modeled | C, D | Sourced 3/7/10% direct-Fire hit proc emits target-owned 2s stun aura; movement/AI control and low-rank suppression unavailable |
| fire | `burning-soul` | partially-modeled | B, E | Fire pushback and threat implemented; source spell eligibility still needs certification |
| fire | `improved-flamestrike` | partially-modeled | C | Damage/crit effects execute; real AoE geometry absent |
| fire | `improved-fire-blast` | partially-modeled | C | Cooldown reduction executes; added range is unsupported |
| fire | `improved-fire-ward` | not-modeled | B | Fire Ward mechanics not modeled |
| fire | `blast-wave` | partially-modeled | C | AoE damage executes; Daze/geometry unsupported |
| fire | `combustion` | partially-modeled | D, E | Actual direct landed Fire hit/crit events raise crit chance until 3 crits remove 180s cooldown buff; resisted and periodic hits excluded; multi-target nuances provisional |
| frost | `frost-warding` | partially-modeled | B, C | Ice Barrier absorb increases; ward negation/armor contributions incomplete |
| frost | `frostbite` | partially-modeled | C, D | Rank 5/10/15% direct Chill proc applies owner-targeted 5s Frozen aura and Shatter/Ice Lance effects; enemy control and low spell-rank suppression unavailable |
| frost | `permafrost` | partially-modeled | C | Chill duration extension executes; movement slow cannot affect stationary target |
| frost | `improved-blizzard` | partially-modeled | C | Slow aura emits; moving targets and control not represented |
| frost | `ice-lance` | partially-modeled | C, D | 70707 +300% vs Frozen (4x) and shared 8s Fire Blast lockout execute; external Frozen states and Arctic Reach range partial |
| frost | `arctic-reach` | not-modeled | C | Range geometry absent |
| frost | `ice-block` | partially-modeled | C | Invulnerability/lockout scripted; other control conditions not fully certified |
| frost | `shatter` | partially-modeled | C | Frozen critical chance executes for FoF; external Frozen sources limited |
| frost | `improved-cone-of-cold` | partially-modeled | C | Frost Cone damage scaling executes; geometric coverage limited |
| frost | `fingers-of-frost` | partially-modeled | A, B | Sourced 15s aura and 1/2 rank charges are granted/consumed from actual qualifying Chill hits; Ice/Frost Armor triggers unavailable |
| frost | `winters-chill` | partially-modeled | C, D | Target-owned stacks and 70707 +20% next Cone damage per stack execute; external immunity and source rank validation provisional |
| frost | `ice-barrier` | partially-modeled | B, C | 70707 819+10% Frost SP shield, +15% Frost damage and +5% while shielded execute; pushback/immunity and wider encounter partial |

**Core-DPS candidates:** `wand-specialization` requires an explicit wand action/autoattack (not ordinary rank-60 spell priority), `arcane-wrath` remains missing verified projectile impact/travel timing, `arcane-concentration` has unresolved low-rank proc exclusions, and `fingers-of-frost` lacks armor-triggered procs when scripted damage is configured. These four remaining A candidates should be assessed by whether the corresponding combat conditions exist; do not expand the current encounter model solely to eliminate a warning.

**Not ordinary single-target DPS defects:** range/geometry, enemy movement and immunities, root/silence/interrupt, AoE target positioning and external Frozen-state sources (category C). These are still meaningful capabilities, not excuses to call them fully simulated. Defensive and threat gaps (B) such as ward negation and resist handling also remain explicitly disclosed.

**Historical compatibility:** The six source-fallback no-effect nodes must be labeled **not implemented** before spending points. Valid itemized allocation is not a promise of gameplay implementation. A selected talent's `simulationStatus` and `dataConfidence` remain independent in the existing API; do not create additional badges from these classification categories.

**F3B4 integration defects addressed:** learned class trainer spells across starting presets, Cold Snap's Ice Lance group reset and removal of the automatic Cone-of-Cold prerequisite from explicit custom Cold Snap, off-tree reactive/cooldown automatic priorities, Arcane Mind's resolved stat preview, and rejecting stale imported priority actions or removing them on talent deallocation. These are covered by focused tests and real Chrome fixtures; report CI only after exact-final-head success.
