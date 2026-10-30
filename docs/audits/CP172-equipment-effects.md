# CP172 — Enchants and executable equipment effects (development checkpoint)

**Starting main:** `99b0a41aed33f8864869cd0681ca137700b8ab44` (verified CP171 merge).  
**Branch:** `cp172-enchants-item-effects` · **Draft PR:** [#25](https://github.com/layoung3/WarcraftSim/pull/25).  
**Status:** Partial source-backed executable foundation; **not universal enchant/proc coverage or CP172 complete**. Do not merge without local validation and explicit owner approval.

## Source and accuracy controls

- The embedded catalog `WarcraftSim.Api/Catalog/forever-equipment-cp172-effects.json` retains source URL, requested client build, independent verification labels, numeric values and whether an effect is provisional or unmodeled.
- It adds 9 enchant spell definitions to the 3 CP170 enchant entries (12 total), 2 source-backed trinket IDs to the 54 equipment IDs from CP171 (56 total), and four explicit effect definitions (3 executable; 1 unmodeled Crusader). The source set is a **curated working subset**, not a broad complete catalog yet.
- Permanent static examples: weapon Spell Power +30 (22749), gloves Agility +7 (13815), bracer Stamina +9 (20011), boots Stamina +7 (20020), chest Health +100 (20026), bracer MP5 +4 (23801), gloves Agility +10 (20012), and the retained +5 damage weapon enchant (20031).
- Temporary oil: Brilliant Wizard Oil (25122): +36 Spell Power and +1% spell critical chance; flagged temporary for 1800 seconds. A separate `TemporaryEnchantSpellId` may coexist with one permanent enchant; both are independently validated and additive while present. Existing simulated encounter durations are max 1200 seconds, so the oil does not expire mid-sim.
- On-use: Talisman of Ephemeral Power (18820) applies 175 Spell Power for 15 seconds on a 90-second cooldown and shares an on-use category. Published [Forever Wowhead](https://www.wowhead.com/forever/item=18820/talisman-of-ephemeral-power), [MetaForge](https://metaforge.app/wow-forever/database/armor/talisman-of-ephemeral-power). This source is a Forever tooltip, not an independently attested live server drop.
- Physical haste on-use: Kiss of the Spider (22954; spell 28866) applies 20% attack speed for 15 seconds with 120-second cooldown. [Forever item](https://www.wowhead.com/forever/item=22954/kiss-of-the-spider) and [effect spell](https://www.wowhead.com/forever/spell=28866/kiss-of-the-spider). It updates active Hunter swing timers at buff activation/expiration. Its static crit tooltip is **provisional**: Wowhead displays 1%, [MetaForge](https://metaforge.app/wow-forever/database/armor/kiss-of-the-spider) displays 1.4% for Forever. The current seed uses 1% with conflict noted, not a certified resolved value.
- Proc: Fiery Weapon (13898) generates sourced 40 Fire damage on eligible melee hits using a 6-PPM **explicit Classic proxy**, NOT a measured Forever trigger rate. Its proc effects execute with the existing `AbilityExecutor.ExecuteBackgroundDirectDamage`; no recursive retriggering. Crusader (20034) remains **unmodeled** because rate/server-specific mechanics are not established; only reference/provisional metadata retained.

## Architecture / runtime integration

- CP170/171 item catalog and stat resolver stay authoritative; the CP172 catalog is **additively embedded**, not a competing resolver. Original stat-only loadouts and class mechanics are preserved by default.
- `ForeverEquipmentEffectProcessor` is one per-run, seeded, event-driven executor for equipped item/enchant on-use and procs; `AuraManager` handles buff application and expiration; `AbilityExecutor` handles actual proc damage/attribution; shared actor cooldown groups are used for trinkets. Owner-only triggered damage excludes pet events and equipment-originated damage to avoid recursive effects. Applications are added to `SimulationRunResult.CustomMetrics`.
- `ForeverEquipmentDynamicSpellScaling` converts exact, linear Mage/Warlock Spell Power terms into the shared live scaling path **only** if a temporary SP effect is equipped. Pre-existing snapshots/rotations remain unchanged without the effect; non-linear/unsupported mechanics are not silently transformed.
- Mage, Warlock and Marksmanship/Survival/Beast Mastery Hunter now use the same shared processor. Physical haste reschedules active hunter auto-attack swings. Pet inheritance of **dynamically changing** owner stats, non-linear spell snapshots, dynamic Crit/Haste cast-time updates, all temporary weapon-imbue categories and all specific proc-trigger policies **are not comprehensively certified**.
- The API adds `GET /api/forever/equipment/enchants` with name, slot, temporary, modeled filters, `GET /enchants/{spellId}`, `GET /effects`, `GET /effects/{key}`. The existing `POST /resolve` includes selected executable effect metadata.
- Unsupported item links remain `not-modeled`. CP171 static set effects still activate only at correct thresholds; unmodeled 4-piece and other nonstatic set effects remain inert. This checkpoint did not invent new dynamic set-bonus rates.

## Repeatable build-pinned source normalization

The Python script `tools/ForeverEquipmentImportCli/normalize_enchants.py` reads `SpellItemEnchantment.csv` and `SpellEffect.csv` from the existing CP171 pinned-download directory, checks `acquisition-report.json` table SHA-256s, joins application spell IDs, directly maps supported numeric `Effect_*/EffectArg_*/EffectPointsMin_*` stats, and retains other effect types unmodeled. It labels outputs `client-derived-candidate-not-reviewed`; no bulk record automatically becomes executable.

Run after a permitted local fetch (Python 3.10+):

```powershell
python tools/ForeverEquipmentImportCli/download_client_csv.py fetch --build 1.60.1.70707 --directory local-forever-db2 --tables SpellItemEnchantment SpellEffect
python tools/ForeverEquipmentImportCli/normalize_enchants.py --directory local-forever-db2 --build 1.60.1.70707 --output local-forever-db2/cp172-enchant-candidates.json
```

Review source-build matches, effect/trigger IDs and applicable slots before adding a source-backed executable record. GitHub Actions allows a manually dispatched run with pinned build; normal PR CI runs synthetic source fixtures without repeatedly downloading external data.

## Tests / validation

`CP172_EquipmentEffects.cs` exercises permanent and temporary stat effects and exclusivity, API discovery/serialization, live Mage and Warlock on-use damage, exactly timed Talisman 90s activations/15s expiry, Hunter owner-only Fiery damage, Kiss of the Spider swing timer changes and effect attribution. The new `test_normalize_enchants.py` checks structured source joins, missing/incompatible table checksums, unknown proc preservation, build mismatch and deterministic serialization. All CP166–CP171 suites remain in scope.

```powershell
dotnet build WarcraftSim.slnx -c Release -warnaserror
dotnet test WarcraftSim.slnx -c Release --no-build
python -m unittest discover -s tools/ForeverEquipmentImportCli -p "test_*.py" -v
python tools/ForeverEquipmentImportCli/integration_smoke.py --build
```

CI must pass on the final branch HEAD before describing this checkpoint as ready for local validation. Do not merge or deploy without approval.

## Next CP172 work before completion

A genuinely broad build-pinned enchant catalog with more source-verified slot restrictions, executable temporary weapon/rune/imbue categories, accurate PPM/ICD/charge and shared-cooldown interactions, stat-buff stacking, dynamic owner/pet stat and swing inheritance, effect-trigger eligibility covering critical strikes, spell hits, periodic/damage taken/healing, and dynamic nonstatic set bonuses. Source/server uncertainty stays explicit rather than silently fabricating effects.
