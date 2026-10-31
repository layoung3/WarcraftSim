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

## Rank-derived talent path — *partially delivered*

A new `POST /api/forever/talents/{class}/{spec}/validate-itemized` rejects forged aggregate totals, unknown/over-ranked talents, tiers without enough **earlier-row** ranked points, and unsupported prerequisites. A `GET .../itemized-catalog` exposes historical-only sourced talents as explicitly **unmodeled**, not as executable abilities. Their allocated ranks can count toward a strict rank-derived budget. The unified simulator's `itemizedTalentMode=true` request adds `itemizedTalentRanks` (full sourced ranks) while its typed `talents.ranks` contains executable subset only; a mismatch or selected modeled talent belonging to an unsupported runner fails closed.

The **Use itemized fallback** button deterministically retains only ranked talents meeting sourced tier, prerequisite and budget checks and drops unreconciled legacy deep selections. It does **not** assign unrequested filler ranks. It then unlocks selection of the historical-only unmodeled catalog entries, which consume real points but never grant phantom combat effects. Saved strict-mode builds retain the mode and revalidate at import and simulation time.

**Not fully completed:** old typed default presets and legacy direct APIs still accept aggregate `PointsSpent` for compatibility. Switching to strict mode is currently explicit, and an itemized fallback may lose talents that were previously unlocked by synthetic default totals. The source-only records come from a pinned historical WoWSims tree and are *not independently verified for 1.60.1.70707*. The engine-modeled catalog remains partial, so unlimited cross-tree gameplay execution is not asserted. Full preset migration, source verification and unconditional rank-only enforcement across every typed public API are still required for owner acceptance.

## Supported consumables and boundaries

The combat item executor remains the authority for resource charges, personal/shared cooldowns and actual impact events. Source-backed tested candidates include Mage mana gems (Ruby/Citrine/Jade/Agate), runes, currently modeled potions, Dense Dynamite and Goblin Sapper Charge. Their live-server cooldown group interactions are provisional as documented in `CP174-consumable-stacking-and-priority.md`.

- Mage/Warlock expose the supported combat-use items; Hunter typed requests do not yet expose equivalent consumable inventory. Unsupported Hunter controls are not rendered.
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

Open `/solo-dps.html`. Confirm the historical-provisional baseline is an explicit choice. Use **Use itemized fallback** to remove inherited synthetic tiers; the UI tells you which preset talents were dropped. Add a historical-only unmodeled first-row talent, verify the rank count and warning, and run again. Equip a compatible weapon, add/remove Brilliant Wizard Oil on Buffs & Consumables, and edit its permanent enchant separately. Run all starting presets, export a JSON report, click Replay same seed, and inspect the complete original request, preview stat contributions, events and version receipt. Download three report examples from the latest successful PR CI run's `cp174-level60-full-simulation-reports` artifact.

**Owner acceptance remains blocked on the explicitly unfinished talent migration, full current-client source verification, and Hunter combat consumable parity.**
