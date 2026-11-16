# CP175-F1D — Forever bulk equipment source feasibility (blocked)

Date: 2026-11-01. Repository: `layoung3/WarcraftSim`.
Branch: `cp175-shared-character-simulation`; existing draft PR #28.
Starting commit `11afabf48d96900efcf2937f4c7178f51c3c2a29`;
exact-head Actions #41365649457 succeeded.

## Decision

**No inspected external source has been confirmed to provide both a permitted
repeatable feed and complete, independently usable, build-pinned numeric combat
values for a meaningful new batch.** This is a *source-validation blocker*, not
proof that no such export exists anywhere. Consequently F1D takes the explicitly
authorized **blocked** path: **do not add an adapter, do not promote speculative
items, do not introduce another ingestion framework**. Move to CP175-F2 and
retain F1 equipment breadth as an open limitation pending new source evidence.

## External sources investigated

### 1. Bind on Forever

* Site and catalog: https://bindonforever.com/items
* Usage terms: https://bindonforever.com/terms (last updated 2026-10-26,
  as indexed on 2026-11-01).
* Advertised client: `1.60.1.70707`, dated October 30; catalog lists
  approximately 19,844 client-derived items. Human-readable inventory tables
  and individual item pages include names, class/slot filters, required and
  item level, many numeric static stats, equipment-effect indicators and
  source/build labels. This demonstrates useful *individual* item evidence.
* **Permissions:** the terms permit viewing and linking, and short attributed
  quotations, but explicitly prohibit copying large parts of the site/data
  in bulk (scraping or mirroring). They also prohibit disruptive automated
  traffic. No documented and expressly authorized bulk resolved-stat export
  or API was found in the published catalog/terms.
* **Result:** bulk reuse would require provider permission or an independent
  permitted original client export. Do **not** automate harvesting HTML pages,
  discover private interfaces, or assume a hidden API is licensed. Published
  item-level weapon speed/minimum/maximum damage are not a verified bulk feed.

### 2. AHledger Open Data

* Catalog overview: https://ahledger.com/data
* API reference: https://ahledger.com/developers
* Terms: https://ahledger.com/terms (updated 2026-09-19)
* Advertised client catalog: `1.60.1.70707`, imported 2026-10-30,
  approximately 27,495 item identities, including inherited Classic Era data.
* **Documented structured interfaces:**
  `GET https://api.ahledger.com/v1/items/{itemId}?game=forever` offers name,
  quality, item class, level, requirement, vendor value and icon. It does not
  document a final numeric stats array, armor, weapon min/max or weapon speed.
  The open-data page also links a class-filtered catalog JSON endpoint,
  `https://ahledger.com/api/catalog?class=trade-goods`; the format is
  described as one item class per request. Attempts to inspect this endpoint
  were blocked by the research access layer / robots policy; its **actual
  response schema, resolved stats and weapon fields remain unverified**.
* **Permissions:** no API key required, read-only, free personal/commercial use
  with linked credit, no attribution falsification and no resale as a feed;
  documented 300 requests/minute limit and cache rules. Terms prohibit wholesale
  page/catalog copying outside the API terms and scraping pages instead of
  using the API. Attribution requirements would apply if catalog data were used.
* **Result:** permitted metadata acquisition is documented, but no actual
  source response was obtained proving independently usable full combat
  numeric values or item hotfix overrides; cannot supply a certified
  `--resolved-values` approval from the available evidence.

## Existing, pinned repository data and tools

* `tools/ForeverEquipmentImportCli/download_client_csv.py` fetches
  build-requested Wago DB2 CSVs, saves per-table hashes and remains the
  preferred client-metadata acquisition path. Previous workflow #41085378202
  ingested 15 tables and found **19,267 ItemSparse IDs, 19,267 metadata-only
  candidates, zero approved combat records** from the live bulk data.
  Build number is requested, but the downloaded response does not independently
  attest the build in a header.
* `bulk_normalize.py` joins the versioned Item/ItemSparse/effects/scaling
  tables and outputs reviewed records only with independently attested
  numeric `--resolved-values`, matching `--approval` and SHA-256 hashes.
  Derived formula results remain `candidate-unverified-not-for-combat`.
* `verify_formula_observations.py` has small current-build observation
  fixtures in `observations/forever-70707-current-categories.json` and
  `forever-70707-reviewed-examples.json`. They are *individual review
  samples*, not a 10–15-item source feed. CP171 documented that only
  Hunter gloves 272715 matched all observed current-build constraints in
  one calibration run; other selected observations were absent from the
  exported client table and quarantined.
* `promote_approved_bulk.py` already merges checksum-approved complete
  numeric records into the existing catalog, protects CP172 effect-owned IDs,
  and supports idempotent reruns. No replacement architecture is needed.
* Existing frozen baseline: **79 unique registered IDs**, including
  CP170/171 reviewed entries and CP172 effect-owned records, with the
  explicit CP171 override for 279259. Not all registered historical/provisional
  IDs imply fully certified current-build combat data.

## Source-field acceptance matrix

| Required field | Bind on Forever public pages | AHledger documented API/catalog |
| --- | --- | --- |
| IDs, names | Visible | Documented |
| Slots, class, required level | Visible/filterable | Class/level/requirement documented; slot specifics not certified from JSON |
| Current build | Page labels 70707 | Catalog labels 70707 |
| Final primary/secondary stats | Individually shown | Not in documented single-item response; catalog schema unknown |
| Armor numeric value | Individual evidence varies | Not documented |
| Weapon min/max and seconds | May appear on individual pages; no permitted bulk export identified | Not documented |
| Effects and execution | Page effects, not runtime-certified | Not documented |
| Repeatable, permitted bulk retrieval | **No** (bulk copying prohibited without permission) | **Yes for API use**, but resolved values not established |
| Valid approved bulk for CP171 | **No** | **No** |

## Actual attempted access, promotion and evidence

* Inspected source catalog, public terms, open-data page, API developer page,
  and pinned repository manifests/observation files.
* Attempted AHledger catalog URL and documented single-item API URL through
  research access. The catalog link returned a tool cache miss and robots
  restrictions; the item API could not be fetched in the research tool.
  This is an access/testing limitation, **not** a claim the live API fails.
* No mass extraction or bulk crawling was attempted; no provider terms bypassed.
* **0 candidate records submitted for F1D promotion, 0 newly accepted,
  0 rejected by F1D importer**, 0 newly selectable item IDs, and no new
  browser screenshots or combat receipts. Baseline **79 -> 79**.
* No source adapter, code changes or tests were added because the stated
  blocked-path gate prohibits further speculative infrastructure. The
  existing successful baseline CI validates prior code, not a new source.

## Reopen criterion and recommendation

Reopen F1 only after one of:
1. AHledger supplies an accessible documented JSON schema/response with
   build-pinned final stat and weapon fields, whose API terms permit this use;
2. Bind on Forever grants express bulk-data permission and provides an
   approved export or API; or
3. An independently licensed client-derived resolved-value export passes the
   CP171 exact-build, checksum, numeric and weapon validation gates.

Then run the *existing* CP171 normalizer, approval and promotion stages;
compare source values to current 70707 tooltips, verify CP172 precedence,
and demonstrate four-class selectable equipment and seeded replay.

**Proceed to CP175-F2 now.** Keep equipment coverage marked incomplete and
PR #28 draft/unmerged. Do not inflate the catalog from metadata-only or
unverified formula candidates.