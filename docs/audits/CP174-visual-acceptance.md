# CP174 visual redesign — desktop and responsive acceptance

This checklist is required in addition to the existing backend integration smoke at `docs/audits/CP174-solo-dps-smoke.md`. **GitHub CI cannot establish actual rendered appearance.** No browser screenshot evidence is claimed until the owner or a browser-enabled runner captures it.

## Launch

```powershell
git checkout cp174-unified-solo-dps-frontend
dotnet restore WarcraftSim.slnx
dotnet build WarcraftSim.slnx -c Release --no-restore -warnaserror
dotnet test WarcraftSim.slnx -c Release --no-build
python -m unittest discover -s tools/ForeverEquipmentImportCli -p "test_*.py"
node --check WarcraftSim.Api/wwwroot/solo-dps.js
node tools/solo-dps-ui-contract.test.cjs
dotnet run --project WarcraftSim.Api
```

Open the displayed server origin + `/solo-dps.html`.

## UX verification matrix

1. At **1920×1080, 100% zoom**, screenshot Equipment, Talents, Effects, Encounter and Results. Confirm header, tabs, stat sidebar and slot grid are readable without zooming out. Expect the first four sections to use dedicated tabs rather than simultaneously showing full forms.
2. Repeat at **1440×900**, **768×1024**, and **390×844**. Use browser developer responsive mode. Confirm no body-level horizontal scroll or clipped header action; the stat sidebar collapses into an inline summary at tablet sizes.
3. Change Mage → Fire → Human; tick historical provisional only after reading baseline status. Navigate across all five tabs and back: controls must keep their unsaved state.
4. In Equipment, activate an empty slot with mouse, then with keyboard Tab and Enter. Confirm the picker opens, focus moves to search, filtering works, page navigation works, item details show verification and stats, and Escape returns to page. Equip, reopen, add compatible permanent and temporary enchants, verify server validation, then unequip.
5. Attempt invalid item combinations. The server error must appear in the status area, and the invalid selection should roll back rather than remain equipped.
6. In Talents, add/remove ranks from multiple tree groups, verify max ranks, reset and restore default. Intentionally fail a prerequisite and confirm server validation before a run.
7. In Buffs, select a food, elixir, external buff and target debuff. Attempt competing exclusivity-group effects, verify the conflicting checkbox reverts with a message. Check racial active.
8. In Encounter, adjust seed, duration, target HP, timeline capture, companion family/type and rotation choices for each relevant class.
9. Run each of the nine Mage/Warlock/Hunter specs. Confirm Results tab auto-opens, actual DPS and damage appear, ability table uses backend counts, timeline and JSON are available when returned.
10. Save locally, change fields, load, and verify every selection survived. Repeat with export/import JSON; reject unsupported schema and server-invalid talent/gear.
11. Check keyboard focus indicators, tab keyboard arrow switching, dialog focus/escape, button labels, text contrast, and modal scroll.
12. Run `/sim-lab.html` to check legacy debug frontend remains functional.

## Known boundaries

The backend talent catalog contains **modeled subset only**; no fabricated tier positions or historical descriptions are displayed. Item search currently paginates a server-filtered catalog already retrieved in batches; it is not yet server-driven incremental page search. Combat consumable activation is still governed by existing typed defaults; the modifier catalog does not expose an independently selectable combat item loadout here. The sidebar shows available backend stat values without calculating totals in the browser.
