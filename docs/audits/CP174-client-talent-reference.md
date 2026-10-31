# CP174: Pinned historical talent visuals and icon cache

## Source and license

- Reference: [gunba/wow-forever-sim](https://github.com/gunba/wow-forever-sim), pinned commit `1cc5ae1e9abdbf941b7326d2d90421822dce31ba`.
- Imported source paths: `ui/core/talents/trees/{mage,warlock,hunter}.json`.
- Derived manifest: `WarcraftSim.Api/Catalog/forever-talent-visual-reference-v1.json` (156 records).
- Upstream code/data source is MIT-licensed; preserve upstream attribution. World of Warcraft names and image artwork are Blizzard intellectual property and are not relicensed here.

The input is a **historical reference**, not independently verified Forever build 1.60.1.70707. WarcraftSim's current modeled talent keys, rank caps, tier/prerequisite validation and combat execution remain authoritative.

## Fail-closed reconciliation

The talent discovery endpoint only attaches a visual reference when the class, tree, exact normalized talent name and maximum rank match an existing engine-owned talent. Outdated/removed talents never become selectable merely because they exist in the reference.

For Mage, specifically corrected 70685 talent positions in `ForeverMageTalentBuild.HotfixPositions` override historical row/column metadata. Other historically sourced positions and tooltip values must stay marked as unverified until 70707 observations reconcile them. Duplicate coordinates remain in an unplaced list rather than overlapping.

Rank tooltip templates and numeric reference values are shown as historical source information. No older prerequisite is promoted into a new combat rule by the UI.

## Local artwork cache

The web app uses `/forever-icons/<iconKey>.jpg` for explicitly matched talent icon identifiers, falling back to a neutral glyph whenever the cache is absent. It does not contact an external CDN at runtime.

A developer may opt in to locally populating this cache, subject to applicable artwork rights and network policies:

```powershell
python tools/cache_forever_talent_icons.py
```

The script only downloads identifiers present in the pinned manifest, verifies JPEG content and size, and stores assets under `WarcraftSim.Api/wwwroot/forever-icons/`. The directory is gitignored so image binaries and large archives are not committed. The script explicitly contacts Wowhead's image CDN at cache-preparation time; it is not a hidden runtime dependency.

Icon names are source-backed; **image-file existence, latest appearance and licensing of each downloaded asset remain separate checks**. Item-ID-to-icon mapping is not solved by this talent-only import. Item artwork must be sourced from an independently verified Item/ItemModifiedAppearance/ItemAppearance join or in-game observation, not guessed from item names.

## Remaining work

A full, trustworthy WoW-style talent builder still requires latest Forever tree metadata, engine support for unmodeled filler and unrestricted cross-spec execution, and integration tests exercising every new tree interaction. This checkpoint preserves the distinction between visual reference and actual simulation rules.
