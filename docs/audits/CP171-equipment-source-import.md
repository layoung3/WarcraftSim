# CP171 — Reviewed Forever equipment snapshots (initial implementation batch)

**Status:** Partial implementation; not certified. Starting `main`: `1b537f64a0d75f3565b0b10b2a7d52671a1e0fd1`. Branch: `cp171-forever-equipment-database`.

## What is implemented

`ForeverEquipmentSnapshotPipeline.Import(manifestJson, ForeverEquipmentCatalog.GetCatalog())` accepts a manually reviewed, explicitly versioned JSON manifest and generates a deterministic `ForeverEquipmentCatalogDocument`. It validates IDs, known stat keys, weapon ranges/speed, permitted slots, Forever client build, HTTPS attribution and combat verification markers. Existing catalog IDs require a source-backed, explicit override. `MetadataOnly` candidates remain separate from simulation items. It produces an import report, a stable JSON serializer, and field-level item change records. `ForeverEquipmentEffectReference` preserves spell IDs with execution state and effect category; it does not activate unsupported proc effects.

The import runs offline. It does **not** fetch websites, download proprietary assets, parse DB2 directly, or automatically replace the embedded catalog. Reviewers can use `CanonicalCatalogJson` for a pinned artifact and inspect `Compare` differences before intentionally packaging a new catalog. The existing CP170 catalog remains the runtime default.

## Immediate status and coverage

Current starting catalog has **24** item records, including **3 Classic-proxy/unverified** entries. Most other entries cite Forever-oriented item pages but do not consistently carry source-build and verification markers. The one specifically pinned verified caster example is item **19102**, Crackling Staff, build **1.60.1.70532**. Unit tests use a re-reviewed **19102** override to exercise the import pipeline, not to claim any new item count. **New combat-verified items in this first batch: 0.** **Metadata-only imported into production: 0.** No complete Phase 1 or class equipment coverage is claimed.

## Data-source strategy / WoWSims comparison

- **Client-derived:** WoWSims Cataclysm uses `tools/DB2ToSqlite/`, `tools/database/gen_db/`, `generator-settings.json`, and `sim/core/database_load.go`. Its generator lists Item, ItemSparse, ItemEffect, SpellEffect, SpellItemEnchantment, ItemSet, scaling, armor and weapon damage tables. This is a useful *architecture*, not a drop-in Forever parser. It expects build-specific files, layouts and compatible DBD definitions. Forever installation availability, DB2 compatibility, available hotfixes, and redistribution rights were **not validated**. No Forever client files are present here.
- **External structured:** CP170's documented AHledger adapter can retain stable identities and review metadata, but is not a verified combat-stat feed. Game tooltip sites are useful for individually reviewed source attribution; respect provider terms and do not bulk scrape.
- **Curated versioned manifests:** currently implementable with source-by-source review. Store a timestamp, client build, source URL, content version and verification status. Apply explicit reviewed overrides and preserve old artifacts for comparison.

## Sample manifest

```json
{
  "catalogVersion": "forever-1.60.1.70532-reviewed-1",
  "contentVersion": "reviewed-1",
  "clientBuild": "1.60.1.70532",
  "sourceName": "reviewed-forever-client-tooltips",
  "sourceUrl": "https://theforeverera.com/en/items/",
  "importedAt": "2026-10-30T13:56:00Z",
  "items": [],
  "metadataOnly": [],
  "overrides": []
}
```

One effect example: `{"kind":"proc","spellId":12345,"executionStatus":"not-modeled","notes":"Not yet implemented"}`. Spell IDs are **only illustrative** in this example and must never be loaded without verification.

## Remaining CP171 work before acceptance

1. Verify access to a legitimately installed current Forever client; document actual build, data layout, DBD schema and permissible extraction/redistribution. Implement and test DB2 ingestion **only** if practical and authorized.
2. Populate meaningful item coverage across Mage, Warlock, Hunter and neutral gear from individually verified Forever client stats; keep uncertain metadata-only records separate.
3. Wire an approved generated snapshot as the embedded runtime catalog (or a validated version-selectable offline catalog) without breaking existing CP170 resolution.
4. Cover end-to-end Mage, Warlock and Hunter results using newly imported items and item-set cases; test source versions, conflicts, pagination and offline reproducibility.
5. Run `dotnet build WarcraftSim.slnx -c Release -warnaserror` and `dotnet test WarcraftSim.slnx -c Release --no-build` on the final branch and check .NET 10 GitHub Actions. **No build or test execution was available at this initial coding stage.**

Do not merge this draft PR until the completion gate and owner's local validation.
