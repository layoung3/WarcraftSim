# CP173 — Character stats, racials and external modifiers

Starting point: merged main `32a74a1b334b5267cf09ed97bd6cb791a6d98cf7` (CP172 PR #25).

## Source and safety contract

- Retain all CP170–CP172 stat/gear/enchant behavior, class presets and previous tests.
- Implement opt-in source-identified modifiers through the existing character buff and simulation-event pipelines.
- No automatically active external buffs, no fabricated client progression/racial proc effects, and no double-use of CP172 weapon oil effects.
- Target debuffs are owned by the target aura/metadata, not inserted as attacker permanent stats.
- For current Forever data, directly established numeric values are usable **provisionally**; masked/hotfix fields and server-only mechanics are unmodeled.
- Prefer a shared collector/resolver plus thin Mage/Warlock/Hunter bridges. Preserve legacy explicit stats when no selections are made.

## Work plan

1. Add source-backed modifier catalog, validation, stacking/unique group policy, opt-in simulation selections, and a discovery API.
2. Connect approved self buffs to existing derived stat pipeline and actual Mage/Warlock/Hunter simulations. Integrate targeted vulnerability through the shared hit/damage pipeline with target ownership and nonstacking safeguards.
3. Add true build-pinned source census pipeline, reproducibility checks and integration tests. Broaden safe content where data allows.
4. Review race/class/level baseline sourcing and implement only verified/provisionally explicit ranges. Report unsupported combinations rather than inventing progression.
5. Strict .NET 10 Release build, full xUnit suite, Python import checks and exact-HEAD GitHub Actions validation.

**Draft PR only. Do not merge without explicit owner approval.**

## CP173 implementation status (2026-10-30)

**Branch:** `cp173-character-buffs-consumables-racials`; **draft PR:** [#26](https://github.com/layoung3/WarcraftSim/pull/26). **Do not merge without local validation and explicit approval.**

### Actual data acquisition and exact source identification

CI acquired five **real, build-pinned `1.60.1.70707` Wago DB2 CSV tables**: `Spell` (32,030 records), `SpellEffect` (42,859), `PlayerExpectedStat` (1,107), `ChrRaces` (58), `ChrClasses` (9). The normalizer found **21,320 aura effect records** across **15,946 candidate spells**. It correctly marked **zero** of the bulk raw aura records executable: unknown aura semantics, trigger rate, targets, ranks and exclusive categories must not be guessed. Full checksum-pinned source candidate artifact appears in GitHub Actions as `cp173-real-70707-character-modifier-census`.

The exact source inventory was retained in `tools/ForeverEquipmentImportCli/normalize_modifiers.py` and tested with checksum tampering, deterministic repeated exports, and preserved content-set IDs. Source SHA-256:
- SpellEffect: `119652fb66750946d5956592a7144a2159b08839498fc998d3789844141c7eec`
- Spell: `b3a1c2a2e7129a78063f6f81dba6c209526aa6ffd78e2f585877eaf05bac9da0`
- PlayerExpectedStat: `4a269bc8f15d0f449cf67b31941f32f3fa3cd92490b97b2d6820c0e5afa3d1ff`
- ChrRaces: `5e595ec3d19a134d5ea9145a66ecb4c8dd25e8001a704da9ac145630aafcc51c`
- ChrClasses: `e08c2f00e1652ff6857734915d649a80d8a2a86419986f59dbe82ba3a6975597`

For levels **1–60**, exactly **540** records cover all **nine classes**, all with `ContentSetID=0`; `WarcraftSim.Api/Catalog/forever-cp173-base-mana-70707.json` holds **all directly encoded BaseMana values**, including Mage level60 **1213**, Warlock **1373**, Hunter **1720**, Priest **1376**, and Warrior **0**; this is embedded in the API as a read-only source-only progression. The pinned table **does not contain complete race-specific Strength, Agility, Stamina, Intellect or Spirit baseline attributes**. Accordingly, the character builder reports those gaps and continues to require an explicit verified or user-provided base profile for executable simulations instead of inventing Classic-expansion stat curves. The existing class preset numerics remain unchanged for backward compatibility, including whenever a class 60 configured resource differs from this directly sourced BaseMana field.

### Source-identified selectable and executable subset

- **10** faction-specific playable race records, **nine** distinct race families (Skyborne faction-specific), with class and faction validation and level 1–60 selection.
- **12** index entries for racial passive/active mechanics: supported subset (Human/Orc critical bonuses, Human Spirit, Night Elf Dodge, Skyborne Haste, Gnome maximum Mana, Tauren maximum Health, Orc Blood Fury) plus explicitly unmodeled/partially modeled racial behavior. Orc Blood Fury (published spell **20572**) is the first opt-in active with an actual 15s aura, 180s cooldown, derived Attack/Spell Power deltas, reporting and pet inheritance.
- **7** curated external selections: four external buffs (Arcane Intellect, Fortitude, Battle Shout, Blessing of Might), two elixirs (Greater Arcane Elixir, Mongoose) and one target Curse of Elements. Source builds and published URLs are attached; all are opt-in, use server-owned keys, apply class/level and group-exclusion rules, and reject a static pre-pull effect that would expire mid-encounter without a refresh policy.
- **7** existing/new executable combat-use item definitions: existing Mana Ruby, two Mana Runes, Mighty Rage Potion, Dense Dynamite, Goblin Sapper Charge, and CP173 Major Healing Potion (**item 13446 / spell 17534**, restores 1050–1750 actual Health on the existing shared 120-second combat-potion lockout, with inventory and health-threshold gating). No duplicate Brilliant Wizard Oil engine, which remains CP172-authoritative.
- One supported externally configured enemy Curse of Elements uses `AuraManager` and a 300s source-owned `cp173-curse-of-elements` target aura, giving a documented +10% magic damage vulnerability only on **eligible actual impacts before expiration**. It is not applied to attacker stats. Warlock cannot also select its own competing curse. Unknown other target debuffs remain unmodeled.

### Real combat comparisons (deterministic xUnit)

At a known successful intermediate CP173 CI HEAD (pre-final source-progressive base Mana addition):
- Mage Greater Arcane Elixir: **347.3231 → 362.0924 DPS**.
- Warlock Greater Arcane Elixir: **208.6653 → 217.2492 DPS**.
- Mage external Curse of Elements: **330.5929 → 368.1463 DPS**.
- Orc Mage Blood Fury: **323.8422 → 326.6248 DPS**, 1 activation, 15s recorded uptime.
- Marksmanship Hunter Elixir of the Mongoose: owner damage **15,471.16 → 16,354.11**, pet damage **5,651.39 → 5,783.56**.
The ordinary none-selected paths preserve default numeric presets. Buff stat contributors attach source ID `buff:cp173-...`; source preview endpoints expose selected talents, catalog equipment, resistance and derived stats.

### Acceptance boundaries and outstanding independently implementable work

CP173 **is not a universal character-generation implementation**. Race-specific five primary baseline attributes were absent from the acquired tables; character builder refuses unsupported empty base profiles rather than fabricating values. Runtime still accepts level-60 existing class simulation presets, not reparameterized level 1–60 combat spell ranks. Many race active proc/CD behaviors, unsupported world/raid buffs and debuffs, group stacking rank interactions, drinking/food recovery, flasks, weapon stones/imbues, raid roles, and further consumable effects remain unmodeled. Do not imply all 15,946 raw aura candidate spells are selectable or executable. Current source values may be masked/overridden by server hotfixes.

### Developer/local gate

```powershell
dotnet build WarcraftSim.slnx -c Release -warnaserror
dotnet test WarcraftSim.slnx -c Release --no-build
python -m unittest discover -s tools/ForeverEquipmentImportCli -p "test_*.py" -v
python tools/ForeverEquipmentImportCli/integration_smoke.py --build
```

Exact final-HEAD GitHub Actions must pass with zero warnings, full xUnit, Python unit/integration and real source reproducibility; prior successful runs are not proof for untested later commits. Owner must locally validate and explicitly authorize each PR merge. 
