# CP174 Owner Full-Build Acceptance Corrections (2026-10-31)

## Owner evidence — inspect the actual export, not just presets

Owner-exported `warcraftsim-build.json` and `warcraftsim-simulation-report.json` (2026-10-31, Orc Arcane Mage, 120 seconds, seed 132001, historical-provisional baseline). The original report was produced by a local build with sourceCommit=`not-stamped-local-build`, so it is **not** a pinned final-head CI report.

Original typed simulation request (not inferred):

- `itemizedTalentMode=true`, 51 allocated ranks: Arcane 32 / Fire 16 / Frost 3, with Arcane Power and Presence of Mind both at rank 1.
- `useArcanePower=false`, `usePresenceOfMind=false`; the automatic rotation did not execute either cooldown. It used Arcane Wrath, Arcane Blast and missiles instead.
- `useMajorManaPotion=true` and `majorManaPotionCharges=1`, despite no Major Mana Potion in the owner's selected consumables. The original timeline records a Major Mana Potion cast at **19.533835586466164 seconds**.
- `targetDebuffs=[curse-of-elements,faerie-fire-rank4]`. Faerie Fire Rank 4 was applied at **0 seconds** and expired/removed at **40 seconds**, with no automatic reapplication. Its base duration from the catalog is 40 seconds.
- All 16 equipped slots include the 19102 staff with **1219580** permanent spell enchant and **25122** Brilliant Wizard Oil, plus trinkets **19339** and **18820**. External buffs include Arcane Intellect and Fortitude; food/flask/elixirs and four Mage gem tiers/runes were configured. These specific values are reproduced in `CP174_OwnerFullBuildAcceptance.cs`.

## Corrections

1. The builder now initializes `useArcanePower` and `usePresenceOfMind` from the actual learned ranks when loading an itemized Mage preset. Two explicitly labeled controls in Encounter > Ability priority allow the user to disable either offensive cooldown. Rank removal immediately clears its usage flag. Mage auto-priority inserts only these explicitly enabled learned DPS cooldowns before damage fillers, regardless of starting specialization. Custom priority remains user-owned. It does **not** insert unrelated defensive/utility talents.
2. Major Mana Potion is an explicit icon tile and charge counter under Buffs & Consumables, with quantity 0–20. The builder now starts with `useMajorManaPotion=false` and `majorManaPotionCharges=0`; an older imported build without an explicit potion entry also resolves to no prepared potions. The typed legacy Mage API retains backward compatibility, but the new UI cannot silently create an extra potion. Mage mana gems and runes continue through the existing shared-item processor; the engine-owned potion priority is retained only for positively prepared/selected potion charges.
3. Selected external target debuffs offer `targetDebuffMode=initial-only|maintained`. The builder defaults to **maintained** (external raid support keeps debuffs up), and the legacy API defaults to **initial-only** unless explicitly overridden. Maintained mode reapplies the same aura on its real expiration at 40 and 80 seconds in the owner's 120-second run; old stat deltas are removed before the fresh instance contributes, so no extra spell damage or stacked armor reduction is created. Initial-only remains exactly the old natural 40-second expiration.
4. The Enchants dialog only offers permanent enchant entries and a small Remove permanent enchant action; no temporary weapon selection or navigation button remains there. A noninteractive status note can describe the weapon's currently active Wizard Oil. Changing Wizard Oil remains exclusive to Buffs & Consumables using the existing CP172 `temporaryEnchantSpellId` and server compatibility checks.
5. Saved build JSON now exposes `preparedCombatConsumables` in addition to the existing typed encounter charges, and records Mage potion charges/enablement, learned cooldown toggles, and external upkeep mode. Completed reports already preserve the original typed request and raw combat events.

## Verification gates

- `Cp174OwnerFullBuildAcceptanceTests.OwnerItemizedRequestExecutesLearnedCooldownsWithoutUnselectedPotion`: complete 51-rank typed Orc Mage, 16 slots, both trinkets, both weapon enhancements, externals and 120 seconds; Arcane Power/Presence of Mind must cast; unselected Mana Potion must not.
- `SelectedPotionActivatesOnlyWithPositivePreparedInventory`: zero uses when off, one when explicitly prepared, zero with no charges.
- `ExternallyMaintainedFaerieFireRefreshesAtFortyAndEightyWithoutStatStacking`: initial-only application [0], maintained applications [0,40,80], removal at 40/80, no externally sourced combat damage.
- `LearnedOffensiveCooldownsRespectExplicitUseFlags`: same ranked talents with controls turned off do not cast, turned on do.
- Real Chrome tests inspect initial Mage cooldown toggles and zero potion selection, exercise external upkeep policy, confirm no temporary effect selectors in permanent enchant editor, and inspect exported server JSON for zero unsolicited potions.

**Important limits:** No claim about game-server DPS accuracy. Maintained external debuffs are modeled as externally sustained without a player GCD or mana cost; initial-only is the correct setting when no external support is assumed. The owner export's local build SHA was not stamped, so its original numerical DPS should not be conflated with deterministic fixed-head CI comparisons.

**Merge gate:** Keep PR #27 draft and unmerged until owner local acceptance and explicit approval.
