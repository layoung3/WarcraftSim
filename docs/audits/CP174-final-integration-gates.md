# CP174 final integration — evidence and acceptance gates

**Branch / PR:** `cp174-unified-solo-dps-frontend`, draft PR #27. Never merge without owner approval.

## Implemented integration

- Real `/api/forever/character/generate-level60` resolves a source-labelled baseline, equipment, permanent and temporary enchants, racial passives, externally selected buffs/consumables and derived statistics. It returns `readyProfile` separately from `resolvedProfile` to avoid applying stat sources twice. The frontend exports this resolved server response and all contributions verbatim.
- The single typed `/api/forever/character/simulate-level60` route runs the actual Mage/Warlock/Hunter engines. The complete `originalSimulationRequest` (including RNG seed, gear, talents and rotation) plus raw result are preserved in each export.
- `/api/forever/character/report-metadata` returns a runtime GitHub source commit when available, otherwise explicitly labels a local unstamped build; it includes the target Forever client build, current equipment catalog, modifier-source build and pinned historical talent-reference commit.
- JSON export schema `warcraftsim.solo-dps-report` v1 is deliberately distinct from importable `warcraftsim.solo-dps-build` v1. Completed result JSON is **not** silently imported as an editable build. The export includes precombat stat contributions, validated equipment, point/talent provenance, warnings, complete raw events/metrics and the original run input.
- A replay action resubmits exactly the recorded request and compares stable server metrics (DPS, damage, abilities, pet damage and consumable use/decisions). Internal event-execution GUIDs are **not** expected to match byte-for-byte.
- The Chrome suite exercises the UI, downloads one real report per class (Mage, Warlock with demon, Hunter with pet), confirms replay, and submits all nine starting specialization configurations. GitHub Actions uploads the three reports as artifact `cp174-level60-full-simulation-reports`.
- Brilliant Wizard Oil (25122) is the source-backed modeled CP172 temporary weapon enhancement. The consumables tab selects the target weapon and writes its `temporaryEnchantSpellId` through the existing server validator; the permanent-enchant tab no longer duplicates temporary choices. Permanent removal preserves the temporary effect.

## Rank-derived talent path — strict by default

All nine starting builds now have versioned 51-point `warcraftsim.cp174-itemized-preset` v1 JSON files in `WarcraftSim.Api/wwwroot/itemized-presets/`. The API resolves the exact same files through `ForeverItemizedPresetCatalog` and exposes them via `GET /api/forever/talents/{class}/{spec}/default-itemized`, which checks them using the existing `validate-itemized` contract before returning them. The frontend always loads these ranked starting builds and computes all displayed totals from individual selections. The conversion button has been removed. Normal character-builder imports reject legacy aggregate-only files with a clear explanation; saving/reimporting an itemized build preserves the full allocation.

`POST /api/forever/character/simulate-level60` now **requires** `itemizedTalentMode=true` and `itemizedTalentRanks`. It refuses omitted allocations, synthetic tree aggregates, unknown/max-rank violations, tier violations and missing source-backed prerequisites. The server cross-checks modeled ranks sent to the typed runner against the full allocation. Historical-only ranks count toward the actual 51-point budget but never receive invented effects. Direct legacy per-class API routes retain backward compatibility, rather than changing all existing engine contracts.

**Provenance and scope:** These are **reconstructed, source-named 51-point starting choices**, *not* a claim to have recovered a previously equipped character's exact 51 talents. The pinned historical WoWSims tree is a provisional location/rank source, reconciled with specific Mage 70685 changes and current engine caps. All nine reconstructed preset choices are explicitly saved in data for audit. Unknown or removed talent IDs are rejected. Player-selected historical-only ranks are labelled `unmodeled` and exported as such. Full unrestricted cross-tree modeled execution is still limited by current typed Hunter runners.

## Hunter consumable integration

All three Hunter requests now expose the validated shared `CombatUseItemsOptions` inventory and run the actual `CombatUseItemProcessor` in the Hunter event queue. This supports the existing runes, Major Healing Potion, Dense Dynamite and Goblin Sapper Charge, with profession gates, health safety, inventory charges, cooldown groups and deterministic decisions. Conjured Mage gems are explicitly rejected for Hunter. New result fields mirror Mage/Warlock: `combatItemUses` and `combatItemDecisionReasons`. The compact consumable controls are now displayed for Hunter only because these requests genuinely execute the selected items; unsupported Mage-only gems are hidden.

## Supported consumables and boundaries

The combat item executor remains the authority for resource charges, personal/shared cooldowns and actual impact events. Source-backed tested candidates include Mage mana gems (Ruby/Citrine/Jade/Agate), runes, currently modeled potions, Dense Dynamite and Goblin Sapper Charge. Their live-server cooldown group interactions are provisional as documented in `CP174-consumable-stacking-and-priority.md`.

- Mage, Warlock and all three Hunter typed runners now execute the supported common inventory; only Mage may activate conjured mana gems. Additional Hunter-specific consumables have not been invented.
- Sharpening stones and weightstones are **not** yet approved executable CP172 temporary enchant catalog records. Their absence must not be hidden by invented substitutes.
- The strict/current Forever base-stat mode may fail closed when approved race/class observations are unavailable. The historical-provisional mode is explicitly labeled and is not a verified live-server character prediction.
- Temporary dynamic effects execute during the event queue and cannot all be represented as constant precombat attributes. The report includes raw combat events and source notes rather than fabricating missing uptime or resource-starvation measurements.
- A successful deterministic replay is a code/data reproducibility check, **not** a server DPS accuracy certification.

## Local review

```powershell
git switch cp174-unified-solo-dps-frontend
git pull
dotnet restore WarcraftSim.slnx
dotnet build WarcraftSim.slnx -c Release --no-restore -warnaserror
dotnet test WarcraftSim.slnx -c Release --no-build
python -m unittest discover -s tools/ForeverEquipmentImportCli -p "test_*.py"
node --check WarcraftSim.Api/wwwroot/solo-dps.js
node tools/solo-dps-ui-contract.test.cjs
dotnet run --project WarcraftSim.Api
```

Open `/solo-dps.html`. Confirm the historical-provisional baseline is an explicit choice. Each of the nine starting builds should now load **51 explicitly assigned ranks** automatically. Right-click to remove a rank, verify tier/prerequisite enforcement, select an explicitly unmodeled historical-only rank and confirm that it never grants an invented combat ability. Attempt a forged aggregate in the unified API and verify rejection. Equip a compatible weapon, add/remove Brilliant Wizard Oil on Buffs & Consumables, and edit its permanent enchant separately. Run all starting presets, export a JSON report, click Replay same seed, and inspect the complete original request, preview stat contributions, events and version receipt. Download three report examples from the latest successful PR CI run's `cp174-level60-full-simulation-reports` artifact.

**Remaining caveats:** Reconstructed build choices and some talent rows are historically sourced and provisional, not verified against every final 70707 hotfix. Unlimited cross-tree combat execution remains unsupported. Source provenance and deterministic simulation success do not certify actual launch-server DPS.
