# CP175 shared elixir critical component and historical catalog artwork

## Source boundary

The accepted starting point is `3c6232bbc9e4161119eae58721ce56184ff9f796` (Shadow damage factors), with exact-head Actions 43174934773 accepted. This stage uses the already authorized historical `gunba/wow-forever-sim` commit `1cc5ae1e9abdbf941b7326d2d90421822dce31ba`, not a new current-client observation.

`sim/core/consumes.go` applies physical/spell attribute consumes and then one OR-gated `applyForeverElixirConsumes` contribution: Mongoose, Grizzly, Owl or the source's distinct Sages enables **2 percentage points of attack and spell critical chance once**. Selection fields are independent agility, strength and intellect categories (`proto/common.proto`, `ui/core/components/inputs/consumables.ts`, `ui/core/player.ts`); no cross-category reset prevents Mongoose + Grizzly + Owl. This is a startup stat contribution, not a modeled shared aura lifecycle. Source comments acknowledge the absence of an independently verified client stacking table. Values and stacking therefore remain provisional historical implementation parity.

Grilled Squid separately supplies **1 percentage point in both channels**, supported by the pinned consume implementation and `forever_consumes_test.go`. Ferocity belongs to the source strength category, so it can coexist with Mongoose but not Grizzly. The source item IDs for Grizzly250351, Owl250337, Ferocity250350 and Greater Intellect9179 match names and source selection/stat semantics. The affected definitions identify their historical source and explicitly unverified client build; existing level/duration admission remains bounded by the previous catalog, not newly certified by this correction.

Do not conflate source250338 **Elixir of Sages** with local13447 **Elixir of the Sages**. The latter remains18 Intellect/18 Spirit without the new shared crit component. Conflicting9206/3825 names, Defense ranks, unknown class masks and bulk numeric equipment eligibility are not promoted.

## Implementation and reporting

`ForeverExternalModifierCatalog` retains `Stats` as an item's standalone total, exposes `SharedStatGroup`, `SharedStats` and the independent per-item breakdown. A single local helper emits each item's independent attributes under its stable key and one positive `cp173-shared-elixir-critical-chance` contribution for the entire selected group. Both profile construction and raw-stat summation reuse this helper. No negative compensation, first-selected ownership, new Core stat system or build-schema change is introduced.

Squid remains an independent food contribution: any valid nonempty crit-elixir set plus Squid yields3 attack/3 spell critical percentage points before attribute-derived crit. Tooltips distinguish independent attributes from shared-once crit; normal contribution reporting exposes the actual effective totals. Hunter consumes the physical channel once through its existing adapter; the spell channel is not added a second time to physical tuning.

Legacy Warrior Mongoose is routed through the same helper before insertion, retaining its legacy attribute key. Its standalone catalog metadata lists both crit channels and shared-once decomposition. Other legacy support effects are untouched. Enabled recognized member aliases and the reserved group contributor cannot silently double-apply a selected shared group, including trimmed/case-insensitive aliases. Inactive matching contributor keys are replaced only on a copied profile. Caller-authored stats are not rewritten to infer a consume policy.

## Artwork and ingestion boundaries

See [modifier/icon provenance](CP175-modifier-icon-provenance.md). Four exact modifier identities and five equipment joins are artwork-only expansions, retaining separate pinned gunba and existing Napalmsteak source identities. Existing reviewed equipment values and class eligibility do not change. Acquisition uses the existing validated JPEG/hash cache before server enumeration, never application-time external image requests or committed image binaries. Unknown identities retain neutral fallback.

The historical numeric DB is not absent, but lacks sufficient required-level/current-client/ItemSparse eligibility evidence for blanket promotion. The established candidate/approval pipeline and its blocker remain intact.

## Verification gates

Accepted tracked source and complete Release runtime were archived before edits; the runtime matches the preceding accepted manifest. All627 inherited response envelopes were captured from a private archived runtime. The initial independently authored41-case C# regression suite produced40 failures/1 pass against those accepted binaries, before candidate builds. Assertions derive the source OR-gated crit, additive food, independent attributes, selection categories, aliases and exact identities rather than observing candidate numbers.

Focused green tests, strict full aggregate, complete old-runtime authored-stat oracles and unchanged controls, source/runtime custody and independent review are recorded in the stage evidence. Native browser selection, contribution, icon, export/import/replay and failed-import rollback assertions are attached to the existing browser pipeline. Exact-published-head Actions/browser acceptance remains coordinator-owned and must be checked before calling the stage accepted. No merge, deployment, overall completeness or live-server certification is implied.
