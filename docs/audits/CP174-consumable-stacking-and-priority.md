# CP174 — Forever level-60 consumable evidence and modeled policy (2026-10-31)

This is an evidence/provenance ledger, **not** a claim that all server-only cooldown behavior has been verified on launch build 1.60.1.70707.

## Effect coexistence is not an activation cooldown

The Forever client-era explanation (build 70707) identifies **no TBC Battle/Guardian elixir slots**. Different elixirs can coexist. Flask tooltips state only one flask effect at a time. Elixir/flask activation triggers a short (3-second) drink cooldown, **not** broad buff exclusivity. A flask and nonduplicate elixir probably coexist, but the exact live-server combination has not been independently tested.

Evidence: https://bindonforever.com/articles/can-you-stack-elixirs-wow-forever and https://www.reddit.com/r/wowforever/comments/1ww0yde/can_you_stack_elixirs_in_forever/

Implemented catalog groups distinguish one-flask, one-food and known effect families. Mongoose and Greater Agility are modeled as a competing agility effect family. Greater Arcane Elixir and Mongoose are allowed concurrently. Duplicate Battle Shout / Blessing of Might effects retain the existing external melee AP exclusion. The frontend uses the same catalog metadata as the server; no separate TBC category logic.

**Outstanding:** imported scrolls, weapon oils/stones, and more item-effect-to-aura relationships are outside the small approved CP173 catalog. No unverified scroll, oil or stone has been promoted. Buff refresh remains guarded by the existing encounter-duration validation.

## Prepared combat-use inventory and shared cooldown groups

| Item | Modeled effect | Personal / shared policy | Evidence and qualifications |
| --- | --- | --- | --- |
| Mana Ruby | 1,000–1,200 mana | unique prepared one-use gem, 120s shared with other gems | https://bindonforever.com/spell/10054 ; cooldown group provisional |
| Mana Citrine | 775–925 mana | as above | https://bindonforever.com/spell/10053 |
| Mana Jade | 550–650 mana | as above | https://bindonforever.com/spell/3552 |
| Mana Agate | 375–425 mana | as above | https://bindonforever.com/spell/759 |
| Dark Rune / Demonic Rune | 900–1,500 mana, 600–1,000 health cost | own 120s rune group separate from mana gems; one use per inventory charge | https://bindonforever.com/item/20520 ; https://metaforge.app/wow-forever/database/trade-goods/demonic-rune ; cooldown group provisional |
| Dense Dynamite | client tooltip 400 Fire damage (simulation 340–460, mean 400) | 60s engineering explosives group, Engineering 250 | https://www.wowhead.com/forever/item=18641/dense-dynamite |
| Goblin Sapper Charge | 450–750 Fire to enemies, 375–625 self damage | 300s personal; modeled 60s shared explosive lockout, Engineering 205 | https://metaforge.app/wow-forever/database/consumables/goblin-sapper-charge ; Sapper's sharing with dynamite is **provisional**, and differs between some published guides |

Primary profession evidence: https://wowfdb.com/professions/engineering/ says bombs/dynamite/grenades share one 60s group. A Forever Warlock rotation guide at https://www.wowhead.com/forever/guide/classes/warlock/demonology/rotation-cooldowns-pve-dps describes Sapper as separate from other explosives, while older Wowhead Sapper comments describe a 60s shared lockout. The current conservative engine behavior shares a lockout **without asserting confirmation**.

The item scheduler now compares **expected useful mana restored** and **single-target expected explosive damage** on each rotation decision, checks current cooldown readiness/charges/health safety and selects deterministically. It never manufactures effects: it invokes the actual engine ability executor. Successful decisions are returned in `combatItemDecisionReasons`, and use counts in `combatItemUses`. The UI exposes per-item charges; unmodeled class/quantity settings are not shown as active.

## Unresolved gameplay limitations

- Current inventory only represents Mage gem tier and approved runes/explosives, not the full consumable collection.
- Mana-gem shared lockouts, distinct rune lockouts, Sapper interaction and server-side proc/mitigation behavior require real Forever observations. The engine's damage calculations use explicit 340–460 Dense Dynamite spread for a 400 mean despite published tooltip fixed 400.
- Mage and Warlock typed runners expose `CombatItems`. Hunter typed requests do not yet expose `CombatItems`; the UI must not claim they can consume these items.
- Ranged, travel, multi-target explosive opportunity and rune risk valuation remain approximations in the solo one-target scheduler.
- Legacy talent defaults include unitemized point totals. CP174 currently displays the discrepancy and removes manual aggregate inputs, but a separate source-backed talent-rank migration plus backend enforcement is required before claiming automatic allocation is authoritative.

## Suggested local review

Open `/solo-dps.html` on the CP174 branch. In Buffs & Consumables, select Greater Arcane Elixir plus Mongoose, then attempt Greater Agility (blocked). Enable Mage mana gems at one use each and configure at least a 260-second encounter; inspect `combatItemUses`, `combatItemDecisionReasons` and the captured timeline. Configure Engineering 250 to enable Dynamite and Sapper, then compare the chosen order with their cooldown windows. Recheck tooltips and selection after save/load.
