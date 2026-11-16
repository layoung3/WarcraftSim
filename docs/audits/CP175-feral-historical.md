# CP175: historical/manual fixed-Cat backend admission

## Input ownership

This additive backend contract adapts the existing Feral runner; it is not a
new engine, simulation mode, preset or UI. Talent legality is enforced by the
shared itemized controllers; the preparation helper alone is not strict
allocation admission. Direct Cat/Bear requests keep their
legacy authored-inclusive contract and behavior. The strict Feral payload has
its own identity and manual-input blocks. Unknown fields at each new typed
level are rejected, including legacy Tuning, Health/Mana overrides and loadouts.

The selected embedded Druid baseline supplies all five primary attributes and
pre-talent maximum Health/Mana. It must be explicitly historical-provisional,
match the canonical level-60 race/class, and have no melee AP observation.
Arbitrary inline observations and unavailable reviewed baselines are rejected.
No generic Druid Generate or ordinary Strength/Agility AP conversion is used.

Manual pre-talent total Cat AP is required, including explicit zero. It already
includes ordinary attributes and whatever Cat contribution the caller assumes.
The existing preparation adds incremental HOTW, then sourced Predatory Strikes,
exactly once. Manual AP is never represented as an observation. Weapon DPS,
hit/crit, shift cost and Shifting Power base Mana remain manual assumptions;
base Mana is distinct from maximum Mana. Ability coefficients retain existing
provisional values. No character values are inferred for a real player.

Independent Night Elf arithmetic: historical Strength 62, Intellect 100,
Health 2173, Mana 2444; manual AP 800; HOTW 5; Predatory 3; shift 250 gives
AP 800 + 62*0.10*2 + 90 = 902.4, Health 2173, Mana 2444 +100*0.10*15 = 2594,
post-shift Mana 2344. This intentionally differs from legacy 5500/4000/180 inputs,
not an equivalent migration. The nested legacy preview's authored-inclusive
label describes the converted request; the outer receipt identifies the
historical/manual contract and preserves its actual inputs.

Historical records have IncludesRacialPassives=false. Racial effects remain
unavailable and caps are explicitly incomplete: Tauren Endurance is omitted,
including its maximum-Health benefit. No source flag is rewritten.

## Source and admission limits

Primary/resource source: embedded CP173 catalog, WoWSims Classic
7779ebbf79dc7f1341e6ab939b28a3402c9a730a with the retained Forever 70707 base-Mana
derivation. Night Elf and Tauren are historical proxies; Troll/Druid is a
lower-confidence additive-offset extrapolation for a Forever-only pairing.
None is a current-client observation. The catalog contains no Druid AP value.

Talent source: [pinned gunba Druid tree](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/druid.json).
The retained receipt is decoded connector content, not independently downloaded
upstream raw bytes or a verified blob hash. Source parent pointers establish
nodes, while full-parent rank gating is the disclosed existing local convention.

The bounded 14-key candidate is 0/31/5: 36 allocated, 15 legally unspent.
Actual earlier-row Feral budgets are 10/12/19/25/30 before rows 2/3/4/5/7.
It excludes cap-mismatched Rend and Tear and Natural Reaction and does not add
filler. Exact canonical full/typed maps preserve explicit-zero presence.
Raw talent-validation JSON can detect duplicate rank keys; already-bound typed
dictionaries cannot recover collapsed duplicates. Optional named totals may be
absent; explicit null, strings and fractional totals fail typed JSON binding.
New Feral numeric inputs reject numeric strings without changing other classes. Underallocations remain legal.
Furor retains source cap 5 but admission permits only rank 0 or 5, not general
lower-rank correctness.

Fixed Cat, one full-health level 63 target, no transitions, seeded combo maps,
adds, incoming/tank effects, Bear utility or Thorns. No catalog gear/enchants,
modifiers, racial actives, combat items, custom priorities, companions or batches.
Manual armor and weapon/AP assumptions remain visible in the request/receipt.
Feral Swiftness and Furor have no active benefit in this initial-zero-Energy,
no-transition scope; their broader movement/defensive/form effects are not
certified. Natural Instinct eligibility remains provisional. The existing Omen processor
retains its fixed 6% placeholder and 10-second lockout without a selectable
Omen rank here; no talent-complete eligibility claim is made. Whether this
should be baseline or talent-dependent is unresolved; absence from the retained
tree does not settle that classification. These caveats and Natural Instinct
eligibility are exposed in adapter notes, talent notes and capability restrictions. AP consumers are
still only Claw, Shred, Rake impact/ticks and Swipe; no new coefficients are
invented for white attacks or other abilities.

Class-only Druid aliases remain Balance. No frontend, default-itemized Feral
preset, CI/workflow, permission or security changes are part of this milestone.

## Verification lineage

Local evidence is under
acceptance-target-semantics-cloud-20261110/feral-historical-backend/.
Accepted source HEAD 71086dd29bcc80fa6548dd2691558e85a4902253, tree
8d2314d0ad76628dfd0f92b7f556517f089d9a21. The preexisting DLL actually carried
bc2c08724f66031502d4ad297dc7e9306c42da55; it was preserved separately. A serial
strict rebuild of the accepted head passed with zero warnings/errors before
272 whole legacy responses were freshly frozen. Original older captures and
causal comparisons remain retained and are not reset to new expectations.

Phase one passed strict build, 217 independent adapter tests, and 272 whole-response
legacy preservation comparisons before integration. Initial nullable-flow and
missing-test-namespace compiler failures are retained separately; no tests ran
from failed builds. Phase two passed 403 focused tests after numeric-binding
hardening. One discovery-name grep stopped a shell after a successful build;
the actual discovered test name was checked before continuing that same fresh
build. This is setup evidence, not a mechanics regression.

The first new HTTP parity attempt exposed different existing transport timing:
Program.cs adds 80 ms input delay only to /api/sim/*, while the shared
/api/forever/* route has none. The retained unaligned capture gave shared damage
6158.3012 versus direct 6343.7942, with the first Rake at 1 versus 1.08 seconds.
The new gate follows the existing prepared-class gate: copy the returned direct
request, add only direct-HTTP inputLatencyMs=0, and assert exact whole-result
parity with ordered events and exact decimal values after generated GUID
identity normalization. This one transport adjustment is disclosed, not called
literal unadjusted HTTP request equivalence. No middleware or combat timing
implementation was changed; strict Feral still rejects latency overrides.
The strict result includes inputTiming.additionalInputLatencyMilliseconds and
the direct-route replay caveat, so future reports cannot imply the legacy
80 ms transport default was included.

The first full regression run passed 5711 tests and exposed one outdated
Enhancement discovery assertion that assumed only one specialization override
could exist globally. Its existing Shaman assertions stayed unchanged; the
single-item assertion now selects Shaman overrides, while Feral is tested
separately. This intentional additive discovery change is not a gameplay golden
reset. That failing run is preserved. The first narrow test edit then triggered the
xUnit2031 analyzer; using Assert.Single's predicate overload resolved it before
any further tests. Finally, representable extreme decimals are converted to an
actionable HTTP 400 on arithmetic overflow only for the strict Feral route;
there is no invented game cap or change to legacy formulas/errors.

Final `final-r5/` verification passed strict .NET 10 Release with zero warnings
and errors, all **5716 .NET tests** (409 new adapter/strict cases), **126 Python
tests**, importer smoke, and all **15 pure Node suites**. Existing HTTP gates
passed: prepared casters, prepared extra classes 199, Rogue Expose Armor 72,
Enhancement strict 23/UI 45, Rogue UI 53, and Feral standalone 56/optional replay
81. All 272 prior Predatory causal comparisons and 272 exact accepted-head
legacy response comparisons passed. The new reusable bounded HTTP gate passed
**977 requests**, including raw/typed binding failures, source/rank/row/parent
and unsupported-input gates, independent numerical receipts, full direct parity
with the explicit transport adjustment, exact replay and concurrency.
The final note-only consistency pass also verifies that preparation, talent
receipts and capabilities disclose inherited provisional Omen behavior and
unresolved eligibility, plus provisional Natural Instinct critical eligibility.
No mechanics changed in that pass; the prior final-r4 receipts and binaries
remain retained separately.

Run `tools/feral-historical-http.py --base http://127.0.0.1:5273 --out <new-directory>`
against a running API; optional `--fixture` cross-checks the retained source
allocation. The harness refuses non-loopback origins, redirects and overwriting
an existing capture directory. Its receipt explicitly notes that all fourteen
caps total 36, so a cap-valid over-51 allocation is impossible: the malformed
52-point probe is not claimed as isolated global-budget branch coverage.

[The compact numerical receipt](CP175-feral-historical-receipts.json) binds the
final tests and captures to tested source/DLL hashes. Final local binaries retain
the accepted base 71086dd informational stamp; they are not falsely described
as a newly published head. No generated full captures are committed. APIs were
stopped normally. Exact-head Actions browser acceptance remains a separate
publication gate owned by the coordinator; no local browsers were run.
No commits, pushes, merges, deployments or PR metadata changes were made.
