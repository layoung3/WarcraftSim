# CP175 Shadow Focus hit parity and Inner Focus critical eligibility

## Scope and historical source

This cohesive correction changes only `ShadowPriestSimRunner`: Shadow Focus contributes **one hit percentage point per rank** to both runtime resolution and precombat preview; Inner Focus's **25-point critical bonus applies only to Mind Blast among the current Shadow damage spells**. Shared free-cast eligibility, reservation/consumption, cancellation, other Priest/healer lanes and Death's Early Demise0/15/30 at<=20% are unchanged. The provisional manual base83, gear contributions, cap100, strict partial0/0/36 default and15unspent disclosure remain unchanged.

The already authorized `gunba/wow-forever-sim@1cc5ae1e9abdbf941b7326d2d90421822dce31ba`, `sim/priest/talents.go`, was freshly retrieved independently as UTF-8 and base64. Bytes agree; Git blob SHA1 is `b8843c8e2f2148214ad18003ac8e954bc67ab337`, SHA256 `957dcd8359974c79d708f6dbc0faac1fd6ff5004e385016611d6172cf09c8c8f`. Focus307–318 supplies1%/rank; Forever IF crit mask370–383 includes Blast but not Death/Flay/Pain/Plague; free-cast logic390–407 is separately broader. Early Demise61–95 supplies15/30 at<=20. Fresh connector receipts/raw source are retained in `.task-evidence/shadow-focus-20261112/source`; these are not recovered original pre-outage artifacts or current-server certification.

## Admission and exclusions

Strict itemized Shadow still has no early Discipline fillers. Its existing IF/Penance/PI rejection tests are preserved; no legal fully itemized cross-tree build is invented. IF execution witnesses use the existing **legacy aggregate** admission with11Discipline and enough Shadow points inside the51point budget. This is not new strict UI support.

Application/landing semantics, Weaving ramp, per-tick Flay resolution, source coefficients, Spirit Tap, additional cross-tree talents, IF cooldown-start timing, new modes, Core architecture and all unrelated class mechanics are deferred. No shared cooldown processor or frontend product behavior changes are included. The browser acceptance adds an independent literal88 hit witness for the base83/Focus5/no-gear setup while retaining full scalar parity, replay/import and accepted catalog tooltip coverage.

## Baseline custody

Before edits, exact accepted source `bd1c1836cf4ca2d1a860be440f814af1ca6e86b5`, tree `3655e74adad2632779de2eed050d96b1486a0e48`, was archived and all1277 source hashes verified. The full local runtime and static assets were archived;25 final-local runtime receipt hashes agree. Acceptance receipt SHA256 is `1d3980f1d33c5fcd102e11f08db449e2805b8305aabc81c443af511b6433ce92` for [Actions43191581952](https://github.com/layoung3/WarcraftSim/actions/runs/43191581952).

This is the **tested equivalent local runtime**, built during the earlier local validation with different informational Git version, not a claim of byte identity to published CI binaries. Source equivalence and complete response reproduction provide the relevant provenance. Archived API launches use Production and an explicit archived content root, preserving descriptors without following mutable Development source asset paths. The original627-response pre-outage corpus remains unavailable and is not claimed restored.

## Source-derived red and green

The identical115 new cases first gave49expected failures/66passes:15literal Focus preview cases,25actual five-spell landing witnesses,6Death IF critical-boundary cases and3actual reserved-Death witnesses. Runtime landing assertions precede preview assertions, so those failures independently prove the runtime defect. Corrected focused execution passes143/143, including those115unchanged tests, existing precombat/three goldens, shared major cooldown and strict admission tests. Five spell free-cast controls and cancellation/reservation behavior remain unchanged. Commands, source/test hashes, logs and TRX are retained under `focused-tests`.

## Complete response oracles

For Focus, candidate base B/rank r is compared with the archived old runtime at base **B-r**, leaving talents and everything else unchanged: old `(B-r)+2r+gear` equals new `B+r+gear`, including the cap. This preserves the full RNG path, not merely DPS. Base<rank has literal focused boundary tests rather than illegal negative-input oracles.

For one-cast execute Death with IF active in both runs, explicit candidate crit C>=25 is compared with archived old crit **C-25**. Their effective Death chances agree; only the public `spellCritPercent` input-derived scalar is shifted25 for complete-envelope comparison. This oracle is not used for mixed spells or repeated Death after IF consumption. Separate literal0/15/30 and Blast/free-cast controls prove the eligibility boundary.

The current affected corpus contains96simulation plans (six native reports, three90s golden cases,48Focus rank/base/gear/seed cases,36one-cast IF Death cases and three unchanged controls) plus80preview/native-observation plans. All176candidate envelopes match complete old-runtime oracles with exact Decimal numbers. Six accepted native report envelopes and all32accepted Shadow HTTP observations were first independently reproduced under their original JS-parsed number boundary; native artifacts cannot preserve extra Decimal digits already rounded by JavaScript. Only source-generated singleton `RotationProfile.Id` and the established anchor-aware timeline ID bijection are renamed; no result fields are dropped. All three original C#golden strings were reproduced exactly before the new strings were derived from old base78/rank5 responses.

Focused requests, raw old/oracle/current responses, hashes and comparison receipts are retained under `.task-evidence/shadow-focus-20261112`. This bounded corpus is precisely counted, not represented as the lost627 originals.

## Final local verification

The strict Release rebuild completes with zero warnings/errors and **8,004/8,004 tests** (no failures/skips). All60 aggregate commands pass, including existing JS/Python/contracts/importer checks and the touched browser script syntax. Full source/runtime custody is recorded separately from compiler informational version.

**412 unaffected backend controls** pass against accepted CI and between archived/candidate runtimes:128native report response envelopes (114single-run and14complete iteration distributions/representatives) plus284legacy nonmetadata responses (18defaultGETs and266POSTs, including34expected400rejections). Six metadataGETs are inventoried, not counted as replayed. These verify entire backend response/distribution envelopes, not locally regenerated browser wrappers or DOM.

After the final strict rebuild, all176affected API envelopes are freshly recaptured and again match their archived old-runtime oracles. The prior candidate binaries/PDB/runtimeconfig remain byte-identical; the only runtime-file difference is900`Last-Modified` header timestamps in the static endpoint manifest, fully enumerated rather than silently ignored. `Program.cs` uses `UseStaticFiles`, not `MapStaticAssets`. Frozen unaffected-control runtime equivalence is bound explicitly; no CI binary-identity claim is made.

For each of the six affected native reports, a complete expected report projection is retained. The same projection first reconstructs every original accepted report field from its old server/preview witnesses; the new projection from candidate API data then agrees with the old-B-r oracle. This verifies all repeated combat-summary/preview fields and leaves build, warnings, attribution and admission unchanged. These files are **expected artifacts, not actual fresh browser exports**. Final CI must compare complete envelopes with only explicitly bound metadata source/checkout commits and established generated identities.

## Remaining acceptance gates

Independent final review and coordinator-owned exact-new-head CI/native acceptance remain separate gates. Local browser routes previously blocked are not retried. No merge, deployment, release, extra repository/service or permission change is authorized.

### Exhaustive accepted-artifact partition and additional prepared packets

Independent review of all2,097 accepted ZIP files found a previously uncovered family:49 standalone `prepared-extra-classes` packets carrying Shadow request identity, including one conflicting Rogue wrapper containing a Shadow payload. These are now covered separately, not folded retrospectively into the176affected or412unaffected counts. The complete partition confirms six full Shadow reports,32HTTP observations and49standalone response packets;33other Shadow files are input-only build/import/rejection/roundtrip fixtures and must remain unchanged in finalCI. Matching is by parsed request identity, not substrings such as Rogue Shadow Dance.

All49packets reproduce archived baseline fidelity. Their13valid responses match complete authored old-runtime B-r oracles on the frozen current runtime;36rejections retain status, schema and complete error bodies. Two valid strict-dispatch/replay envelopes change numerically; the other11valid packets remain numerically unchanged under their cap/seed scenarios, but all13have independent oracles. Source-grounded routing uses the actual harness's typed `/api/sim/shadow-priest/run` and explicit unified dispatch paths, including the conflicting-wrapper rejection, not filename inference.

Five automatic ApiController400ProblemDetails contain request-generated W3C`traceId` values. Comparisons require the exact ProblemDetails key set/status/error structure plus nonzero, valid, singleton trace/span identity before renaming **only** that transport identifier; no error fields are dropped. Accepted native packet comparisons use their JS-parsed number boundary; archived/current raw API comparisons remain exactDecimal.

Additional evidence contains62archived responses (49fidelity+13oracles),49current responses and complete expected next-CI packets, preserving request/schema/scope/status. FinalCI must explicitly bind their `sourceCommit` to its verified head and compare every other field under the stated generated-identity rules. All111response/request hashes and runtime identity were reverified without simulation-source changes. Exhaustive partition receipt SHA256 is `58b76092f133b3dbf18b0c5dd053be67f10491a0a0ed2f660b227ad4f578e6ab`; supplementary acceptance is `.task-evidence/shadow-focus-20261112/additional-packets/ACCEPTANCE.json`. Final native acceptance remains pending; no additional browser execution is claimed.
