# CP167 — Level-60 class ability and talent reconciliation (work in progress)

**Checkpoint:** 2026-10-28; draft [PR #20](https://github.com/layoung3/WarcraftSim/pull/20) only, not approved for merge. This is an **initial runtime-path inspection**, not a signed-off class-versus-client spellbook audit. Preserve CP166 results; re-evaluate all previously audited ranks against contemporary Forever client exports before final certification.

## Evidence and status rules

- **Verified and complete:** all relevant observed gameplay mechanics, talent gates and numeric values have current independent evidence, an executable ability path, deterministic outcome tests and no unresolved high-impact interactions.
- **Executable / numerically provisional:** real combat events exist and outcome tests pass, but coefficients or hit/proc/geometry values remain unverified in Forever.
- **Partial:** one or more important execution paths (other specializations, target switching, companion interaction, ability targeting, pet or totem ownership, mana refund) remain missing.
- **Registered only:** talent/ability appears in a catalog but lacks tested runtime behavior.
- **Missing:** current client confirms a PvE-relevant ability not represented by an executable path.
- **Encounter dependent:** legitimate effect requires target/position/raid state not yet represented, and no fabricated AI or aura benefit may substitute.
- **Unsupported/removed:** an ability exists in other expansions or has been removed in Forever; exclude it unless current client evidence re-adds it.

## First-pass runtime registry → executor trace

This table identifies concrete code paths but is **not** the exhaustive per-rank match against the live spellbook. A reference to a talent string in code does not prove the effect functions correctly.

| Class | Specialization(s) | Current executable entry point | Initial completeness status and next audit |
|---|---|---|---|
| Warrior | Arms | `ForeverWarriorSimRunner`, `ForeverWarriorArmsTalentConfigurator`, `WarriorStanceDanceProcessor`, `WarriorSweepingStrikesProcessor` | **Partial/reverify**: major core weapon, stance and proc paths exist; validate every current Arms rank, target interaction and optional Berserker Rage |
| Warrior | Fury | `ForeverWarriorSimRunner`, `WarriorRageTalentProcessor`, `WarriorWeaponTalentProcessor`, `ForeverBloodthirstFactory` | **Partial/reverify**: verify all Fury talent ranks, cooldown overlap and dual-wield hit/proc profiles |
| Warrior | Protection | `WarriorProtectionAbilityFactory`, `WarriorProtectionProcessor`, `WarriorIncomingTalentProcessor`, `WarriorTankSpikeProcessor` | **Partial/reverify**: defensive talents and healing-dependent burst metrics exist; compare all live mitigation/utility spells, shield/stance conditions |
| Rogue | Assassination | `ForeverRogueSpellbook`, `RogueCombatProcessor`, `RoguePoisonProcessor`, `RogueSimRunner` | **Partial/reverify**: Mutilate/poisons and proc rules need exact live rank checks and multi-enemy finishing ownership review |
| Rogue | Combat | Same spellbook and runner; `RogueThreatAndDefenseProcessor` | **Partial/reverify**: Blade Flurry, Adrenaline Rush, Hack and Slash, weapon-hand and target-sweep interactions must be checked against current client |
| Rogue | Subtlety | Same spellbook and runner, including stealth/positioning gates | **Partial/reverify**: Hemorrhage, Premeditation, Preparation and Ghostly Strike require every-talent execution trace |
| Hunter | Marksmanship | `MarksmanshipHunterSimRunner`, `ForeverMarksmanshipHunterSpellbook`, `HunterAspectProcessor` | **Executable/partial**: Hawk, Beast, Volley and dynamic RAP; other Aspect transitions, Tranquilizing Shot and actual range/geometry need review |
| Hunter | Survival | `SurvivalHunterSimRunner`, `ForeverSurvivalHunterSpellbook`, `HunterAspectProcessor` | **Executable/partial**: Beast/Hawk are separate melee/ranged AP, Volley is opt-in without forced stance switch; verify ranged shot AP, trap ownership and tracked-target effects |
| Hunter | Beast Mastery | `BeastMasteryHunterSimRunner`, `ForeverBeastMasteryHunterSpellbook`, `HunterAspectProcessor` | **Executable/partial**: Hawk/Beast/Volley, pet hit and expertise preserved, live AP on Aimed/Multi; Deadly Aspects Hawk Auto Shot and explicit owner Beast melee-auto proc are executable with shared 12-second +30% haste and swing rescheduling; non-BM paths still require certification; temporary Hawk AP is not transferred to pet |
| Warlock | Affliction | `WarlockSimRunner`, `ForeverWarlockDpsSpellbook`, `DamageToSelfHealingProcessor` | **Executable/provisional**: Drain Life and Siphon Life use resolved damage; DoT coefficients, snapshot and curse group effects need live verification |
| Warlock | Demonology | `WarlockDemonCompanion`, `WarlockDemonicBrandProcessor`, `WarlockSimRunner` | **Partial**: Demonic Brand / pet family school provisional; **Decimation 2 ranks now executable** through current rank 45/90% Soul Fire cooldown and 20/40% faster shard-free execution (review pending); Soul Fire shard accounting remains an abstraction |
| Warlock | Destruction | `WarlockSimRunner`, `WarlockConflagrateConsumptionProcessor`, `WarlockShadowburnShardProcessor` | **Executable/partial**: Searing Pain/Shadowburn, Rain of Fire; verify threat tables, death-window refunds, direct/periodic target and target-area behavior |
| Mage | Arcane | `MageSimRunner`, `MageAdaptiveRotation`, `ForeverMageDpsSpellbook` | **Executable/partial**: PoM and Arcane Power, Clearcasting, AoE; **Magic Attunement** confirmed baseline but Amplify/Dampen Magic effects absent; Magic Absorption 1s ICD needs audit |
| Mage | Fire | Same runner/spellbook, `MageIgniteRollingProcessor` | **Executable/partial**: Combustion, Scorch, Flamestrike; verify multi-target critical/rolling Ignite and source-specific DoT interaction |
| Mage | Frost | Same runner/spellbook, Frost proc/Cold Snap state | **Executable/partial**: Blizzard and Cold Snap; complete target-level slow/control and current rank comparison before certification |
| Priest | Discipline | `HolyPriestPreviewRunner`, `ForeverPriestHealingSpellbook`, `PriestMajorCooldownProcessor` | **Executable/partial**: Inner Focus, Power Infusion, Penance; external ally PI target and group-heal modifiers need fully executable support |
| Priest | Holy | Same healer runner, `PriestCombatIntegration` | **Executable/partial**: configured heals, heals-over-time and prayer abilities; Holy Nova, rank-by-rank effects and raid-group targeting remain unverified |
| Priest | Shadow | `ShadowPriestSimRunner`, `ForeverPriestShadowSpellbook`, `ForeverPriestShadowReactiveProcessor` | **Executable/partial**: Shadow Weaving, Mind Flay and execute, cross-tree Inner Focus/PI; confirm current Shadow rank coefficients and triggered effects |
| Shaman | Elemental | `ElementalShamanSimRunner`, `ForeverElementalShamanSpellbook`, `ShamanTotemLifecycleProcessor` | **Executable/partial**: direct/periodic spells, Fire Nova, Lightning Shield, independently paid Call/Recall; radius, totem actors and Totemic Projection remain open |
| Shaman | Enhancement | `EnhancementShamanSimRunner`, `ForeverEnhancementShamanReactiveProcessor`, `ShamanWeaponImbueProcessor`, `ShamanTotemLifecycleProcessor` | **Executable/partial**: MH/OH imbues, WF nonrecursive proc, totem calls; precise Flametongue/Frostbrand probabilities and Rockbiter threat not measured |
| Shaman | Restoration | `RestorationShamanSimRunner`, `RestorationShamanCombatProcessor`, `ForeverRestorationShamanSpellbook` | **Executable/partial**: opt-in shared Earth/Fire/Water/Air ownership, three programmed Calls and Recall use the existing manager while healer Water Shield/Mana Tide/Healing Stream continue through the existing heal processor; external ally effect/radius and legacy water pulse reconciliation require full certification |
| Druid | Balance | `BalanceDruidSimRunner`, `ForeverBalanceDruidSpellbook`, `ForeverDruidSharedCooldowns` | **Executable/partial**: Eclipse, Innervate, Barkskin; current Moonkin talents and ally-innervate targeting unresolved |
| Druid | Feral Cat | `FeralDruidSimRunner`, `FeralDruidCombatProcessor`, `ForeverFeralDruidSpellbook` | **Executable/partial**: source-owned combo totals keyed to actual target, two-target switching and Rip hit-application, missed-finisher point retention and **provisional** 80% Energy refund; still verify bleed refresh snapshots and exact client refund |
| Druid | Feral Bear | Same runner, `FeralDruidTankMetricsProcessor`, `FeralBearIncomingRageProcessor` | **Executable/partial**: defensive and incoming-Rage metrics, Swipe/Lacerate; verify form-specific cooldowns and tank utility |
| Druid | Restoration | `RestorationDruidSimRunner`, `ForeverRestorationDruidSpellbook` | **Executable/partial**: HoTs, Swiftmend, Barkskin/Innervate; direct ally Innervate and other cooldowns, group heal targeting, current ranks need comparison |

## Confirmed new or expanded omissions (outside original 38-row CP167 inventory)

| Item | Current source / scope | Status |
|---|---|---|
| Warlock Decimation (Demonology, 2 ranks) | Current 70532 tree: 45/90% Soul Fire CD reduction; Shadow Bolt/Searing Pain below 35% enable 20/40% faster shard-free Soul Fire for 10 seconds, 3/6% damage bonus | **Executable with client rank values; pending full certification**: target-health-before-hit eligibility, 10s aura, cast timing, cast-reserved zero shard cost, 45/90% cooldown and 3/6% direct damage bonus in `CP167_S_WarlockDecimation`; test miss/expiration edge cases in final audit |
| Mage Magic Attunement | Current baseline Mage effect enhances Amplify Magic / Dampen Magic by 50%; not a selectable Arcane talent | **Encounter dependent/partial**: no corresponding raid magical damage/healing modifiers in solo runners; never grant phantom DPS |
| Hunter Deadly Aspects (BM five ranks) | Hawk Auto Shot procs 2/4/6/8/10%; Beast melee auto similarly 30% haste for 12 seconds | **Executable in BM with opt-in melee engagement**: Hawk Auto Shot and owner Beast melee auto use 2/4/6/8/10% rank chance, +30% haste for 12s and rescheduled swings, `CP167_T_HunterDeadlyAspects`; external other-spec and target-position eligibility still open |
| Shaman Call of Ancestors and Call of Spirits | Current 70532 CLIENT: Ancestors level 30 / Spirits level 40, three-second casts and separate Totem Bar presets; Blizzard Oct 7 prose reverses the two names | **Executable / partial**: Elemental/Enhancement plus opt-in Restoration use one four-slot executor, `CP167_U_RestorationShamanTotems`; no simulated raid-range checks or active totem actors |
| Shaman Totemic Projection | Current class spell moves active totems to a new location | **Encounter dependent / missing**: requires position representation; no fictitious flat effect |
| Hunter other Aspect and PvE utility ranks | Current level-60 spellbook differs from Classic, including Aspect of the Beast melee AP | **Partially inventoried**; remaining verified baseline ranks and positioning effects must be cataloged, not guessed |

## CP167 continued implementation evidence — October 28, 2026

These are **focused source-to-runtime comparisons**, not completion certificates for the 25 specializations. No P1 row is elevated to independently verified until all associated rank/encounter paths pass the full acceptance gate.

| Source-verified feature | Current client / rule | Concrete engine route | Outcome tests | Residual limitation |
|---|---|---|---|---|
| Warlock Demonology Decimation ranks 1–2 | [70532 talent](https://theforeverera.com/en/talents/warlock/decimation/): permanent Soul Fire cooldown -45%/-90%; eligible Shadow Bolt/Searing Pain under 35% gains +3%/+6% damage and refreshes 10s faster, shard-free Soul Fire (20%/40% faster) | `ForeverWarlockTalentBuild` → `ForeverWarlockDpsSpellbook` → `WarlockDecimationProcessor` `IAbilityTimingProvider`, `AbilityExecutor` and shard resource; hit bonus in `WarlockRollResolver` | `CP167_S_WarlockDecimation` rank/cost, hit/damage, no-window-above-threshold, cast timing, JSON | Miss versus cast-trigger server policy, buff restoration across simultaneous transitions, pet/raid triggers not independently certified |
| Hunter Beast Mastery Deadly Aspects ranks 1–5 | [70532 talent](https://theforeverera.com/en/talents/hunter/deadly-aspects/): owner Hawk Auto Shot or owner Beast melee-auto has 2/4/6/8/10% chance of +30% matching swing haste for 12s | `BeastMasteryHunterSimRunner.BeastState` live owner-only Damage listener; `AutoAttackProcessor.RescheduleForHasteChange`, exclusive `HunterAspectProcessor`; explicit optional owner melee stream | `CP167_T_HunterDeadlyAspects`: owner swing proc and expiration, pet ineligibility, rank chances, API round trip | Geometry, switching aspects during swing and non-BM specialization talent builds are not fully reconciled |
| Shaman programmed Calls — client-level correction | [70532 Ancestors](https://theforeverera.com/en/spells/shaman/call-of-the-ancestors/) level 30, Spirits level 40. **Official October 7 news text interchanged those labels**; use live client ranks pending in-game confirmation | `ForeverShamanTotemCalls.Call` reuses four-slot `ShamanTotemLifecycleProcessor`; corrected regression expectations | `CP167_O_ShamanTotemLifecycle`, `CP167_U_RestorationShamanTotems` | No position/radius/totem enemy targetability |
| Shaman Restoration four slots | [Forever Shaman totem rules](https://news.blizzard.com/en-gb/article/24302097/world-of-warcraft-forever-class-deep-dives-mage-and-shaman): programmed four-element cast; 25% paid-Mana Recall | `RestorationShamanSimRunner` opt-in, shared `ShamanTotemLifecycleProcessor`, `ShamanFireTotemProcessor`, existing `RestorationShamanCombatProcessor` for Mana Tide/Healing Stream pulses, one-shot `RestorationShamanTotemRecallProcessor` | `CP167_U_RestorationShamanTotems`: four auras, combined spend, Water replacement, refund and serialization | Default preview unchanged; exact multi-ally Windfury/other group buffs, real radius and destruction deferred |

**Current additional-omission categories:** Decimation and BM Deadly Aspects Beast-path are now executable, and all three Shaman programmed Calls have initial shared lifecycle integration across Elemental, Enhancement and optional Restoration. Magic Attunement and Totemic Projection remain encounter-dependent or incomplete; remaining independently implementable omissions must be found with the complete 25-spec comparison. These resolutions **do not** imply any specialization is certified.

## Remaining certification gates

1. Read each of the 25 specialization's full contemporaneous spell and talent catalogs, **including all historical/unselected ranks**, and mark each row by source/version, executable test, numeric confidence, and actual PvE effect.
2. Close all confirmed independently executable high-impact omissions, including any remaining disconnected Decimation and other talent proc interactions, and repair all disconnected validator-only ranks.
3. Exercise all default presets and opt-in spell combinations; add deterministic target switch, pet, channel, periodic, missed application, proc, aura replacement and cost accounting tests.
4. Keep the existing original 38 CP167 entries and prior CP166 regressions; never count a mere dictionary entry, non-applying aura or documentation note as mechanically complete.
5. Only after final strict CI, owner local validation and explicit merge authorization may draft PR #20 be merged.

**Source posture:** client spellbook/tooltip and contemporaneous official hotfix notes establish mechanic *intent*, not full in-game numeric verification. For the above checked gaps: [Decimation](https://theforeverera.com/en/talents/warlock/decimation/), [Magic Attunement](https://www.wowhead.com/forever/spell=12606/magic-attunement), [Deadly Aspects](https://talcalc.com/hunter/talents/deadly-aspects/), [Shaman spellbook](https://questing.wiki/wow-forever/spells/shaman/).

## Group 1 — explicit talent-by-talent comparison (Mage and Warlock)

**Source**: [Mage 54-talent 70532 tree](https://theforeverera.com/en/talents/mage/), [Warlock 52-talent 70532 tree](https://theforeverera.com/en/talents/warlock/), both dated October 24, 2026. The rank cap in this table is the source-visible per-talent cap, **not a claim that every talent prerequisite or numerical effect has been certified**. Runtime references were inspected in current PR #20, not `main`. A detected runtime path is not a passing outcome test; status is deliberately conservative. A catalog-only talent requires functional implementation. All specialization trees share the same class spells, subject to learned talent gates.

| Class / tree | Current talent | Rank cap | Runtime disposition | Concrete code path | Remaining verification |
|---|---|---:|---|---|---|
| Mage / Arcane | [Wand Specialization](https://theforeverera.com/en/talents/mage/wand-specialization/) | 2 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Mage / Arcane | [Arcane Focus](https://theforeverera.com/en/talents/mage/arcane-focus/) | 5 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Arcane | [Improved Channeling](https://theforeverera.com/en/talents/mage/improved-channeling/) | 5 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Mage / Arcane | [Missile Barrage](https://theforeverera.com/en/talents/mage/missile-barrage/) | 2 | Executable route; cert. pending | `MageSimRunner.cs`, `MageAdaptiveRotation.cs` | Rank effect requires final outcome audit |
| Mage / Arcane | [Arcane Subtlety](https://theforeverera.com/en/talents/mage/arcane-subtlety/) | 2 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Mage / Arcane | [Magic Absorption](https://theforeverera.com/en/talents/mage/magic-absorption/) | 2 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Mage / Arcane | [Arcane Concentration](https://theforeverera.com/en/talents/mage/arcane-concentration/) | 5 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Arcane | [Arcane Resilience](https://theforeverera.com/en/talents/mage/arcane-resilience/) | 2 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Mage / Arcane | [Arcane Geometry](https://theforeverera.com/en/talents/mage/arcane-geometry/) | 2 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Mage / Arcane | [Arcane Impact](https://theforeverera.com/en/talents/mage/arcane-impact/) | 3 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Arcane | [Arcane Blast](https://theforeverera.com/en/talents/mage/arcane-blast/) | 1 | Executable route; cert. pending | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Arcane | [Arcane Shielding](https://theforeverera.com/en/talents/mage/arcane-shielding/) | 2 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Mage / Arcane | [Improved Counterspell](https://theforeverera.com/en/talents/mage/improved-counterspell/) | 2 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Mage / Arcane | [Arcane Meditation](https://theforeverera.com/en/talents/mage/arcane-meditation/) | 3 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Arcane | [Arcane Wrath](https://theforeverera.com/en/talents/mage/arcane-wrath/) | 1 | Executable route; cert. pending | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Arcane | [Presence of Mind](https://theforeverera.com/en/talents/mage/presence-of-mind/) | 1 | Executable route; cert. pending | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Arcane | [Arcane Mind](https://theforeverera.com/en/talents/mage/arcane-mind/) | 5 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Arcane | [Arcane Instability](https://theforeverera.com/en/talents/mage/arcane-instability/) | 3 | Executable route; cert. pending | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Arcane | [Arcane Power](https://theforeverera.com/en/talents/mage/arcane-power/) | 1 | Executable route; cert. pending | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Fire | [Improved Fire Blast](https://theforeverera.com/en/talents/mage/improved-fire-blast/) | 2 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Mage / Fire | [Incineration](https://theforeverera.com/en/talents/mage/incineration/) | 3 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Fire | [Improved Fireball](https://theforeverera.com/en/talents/mage/improved-fireball/) | 5 | Executable route; cert. pending | `ForeverMageDpsSpellbook.cs` | Rank effect requires final outcome audit |
| Mage / Fire | [Ignite](https://theforeverera.com/en/talents/mage/ignite/) | 5 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Fire | [Flame Throwing](https://theforeverera.com/en/talents/mage/flame-throwing/) | 2 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Mage / Fire | [Impact](https://theforeverera.com/en/talents/mage/impact/) | 3 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Mage / Fire | [Burning Soul](https://theforeverera.com/en/talents/mage/burning-soul/) | 3 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Mage / Fire | [Improved Flamestrike](https://theforeverera.com/en/talents/mage/improved-flamestrike/) | 3 | Executable route; cert. pending | `ForeverMageDpsSpellbook.cs` | Rank effect requires final outcome audit |
| Mage / Fire | [Pyroblast](https://theforeverera.com/en/talents/mage/pyroblast/) | 1 | Executable route; cert. pending | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Fire | [Improved Scorch](https://theforeverera.com/en/talents/mage/improved-scorch/) | 3 | Executable route; cert. pending | `MageSimRunner.cs`, `MageImprovedScorchProcessor.cs` | Rank effect requires final outcome audit |
| Mage / Fire | [Improved Fire Ward](https://theforeverera.com/en/talents/mage/improved-fire-ward/) | 2 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Mage / Fire | [Heating Up](https://theforeverera.com/en/talents/mage/heating-up/) | 1 | Executable route; cert. pending | `MageSimRunner.cs`, `MageAdaptiveRotation.cs` | Rank effect requires final outcome audit |
| Mage / Fire | [Master of Elements](https://theforeverera.com/en/talents/mage/master-of-elements/) | 3 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Fire | [Critical Mass](https://theforeverera.com/en/talents/mage/critical-mass/) | 5 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Fire | [Blast Wave](https://theforeverera.com/en/talents/mage/blast-wave/) | 1 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Mage / Fire | [Fire Power](https://theforeverera.com/en/talents/mage/fire-power/) | 3 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Fire | [Combustion](https://theforeverera.com/en/talents/mage/combustion/) | 1 | Executable route; cert. pending | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Frost | [Frost Warding](https://theforeverera.com/en/talents/mage/frost-warding/) | 2 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Mage / Frost | [Improved Frostbolt](https://theforeverera.com/en/talents/mage/improved-frostbolt/) | 5 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Mage / Frost | [Elemental Precision](https://theforeverera.com/en/talents/mage/elemental-precision/) | 3 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Frost | [Ice Shards](https://theforeverera.com/en/talents/mage/ice-shards/) | 5 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Frost | [Frostbite](https://theforeverera.com/en/talents/mage/frostbite/) | 3 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Mage / Frost | [Improved Frost Nova](https://theforeverera.com/en/talents/mage/improved-frost-nova/) | 2 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Mage / Frost | [Permafrost](https://theforeverera.com/en/talents/mage/permafrost/) | 3 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Mage / Frost | [Frost Channeling](https://theforeverera.com/en/talents/mage/frost-channeling/) | 3 | Executable route; cert. pending | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Frost | [Improved Blizzard](https://theforeverera.com/en/talents/mage/improved-blizzard/) | 3 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Mage / Frost | [Ice Lance](https://theforeverera.com/en/talents/mage/ice-lance/) | 1 | Executable route; cert. pending | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Frost | [Arctic Reach](https://theforeverera.com/en/talents/mage/arctic-reach/) | 2 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Mage / Frost | [Ice Block](https://theforeverera.com/en/talents/mage/ice-block/) | 1 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Mage / Frost | [Shatter](https://theforeverera.com/en/talents/mage/shatter/) | 3 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Frost | [Improved Cone of Cold](https://theforeverera.com/en/talents/mage/improved-cone-of-cold/) | 3 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Mage / Frost | [Cold Snap](https://theforeverera.com/en/talents/mage/cold-snap/) | 1 | Executable route; cert. pending | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Frost | [Fingers of Frost](https://theforeverera.com/en/talents/mage/fingers-of-frost/) | 2 | Executable route; cert. pending | `MageSimRunner.cs`, `MageAdaptiveRotation.cs` | Rank effect requires final outcome audit |
| Mage / Frost | [Winter's Chill](https://theforeverera.com/en/talents/mage/winters-chill/) | 5 | Executable route; cert. pending | `MageSimRunner.cs` | Rank effect requires final outcome audit |
| Mage / Frost | [Ice Barrier](https://theforeverera.com/en/talents/mage/ice-barrier/) | 1 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Affliction | [Improved Life Tap](https://theforeverera.com/en/talents/warlock/improved-life-tap/) | 2 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs` | Rank effect requires final outcome audit |
| Warlock / Affliction | [Suppression](https://theforeverera.com/en/talents/warlock/suppression/) | 5 | Executable route; cert. pending | `WarlockSimRunner.cs` | Rank effect requires final outcome audit |
| Warlock / Affliction | [Improved Corruption](https://theforeverera.com/en/talents/warlock/improved-corruption/) | 5 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs` | Rank effect requires final outcome audit |
| Warlock / Affliction | [Grim Reach](https://theforeverera.com/en/talents/warlock/grim-reach/) | 2 | Registered only / disconnected | No executable route found | Catalog acceptance is not functionality |
| Warlock / Affliction | [Soul Harvest](https://theforeverera.com/en/talents/warlock/soul-harvest/) | 2 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Affliction | [Improved Drains](https://theforeverera.com/en/talents/warlock/improved-drains/) | 3 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs` | Rank effect requires final outcome audit |
| Warlock / Affliction | [Improved Bane of Agony](https://theforeverera.com/en/talents/warlock/improved-bane-of-agony/) | 2 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs` | Rank effect requires final outcome audit |
| Warlock / Affliction | [Fel Concentration](https://theforeverera.com/en/talents/warlock/fel-concentration/) | 3 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Affliction | [Amplify Curse](https://theforeverera.com/en/talents/warlock/amplify-curse/) | 1 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Affliction | [Pandemic](https://theforeverera.com/en/talents/warlock/pandemic/) | 3 | Executable route; cert. pending | `WarlockSimRunner.cs` | Rank effect requires final outcome audit |
| Warlock / Affliction | [Malevolence](https://theforeverera.com/en/talents/warlock/malevolence/) | 5 | Executable route; cert. pending | `WarlockSimRunner.cs` | Rank effect requires final outcome audit |
| Warlock / Affliction | [Nightfall](https://theforeverera.com/en/talents/warlock/nightfall/) | 2 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Affliction | [Curse of Exhaustion](https://theforeverera.com/en/talents/warlock/curse-of-exhaustion/) | 1 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Warlock / Affliction | [Siphon Life](https://theforeverera.com/en/talents/warlock/siphon-life/) | 1 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Rank effect requires final outcome audit |
| Warlock / Affliction | [Soul Siphon](https://theforeverera.com/en/talents/warlock/soul-siphon/) | 3 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Affliction | [Shadow Mastery](https://theforeverera.com/en/talents/warlock/shadow-mastery/) | 5 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs` | Rank effect requires final outcome audit |
| Warlock / Affliction | [Wrack](https://theforeverera.com/en/talents/warlock/wrack/) | 1 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Rank effect requires final outcome audit |
| Warlock / Demonology | [Improved Health Funnel](https://theforeverera.com/en/talents/warlock/improved-health-funnel/) | 2 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Demonology | [Improved Imp](https://theforeverera.com/en/talents/warlock/improved-imp/) | 3 | Executable route; cert. pending | `WarlockDemonCompanion.cs` | Rank effect requires final outcome audit |
| Warlock / Demonology | [Demonic Embrace](https://theforeverera.com/en/talents/warlock/demonic-embrace/) | 5 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Demonology | [Unholy Power](https://theforeverera.com/en/talents/warlock/unholy-power/) | 5 | Executable route; cert. pending | `WarlockSimRunner.cs` | Rank effect requires final outcome audit |
| Warlock / Demonology | [Demonic Aegis](https://theforeverera.com/en/talents/warlock/demonic-aegis/) | 2 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Demonology | [Improved Voidwalker](https://theforeverera.com/en/talents/warlock/improved-voidwalker/) | 3 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Demonology | [Fel Vitality](https://theforeverera.com/en/talents/warlock/fel-vitality/) | 3 | Executable route; cert. pending | `WarlockSimRunner.cs`, `WarlockDemonCompanion.cs` | Rank effect requires final outcome audit |
| Warlock / Demonology | [Demonic Energies](https://theforeverera.com/en/talents/warlock/demonic-energies/) | 2 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Demonology | [Improved Sayaad](https://theforeverera.com/en/talents/warlock/improved-sayaad/) | 3 | Executable route; cert. pending | `WarlockDemonCompanion.cs` | Rank effect requires final outcome audit |
| Warlock / Demonology | [Demonic Sacrifice](https://theforeverera.com/en/talents/warlock/demonic-sacrifice/) | 1 | Executable route; cert. pending | `WarlockSimRunner.cs`, `WarlockDemonCompanion.cs` | Rank effect requires final outcome audit |
| Warlock / Demonology | [Master Summoner](https://theforeverera.com/en/talents/warlock/master-summoner/) | 2 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Demonology | [Decimation](https://theforeverera.com/en/talents/warlock/decimation/) | 2 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Rank effect requires final outcome audit |
| Warlock / Demonology | [Fel Domination](https://theforeverera.com/en/talents/warlock/fel-domination/) | 1 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Demonology | [Demonic Brand](https://theforeverera.com/en/talents/warlock/demonic-brand/) | 3 | Executable route; cert. pending | `WarlockSimRunner.cs`, `WarlockDemonicBrandProcessor.cs` | Rank effect requires final outcome audit |
| Warlock / Demonology | [Improved Felhunter](https://theforeverera.com/en/talents/warlock/improved-felhunter/) | 3 | Executable route; cert. pending | `WarlockDemonCompanion.cs` | Rank effect requires final outcome audit |
| Warlock / Demonology | [Soul Link](https://theforeverera.com/en/talents/warlock/soul-link/) | 1 | Executable route; cert. pending | `WarlockSimRunner.cs` | Rank effect requires final outcome audit |
| Warlock / Demonology | [Demonic Knowledge](https://theforeverera.com/en/talents/warlock/demonic-knowledge/) | 3 | Executable route; cert. pending | `WarlockSimRunner.cs`, `WarlockDemonCompanion.cs` | Rank effect requires final outcome audit |
| Warlock / Demonology | [Master Demonologist](https://theforeverera.com/en/talents/warlock/master-demonologist/) | 5 | Executable route; cert. pending | `WarlockSimRunner.cs` | Rank effect requires final outcome audit |
| Warlock / Demonology | [Demonic Pact](https://theforeverera.com/en/talents/warlock/demonic-pact/) | 1 | Executable route; cert. pending | `WarlockDemonCompanion.cs`, `WarlockPrePullSacrificeProcessor.cs` | Rank effect requires final outcome audit |
| Warlock / Destruction | [Destructive Reach](https://theforeverera.com/en/talents/warlock/destructive-reach/) | 2 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Destruction | [Improved Shadow Bolt](https://theforeverera.com/en/talents/warlock/improved-shadow-bolt/) | 5 | Executable route; cert. pending | `WarlockSimRunner.cs`, `WarlockImprovedShadowBoltProcessor.cs` | Rank effect requires final outcome audit |
| Warlock / Destruction | [Bane](https://theforeverera.com/en/talents/warlock/bane/) | 5 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs` | Rank effect requires final outcome audit |
| Warlock / Destruction | [Molten Skin](https://theforeverera.com/en/talents/warlock/molten-skin/) | 5 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Destruction | [Cataclysm](https://theforeverera.com/en/talents/warlock/cataclysm/) | 3 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs` | Rank effect requires final outcome audit |
| Warlock / Destruction | [Aftermath](https://theforeverera.com/en/talents/warlock/aftermath/) | 5 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Warlock / Destruction | [Ruin](https://theforeverera.com/en/talents/warlock/ruin/) | 5 | Executable route; cert. pending | `WarlockSimRunner.cs` | Rank effect requires final outcome audit |
| Warlock / Destruction | [Shadowburn](https://theforeverera.com/en/talents/warlock/shadowburn/) | 1 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs`, `WarlockAdaptiveRotation.cs` | Rank effect requires final outcome audit |
| Warlock / Destruction | [Intensity](https://theforeverera.com/en/talents/warlock/intensity/) | 3 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Destruction | [Agonizing Flames](https://theforeverera.com/en/talents/warlock/agonizing-flames/) | 3 | Executable route; cert. pending | `WarlockSimRunner.cs` | Rank effect requires final outcome audit |
| Warlock / Destruction | [Conflagrate](https://theforeverera.com/en/talents/warlock/conflagrate/) | 1 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Rank effect requires final outcome audit |
| Warlock / Destruction | [Pyroclasm](https://theforeverera.com/en/talents/warlock/pyroclasm/) | 2 | Encounter/position limited | No executable route found | Encounter gate; no phantom numerical benefit |
| Warlock / Destruction | [Bane of Havoc](https://theforeverera.com/en/talents/warlock/bane-of-havoc/) | 1 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs`, `WarlockAdaptiveRotation.cs` | Rank effect requires final outcome audit |
| Warlock / Destruction | [Fire and Brimstone](https://theforeverera.com/en/talents/warlock/fire-and-brimstone/) | 3 | Missing from executable catalog | No executable route found | Confirmed talent in client; implementation candidate |
| Warlock / Destruction | [Shadow and Flame](https://theforeverera.com/en/talents/warlock/shadow-and-flame/) | 5 | Executable route; cert. pending | `WarlockSimRunner.cs` | Rank effect requires final outcome audit |
| Warlock / Destruction | [Incinerate](https://theforeverera.com/en/talents/warlock/incinerate/) | 1 | Executable route; cert. pending | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Rank effect requires final outcome audit |

This table enumerates all **106** source-listed talent entries. Preliminary static cross-reference counts: **62 executable references; 1 accepted without a confirmed runtime reference; 29 not in the executable catalog; 14 omitted/encounter-limited**. These are *inspection bins*, not certification numbers. Their counts can change as execution tests uncover disconnected effects or new class patches alter sources.

## Group 1 — level-60 spellbook registry comparison (Mage and Warlock)

**Source rank lists:** [Mage 70658 spellbook, 62 entries](https://foreverchanges.pro/spellbook/mage), [Warlock 70630 spellbook, 69 entries](https://talentsforever.com/warlock/abilities). *Rank count* is the number of client ranks available by level 60 and is **not the number of separately runnable sim abilities**. The cited spellbook pages show all rank levels and tooltips; later individual-rank numerical checks remain pending for many abilities. The Warlock source separates 18 Affliction, 25 Demonology, 10 Destruction and 16 Demon spells; Mage has 38 Arcane, 10 Fire, 12 Frost and two Comprehension abilities. Talented abilities exist only with a learned rank; caster/conjuring/teleportation spells are preserved as catalog rows without being granted artificial DPS.

| Class / spellbook tab | Current spell | Ranks to 60 | Runtime status | Inspected execution files | Rank/effect confidence |
|---|---|---:|---|---|---|
| Mage / Arcane | Arcane Intellect | 5 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Arcane | Conjure Water | 7 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Conjure Food | 7 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Arcane Missiles | 8 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Arcane | Polymorph | 4 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Dampen Magic | 5 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Arcane | Slow Fall | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Arcane Explosion | 6 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageAdaptiveRotation.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Arcane | Amplify Magic | 4 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Arcane | Remove Lesser Curse | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Arcane Blast | 5 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Arcane | Blink | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Evocation | 1 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Arcane | Mana Shield | 6 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Arcane | Teleport: Ironforge | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Teleport: Orgrimmar | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Teleport: Stormwind | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Teleport: Undercity | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Counterspell | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Arcane Wrath | 4 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Arcane | Conjure Mana Agate | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Teleport: Darnassus | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Teleport: Thunder Bluff | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Mage Armor | 3 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Arcane | Conjure Mana Jade | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Portal: Ironforge | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Portal: Orgrimmar | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Portal: Stormwind | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Portal: Undercity | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Conjure Mana Citrine | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Portal: Darnassus | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Portal: Thunder Bluff | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Teleport: Dalaran | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Arcane Brilliance | 1 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Arcane | Conjure Mana Ruby | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Polymorph: Cow | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Arcane | Presence of Mind | 1 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Arcane | Arcane Power | 1 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Fire | Fireball | 12 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Fire | Fire Blast | 7 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Fire | Flamestrike | 6 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageAdaptiveRotation.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Fire | Fire Ward | 5 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Fire | Pyroblast | 8 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Fire | Scorch | 7 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Fire | Blast Wave | 5 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Fire | Frostfire Bolt | 3 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Fire | Felfire | 1 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Fire | Combustion | 1 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Frost | Frost Armor | 3 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Frost | Frostbolt | 11 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Frost | Frost Nova | 4 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Frost | Blizzard | 6 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageAdaptiveRotation.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Frost | Ice Lance | 6 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Frost | Frost Ward | 5 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Frost | Frost Blast | 3 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Frost | Cone of Cold | 5 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Frost | Ice Armor | 4 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Frost | Ice Barrier | 4 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Frost | Ice Block | 1 | Missing executable path | — | Does not generate free simulated benefit |
| Mage / Frost | Cold Snap | 1 | Executable / partial | `ForeverMageDpsSpellbook.cs`, `MageSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Mage / Comprehension | Comprehend Scroll | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Mage / Comprehension | Study | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Affliction | Amplify Curse | 1 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Affliction | Bane of Agony | 6 | Executable / partial | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Affliction | Bane of Doom | 1 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Affliction | Corruption | 7 | Executable / partial | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Affliction | Curse of Exhaustion | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Affliction | Curse of Recklessness | 4 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Affliction | Curse of the Elements | 4 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Affliction | Curse of Tongues | 2 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Affliction | Curse of Weakness | 6 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Affliction | Death Coil | 3 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Affliction | Drain Life | 6 | Executable / partial | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Affliction | Drain Mana | 4 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Affliction | Drain Soul | 4 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Affliction | Fear | 3 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Affliction | Howl of Terror | 2 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Affliction | Life Tap | 6 | Executable / partial | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Affliction | Siphon Life | 4 | Executable / partial | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Affliction | Wrack | 1 | Executable / partial | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Demonology | Banish | 2 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Create Firestone | 4 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Create Healthstone | 5 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Create Soulstone | 5 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Create Spellstone | 3 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Demon Armor | 5 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Demonology | Demon Skin | 2 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Demonology | Demonic Sacrifice | 1 | Executable / partial | `WarlockSimRunner.cs`, `WarlockDemonCompanion.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Demonology | Detect Invisibility | 3 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Eye of Kilrogg | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Fel Domination | 1 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Demonology | Health Funnel | 7 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Demonology | Inferno | 1 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Demonology | Ritual of Doom | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Ritual of Summoning | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Sense Demons | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Shadow Ward | 4 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Demonology | Soul Link | 1 | Executable / partial | `WarlockSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Demonology | Subjugate Demon | 3 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Summon Felhunter | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Summon Imp | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Summon Incubus | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Summon Succubus | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Summon Voidwalker | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demonology | Unending Breath | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Destruction | Bane of Havoc | 1 | Executable / partial | `ForeverWarlockDpsSpellbook.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Destruction | Conflagrate | 6 | Executable / partial | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Destruction | Hellfire | 3 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Destruction | Immolate | 7 | Executable / partial | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Destruction | Incinerate | 3 | Executable / partial | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Destruction | Rain of Fire | 4 | Executable / partial | `ForeverWarlockDpsSpellbook.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Destruction | Searing Pain | 6 | Executable / partial | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Destruction | Shadow Bolt | 9 | Executable / partial | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Destruction | Shadowburn | 6 | Executable / partial | `ForeverWarlockDpsSpellbook.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Destruction | Soul Fire | 2 | Executable / partial | `ForeverWarlockDpsSpellbook.cs`, `WarlockSimRunner.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Demons | Blood Pact | 5 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Demons | Consume Shadows | 6 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Demons | Devour Magic | 4 | Executable / partial | `WarlockDemonCompanion.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Demons | Fire Shield | 5 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Demons | Firebolt | 7 | Executable / partial | `WarlockDemonCompanion.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Demons | Lash of Pain | 6 | Executable / partial | `WarlockDemonCompanion.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Demons | Lesser Invisibility | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demons | Paranoia | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demons | Phase Shift | 1 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demons | Sacrifice | 6 | Executable / partial | `WarlockDemonCompanion.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Demons | Seduction | 1 | Executable / partial | `WarlockDemonCompanion.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Demons | Soothing Kiss | 4 | Non-DPS utility / encounter | — | Does not generate free simulated benefit |
| Warlock / Demons | Spell Lock | 2 | Executable / partial | `WarlockDemonCompanion.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Demons | Suffering | 4 | Executable / partial | `WarlockDemonCompanion.cs` | Compare current highest rank and per-effect outcomes |
| Warlock / Demons | Tainted Blood | 4 | Missing executable path | — | Does not generate free simulated benefit |
| Warlock / Demons | Torment | 6 | Executable / partial | `WarlockDemonCompanion.cs` | Compare current highest rank and per-effect outcomes |

The table contains **131** current spell names (Mage 62, Warlock 69), **45** with at least one executable/code-literal path, **34** lacking a detected code-literal path, and **52** cataloged utility/encounter-only spells without direct simulated DPS. These are static inspection bins; spell ranks/effects still need individual source-to-execution confirmation. Many source-relevant missing defensive spells (Mage Armor, Mana Shield, Fire/Frost Ward, Ice Barrier, Shadow Ward, Demon Armor) can affect raid encounters and should not be mislabeled as purely cosmetic. Demonic conjures, polymorphs, travel, corpse-only abilities and threat/CC-specific utility require realistic encounter predicates; excluding them from a stationary dummy rotation does not imply they were removed from Forever.

### Warlock demon-family cross-check (all seven recognized types)

| Demon actor | Forever simulated archetype | Actual connected action | Runtime pathway | Remaining sign-off |
|---|---|---|---|---|
| Imp | Permanent Fire caster | Firebolt; Fire Shield / Blood Pact not fully connected | `WarlockDemonCompanion`, `ForeverWarlockDemonSpellbook` | Improved Imp rank interaction, pact/defensive aura and actual 70630 Firebolt ranks |
| Voidwalker | Permanent physical tank | Melee + Torment/Suffering threat | Same shared companion executor | Improved Voidwalker, Consume Shadows, Sacrifice, boss-threat and real 70630 ranks |
| Succubus | Permanent Shadow melee/special | Melee, Lash of Pain, conditional Seduction | Same shared companion executor | Improved Sayaad, Soothing Kiss, utility validation and hit masks |
| Incubus | Permanent Shadow melee/special | Melee, Lash of Pain, conditional Seduction | Same shared companion executor | Same Sayaad and targeting checks; verify variant spellbooks independently |
| Felhunter | Permanent Shadow melee/special | Shadow Bite, Spell Lock, optional Devour Magic | Same shared companion executor | Tainted Blood, Paranoia, healed Devour Magic and talent-specific effects |
| Infernal | Temporary summoned unit | Melee + Immolation | Same executor with configured duration | Summoning ritual/uptime and actual client damage tick coefficients |
| Doomguard | Temporary summoned unit | Melee + Doom Bolt | Same executor with configured duration | Ritual, high-tier spell actions, cooldown/rank verification |

**Removed / explicitly excluded based on current catalogs:** Mage **Detect Magic** and Frost talent **Piercing Ice**; Warlock **Curse of Shadow** and former **Dark Pact** talent/spell. Verify any later re-introduction against a later client build before enabling them.
