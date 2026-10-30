# CP171 — Source-backed equipment database and repeatable acquisition

**Status:** Implemented reviewed-source runtime batch, acquisition tools and testable import pipeline. **Not a complete Forever item database.** Branch `cp171-forever-equipment-database`, draft PR #24, starting main `1b537f64a0d75f3565b0b10b2a7d52671a1e0fd1`. Do not merge before local owner validation.

## Source strategy and limitations

1. **Client CSV primary path (opt-in, local):** Download pinned DB2 CSV exports from `wago.tools` for the Forever `wow_classic_beta` product. The independently published endpoint is `https://wago.tools/db2/<Table>/csv?build=<full-build>&locale=enUS`. This is based on the publicly documented WoWSims/Cataclysm DB2 -> SQLite -> normalization architecture and the community's wago CSV clients. CP171's `download_client_csv.py` downloads the relevant equipment/spell tables, saves each raw file locally with SHA-256 and a source URL, and stages `ItemSparse` **metadata only**. It intentionally does not guess client layout definitions, level-60 armor/damage scaling, class masks, set procs or hotfix overrides. It is ready for an authorized client/table run, but **no real Forever DB2 export was downloaded or validated in this development environment**.
2. **Reviewed published client tooltip data (initial combat-capable catalog):** `forever-equipment-cp171-reviewed.json` contains **29 individually reviewed records** drawn from public build-attributed client-derived tooltips at `bindonforever.com`. Each has a numeric item ID, source URL, item-reported client build, quality, slot, restrictions, static stats and weapon properties where present. These records are labeled `published-client-tooltip`—**not** direct on-disk client extraction and **not** verified against the latest client build. The manifest mixes historical sources from 1.60.1.70291, 70338, 70450, 70532, 70634 and 70685 and does not claim 1.60.1.70707 verification.
3. **AHledger metadata (secondary):** Retains its CP170 documented read-only single-item identity endpoint. AHledger's documented `/v1/items/{id}?game=forever` does not certify combat stat arrays; it cannot silently promote item data. The open-data pages mention broader JSON catalog access with attribution; the complete combat schema and redistribution/copying restrictions were not independently verified here. Do not assume it is a free full-stat feed.
4. **Original CP170:** The existing 24 entries remain, including 3 Classic-proxy IDs that are rejected by the shared DPS resolver. New records do not reclassify or automatically validate them.

**Totals as packaged:** **53 runtime item records** (24 baseline + 29 independently source-backed additions), **0 newly staged metadata-only records in the repository**, **3 baseline Classic-proxy records** remain. Source-supported static combat stats in the 29 new records are usable in the solo simulator. Missing/unsupported spell penetration, unverified hotfixes, unmodeled set effects and source build drift must not be confused with complete current-client verification.

## Runtime pipeline

- `ForeverEquipmentCatalog.Load()` loads the original CP170 embedded JSON and applies the pinned approved CP171 reviewed manifest through **`ForeverEquipmentSnapshotPipeline.Import`**; it then runs the existing duplicate/source/set validation. No independent resolver or class catalog was added. The 29 approved additions are available via existing `FindItem`, `GET /api/forever/equipment/items`, `GET /api/forever/equipment/items/{id}`, `POST /api/forever/equipment/resolve`, and Mage/Warlock/Hunter simulation entrypoints. No networking occurs in simulation.
- Manifest import validates stable IDs, supported stat keys, weapon speed/damage, restriction slots, build references and HTTPS attribution; duplicate original IDs require explicit source-backed overrides. An input manifest with `clientBuild=mixed-reviewed` permits separate pinned `sourceBuild` values; otherwise every item must match the one pinned build. Metadata-only candidates are rejected from the combat catalog and reported separately.
- `ForeverEquipmentImportCli` produces deterministic serialized catalogs, a change-by-field diff/report, and a SHA-256 for the generated output. Client source CSV data stays local; the only committed item records are separately reviewed normalized facts.
- Imported item-set memberships and thresholds are represented. **Unmodeled** effects (Plagueheart, Frostfire, Cryptstalker, Doomcaller, etc.) are tagged `modeled:false` and never added as fabricated static bonuses. Older CP170 stat-only Devilsaur/Dal'Rend effects continue to execute exactly as before.
- Parsed spell-effect IDs can be linked through `ForeverEquipmentEffectReference`; unknown numeric IDs are not manufactured. The initial newly reviewed set-effect records have verified human-readable semantics but no independently verified spell IDs.

## Repeatable local workflows

Run these from repository root. Python 3.10+ and .NET 10 SDK are required.

**A. Optional Forever client-table data acquisition — build must be pinned.**

```bash
python3 tools/ForeverEquipmentImportCli/download_client_csv.py fetch --build 1.60.1.70707 --directory ./local-forever-db2
```

This downloads and hashes: `Item`, `ItemSparse`, `ItemEffect`, `ItemXItemEffect`, `SpellEffect`, `SpellItemEnchantment`, `ItemSet`, `ItemSetSpell`, `ItemDamageOneHand`, `ItemDamageTwoHand`, `ItemDamageRanged`, `ItemArmorTotal`, `ItemArmorQuality`, `ItemArmorShield`, `ArmorLocation`. Inspect `local-forever-db2/1.60.1.70707/acquisition-report.json`. Tables that return an unavailable/changed schema fail with an explicit report. Verify client build, DB2 DBD definitions, encrypted/hidden rows and hotfix overlay separately.

**B. Stage metadata without promoting it to combat stats.**

```bash
python3 tools/ForeverEquipmentImportCli/download_client_csv.py stage --build 1.60.1.70707 --directory ./local-forever-db2 --imported-at 2026-10-30T15:00:00Z --output ./local-forever-db2/candidates.json
```

This reads the local ItemSparse CSV. It requires known ID/name columns, fails rather than guessing a new schema, and emits `metadataOnly`. Reviewers can use the candidate IDs to investigate gear and prepare a **separately source-backed** manifest; do not copy a metadata-only object into `items` without verified stats and effect disposition.

**C. Recreate the approved combat-capable catalog and diff.**

```bash
dotnet run --project tools/ForeverEquipmentImportCli -- import WarcraftSim.Api/Catalog/forever-equipment-v1.json WarcraftSim.Api/Catalog/forever-equipment-cp171-reviewed.json ./local-forever-db2/approved-catalog.json ./local-forever-db2/approved-report.json
```

This outputs the same CP170+CP171 53-item catalog as runtime. It fails on bad data, duplicate IDs, unsupported slots/stats, missing source build, or unreviewed overwrite. To add many new items, supply a new reviewed versioned manifest in the **existing schema** without changing the simulator code. If approving a new runtime snapshot, review the catalog diff, source permission, stats and build metadata, then replace the embedded reviewed-manifest input in a new PR and rerun tests.

**D. Strict local validation.**

```bash
dotnet build WarcraftSim.slnx -c Release -warnaserror
dotnet test WarcraftSim.slnx -c Release --no-build
```

GitHub Actions additionally runs two clean CLI exports and byte-compares them and stages a fixture ItemSparse CSV as metadata only.

## End-to-end simulation evidence

`CP171_RuntimeEquipmentIntegration` checks:
- Mage Frostfire Robe 22496: +47 Spell Power, +27 Intellect from the imported record, and increased deterministic Fire damage.
- Warlock Plagueheart Robe 22504: +51 Spell Power and increased actual Warlock and Imp damage without double attribution.
- Hunter Bland Bow 20368 (1.50s) versus Thornflinger 16622 (2.80s): changed Auto Shot timing and actual output.
- Hunter Cryptstalker Tunic 22436: +576 armor, increased actual inherited pet incoming mitigation.
- Source-backed full Frostfire set reconciles to +313 Spell Power, +177 Intellect, +162 Stamina and the published hit/crit totals; unsupported set bonuses remain nonexecuted.\n- Crafted Bloodvine 3-piece static stats and the Robe of the Archmage on-use spell ID 18385 are preserved without fabricating dynamic effects.\n- Items by ID, source build and API search, restrictions, set bonus nonexecution, and deterministic repeated runs.
- The previously established CP170 shared class, rating, weapon and set tests remain in the full xUnit suite.

## Outstanding before calling CP171 complete / broad item database

- Run wago.tools pinned CSV acquisition on a real Forever build; obtain exact ItemSparse/Item/ItemEffect etc. column definitions and compare with local legitimate client DB2 and hotfix caches. No successful live extraction is claimed.
- Build and verify a **schema-correct bulk combat-stat normalizer** for an actual accessible source, including armor/weapon scaling tables and set/proc linkage, with source/raw checksums. The current CSV tool only creates metadata candidates automatically.
- Acquire much broader independently verified dungeon/crafted/quest/raid equipment including new weapons and relevant all-class choices, not merely the 29 reviewed seed entries; distinguish encrypted/server-observed data.
- Resolve all item restriction subtleties, item-specific effects, unmodeled stat schools (including spell penetration), and source build changes; do not infer replacement stats from Classic.
- Run the final full strict CI and owner local tests, then obtain explicit merge approval. Keep PR #24 draft/unmerged until then.

## Data licensing

WoWSims' source code is a workflow reference; no game binaries or WoWSims assets are embedded. Public CSV and game records remain subject to the original provider and Blizzard terms; the downloaded CSVs, DB2/DBC files, hotfix caches and client assets should stay local and uncommitted. Published item page facts are reviewed individually and attributed, not bulk scraped. All snapshots must preserve source build and provenance.
