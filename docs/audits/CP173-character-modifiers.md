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


## CP173 continuation: level-60-only character building and broader executable modifiers

**Scope correction:** Level 60 is the acceptance target. Lower-level character-primary progression and lower-level combat are explicitly deferred.

### Naked character source inventory and fallback policy

The pinned 1.60.1.70707 public client ingestion establishes all nine class-level Base Mana series (540 rows), limited stat conversion fields, 58 ChrRaces records and nine ChrClasses records, but **not** the five race/class-specific naked primary attributes nor a server-attested per-race max-HP or weapon AP table. `ChrRaces` includes race identity metadata, not automatically the level-60 naked primary totals. No independently verified naked level-60 observations were supplied at this checkpoint.

- `forever-cp173-level60-baselines.json`: versioned source ledger, **zero approved race/class observations** (deliberately empty). It refuses unknown combos. Inline observations must carry explicit build, source reference, timestamp and a visible verification status; claims inside user-provided metadata are not an independent attestation.
- `ForeverLevel60Baseline.Generate` replaces only the five explicitly observed *naked primary attributes*, deep-clones the original `CharacterProfile` and retains talents, equipment, buffs and enchants. The existing CP170–173 order derives stats once and source IDs are retained. Racial Spirit and the Gnome/Tauren resource-cap passives are not duplicated when their observed naked values already contain them.
- Optional `Level60Baseline` / `UseReviewedLevel60Baseline` in Mage, Warlock and Hunter requests. Mage/Warlock use observed primaries, naked HP/Mana (if provided) and class conversion; Hunter additionally **requires** measured unbuffed melee AP, ranged AP and Armor to avoid silently retaining illustrative tuning. No user uploads are ever auto-promoted into the approved ledger.
- `GET /api/forever/character/level60-baseline-coverage` gives every race/class's approved/unobserved status and source-pinned Base Mana; `POST /api/forever/character/generate-level60` resolves a validated observed build with selected talents/gear/enchants/consumables/buffs, returning full contributor attribution and a simulation-ready profile. `POST /resolve` remains backward compatible.
- Old numerical/preset mode remains unchanged when no observed/approved baseline is explicitly chosen. Its values are illustrative, not validated naked level-60 Forever stats.

### Consumables and target modifiers

Added **twelve** sourced provisional static consumable entries to the original seven selections: three mutually exclusive flasks (Supreme Power +150 SP, Titans +1200 Health, Distilled Wisdom +2000 Mana); four elixirs (Sages +18 Intellect/Spirit, Greater Agility +25 Agility, Greater Strength +25 Strength, Lesser Fortitude +120 Health); and five mutually exclusive *Forever-specific* Well Fed foods (Grilled Squid +1% crit, Nightfin Soup +22 Spell Power, Runn Tum Tuber Surprise +15 Intellect, Smoked Desert Dumplings +20 Strength, Chimaerok Chops +25 Stamina). All have item/source IDs, URL, provisional status and expiration restrictions. Unsupported food application spell IDs stay unassigned (0), not fabricated. `Flask`, `battle-elixir`, `guardian-elixir`, `well-fed-food` groups are exclusive within group; duration-too-long simulations refuse static prebuffs without an explicit refresh policy. All are opt-in.

Added source-backed external Faerie Fire (Rank 4, spell 9907), **-505 target Armor for 40 seconds**, applied/removable through `AuraManager` to the *enemy*; all three Hunter specialization physical mitigators now consume current target armor, including source-owned timed debuffs. Curse of the Elements remains source-owned magical vulnerability. Existing potion/engineering item processing and CP172 temporary weapon oil/enchant exclusivity remain unchanged; no duplicate oil processor.

### Race actives

Expanded shared `ForeverRacialActiveProcessor` from Orc Blood Fury to include **Troll Berserking (+10% haste for 10s/180s)** and **Night Elf Elune's Light (+10% critical chance for 15s/180s)**, using independent source-owned auras, actor stat updates, expiry, metrics and Hunter swing-progress rescheduling. Preserve legacy Orc metrics. Troll's possible 15-second launch duration is not certified as current 70707; the 10-second published client value is provisional. Corrected both Skyborne Wind Blessed entries to **1% haste** from an older 3% preview, retaining uncertainty.

### Reproducible read-only local client evidence request (only if needed)

The user has an older beta and newer preinstall on Windows. **Do not send entire game folders.** Run this command separately for each install after replacing the directory paths, in a local PowerShell window, WITHOUT launching or modifying the game:

```powershell
./tools/ForeverEquipmentImportCli/collect_local_forever_build.ps1 -InstallDirectory "D:\\Games\\WoWForeverBeta" -OutputDirectory "$env:USERPROFILE\\Desktop\\ForeverBetaEvidence"
./tools/ForeverEquipmentImportCli/collect_local_forever_build.ps1 -InstallDirectory "D:\\Games\\WoWForeverPreinstall" -OutputDirectory "$env:USERPROFILE\\Desktop\\ForeverPreinstallEvidence"
```

Review and share only each generated `forever-client-build-inventory.json`. The script records available `.build.info`, product/build metadata and executable versions (not account data, archives, or game content). If source investigation identifies a directly relevant `.db2`/DBC, ask only for the specific permitted CSV export, its exact client build, and SHA256. `PlayerExpectedStat`, `ChrRaces` and `ChrClasses` are already pinned via public Wago, so reuploading them alone will **not** solve the naked-primary-stat blocker.

If a genuine, unbuffed **level-60** character is available in a running, legitimately accessed client: record selected race, class, exact `/dump GetBuildInfo()` output, five `UnitStat("player", index)` raw base/current values for indices 1–5, naked maximum HP/Mana, melee/ranged AP and Armor with no equipment, talents, buffs, raid aura or temporary effect. Include a screenshot/structured output and record which racial passives persist. If max-level characters cannot yet exist, these observations cannot be produced and the reviewed baseline ledger remains empty. Never request credentials, account logs or other players' data.

### Remaining functionality and source gaps

1. Approved official Forever naked primary stats for each race/class at level 60: **zero** complete observations yet. The new pathway is mechanically executable with explicit observation but not an automatic server-verified race/class generator for all combinations.
2. Sourced additional race actives, verified prebuff timing/refresh, additional rank and debuff group interactions, precise percentage modifier ordering where server hotfixes differ, and per-character Flask/Well Fed item inventory rules.
3. Provisional current game values may differ from eventual launch; the 70707 schema alone is insufficient to resolve server-only mechanics. Lower-level support is deferred by user instruction.

Do not merge before owner local validation and explicit per-PR approval.
