# CP175-F3B0 — Mage 70707 talent-by-talent implementation coverage

**Snapshot:** November 2, 2026; client **1.60.1.70707**. Source [Forever DB Mage calculator](https://wowfdb.com/talents/mage/) and [client change history](https://wowfdb.com/classes/mage/changes/). Source is published/client-derived and **provisional; not in-game mechanically verified**. Status describes the bounded solo engine, not universal PvP/raid completeness.

**Coverage:** 53 definitions: Arcane 18, Fire 17, Frost 18. **16 fully modeled, 28 partial, 9 not modeled** (conservative inspected-engine classification, not 53 independent combat attestations). Declared typed-rank keys: 43 (Arcane 14, Fire 14, Frost 15). Ten historical-only tree nodes cannot grant executable effects.

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
| fire | `ignite` | 5 | 2 | — | Yes | Partial | Source-owned rolling fire critical DoT executes; precise rollover still provisional |
| fire | `flame-throwing` | 2 | 2 | — | No | None | Fire range increase not modeled |
| fire | `impact` | 3 | 2 | — | No | None | Stun and low-rank proc penalties not modeled |
| fire | `burning-soul` | 3 | 3 | — | Yes | Partial | Fire pushback and threat implemented; source spell eligibility still needs certification |
| fire | `improved-flamestrike` | 3 | 3 | — | Yes | Partial | Damage/crit effects execute; real AoE geometry absent |
| fire | `pyroblast` | 1 | 3 | — | Yes | Partial | Talent-gated direct/periodic damage; current-rank client details provisional |
| fire | `improved-fire-blast` | 2 | 3 | — | Yes | Partial | Cooldown reduction executes; added range is unsupported |
| fire | `improved-scorch` | 3 | 4 | — | Yes | Full | Landed Scorch vulnerability per target and stacks execute |
| fire | `improved-fire-ward` | 2 | 4 | — | No | None | Fire Ward mechanics not modeled |
| fire | `heating-up` | 1 | 4 | pyroblast 1/1 | Yes | Partial | Crit stacks/Pyro cast execute; precise Fire Blast automatic stack rules not certified |
| fire | `master-of-elements` | 3 | 4 | — | Yes | Full | Fire/Frost critical base mana refunds execute |
| fire | `critical-mass` | 3 | 5 | — | Yes | Full | Fire critical chance rank-scaled |
| fire | `blast-wave` | 1 | 5 | — | Yes | Partial | AoE damage executes; Daze/geometry unsupported |
| fire | `fire-power` | 3 | 6 | — | Yes | Partial | Fire effect count/damage executes; published eligible-effect list needs correction |
| fire | `combustion` | 1 | 7 | critical-mass 3/3 | Yes | Partial | Critical stack/consumption executes; rank/proc edge cases remain |
| frost | `frost-warding` | 2 | 1 | — | Yes | Partial | Ice Barrier absorb increases; ward negation/armor contributions incomplete |
| frost | `improved-frostbolt` | 5 | 1 | — | Yes | Full | Frostbolt cast-time decrease executes |
| frost | `precision` | 3 | 1 | — | Yes | Full | Hit across every spell school rank-scaled |
| frost | `ice-shards` | 5 | 2 | — | Yes | Full | Frost critical damage bonus executes |
| frost | `frostbite` | 3 | 2 | — | No | None | Freeze proc and low-rank penalties not modeled |
| frost | `improved-frost-nova` | 2 | 2 | — | No | None | Nova cooldown/range mechanic not modeled |
| frost | `permafrost` | 3 | 2 | — | Yes | Partial | Chill duration extension executes; movement slow cannot affect stationary target |
| frost | `frost-channeling` | 3 | 3 | — | Yes | Full | Frost cost and threat modifiers execute |
| frost | `improved-blizzard` | 3 | 3 | — | Yes | Partial | Slow aura emits; moving targets and control not represented |
| frost | `ice-lance` | 1 | 3 | — | Yes | Partial | FoF Frozen multiplier exists; 70707 300% increased bonus and shared Fire Blast cooldown not corrected yet |
| frost | `arctic-reach` | 2 | 4 | — | No | None | Range geometry absent |
| frost | `ice-block` | 1 | 4 | — | Yes | Partial | Invulnerability/lockout scripted; other control conditions not fully certified |
| frost | `shatter` | 3 | 4 | — | Yes | Partial | Frozen critical chance executes for FoF; external Frozen sources limited |
| frost | `improved-cone-of-cold` | 3 | 5 | — | Yes | Partial | Frost Cone damage scaling executes; geometric coverage limited |
| frost | `cold-snap` | 1 | 5 | — | Yes | Partial | Existing cooldown resets; not all current Frost abilities included |
| frost | `fingers-of-frost` | 2 | 5 | ice-lance 1/1 | Yes | Partial | Chill-triggered charges execute; Armor-triggered procs unavailable |
| frost | `winters-chill` | 5 | 6 | — | Yes | Partial | Target-specific stack/Cone interaction executes; 70707 20% damage-taken per stack not reconciled |
| frost | `ice-barrier` | 1 | 7 | cold-snap 1/1 | Yes | Partial | Absorb and Frost bonus execute; 70707 revised Frost damage values pending |

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
