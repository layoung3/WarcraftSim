# CP175: existing Feral workflow and validation

## Bounded behavior

This stage repairs three defects in the existing Cat/Dire Bear preview runner.
It does not add a simulation mode, strict admission, UI, equipment support or new
coefficient assumptions. Historical talent and base-damage confidence remain as
stated in [the sourced-rank audit](CP175-feral-sourced-ranks.md).

The fixed Cat priority maintains Rip first when the current target has at least
four combo points and no active Rip. While that target's Rip is active, at least
four points instead enable Ferocious Bite. Existing priority order, source-aura
requirements and current-target bleed-source convention are unchanged. Points
remain separately owned by each target. Prowl disables both finishers. Rip's
existing strict active-duration test flips eligibility at expiration, while its
permitted expiration-boundary tick retains the original snapshot.

This is an explicit automatic-preview policy, not an optimal rotation. It adds
no Energy pooling, refresh threshold, execute timing or end-of-fight fallback.
A short fight with no Rip still selects Rip even if no tick can land. Events
exactly at encounter end remain excluded by the existing engine contract.

An initial Cat request with StartInProwl, preferred Ravage and TargetBehind=false
now fails with an actionable ArgumentException, returned by the existing
controller as HTTP 400 JSON with an error string:

> Ravage from Prowl requires TargetBehind=true. Choose Pounce or disable StartInProwl for a front-facing Cat simulation.

Valid behind-target Ravage, front-facing Pounce, non-Prowl Cat and Bear's unused
opener settings remain valid. A later target/form transition does not make an
impossible initial opener valid. Existing case-sensitive role/opener values and
prior structural/talent/tuning errors are preserved.

Initial shift affordability now compares the existing derived Mana against the
actual ForeverDruidFormState.ShiftManaCost before creating the engine. The
inclusive-resource formula is evaluated once: ordinary Intellect Mana is not
added again. Authored Mana 200 plus HOTW 5 at default Intellect 180 adds 270, giving
470 effective Mana. Cost 250 leaves 220; cost 470 succeeds at equality; cost 470.01
now returns 400 instead of an uncaught 500. The error identifies effective Mana,
Heart of the Wild, MaximumMana and Tuning.ShapeshiftMana. Free shifts remain
valid with positive authored Mana. Later unfunded scheduled shifts still count
as failed shifts rather than invalidating the request. The defensive initial
shift exception remains; there is no blanket InvalidOperationException catch.

## Finisher execution ownership

Making Bite reachable exposed two related execution-order defects. Direct
zero-travel damage resolves inside AbilityExecutor before the Feral processor
receives cast completion. A gate-only patch therefore produced Bite 14 for 2000
using the preceding five-point Rip, rather than the current four-point 1600.
Likewise, spend events are queued: the old completion handler read the previous
finisher's Energy spend. The first paid Rip miss refunded 0 instead of 24, and a
later free miss could inherit a prior paid cost.

The correction stays inside Feral. Its direct roll adapter captures target-owned
points and the just-paid combined cost under the current execution ID before
hit/miss resolution. It requires matching source, target and ability, completed
noncancelled execution and capture-once ownership. The same shared cost resolver
is used while the cost definition is still unchanged, before clearcasting is
removed. Completion consumes only a captured execution's points. Bite reads that
snapshot; Rip retains its independent bleed snapshot. Direct hit/miss resolution
removes pending state, and duplicate miss dispatch cannot restore points twice.
No shared executor ordering, RNG consumption rule or scheduler was changed.

The existing provisional 80% refund is retained: paid Bite 35 refunds 28 (net 7),
paid Rip 30 refunds 24, and a free miss refunds 0. These are compatibility oracles,
not independent current-client certification of the refund rate.

## Test-first and causal evidence

Base source HEAD: f6443d6389181fd9fd1e44c4c0db9ca9568833b7. The pre-edit API
binary has SHA-256 3cadb0442199fb81a2a70f3667b78821f523d7700a3d8ac9e5338f7546bed27c
and retains its original 5536d000 informational stamp. The frozen source manifest
and 25 request/response captures were saved before production edits: 17 successful
runs with timelines and eight initial-shift error responses. Changed-tree binaries embed the base f644 stamp, not a new published SHA.
[The compact causal receipt](CP175-feral-workflow-receipts.json) records both
lineages, hashes, replay comparisons and test results. Detailed local evidence
is under acceptance-target-semantics-cloud-20261110/feral-workflow-fixes/.

- Pre-edit 105 cases: 64 passed, 41 failed. Initial compiler/analyzer diagnostics
  are retained separately and are not counted as mechanics failures.
- Three gates alone: 97 passed, 8 failed, exposing stale finisher points/refunds.
- Finisher repair: 433 focused cases passed, including all 322 sourced-rank tests
  and the six existing CP167 target-ownership cases.
- Final suite adds 110 workflow cases. It covers CP 0–5, Rip expiration, Prowl,
  Cat source-aura requirements, target switches, another target's bleed,
  source-convention preservation, snapshot/armor/zero-damage behavior,
  affordability, clearcasting, paid/free/repeated misses, duplicate events,
  invalid completion, end boundaries, opener miss continuation, HOTW 0–5,
  free/equal/underfunded shifts, casing, controller errors and concurrent caller
  reuse without mutation.

The forced-crit fast fixture independently derives Rip 3, Rake 6.5, Claw 10.5,
Bite 14. Claw leaves four points and 0 Energy. At 10 Energy/sec, Bite's 35 cost becomes
affordable at 14; no scheduler adjustment is needed. The Bite deals 4 * 200 * 2 = 1600.
All six original Rip ticks at 5/7/9/11/13/15 remain 450 under the original five-point
execution. The timeline before 14 is unchanged. Later actions and random-roll
consumption can differ; total damage moves 17166 to 17792, not simply old damage
plus Bite. All four previously pinned rank-zero Cat/Bear/HOTW fingerprints remain
unchanged. No existing golden was replaced.

## Local acceptance and remaining gate

Strict .NET 10 Release passed with 0 warnings/errors using single-node,
non-shared compilation. All 5061 .NET tests, 126 Python tests, 15 pure Node suites,
prepared-caster/isolated importer smoke and existing HTTP 199/72/23/45/53 gates
passed. The tracked loopback HTTP gate tools/feral-workflow-http.py passes 56
POSTs without any local baseline files. It includes real controller binding,
error payloads, HOTW boundaries, first-miss refunds, concurrent runs and an
explicit synthetic fast-Cat fixture. Independent expectations require Bite at
14 for 1600 damage and 35 Energy, plus all six original 450 Rip ticks under the
original execution ID. Both standalone and optional-baseline modes use this oracle.

Run it against a locally started API with:

`python3 tools/feral-workflow-http.py --base http://127.0.0.1:5273 --out /tmp/feral-workflow`

Optional `--baseline <frozen-directory>` adds the 25 historical replays for 81
POSTs total. Only that mode reports frozen replay verification. Both modes passed
against the unchanged final API in one shell; its binary hash was verified before
and after. The original 80-request evidence is retained separately. No framework,
dependency or CI workflow change was required.

The 25 frozen replays show eight 500 → 400 changes, one impossible-opener 200 → 400,
15 unchanged successful controls and the explained Bite trace change. API
processes stopped normally. No browser suite was run locally; the existing
exact-head Actions browser suite remains required for publication acceptance.

Predatory Strikes/AP consumers, form-stat recalculation, lower-rank Furor, strict
Feral integration and UI remain separate work. No DTO/default/talent-cap/preset
changes or new tank/healing capability are included here.
