# CP174 follow-up: talent, icon and priority provenance

## Implemented in this continuation
- Two faction-specific Skyborne entries are retained via existing race eligibility, with the labels **Skyborne (Alliance)** and **Skyborne (Horde)**. The frontend does not add eligibility or invent baselines.
- Modeled talent rows now accept left-click to increment, right-click to decrement (no browser context menu), Enter/Space to increment, and Delete/Backspace to decrement. Each point modification round-trips through the authoritative typed talent validation endpoint, and an invalid change is rolled back.
- Modifiers use compact selected icon tiles, with a neutral generic glyph explicitly not presented as a source icon. Exclusive groups keep backend-aligned semantics.
- Mage/Warlock engine rotation preview route `POST /api/forever/rotation/preview` derives from `MageAdaptiveRotation.Build` or `WarlockAdaptiveRotation.Build`; the encounter editor can preview, explicitly apply, reset, and persist a custom ordered list in the existing v1 schema's encounter subobject. Preview rejects unlearned or unsupported actions. Hunter has no unified preview contract and is not misrepresented as supporting it.

## Unresolved source and architecture constraints
- **Talent icons and tree positions:** the current combat-connected model catalogs expose keys, trees, and max ranks; client-complete row/column, prereq connectors, spell rank IDs, full descriptions and verified icon file IDs are missing for many talents. The UI therefore remains a three-column grouped fallback rather than drawing fabricated historical links.
- **Unrestricted cross-specialization builds:** the Mage and Warlock priority/default policies and several talent execution paths explicitly dispatch on the selected specialization. Hunter has three materially distinct runners. A displayed preset is therefore still a runner choice; arbitrary same-class talent builds are **not yet certified as unrestricted**. Correctly eliminating this restriction requires controlled combat-engine refactoring and all-nine end-to-end tests. Do not claim it is complete.
- **Equipped item icons:** Forever client-derived appearance and FileDataID pipelines exist, but WarcraftSim's reviewed runtime catalog has no safely joined item-ID-to-icon-ID metadata. Until a reviewed join and local, licensed/correctly sourced assets are available, the neutral equipment glyph must remain.
- **Spell/buff icons:** reliable keyed icon data likewise has not been connected to the executable modifier catalog. The new tile layout labels these with generic symbols, not authentic spell artwork.
- **Full custom condition editor:** the current UI orders valid engine-owned ability keys. Recovery, cooldown and aura conditions are sourced from the engine and not exposed as arbitrary editable predicates. Hunter custom priority behavior still uses the individual runners, not the shared editor.
- **Combat consumables and weapon oils/stones:** the existing combat-item catalog and temporary enchant endpoints are different data sources from the static external modifier catalog; these controls remain separate until executable consumable choices are wired.
- **Browser interaction tests:** CI syntax, static DOM contracts and viewport screenshots are not equivalent to actual click/keyboard simulation through a browser.

## Next acceptance gates
1. Build validated icon join from pinned Forever source tables and reviewed local image cache. Preserve provenance per icon, never guess from a name.
2. Pin explicit historical Forever talent tree coordinates, prerequisite links, rank descriptions and spell IDs; extend backend validation and build the genuine 3-tree grid.
3. Prove custom cross-tree talents and rotations affect each of the nine specialization engines (including 19 Hunter families) independently from display-label assumptions.
4. Run Playwright/WebDriver click, right-click, keyboard, tooltip, switch, import/export, responsive, and identical-seed roundtrip tests.
