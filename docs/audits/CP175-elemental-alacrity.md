# CP175 Elemental Alacrity: exact partial-rank cast reductions

## Scope and source

Accepted predecessor: Balance Thorns at `b210558aac43832dd04b742bab7553e821f86289`, accepted by [Actions 43154827830](https://github.com/layoung3/WarcraftSim/actions/runs/43154827830).

Pinned historical source is `gunba/wow-forever-sim@1cc5ae1e9abdbf941b7326d2d90421822dce31ba`. [talents.go lines 139–142](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/shaman/talents.go#L139-L142) explicitly implements **0/170/330/500 milliseconds**. [electric_spell.go](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/shaman/electric_spell.go#L54-L60) and [lava_burst.go](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/shaman/lava_burst.go#L65-L75) subtract this reduction before haste.

Preserved read-only audit SHA-256: `a8480c4b69c89c06bcec87077b2db001b23c8649a4aa19fb67dbd0429b7d9689`; manifest SHA-256: `a4e2a35c88f1ce0b86f68b0696111ffa3d013bdeea958933d91e04f03d0b6728`. Seven complete connector responses, decoded files and recomputed reported Git blobs are retained under `.task-evidence/next-elemental-talents-20261112`. This establishes retained-content consistency, not independent commit-tree authentication or current-client certification.

The sole production change is one explicit reduction table shared by four existing expressions in `ForeverElementalShamanDps.cs`. Bolt, rank-7 Bolt and Lava base times become **2.50/2.33/2.17/2.00 seconds**; Chain becomes **2.00/1.83/1.67/1.50**. Rank zero and three remain unchanged. All existing base tuning, cost, GCD, cooldown, damage, other talents and admission rules remain intact.

## Execution and acceptance contract

The executor schedules decimal completion time directly. It checks affordability at cast start and debits Mana at successful completion. Literal Convection 5 costs remain Bolt 198, rank 7: 121.5, Chain 283.5 and Lava 238.5; corresponding zero-power ordinary damage remains 196/147/123/220. Boundary tests distinguish completed paid casts from unfinished casts.

Existing Mastery mutates eligible definitions by minus/plus .5 seconds, rather than restoring cached values. Rank 1/2 active eligible times are 1.83/1.67; Chain cast time is unchanged by Mastery. Existing Chain cooldown removal/restoration is preserved. In-flight timing is snapshotted. Same-timestamp expiry ordering is retained: a queued decision may resolve timing before the queued AuraRemoved callback even when its visible start appears after removal. Direct runner tests use zero input delay. The typed Elemental HTTP endpoint retains its .08-second default; the shared level-60 endpoint retains its explicitly resolved zero-delay default in these fixtures.

Haste applies only on the baseline-prepared Elemental runner path and follows Alacrity/Mastery subtraction. Tests exercise 25% authored equipment haste through actual preparation. Naked baseline preparation can contribute critical chance even when the explicit manual input is zero; damage assertions preserve that existing behavior. GCD remains 1.5 seconds.

The identical finalized 80-case regression source produces **38 failures/42 passes on old production**, then **80 passes on corrected production**. Coverage includes literal four-spell rank curves, real start/completion/resource/damage events, prepared haste, Mastery expiry/in-flight restoration, exact boundaries, strict legal Convection 5 + Alacrity allocations, malformed/prerequisite rejection, JSON request replay, repeated/concurrent request reuse and preview isolation. Strict partials have exact totals 5+r and matching rank maps with incompatible Overload/Fury absent. No preview timing field or frontend/admission behavior is added. Existing CP166 prerequisite and CP137/CP133 rank-three assertions are unchanged.

Old accepted source/runtime and complete controls are frozen before production edits. Complete response comparisons must independently project partial-rank schedules, economic events, summary counts, resources and internal-event counts, retaining every other response field. Rank 0/3/default, earlier Balance, genuine Feral/Restoration and legacy responses remain complete controls. Generated event identities are compared bijectively at known paths, not discarded arbitrarily.

All local evidence and retained fixture-development attempts are under `.task-evidence/elemental-alacrity-20261112`. Focused tests alone do not replace full aggregate/API gates and independent review. Coordinator owns publication and exact-new-head GitHub Actions/native-browser acceptance; no merge or deployment is authorized.

## Explicit limitations

- Historical reduction parity only; existing provisional base tuning, haste divisor, hit/crit/resistance and broader combat models are not certified.
- Call of Flame's base-only helper comment conflicts with actual source spell/core execution that applies it after spell power. No base-only rewrite is made.
- Chain Overload coefficient, base damage and child/proc semantics require a separate audit and remain unchanged.
- Full/default rank-three behavior is unchanged. Partial-rank timing can change boundary cast counts, Mana and total damage, so no blanket unchanged-DPS claim is made.
