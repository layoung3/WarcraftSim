# CP175-F3A — Warrior talent-by-talent coverage gate

**Scope:** existing draft PR #28, branch `cp175-shared-character-simulation`. Warrior only. No class-update PR was open at the start of F3A. Last inspected published beta talent calculator: [WoW Forever DB Warrior build 1.60.1.70707](https://wowfdb.com/talents/warrior/) (November 2, 2026); [Forever Guides 70707](https://theforeverguides.com/tools/talents/warrior/) is secondary context. Client-derived tooltips remain provisional, **not independently verified live-server formulas**. Repository talent catalog previously pinned 70338 preview; F3A source metadata explicitly labels 70707 as a provisional reviewed reference.

The table lists **declared engine coverage**, not a guarantee of full individual mechanical certification. A catalog ID, icon or tooltip alone is never counted as an executable talent. Current runtime coverage needs event-level tests where applicable; missing encounter actors, target control and full mitigation formulas remain limitations.

| Tree | Talents | Declared executable | Partial | No executable mechanic |
| --- | ---: | ---: | ---: | ---: |
| arms | 17 | 11 | 4 | 2 |
| fury | 17 | 6 | 7 | 4 |
| protection | 18 | 9 | 5 | 4 |

## Per-talent coverage matrix (build 70707 provisional)

| Tree / position | Talent | Cap | Prerequisite | Declared implementation | Numeric/event intent and remaining qualification |
|---|---|---:|---|---|---|
| arms 1:1 | `improved-heroic-strike` | 3 | — | Declared executable | Reduces Heroic Strike Rage cost by 1 per rank. |
| arms 1:2 | `deflection` | 5 | — | Declared executable | Grants 1% parry chance per rank against incoming melee. |
| arms 1:3 | `improved-rend` | 3 | — | Declared executable | Increases Rend bleed damage by 12/23/35%. |
| arms 2:1 | `improved-charge` | 2 | — | No executable effect | Increases Charge Rage. |
| arms 2:2 | `improved-tactical-mastery` | 5 | — | Declared executable | Extra 3 Rage retained per rank when automated stance dancing is enabled; baseline retained Rage 10. |
| arms 2:3 | `improved-overpower` | 2 | — | Declared executable | Increases Overpower critical chance by 25% per rank. |
| arms 3:1 | `anger-management` | 1 | `improved-tactical-mastery` | Partially modeled | Gain 1 Rage every 3 seconds in combat (out-of-combat retention not modeled). |
| arms 3:3 | `deep-wounds` | 3 | `improved-rend` | Declared executable | Critical strikes apply a rolling bleed. |
| arms 4:1 | `spearing-strike` | 1 | — | Declared executable | Battle Stance weapon strike; additional damage to specific creature types. |
| arms 4:2 | `two-handed-weapon-specialization` | 3 | — | Partially modeled | Preview: 1% physical damage per rank when two-handing; weapon-specific delivery still pending. |
| arms 4:3 | `impale` | 2 | `deep-wounds` (1 rank) | Declared executable | Improved critical damage of abilities; requires 1/3 Deep Wounds. |
| arms 5:1 | `bloodthrill` | 5 | — | Declared executable | Your Rend-afflicted targets can enable Overpower on main-hand melee hits. |
| arms 5:2 | `sweeping-strikes` | 1 | — | Partially modeled | Your next five melee strikes also hit another target. |
| arms 5:3 | `weaponmaster` | 5 | — | Partially modeled | Different effects for swords, axes, polearms, maces, and staves. |
| arms 6:1 | `improved-slam` | 2 | — | Declared executable | Reduces Slam cast/GCD by 0.25s per point and cooldown by 3s; no longer suspends weapon swings. |
| arms 6:3 | `improved-hamstring` | 3 | — | No executable effect | Hamstring can immobilize the target. |
| arms 7:2 | `mortal-strike` | 1 | `sweeping-strikes` | Declared executable | Unlocks Mortal Strike. |
| fury 1:1 | `booming-voice` | 5 | — | Partially modeled | Shouts cover a wider area and cost less Rage. |
| fury 1:3 | `cruelty` | 5 | — | Declared executable | +1% melee critical chance per rank. |
| fury 2:1 | `lingering-rage` | 5 | — | No executable effect | Delays Rage decay after leaving combat. |
| fury 2:3 | `unbridled-wrath` | 5 | — | Partially modeled | Chance for 1 Rage on weapon-damage hits; other combat modes pending. |
| fury 3:1 | `furious-precision` | 3 | — | Declared executable | 4/7/10% additional off-hand hit chance. |
| fury 3:2 | `piercing-howl` | 1 | — | No executable effect | AoE slow. |
| fury 3:3 | `blood-craze` | 3 | — | Partially modeled | Healing after taking certain damage. |
| fury 4:1 | `dual-wield-specialization` | 5 | — | Partially modeled | +5% off-hand damage and +10% white-hit Rage per rank (queued replacement interactions pending). |
| fury 4:2 | `raging-blows` | 1 | — | Declared executable | Reduces Cleave/Whirlwind cost by 3 Rage. |
| fury 4:3 | `enrage` | 5 | — | Partially modeled | Physical damage bonus after receiving damage. |
| fury 4:4 | `improved-execute` | 2 | — | Declared executable | Reduces Execute Rage cost. |
| fury 5:1 | `improved-berserker-rage` | 2 | — | No executable effect | Berserker Rage grants Rage. |
| fury 5:2 | `death-wish` | 1 | — | Partially modeled | 30s physical damage buff; 3 min cooldown. |
| fury 5:4 | `improved-intercept` | 2 | — | No executable effect | Reduces Intercept cooldown. |
| fury 6:2 | `flurry` | 5 | `death-wish` | Declared executable | Attack speed increased after melee crit; requires Death Wish. |
| fury 6:3 | `gore-drinker` | 2 | `enrage` | Partially modeled | Healing from following melee attacks; requires Enrage. |
| fury 7:2 | `bloodthirst` | 1 | — | Declared executable | Unlocks Bloodthirst. |
| protection 1:1 | `improved-bloodrage` | 2 | — | Declared executable | Increases Rage from Bloodrage. |
| protection 1:2 | `shield-specialization` | 5 | — | Declared executable | Block chance and Rage when blocking. |
| protection 1:3 | `iron-will` | 5 | — | No executable effect | Reduces stun and fear duration. |
| protection 2:1 | `anticipation` | 5 | — | Declared executable | Increases Defense skill. |
| protection 2:2 | `improved-revenge` | 3 | — | Declared executable | Increases Revenge damage. |
| protection 2:4 | `improved-thunder-clap` | 3 | — | Partially modeled | Reduces Thunder Clap cost. |
| protection 3:1 | `last-stand` | 1 | — | Declared executable | Temporary maximum health increase. |
| protection 3:2 | `master-of-defense` | 2 | `shield-specialization` | Declared executable | Generate Rage when avoiding attacks with a shield. |
| protection 3:3 | `improved-disarm` | 3 | — | No executable effect | Reduces Disarm cooldown. |
| protection 3:4 | `defiance` | 3 | — | Declared executable | Defensive Stance shield threat modifier. |
| protection 4:1 | `improved-sunder-armor` | 3 | — | Declared executable | Reduces Sunder Armor cost. |
| protection 4:2 | `vanguard` | 1 | — | No executable effect | Charge becomes available in Defensive Stance. |
| protection 4:4 | `improved-shield-bash` | 2 | — | No executable effect | Improves Shield Bash silence chance. |
| protection 5:1 | `improved-shield-wall` | 2 | — | Declared executable | Reduces Shield Wall cooldown. |
| protection 5:2 | `concussion-blow` | 1 | — | Partially modeled | Stuns the target. |
| protection 5:3 | `focused-rage` | 3 | — | Partially modeled | Reduces modeled Protection offensive Rage costs; other Warrior action costs remain under review. |
| protection 6:2 | `bastion` | 5 | — | Partially modeled | Modeled as physical damage +2% per rank with a shield; nonphysical damage and exact delivery are not verified. |
| protection 7:2 | `shield-slam` | 1 | `concussion-blow` | Partially modeled | Unlocks Shield Slam: 421–439 damage plus Block Value. Additional threat and dispel remain unverified. |

## Corrections implemented during F3A

- **Source/tier:** Impale now requires 1 rank of Deep Wounds (not max 3); its earlier-tier 15-point rule and Improved Rend prerequisites remain enforced. The same required rank travels to the frontend talent tile and backend `validate` response. Data-confidence and mechanical-completeness fields remain separate.
- **Arms outside preset:** learned Spearing Strike is registered with its normal Battle Stance, Rage and cooldown gates even under the Fury preset; learned Sweeping Strikes is likewise registered independent of template. Automatic priority may propose either only when learned and relevant, with the engine still enforcing stance and target count.
- **Protection outside preset:** Shield Slam and Concussion Blow (with a shield), and Last Stand where talented, may be registered without selecting the Protection template. The existing shield, Rage, cooldown and Defensive Stance requirements remain binding.
- **Fury outside preset:** previously implemented Bloodthirst cross-tree execution was preserved, including a negative test when the rank is removed. Existing Flurry, Enrage, Unbridled Wrath and improved-cost behaviors were not replaced.
- **Custom rotation:** unlocked off-tree actions are legal; unallocated cross-tree actions are rejected with an explicit message and cannot execute. Legacy Fury Overpower remains usable when stance dancing is enabled. Saved rotation dependencies remain talent-gated.
- **Frontend:** Warrior nodes use the existing F2B1 source-confidence/implementation-status indicator, with 70707 provisional source labeling. The passive data confidence never automatically disables legal numerical effects.

## Source and scope limitations

- **Not newly implemented:** Improved Charge/Intercept, lingering out-of-combat Rage, AoE Piercing Howl control, Improved Hamstring, Iron Will control-duration effects, Vanguard Charge stance permission, Disarm/Shout crowd control and client-uncalibrated procs remain as cataloged. Do not represent them as fully executable.
- **Concussion Blow:** ability cost/cooldown is represented, but actual stun/AI response remains partial.
- **Shield Slam:** current static base damage and Block Value scaling are supported; its separate dispel and exact bonus-threat mechanisms remain provisional.
- **Protection browser blocker:** the current usable equipment picker has very limited reviewed shields. A no-shield build must not be represented as an actual Shield Slam cast; isolated manual-shield runner tests can certify the existing talent engine while browser target eligibility remains separately constrained.
- **Other source ambiguity:** the published 70707 data references both 640–670 and 421–439 for Shield Slam (tooltip vs change description). No value is changed until this inconsistency is independently resolved. Gore Drinker/Flurry have current source prereqs and existing event processors, but full encounter outcome certification is not inferred solely from metadata.

## Validation boundary

F3A should only be marked complete once the exact-final-head strict Release, xUnit, Python, DOM/JS and Chrome job succeeds and new cross-tree tests prove combat casts, proc/mitigation behavior and deterministic replay. Existing CP175 fixtures must remain importable. The ledger records any acceptance shortfall; PR #28 stays draft and unmerged.
