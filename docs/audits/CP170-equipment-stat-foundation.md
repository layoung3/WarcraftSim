# CP170 — Shared equipment and character stat foundation

**Status:** executable foundation implemented, final-head CI and owner local validation required before merge. **Starting main:** `f6cbf33f948c49119ff13cb227553c3d594bc112`. **Branch/PR:** `cp170-shared-equipment-stat-foundation` / #23. Scope is versioned offline equipment and Mage/Warlock/Hunter solo DPS integration, **not** a full BiS or proc/consumable database.

## Reuse, not duplication

- **Core** already has `CharacterEquipmentLoadout`, `CharacterEquipmentItem`, `CharacterEquipmentModifier`, `CharacterEquipmentStatContributor`, source attribution, and ordered base/equipment/derived-stat contributors. Those types and `ForeverCharacterSimulationRuntimeCalculatorFactory` remain the authoritative stat aggregator.
- **Warrior** already has `ForeverEquipmentCatalog`, versioned `forever-equipment-v1.json`, unique/set/enchant examples, and `ForeverSupportEffectCatalog`. The existing Warrior resolution path is preserved unchanged.
- **CP170** extends the same catalog models with verification metadata (source build, quality, class/subclass, allowed class, unique-equipped group), and gives Core equipment items optional server-owned `ItemId`/`EnchantSpellId`. Do not trust client-supplied item `Stats` or `Modifiers` whenever an item ID is supplied.

`ForeverSharedEquipmentResolver.Resolve(loadout,classKey,level,race)` is the one class-neutral normalization/validation entry point. It returns normalized Core loadout, **RawStats** before conversion, **Stats** after the same existing Forever contributors, eligible weapons, and one provenance entry per real item. Slots: head, neck, shoulder, back, chest, wrist, hands, waist, legs, feet, finger1/2, trinket1/2, main-hand, off-hand, ranged. Some common UI aliases normalize to those canonical keys. Empty/partial sets work; duplicates, unknown IDs/slots, wrong class/level/faction, explicit unique-group conflicts, two-hander/off-hand combinations, unsupported enchants, or user stat overrides of catalog IDs fail with clear messages.

**Precedence:** raw base character stats + each server-owned catalog item + source-owned enchant + each qualified set bonus **once**, then Forever class-aware rating and primary-stat conversions. Static effects remain stat contributions, not fabricated procs. `ForeverEquipmentStatBridge` maps the resolved stats into the pre-existing Mage/Warlock `CharacterProfile` calculator or Hunter spec tuning/weapon models; the original user request is never mutated. A separate raw vs derived stat collection prevents double counting Hunter primary-stat attack power. Final hit/crit chances cap at 100%; pet inheritance continues to consume the owner's *bonus* hit as before. Class-specific temporary states still use their existing combat engine.

## Verified starter item and source provenance

The existing Warrior starter items remain; existing Classic-proxy values remain explicitly labeled. New all-caster starter example:

| ID | Item | Raw client stats | Weapon | Provenance |
|---|---|---|---|---|
| 19102 | Crackling Staff | 25 Intellect, 25 Spell Power | 138–207, speed 3.10, two-hand Staff | [Forever client tooltip](https://theforeverera.com/en/items/19102/), build 1.60.1.70532 |

The 19102 catalog entry records quality, source URL, build and client-verification status. Unverified `classic-proxy-*` entries **cannot** enter CP170's shared DPS resolver; no Classic-only value is silently promoted. Older Warrior scenarios retain their separate historical opt-in/provisional behavior.

## Public item data and legal importer

Current candidate providers include [The Forever Era](https://theforeverera.com/en/items/), [Bind on Forever](https://bindonforever.com/items), and [AHledger's documented open-data page](https://ahledger.com/data). **AHledger explicitly permits the public read-only API with attribution** ([developer terms](https://ahledger.com/developers)), and documents `GET https://api.ahledger.com/v1/items/{itemId}?game=forever`, which returns stable item identity, quality, class and level **but does not document the full raw combat-stat/effect schema**. `IForeverEquipmentMetadataSource`, `AhledgerForeverItemMetadataSource` and `ForeverEquipmentMetadataImporter` use that supported HTTPS endpoint, preserve raw JSON, stable Blizzard ID, source URL, provider, import date and build metadata. Imported records are `metadata-only` until a separate source-verified client tooltip/effects snapshot is curated; `PromoteToSimulationItem` intentionally rejects them. The full source-stat data of external item sites is not scraped or assumed legal for automatic copying.

The versioned embedded local `forever-equipment-v1.json` provides deterministic offline simulations. New catalog snapshots must record provenance, distinguish live client values from Classic placeholders, and be reviewed against changes to source builds. No external website is contacted during a simulation.

## Public API

- `GET /api/forever/equipment/items?search=staff&slot=main-hand&page=1&pageSize=25` — versioned local catalog, stable IDs, total count and paging (1–200 entries).
- `GET /api/forever/equipment/items/19102` — item metadata, source and verification status.
- `POST /api/forever/equipment/resolve` — validates the following shape and returns normalized loadout, raw/derived stats, weapons, catalog version and individual source attribution.

```json
{
  "classKey": "mage",
  "level": 60,
  "equipment": {
    "items": [
      {"slotKey": "main-hand", "itemId": 19102}
    ]
  }
}
```

For Mage/Warlock simulations, populate the existing `request.character.equipment.items` with the same item-selection shape. For MM/Survival/BM use the new `request.equipment.items` field. No full set is required. Legacy `CharacterProfile.Equipment` stat-only items and all prior numeric-only request modes remain operational. To support faction-locked items, send `race` with the equipment API request or `character.race` on caster profiles. Racial combat effects remain a later checkpoint.

## Executable regression evidence

| Target | Scope / test |
|---|---|
| Shared loader | `CP170_SharedEquipmentResolution`: slots, partial loadouts, unique, 2H conflict, class/level/faction, Classic-proxy rejection, supported enchants and set threshold, raw vs derived stats, HTTP endpoint JSON |
| Importer | `CP170_EquipmentImporter`: valid/invalid ID and provenance, cannot invent combat stats, public read-only endpoint exercised with a deterministic fake HTTP handler |
| Mage | `CP170_EquipmentDpsIntegration`: real equipped 19102 increases owner spell power/damage for Arcane/Fire/Frost with identical seeds and no request mutation |
| Warlock | Same integration suite: Demonology owner and active Imp both respond to 19102 Spell Power, independently attributed without duplicated pet damage |
| Hunter | Same suite: equipped ranged Bow changes successive Auto Shot intervals/damage; attack-power Ring increases owner and pet; Survival main-hand weapon and BM owner's item change actual outcomes |
| Compatibility | Existing legacy numeric/statically authored equipment, request/response serialization, all CP166–CP169 regressions remain in full .NET 10 suite |

## Limits and next checkpoints

Server coefficients, pet AP/health inheritance, attack-speed normalization, unverified weapon proficiencies, raw rating-to-combat conversions where unpublished, exact racial restrictions for new races, nonstat item equip effects, target-specific resistances, and full derived health/mana conversions remain subject to explicit source updates. The present Hunter/health conversions reuse existing stat-tuning assumptions. Not yet modeled: all possible enchants, item procs/on-use trinkets, consumables, racials, set-proc effects, raid/world buffs, Phase-1 BiS sets, dynamic gear swaps, guild/raid simulation or frontend overhaul. Each later effect should emit a typed source-aware modifier/event into the **existing** aura, cooldown, proc and contributor pipeline, not a new class simulator.

**Acceptance and approval:** strict `dotnet build WarcraftSim.slnx -c Release -warnaserror` and `dotnet test WarcraftSim.slnx -c Release --no-build` on final branch HEAD, zero warnings and no regression failures. **Do not merge PR #23** until owner locally validates and explicitly approves.
