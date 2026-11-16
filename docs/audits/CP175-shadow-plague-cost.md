# CP175 Shadow Devouring Plague: additive Mana-cost reductions

## Scope and historical evidence

Accepted predecessor: Elemental Alacrity at `d688a04edd4bad9317a9acd5ad5125c016f78773`, accepted by [Actions 43159543585](https://github.com/layoung3/WarcraftSim/actions/runs/43159543585).

Pinned implementation: `gunba/wow-forever-sim@1cc5ae1e9abdbf941b7326d2d90421822dce31ba`. [Devouring Plague](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/priest/devouring_plague.go#L47-L58) initializes Mana multiplier `100 - 25*rank`. [Active Shadowform](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/priest/talents.go#L442-L499) subtracts 50 from that same multiplier. Rank2 therefore initializes at50 and becomes0 after form activation; registration's zero-to100 default is irrelevant. The final spell cost pipeline clamps nonnegative cost; Mana admission reads that cost, and source spend/five-second refresh occur only for positive cost.

Audit SHA256 `b3523457810fd232535b58212fae4c33c35a9ecc1627bf209c455e26718153cb`; manifest SHA256 `aaa3019cd5127dfe1be34cd3536374ac7631270a2d8bf6317db9619f1e4e243a`. Nine decoded same-pin sources and their reported Git blobs were reverified; ten original connector responses including the retained nonexistent-shadowform.go404 remain under `.task-evidence/next-shadow-talents-20261112`. This verifies retained-content consistency, not independent commit-tree authentication or current-server certification.

## Sole production correction

Only `ForeverPriestShadowSpellbook` changes: after constructing Plague, form-on positive Contagion ranks receive the final clamped combined cost directly, without passing through `instantCost` again. Other spells and the helper remain untouched. Rank0 and form-off retain exact prior operation order and decimal serialization. Provisional local base790 remains790, despite historical source985. User tuning, zero/negative-cost clamping, fixed form-active assumption, cooldowns, damage and eligibility remain unchanged.

Literal rank0/1/2 costs at base790 are **790/592.5/395 without form**, **395/197.5/0 with form**. At base400 they are **400/300/200**, **200/100/0**. Both legacy10/0/41 and strict/UI0/0/36 defaults carry rank2+form. No unspent points are filled and strict admission/UI are unchanged.

## Proof and preservation contract

Source/runtime archives precede production edits. Baseline responses retain all574 preceding Alacrity rows, including290 legacy responses,53 native request replays and active Feral/genuine Restoration controls. Only verified publication `sourceCommit` metadata advances when comparing the preceding stage's pre-publication captures. Every candidate request is byte-bound to its baseline request.

All affected retained native Shadow requests, typed scenarios and three seeded precombat goldens have independent old-runtime zero-Plague-tuning oracles. Each changes exactly the authored tuning field (or adds its containing tuning object when absent); response endpoints have no tuning/request echo to remove. Complete response structure, events, timing, damage/healing/backlash, finite health, resources, counts and notes compare against those oracles. Known generated event identities are compared bijectively at narrow paths, not dropped. This accounts for downstream random-stream and affordability changes without independently changing unrelated mechanics.

Rank1 is proved separately: abundant-Mana no-regeneration fixtures retain literal starts0/24/48, with exact default197.5/custom100 debits and final Mana. A strict full-rotation base300 fixture expects75; unchanged old code with explicitly tuned base200 yields exactly75 (`200*.75*.5`), independently matching `300*(1-.25-.5)`. This positive-cost oracle is not an extrapolation from rank2 free casts.

The three original goldens are first reproduced field-for-field with explicit HTTP input latency0, matching direct-runner semantics. An initial80ms-default HTTP capture is preserved and excluded from golden acceptance. New full response envelopes must match the frozen zero-cost oracle before golden replacement. Old/new complete events and summary witnesses are retained in `golden-causal-proof.json`. CP129's obsolete25% Plague expectation becomes0; its Mind Blast half-cost assertion remains unchanged.

The direct regression suite covers literal definitions and actual executor debits, zero-Mana free-cast admission, exact/below rank1 affordability, paid/free five-second-rule interactions, explicit zero/negative/decimal tuning, strict/default/custom maps, validation, preparation/preview caps, serialization/replay and repeat/concurrent caller isolation. The harness observes internal wake events at the processor boundary because public timeline intentionally excludes them; no Core behavior is changed. Core's existing zero-cost ResourceChanged event is retained and does not restart suppression.

Evidence, interrupted invocations and fixture-development failures remain under `.task-evidence/shadow-plague-cost-20261112`. Full final-source aggregate/API gates, source/runtime custody and independent review remain required; focused tests alone are not acceptance. Coordinator owns publication and exact-new-head Actions/browser acceptance.

## Explicit boundaries

Darkness/form/Weaving damage composition, critical multiplier, Shadow Focus, on-death spread, DoT snapshot/proc eligibility and other talent leads remain separate. No Shadow potion support is introduced; current restricted Shadow has no shared combat-item processor. No Core, source services/accounts, workflow, permission, branch/PR metadata, merge or deployment change is part of this stage. Historical cost parity does not certify provisional local spell bases or broader current-client behavior.
