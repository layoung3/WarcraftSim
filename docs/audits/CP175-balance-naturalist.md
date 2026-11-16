# Balance Naturalist: historical rank and stacking correction

## Scope and source

This stage follows accepted Thousand Cuts head `f81780091287dc7d9ca4ea59aed8dcaf9102ad45` and changes two Balance-only damage inputs. No rank/admission, default allocation, preset, UI, scheduler, dependency, Moonglow, Vengeance or Eclipse behavior changes are intended. Shared Feral/Restoration Thorns code and callers are unchanged.

Both the [Naturalist tooltip](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/druid.json#L1079-L1112) and [same-pin implementation](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/druid/talents.go#L488-L495) specify 1% all-damage per rank, up to five ranks. [Moonfury](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/druid/talents.go#L308-L319) independently multiplies Nature/Arcane damage. [Attacker scaling](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/core/spell_result.go#L704-L717) multiplies global and school factors.

Seven decoded files, complete serialized connector results, SHA-256 and matching reported Git blob IDs are retained in `.task-evidence/next-balance-naturalist-20261112`. Recommendation SHA-256: `1199e188ee4bb65f0ecbb3fb7c51ef1727d98bc1cda61d6904dfe4075d44d051`. Blob consistency is not independent commit-tree authentication. This is pinned historical/provisional evidence, not current-client or current-server certification. Inspected upstream tests do not independently assert this rank curve.

## Correction

The six authored components (Wrath, Starfire, Moonfire impact/tick, Insect Swarm tick and Hurricane tick) now use `(1 + .02 * Moonfury) * (1 + .01 * Naturalist)`. Hurricane secondary hits inherit the authored tick value. Naturalist absence/zero skips the neutral multiplication to preserve existing decimal serialization as well as numeric results. Dictionary comparer behavior is unchanged.

Balance's optional Thorns input uses `(existing base + existing coefficient * spell power) * (1 + .01 * Naturalist)`. The [pinned Thorns implementation](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/core/buffs.go#L847-L880) has attacker modifiers enabled. Only Balance wiring changes; shared reactive eligibility, aura checks, positive-hit requirements, living-target checks, overkill and threat remain existing behavior.

## Explicit limitations

The local Thorns raw base is **45**, whereas this historical pin uses **18**, with its own Improved Thorns term. Local missing **Moonfury** Thorns scaling is also a separate discrepancy. Neither is repaired here. Thus 47.25 at zero spell power and 60.48 at 210 spell power are Naturalist regressions using existing local tuning, not certification of complete Thorns accuracy. Shared Thorns also retains its existing lack of an ActorDied notification after lethal reflection; health clamping and the living-target gate remain tested without repairing that callback here. The legacy unprepared timing provider retains its one-second minimum for otherwise instant spells. Existing provisional spell coefficients, Omen approximation, generic periodic resolution and previously deferred callback limitations remain unchanged.

Strict Feral continues rejecting Naturalist, including explicit zero. Direct/legacy CP138 allocations do not imply strict historical legality. New strict fixtures use actual named partial allocations: Improved Wrath 5, Nature's Focus 5 and Naturalist r, with exact 5/0/(5+r) totals and matching typed/full maps; no filler points are invented.

## Verification design

Independent oracles are raw100 → 100/101/102/103/104/105; Moonfury5 plus Naturalist5 →115.5 rather than120; and Wrath91 at zero spell power →105.105 rather than109.2. Tests cover six components, inherited Hurricane secondary hits, spell power, actual debit/timing, crit/miss, zero/custom tuning, threat/overkill, original rank-map comparers, input isolation, concurrent reuse, CP138 builds and legal strict dispatch. Existing default serialization snapshots remain intact.

The accepted source/runtime and complete old raw responses were frozen before production changes. New focused tests first produced genuine numerical failures against unchanged production. Evidence is retained in `.task-evidence/balance-naturalist-20261112`; failed harness attempts are retained separately from simulator correctness results. Complete-response comparisons must account for every change using damage formulas and actual health/threat consequences, not blind rebaselining or indiscriminate numeric normalization.

Full local aggregate, HTTP preservation, independent review and exact published-head GitHub Actions/native-browser acceptance are separate gates. This document does not claim those gates before their receipts exist, does not claim a local browser run, and does not authorize merge or deployment.
