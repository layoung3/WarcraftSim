# CP175: existing Feral historical rank corrections

## Scope and source confidence

This bounded stage changes only Savage Fury damage and Sharpened Claws critical
chance in the existing Cat/Dire Bear runner. It adds no DTOs, defaults, tuning
knobs, profiles, endpoints, UI, strict admission or talent caps. Bear is covered
because the existing spellbook is shared, not as a new simulation mode.

The source is the [pinned Druid tree](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/druid.json).
`CP175-feral-sourced-ranks-source.json` preserves the two exact decoded entries,
rank arrays, wording and acquisition limits. The connector reported modification
on October 9, 2026; this is metadata, not an independently verified commit date.
Conflicting local literals were introduced October 19–20. No later per-talent
support was located. This is **historical/provisional source alignment**, not
current-client certification or evidence that every Feral mechanic is complete.
The receipt hash identifies saved decoded audit evidence, not original upstream
bytes or a verified Git blob. No new external source was acquired for this stage.

## Corrected effects and independent arithmetic

Savage Fury is 5%/10% on Claw, Rake (impact and ticks), Shred, Maul and Swipe.
Ravage is excluded. Feral Instinct remains a separate authored multiplier,
including its explicit zero setting. Existing secondary Swipe damage continues
to copy primary damage, so it inherits the correction without another multiplier.

At AP 800, WeaponDps 75, default base coefficients, no armor/crit/other talents:

| Component | Rank0 | Rank1 | Rank2 |
| --- | ---: | ---: | ---: |
| Claw:75+100+.15*800 |295|309.75|324.5|
| Shred:2.25*75+220+.10*800 |468.75|492.1875|515.625|
| Rake impact:80+.05*800 |120|126|132|
| Rake tick:40+.03*800 |64|67.2|70.4|
| Maul:2.5*75+120 |307.5|322.875|338.25|
| Swipe:90+.03*800 |114|119.7|125.4|
| Ravage:3*75+260 |485|485|485|

Swipe with Feral Instinct 3 and Savage Fury 2 is 114*1.3*1.1=163.02.
The unchanged negative controls are Rip 45/point, Bite 200/point, Pounce 60/tick,
Lacerate 18.75 impact / 24 tick, Primal Bite 367.5 and Cat white 75. These controls
preserve the current provisional coefficients; they do not certify them.

Sharpened Claws adds 3/6 percentage points, preserving manually authored base
crit and Leader of the Pack's independent 3 points. Base 20 therefore becomes
20/23/26 (23/26/29 with Leader). Controlled RNG intervals test both sides of each
threshold, including an actual Cat/Bear hit with a roll between 22 and 23.
The existing hit-then-crit policy, miss behavior and RNG consumption are unchanged.
Existing periodic and white critical eligibility remain provisional and unchanged.

Authored AP/Health/Mana already include ordinary attributes. Heart of the Wild
continues to add incremental conversions only. With AP 400, attributes 100,
Health 5000, Mana 3000 and HOTW 5, Cat remains 420/5000/3150 and Bear 400/5200/3150.
Explicit zero authored attributes/AP/weapon/base damage remain zero where valid.
No generic builder or ordinary attribute conversion is introduced.

## Test-first and causal preservation

Baseline production HEAD: `5536d000705584fba2257107a101f5fb3f819aec`.
Local evidence is retained under
`acceptance-target-semantics-cloud-20261110/feral-sourced-ranks/` (untracked).
Before production edits, a strict build and 16 full request/response/timeline
captures were made, with frozen API/Core/Data DLL/PDB hashes and assembly stamp.
The isolated capture tool references those preserved binaries directly.
The tracked [compact causal receipt](CP175-feral-sourced-rank-receipts.json)
contains all 16 comparisons, independent old/corrected oracles and the four
matching Sharpened counterfactual hashes without binaries or full timelines.

- Original raw responses and GUID-only fingerprints remain in `baseline/`.
- `baseline-canonical/` was also captured before production edits from the same
  frozen binaries. It replaces random GUIDs and canonicalizes decimal number
  representation with exact decimal `G29`, never floating-point conversion or
  rounding. The four rank0/default/HOTW whole-response hashes are pinned in tests.
  They remain exact semantic fingerprints, **not byte-identical raw JSON**:
  multiplying by 1 can change an otherwise equal JSON decimal's trailing zeros.
- `red-build-r1.log` is a test-harness accessibility compile error, corrected by
  reflection without widening the production API. It is not a mechanics failure.
- `red-tests-r3.log` records 322 test cases before production changes: 206 pass,
 116 fail on the intended rank/ability differences. The test source is preserved.
- `green-tests-r1.log` records 322/322 passing after the narrow production edit.
  No existing golden expectation was replaced.
- `after/` records full changed-tree outputs, binary hashes and production diff.
  Assembly metadata still names the base commit; the working-tree diff and binary
  hashes identify the actual uncommitted implementation. Do not call it a new SHA.
- `causal-comparison.json` accounts for every changed damage event in all 16
  captures, including secondary Swipe. Selected Savage rank1/rank2 Cat totals
  move 12753.55→12946.425 and 12813.6→13199.35; Bear moves 6322.5→6368.625/6414.75.
  The Ravage fixture separately removes its incorrect bonus. Damage and threat deltas
  reconcile exactly, with no target deaths and preserved resources/event schedules
  for these seeds.
- `baseline-crit-counterfactual/` executes preserved old DLLs with authored base
  crit increased by exactly the Sharpened rank. For fixed Cat and Bear, all four
  rank1/rank2 counterfactual whole-response fingerprints equal the corrected
  runner with unchanged authored base. This checks complete downstream results,
  not just aggregate DPS. It is not a mixed-form equivalence claim. Different
  seeds may change Bear Rage/rotation when newly critical autos grant more Rage.

The tests also cover every rank, direct/periodic damage, crit and 50% armor,
negative abilities, custom/zero tuning, source receipt consistency, repeated and
concurrent caller reuse, talent removal, hit policy and inclusive HOTW budgets.

## Local verification

- Strict .NET 10 Release build with warnings as errors, no restore, single-process
  MSBuild, disabled node reuse and no shared compiler: zero warnings/errors.
- Full xUnit suite: 4,951 passed (4,629 previous plus 322 new), zero skipped.
- Python importer tests: 126 passed. Fifteen pure Node suites passed, including
  the concurrently reviewed Rogue capture-contract regression. JavaScript syntax
  and diff whitespace checks passed.
- Existing HTTP suites passed: prepared casters, 199 extra-class requests,
  45 Enhancement UI gates, 53 Rogue UI gates, 23 strict Enhancement requests and
  72 Rogue Expose Armor requests. Offline importer integration smoke passed.
- No local browser was attempted for this mechanics-only stage. Real browser
  acceptance remains the user-selected exact-head Actions gate.

## Remaining boundary

Predatory Strikes/AP consumers, form-transition stat recalculation, lower-rank
Furor, Bite priority, impossible Prowl positioning and unaffordable startup Mana
remain separate work. The partial historical 36-point allocation is not a strict
full build, current-client-certified preset or evidence of complete Feral DPS.
No new admission, UI, catalog expansion or automatic-rotation policy is claimed.

Final local validation and exact-head publication/browser status are recorded in
the implementation ledger and retained acceptance logs. Exact-head Actions/browser acceptance remains required for this unpublished
stage; no publication, merge or deployment is claimed here.
