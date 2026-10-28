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
| Hunter | Beast Mastery | `BeastMasteryHunterSimRunner`, `ForeverBeastMasteryHunterSpellbook`, `HunterAspectProcessor` | **Executable/partial**: Hawk/Beast/Volley, pet hit and expertise preserved, live AP on Aimed/Multi; Deadly Aspects **Hawk Auto Shot** proc modeled, **Beast melee** proc remains partial; temporary Hawk AP is not transferred to pet |
| Warlock | Affliction | `WarlockSimRunner`, `ForeverWarlockDpsSpellbook`, `DamageToSelfHealingProcessor` | **Executable/provisional**: Drain Life and Siphon Life use resolved damage; DoT coefficients, snapshot and curse group effects need live verification |
| Warlock | Demonology | `WarlockDemonCompanion`, `WarlockDemonicBrandProcessor`, `WarlockSimRunner` | **Partial**: Demonic Brand / pet family school provisional; **Decimation 2 ranks is confirmed current and missing** from executable talent registry; Soul Fire shard accounting remains an abstraction |
| Warlock | Destruction | `WarlockSimRunner`, `WarlockConflagrateConsumptionProcessor`, `WarlockShadowburnShardProcessor` | **Executable/partial**: Searing Pain/Shadowburn, Rain of Fire; verify threat tables, death-window refunds, direct/periodic target and target-area behavior |
| Mage | Arcane | `MageSimRunner`, `MageAdaptiveRotation`, `ForeverMageDpsSpellbook` | **Executable/partial**: PoM and Arcane Power, Clearcasting, AoE; **Magic Attunement** confirmed baseline but Amplify/Dampen Magic effects absent; Magic Absorption 1s ICD needs audit |
| Mage | Fire | Same runner/spellbook, `MageIgniteRollingProcessor` | **Executable/partial**: Combustion, Scorch, Flamestrike; verify multi-target critical/rolling Ignite and source-specific DoT interaction |
| Mage | Frost | Same runner/spellbook, Frost proc/Cold Snap state | **Executable/partial**: Blizzard and Cold Snap; complete target-level slow/control and current rank comparison before certification |
| Priest | Discipline | `HolyPriestPreviewRunner`, `ForeverPriestHealingSpellbook`, `PriestMajorCooldownProcessor` | **Executable/partial**: Inner Focus, Power Infusion, Penance; external ally PI target and group-heal modifiers need fully executable support |
| Priest | Holy | Same healer runner, `PriestCombatIntegration` | **Executable/partial**: configured heals, heals-over-time and prayer abilities; Holy Nova, rank-by-rank effects and raid-group targeting remain unverified |
| Priest | Shadow | `ShadowPriestSimRunner`, `ForeverPriestShadowSpellbook`, `ForeverPriestShadowReactiveProcessor` | **Executable/partial**: Shadow Weaving, Mind Flay and execute, cross-tree Inner Focus/PI; confirm current Shadow rank coefficients and triggered effects |
| Shaman | Elemental | `ElementalShamanSimRunner`, `ForeverElementalShamanSpellbook`, `ShamanTotemLifecycleProcessor` | **Executable/partial**: direct/periodic spells, Fire Nova, Lightning Shield, independently paid Call/Recall; radius, totem actors and Totemic Projection remain open |
| Shaman | Enhancement | `EnhancementShamanSimRunner`, `ForeverEnhancementShamanReactiveProcessor`, `ShamanWeaponImbueProcessor`, `ShamanTotemLifecycleProcessor` | **Executable/partial**: MH/OH imbues, WF nonrecursive proc, totem calls; precise Flametongue/Frostbrand probabilities and Rockbiter threat not measured |
| Shaman | Restoration | `RestorationShamanSimRunner`, `RestorationShamanCombatProcessor`, `ForeverRestorationShamanSpellbook` | **Partial**: existing Water Shield, Mana Tide, Healing Stream slot behavior, healing; migrate to coordinated Earth/Fire/Water/Air owner if current support is required; do not double-count old and new water-slot actors |
| Druid | Balance | `BalanceDruidSimRunner`, `ForeverBalanceDruidSpellbook`, `ForeverDruidSharedCooldowns` | **Executable/partial**: Eclipse, Innervate, Barkskin; current Moonkin talents and ally-innervate targeting unresolved |
| Druid | Feral Cat | `FeralDruidSimRunner`, `FeralDruidCombatProcessor`, `ForeverFeralDruidSpellbook` | **Executable/partial**: source-owned combo totals keyed to actual target, two-target switching and Rip hit-application, missed-finisher point retention and **provisional** 80% Energy refund; still verify bleed refresh snapshots and exact client refund |
| Druid | Feral Bear | Same runner, `FeralDruidTankMetricsProcessor`, `FeralBearIncomingRageProcessor` | **Executable/partial**: defensive and incoming-Rage metrics, Swipe/Lacerate; verify form-specific cooldowns and tank utility |
| Druid | Restoration | `RestorationDruidSimRunner`, `ForeverRestorationDruidSpellbook` | **Executable/partial**: HoTs, Swiftmend, Barkskin/Innervate; direct ally Innervate and other cooldowns, group heal targeting, current ranks need comparison |

## Confirmed new or expanded omissions (outside original 38-row CP167 inventory)

| Item | Current source / scope | Status |
|---|---|---|
| Warlock Decimation (Demonology, 2 ranks) | Current 70532 tree: 45/90% Soul Fire CD reduction; Shadow Bolt/Searing Pain below 35% enable 20/40% faster shard-free Soul Fire for 10 seconds, 3/6% damage bonus | **Missing**; implement with effective cast-time/provider, shard inventory and true execute state |
| Mage Magic Attunement | Current baseline Mage effect enhances Amplify Magic / Dampen Magic by 50%; not a selectable Arcane talent | **Encounter dependent/partial**: no corresponding raid magical damage/healing modifiers in solo runners; never grant phantom DPS |
| Hunter Deadly Aspects (BM five ranks) | Hawk Auto Shot procs 2/4/6/8/10%; Beast melee auto similarly 30% haste for 12 seconds | **Partial**: Hawk chance modeled in BM; Beast melee trigger not yet in all eligible physical streams |
| Shaman Call of Ancestors and Call of Spirits | Current client spellbook levels 30/40 and three-second casts; separate Totem Bar presets | **Executable initial**: independently configured sets in Elemental/Enhancement, same shared four-slot executor; Restoration integration pending |
| Shaman Totemic Projection | Current class spell moves active totems to a new location | **Encounter dependent / missing**: requires position representation; no fictitious flat effect |
| Hunter other Aspect and PvE utility ranks | Current level-60 spellbook differs from Classic, including Aspect of the Beast melee AP | **Partially inventoried**; remaining verified baseline ranks and positioning effects must be cataloged, not guessed |

## Remaining certification gates

1. Read each of the 25 specialization's full contemporaneous spell and talent catalogs, **including all historical/unselected ranks**, and mark each row by source/version, executable test, numeric confidence, and actual PvE effect.
2. Close all confirmed independently executable high-impact omissions, including Decimation and connected talent procs, and repair all disconnected validator-only ranks.
3. Exercise all default presets and opt-in spell combinations; add deterministic target switch, pet, channel, periodic, missed application, proc, aura replacement and cost accounting tests.
4. Keep the existing original 38 CP167 entries and prior CP166 regressions; never count a mere dictionary entry, non-applying aura or documentation note as mechanically complete.
5. Only after final strict CI, owner local validation and explicit merge authorization may draft PR #20 be merged.

**Source posture:** client spellbook/tooltip and contemporaneous official hotfix notes establish mechanic *intent*, not full in-game numeric verification. For the above checked gaps: [Decimation](https://theforeverera.com/en/talents/warlock/decimation/), [Magic Attunement](https://www.wowhead.com/forever/spell=12606/magic-attunement), [Deadly Aspects](https://talcalc.com/hunter/talents/deadly-aspects/), [Shaman spellbook](https://questing.wiki/wow-forever/spells/shaman/).
