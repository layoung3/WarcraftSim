# CP175 bounded Rogue damage corrections

Status: independently reviewed local pass after accepted Combat UI head `fa53137e8194e7c84dd84fdae74c54f3ef265cae`. Final local evidence is frozen. Publication and new exact-head complete CI/browser acceptance remain pending. This record does not supersede prior evidence or certify overall completion.

## Source and implementation boundary

The pinned [historical Rogue tree](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/rogue.json) supplies these two facts:

- Ghostly Strike rank `[125,180,15,7,1]` explicitly distinguishes a dagger equipped in the **main hand**.
- Hemorrhage rank `[100,145,15,15,1]` specifies 15% increased Rupture damage **from the Rogue**, lasting 15 seconds.

The retained decoded-content representation has SHA-256 `9776311caefba3f7100b3220ffeaaccfc33dd6fb280c18f21172e85c5e984e09`. This is historical/provisional tooltip and rank evidence, not independently retrieved raw bytes, an upstream executable implementation or current-client certification. It does not certify AP normalization, attack tables or every other local mechanic.

Only three product files change:

1. [`ForeverRogueSpellbook.cs`](../../WarcraftSim.Data/Forever/Rogue/ForeverRogueSpellbook.cs) replaces Ghostly Strike's old legacy-flag 110%/135% coefficients with 125%/180% selected by an explicit main-hand predicate. Its old positional/named `Create(..., bothDaggers, behindTarget)` call meaning survives through a delegating overload. That old signature can only infer main-hand dagger from `bothDaggers`; it is not exact mixed-hand identity. Backstab/Ambush/Mutilate retain their original legacy-flag gates.
2. [`RogueSimRunner.cs`](../../WarcraftSim.Api/Simulation/RogueSimRunner.cs) passes the existing validated main-hand type without reconciling or mutating the independent weapon fields or legacy flag.
3. [`RogueCombatProcessor.cs`](../../WarcraftSim.Api/Simulation/RogueCombatProcessor.cs) requires the acting Rogue's live Hemorrhage mark on the actual Rupture target and applies 1.15 once. Foreign-only marks no longer grant the old 1.10 bonus. Existing case-insensitive actor identity, half-open expiry, refresh identity and failed-refresh behavior remain intact. Rupture still snapshots target-owned combo points but evaluates the mark live at each tick.

Hemorrhage's direct 100%/145% conversion is deferred: its tooltip does not identify which hand qualifies, and the editable +45 default needs a separate compatibility decision. Direct damage, that numeric default and explicit overrides remain unchanged. No DTO, cap, rotation, cost/cooldown, Core, UI, strict-admission, dependency or workflow-permission change is included. Ghostly Strike retains its seven-second defensive marker, which incoming-defense calculation does not consume; no implemented dodge benefit is claimed.

## Numerical and route contracts

Independent literal oracles use actual spellbook, executor, roll, armor, aura and combat-processor paths. With weapon damage 100 and AP 0, Ghostly Strike is 125 without a main-hand dagger and 180 with one. At AP 140 and main-hand speed 2, it is 150/216 before crit/armor. Rupture base tick 24 with snapshotted CP1/CP5 is 24/120 without an eligible mark and 27.6/138 with one. Hemorrhage's own direct strike remains 145 at weapon damage 100/AP0/default bonus45, or 100 with explicit bonus0.

Foreign owner, wrong target, exact expiry, removal, successful/failed refresh and retained expired-object boundaries are real **in-process** tests. They must not be presented as HTTP controls that the public request contract cannot express. Prepared HTTP fixtures with Agility 1 retain a small real crit chance; numerical comparison reads actual critical flags rather than claiming forced-zero crit.

Direct `/api/sim/rogue/run` consumes top-level `inputLatencyMs` in `Program.cs` middleware before DTO binding: default 80 ms, clamped to 0..1000, propagated through the timing scope and engine. The field's absence from the Rogue DTO does not mean it is ignored. Explicit zero is required when aligning direct HTTP with zero-latency in-process/shared observations. Strict `preview-rogue` still rejects Subtlety. Positive prepared Subtlety uses supported direct run with an explicit baseline or the existing preparation harness; it is not strict Subtlety admission.

## Corrected long-default prediction

The initial plan overgeneralized from 27 retained no-Rupture defaults and an older zero-latency 120-second in-process result. Four new 120-second **direct HTTP at 80 ms** defaults (legacy/prepared, each with Mutilate13/explicit17.25) contain seven positive CP4 Rupture ticks. Five normal ticks change 105.6 to 110.4 and two critical ticks change 211.2 to 220.8. Rupture totals change 950.4 to 993.6, total damage increases 43.2 and DPS increases 0.36; other ability totals stay fixed.

Each response has exactly 40 causal changed fields: seven raw amounts, seven final amounts, seven description strings, seven matching threat amounts and twelve ability/actor/target/global/DPS fields. Every unrelated field remains exact after the permitted identity normalization. Descriptions are changed strings, not numerical deltas. The old 27-case and zero-latency observations remain true; the blanket unchanged prediction was wrong. The first failed comparison is retained, not relabeled or overwritten.

## Evidence standard and remaining gates

[`solo-dps-rogue-subtlety-http.py`](../../tools/solo-dps-rogue-subtlety-http.py) reconstructs affected damage, threat and totals from captured requests, actual crit flags, per-target CP and live own-mark events, then compares the complete remainder. Exact Decimal processing uses no numerical tolerance; sequential System.Decimal-style addition accounts for existing fractional totals. Generated event IDs may be bijectively renamed only in the designated real event fields. Generated GET-default character IDs and the exact expected API source stamp are separate explicit allowances; all other metadata and provenance remain exact. There is no blanket Subtlety or metadata mask.

Raw-wire Decimal receipts are distinct from JavaScript-parsed/re-serialized report/replay wrappers. Both retain useful but different evidence. The [existing-corpus comparator](../../tools/solo-dps-rogue-damage-corpus-compare.py) ties report-owned character IDs to the exact retained default-GET pair at named paths; remaining values and metadata stay exact except for the explicit source stamp. The standalone corrected numerical smoke in [`solo-dps-capture.sh`](../../tools/solo-dps-capture.sh) supplements retained local before/after comparison; it does not replace it or establish old-code failures by itself. Existing browser suites remain required, with no workflow topology, permission or timeout increase.

## Frozen local result and provenance

The immutable `local-receipt-r1.json` under `.task-evidence/rogue-subtlety-corrections-20261111/` has SHA-256 `c5d48da72cb6eb16e7df3780c0e2fe08d617afe3c31c35763355a397f164b901`. `FINAL-LOCAL-REVIEW.md` has SHA-256 `375f4af0c30d6ae92730ff0ca09df390d5a26eed29dfbdef848f43365cb9492f` and independently verifies the aggregate's 28 linked hashes. No scoped local blocker remains.

- Fresh old-code baseline: exact accepted head, zero-warning/error build and 6,169 tests. The new 210-test regression set produces 178 intended numerical failures and 32 passing controls, with no harness exceptions.
- Final corrected strict serial Release rebuild: zero warnings/errors; 210/210 focused and 6,379/6,379 full .NET tests. Python passes 25/25 with retained evidence; clean mode passes 24 and explicitly skips one optional retained-corpus check. Prior importer 126, Feral identity 22, stage regression eight, 19 earlier Node suites, five Combat suites, importer smoke and all prior HTTP gates pass.
- Targeted before/after: 127/127 complete responses, comprising 91 causal fixtures (87 isolated plus four exact long defaults) and 36 controls/endpoints. The failed first comparison remains preserved. Separate standalone corrected smoke passes 127 requests/91 literal causal fixtures, with 67 positive Ghostly Strike events and 726 positive Rupture events. Concurrent requests have verified overlap.
- Existing corpus: 810 raw full responses pass (historical 310, standalone 119, accepted-stage 317, Combat UI 50, report 14), plus two separately parsed complete 31/30 reports. This count excludes older wrapper-only and other-class raw suites; their passing gates remain separate evidence.
- All 1,066 source inputs and ten binary paths/four distinct assemblies match frozen manifests. Source-manifest SHA-256: `7a9a362ab1a7c27c250beb43746b85f52628368b9ae65490cfb00b79cd86cd96`; binary-manifest SHA-256: `56498ced5a8e1c53b9f8cfc724cca8fbafd3fa036399bb4c56287ea85aaed8b8`. Final binaries equal those used for the corrected full HTTP capture. The API is stopped.

These are **uncommitted working-candidate** results. Informational versions still name parent `fa53137e`; the frozen source/patch identifies the correction and API stamp `fa53137e8194e7c84dd84fdae74c54f3ef265cae-local-rogue-damage` identifies its local responses. This is not a clean final-head build or CI receipt. Existing [Combat UI CI](https://github.com/layoung3/WarcraftSim/actions/runs/42999863967) accepts only its earlier exact head. Publication and new exact-head complete CI/runtime review remain required; no local browser pass is claimed here.

Strict Subtlety remains unavailable. Early-row admission gaps, Serrated Blades/Initiative and timed Thousand Cuts/Cutthroat discrepancies are not resolved by these two damage fixes. No full-class mechanics, optimized build, current-client certification, merge, deployment or overall assignment completion is claimed.
