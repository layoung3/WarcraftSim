# CP175: isolated Feral preparation and bounded Predatory Strikes correction

## Scope and contract

This milestone contains two independently verified local stages on the existing
CP175 branch. It does not admit Feral into strict unified simulation, add an HTTP
endpoint or preset, or claim a complete Feral stat sheet/talent implementation.
The 36-point candidate remains unadmitted metadata. Previously accepted Savage
Fury, Sharpened Claws and finisher/workflow repairs are preserved.

`FeralDruidSimRunner.Preview` and `Run` now share a private preparation path. Each
call snapshots the input graph, preserving talent and initial-combo dictionary
comparers, insertion order, explicit zeros and invalid nulls. Validation order
remains structural request, talent validation, tuning validation, impossible
Ravage/Prowl, initial-shift affordability, then spellbook creation. Null runner
inputs still name the `request` parameter. The actual per-call spellbook and
configured rotation are used by Run. Preview returns its own graph; mutating its
nested effects, costs, aura requirements or rotation targets cannot affect other
calls. No combat or miniature simulation is run for Preview.

The display-only initial boundary reports authored-inclusive AP, the HOTW AP
increment, separate Predatory AP, final spellbook AP, maximum Health/Mana,
mandatory shift cost and post-shift Mana. It shows Energy maximum 100/start 0
and legacy Rage maximum 100/start 0 Cat or 20 Bear. Configured entries are not
immediately eligible actions. Shifting Power may execute at time zero after this
boundary. Base Strength/Stamina/Intellect values remain incremental conversion
inputs, not a complete effective character sheet.

## Stage 1: preservation before correction

Accepted source was `bc2c08724f66031502d4ad297dc7e9306c42da55`, tree
`f3a96ab96e43ea736c070c2d43353b5e41645248`. Preexisting API binaries were retained
with their actual `f6443d6389181fd9fd1e44c4c0db9ca9568833b7` informational stamp;
then a strict serial rebuild of the accepted source produced and froze a
`bc2c087...` stamped binary before implementation edits. This distinction is
recorded rather than relabeling old binaries as exact-head builds.

The old binary captured 272 request/response pairs: original Predatory ranks
0–3 plus rank-zero authored-AP counterfactuals. Cat/Bear, HOTW 0/5, crit 0/100/31
(or 23 for the base matrix), zero inputs, armor/Faerie Fire, Prowl, target ownership,
mixed forms, Savage Fury/Berserk, Natural Instinct, boss/tank effects, zero hit,
low-health deaths and current finisher controls are retained in full.

Before the talent correction, strict build passed, all 479 Feral tests passed,
and all 272 whole-response preservation comparisons passed. This includes the
four existing immutable rank-zero/HOTW fingerprints and existing finisher gates.
The preservation source, binaries, hashes and diff were frozen separately.
Initial missing-namespace compiler failures and a stale prior-DLL test run are
retained but excluded from acceptance. Three initial preview-test failures were
fresh rotation GUID comparisons; normalization was corrected without changing
runtime identity generation. These are not claimed as mechanics red evidence.

## Stage 2: historically sourced Predatory Strikes

The pinned source and local decoded-receipt hash are in
[CP175-feral-precombat-source.json](CP175-feral-precombat-source.json). Source:
[gunba/wow-forever-sim, pinned Druid tree](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/druid.json).
It says 50/100/150 percent of level as melee AP in Cat, Bear and Dire Bear.
This is historical/provisional decoded connector evidence, not independently
verified upstream raw bytes or proof of current client behavior.

For this level-60 runner, add a flat 30/60/90 AP **after** initial-form HOTW and
remove the unsupported Predatory critical-damage term. The existing consumers
are Claw, Shred, Rake impact/ticks and Swipe. No coefficient or consumer is
changed. White swings, Ravage, Rip, Ferocious Bite, Maul, Primal Bite, Pounce and
Lacerate still ignore AP. Their critical damage loses the unsupported multiplier;
without Natural Instinct, crit is 2x at every Predatory rank. Natural Instinct
and bleed snapshots remain independent and unchanged. All existing AP consumers
require animal-form auras, so the same flat bonus needs no dynamic recalculation.

Independent no-HOTW/no-Savage/no-armor oracles at authored AP800/weaponDPS75:

| Rank | Spellbook AP | Claw | Shred | Rake impact | Rake tick | Swipe |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | 800 | 295 | 468.75 | 120 | 64 | 114 |
| 1 | 830 | 299.5 | 471.75 | 121.5 | 64.9 | 114.9 |
| 2 | 860 | 304 | 474.75 | 123 | 65.8 | 115.8 |
| 3 | 890 | 308.5 | 477.75 | 124.5 | 66.7 | 116.7 |

Default HOTW5/Predatory3 gives Cat AP926/Health5500 and Bear AP890/Health5940;
both have Mana4270 and post-shift Mana4020. Explicit authored AP0 remains zero
in the input and receives the selected talent's flat bonus. If authored AP
already includes Predatory, remove that manual bonus: selecting the talent
otherwise double-counts it. There is no heuristic adjustment.

The legacy `feral-heart-of-the-wild-attack-power` metric deliberately remains
HOTW-only. Preview's `SpellbookAttackPower` is the final total. Forced-critical
DPS can correctly decrease after removing the invented modifier; aggregate DPS
increase is not the acceptance oracle.

## Causal evidence and remaining limits

The pre-correction binary with Predatory0 and authored AP increased by 30/60/90
predicts corrected combat. Compare complete summaries, resources and ordered
events using exact decimal values and stable generated-GUID identity mapping.
The counterfactual's HOTW-only AP metric is higher by the injected AP. Assert
that one named delta, then reconcile it before comparing; do not claim literal
whole-response equality without that qualification. Low-health death cases are
separate controls: they are compared against the matching counterfactual, not
assumed to retain the original old-talent event schedule.

Full AP coefficients, mixed-form HOTW recalculation, lower-rank probabilistic
Furor and validated baseline/form/talent observation semantics remain visible
limitations. The current mixed-form HOTW leakage is preserved, not corrected.
This milestone neither changes CI/security/workflows nor certifies full Feral
mechanics or strict-mode eligibility.

Local evidence lives under
`acceptance-target-semantics-cloud-20261110/feral-precombat/`; historical evidence
is retained. The reusable loopback harness is `tools/feral-precombat-http.py`.
Run `--phase freeze` only against preserved pre-correction binaries; then
`--phase preservation --baseline <frozen-directory>` or
`--phase correction --baseline <frozen-directory>`.
The source/causal receipt and final verification section below bind acceptance
to local file hashes rather than an unpublished commit claim. Exact-head Actions
browser acceptance remains the publication gate owned by the coordinator.

## Final local verification

The final working tree passed strict Release build with zero warnings/errors,
all **5307 .NET tests**, **126 Python tests**, importer smoke and **15 pure Node
suites**. Focused Feral coverage is 698 tests, including 27 preparation tests
and 219 new Predatory/source/consumer tests. The separate correction red run
compiled successfully: 113 genuine mechanics failures, 100 passes and four
excluded white-swing fixture failures (the duration ended before the first swing).
The corrected fixture uses 1.1 seconds; no production timing was changed.

HTTP gates passed: prepared casters, prepared extra classes199, Enhancement
strict23/UI45, Rogue Expose Armor72/UI53, Feral standalone56 and optional frozen
replay81. All 272 final causal comparisons passed. The first combined HTTP pass
hit a static-asset404 because ContentRoot was the repository directory; the
complete `final-r2/` pass explicitly used `WarcraftSim.Api` as ContentRoot and
passed. This setup failure is retained, not represented as a gameplay failure.

A forced-crit Cat rank3 case decreased from damage12421.2 to11457.6 while its
white crit changed165 to150. This is expected and supports the bounded causal
correction, rather than an incorrect requirement that aggregate DPS always rise.

[The durable receipt](CP175-feral-precombat-receipts.json) records tested file
hashes, accepted binary lineage, phase manifest hashes, representative exact
fingerprints and the one named HOTW-metric reconciliation. No merge, deployment,
push, PR metadata or CI/security change was made in this local milestone.
