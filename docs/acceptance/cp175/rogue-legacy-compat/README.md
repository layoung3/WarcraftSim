# Legacy Rogue compatibility qualification

This bounded repair continues PR #28 from
`a2aa5f45ba0fff11b4585f5623f616dcf8837303`. It changes legacy serialization,
report admission and comparison only; no combat formulas or replay identity
rules change. It does not certify the remaining class-wide mechanic backlog.

## Retained evidence

- `local-validation.json`: exact commands, results, tested frontend hashes,
  backend assembly hashes, environment limitations and source association.
- `legacy-saved-combat-report.json` and
  `legacy-saved-assassination-report.json`: complete, unmodified actual local
  reports produced by importing genuine older saved builds through the production
  wrappers and HTTP API, then reimporting and replaying under the current engine.
- `../../../../tools/fixtures/rogue-legacy-compat/`: the original failing CI report,
  genuine old Combat build and their byte/source provenance. Older Assassination
  fixtures retain their existing provenance in the adjacent fixture directory.

The local reports honestly retain `not-stamped-local-build` metadata. The receipt
binds their bytes to the tested working-tree files and compiled backend; no source
stamp was rewritten. The publication commit and exact-head CI run are recorded in
the existing PR delivery receipt. Importing an older report is not a promise of
cross-version replay.

## Actual local results

- Strict serial .NET 10 Release build: zero warnings/errors.
- Full .NET suite: 8,318 passed, zero failed/skipped.
- All 22 Node entry points registered in the existing workflow/capture script
  passed, including 936 compatibility cases and 52 A/B import rollback checks.
- Fifteen changed JavaScript files passed syntax checks.
- Intended Python entry points: 359 passed, three explicitly optional historical
  retained-input skips. The older 79-item intake audit passed separately.
- Real HTTP: 82 class-wide checks; 108 current/older Rogue report requests with
  four complete reports/replays; 53 Assassination UI checks; 40 Feral requests;
  existing exact-Decimal Combat HTTP acceptance.
- The existing import smoke passed against the explicitly built Release importer.
  The offline CSV stage/import kept two records metadata-only and promoted none.

The smoke script's separate implicit `--build` attempt exited 1 during its default
build/restore invocation, reporting zero compiler errors but no further cause.
It is not counted as passing. The solution's explicit serial no-restore build
and subsequent actual importer execution passed. CI must run the original
`integration_smoke.py --build` gate successfully.

Local Chromium aborted with `socket() failed: Operation not permitted (1)`.
No local native-browser pass is claimed. The existing Actions native-browser job
is the required final gate, including the previously unreached Combat workflow,
genuine older-build imports, file downloads, strict replay and coherent A/B
rejections. Its final result must be read from the exact published-head run in
PR #28; these local results alone do not establish complete acceptance.
