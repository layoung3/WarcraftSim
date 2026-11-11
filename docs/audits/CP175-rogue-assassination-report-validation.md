# CP175: historical Assassination report talent admission

Date: 2026-11-11. Status: **corrected r2 full local pass; final independent review and exact-head CI/browser acceptance pending**. This audit records verified implementation evidence separately from remaining publication, native browser and visual gates.

## Accepted starting point and reviewed purpose

The accepted Dirty Deeds head is `c1c599199d2628c1943f3252b24861ac5a740329`, tree `3812ede4f8e5ddc404f6dfce80f9127d9abb2669`, with [Actions 43031237502](https://github.com/layoung3/WarcraftSim/actions/runs/43031237502). Its receipt `.task-evidence/rogue-dirty-deeds-corrections-20261111/ci43031237502/receipt.json` has SHA-256 `9176f73285e4fde91a7a55fe32b93983a93a81e50540be7a8f20d6518ca93858`. That acceptance covers the bounded Energy correction, not this report-reader stage.

The approved plan is `.task-evidence/assassination-report-validation-plan-20261111/PLAN.md`, SHA-256 `006a5e96f981b2a9965acc409a9b56bebb1e35e63e89143afc1ebf56d7adb562`; independent approval addendum SHA-256 is `ed9c8fdb46f12e1576d651c8deffb9d963618c687e55b0b454a1af2adfad9a9e`. The plan's earlier not-released status is historical: this stage was subsequently released against the accepted Dirty Deeds head. Plan approval and implementation release are not implementation acceptance.

The retained earlier probe `.task-evidence/next-dps-stage-after-cutthroat-20261111/report-inspection-probe.json`, SHA-256 `1ff0404941c3b995a1e36615ab1dc7b00577fc524fd99d88a6fd1946416a57f2`, records the old generic Assassination inspector accepting consistently copied Dirty Deeds 0/1/2 and Cutthroat 0/1/5 without warnings. It retained original metrics and receipts; those mutated files are not valid simulations. Combat's mutations in that probe fail at preparation-copy equality before its source-confirmed allowlist. This historical result remains unchanged, not rewritten as rejection evidence for a later guard.

The narrow correction is an Assassination-only guard at the common `inspectReport` boundary in [`solo-dps-comparison.js`](../../WarcraftSim.Api/wwwroot/solo-dps-comparison.js). It checks the historical talent/allocation contract before file-slot admission, current-run use or comparison. This is the only production file changed; corrected r2 SHA-256 is `43af94e808c4ebe6139ff599ed2fd37665e5e4736f1afea9e20a6bc612eb5efa`. No new provenance rejection, comparison-eligibility policy, general schema framework, catalog fetch or backend change is included.

## Distinct admission boundaries

1. **Legacy raw APIs and previews.** Otherwise-valid legacy Rogue requests may use talents such as Dirty Deeds or Cutthroat outside the historical unified UI subset. Raw responses are not report-v1 wrappers. Their admission, cost/damage behavior and replay remain unchanged.
2. **Strict shared simulation.** Existing controllers validate canonical identity and restricted full/typed ranks, including supported-zero presence. Raw itemized validation checks provided aggregate declarations; the shared controller also has an existing full-map fallback. The report's explicit three-map and allocation-receipt requirements come from the original UI exporter, not a claim that every direct server request requires those fields.
3. **Saved build import.** Build import validates its restricted build shape and uses itemized validation during preparation. A report does not become an executable build, and a saved build or raw response does not become a report. Build import, rollback, defaults, tuning and replay are outside this production change.
4. **Parsed report inspection/import.** This correction checks canonical exported identities and internal talent/allocation agreement. Assassination's raw result has no execution-owned copy of the talent maps. Consistent copies therefore do not attest to what the server executed, and a coherent fabricated report can still pass.
5. **A/B comparison.** Admitted reports may still be incomparable under existing scenario/settings rules. No new rule rejects or suppresses deltas because optional engine, target-build, historical-source or baseline metadata differs or is missing. Existing warnings, cross-spec non-comparability and numerical comparison behavior remain distinct from admission.

## Bounded historical contract

The reviewed local [`ForeverHistoricalCasterTalentCatalog.cs`](../../WarcraftSim.Api/Simulation/ForeverHistoricalCasterTalentCatalog.cs) supplies twelve exact keys:

| Row | Keys and maximum ranks |
| --- | --- |
| 1 | `malice` 5 |
| 2 | `ruthlessness` 3; `improved-slice-and-dice` 3 |
| 3 | `improved-expose-armor` 2; `lethality` 5 |
| 4 | `vile-poisons` 5; `cold-blood` 1; `improved-poisons` 5 |
| 5 | `vigor` 2; `mutilate` 1 |
| 6 | `seal-fate` 5 |
| 7 | `venom` 1 |

Each positive rank requires `(row - 1) * 5` actually allocated earlier-row points. Lethality requires Malice 5; Venom requires Mutilate 1. Full-parent gating is the existing disclosed local convention, not newly certified upstream behavior. Default 38, legal 36, other legal underallocations and consistent empty allocations remain valid; no 51-point filler is invented. All twelve caps sum to 38, so oversized-count controls test declarations, not a reachable over-51 allocation with valid keys/caps.

The required bounded fields are:

- Canonical Rogue/Assassination identities on build, shared and typed request, level-60 typed character and raw response, plus raw simulation specialization. Build/request itemized flags must be true. No nonexistent `sim.classKey`, new character ruleset check or Combat-only character shape is required.
- Record-valued rank maps at `build.talents.ranks`, `originalSimulationRequest.itemizedTalentRanks` and `originalSimulationRequest.rogue.talents.ranks`. Every own key, including zero, must be one of the twelve. Ranks are actual JSON integers in bounds; strings, booleans, null and fractional values are not ranks. Maps must agree exactly, including supported-zero presence, regardless of key order.
- Saved and typed talent declarations are actual integer Assassination totals and zero Combat/Subtlety totals. Raw `allocatedTalentPoints` and `unspentTalentPoints` must equal the derived total and `51 - total`.
- `talentAllocation` has `mode: "strict-rank-derived"`, matching integer selected/unspent/tree totals and a selected array containing exactly each positive key/rank once. Selected keys must be canonical strings, not arrays or other coercible values. Unknown, duplicate, missing or zero selected entries fail. Extra unrelated fields are retained; this is not a whole-object allowlist. Typed/saved Assassination `totalPoints` or `unspentPoints` fields are not newly required.

Optional descriptive/source fields remain outside the new rejection predicate: metadata schema echoes and stamps, raw source notes, selected display/cap/row/parent echoes, visual provenance, baseline provenance and unrelated extra fields. Actual legality comes from the reviewed local contract, not those descriptions. Missing, null or changed optional fields neither bypass the guard nor introduce new admission or comparison rules wherever the existing reader already permits them. Numeric-string encounter controls remain supported. Existing settings/resource/target-semantic inconsistencies remain warnings with their existing effect on comparison.

A concise non-attestation caveat is explanatory, not a delta-suppressing warning. Supported zero versus omitted key presence is visible in selection details without independently making an otherwise comparable pair ineligible. Unsupported zeros remain rejected.

Parsed JSON cannot expose duplicate textual properties already collapsed by `JSON.parse`. Duplicate selected-array members are checkable; raw duplicate-property detection, non-JSON object security and full strict-backend validation parity are not promised.

## Genuine compatibility fixtures and source confidence

Three complete original v1 browser exports are retained under [`tools/fixtures/assassination-reports/`](../../tools/fixtures/assassination-reports/), with original timelines and provenance metadata. Their source is [Actions 42909945835](https://github.com/layoung3/WarcraftSim/actions/runs/42909945835), head `5536d000705584fba2257107a101f5fb3f819aec`:

- `rogue-manual-report.json`, 77,551 bytes, SHA-256 `6f09ae6b1a7af830b638a3f1e9f1f80e77796375c2866278407977d9d13f9810`.
- `legal-36-report.json`, 76,721 bytes, SHA-256 `9eb0542ab19b3162b13a1a0c7e895791b75a50d2e5e5ec4076659e74c7740aa6`.
- `legacy-tuning-report.json`, 77,544 bytes, SHA-256 `f461750e8e41b430b28c744b5cc3cd85e0f8017b416f3b6e9cd0fba8c0dcaf00`.

These are compatibility inputs from retained original exports, not fresh executions. The legacy-tuning original preserves explicit Mutilate 17.25; old results must not be recomputed with newer defaults. Explicit zero/custom tuning, null visual source URLs/commits and numeric-string encounter controls remain supported. Derived zero/optional-metadata/descriptor cases must be labeled synthetic, not authentic historical exports.

The initial independent review additionally inspected six accepted Dirty Deeds exports for exact mandatory identity/allocation shape and found no unexpected genuine format. Earlier planning examined eighteen named exports; independent plan review inspected 54 retained files across nine source stamps. The completed r2 pure sweep covers **60 original reports across ten cohorts**, including the accepted Dirty Deeds cohort and six files from failed CI 42984473427. All parsed inputs/results remain unmutated; each report's comparison with its same-cohort manual report preserves the old eligibility, warnings, numeric metrics, delta and selection details. Old/new corpus-result JSON bytes agree. These are artifact counts, not 60 successful browser runs. Historical decoded source evidence and the reviewed local catalog establish this bounded contract, not current-client certification, report authenticity or an unrestricted historical-format guarantee.

That byte equality covers the saved projection of input provenance, DPS/damage, `integrityWarnings` and pair eligibility/warnings/deltas/selection details, not every inspector/comparison return field: original report bytes remain unchanged, while new non-attestation `integrityCaveats`/pair caveats intentionally differ and zero-presence detail intentionally changes derived zero cases absent from these originals.

The former Combat fixture relabeled as an “older Assassination” positive contains Combat talents and is not genuine compatibility evidence. The scoped tests replace that positive with an authentic Assassination report and retain the relabeled artifact as a specific negative. This is fixture correction, not a saved-report migration.

## Transaction checks and native verification obligations

File import inspects before assigning either slot. Rejection retains prior accepted slots, rows/deltas/warnings/change details, current build and current results, with a plain-text reason and the existing “Slot A/B was not changed” guidance. Generation checks continue to ignore stale reads after newer imports or Use current run. The **35 counted pure DOM transaction scenarios** cover repeated import/rejection/recovery, the no-file branch, injected parse/read failure, swaps, occupied/incompatible pairs and stale completion. The separate existing simulation-complete failure path clears current-run eligibility/visible comparison detail without replacing accepted slots; it is not the same visible rollback promise as file-import rejection.

Native browser additions reuse existing real run output, check unchanged build/current-result exports and require no network calls during report-only imports. They are authored and syntax checked, not yet executed. Injected `File.text` failures/timing and no-file events do not prove native OS chooser cancellation or an actual filesystem failure. Navigation away/back, actual browser timing, no-network behavior and desktop/mobile error readability remain native acceptance obligations.

Required verification includes old-source defect-versus-existing-rejection classification; authentic fixture/hash and retained-corpus checks; legal allocations, zero presence, key/type/map/selected/identity negatives; optional-metadata and existing comparison preservation; the unchanged generic/other-class suites; strict Release/full .NET/Python/frontend gates; and bounded raw API/build replay preservation. Parsed JavaScript report equality and exact-Decimal raw HTTP equality remain separate proofs.

All existing native browser gates, including encounter-editor checks, remain required in authorized GitHub Actions together with bounded added Rogue import/error cases. Current CI runtime limits remain unchanged. Desktop/mobile error readability needs actual screenshot review; no denied local Chromium or Windows access is retried.

## Evidence state and remaining gates

Stage evidence lives under `.task-evidence/assassination-report-validation-20261111/`. Baseline receipt `baseline-exact-head/receipt.json`, SHA-256 `597c7d5b67bccd780ce4ba061189bec99748ed398d2f13b3f0d7a335c7a71fd9`, records a clean accepted-head freeze of **all 1,220 tracked paths, including all 126 docs paths and 92 docs JSON inputs**, with no exclusions. Manifest SHA-256 is `2081daceb0006267b04c7cc216d2fb62490fecc820f2829d7cbb4dc09b025755`. The strict .NET 10.0.403 Release rebuild passes with zero warnings/errors and actual informational version `1.0.0+c1c599199d2628c1943f3252b24861ac5a740329`. Received pre-rebuild binaries were separately recorded, not relabeled as that head.

Baseline full tests were not repeated: the exact accepted CI is retained, and the corrected candidate's complete suite is recorded below. Existing accepted corrected raw captures are baseline evidence, not a redundant new baseline simulation. Independent initial review SHA-256 `dc283d1fe14a62b5f059d214c2ae4f2507a58aa62493a11a8e21af1ee10d69aa` confirms baseline/source and release-head structural compatibility only.

### Corrected r2 source and pure evidence

`candidate-pure/IMPLEMENTATION-RECEIPT-r2.json`, SHA-256 `6b758acc1302f8f6ad64423283bace8e73857298c14a77f6f483d6cdaa561ec3`, records **19 passing positive controls, 311 passing negative controls and 35 passing DOM scenarios**, with six pair-control outcomes unchanged from old source. Nine existing pure frontend suites pass. The focused module is invoked by the existing Rogue contract gate, with no workflow/dependency change or reliance on untracked evidence for its mandatory fixtures. The 60-original compatibility sweep supplements those clean-checkout fixtures.

Final old-source classification r6 identifies **301 genuinely formerly admitted invalids and ten existing rejection controls**. Its 303 unmet new expectations include two old generic error-message-layer mismatches, not two additional admitted defects. Initial candidate r1 passed its original 305-negative matrix but missed direct/nested array coercion of selected keys. Six added type controls reproduced exactly those two misses; one strict string condition fixes them. Corrected r2 passes the full matrix. Initial harness 0-versus-negative-zero failure, prior classifications, discovery probe and r1 evidence remain retained. R1 completed a zero-warning/error strict rebuild and all 6,642 .NET tests, but no aggregate non-HTTP gates, API captures or browser run; its results are superseded for final-source acceptance, not relabeled as r2. `R1-EXECUTION-DISPOSITION.json` preserves that exact boundary.

Independent `INDEPENDENT-SOURCE-REVIEW-r2.md`, SHA-256 `07785769881fe3fcefc6794a28b09bc6247abe9bf3d9cd938c7ea9e79b9887c6`, accepts corrected source/fixture/pure evidence and independently reproduces old/r1/r2 classifications and compatibility. It does not accept native timing/layout, final raw preservation or CI.

### Frozen r2 full local execution pass

Final candidate source manifest SHA-256 `3859e114483c57dd7b2d0a90908c043f2e1173f8aa8a2347f3f628319c217ccc` covers **1,223 inputs**, including 124 tracked docs paths and **all 92 docs JSON inputs**. Only this audit, the acceptance README and ledger are named prose exclusions; at freeze, two were existing tracked docs and the new audit was untracked. A separate **14,337-path external-anchor freeze**, SHA-256 `da558f38c0d5e1e6457a96b47298cee24ad726b97d88dfe3e115ee4a796fa39b`, preserves historical evidence used by the gates. Neither is a generalized docs exclusion.

`LOCAL-EXECUTION-RECEIPT-r2.json`, SHA-256 `9dfb620af7ebc4c12feb9755b60837ae00373e34290b5a21cc9d5293f43b217b`, records the frozen r2 strict rebuild with zero warnings/errors and **all 6,642 .NET tests passing**, none failed/skipped. API/Core/Data DLL bytes are identical to the accepted-head rebuild. All **24 existing Node suites** (19 prior plus five Combat), the focused 19/311/35 controls, six pair controls, importer smoke, syntax and whitespace pass. The 60-original report sweep preserves exact old/new parsed result bytes and original input hashes without re-simulation.

Python passes importer 126, stage regression 8, Feral 22, Cutthroat 41, Dirty Deeds 43 and damage 25 retained/24 with one explicitly optional skip in clean mode. The same 38 Combat identity checks pass in full and standalone modes, not 76 distinct tests. No required local gate is skipped.

Raw HTTP preservation passes **313 Dirty Deeds cases: 308 complete-response equalities plus five malformed-binding diagnostics**; **127 damage**, **382 Cutthroat**, **63 Cutthroat smoke** and **810 older raw responses** remain complete under the existing narrow allowances. Two newly emitted Combat reports are separately compared as parsed reports. A separate **41-case Dirty Deeds smoke** also passes complete-response comparison. These are distinct capture gates, not all distinct semantic cases. Exact-Decimal wire comparisons permit only existing scoped generated-event/default-character identity and expected source-commit differences; workflow-checkout and other metadata remain exact. Diagnostic error bodies are retained, not called complete-body equalities. Neither raw evidence nor the parsed report sweep is relabeled as the other.

All source, execution-tool, retained/live binary, report-input and external-anchor pre/post checks pass. `SNAPSHOT-CUSTODY-r2.json`, SHA-256 `b133f21d18e2e65bc8b053d7b46ea6e21b408576fbeb2ff53dbe08d29bd22844`, verifies the retained baseline's 1,220 source files and each r1/r2 snapshot's 1,223 files. The API stopped normally at **2026-11-11T14:30:03Z**. These are corrected r2 results, not the superseded r1 run. The API stamp is `c1c599199d2628c1943f3252b24861ac5a740329-local-assassination-report-admission`; actual assemblies retain `1.0.0+c1c599199d2628c1943f3252b24861ac5a740329`. This is a frozen working candidate, not a clean new published-head build.

**Pending:** independent final local/prose review; publication; exact-final-head CI and workflow-checkout tree equivalence; all native browser gates and complete artifacts/runtime checks; and relevant fresh screenshot inspection. No local browser run, new pixel review or final report-hardening acceptance is claimed.

This stage does not change backend/Core/Data, strict simulation or build import, formulas/defaults/tuning, catalog content, presets, report schemas or comparison eligibility. It does not establish provenance validation, server attestation, blanket report integrity, full Rogue/strict Subtlety support or overall assignment completion. Work remains within the existing draft/unmerged PR and $0 scope; no new permanent branch/PR, force push, merge/deploy, deletion, PR metadata/comments, dependency, access or CI-permission change is included. Earlier audits, probes and failed/provisional receipts remain preserved.
