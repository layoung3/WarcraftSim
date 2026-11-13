# CP175 Paladin shared class-build delivery audit — 13 November 2026

**Status: final local qualification passed; publication, native browser acceptance and exact-head CI remain required gates.** The candidate passed 288 focused and 8,455 full .NET tests, the strict Release build, unchanged importer gate, retained Node/Python regressions and all eleven production HTTP commands. This audit records those actual receipts and preserves earlier failed/preliminary attempts. Local qualification does not replace native browser acceptance or exact-published-head CI. No universal all-class or all-build completion is claimed.

The integration worktree is `/workspace/scratch/f370a1002cd9/WarcraftSim-paladin`, based on accepted commit `2bc4d5a364f60e200e9b27c215316da48425d9fa`. Delivery remains on `cp175-shared-character-simulation` and existing draft [PR #28](https://github.com/layoung3/WarcraftSim/pull/28). This document does not record a new published commit or successful final workflow. The implementation ledger and acceptance README remain the coordinator's publication record.

## Implemented integration contract

`/class-build.html?class=paladin` uses the existing shared class-build API under `/api/forever/shared/class-builds`. Creation, preparation, execution, import, report validation and comparison dispatch explicitly distinguish Shaman, Rogue and Paladin. Capability discovery advertises all three class-wide workflows. Paladin remains unavailable through the older typed `generate-level60`/`simulate-level60` adapter; its capability flag and explanation retain that distinction.

The Paladin catalog exposes all **51 current nodes, 17 in each tree**, with sourced rank caps, row gates, prerequisite arrows, stable talent/spell identities and component coverage. A single `PaladinSimRunner` consumes the rank map, actual equipment and encounter. Preset names and editable specialization labels do not choose formulas or a runner. Legal partial ranks and unspent points remain supported. The reviewed examples are Holy **31/5/8** with seven unspent points, Protection **12/31/8**, and Retribution **10/10/31**. They are authored software examples, not claims of popularity, optimality or present best-in-slot gear.

Preparation reuses the existing baseline, equipment resolver, stat contributors, modifier catalog and equipment-effect processor. Gear, enchants and modifiers enter the shared pipeline once; the class applies ranked contributions and derived statistics afterward. Live effect consumers use the existing event pipeline. Available actions and automatic priorities derive from ranks, weapons/shields, targets, resources, auras and cooldowns. Custom priorities remain visible and are revalidated when a required talent or item is removed.

The UI provides class/race/baseline selection, three-tree ranks, equipment and enchants, modifier selection, finite inventory, automatic/custom priorities, Paladin seals/auras and bounded incoming-hit/friendly-target settings. Results retain damage summaries and full events while displaying effective healing, overhealing, absorption, damage taken, blocked damage and configured friendly ending Health separately.

A base-kit review found that the older support catalog did not execute Blessing of Kings. Paladin therefore implements the explicit `blessing-of-kings` action in its own live event path rather than claiming an existing prepared modifier. On the Paladin, active Kings updates current primary statistics, derived contributions and live resource caps; increased Mana capacity does not grant free Mana. The bounded friendly target has declared Health but no class/primary-stat profile, so allied primary statistics are not invented. This is an available custom action, not an automatic undocumented prebuff. Report `maximumMana`/`maximumHealth` remain the initial prepared caps; `endingMaximumMana`/`endingMaximumHealth` record the live ending caps. Paladin result cards show ending value / ending cap, and import validation uses those explicit ending caps. Historical Shaman/Rogue response readers do not acquire these new required fields.

**Apply talent preset** changes only the rank map and preset provenance. Race, baseline, equipment, enchants, modifiers, inventory, encounter, class settings and custom priority are preserved. Automatic policy is regenerated from the resulting character; an unavailable preserved custom action blocks execution. Class switching is a separate full reset.

Build/report import validates before replacing editor state. Failed imports must preserve the build, displayed preparation/results, pinned baseline, comparison and exportable requests. Paladin wire validation requires an explicit baseline mode and actual numeric setting values; legacy Shaman/Rogue serializer behavior remains scoped separately. Unknown schema/class/settings, duplicate or case-aliased properties and malformed ranks are rejected. Legal builds with pending execution prerequisites remain editable and exportable.

Reports contain the build, complete prepared request, preparation/validation snapshots, talent coverage, raw simulation response and source/checkout labels. Paladin report validation requires every response/run/summary member, strict numeric JSON, prepared statistic agreement, coherent notes and seed/duration, and bounded resource/Health totals. These are structural checks. Imported results remain unverified until current-engine replay compares the **complete request and complete simulation response**. Only the known ephemeral profile ID and generated event-correlation IDs are normalized at their exact paths; arbitrary IDs and gameplay fields remain significant. Source/checkout strings are mutable provenance, not authentication.

Pinned comparisons retain both full builds, preparations and prepared requests. Two to forty paired seeds and a separate two-to-forty-seed validation cohort use fresh isolated runs, bounded duration and cancellation. Encounter semantics and relevant explicit probability/resource assumptions must agree. The report gives the paired DPS differences and approximate Student-t uncertainty; it does not optimize or rank healing, absorption or tank assignments.

Detailed formulas, source conflicts and retrieval limitations belong in the separate [Paladin source ledger](CP175-paladin-source-ledger.md), its [machine-readable source records](CP175-paladin-source-ledger.json) and [extracted facts](CP175-paladin-source-facts.json).

## Baseline, race, equipment and artwork boundaries

### Baselines and applicability

Paladin starts in **strict-reviewed** mode. No verified live level-60 Paladin primary-stat record is embedded, so the default visibly blocks execution. Historical proxies require explicit user selection; omitting `baselineMode` from a Paladin wire build does not opt in. An explicit observation retains its own evidence record and passes the shared observation validator.

The supported current race matrix offers **Human, Dwarf and Undead**. Race availability is separate from numerical confidence and racial combat execution. The retained Undead Paladin baseline is a lower-confidence extrapolation and is disclosed as such. Racial actives are not connected, and racial passives are not silently supplied by this adapter.

Source-backed provisional values can execute with their indicators. An unresolved value does not automatically imply that its event mechanic is absent. Seal of Justice illustrates the boundary: the stun event path requires an explicit manual proc-rate assumption because the client sentinel does not establish a rate. Full raid-healing, tank-assignment, positioning and PvP workflows remain separate from the bounded friendly-target, incoming-hit and utility witnesses. Other classes retain their existing coverage inventory and completion backlog.

**A real base-combat omission remains:** Paladin white-hit glancing is not implemented. The shared provisional chance rule does not provide a qualified current Forever glancing damage range, so this resolver disables that outcome instead of borrowing another class's calibration. Preparation notes, capability metadata and `missingCode` disclose it. The 51 implemented talent-component rows do not certify a complete white-hit table or universally accurate melee DPS. Separately, Justice Judgement follows the November 11 general ranged-hit policy; the pinned original model's Always Hit exception lacks independently confirmed raw evidence and remains a source conflict. That disagreement is distinct from the missing glancing mechanic.

### Narrow reviewed loadouts

| Item | Role | Representative existing enchant | Admission boundary |
|---|---|---|---|
| 12782, Corruption | Two-handed sword | 1248668, +25 Strength | Exact published 70888 record and positive Paladin class bit; separately reviewed sword proficiency |
| 21395, Blade of Eternal Justice | One-handed sword | 23799, +15 Strength | Same independent value/restriction/proficiency gates; only one admitted piece of its set |
| 21269, Blessed Qiraji Bulwark | Shield | 1219581, +12 Stamina | Exact published item; shield-use inference from current Paladin equipped-shield prerequisites remains explicitly provisional |

`ForeverPaladinEquipmentCatalog` admits only these exact reviewed records. The 70888 item values are not upgraded to the separately displayed 70938 sword-proficiency source build. The shield-learning SkillLineAbility row was not retrieved; its narrower corroborating evidence and confidence are exposed separately. Item presence is not proof of current acquisition or content availability, and Classic-labeled quest/location records are excluded from that claim.

Unknown class restrictions or proficiency remain **pending**, with the selected build preserved. Manual weapon/shield budgets require explicit selection and cannot coexist with catalog hand inputs. Empty hands do not receive a hidden example weapon. The unsupported three-piece Battlegear of Eternal Justice proc is represented and gated; one admitted set piece does not activate it. Selected static self modifiers and the supported live Faerie Fire path use the existing pipeline; unsupported target effects retain their execution gates.

Review after the first full regression attempt found that adding records to the shared catalog could expose those three IDs through older resolver paths that did not use the new proficiency assessment. `ValidateReviewedExecutionScope` now applies the separate review gate to **only 12782, 21395 and 21269** inside `ForeverSharedEquipmentResolver`. It prevents their raw class-mask bits from granting new execution to unreviewed classes while preserving the policy for pre-existing catalog records. The guard and its regressions passed the subsequent `local-final-03` qualification; the earlier successful focused subset did not establish this boundary.

### Artwork: 49 available source files and five explicit fallbacks

`WarcraftSim.Api/Catalog/forever-paladin-artwork-v1.json` maps 51 talent keys and three item keys from their 70888 records to independently pinned historical artwork. Its default source is gunba commit `1cc5ae1e9abdbf941b7326d2d90421822dce31ba`; the reviewed `inv_sword_07` override uses the existing Napalm commit `c969b8378b63ada9aecfd35db14b373a8aaa66ea`. A complete Git-tree review found **49 of those 54 JPEG files** at the original default source, while the corrected acquisition explicitly selects 48 default records and the one existing item-art record. This is source-file availability, not a claim that 49 images were downloaded or rendered locally.

The five absent files have explicit neutral initials fallbacks: Boundless Light (`ability_paladin_sacredcleansing`), Stoicism (`spell_holy_stoicism`), Holy Shield (`spell_holy_blessingofprotection`), Iron Creed (`spell_holy_improvedresistanceauras`) and Sanctified Judgement (`ability_paladin_judgementblue`).

The initial local acquisition attempted all 54 mappings: **24 downloaded, 30 failed**, exit 1. The failures include absent files and network/403/timeouts. That failed acquisition is retained, not presented as complete artwork qualification. The final manifest separates 49 acquisition records from five known absences, and the CI capture script requests those 49 before API startup. The UI only uses images advertised by the receipt-verified local icon endpoint; missing cache files retain honest fallback titles. Native acceptance checks actual decoded images and fallbacks. Historical artwork does not verify current combat values.

## Twelve acceptance witnesses

The numbering below matches the assignment's twelve required witness categories. For the evidence column, `H` means the passing Paladin HTTP directory `http-final-02/01-paladin-class-build-http.test/paladin-http-mwfev2jn` under the evidence root below; `N` means the completed pure-Node receipts, whose snapshots are identified separately. Final focused/full .NET, strict build, unchanged importer and all eleven HTTP commands passed locally. Native browser and published-head CI remain publication gates. Historical preliminary reports retain their own provenance and are not relabeled. The native harness covers the production UI portions; it does not substitute for focused .NET event-classification, proc-exclusion or rank-effect evidence.

| # | Witness | Retained result / required final evidence |
|---:|---|---|
| 1 | Retribution with real Holy/Protection off-tree effects, plus an off-tree spell consumer in Holy/Protection | `H/retribution-three-trees-report.json` retains 10/10/31, 279 events and a full replay. The 288-pass focused receipt includes `StrengthAndIntellectRanksComposeOnceBeforeActualActionsAndCaps` and `HolyConduitDiscountsActualSelectedSpellCosts`; Protection's example includes Holy Conduit. A mixed rank map alone is not causal proof. |
| 2 | Deeper Holy with actual friendly healing, resources and cooldowns | `H/holy-friendly-target-report.json` retains 31/5/8, 137 events and 6100 effective healing in its explicit scenario; seven points remain unspent. Focused refund/cooldown cases passed in `local-final-03`; native evidence is pending. |
| 3 | Deeper Protection with incoming damage, shield, block and defenses | `H/protection-shield-incoming-report.json` retains 12/31/8, 395 events, 598.4543362105093 absorbed and 1503.3525 blocked. These are the configured bounded scenario's outcomes. |
| 4 | Legal nonpreset partial ranks/unspent points; separate malformed-rank, overspend and prerequisite failures | `H/partial-nonpreset-report.json` retains a legal 12-point three-tree allocation; `zero-intermediate-max-removal.json` checks 0/2/5/0 Strength ranks and exact removal reversal. Separate negative requests are indexed in `H/http-receipts.json`. |
| 5 | Identical effective inputs independent of preset/tree labels | `H/equivalent-labels-report.json` replays identically despite changed provenance/tree labels; `LabelsDoNotSelectAnEngineOrChangeAnEffectivePaladinBuild` supplies the focused contract regression. This is not claimed as a native-browser gate. |
| 6 | Current Judgement hit classification, melee-proc exclusion, Windfury/capstone eligibility and recursion | The passing `local-final-03/focused-final.trx` includes `JudgmentUsesRangedHitAndDoesNotTriggerWeaponSealsOrWindfuryOrCapstone`, `HolyStrikeCannotTriggerWhiteOnlySealsOrStoreUnsealedEchoes` and `SealSwitchConsumesUpToThreeStoredWhiteHitsAndCrusaderNeverStoresAnEcho`; equipment-event tests verify excluded Judgement events and unchanged proc RNG. The native harness does not independently assert these exclusions. |
| 7 | Gear/enchant contribution once, live Kings caps, removal reversal and pending eligibility | `H/retribution-no-enchant-report.json` changes actual preparation/damage; weapon/shield removal changes action availability, restoration reverses it, and unknown item 18820 stays saved but blocked. `H/kings-ending-cap-witness.json` and three Kings reports preserve initial preparation, demonstrate live cap growth and exact removal replay; native display evidence remains pending. |
| 8 | Finite inventory positive, disabled, unselected/zero and incompatible cases | `H/finite-mana-inventory-report.json`, `mana-item-disabled-report.json` and `mana-item-unselected-zero-charge-report.json` retain the 150-second resource witnesses. |
| 9 | Talent preset preserves every independent input and custom priority | `N` and `H/talent-only-preset-before-after.json` preserve independent fields and the now-unavailable custom Holy Shock priority. Native desktop before/after downloads and the mobile preset roundtrip are authored; execution is pending. |
| 10 | Complete build/report roundtrips and full deterministic replay with a nonempty timeline | `H/holy-replay.json`, `protection-replay.json`, `retribution-replay.json` and `kings-active-self-blessing-replay.json` compare complete requests/responses under the allowed generated-ID policy. The run passed all 47 HTTP gates, including six ending-cap negatives. Native roundtrip/replay remains pending. |
| 11 | Atomic malformed/coherently altered import rollback and invalid comparison preservation | `N` and `H` cover malformed ranks/schema/class/settings and coherent report contradictions. The native harness retains before/after downloads for five bad builds, seven bad reports and two invalid comparisons, including displayed result, pinned baseline and comparison state; execution is pending. |
| 12 | Paired comparison, held-out validation, reproducible preparation and comparable scenarios/sources | `H/paired-comparison.json` retains 8 paired comparison seeds plus 8 separate validation seeds, reproduced exactly. Mean enchant improvement was 7.338169169047619 DPS on comparison seeds and 7.275932158829365 on validation seeds; finite-scenario uncertainty remains in the report. Comparison negatives and focused assumption guards passed. Native desktop 4+3 and mobile 3+3 cohorts are pending. |

The separate native workflow requirement is implemented by `tools/paladin-class-build-browser.test.cjs` at 1440×1050 and 390×844: trusted input, three-tree editing, class switching, preparation, representative equipment, simulation, downloaded JSON, import, replay, comparison, Kings ending-cap display and screenshots. **It has not run locally.** Its `two-hand-talents-icon-coverage.json` and `one-hand-shield-talents-icon-coverage.json` retain each actual image/fallback key and state. A two-handed loadout and a one-hand/shield loadout show different item counts, so their rendered-image counts must not be relabeled as 49 each. The manifest's 49 source-available files and five documented absences remain separate from observed per-view cache/loading counts.

The four authored fixture paths are `docs/acceptance/cp175/cp175-paladin-retribution-three-trees.json`, `cp175-paladin-holy-friendly.json`, `cp175-paladin-protection-shield.json` and `cp175-paladin-partial.json`. All four match their final report's complete `build` field/value structure, normalizing only JSON object key order; `http-final-02/01-paladin-class-build-http.test/fixture-to-final-report.json` retains each exact comparison and file hash. Their intended use is representative acceptance, not proof that every legal talent combination or equipment interaction is complete.

## Validation actually retained

All evidence paths in this section are under:

```text
/workspace/scratch/f370a1002cd9/evidence/paladin-delivery-20261113
```

| Qualification | Actual outcome | Receipt |
|---|---|---|
| Earlier frozen frontend Node pass, 19:45:52–19:46:05 UTC | 28 commands, all exit 0: 22 retained legacy entrypoints, new Paladin contract, five syntax checks; 75 Paladin assertions. Source hashes unchanged during the run. This precedes the necessary Kings ending-cap correction below. No HTTP/native execution. | `node-final/receipts.json` and its 28 raw logs |
| Post-correction affected Node pass, 19:58:58–19:58:59 UTC | Seven commands, all exit 0: Paladin contract now 102 checks; existing class-build contract including 74 historical talent-only preservation assertions; five syntax checks. Hashes unchanged during qualification. The 28-command receipt is preserved separately. No HTTP/native execution. | `node-ending-caps/receipts.json` and its seven raw logs |
| Kings event-identity correction, 20:07:28 UTC | Five commands, all exit 0: the same 102 Paladin and 74 historical preservation assertions, two harness syntax checks, and 16 checks applying each actual harness predicate to the retained exported event and mutated identities. No live HTTP or native browser execution. | `node-kings-event-identity/receipts.json`, `01.log` through `05.log` |
| Python offline regression pass, 19:42:23–19:42:26 UTC | 10 commands exit 0; 362 test cases reported, 359 passed and three explicit optional retained-evidence cases skipped. Filenames containing `http` do not make these unittest runs live HTTP acceptance. | `python-final/receipts.json`, `01.log` through `10.log` |
| Existing reviewed intake audit, 19:48:18 UTC | Exit 0; 79 registered legacy intake IDs, no rejections. This command's scope is not a new Paladin live-catalog count. | `python-final/reviewed-intake-receipt.json`, `reviewed-intake.log` |
| Preliminary .NET integration, 19:36:44–19:37:07 UTC | **Exit 1: 44 passed, two failed, zero skipped, 46 total.** Failures were the old earlier-row-only parent expectation for same-row Boundless Light/Holy Shock and the previous coverage expectation that Paladin's engine was `missing`. Corrections exist in source; this receipt is not a passing rerun. | `focused-integration-initial/receipt.json`, `command.log`, `paladin-initial.trx` |
| First final-candidate local .NET attempt, 20:05:12–20:06:08 UTC | Focused subset: **174 passed**. Strict solution Release build: **exit 0, zero warnings/errors**. Full suite: **exit 1, 8,451 passed / three failed / zero skipped, 8,454 total**. Subsequent guard/coverage corrections require `local-final-02`; this attempt is preserved as failed overall. | `local-final/receipts.json`, `01-focused.log`, `02-release-build.log`, `03-full-dotnet.log`, `focused-final.trx`, `full-final.trx` |
| Second local focused attempt, 20:08:53–20:09:39 UTC | **Exit 1: 287 passed / one failed / zero skipped, 288 total.** The new older-adapter guard test reached pre-existing gear-shape validation before the intended review guard. Its fixture was corrected; the failed result remains retained. | `local-final-02/receipts.json`, `01-focused.log`, `focused-final.trx` |
| Final local qualification, 20:09:56–20:11:44 UTC | **All four commands exit 0.** Focused: **288/288 passed**. Strict solution Release build: **zero warnings/errors**. Full .NET: **8,455 passed / zero failed / zero skipped**. Original importer `--build` passed unchanged, including its nested Release build, byte-identical synthetic imports, reviewed override and retained pending metadata. | `local-final-03/receipts.json`, `01-focused.log`, `02-release-build.log`, `03-full-dotnet.log`, `04-original-importer-build.log`, `focused-final.trx`, `full-final.trx` |
| Preliminary production HTTP, 19:38:28–19:38:32 UTC | Exit 0; **76 real requests, 39 gates, 13 nonempty-timeline report witnesses** against `http://127.0.0.1:5142`. Later runtime and boundary corrections are outside this binary's qualification. | `http-initial/receipt.json`, `command.log`, `api.log`; preliminary directory below |
| First final-candidate HTTP attempt, 20:06:10–20:06:12 UTC | **Exit 1 after 22 requests, six passed gates and six stored report witnesses.** The Kings harness asserted a nonexistent public `auraKey` field. Actual exported identity and live cap growth were present; the two harness assertions were corrected separately and requalified in `http-final-02`. This failed receipt remains unchanged. | `http-final/receipts.json`; `http-final/01-paladin-class-build-http.test/paladin-http-mwfeq4cc/failed-acceptance.json` and adjacent raw receipts/reports |
| Corrected Paladin/shared HTTP, 20:10:01–20:10:10 UTC | Paladin: **exit 0, 88 real requests / 47 gates / 16 nonempty-timeline reports**. Existing shared Shaman/Rogue: **exit 0, 82 responses/checks / six complete build-report witnesses**. | `http-final-02/receipts.json`; Paladin directory `H`; `02-class-build-http.test.log` and its report directory |
| Subsequent legacy HTTP launcher failure, 20:10:10–20:10:11 UTC | `solo-dps-prepared-casters.test.cjs` exited 1 with **`spawn dotnet ENOENT`** before its child API started. The outer helper lacked the local SDK directory on PATH. This is not a simulated combat failure or a passing test; only remaining legacy commands were retried under `http-final-03`. | `http-final-02/03-solo-dps-prepared-casters.test.log`, child `synthetic-api.log`, and `receipts.json` |
| Remaining legacy production HTTP, 20:10:57–20:11:06 UTC | **All nine commands exit 0**, using the same final API binary. Includes prepared casters, 199 extra-class requests, 72 Expose Armor requests, 38 strict Enhancement requests, 88 Enhancement and 53 Rogue UI HTTP gates, 40 Feral requests/eight full reports, Combat Python HTTP and Combat report compatibility/replay. | `http-final-03/receipts.json`, nine numbered logs and their raw report directories |
| Local Chromium capability attempt, 19:30:42 UTC | Exit −6 before acceptance: `socket() failed: Operation not permitted (1)`, followed by crashpad/zygote errors. No native acceptance or screenshot success is inferred. | `chromium-capability.json` |
| Initial artwork download | Exit 1; 24/54 downloaded, 30 failed. Independent full-tree review subsequently separated the five absent files. | `artwork/receipt.json`, `command.log`, `pinned-directory-review.json` |

The preliminary directory is `/workspace/scratch/f370a1002cd9/evidence/paladin-delivery-20261113/http-initial/paladin-http-mwfdqi5i`. Its `http-receipts.json` indexes raw request/response bodies with byte counts, SHA-256, timestamps and status codes; `acceptance-summary.json` indexes every gate and named report. The preliminary API DLL SHA-256 is `6f563d17b4b4e99311ddfe70ad1b219f75832dd45a1e072ddfd26a218232cb64`. Reports explicitly identify the uncommitted preliminary source and local checkout, rather than claiming a published SHA. These earlier Paladin reports predate the explicit ending-cap fields and remain historical evidence.

The passing current Paladin directory `H` is `/workspace/scratch/f370a1002cd9/evidence/paladin-delivery-20261113/http-final-02/01-paladin-class-build-http.test/paladin-http-mwfev2jn`. The API ran at `http://127.0.0.1:5271`; its DLL SHA-256 `ade4d775ac043160ab2299f816228056dfdb526f19a04892b23ff069c4ad74a8` matches both final HTTP receipts and the subsequently inspected built API. Source and checkout labels explicitly describe uncommitted local final content. `http-final-02/receipts.json` retains the exact API launch and all three command outcomes, including the later launcher failure. The first two passing HTTP suites were not rerun merely to erase that failure. All nine remaining commands passed in `http-final-03` after the helper's PATH correction.

Receipt SHA-256 values: Node `65f6d717048482525da038761dd6270dd144e98dedda68e99cff04ba57eba8e6`; Python `7b3e4576f6b1df1723dda94715e25c306126e8c0d9e260b68025f8e621c1bad4`; preliminary HTTP acceptance summary `c583c8951221b30b60a33772fa6d5fad636ae5fbc94ff6fcd813fa0c3b46b456`.

The separate post-correction Node receipt has 6,699 bytes and SHA-256 `49b8e8ac3b53291865bee7fb44ff85708c5106184667673ef26105b9202dcecd`. It adds missing/invalid/inconsistent ending-cap cases, accepts legal growth beyond initial prepared caps, and verifies that ending caps remain significant in replay. Its exact commands were:

```bash
node tools/paladin-class-build-contract.test.cjs
node tools/class-build-contract.test.cjs
node --check WarcraftSim.Api/wwwroot/class-build.js
node --check WarcraftSim.Api/wwwroot/class-build-contract.js
node --check tools/paladin-class-build-contract.test.cjs
node --check tools/paladin-class-build-http.test.cjs
node --check tools/paladin-class-build-browser.test.cjs
```

### Preserved final HTTP failure and exact correction

The first final-candidate HTTP run failed on this assertion in `tools/paladin-class-build-http.test.cjs`:

```javascript
assert.ok(kingsActive.simulation.run.timeline.some(x =>
  x.type === 9 && x.auraKey === "paladin-blessing-of-kings" &&
  x.targetActorKey === "paladin"));
```

The retained `http-final/01-paladin-class-build-http.test/paladin-http-mwfeq4cc/kings-active-self-blessing-report.json` confirms the cause: public `CombatEvent` has no `auraKey`. Its actual applied-aura event has type 9, source/target `paladin`, `abilityKey="blessing-of-kings"`, `effectKey="paladin-blessing-of-kings-effect"` and a nonempty `auraInstanceId`. Mana capacity grew from **2367 to 2482.5**, and Health capacity from **2681 to 2811**. Those observations support the event/cap behavior in that report; they do not make the interrupted acceptance run successful.

Only the HTTP and native Kings identity predicates were corrected, using all of those exported identity fields rather than descriptive text. Neither harness retains an `auraKey` assumption. `node-kings-event-identity/receipts.json` has 6,584 bytes and SHA-256 `4c810db49d715c8aee1886c3ee8fcce839717294074a2348553bd348384cf124`. It records the exact five commands, including the verbatim `node -e` check that extracts the actual two predicates from the harnesses, accepts the retained event and rejects seven altered identities per harness. The other commands rerun the Paladin/shared contracts and syntax-check both harnesses. The failed HTTP directory remains unchanged; the subsequent passing Paladin attempt is retained separately in `http-final-02`.

### Preserved first full-suite failures and admission correction

The three failures in `local-final/full-final.trx` were:

| Test | Exact failed expectation | Scoped correction |
|---|---|---|
| `Cp174PrerequisiteAllocationTests.ProvisionalItemArtRequiresExactPinnedIdAndName` | Expected 24 item-art mappings; actual 27 | Account for the three newly sourced item mappings while retaining the existing 20 legacy exact-ID/name mappings and their assertions. |
| `Cp175PreparedCasterStaticCatalog.EmbeddedCatalogRemainsPendingEvidenceWithoutChangingLegacyRecords` | Expected null class evidence; actual evidence for new item 12782 | Admit only the three exact reviewed Paladin records in the census; retain prior records' evidence expectations. Independent review also found and fixed the real older-resolver admission gap described above. |
| `Cp175SharedArchitectureStageATests.CapabilityDiscoveryIncludesFourInitialClassesAndRestrictedCasters` | Expected eight capability classes; actual nine | Include the explicit Paladin capability while preserving the old typed-endpoint restriction. |

These failures are not relabeled as passes. `local-final/receipts.json` retains each exact command, timestamps and log hashes; it contains no importer execution. The subsequent `local-final-02` fixture failure is also retained. The final `local-final-03` qualification passed the corrected assertions, narrow three-item admission guard and disclosed base-combat/source limitations, including the full suite and unchanged importer gate.

### Exact completed Node commands

These commands were run from the integration worktree. The receipt is authoritative for timestamps, output, file hashes and exit codes. Names containing `browser` here are offline helper/contract checks.

```bash
node tools/solo-dps-ui-contract.test.cjs
node tools/solo-dps-comparison.test.cjs
node tools/solo-dps-browser-identity.test.cjs
node tools/solo-dps-enhancement-rejections.test.cjs
node tools/solo-dps-loading-contract.test.cjs
node tools/solo-dps-browser-click.test.cjs
node tools/class-build-contract.test.cjs
node tools/solo-dps-elemental-contract.test.cjs
node tools/solo-dps-balance-contract.test.cjs
node tools/solo-dps-shadow-contract.test.cjs
node tools/solo-dps-enhancement-contract.test.cjs
node tools/solo-dps-rogue-contract.test.cjs
node tools/solo-dps-rogue-legacy-compat.test.cjs
node tools/solo-dps-feral-contract.test.cjs
node tools/solo-dps-feral-loading.test.cjs
node tools/solo-dps-feral-comparison.test.cjs
node tools/solo-dps-feral-browser-support.test.cjs
node tools/solo-dps-combat-contract.test.cjs
node tools/solo-dps-combat-loading.test.cjs
node tools/solo-dps-combat-identity.test.js
node tools/solo-dps-combat-comparison.test.js
node tools/solo-dps-combat-browser-support.test.cjs
node tools/paladin-class-build-contract.test.cjs
node --check WarcraftSim.Api/wwwroot/class-build.js
node --check WarcraftSim.Api/wwwroot/class-build-contract.js
node --check tools/paladin-class-build-contract.test.cjs
node --check tools/paladin-class-build-http.test.cjs
node --check tools/paladin-class-build-browser.test.cjs
```

### Exact completed Python and preliminary integration commands

```bash
python3 -m unittest discover -s tools/ForeverEquipmentImportCli -p 'test_*.py' -v
python3 tools/test_feral_historical_identity.py -v
python3 tools/test_rogue_combat_identity.py -v
python3 tools/test_solo_dps_combat_stage_regression.py -v
python3 -m unittest discover -s tools -p test_solo_dps_rogue_subtlety_http.py -v
python3 -m unittest discover -s tools -p test_solo_dps_rogue_cutthroat_http.py -v
python3 -m unittest discover -s tools -p test_solo_dps_rogue_dirty_deeds_http.py -v
python3 -m unittest discover -s tools -p test_solo_dps_rogue_elusiveness_http.py -v
python3 -m unittest discover -s tools -p test_solo_dps_rogue_initiative_http.py -v
python3 -m unittest discover -s tools -p test_solo_dps_rogue_serrated_blades_http.py -v
python3 tools/ForeverEquipmentImportCli/reviewed_intake_audit.py

/workspace/scratch/f370a1002cd9/tooling/dotnet/dotnet test \
  WarcraftSim.Tests/WarcraftSim.Tests.csproj -c Release --no-restore \
  -m:1 -nr:false -p:UseSharedCompilation=false \
  --filter 'FullyQualifiedName~Cp175Paladin|FullyQualifiedName~Cp175ClassBuild' \
  --logger 'trx;LogFileName=paladin-initial.trx' \
  --results-directory /workspace/scratch/f370a1002cd9/evidence/paladin-delivery-20261113/focused-integration-initial

node tools/paladin-class-build-http.test.cjs
```

The HTTP command used the running local production API and retained its output beneath `H`; it was not a mocked server or browser execution. The three Python skips were one optional Subtlety immutable-old-capture check and two Dirty Deeds optional retained local-evidence checks; none is counted as passing.

## Final local qualification and publication gates

Final local receipts are retained under `/workspace/scratch/f370a1002cd9/evidence/paladin-delivery-20261113`, including executed commands, start/end UTC, exit codes, complete logs/TRX and the tested source or binary identity. A candidate change after a gate invalidates the affected qualification; it does not turn an earlier failure into a pass. Publication remains the coordinator's responsibility. The final PR receipt must tie the published SHA to these tested inputs and supply the actual native-browser/artifact inspection and exact-head CI outcome; those future outcomes are not claimed here.

| Gate | Final result |
|---|---|
| Candidate/published commit and source-file snapshot | Locally tested uncommitted candidate descended from `2bc4d5a364f60e200e9b27c215316da48425d9fa`; final API DLL SHA-256 below. **Published SHA pending** in the final PR receipt. |
| Strict Release build, warnings as errors | **Passed locally** in `local-final-03/02-release-build.log`; zero warnings/errors; exact command in its receipt |
| Focused final Paladin/shared regressions | **288/288 passed locally** in `local-final-03/focused-final.trx`, including the expanded equipment/discovery/legacy contract filter |
| Full .NET suite | **8,455/8,455 passed locally; zero failed/skipped** in `local-final-03/full-final.trx` and `03-full-dotnet.log` |
| Original importer gate, unchanged | **Passed locally, exit 0**, `python3 tools/ForeverEquipmentImportCli/integration_smoke.py --build`; `local-final-03/04-original-importer-build.log` retains the complete result |
| Final production HTTP against that compiled candidate | **All eleven commands passed locally**: Paladin and existing shared class-build in the first two `http-final-02` entries, nine remaining commands in `http-final-03`. Earlier launcher failure remains recorded. |
| Native desktop/mobile browser acceptance and artifact inspection | **Pending** — record trusted-input receipts, screenshots, downloads and decoded-image/fallback counts |
| Exact-published-head CI | **Pending** — record workflow URL, `head_sha`, conclusion and inspected artifact names |

The existing CI commands remain the final gate, including:

```bash
dotnet build WarcraftSim.slnx --no-restore --configuration Release -warnaserror
dotnet test WarcraftSim.slnx --no-build --configuration Release --logger 'console;verbosity=normal'
python3 tools/ForeverEquipmentImportCli/integration_smoke.py --build
bash tools/solo-dps-capture.sh
```

### Final receipt index

| Evidence group | Final receipt / identity / inspection |
|---|---|
| Tested candidate | Paladin integration descended from accepted base `2bc4d5a364f60e200e9b27c215316da48425d9fa`; API DLL SHA-256 `ade4d775ac043160ab2299f816228056dfdb526f19a04892b23ff069c4ad74a8`. The first publication and its content-equivalent local commit are recorded below. Final corrected published source/tree mapping belongs in the PR receipt. |
| Local mandatory gates | `local-final-03/receipts.json`: strict Release, focused 288, full 8,455 and unchanged importer all passed. `http-final-02/receipts.json` first two entries and every `http-final-03/receipts.json` entry passed. Node/Python snapshots and explicit historical failures/skips are retained above. |
| Final representative reports | `H/acceptance-summary.json` indexes all 16 nonempty-timeline reports and 47 gates; `H/http-receipts.json` records 88 actual request/response pairs with bytes/hashes. Holy, Protection, Retribution, partial, three Kings, inventory and comparison files are retained with ending caps. Four fixture/build comparisons passed in `fixture-to-final-report.json`. |
| Native production observations | **Pending** — final acceptance summary, trusted-input and HTTP observations, complete downloads, replay outcomes and before/after rollback state |
| Visual and icon inspection | **Pending** — inspected desktop/mobile screenshot paths, actual clipping/control/text findings, decoded-image counts and documented fallback keys |
| Published exact head and CI artifacts | **Pending** — full published SHA, parent/tree relationship to tested content, workflow URL/`head_sha`, individual gate conclusions, artifact IDs/names and inspected files |

The capture script caches the 49 available Paladin artwork files, launches the built production API, then runs the new HTTP harness with a 240-second command bound and the native harness with a 300-second command bound while retaining existing acceptance paths. The overall workflow remains at its existing 20-minute timeout. Expected new report directories are `/tmp/cp174-full-reports/paladin-class-build-http/paladin-http-*` and `/tmp/cp174-full-reports/paladin-class-build-browser/paladin-browser-*`; screenshots use `/tmp/cp174-ui-captures/cp175-paladin-browser-*.png`.

Native output should include `acceptance-summary.json`, `native-input-receipts.json`, `http-observations.json`, replay receipts, actual downloaded build/report/comparison JSON and before/after invalid-import snapshots. CI's existing artifacts are `cp174-level60-full-simulation-reports`, `cp174-ui-viewport-captures` and `cp174-pinned-talent-icons`. Artifact existence alone does not establish that every gate passed; inspect the relevant summaries, images and raw reports against the final workflow SHA.

## Changed-file map and remaining scope

The integration lives in `ForeverClassBuildService`, `ForeverClassBuildController`, shared capability discovery, catalog/coverage/models, and the four `class-build` frontend files. New runtime files are `PaladinSimRequest`, `PaladinSimRunner`, `PaladinSpellbook`, `PaladinCombatProcessor` and `PaladinResolution`; source topology/data live under `WarcraftSim.Data/Forever/Paladin`. The narrow equipment catalog, the three-ID guard in `ForeverSharedEquipmentResolver`, and artwork manifest are separate from talent formulas. Regression entrypoints are `CP175_PaladinClassBuild.cs`, `CP175_PaladinMechanics*.cs`, `CP175_PaladinEquipmentEvents.cs`, `CP175_PaladinSources.cs` and the three `tools/paladin-class-build-*.test.cjs` files.

Local qualification is complete for this candidate. Remaining delivery gates are publication on the existing branch, actual native acceptance and report/screenshot inspection, and exact-final-head CI recorded in the final PR receipt. Remaining product scope includes the unimplemented white-hit glancing outcome and its unresolved current damage calibration, wider interaction evidence, unreviewed item/proficiency and numerical-source gaps, racial combat integration, the separate raid-healing/tank/PvP workflows, and the other classes' class-wide backlog. No new permanent branch/PR, merge, deployment, release, destructive cleanup or additional spending is part of this milestone.

## First published-head CI and artwork-source correction

The first Paladin publication was `c4ecfef10bd27312b0b1f85a5ea3ad0d5d46d0d2`,
parent `2bc4d5a364f60e200e9b27c215316da48425d9fa`, tree
`b92238ad622f864bc188e864f62a6251099a0db8`. Local candidate
`9646906da380de7090ac74eff5b846f212bb4a6c` has the same parent and file tree;
publication changed metadata only. [Workflow #43500754009](https://github.com/layoung3/WarcraftSim/actions/runs/43500754009)
passed the strict build and all 8,455 .NET tests, then failed in the browser-capture
step **before launching the API/browser**. No native screenshots or reports were
produced, and subsequent required gates were skipped. The exact error was:

```text
Unavailable: inv_sword_07 Cache source/version/build/product mismatch; use a separate reviewed destination
Pinned reference icons: 49 {'downloaded': 46, 'cached': 2, 'failed': 1}
```

The confirmed cause is the existing item-icon census having already acquired
`inv_sword_07` from the reviewed immutable Napalm item-art source; the new Paladin
manifest requested that filename from the different reviewed gunba art source.
The acquisition guard correctly refused to overwrite or relabel the receipt.
The retained file is 1,394 bytes, SHA-256
`d6eb389cd0c4268953f8c91a3c557fb2680b610c0b6ef0a39e74c9ef870a72ce`,
Git blob `a0304e14e79f8f899b8ad15265d7309ab306fd2b`, at Napalm commit
`c969b8378b63ada9aecfd35db14b373a8aaa66ea`. An independent complete Git-tree
response confirms this exact blob/path/size. The other 48 Paladin records match
their gunba pin and blob hashes.

The correction explicitly selects the existing Napalm source for this one
manifest record. All other records retain the original gunba default. Both
sources are already accepted by the strict runtime availability controller;
its byte/hash/URL/source checks remain unchanged. Unknown source declarations
and conflicting references fail closed. Existing cache files and receipts are
preserved. The 49 source-available keys therefore have 48 selected gunba records
and one selected Napalm record; the five unavailable mappings still render
neutral fallbacks. Historical artwork never upgrades current combat provenance.

The failure log, per-step outcomes, immutable icon ZIP, original sword bytes and
receipt, and tree-membership proof are retained under
`evidence/paladin-delivery-20261113/ci-attempt1/`. The correction passed 12 new
source-selection regressions, the existing two icon-cache and 40 acquisition
regressions, and the complete importer Python discovery (141 passed, zero
failed/skipped). The latter command and file hashes are retained in
`artwork-correction/receipts.json`:

```text
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tools/ForeverEquipmentImportCli -p 'test_*.py' -v
```

The real retained-artifact witness reused all 49 image/receipt pairs offline
(`downloaded: 0, cached: 49, failed: 0`), checked every manifest Git blob SHA,
and preserved every byte. Corrupt image bytes, a foreign receipt and the old
default source requested for the Napalm sword all still fail without overwrite.
It made zero network requests and retained its outcomes in
`icon-source-correction-retained-cache-witness.json`.

Combat C# and frontend/native harness content is unchanged by this correction;
their existing local tests remain applicable. Final acceptance still requires
the corrected published head's full workflow, downstream gates and actual
native artifacts, recorded in the final PR #28 delivery receipt.

## Second published-head CI and explicit native scenario transition

The artwork correction published as `9a9a4cd9a725150f3cedaf57ad117f707100597a`,
parent `c4ecfef10bd27312b0b1f85a5ea3ad0d5d46d0d2`, tree
`0121c11c6388c8bfe8a75d4bd342babb93aa8196`.
[Workflow #43502706165](https://github.com/layoung3/WarcraftSim/actions/runs/43502706165)
passed the strict build, all 8,455 .NET tests, all 49 requested icon acquisitions,
Paladin HTTP (88 requests / 47 gates / 16 reports), and the existing shared
native suite (31 checks). The new Paladin native suite then failed at the
Retribution damage-event assertion:

```javascript
assert.ok(ret.data.simulation.run.timeline.some(
  x => x.type === 6 && String(x.abilityKey).includes("judgement")))
```

The retained downloaded report proves the fixture cause. After correctly
verifying talent-only preset preservation, the harness still requested the Holy
friendly-target scenario: ally at 40% of 6,000 Health, 250 incoming damage every
three seconds, healing below 95%, and 10% starting Mana. The automatic policy
completed 14 Flash of Light casts, healed 5,199.14478955881, used one finite Mana
potion and ended with 9.6557605474052 Mana. It had no seal or Judgement casts.
Preserving these inputs is required behavior; a talent preset must not silently
replace the encounter or policy conditions.

The harness now explicitly selects self healing, disables the friendly target
and chooses a 70% healing threshold through trusted UI controls **after** the
unchanged preservation assertions. Starting Mana remains 10% and inventory
remains one finite potion. Protection independently declares its 70% threshold;
the isolated Kings scenario clears incoming attack power as well as base damage.
All positive Judgement, block/absorption, replay and live-cap assertions remain.
The scenario correction leaves simulator and preset behavior unchanged.

Separate visual inspection of the actual desktop Holy screenshot found its
`1,533.19` healing value extending beyond the narrow result card. The result
card minimum width is now `min(100%, 190px)` instead of 140px, preserving flex
wrapping and allowing one full-width column on narrow screens. The native
screenshot helper now checks displayed metric-value rectangles against their
cards' content bounds (excluding border/padding, with one-pixel tolerance) and
retains those measurements. This is a targeted presentation correction; final
desktop/mobile rendering still requires the corrected CI capture.

Targeted local HTTP qualification used the unchanged qualified API DLL and
reproduced the failed native report's full prepared request and complete
simulation after the contract's allowed generated-ID normalization. Source and
checkout labels correctly differed between CI and the local reproduction.
The corrected Retribution scenario produced four Judgement damage events and
one Mana potion use. The subsequent Protection scenario produced actual damage
taken, 1,403.129 blocked and 398.0073362105092 absorbed. The isolated Kings
before/active/removed runs had zero incoming damage, larger active ending caps,
and exact request/simulation equality after removal. Seven real HTTP requests
(six simulations and one talent-preset application) passed. The initial local
reproduction driver used the wrong endpoint and received HTTP 404 before any
simulation; that failed invocation remains retained separately.

`native-scenario-correction-02/receipts.json` and `node-receipts.json` retain the
actual requests/responses and commands. Browser-harness syntax, 102 Paladin
contract checks, 74 existing preservation assertions and complete reproduction/
removal comparisons passed. These local checks are not native-browser execution.
The failed CI's complete log, step receipt, downloaded reports, two completed
Paladin desktop screenshots plus its failure screenshot, and immutable artifact
archives are retained under `ci-attempt2/`. Native mobile acceptance and all
twelve downstream mandatory gates were not reached in that run; they still
require the final corrected published head's successful workflow.

## Third published-head CI and mobile coordinate evidence

The native scenario correction published as
`47b53549e22735add7db774589ea1a87875ad305`, parent
`9a9a4cd9a725150f3cedaf57ad117f707100597a`, tree
`cf1b47009cf1ac403c06f81113e9d582f85c7958`.
[Workflow #43504851146](https://github.com/layoung3/WarcraftSim/actions/runs/43504851146)
checked out PR merge commit `150d83773678d500c2be8c870d36b82d22e42171`;
its complete tree independently equals the published tree. The strict build,
8,455 .NET tests, 49 artwork acquisitions, shared and Paladin HTTP checks, and
31 existing shared native checks passed again. The Paladin native suite passed
41 gates, including the corrected Retribution Judgements, Protection mitigation,
isolated Kings reversal, desktop replay/comparison, all twelve invalid-import
rollback cases, class switching and mobile talent-preset preservation.

It then failed while checking the mobile report-export click:

```text
AssertionError [ERR_ASSERTION]: Trusted click did not reach #exportReport
```

Sequence 304 recorded a visible, enabled, hit-tested center at
`(77.84375, 432.828125)`. Its three correctly targeted, trusted mouse events all
reported `(77, 443)`. The 10.171875-pixel vertical difference violates the
unchanged strict `abs(event.y - point.y) < 1` predicate. The downloaded complete
`132-mobile-protection-unenchanted/warcraftsim-class-build-report.json` exists:
the export itself succeeded. The old receipt did not record visual-viewport
geometry or browser version, so the precise origin of that old difference
cannot be established from the trace alone. A visual-viewport translation is
consistent with the screenshot and Chromium's coordinate conversion; it remains
an inference about the old run, not a measured old offset.

The shared native helper now settles and records visual-viewport offsets,
dimensions, page coordinates, scale and device-pixel ratio alongside the target
rectangle and document scroll. In the enforced unit-device-scale environment it
selects the layout pixel center `floor(rectCenter) + 0.5`, then subtracts the
floored visual origin for CDP dispatch. It hit-tests that actual selected layout
point and checks its viewport bounds. The original rectangle center still owns
the strict less-than-one-pixel correlation. Event-time viewport samples must
equal the dispatch sample. Trusted target/button/sequence/order checks, exactly
one press/release, and the existing narrow right-click release exception remain.
There is no DOM activation, relaxed tolerance or retry of failed input.

This bounded mapping follows the pinned Chromium implementation at
`8fec18bd72555558739ea7c21a6dc2733ced58bf`:

- [FrameTranslation](https://github.com/chromium/chromium/blob/8fec18bd72555558739ea7c21a6dc2733ced58bf/third_party/blink/renderer/core/events/web_input_event_conversion.cc)
  floors the visual origin and adds elastic overscroll.
- [WebMouseEvent root conversion](https://github.com/chromium/chromium/blob/8fec18bd72555558739ea7c21a6dc2733ced58bf/third_party/blink/common/input/web_mouse_event.cc)
  removes frame scale and adds that translation.
- [MouseEvent client coordinates](https://github.com/chromium/chromium/blob/8fec18bd72555558739ea7c21a6dc2733ced58bf/third_party/blink/renderer/core/events/mouse_event.h)
  floor ordinary mouse coordinates.
- [CDP InputHandler](https://github.com/chromium/chromium/blob/8fec18bd72555558739ea7c21a6dc2733ced58bf/content/browser/devtools/protocol/input_handler.cc)
  already applies browser and page/pinch scaling; the helper does not apply it
  twice. Fresh profiles use default browser zoom; non-unit DPR is rejected.

Active elastic overscroll is outside the helper's compensation. A changing
viewport or mismatched delivered event fails instead of inventing an offset.
The Paladin harness now retains browser version and `Page.getLayoutMetrics`
receipts at both desktop and mobile sizes, including the configured viewport,
DPR and optional browser-zoom assertion.

Actual visual inspection also found the long source SHA clipping the mobile
right edge. The section header can now shrink and the source text wraps without
hiding content. Screenshot assertions retain document `clientWidth` and require
page width to fit both that width and the configured viewport; an expanded
mobile `innerWidth` can no longer mask overflow. The earlier metric-card content
bounds assertions remain intact.

All 28 retained offline Node commands passed against this correction, including
12 new visual-viewport/scale/drift cases, ten core navigation causal checks,
existing Elemental export checks, 936 legacy Rogue regression cases plus 52
actual import-rollback checks, 102 Paladin contract checks, 74 talent-only
preservation assertions, and syntax checks. Tracked source hashes remained
unchanged throughout validation. Two independent read-only reviews found no
concrete blocker. These are local modeled/contract checks, not native Chromium.
No C# or HTTP runtime behavior changed in this correction, so its prior local
qualification remains applicable; the mandatory full final-head CI still runs.

`native-viewport-correction/receipts.json` retains commands, outcomes and source
hashes; four source receipts retain the pinned Chromium input URLs, byte sizes
and SHA-256 hashes. `ci-attempt3/` retains the complete failed log, per-step and
tree-identity receipts, downloaded reports and actual screenshots. All twelve
downstream mandatory gates were skipped in that failed run. Final acceptance
still requires the corrected published head's successful native workflow,
downstream gates and visual artifact review, recorded in PR #28's final receipt.
