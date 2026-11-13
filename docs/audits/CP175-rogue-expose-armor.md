# CP175 historical Improved Expose Armor refund contract

## Evidence and scope

The [pinned historical Rogue tree](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/rogue.json) describes Improved Expose Armor as reducing Energy cost by 5/10 and refunding **1/2 Combo Points when cast with five Combo Points**, at ranks 1/2. The two rank arrays are `[5,1,"",5]` and `[10,2,"s",5]`. [Preserved entry and provenance](CP175-rogue-expose-armor-source.json) retain the original description, spell IDs, cap and zero-based location. This is historical/provisional evidence, not certification of the current Forever client or full Rogue class.

The original fetch supplied decoded content in a tool wrapper. Its retained UTF-8 representation SHA-256 is `9776311caefba3f7100b3220ffeaaccfc33dd6fb280c18f21172e85c5e984e09`. That hash identifies the saved representation, not an independently downloaded raw source blob. The selected entry was parsed from the preserved full representation again for this milestone.

Only the default refund changes. The existing 25/20/15 Energy costs remain unchanged, as do five-point spend reporting, armor magnitude, target/source ownership, execution deduplication, failed application behavior and separate Ruthlessness processing. No Aggression stacking, source/cap-conflicting or absent talent keys, presets, strict admission, UI, catalog, other class mechanics, endpoints, dependencies or global serialization settings are changed by this patch.

## Public compatibility contract

`ForeverRogueTuning.ImprovedExposeArmorFivePointRefund` keeps its name and changes its CLR type from `int` to `int?`, with default `null` instead of `5`. This is a deliberate public type/default change; compiled or typed external consumers expecting a non-nullable integer may require adaptation.

- Omitted or explicit JSON `null` selects the historical rank-dependent default, giving 0/1/2 points at rank 0/1/2 after five actually spent.
- An explicit integer 0 through 5 is an **absolute** override at either positive rank. It is never multiplied by rank. Zero disables the refund; five refunds five.
- A previously exported payload containing 5 keeps its exact absolute-five behavior even if an older server inserted that value automatically. Authored and auto-inserted fives are indistinguishable. This patch does not infer historical user intent, migrate payloads, or reinterpret five as a sentinel.
- Rank 0 never refunds, regardless of override. Spending 0 through 4 points never refunds. Other finishers do not receive this refund.
- Negative or above-five integers fail tuning validation in both Preview and Run. Fractional JSON numbers, fractional numeric strings, nonnumeric strings, booleans, objects and arrays fail JSON binding.
- ASP.NET's existing web JSON settings accept integer strings such as `"0"` and `"5"`; those retain their absolute override meaning. Plain default `JsonSerializer` options do not have identical numeric-string input policy. No strict-number attribute or global binder tightening was introduced.
- Actual GET defaults serializes the field as camelCase `improvedExposeArmorFivePointRefund: null`. Newly accepted null and the changed default are intentional API behavior, not transparent preservation of every old response.

The refund is resolved locally during spending as `override ?? rank`, after the existing positive-rank/five-spent gate. Preparation and the JSON snapshot do not replace null with a number or mutate the caller. The existing nullable range pattern skips null and validates all supplied values. No converter, second mode/property, migration or snapshot rewrite is needed.

The five-point armor application still reports five points spent and reduces the provisional armor value by 5×250, even when only one or two points return. Ruthlessness rolls separately afterward and the existing point cap remains five. A missed Expose Armor still costs Energy but does not spend/refund points or apply armor; its existing aura path emits no separate damage/miss event.

## Test-first and before/after evidence

Baseline HEAD is `a2f06a90e7a4a534962189b7a8b71d26d7c6cc2a`. Before source changes, original API/test binaries and hashes were preserved and the three existing default fingerprint tests passed. All three untouched defaults disable Expose Armor and allocate rank 0. Their fingerprints remain unchanged; their lack of Eviscerate casts results from higher-priority finishers spending points, not from this refund defect. No golden was replaced.

The initial red test run contained three incorrect assertions expecting a separate miss event. Those test assumptions were corrected before production edits; the second red run then had 27 genuine intended-contract failures and 153 passes. Failures exposed the old five-point default, missing/null serializer/snapshot behavior, separate Ruthlessness combination and actual rotation differences. The real armor/ownership/energy tests already passed against old code.

The original HTTP capture's summary parsed numbers as Python floats. It is retained as an intermediate attempt only. Exact comparison evidence instead uses separately retained **raw HTTP JSON response bytes**, parsed with decimal-preserving `Decimal`; no binary floating-point conversion participates in exact equality. Comparison retains the full response summary, resources, damage, threat, aura times and complete event data, excluding only generated execution/aura/autoattack GUID identity fields and the newly added modeling disclosure. Requests use zero input latency consistently on both binaries.

The three untouched default responses match exactly under that normalization. Opt-in scenarios are Assassination builds with guaranteed-critical Mutilate plus separate Seal Fate generation, controlled Energy and armor, using each of the three preset-derived seeds. They are not claimed as three-specialization opt-in coverage. For every rank and seed:

- Explicit 0/1/2/5 and web numeric strings 0/5 exactly match old complete normalized responses and timelines.
- New missing/null exactly equals the old binary supplied the literal rank override (0/1/2).
- Old missing exactly equals new explicit 5.
- Rank 0 is unchanged. Rank 1/2 default differences are fully reproduced by changing only that old explicit refund value. This accounts for downstream rotation, resource and damage changes without a second implementation of the combat model.

[Durable numerical attribution receipts](CP175-rogue-expose-armor-receipts.json) record the comparisons and totals. Full raw responses, requests, baseline binaries, red tests, scripts and logs remain in the acceptance directory; previous artifacts are preserved.

## Regression and acceptance coverage

`CP175_RogueExposeArmorRefund.cs` adds 184 cases using the real spellbook, ability executor, aura engine, runner adapters and processor, plus public Preview/Run. Independent literal 0/1/2 refunds, 25/20/15 costs and 4250-versus-5500 target armor expectations anchor the tests rather than mirroring the production refund code as the sole oracle.

Coverage includes ranks 0/1/2 × spent 0..5 × missing/null/all explicit 0..5; ownership, wrong target/source, missing execution, duplicate cast/aura delivery, actual miss behavior, full five-point armor on only the exposed target, separate deterministic Ruthlessness success/failure and cap five, unrelated finishers, public nullable type/serialized default, web numeric strings, invalid values/types, snapshot isolation, caller preservation, repeated/concurrent Preview/Run, actual rotation/resource/armor events and exact replay. `solo-dps-rogue-expose-armor.test.cjs` adds real loopback HTTP defaults, binder, validation, default/explicit controls, mixed concurrent requests and replay checks without adding an endpoint or dependency.

Final local acceptance: clean single-process no-restore Release rebuild with warnings as errors (0 warnings/errors), all 4,442 xUnit tests passed with 0 skipped, 277 focused refund/preview cases passed, 13 existing Node suites and 126 Python cases passed, JavaScript syntax and whitespace checks passed, 148 existing extra-class HTTP requests and 72 new refund/serializer HTTP requests passed. Task-owned APIs were stopped. Twelve durable receipt groups cover exact before/after controls; the three existing fingerprint tests are unchanged. An earlier 4,439-test run did not yet contain the last three unrelated-finisher cases and is retained as intermediate evidence; the clean final rebuild includes them.

Independent review and existing Actions real-browser acceptance are separate. The current base's Actions run failed a Balance cancel/export browser check, so it is not a green base gate; a separate scoped task investigates that failure. This Rogue step claims no local real-browser pass, no published correction-head result, and no completed broader assignment. No commit/push, merge/deployment, new permanent branch/PR, source/account/security changes, or spending occurred in this implementation step.
