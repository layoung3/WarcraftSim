# CP175 Paladin source reconciliation — 13 November 2026

This ledger supports one class-wide Holy/Protection/Retribution implementation. Allocation legality, runtime component coverage and source confidence are independent. A number with a provisional source can execute; a missing event consumer must be reported as missing or partial regardless of the quality of its tooltip.

## Reproduction and provenance

`CP175-paladin-source-ledger.json` pins 38 retained inputs with URL, retrieval/capture time, actual byte count, SHA-256, source kind and supported fields. `CP175-paladin-source-facts.json` contains the compact extraction: 51 talent nodes (17 per tree), 61 spellbook groups including explicitly removed groups, and 120 selected spell/effect records. The spellbook groups are an inventory, not a claim that every group is executable. Raw source documents stay in the retained evidence directory, outside the project tree.

```bash
python3 docs/audits/CP175-paladin-source-extract.py \
  --inputs /workspace/scratch/f370a1002cd9/evidence/paladin-sources-20261113 --check
```

The offline check verifies every input hash before extracting. It checks independent talent identity sets, all three tree sizes and current rank parsing. Crossed-out Classic values are removed before parsing: Divine Favor is learned at 20, not the concatenated `2030`; Righteous Fury is learned at 10, not `1016`. A changed source fails the check and requires a new review. No web retrieval, build, test-suite execution or publication is performed by this script.

The [talent page](https://theforeverera.com/en/talents/paladin/) and [spell page](https://theforeverera.com/en/spells/paladin/) displayed **1.60.1.70888** when captured. Their input hashes are respectively `21a8fd91f3c21962ed36255e84766a742c802a22f609fcb7f33e3a00208ff31a` and `bdc54510bac481ecb939d6ca5448ffe066e2a5266c5a32e0853f48498f674a9f`. The independently captured homepage advertised **70938**; it does not upgrade either resource. These pages are published client mirrors, not an independently acquired official DB2 export.

The additional [wowsims/forever extraction and original analysis](https://github.com/wowsims/forever/tree/8c7b5a5f5ae85b6222c16d1780c342992cd74f71) is pinned to commit `8c7b5a5f5ae85b6222c16d1780c342992cd74f71`, committed 13 November at 18:47:53 UTC. Each consumed file has its own raw-byte receipt. A source file without a displayed client build is labeled that way. The generated spell store contains documented simulator overrides; it is not described as raw Blizzard tables. Its 5,017,997 bytes hash to `aae81a86f6902adf6f287720140f6becbea6ef936a9ddbd035c1de9fad5afceb`.

Direct Wago acquisition of ten 70888 DB2 CSV tables returned **HTTP 403**. Direct Wowhead HTML attempts for two pages returned zero bytes. Those failures remain recorded; rendered search access was useful for investigation but is not counted as a successful raw capture.

## Dated official behavior and identity

The controlling rework is the **ninth staff post, 3 November at 03:20 UTC**, in the [development thread](https://us.forums.blizzard.com/en/wow/t/wow-forever-beta-development-notes-%E2%80%93-updated-november-2/2360696/9). The opening post contains older revisions. The retained full topic allows the earlier 8 October Retribution Aura coefficient and 15 October Consecration change to be checked independently.

The [11 November official hotfix](https://us.forums.blizzard.com/en/wow/t/wow-forever-hotfixes-november-11-2026/2403040) overrides client-derived or simulator classifications: Judgement uses **ranged hit**, and cannot trigger effects restricted to melee attacks. The hotfix also excludes Windfury Totem attacks from generating capstone echoes. Official-domain searches for later 12/13 November Paladin changes and the latest captured hotfix thread found no later applicable staff change; this is a dated search result, not a future guarantee.

The canonical talent is `unsealed-wrath`, talent ID **105692**, talent spell ID **1310735**. November's official rework and current talent page name it Unsealed Wrath. The 11 November post calls the corresponding seal-echo effect Unsealed Fury without publishing a spell ID. Treating that name as an alias is an explicit inference from the matching capstone behavior, rather than adding a second talent. The earlier Twist of Light used spell 1310735, as the original [rework analysis](https://github.com/wowsims/forever/pull/772) records. Its old seal-cost reduction is removed.

`ForeverPaladinClassTalents.cs` holds allocation topology and component names. `ForeverPaladinSourceData.cs` maps every talent to its spell ID. A talent ID, talent spell ID and trained ability rank ID are different identities. A single talent spell can carry a rank curve; an empty `RankSpellIds` list does not mean the talent is unidentified or unsupported.

## Reviewed examples and legality

The authored examples are **reviewed examples**, with no popularity or optimality claim:

| Example | Holy / Protection / Retribution | Intended witness |
|---|---:|---|
| Holy | 31 / 5 / 8; seven unspent | Friendly healing, critical-heal mana return, Oath, Shock and Vigil with off-tree Intellect/critical effects |
| Protection | 12 / 31 / 8 | Shield-dependent incoming-hit, block, mitigation and mana effects with Holy cost/seal and Retribution critical effects |
| Retribution | 10 / 10 / 31 | Seal switching and capstone, Holy Strength/seals and Protection Intellect/hit |

Their ranks are constructed independently of preset identity. The tests preserve legal partial ranks, zero ranks, unspent points and mixed allocations. Row gates count points in earlier rows. Explicit prerequisite arrows remain independent: **Boundless Light requires Holy Shock in the same row**, which is valid. The first source test accidentally required every parent to be in an earlier row; that test expectation was corrected without changing source topology. Case-insensitive legal input must execute equivalently after JSON deserialization; the rank accessor handles an ordinary case-sensitive deserialized dictionary.

## Numeric values and source decisions

The facts file retains rank costs/levels/cooldowns and underlying effect values. The runtime constants use rounded intended decimal coefficients instead of exposing float32 representation noise. Source uncertainty is disclosed separately from implementation.

| Mechanic | Consumed value / rule | Evidence and qualification |
|---|---|---|
| Consecration | Eight one-second ticks; 12 base per target plus 27 and 0.042 SP on the first four | Current ranks; **0.042** from official 15 October update. Current model rolls one hit/critical result per target/tick. First-four mark child 1280353 has a **0.6-second tail**. |
| Retribution Aura | Rank-5 20 Holy + **0.06 SP** | Official 8 October change, current spell rank. Dynamic caster SP, not target SP. |
| Holy Shield | Rank 4 spell1239570: 240 Mana, 10-second cooldown/aura, 30% block, four charges, 221 + **0.08 SP** per block | Current extracted row; shield equipment requirement is explicit. |
| Holy Light | Rank9: 1495–1665 at60, 660 Mana, 2.5 seconds, **0.714** healing coefficient | Base level60, maximum scaling level65. The larger 1524–1694 tooltip value corresponds to the scaling cap and is not the level60 base. |
| Flash of Light | Rank6: 286–320 at58 plus 2.6 per level, giving **291.2–325.2** before engine rounding at60; 140 Mana, 1.5 seconds, **0.429** | Current effect19943. Numeric level scaling must not be silently omitted. |
| Holy Shock | Rank4 spell20930: 325 Mana, 10-second cooldown; hostile334–362/friendly307–333, **0.429** in each child effect | Hostile child25902 has20-yard range; friendly child25903 has40-yard range. The spellbook's aggregate40-yard display is not the hostile range. |
| Light's Vigil | Rank3 spell1311595: 1340 Mana, 1.5-second cast, six-second cooldown, 30-second friendly mark; party heal684–724 at60 + **0.143** healing power | Child heal1311596; friendly-only rework. The old hostile strike/refund and old coefficient are not reused. |
| Oath of Light | 8% base Mana, five minutes, one friendly target; at60 +400 Holy Light/+115 Flash/+**229** Holy Shock | Current effect PPL values. The last amount floors the raw float32 3.833329916×60; mirror-rounded230 is not consumed. Lower-rank/downranking behavior remains unmeasured and is outside this max-rank adapter. |
| Judgement of Righteousness | 162–178 at58 +4.1 per level = **170.2–186.2** at60; **0.429 SP** | Spell20286; correct level scaling, then official ranged-hit overlay. |
| Judgement of Fury | 146–160 at58 +3.69 per level = **153.38–167.38** at60; **0.429 SP**, three-second taunt | Spell20414. Rendered168–182 is the level64 cap, not level60. |
| Seal of Fury | 35 +**0.10 SP**; with shield, absorb50% of actual Holy damage, replacement rather than stacking | Current effect20418 and official [Paladin deep dive](https://worldofwarcraft.blizzard.com/en-us/news/24302099). Thirty-second replacement lifetime is a source-backed simulator model. |
| Seal of the Crusader | **325 AP** at60, 40% haste, white-auto damage divided by1.4, including extra white attacks | AP floors306+2.4×8. The current original model records measurements at1.6/3.6 weapon speeds, including Vindication's per-swing chance divided by1.4. The raw combat logs are not retained here; this remains a source-measured provisional model. |
| Holy Strike | 50%×(normalized weapon damage +81–105 +0.429 own SP), before later damage multipliers | Current original model and earlier direct beta observation. Judgement of the Crusader's target-side bonus uses full0.429 after done multipliers. Extrapolation to rank8 is provisional. |
| Seal of Righteousness | Weapon-speed/hand-specific base; own SP coefficient0.10 one-hand/0.12 two-hand | Current source preserves original beta measurements on eighteen weapons and later70425 coefficient measurements. Fast one-hand shape below measured1.7 seconds is extrapolated. |
| Seal of Command | 7 PPM, live1-second ICD, 70%×(weapon +0.29 own SP); its own hit/critical roll | Pinned Forever model/observations; per-echo proc rolls bypass the live ICD. Target-side Holy bonus uses full0.29. |
| Holy Wrath | Rank2 spell10318: 805 Mana, two-second cast,60-second cooldown,490–576 +0.19 SP at60;20-yard area and two-second stun | Current child429147 is a two-second `A_MOD_STUN`; control immunity remains a real prerequisite. Purifying Power reduces its cooldown; Holy Conduit reduces its Mana cost. |
| Blessing of Kings | Spell20217:8% base Mana, instant/1.5-second GCD, no cooldown,30 yards,one hour;10% total primary stats | `A_MOD_TOTAL_STAT_PERCENTAGE`, all five primary stats. The older support catalog marks Kings unmodeled, and the shared external-modifier catalog has no Kings consumer. The Paladin implementation must supply the real multiplier and derived-stat/resource changes exactly once, with reversible blessing replacement. |
| Blessing of Sacrifice | Rank2 spell20729:100 Mana, instant/1.5-second GCD,no cooldown,30 yards,30 seconds;55 damage per hit transferred from a party member | Effect698492 is `A_SPLIT_DAMAGE_FLAT` with school mask127 and party target57. One blessing per caster applies. Exact ordering against target absorbs and transfer-side mitigation is not established by these fields; any selected shared-engine ordering remains explicit and provisional. |

Full precision and source timestamps remain in the machine-readable ledger/facts. Fractional damage/healing bounds express the expected continuous amount used by this simulator; they are not a claim of exact integer rounding in every server combat-log event.

### Rank math and event boundaries

Holy Power's current aura 50 multiplies the **whole critical heal**: `1.5 × (1 + 0.10 × rank)`, reaching 2.25 at rank 5. The original rework's [PR772](https://github.com/wowsims/forever/pull/772) explicitly distinguishes this from multiplying only the extra critical-healing portion. Precision's spell 20189 has separate melee/ranged-hit and spell-hit effects; adding its ranks to both hit tables is supported. Spiritual Focus's current tooltip explicitly includes Light's Vigil as well as Flash of Light and Holy Light.

The independently pinned [Holy talents model](https://github.com/wowsims/forever/blob/8c7b5a5f5ae85b6222c16d1780c342992cd74f71/sim/paladin/talents_holy.go) applies Holy Power to Lay on Hands, and the [Lay on Hands model](https://github.com/wowsims/forever/blob/8c7b5a5f5ae85b6222c16d1780c342992cd74f71/sim/paladin/lay_on_hands.go) explicitly rolls a critical heal. It remains outside the named Illumination and Divine Favor spells. Infusion is consumed when Holy Light completes; a canceled cast should retain the proc until its ordinary expiry. The exact number of Illumination refunds from several critical Light's Vigil party-heal impacts is not resolved by these fields. A bounded one-refund-per-Vigil-execution choice must stay visible as provisional, and the Vigil execution must remain distinct from the triggering Holy Shock so neither spell suppresses the other's refund.

The current [Retribution implementation](https://github.com/wowsims/forever/blob/8c7b5a5f5ae85b6222c16d1780c342992cd74f71/sim/paladin/talents_retribution.go) records Sanctified Judgement measurements at Benediction ranks 0, 1, 3 and 5. Its refund is `base seal cost × (1 + Benediction reduction) × rank refund fraction`; it is not calculated from the reduced amount paid. At maximum ranks a 90-Mana seal costs 81 and refunds 59.4. Judgement and Swift Judgement both refund on their qualifying cast even if the unleashed effect misses. This counterintuitive rule remains source-measured and provisional.

Vindication's current triggered row 440667 gives `6 − 3.5 × 60 = −204` Attack Power at the maximum rank, or 68/136/204 by allocated rank. The talent-page renderer's −2/−4/−6 text is not used as a level-60 value. A successful 5-PPM opportunity then makes a separate Holy spell-hit roll before applying its 30-second debuff. The current original model documents both stages. Eye for an Eye's reflection uses its actual damage-taken fraction, capped at half the Paladin's maximum Health, and ignores ordinary outgoing damage modifiers; applying Vengeance or Two-Handed Weapon Specialization again would change that fraction and could exceed the cap.

The pinned Protection model grants Iron Creed after any Holy Strike attempt while Righteous Fury is active, including misses and avoidance. The reduction then lasts six seconds even if Righteous Fury ends. Reckoning consumes stored attacks on the next main-hand swing attempt; the later PR982 observations include avoided swings spending charges. These event distinctions are separate from Shieldsworn's landed-only free block effects below.

Current Judgement 20271, Swift Judgement 1310994 and Templar's Bulwark 1311015 have zero global-cooldown fields and no global recovery category. They must not inherit an ordinary 1.5-second action GCD. Divine Favor 20216 is also outside the GCD; the ordinary cast/instant actions explicitly carry a 1500ms recovery. Their cooldowns and other prerequisites still apply.

Spiritual Focus and Concentration Aura have sourced pushback-resistance magnitudes. Neither the retained client fields nor the checked original analysis establishes whether a fully absorbed incoming hit causes pushback. That event boundary and the bounded pushback schedule remain explicit provisional assumptions; this uncertainty does not erase the implemented rank-dependent resistance consumer.

## Proc events and later measurements

The [current proc-override source](https://github.com/wowsims/forever/blob/8c7b5a5f5ae85b6222c16d1780c342992cd74f71/tools/database/overrides/spell_overrides.go) records original measurements: Light15 PPM on rank1 and rank2, Wisdom15 PPM on rank1, judged Light50% on landed melee, and judged Wisdom50% on landed melee/ranged/direct spell attacks. Higher ranks carry these measured rates provisionally. This is not an imported Classic assumption. Periodic and damage-shield events do not become ordinary judged-Wisdom opportunities.

**Seal of Justice's stun rate remains unresolved.** Its client ProcChance100 is explicitly classified as a PPM sentinel with no rate supplied. It cannot become a100% proc or an invented fixed chance. Duration, cost, Judgement and control prerequisites can be implemented separately. Any unimplemented automatic stun trigger must remain visible at that component.

**Judgement of Justice has a separate hit-resolution source conflict.** The pinned original `seal_of_justice.go` model uses an Always Hit outcome, but the corresponding raw-client flag has not been independently confirmed. The implementation applies the dated 11 November official general Judgement policy: ranged-hit resolution, with no melee-only proc eligibility. It therefore uses the configured ranged hit table for Justice rather than asserting a guaranteed-hit exception. Whether that original-model exception survives the official change remains source-provisional; it is unrelated to the seal's unknown stun proc rate.

| Event | Live Righteousness/Fury/Command | Live Light/Wisdom | Unsealed storage/spending |
|---|---|---|---|
| Landed normal melee auto | Eligible | Eligible | Eligible |
| Landed Reckoning white copy | Eligible | Eligible | Eligible, bounded by event identity |
| Landed Windfury white copy | Eligible | Eligible | Excluded by11 November hotfix |
| Holy Strike melee ability | Excluded by current white-hit seal masks | Eligible | Excluded by current capstone auto-attack mask |
| Judgement ranged attack | Excluded | Excluded from live melee seal triggers | Excluded |
| Seal/echo damage child | Does not recursively trigger live seals | Not an ordinary fresh melee trigger | Excluded |

Righteousness/Fury children use a critical-only damage roll after their landed parent. Command makes its own melee hit/critical roll. Their eligible weapon-item interactions need explicit child event identity; disabling every proc child would omit supported weapon effects, while treating them as ordinary fresh auto-attacks would recurse. Current upstream Judgement comments still say melee; the official ranged override controls here.

Unsealed uses six distinct echo IDs (Command/Fury/Righteousness/Justice/Light/Wisdom); Crusader has no stored-hit echo. A changing-seal hit consumes the prior seal copies and begins the current seal's first stack in the pinned model. Each copy uses its stored seal's normal chance. Duration15 seconds, maximum three stacks, main-hand equipment and recursion boundaries remain explicit.

Original [measurement PR982](https://github.com/wowsims/forever/pull/982), merged13 November12:25UTC, is later than the rework. It supports Redoubt on **all incoming melee attempts**, including avoided and fully absorbed hits; spell/ranged events are excluded. It also distinguishes Shieldsworn's six-second window after any shield-equipped Holy Strike attempt from free Holy Shield/Shield Specialization effects after a landed strike. It finds Two-Handed Weapon Specialization on seals/Judgements/Holy Strike/Consecration, but not Exorcism, Hammer of Wrath or Retribution Aura. Holy Shock/Holy Wrath remain provisionally included because unmeasured. No healing multiplier is implied.

### Shieldsworn conflict resolution

The3 November official post says all talents activated by a block and permits spell blocking. The70888 tooltip names Holy Shield and Shield Specialization. Upstream's narrower “magic spells remain unblockable” choice traces to [PR772](https://github.com/wowsims/forever/pull/772), created30 October/merged31 October, and is a model ruling rather than an incoming-magic measurement. Current child1328551 contains `A_ALLOW_BLOCKING_SPELLS` without a school restriction. PR982's later measurements establish the **trigger timing**, but do not establish a Reckoning or magic-spell exclusion.

The implementation decision is therefore official broad spell blocking and applicable block talents, including Reckoning, with this unresolved server breadth disclosed. Adopt the later measured any-attempt/window versus landed/free-trigger distinction. Redoubt is an incoming-melee effect, not itself an on-block effect, so Shieldsworn does not synthesize Redoubt. A simulation test establishes the selected contract's behavior; it does not independently certify the unresolved server interpretation.

## Incoming damage, resources, statistics and equipment

The [original resistance analysis](https://github.com/wowsims/forever/pull/436) fits average resistance to `R / (abs(R) + 9 × attacker level)`. Its observations are at levels20–30; the author explicitly reports that plausible alternatives diverge at60. Use this source-backed provisional model for bounded resistance-aura scenarios. The old Classic75%-cap/5L average formula is not substituted. This does not certify every resist distribution or piercing edge case.

Fury's50% absorb share is supported by4077/4078 open-world observations in PR982. Dungeon scaling depends on expected creature-health curves and an instance base level. A bounded open-world scenario is supported; applying the same value to every dungeon without disclosure is not. Absorb replacement and consumption are distinct from healing and overhealing.

The existing shared character pipeline remains authoritative: `ForeverPrimaryPhysicalStatProfiles` supplies Paladin2 AP/Strength and19.8 Agility/melee-critical-percent at60; `ForeverPrimarySpellStatProfiles` supplies59.9 Intellect/spell-critical-percent; `ForeverPhysicalStatConversions` supplies20 Strength/block value. The existing70707 Base Mana subset is reused with its retained hash. No new subsystem redefines these conversions. Historical level60 baseline proxies, particularly the lower-confidence Undead extrapolation, remain provisional rather than newly measured; strict, explicit provisional opt-in and observation modes remain separate.

The captured current race matrix supports Human, Dwarf and Undead Paladin; the [official deep-dive panel recap](https://worldofwarcraft.blizzard.com/en-us/news/24303313) independently confirms the new Undead pairing. Availability does not establish that all racial active/passive mechanics execute. Coverage must disclose those effects separately.

The coordinator's `WarcraftSim.Api/Catalog/forever-paladin-equipment-70888.json` pins the exact three reviewed item records, positive Paladin class bits and separate sword proficiency evidence. Shield corroboration is separate: current Holy Shield1239570 requires armor class4/subclass mask64, and the captured70888 Seal of Fury tooltip explicitly requires an equipped shield for its absorb. The official Paladin deep dive corroborates the absorption mechanic and non-stacking behavior, but its word “shield” describes the absorb rather than proving equipped-shield proficiency. The current two client-derived requirements support a provisional shield-use inference, not a successfully retrieved SkillLineAbility learning row. Item existence/class eligibility is not proof of present acquisition or dungeon availability. Unknown masks and unreviewed proficiency remain pending.

Independent source review caught two integration risks for the coordinator/runtime: shield21269 has55 intrinsic block **plus**15 bonus block value; its3 block stat is chance. Its8 Defense must reach the canonical runtime defense skill. Corruption12782's removed Classic−40 Spirit must not leak into current stats. Set506's three-piece Judgement proc is separately disclosed and cannot be activated by its single admitted piece.

## Verification status

The offline extractor and subsequent `--check` completed: 38 input hashes, 51 talent nodes, 61 spellbook groups and 120 effect records. Focused .NET source tests are in `CP175_PaladinSources.cs`; the coordinator owns their executed receipts and final candidate qualification. No broad test-suite, browser, push or CI success is claimed by this source ledger. Runtime/component evidence and publication status belong in the implementation ledger and acceptance manifest.
