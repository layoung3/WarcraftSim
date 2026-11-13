# Legacy Rogue compatibility fixtures

`current-combat-report.json` is the unmodified report that failed admission in
workflow 43293109074 at PR head `a2aa5f45ba0fff11b4585f5623f616dcf8837303`.
It was extracted from artifact 13978857337 at
`combat-solo-ui/rendered-bwyy4Z/31-report.json` before artifact expiration.
`legacy-combat-build.json` is an unmodified native-browser download from the
earlier accepted `b21d567c97a44ebc5527953b259c1ad3caa6bbc8` workflow 43254621489.
The exact source paths, byte sizes and SHA-256 hashes are in `provenance.json`.

The genuine older Assassination reports remain in `../assassination-reports/`
with their existing provenance. The existing historical Combat HTTP responses
remain in `../../solo-dps-combat-comparison-fixture.json`, assembled by that
test's exported report factory. These historical reports retain their original
source stamps. Importing their builds for a new current-engine execution does
not claim deterministic replay across different source versions.

Run the actual production-function regression entry point from the repository:

```sh
node tools/solo-dps-rogue-legacy-compat.test.cjs
```

The test checks the original failing-report hash before deriving in-memory
positive/negative cases. No fixture is rewritten or normalized. Live HTTP and
native-browser tests use the older saved builds through the production importer,
serializer, report admission and full-response replay paths.
