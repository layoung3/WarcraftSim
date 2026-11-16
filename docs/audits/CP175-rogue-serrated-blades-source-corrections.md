# CP175 Rogue Serrated Blades: bounded source correction

## Status and source

**Local focused and HTTP checks passed; final independent acceptance, full aggregate, publication and exact-head CI remain pending.** Accepted predecessor is Initiative head `2cef14897fdb9c94390db4c577abec9a127296f3`, accepted by [Actions 43080889858](https://github.com/layoung3/WarcraftSim/actions/runs/43080889858) with 6,933 .NET tests and eight native suites. Its acceptance record is `.task-evidence/rogue-initiative-corrections-20261111/ci43080889858/INDEPENDENT-CI-ACCEPTANCE-r1.json`. Earlier Initiative pending prose remains historical. Serrated is not yet a published milestone.

Approved plan: `.task-evidence/next-stage-after-initiative-20261111/PLAN.md`. Stage evidence below is relative to `.task-evidence/rogue-serrated-blades-corrections-20261111/`.

The [pinned historical Rogue source](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/rogue.json) is retained as `acceptance-target-semantics-cloud-20261110/rogue-source-audit/rogue.decoded-content.json`, SHA-256 `9776311caefba3f7100b3220ffeaaccfc33dd6fb280c18f21172e85c5e984e09`. Serrated Blades has cap three, spell IDs 14171/14172/14173, and rank pairs **[3,10], [6,20], [9,30]** for percent target armor ignored and percent Rupture damage increased. This decoded historical evidence and the modeled numbers remain provisional, not independently certified raw Git-blob bytes or current-server behavior. The tooltip supplies modifiers, not every composition or execution rule below.

## Narrow implementation and compatibility

Product edits are confined to `ForeverRogueTalentBuild.cs` (legacy cap three), `RogueSimRunner.cs` (private armor resolver), and `RogueCombatProcessor.cs` (Rupture multiplier). Legacy ranks 0–3 are admitted for the existing three specs with existing aggregate point accounting; negative/4 remain invalid. Legacy admission and prepared previews are not strict historical tree legality or strict Subtlety support.

The personal armor convention preserves exact decimal order: **max(0, target armor − existing owned Expose) → actual-hand mace factor → Serrated factor (1 − 0.03 × rank) → existing denominator and 75% cap**. Existing eligible physical nonbleed resolutions use it; bleeds and poison remain exempt. Neither target armor nor prepared stats are mutated. Rupture receives **(1 + 0.10 × rank) once after existing CP and live own-Hemorrhage factors**. The mark remains live rather than snapshotted, and foreign marks do not qualify. Multiplication with mace/Hemorrhage is an explicit provisional local composition convention, not an additional claim from the tooltip.

Missing/zero preserve prior arithmetic and RNG behavior; newly accepted explicit zero is still an admission change where previously rejected. No Core, UI, strict catalog, defaults, explicit tuning, scheduling, RNG or stat-mutation change is included. Strict builds, imports and both report contracts continue rejecting the key, including explicit zero and aliases. The strict-dispatch expected legacy-only inventory grows **31→32**, with the full rejection loop intact.

### Preserved limitations

- Expose points are processor-wide, not independent per-target snapshots. Owned-aura checks do not remove that limitation.
- Blade Flurry copies the already resolved primary amount and applies secondary armor again; it does **not** run the combat multiplier again. The inherited two-stage behavior and handless main-hand fallback are numerically characterized, not repaired or certified as game rules.
- Extreme decimal overflow remains an inherited validation limit, not a new supported numeric domain.
- The public runner has one Rogue. Controlled multi-actor/foreign-owner fixtures are not public multiplayer certification.
- Positive damage changes can change death timing and later opportunities. No universal DPS gain or reconstruction of arbitrary hidden RNG follows from replay controls.

## Verified local evidence

`INDEPENDENT-CANDIDATE-FOCUSED-REVIEW-r1.json` records the byte-identical **138 focused cases passing** after correction: the old run had **40 passing controls, 83 numerical assertion failures and 15 admission failures**. Admission failures are not numerical proof. Strict Release has zero warnings/errors and the complete 32-key strict rejection loop passes. Literal tests cover rank/CP/mark/crit Rupture amounts, armor composition/caps, actual hands and extra attacks, ownership controls and preserved cleave. For example, a base-24 CP1 Rupture tick is 24/26.4/28.8/31.2 across ranks 0–3, or 27.6/30.36/33.12/35.88 with the live own mark. Raw100 against armor100 and denominator91 yields rank-three final50; the preserved equal-armor cleave example resolves primary50 then secondary25.

`candidate-http-owner-r1/receipt.json` records **155 corrected requests plus 155 replay requests**, successful custody postchecks and normal API shutdown at **2026-11-11T19:42:24Z**. That owner receipt explicitly deferred arithmetic review; later `candidate-analysis-r1/summary.json`, `candidate-replay-analysis-r1/summary.json`, `candidate-old-new-r1.log` and `candidate-independent-replay-r1.log` record passing offline checks. Same-version replay comprises **102 complete successful envelopes, 46 exact retained rejections and seven binder/key diagnostics**, not 155 complete successful equalities. Numerical evidence covers **63 arithmetic fixtures, 5,624 events and 23 full-envelope positive projections**. Broad poison/prepared/cleave replay controls are separate from independent arithmetic proof and do not reconstruct hidden RNG.

Comparison preserves exact decimals, array order, provenance and complete successful envelopes, with only narrowly documented generated-ID bijections and expected source stamps. Intentional new admissions are separate from old successful-response equivalence. `candidate-binding-r1.json` records numeric-string `1` matching canonical rank one; a case alias is admitted but ineffective through inherited ordinal rank lookup. Strict maps still reject aliases. Five binder bodies are compared structurally because trace IDs vary; two whitespace-key diagnostic bodies compare exactly. Parsed report/build checks remain separate from raw HTTP proof and are not native-browser import evidence.

## Remaining gates and custody

Independent final candidate/prose acceptance, full current aggregate and retained-corpus preservation, any separately reviewed smoke integration, publication and exact-published-head CI/native artifact acceptance remain pending at this checkpoint. Candidate API provenance is the accepted head plus `-local-serrated-blades`; it is not new published-head CI. No fresh browser or pixel inspection is claimed. No strict Subtlety/UI admission, full Rogue or overall project completion follows.

The deduplicated input inventory preserves original path/hash mappings, historical JSON and original reports; `INDEPENDENT-INPUT-INVENTORY-REVIEW-r2.md` records the two additive Combat report dependencies. Original failures, earlier drafts and prior Initiative custody remain intact. These prose updates postdate focused/API execution and require separate final source accounting; existing execution manifests are not relabeled. Existing branch/draft PR 28, unchanged PR description/no comments, $0, no merge/deploy and evidence-retention restrictions remain in force.

## Final local aggregate passed

This later checkpoint supersedes the pending **local execution** gates above, not publication or final independent acceptance. `final-local-aggregate-receipt-r1.json` has SHA-256 **`79318568fb622c25956e4300095b25e260939645c1e2c0c7986d6b15dc79eec4`**. Fresh strict Release passes with zero warnings/errors; **all 7,071 .NET tests pass**, including the same 138 Serrated cases. Ten Python invocations total **355 discovered, 354 passed and one expected clean-only skip**: clean damage mode skips the immutable retained-input check, which passes in its separate retained run. All **25 Node suite invocations**, importer checks and the **60-original-report corpus** pass.

Prior HTTP corpus preservation passes, including **810 complete raw-response comparisons plus two separately parsed reports**; retained damage, Cutthroat, Dirty Deeds, Elusiveness and Initiative gates also pass. These overlapping inventories are not unique-scenario counts. Final Serrated **155-request capture plus 155-request replay** pass arithmetic analysis, old/new classification, candidate preservation and full replay comparison. The earlier distinction between complete successful envelopes, retained rejections and binder diagnostics still applies; this is not 155 successful-200 numerical equalities.

The exact existing smoke blocks pass in **40.420265483 seconds**. The new **22-response Serrated block**, including **16 pure tests**, passes in **3.358797845 seconds** (about **0.355 seconds** for capture). Those are measured block runtimes, not full browser/CI runtime. Build, non-HTTP, HTTP and offline phases all exit zero; custody postchecks pass. The sole-owner API shuts down normally at **2026-11-11T20:06:30Z**.

**This prose update postdates the aggregate.** Its source manifest (`2594adf20f69e832b8a443d9e7d19dd575cf9002d26b85bc72bfd27593eb5a6b`) continues to identify the earlier executed bytes; neither it nor historical receipts are rewritten. The three final prose hashes require separate publication accounting and independent review. Publication and exact-new-head CI/native artifact acceptance remain pending. No fresh local browser/pixels, current-server certification, strict Subtlety, full Rogue or overall completion is claimed; inherited Expose/cleave/overflow limits and all release restrictions remain unchanged.


## Accepted exact-head CI after recovery

The bounded stage is accepted at **3978be7a9ba4513ecfc37a2c6714adde9696307c** after [CI 43094771706 attempt 2](https://github.com/layoung3/WarcraftSim/actions/runs/43094771706) passed 7,071 tests, 138 Serrated cases and eight native suites. This supersedes the pending publication/CI status above. Later original raw local receipts were not recovered after rollback; fresh recovery verification and recovered genuine CI artifacts are explicitly separate, without synthesizing originals. The historical/provisional mechanics and unavailable strict Subtlety limits remain. The subsequent [Enhancement stage](CP175-enhancement-cross-tree.md) has local verification but its own exact-head CI remains pending.
