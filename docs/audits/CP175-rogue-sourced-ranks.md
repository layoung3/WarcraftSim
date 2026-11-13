# CP175 bounded historical Rogue talent corrections

## Source and limits

This milestone uses [gunba/wow-forever-sim rogue.json at 1cc5ae1e9abdbf941b7326d2d90421822dce31ba](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/rogue.json). It is historical, provisional evidence, not certification of the current Forever client, full Rogue mechanics, or a strict-valid build.

The prior GitHub fetch returned a wrapper with decoded content text, not an independently retrieved raw asset. The saved UTF-8 decoded representation has SHA-256 `9776311caefba3f7100b3220ffeaaccfc33dd6fb280c18f21172e85c5e984e09`. That checksum identifies the saved representation only; no independent source blob hash was supplied. The three entries were parsed again from that preserved full representation and compared exactly with the earlier matching-key audit. [Preserved source excerpts](CP175-rogue-sourced-ranks.json) retain descriptions, ranks, zero-based positions, prerequisite locations, spell IDs (including Puncturing Wounds' zero IDs) and this provenance.

## Bounded changes

| Talent | Historical description / rank evidence | Correction and compatibility |
| --- | --- | --- |
| Improved Eviscerate, Combat row 1 column 1, cap 3 | Eviscerate damage +7/13/20% | Exact rank table replaces approximate thirds. Controlled base 100 gives 100/107/113/120; five points gives 500/535/565/600. Base damage tuning, AP/weapon coefficients, CP spend, crit/armor, Energy costs and other damage modifiers are unchanged. |
| Lethality, Assassination row 3 column 3, cap 5 | Critical strike damage **bonus** +4/8/12/16/20% on Sinister Strike, Gouge, Backstab, Mutilate, Ghostly Strike and Hemorrhage | Remove Ambush from the eligible damage list because the explicit source list omits it. The existing `2 + rank * perRankBonus` formula and explicit tuning override stay intact: base 100 rank 5 eligible crit 220, Ambush crit 200, normal 100. Current Gouge is control-only/noncritical; no damaging Gouge mechanic was invented. The exclusion is a reading of that enumerated historical list, not independent live observation. |
| Puncturing Wounds, Combat row 2 column 1, cap 3 | Backstab crit +10/20/30%, Mutilate crit +5/10/15%, Backstab extra CP chance 15/30/45% | Change only default extra-CP chance per rank from 10 to 15. Existing crit bonuses and scalar tuning overrides retain their meaning, including explicit 0, 10 and 100. No changes to processor RNG order, eligible ability, point cap, source/target ownership or separate Seal Fate behavior. |

Improved Expose Armor's absolute-refund tuning contract, Aggression/Eviscerate stacking, Dual Wield Specialization interpretation, Cutthroat and the eight audited source/cap conflicts are deferred. The four differing caps (Precision, Deflection, Improved Kidney Shot, Cutthroat), four absent keys (Improved Garrote, Quick Recovery, Fleet Footed, Master of Subtlety), strict admission, presets and UI are unchanged. No new inferred coefficients, new spells, catalog equipment, endpoints, dependencies or workflow changes are included.

## Before/after evidence, without replacing goldens

Baseline HEAD was `70e7149ae82bb78c70b724b8091093d1c27c86ae`. Before production edits, a strict build and the three existing fingerprint tests passed; the original production DLLs and complete deterministic preset projections were preserved. Baseline API DLL SHA-256 is `7400e1743ae03af1d53e3f4551c2ac837500e354da4069e41cba9f22fedc0052`, Core `4a82df0cf5f15886b1cecf8d7258b4b4bfe3cbd814042f8d9edbccfaa044c067`, Data `13095e163cb2affe7bf46a60b0421f7fdc494f24bb09956b047f668f7d689a73`.

All three original 120-second preset fingerprints remain byte-identical. No golden was updated:

- Assassination: `585EE6668BF114A5789517C2409E0E66E8453F17138FA993E73E0D8A3B3929ED`.
- Combat: `2DF8B06C285F5A99095E5D938CD16B9ED88ED986F57EEBA397A7FA8231764651`.
- Subtlety: `C3AAE253895B47D027782DEAD310E1D13F1CF4202E830B365CA91FDD679821DE`.

Those default timelines do not cast Eviscerate and contain neither Lethality nor Puncturing Wounds ranks. Earlier-priority finishers consume their points: Assassination uses Slice and Dice 5 / Venom 5 / Rupture 2; Combat uses Slice and Dice 8 / Rupture 1; Subtlety uses Slice and Dice 7 / Rupture 1. Expose Armor is disabled and is not the cause. This is an observed consequence of the existing provisional priority/resource model, not a claim of optimal rotation or a reason to alter it in this milestone.

Additional runs against the preserved old DLLs and corrected DLLs exercise each changed mechanic. [Eighteen numerical attribution receipts](CP175-rogue-sourced-rank-receipts.json) summarize the independent comparisons:

- Fast-Energy Eviscerate rank 1/2/3 runs preserve every timestamp, result, actor, target, resource event, cast and finisher count. Only Eviscerate damage, its matching threat event and aggregate damage/DPS/threat totals change. Every old/new Eviscerate amount is reconstructed from the independently captured rank 0 timeline using respectively `1 + rank*.0666666667` and literal 1.07/1.13/1.20. Combat rank 1 total damage changes 72673.333333568→72696.8; rank 2 changes 73142.666667136→73119.2; rank 3 changes 73612.000000704→73612. Counts and unrelated attacks stay fixed.
- Controlled rank 5 Lethality runs change one critical Ambush and its threat event only. The original 220%-of-base result becomes 200%; qualifying attacks, resources, RNG results and cast timing are unchanged. The live runner totals are also reconciled to that one hit delta.
- Repeated-Backstab Subtlety with rank 3 changes total 46988→47288, finishers 24→26 and ending CP 3→0. This is not inferred from DPS alone: the **entire** corrected normalized response/timeline equals old code supplied an explicit 15% override; corrected code supplied explicit 10% exactly restores the old normalized response/timeline. Same-rank comparisons keep Backstab/Mutilate crit bonuses fixed. The default chance fully explains downstream CP and rotation differences.
- All zero-rank, talent-removal, unaffected preset, Lethality-zero and explicit chance-override control projections compare numerically equal. Some non-default JSON decimal strings lose insignificant trailing zeros when the approximate Eviscerate factor is replaced, so those comparisons parse numbers as decimals rather than claiming byte equality. The original three preset hashes are genuinely byte-identical.

The attribution script permits only a sub-1e-20 aggregate tolerance for .NET decimal's 96-bit intermediate rounding, far below the old rank 3 per-hit error. Individual Eviscerate rank amounts use literal expected values in xUnit, not that tolerance. Generated execution GUIDs are excluded from deterministic projections, as in the existing fingerprint tests.

## Tests and acceptance

`CP175_RogueSourcedRankEffects.cs` adds 161 cases. They exercise actual spellbook definitions, the runner's own roll/armor adapters through a test-only reflection harness, the real ability executor and Rogue combat processor, plus public-runner replays. There is no production test hook or weakened visibility.

Coverage includes all Eviscerate ranks with 1/5 points, normal/critical hits, zero/50% armor, source/target ownership and actual 35-Energy spend; unchanged unrelated spell definitions and removal; all Lethality ranks with normal/critical hits across five qualifying attacks (including both Mutilate hands), Ambush, Eviscerate and an auto attack; explicit Lethality tuning; retained Puncturing Wounds crit tables/overrides; literal 15/30/45 threshold-adjacent deterministic seeds; 200 seeds per rank with independent next-random-value checks; exact equality through a chosen tuning threshold; zero/legacy/high chance overrides and JSON round trips; source/target ownership, cap 5, Seal Fate stacking, miss/periodic/non-Backstab negatives; repeated same-rank default-versus-legacy chance timelines, replay, removal and weapon/position gate controls. Existing finisher miss/avoidance and prior Rogue suites remain enabled.

Test-first receipts preserve the initial red runs. Initial test setup mistakes were corrected separately: the half-second harness legitimately regenerates 5 Energy after the 35-Energy cast, and default Assassination does not schedule Backstab. A subsequent overstrong assumption that rank-removal must change finisher count was replaced by the more direct same-rank chance comparison. The source-oracle failures independently exposed 24 Eviscerate cases, 6 Ambush eligibility cases and 6 Puncturing Wounds cases before the production fix. None were suppressed or converted to snapshots from the new implementation.

Final local acceptance:

- Strict Release solution build: 0 warnings, 0 errors.
- Full xUnit: 4,258 passed, 0 failed, 0 skipped; focused new/preview run 254 passed.
- Thirteen pure Node suites, 126 Python cases, JavaScript syntax and whitespace checks pass.
- Real loopback extra-class HTTP: 148 requests pass. The task-owned API was stopped.
- Eighteen before/after attribution receipts pass with no unexplained timeline/resource/cost/rotation deltas.

Baseline binaries, capture harness, complete requests/timelines, failed attempts, comparison script and logs remain under `acceptance-target-semantics-cloud-20261110/rogue-sourced-ranks/`; earlier artifacts are preserved. The SDK/packages are existing task-local official distributions. No local real-browser success is claimed. Existing Actions browser gates are still required for the eventual published correction head. No commit/push, new branch/PR, merge, deployment, artifact deletion, account/security changes or spending occurred in this implementation step.
