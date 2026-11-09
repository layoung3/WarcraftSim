# CP175 modifier artwork provenance

## Scope and result

The existing modifier tile renderer can now use **21 of 46** source-backed
historical artwork references: 14 consumables, five external buffs, and two target
debuffs. The remaining 25 retain the neutral tile. This is display metadata only;
no modifier values, IDs, duration, stacking, eligibility, or combat behavior changed.
All new mappings are provisional, not current-build/server verification.

The existing optional talent artwork downloader supplies the same pinned JPEG
source. All 21 referenced images were successfully cached locally for review.
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

- 15 entries have no existing item or spell ID. Names alone do not establish IDs.
- Eight existing IDs are absent from this pinned database.
- Two name conflicts are deliberately not promoted: item 9206 is `Elixir of Giants`
  in the source but `Elixir of Greater Strength` in the catalog; item 3825 is
  `Elixir of Fortitude` in the source but `Elixir of Lesser Fortitude` in the catalog.
  These require a separate current-source identity/mechanics review, not an art guess.

The prior equipment-only cache contains no matching consumables; it was not used
as fallback evidence. This work does not claim completion for every combat-use
inventory control, temporary enchant, ability, or remaining modifier tile.

## Reproduce and verify

Run from the repository root in an environment permitted to read the pinned
public source. `--check` verifies the committed manifest without rewriting it.

```sh
python tools/ForeverEquipmentImportCli/review_forever_modifier_icons.py --check --image-dir WarcraftSim.Api/wwwroot/forever-icons
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
