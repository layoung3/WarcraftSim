# Enhancement cross-tree spell passives

## Scope

The unified Enhancement workflow admits exactly three additional historical Elemental talents: Convection, Concussion and Reverberation. The twelve previously supported Enhancement entries and the original 0/36/0 starting preset remain unchanged. That preset spends 36 points and leaves 15 unspent. Optional 10/36/0 and 15/36/0 builds spend 46 and 51 actual ranks respectively, without filler. Fully allocated is not fully modeled, optimized or current-server certified. Other talents remain unavailable and are rejected rather than granted phantom effects.

## Source and bounded effects

Historical source: [Shaman talent tree](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/shaman.json), blob `ca93c9b67fc585c5aed52eabb3a90d6048fce25a`.

- Convection: 2/4/6/8/10% Mana reduction on existing Lightning Bolt and Earth Shock.
- Concussion: 1/2/3/4/5% damage on existing Lightning Bolt, its Maelstrom alias and Earth Shock.
- Reverberation: 0.2/0.4/0.6/0.8/1.0 seconds off Earth Shock cooldown.

No missing Chain Lightning, Lava Burst or other Shock is introduced. Shield, Searing periodic damage, melee and weapon imbue procs receive no new spell-passive bonus. Explicit tuning and Enhancement's existing .714 Bolt and .429 Earth Shock spell-power coefficients are retained. Existing Focus/Maelstrom cost composition and owned live Stormstrike damage composition remain provisional conventions, not independently source-certified stacking order.

Strict itemized allocations retain canonical keys, caps, earlier-row requirements, per-tree totals, exact named-rank equality and the 51-point budget. Legacy typed aggregate requests retain their distinct capacity-validation contract, including the historical 10/41/0 default; no ranks are silently granted.

## Unified workflow and verification boundary

The existing native Enhancement browser suite now exercises rank-by-rank selection, 46/51-point export/run/replay, report allocation identity, full-build import round-trip, real cap/row rejection with atomic rollback, rank removal, default restoration and visible unsupported scope. Existing manual settings, other classes and preset bytes are preserved. All browser runs remain in the existing GitHub Actions gate; this document does not assert an unexecuted local browser or exact-head CI pass. Final execution and independent acceptance receipts are recorded separately by the stage coordinator.


## Local verification: 2026-11-11

This stage follows accepted Serrated head **3978be7a9ba4513ecfc37a2c6714adde9696307c**, whose [CI 43094771706 attempt 2](https://github.com/layoung3/WarcraftSim/actions/runs/43094771706) passed. Original later local receipts lost in rollback remain unrecovered; fresh recovery/source reads and genuine CI artifacts are not described as restored original evidence.

The unchanged-production numerical baseline had 18 intended failures and two passing controls. Final local verification passes a zero-warning/error Release rebuild and **7,127 .NET tests**, including unchanged legacy timeline fingerprints, exact rank/cost/cooldown oracles, positive proc/Shield/Searing exclusion controls and failed-completion ownership. All 25 Node suites pass. The nine documented Python tool entrypoints have 230 passes and three optional historical retained-input skips; importer tests pass 126/126 and smoke checks pass.

Real HTTP checks pass **38 strict and 58 UI cases**, including 36/46/51 allocations, unchanged preview, direct/shared equal-latency execution, removal restoration and replay. Both suites reuse the existing anchor-aware oracle for complete simulation responses, preserving literal IDs, identity joins, array order and every other field. Existing Feral/Rogue/Combat HTTP checks pass; 155-case Serrated arithmetic, replay and genuine recovery-baseline preservation also pass with rejection/binder distinctions retained. The API exits normally with unchanged tested source/runtime. Failed attempts remain preserved, including the stale twelve-key disclosure assertion corrected without changing other-class expectations.

A controlled 20-second 15/36/0 HTTP example uses zero added latency, 33 Spell Power, default Shock tuning, Focus 1 and a live owned Stormstrike mark. A noncritical Earth Shock at 9.5 seconds deals **534.43782** damage for **212.85 Mana**; casts at 4.5, 9.5, 14.5 and 19.5 seconds demonstrate the five-second cooldown. The matching 0/36/0 control uses 236.5 Mana and deals 508.9884 on a noncritical hit. Removing the three passives reproduces its complete simulation response. This is a controlled result, not a universal DPS gain or optimized-build claim.

The executed candidate manifest is `f005f2972ac945b02cbc5c1fd5fe9932c11f506c4770f15c63da4cb9eec4657d`; its last change is limited to the two HTTP oracles, followed by targeted reruns. These final documentation additions are accounted separately. **Enhancement publication and exact-head GitHub Actions/native-browser acceptance remain pending.** No local browser or pixel run, current-server certification, merge, deployment or overall completion is claimed. See the [implementation ledger](CP175-implementation-ledger.md#accepted-serrated-checkpoint-and-locally-verified-enhancement-cross-tree-stage) and [acceptance index](../acceptance/cp175/README.md#accepted-serrated-and-enhancement-local-verification).
