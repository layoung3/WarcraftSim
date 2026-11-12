# CP175 — Shaman core talent execution

## Starting point and handoff

This cohesive pass continues `cp175-shared-character-simulation` / draft PR #28 from
`2253a005129a5c21c129336d1125e8982dfa22e7`. Its exact-head Actions run
[43198739363](https://github.com/layoung3/WarcraftSim/actions/runs/43198739363)
succeeded. The supplied November 12 handoff identifies Shaman as the next
unimplemented milestone; the Shadow Focus / Inner Focus correction is already
published and is not reimplemented here.

The handoff's current checkout is tracked-clean with no unpublished source. The
older checkout's eight tracked edits and three untracked source files were
independently reconciled to Enhancement integration commit
`8fbc39a28ea7c214d394e4952af79d35f6e092ad` and its descendants. They were not
reapplied. The original archive, inventories and historical patches remain
preserved. The 19,034 evidence files and six caches inventoried in the current
checkout are not unpublished implementation; their full contents were not
included in the handoff archive.

## Source and implementation contract

The numerical reference is the historical upstream implementation at
[`gunba/wow-forever-sim@1cc5ae1e9abdbf941b7326d2d90421822dce31ba`](https://github.com/gunba/wow-forever-sim/tree/1cc5ae1e9abdbf941b7326d2d90421822dce31ba).
It is **historical/provisional**, not current-client or current-server certification.

| Talent | Executable behavior in this pass | Explicit boundary |
|---|---|---|
| Trial by Fire, ranks 0/1/2 | Fire Nova damage multiplier 1 / 1.10 / 1.20; cooldown 10 / 8 / 6 seconds. Multiplies separately from Call of Flame. Retains Mana cost, target limit, and the existing owned Fire totem requirement. | The source also extends Magma Totem duration; this runner has no Magma Totem, so that mechanic remains unavailable and the talent is labeled partial. Enhancement does not gain Fire Nova or Trial by Fire admission. |
| Elemental Devastation, ranks 0/1/2/3 | Owned direct harmful spell criticals grant 0 / 3 / 6 / 9 percentage points of melee critical chance for ten seconds. A later qualifying critical refreshes the window without stacking. | Periodic/channel damage, Fire Nova, Overload, totem proxy damage, weapon imbues, Lightning Shield, melee and foreign actors do not trigger it. No static or spell-critical bonus is added. |
| Mindfulness, ranks 0/1/2/3 | Retains exactly 0 / 17 / 33 / 50 percent of the existing Spirit regeneration rate while inside the five-second rule. | MP5 and out-of-rule regeneration are unchanged. The existing continuous regeneration approximation is retained; no healing runner or discrete Mana tick system is changed. |

References:

- [`sim/shaman/talents.go`](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/shaman/talents.go), Git blob `bf5f6513aff5080f061c31334d099e6b813f02f7`: Mindfulness rank table, ImprovedFireNova helpers, and Elemental Devastation trigger/aura.
- [`sim/shaman/fire_totems.go`](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/shaman/fire_totems.go), Git blob `968a7a57315c2108b35d11dfe92cd34d5b7cc0a7`: actual cooldown/damage consumption and active owned Fire totem gate.
- [`ui/core/talents/trees/shaman.json`](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/shaman.json): Trial by Fire maps to `improvedFireNova`; Elemental Devastation has three ranks in the second Elemental row. Existing full-parent rank validation remains the disclosed local convention.

The pinned source's Fire Nova rank-5 base range differs from the existing local
70532 range. This pass deliberately changes the evidenced **talent modifiers**
and retains the local 239 midpoint, .143 spell-power coefficient and 375 Mana
cost. That numerical-source discrepancy is separate from the missing talent
execution and is not resolved by passing software tests.

## Runtime, admission and user-visible behavior

Both existing Shaman runners register the same per-run Elemental Devastation
processor. It maintains an owned aura through the existing aura manager. The
Enhancement melee roll reads its live state; `IsActiveAt` excludes the exact
expiration timestamp, even before a queued removal callback. Refreshes cannot
be removed by an older expiration event. Requests and precombat stats stay
isolated. The existing same-time event-dispatch order is retained: a proc does
not retroactively affect a melee roll already resolved at that timestamp.

Elemental can report an admitted Devastation proc, but its current pure-caster
rotation has no melee attack to benefit from the buff. This is an explicit
scenario limitation, not an added spell-critical bonus. Enhancement records
actual aura applications and a selected-talent proc counter in readable results
and the full report.

Enhancement now admits sixteen exact historical keys. Its default remains
**0/36/0 with 15 unspent**. The existing optional 15/36/0 allocation remains
legal. A 13/36/0 witness selects Convection 5, Concussion 5 and Elemental
Devastation 3 alongside the existing Enhancement ranks: 49 allocated, two
unspent. This is a testable allocation, not an optimized preset or a claim of
complete class mechanics. The UI supports selecting/removing it, keeps the
precombat critical percentage unchanged, reports the source/confidence and
preserves import/export, report comparison and deterministic replay. Invalid
ranks, missing earlier-row points, mismatched maps and excessive point budgets
remain rejected.

## Validation

The unchanged starting tree was independently built in Release with warnings as
errors, then passed **8,004 .NET tests**, with zero failures or skips. Its
downloaded 2,097-file report artifact matches GitHub's SHA-256
`23aba06a65f59e2c5b6b32374f4930a3eb0b301a284dc619e6a8d877e0188485`;
the native summaries report 32 Shadow and 23 Enhancement gates and the Shadow
report binds to the starting commit. These are existing baseline receipts.

New regression coverage checks every touched rank, independent source equations,
actual Fire Nova damage/cadence/Mana/targets, automatic and explicit priorities,
Spirit/MP5 separation, proc ownership/exclusions, refresh and exact expiry,
actual melee benefit, unchanged spell rolls and precombat stats, legal mixed-tree
admission, removal, request nonmutation and replay. The existing Enhancement
HTTP/browser gates now exercise the admitted talent, actual proc receipts,
build/report roundtrips, rejected-import rollback and restoration of the
rank-zero gameplay response.

The local environment uses .NET SDK 10.0.404 / runtime 10.0.15. Parallel MSBuild
worker IPC is denied here; supported `-m:1 /p:UseSharedCompilation=false
/nr:false` options allow the strict build. Local Chromium aborts before opening
a page because its required socket operation is denied. Local backend, HTTP and
Node evidence therefore must be distinguished from native browser evidence
produced by the existing exact-head GitHub workflow. No local browser pass is
claimed.

The frozen candidate passed these local gates before publication:

| Gate | Observed result |
|---|---|
| Full strict Release rebuild | Zero warnings and errors; all 1,159 source inputs unchanged before build, after build and after tests |
| Complete .NET suite | **8,113 passed, zero failed or skipped**, including 109 additional cases over the starting tree |
| Focused Shaman regression suite | **207 passed**: 51 Devastation, 44 Fire Nova/regeneration and 112 strict Enhancement cases; these are included in the full suite |
| Frontend contracts | **27 Node suites passed**, plus eight JavaScript syntax checks |
| Python acceptance/importer entry points | **359 passed, three optional retained-evidence checks skipped**, out of 362 discovered cases |
| Actual local API acceptance | **126 passed**: 38 strict Enhancement and 88 UI/HTTP parity checks |
| Native browser | Local launch blocked before page creation; acceptance remains the existing exact-published-head Actions browser pipeline |

An earlier incremental test assembly still contained the old fifteen-key
expectation. Its PDB checksum proved it was compiled from the original source;
a forced full rebuild produced the current sixteen-key checksum and the green
8,113-test result above. Earlier failed logs remain retained. Python totals use
the repository's normal per-file CLI entry points: generic discovery also
collects retained-artifact tests whose required inputs are normally selected by
those entry points, so that earlier unsuitable invocation is not a pass.

The ordinary manual browser fixture was independently exercised through the
actual API at seed 130001 for Orc, Tauren and Troll. It produced owned
Devastation aura receipts, unchanged precombat stats, exact deterministic replay
and complete rank-zero restoration. Those are API results, not browser exports
or proof of a universal DPS gain. The native gate adds actual talent selection,
readable aura rows, build/report roundtrips, rejected-import rollback and two
screenshots. Published-head CI and pixel acceptance belong in the existing PR's
verification receipt once observed; this pre-publication record does not claim
that future result.

No new branch, PR, deployment, release, merge, paid service or artifact deletion
is part of this milestone. Full cross-tree class parity, broader equipment
coverage, tanking/healing workflows and unresolved numerical provenance remain
separate work.
