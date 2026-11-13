# Balance Thorns: historical Moonfury scaling

## Source and bounded correction

Pinned historical source: `gunba/wow-forever-sim@1cc5ae1e9abdbf941b7326d2d90421822dce31ba`.

[Moonfury](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/druid/talents.go#L308-L319) multiplies Nature and Arcane school damage by 2% per rank in Forever, including spell power. [Thorns](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/sim/core/buffs.go#L847-L880) is a Nature spell using ordinary attacker modifiers. The traced damage pipeline independently composes global Naturalist and school Moonfury. This is execution evidence, not generalized tooltip inference or current-server certification.

The read-only source audit is `.task-evidence/next-balance-thorns-20261112/RECOMMENDATION.md`, SHA-256 `cc52ea917da408280b9d14f1a58170f970218629f2b05d6e2729f3baa3472309`. Its manifest and reused-source receipts retain decoded bytes and reported Git-blob consistency, not independent commit-tree authentication.

Only `BalanceDruidSimRunner` production behavior changes: its optional Thorns caller multiplies the existing complete base-plus-power/Naturalist value by `1 + .02 * Moonfury rank`. Zero/absent ranks skip multiplication, preserving decimal serialization and dictionary-comparer semantics. Shared `FeralThornsProcessor`, Feral caller, six already-scaled Balance components, Vengeance, admission, defaults, UI and timing remain unchanged. Restoration has no Thorns wiring.

## Numerical and preservation contract

Literal SP0 rank0–5 values are **45, 45.9, 46.8, 47.7, 48.6, 49.5**. At SP210/coefficient .06 they are **57.6, 58.752, 59.904, 61.056, 62.208, 63.36**. Naturalist5/Moonfury5 gives **51.975** or **66.528**. Custom12/.2/SP10 gives **15.4**, or **16.17** with Naturalist5. Zero base with positive power is covered separately from fully zero input.

The same finalized 60-case test file produces **34 failures/26 passes on old production**, then **60 passes on corrected production**. Actual prepared/unprepared runner events, threat, clipping, strict named tier-six partial maps, admission rejections, comparer/serialization, concurrent reuse and preview isolation are covered. Prepared fixtures supply exact power through authored equipment because naked preparation replaces BaseStats. Strict Balance totals are 25+r; optional Nature's Focus5/Naturalist5 adds Restoration10.

Old source/runtime and complete API responses are frozen before production edits. Independent old-runtime oracles change only explicit Thorns base and coefficient by literal rank factors; unchanged talent inputs retain other spell behavior. This proves complete response causality even when clipping changes reflected hit counts. Final response comparison allows only enumerated generated identities and verified authored tuning echoes, not arbitrary numeric normalization. Explicit default Thorns-off, Feral and Restoration controls remain whole-response comparisons. Existing assertion values are not rebaselined.

Stage evidence, including retained attempts, source/runtime custody, full aggregate, API-client results and independent review, is kept under `.task-evidence/balance-thorns-moonfury-20261112`. Focused red/green is not a substitute for those separate gates. Publication and exact-new-head GitHub Actions/native-browser acceptance remain coordinator-owned gates; no merge or deployment is authorized.

## Deferred discrepancies

- Local provisional raw45 differs from pinned raw18 and its Improved Thorns term. Neither base nor coefficient is corrected here.
- The pinned default provider is external SP0; local Balance uses prepared self spell power. Explicit simulated upstream providers add SpellPower, SpellDamage and NaturePower.
- Upstream triggers on landed melee and uses magic-hit/resistance handling; local triggers on positive physical Damage events and directly deals Nature damage. Forever partial-resistance handling precedes the Binary early return, so Binary does not imply all resistance is bypassed.
- Local reflection emits no ActorDied and checks a living enemy but permits current dead-druid behavior. These contracts are preserved, not endorsed as full fidelity.
- Thorns cannot crit or benefit from Vengeance. Finite-health damage changes may alter later events, so no universal unchanged schedule or DPS increase is claimed.
