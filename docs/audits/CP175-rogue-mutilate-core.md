# CP175 bounded historical Mutilate correction

## Source and scope

Pinned [gunba/wow-forever-sim Rogue tree at 1cc5ae1e9abdbf941b7326d2d90421822dce31ba](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/rogue.json) describes Mutilate as **75% weapon damage plus 13 with each weapon, 20% increased damage against Poisoned targets, and 2 Combo Points**. The [preserved entry](CP175-rogue-mutilate-source.json) has rank array `[75,13,20,2]`, cap1, zero-based location row4/column1, and spell ID0. That zero ID is not silently replaced with an invented client identifier.

The source is **historical/provisional, not current-client or multiplayer certification**. It was fetched as a tool wrapper containing decoded text, not independently downloaded raw bytes. SHA-256 `9776311caefba3f7100b3220ffeaaccfc33dd6fb280c18f21172e85c5e984e09` identifies the retained decoded UTF-8 representation only; no independently verified source blob hash was supplied. This correction does not establish complete Rogue talent coverage, optimal rotations, party poison rules or source-validity of other coefficients.

Only two core DPS behaviors change:

- `ForeverRogueTuning.MutilateBonusDamagePerHand` defaults to13 instead of17.25. Explicit nonnegative tuning values retain their existing meaning, including0, old saved17.25,13 and custom29. Missing serialized properties select13; snapshot/JSON round trips do not substitute13 for explicit0. No new nullable or versioned scalar contract was introduced.
- Mutilate's existing single20% modifier recognizes the target's live exact `deadly-poison`, `wound-poison`, `mind-numbing-poison` or `crippling-poison` aura. Multiple qualifying auras still yield1.2 once. Equipped poison, remaining charges, attempted/failed applications, another target's aura, unrelated names and substring lookalikes do not establish poisoned state.

These are existing `RoguePoisonProcessor` aura identities, not invented utility mechanics. Deadly uses its existing configurable12-second duration, stacks and ticks. Wound, Mind-numbing and Crippling use the existing configurable12-second utility duration. Wound's provisional healing multiplier and Mind-numbing's provisional cast-time multiplier remain unchanged. Crippling remains a state marker; no movement/PvP effect was added. Proc probabilities, charge preservation/consumption, application order, talent admission and allocations are unchanged by this core correction.

**Instant Poison remains direct damage only.** It applies no lingering aura, so an earlier Instant Poison hit does not qualify a later Mutilate impact. No duration or poisoned-state aftereffect was invented from the tooltip.

## Compatibility conventions and timing

The former Deadly-only query had no source-actor filter. The expanded exact-key query deliberately preserves that target-state convention. Synthetic foreign-source tests document compatibility; they do not establish live party/other-actor eligibility or extend the supported solo runner.

Eligibility is evaluated at each actual impact and uses `AppliedAt <= time < ExpiresAt`. A stale aura still in a collection does not count at its exact expiry. Existing apply/refresh/remove behavior remains authoritative; refreshed poison survives an older queued expiry and ends at its refreshed expiry.

The normal spellbook's two Mutilate effects have zero travel time. The executor resolves both inside the same cast-completion handler before dispatching queued Damage events. Therefore a newly procced poison from the first hand does **not** boost the second hand of that normal cast. Both unpoisoned hits remain88/43. A test-only delayed offhand effect proves the distinct impact rule: if the first hand's real poison application occurs before the delayed second impact, the second hit becomes51.6 while the first stays88. This synthetic delay is not a new Mutilate mechanic or a source claim about simultaneous hands.

The existing Mutilate branch returns its poisoned multiplier separately from previously assembled conditional bonuses. This milestone does not opportunistically change absent-source Master of Subtlety or cap-conflicting Improved Kidney Shot behavior. Existing weapon/AP/offhand scaling, Opportunity, crit/armor, costs, point generation and Seal Fate handling remain intact.

## Independent numerical oracles

At AP0, main weapon100, off weapon80, Dual Wield Specialization0 and Opportunity0:

- Unpoisoned: `.75*100+13 = 88`; `.75*(80*.5)+13 = 43`; total131.
- Poisoned: `88*1.2 = 105.6`; `43*1.2 = 51.6`; total157.2.
- Explicit17.25 preserves92.25/47.25 unpoisoned and110.7/56.7 poisoned.
- Explicit0 preserves75/30 unpoisoned and90/36 poisoned.

A second oracle isolates AP/offhand handling: AP140, both weapon speeds2, main100, off80. Main is `.75*(100+140*2/14)+13 = 103`. Offhand is `.75*(80*(.5+.05*rank)+140*2/14*.5)+13`, giving50.5 at Dual Wield Specialization0 and65.5 at rank5. The flat13 is not incorrectly halved or rank-scaled with the offhand weapon component.

The actual application/executor matrix independently checks all four poisons with normal/critical hits, zero/50% armor, and Opportunity0/2. Expected raw damage is the literal105.6/51.6 oracle times Opportunity1/1.1 and crit1/2, with armor applied afterward. It checks both hands, two CP once,60 Energy spend, charges, aura identities/duration and no utility-poison direct damage. Existing sourced-rank tests continue to cover Lethality and Seal Fate behavior.

## Pre-edit capture and causal attribution

The checkout was published HEAD `bcf91d233bd7846ff9ed948a31692b26c180bebe`. Existing pre-edit binaries report `AssemblyInformationalVersion` revision `c520c80000d2a36ced8a410639d0f49711c3ae45`, not a freshly built clean bcf revision. The prior strict-stage README identifies c520 as its base, its successful strict build log is preserved, and all seven entries in that stage's frozen source manifest match the published bcf files byte-for-byte. This supports the prior precommit working-tree build provenance; the informational stamp still identifies c520 and is not relabeled bcf. The comparison is retained in `baseline-strict-stage-provenance.json`. `git diff` confirms the tuning, spellbook, combat/poison processors, runner, request and snapshot mechanics sources are identical between c520 and bcf. The independent old fingerprints and explicit-override captures establish the numerical baseline without claiming fresh bcf binary provenance. Before production edits, API/Core/Data DLLs were copied and48 deterministic scenarios captured with original requests, complete responses/timelines and the established fingerprint projection. Preserved API SHA-256 is `76f6d3c93c10348f56ae1c0d4d995327361c8ad56269f96d2bd967b2d991afb6`, Core `f0b50fe55eaf12b6ec6e891ee798769a0e0df9b8e68953730a97a5bd44eed7ad`, Data `a0974051eb0e3fdfbece64b719de44145eee0be45447f6f3d853b68b67bc93e5`.

No unexplained golden was replaced. The existing Assassination hash `585EE6668BF114A5789517C2409E0E66E8453F17138FA993E73E0D8A3B3929ED` remains asserted with explicit17.25. New default hash `EE6D98EC2E9865F9BE6C5ED95D3B85C07813852DAB4D8BC8485EF21F11AE14CE` was independently captured from **preserved old DLLs supplied explicit13**, before the correction. New explicit17.25 exactly restores old default gameplay; new default exactly equals old explicit13 gameplay.

Of48 same-request-variant before/after projections,38 are byte-identical and10 Assassination cases change. All Combat/Subtlety variants, all zero-Mutilate controls and explicit0/13/17.25/29 default-poison controls remain identical. The original Combat/Subtlety hashes remain `2DF8B06C285F5A99095E5D938CD16B9ED88ED986F57EEBA397A7FA8231764651` and `C3AAE253895B47D027782DEAD310E1D13F1CF4202E830B365CA91FDD679821DE`.

[The50 comparison receipts](CP175-rogue-mutilate-receipts.json) include those48 comparisons plus the two cross-code default/override checks. They validate the complete normalized gameplay response, not only aggregate DPS:

- Default Assassination:41878.828571428571… →41751.328571428571…, exactly127.5 less damage.22 landed Mutilate hands change by4.25 times their existing crit multiplier;0 are persistently poisoned because the default equips Instant Poison. All proc outcomes, casts, resources, results, timing and unrelated damage stay fixed.
- Deadly-only:34762.442857142857… →34583.942857142857…,178.5 less.23 landed hands retain the existing1.2 multiplier; only the flat default changes. Explicit17.25 restores the entire old Deadly-only gameplay result.
- Wound, Mind-numbing and Crippling: each28470.285714285714… →29315.942857142857…. Every one of23 landed hands is independently reconstructed as `(oldRaw/crit - 17.25 + 13)*1.2*crit` from prior actual application/expiry events. Only Mutilate damage and its matching threat change, with aggregate totals reconciled.
- With explicit17.25 fixed on both sides, each utility-poison run becomes29494.442857142857…. Each eligible Mutilate hit is exactly its old damage times1.2. The Wound explicit13 control also isolates the predicate at the new scalar. These controls separate the aura correction from the default correction.
- No-poison Assassination loses131.75 from24 landed hands, solely from13 replacing17.25.

The verifier checks every response/timeline field, allowing only reconstructed Mutilate damage/raw amounts and displayed damage text, matching threat amounts and reconciled damage/DPS/threat aggregates. It excludes only the corrected display `ModelingNotes` and generated identity GUIDs; request comparison excludes its generated `Character.Id`. The complete response including modeling notes is **not** claimed byte-identical. The established gameplay fingerprint is byte-identical for unchanged controls. Decimal comparisons permit less than1e-20 solely for .NET96-bit intermediate rounding, far below meaningful damage differences; literal unit-test oracles are exact.

## Tests and remaining acceptance

`CP175_RogueMutilateSourceCorrections.cs` adds92 cases covering source defaults, old/zero/custom serialized overrides, nonzero AP, real applications/impacts, crit/armor/Opportunity composition, exact keys, source convention/target isolation, half-open boundaries, refresh/removal, multiple poisons, Instant-only negatives, ordinary/delayed hand ordering, unsuccessful application controls and unchanged non-Mutilate definitions. The preview suite adds the independent old-code13 fingerprint while retaining all original goldens.

Test-first evidence is preserved. The first build caught a concurrent UI test before its controller method was present and is **not** a mechanics red run. The second strict build succeeded against unchanged Mutilate production:91/92 new cases failed and the unrelated-definition control passed. Explicit override cases isolate the missing utility-poison predicate independently of the default13 failures. After the two corrections, the strict Release solution build had0 warnings/errors and all731 broad Rogue/CP144 cases passed, including the92 new cases, CP144 poison behavior, prior sourced ranks, preview, legacy fingerprints, strict38/36 parity and the concurrent Rogue UI contract cases.

The final combined build/full tests/HTTP acceptance and existing Actions real-browser gate remain required for the combined milestone. No local real-browser success, full-suite result or CI success for this unpublished change is claimed here. A subsequent wording-only historical catalog note accurately identifies restricted UI admission and unchanged legacy **talent allocations**, not unchanged combat tuning; the combined build must include that note.

All binaries, requests/results/timelines, failed attempts, capture harness, comparison script and logs remain under `acceptance-target-semantics-cloud-20261110/rogue-mutilate-core/`. Prior evidence is preserved. No branch, commit, push, PR, merge, deployment, dependency/security/account change or purchase occurred in this bounded stage.
