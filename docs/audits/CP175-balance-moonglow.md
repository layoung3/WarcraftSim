# Balance Moonglow rank and Mana-cost composition

## Source-backed contract

Pinned historical source: `gunba/wow-forever-sim@1cc5ae1e9abdbf941b7326d2d90421822dce31ba`.

- [Tooltip](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/druid.json#L74-L95) and [talent implementation](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/druid/talents.go#L89-L105): Moonglow reductions are **0%, 8%, 17%, 25%**, not linear interpolation of 25% across three ranks.
- [Wrath](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/druid/wrath.go#L74-L82) sets the Improved Wrath cost multiplier; Moonglow subtracts from that same multiplier. The [core cost pipeline](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/core/spell.go#L726-L743) and [Mana affordability/debit implementation](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/core/mana.go#L349-L376) carry the resolved cost into execution.

With base cost 120, Improved Wrath 5 and Moonglow 3, Wrath costs **30 Mana**, replacing the local 45: `120 * (1 - .50 - .25)`. Its costs at Moonglow ranks 0–3 are 60, 50.4, 39.6 and 30. Eligibility remains exactly Wrath, Starfire, Moonfire, Insect Swarm and Hurricane; Faerie Fire is excluded. Explicit custom and zero tuning remain supported. Unaffected rank-zero and rank-three decimal paths are preserved.

The read-only audit and retained source receipts are under `.task-evidence/next-balance-moonglow-20261112`; audit SHA-256 is `ebb83b5279037a8a5697af31da1f6e90566320ecb50af481a15c0f508e263178`. Reported Git-blob consistency is not independent commit-tree authentication. This is historical/provisional parity, not current-server certification.

## Bounded implementation and regression evidence

Only `ForeverBalanceDruidDps.cs` changes production behavior. No shared resource engine, admission, UI, spell damage/timing, Naturalist, Vengeance or Omen policy changes are included.

The finalized 101-case Moonglow suite uses independent literal tables across all Improved Wrath and Moonglow ranks. It covers actual affordability/debits/resource reports, custom/zero tuning, absent/zero and dictionary-comparer semantics, prepared instant/channel and existing unprepared timing, misses/unfinished casts, strict legal partial allocations and negative admission, plus repeated/concurrent request isolation. Omen restoration is tested through the real processor in a controlled no-reproc interval, supplemented by actual runner free/paid casts and Omen-enabled concurrency. Identical finalized assertions produce **64 failures/37 passes on old production**, then **101 passes on corrected production**.

Final local verification passes **7,546 tests and 47 aggregate commands**, including 29 Node suites. One owned final API lifecycle verifies 169 complete response envelopes, five existing HTTP clients and 290 retained legacy replay rows, then exits normally with unchanged source/runtime hashes. Independent local review passed. Evidence and failed attempts are retained under `.task-evidence/balance-moonglow-20261112`.

## Causal changes and preserved boundaries

Twenty-eight frozen old-runtime oracles set only `WrathMana = 80`, giving old `80 * .75 * .5` the corrected default cost of 30. They explain complete changed rank-three/rank-five envelopes, including all 12 CP138 combinations, preparation snapshots and five affected native Balance report requests. Other ranks rely on separate literal/runtime tests; the oracle is not extrapolated to rank-one/two rounding. Generated event identities are compared bijectively, with every other response field retained. API replays are not fresh browser reports.

CP138 and Naturalist's obsolete cost assertions change only from 45 to 30. Two exact preparation snapshots use expectations derived from the frozen old-runtime oracles, with full timeline-enabled counterparts verified. Cheaper Wrath can change adaptive choices, proc/potion timing, damage and finite-health outcomes; no universal DPS gain or unchanged timeline is claimed.

The CP167 Innervate fixture's immediate default potion could keep cheaper Wrath above its 35% activation threshold for the whole encounter. Exact-decimal original-request old/current/oracle responses establish causality. Disabling potions symmetrically isolates Innervate while retaining its aura and ending-Mana assertions and adding zero-potion and reconstructed recovery checks. No cooldown behavior changes.

Hurricane's explicit local base cost of 850 versus pinned 1495, its cadence/coefficient differences, and existing Omen approximations remain separate and provisional. This documentation is a post-test provenance supplement, separately accounted against the immutable tested-source manifest. **Publication and exact-new-head GitHub Actions/native-browser acceptance remain pending.** No merge, deployment or overall project completion is implied.
