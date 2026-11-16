# CP175 modifier artwork provenance

## Scope and result

The existing modifier tile renderer can now use **25 of 46** source-backed
historical artwork references: 18 consumables, five external buffs, and two target
debuffs. The remaining 21 retain the neutral tile. The icon join is display metadata only and never imports numerical stats or
eligibility. The later shared-elixir catalog stage separately corrects source-backed
consumable IDs, crit aggregation and family selection; artwork is not mechanical evidence.
All new mappings are provisional, not current-build/server verification.

The existing optional talent artwork downloader supplies the same pinned JPEG
source. All 25 modifier images plus five additional equipment images were successfully cached locally for review.
No copyrighted image binaries are committed, no external requests are made by the
application, and an empty local cache still produces a working neutral fallback.

## Exact source and join

- Repository: [gunba/wow-forever-sim](https://github.com/gunba/wow-forever-sim).
- Commit: `1cc5ae1e9abdbf941b7326d2d90421822dce31ba`, identical to the existing
  talent visual-reference snapshot.
- [Database source](https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/assets/database/db.json).
- Raw database SHA-256: `e342a7ce2dbc9ebc3e3beb2b0c23ed379b665f549ff64c97f4adec2ed61eb8de`.
- Item references require the catalog's existing item ID and exact source name.
  Spell references require its existing spell ID and exact name. The explicit UI
  suffixes `(Well Fed)`, `(Forever)`, and `(Rank N)` are the only removed text.
  Where the source supplies rank, a conflicting rank rejects the match.
- Unsafe filenames, ambiguous keys, absent IDs, and name conflicts are unresolved.
  Runtime attachment additionally requires the unchanged catalog key, full display
  name, category, spell ID and item ID, plus pinned manifest identity.
- API/export metadata exposes `iconKey`, `iconSourceStatus`, `iconSourceUrl`, and
  `iconSourceCommit` independently of mechanical `dataConfidence`.

`forever-modifier-icons-cp175-provisional.json` retains every matched and unresolved
catalog identity, so absence is auditable rather than silently substituted.

## Unresolved coverage

- 11 entries have no existing item or spell ID. Names alone do not establish IDs.
- Eight existing IDs are absent from this pinned database.
- Two name conflicts are deliberately not promoted: item 9206 is `Elixir of Giants`
  in the source but `Elixir of Greater Strength` in the catalog; item 3825 is
  `Elixir of Fortitude` in the source but `Elixir of Lesser Fortitude` in the catalog.
  These require a separate current-source identity/mechanics review, not an art guess.

The prior equipment-only cache contains no matching consumables; it was not used
as fallback evidence. This work does not claim completion for every combat-use
inventory control, temporary enchant, ability, or remaining modifier tile.

## Additional exact identities and equipment artwork (2026-11-12)

The pinned source UI selection and database agree on four formerly missing item
identities: Elixir of Ferocity **250350**, Elixir of the Grizzly **250351**, Elixir
of the Owl **250337**, and Elixir of Greater Intellect **9179**. These now join their
exact item icons. This does not substitute source250338 “Elixir of Sages” for the
separate local13447 “Elixir of the Sages”, or resolve the two name conflicts above.

Equipment coverage is **20 of 79** registered items. The original15 mappings in
`forever-equipment-icons-cp174-provisional.json` retain their Napalmsteak source
commit/status unchanged. A separate `forever-equipment-icons-cp175-provisional.json`
adds five exact gunba item ID/name joins, using the database pin above:

| Item ID | Item | Icon key |
| --- | --- | --- |
|11287|Lesser Magic Wand|inv_staff_02|
|14152|Robe of the Archmage|inv_chest_cloth_38|
|19682|Bloodvine Vest|inv_chest_cloth_07|
|19683|Bloodvine Leggings|inv_pants_cloth_14|
|19684|Bloodvine Boots|inv_boots_cloth_02|

The existing reviewer reproduces these additive mappings with
`--equipment-manifest`; it rejects conflicting old mappings rather than replacing
their provenance. The embedded loader validates source identity, exact registered
ID/name, safe key and unique IDs before applying any row. Existing matching artwork
retains its original status. Numeric fields, unknown class restrictions and the
separate bulk-import evidence gate are unchanged.

All nine new assets were acquired using the existing ignored local cache and
immutable receipt/JPEG validator. Their byte counts, SHA-256 and Git blob hashes,
source URLs/commit, receipts and decoded pixels were inspected. Equipment hashes
match the retained pinned source tree. The four elixir icons show distinct potion
artwork; the five equipment references show wand/robe/vest/legs/boots artwork.
This is image-byte inspection, not a claim of full browser acceptance. The existing
browser capture pipeline now checks and caches the additional equipment manifest
before starting its local server. Missing local artwork still uses the neutral
fallback; no runtime network fetch or committed image binary was introduced.

## Reproduce and verify

Run from the repository root in an environment permitted to read the pinned
public source. `--check` verifies the committed manifest without rewriting it.

```sh
python tools/ForeverEquipmentImportCli/review_forever_modifier_icons.py --check --equipment-manifest WarcraftSim.Api/Catalog/forever-equipment-icons-cp175-provisional.json --image-dir WarcraftSim.Api/wwwroot/forever-icons
python -m unittest discover -s tools/ForeverEquipmentImportCli -p 'test_cp175_modifier_icons.py' -v
dotnet test WarcraftSim.Tests/WarcraftSim.Tests.csproj -c Release --filter FullyQualifiedName~Cp175ModifierIconProvenance
```

An optional `--source-file` permits offline regeneration from bytes with the exact
pinned checksum. Omit `--check` only when intentionally regenerating/reviewing the
metadata manifest. Cached JPEGs remain under the existing ignored directory.

## Source notice

Repository metadata is published under the following MIT license. This does not
assert a redistribution license for Blizzard game artwork, which remains excluded
from committed deliverables.

MIT License

Copyright (c) 2024 wowsims team

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
