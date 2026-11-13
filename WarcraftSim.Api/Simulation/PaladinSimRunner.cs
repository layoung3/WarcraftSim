using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Paladin;
using WarcraftSim.Data.Forever.Simulation;

namespace WarcraftSim.Api.Simulation;

/// <summary>One Paladin execution path: ranked effects compose before preparation and
/// during actual events. The caller's preset/specialization labels are never read.</summary>
public sealed class PaladinSimRunner
{
    private sealed record Prepared(PaladinSimRequest Request,PaladinSimRequest RawRequest,StatCollection Stats,
        decimal Mana,decimal Health,ForeverLevel60BaselineObservation? Baseline,
        ForeverSharedCharacterInputPreparation Shared,string WeaponKind,bool Shield,
        IReadOnlyList<AbilityDefinition> Abilities,RotationProfile Rotation,List<string> Notes);

    public static ForeverTalentRuntimeCoverage TalentCoverage(string key)
    {
        var node=ForeverPaladinClassTalents.Find(key)??throw new ArgumentException("Unknown Paladin talent key.");
        var condition=key switch {
            "spiritual-focus"=>"Incoming damaging events during Flash of Light, Holy Light or Light's Vigil hard casts.",
            "reverence"=>"Explicit nonzero Spirit-regeneration scenario input while inside the shared five-second rule.",
            "illumination"=>"Critical qualifying direct heal, including overhealing; one refund roll per execution.",
            "divine-favor"=>"Next Holy Light, Flash of Light or offensive/friendly Holy Shock; consumed after resolution.",
            "divine-precision"=>"Holy spell hit resolution and explicit incoming silence events; Judgement retains ranged hit classification.",
            "infusion-of-light"=>"Critical Holy Shock or Flash of Light followed by Holy Light within 15 seconds.",
            "oath-of-light"=>"One explicitly selected friendly or self target; current level-60 source bonuses.",
            "consecrated-ground"=>"First four stable enemy targets within active Consecration; further enemies retain base ticks.",
            "boundless-light"=>"Friendly Holy Shock target lacks this Paladin's active Oath of Light.",
            "lights-vigil"=>"Explicit living friendly party target; mark followed by Holy Shock; bounded party is player plus configured ally.",
            "redoubt"=>"Equipped shield and any incoming melee attempt, including avoidance/absorption, per independently pinned November 13 observations; ten-second/five-block lifetime.",
            "shield-specialization"=>"Equipped shield and actual block, or sourced Shieldsworn block-effect trigger; real Mana deficit determines effective gain.",
            "holy-shield"=>"Allocated action, equipped shield and incoming block; four charges or ten-second expiration.",
            "reckoning"=>"Incoming blocks/nonperiodic criticals store up to four attacks; next normal main-hand auto spends the store.",
            "shieldsworn"=>"Shield plus Holy Strike; free active block effects, six-second spell-block window, action cooldown; generated attacks have independent identities.",
            "improved-righteous-fury"=>"Righteous Fury active at the actual incoming hit.",
            "iron-creed"=>"Holy Strike threat; temporary mitigation additionally requires active Righteous Fury.",
            "toughness"=>"Raw armor from equipped items/modifiers; excludes Agility-derived armor and aura armor.",
            "anticipation"=>"Incoming attack avoidance/block/critical resolution consumes actual Defense skill.",
            "deflection"=>"Incoming parryable melee; never spells or ranged attacks.",
            "templars-bulwark"=>"No active Forbearance; real incoming damage consumes the eight-second maximum-Health absorb.",
            "guardians-favor"=>"Blessing of Protection cooldown and Freedom movement-effect immunity duration on a valid friendly target.",
            "sacred-duty"=>"Selected defensive cooldowns and actual Forbearance gates/expiration.",
            "stoicism"=>"Explicit incoming stun/charm scenario events.",
            "unyielding-faith"=>"Explicit incoming fear/disorient scenario events.",
            "purifying-power"=>"Valid poison/disease/magic friendly-target effects for cleansing; undead/demon enemies for Exorcism/Holy Wrath.",
            "turn-evil"=>"Controllable undead/demon target; learned rank determines cast time, cooldown and demon eligibility.",
            "repentance"=>"Controllable humanoid; subsequent actual damage breaks incapacitation.",
            "pursuit-of-justice"=>"Explicit one-dimensional normal or mounted movement; maximum, not sum, with other speed bonuses.",
            "vindication"=>"Eligible damaging melee proc; target incoming attack has explicit Attack Power contribution to reduce.",
            "eye-for-an-eye"=>"Actual incoming nonperiodic critical damage; reflected amount capped at half current maximum Health.",
            "two-handed-weapon-specialization"=>"An actual two-handed melee weapon is equipped; physical/Holy damage and all threat consumers.",
            "unsealed-wrath"=>"Eligible landed attacks with active seals; switching releases stored proc rolls. Windfury, seal impacts and echoes cannot build or recursively release echoes.",
            "seal-of-command"=>"Learned seal, melee weapon, eligible landed weapon event; sourced PPM and internal cooldown; control-state Judgement bonus.",
            "sanctified-judgement"=>"Judgement/Swift Judgement with active seal; rank-specific probability/refund and actual Mana cap.",
            "vengeance"=>"Landed nonperiodic damaging critical; five stacks, thirty-second refresh/expiration; Physical/Holy only.",
            "champion-of-the-light"=>"Total prepared/live Intellect adds spell damage; Hammer of Wrath requires target at or below twenty percent Health.",
            _=>"Allocated ranks and actual qualifying stat, action, target, resource or cooldown conditions; independent of preset origin."
        };
        return new("implemented",node.EffectComponents.ToArray(),[],[condition]);
    }

    private static Prepared Prepare(PaladinSimRequest input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var raw=ForeverClassBuildJson.Clone(input);var r=ForeverClassBuildJson.Clone(input);
        if(r.Character is null||r.Character.ClassKey!="paladin"||r.Character.Level!=60||
            r.Talents?.Ranks is null||r.ExternalModifiers is null||r.CombatItems is null||
            r.DurationSeconds is <=0m or >600m||r.TargetMaximumHealth<=0m||r.TargetArmor<0m||
            r.MaximumHealth<=0m||r.TargetCount is <1 or >9||
            r.WeaponMinDamage<0m||r.WeaponMaxDamage<r.WeaponMinDamage||r.WeaponSpeedSeconds<=0m||
            r.ManualWeaponKind is not ("none" or "one-hand" or "two-hand")||r.ManualShieldBlockValue<0m||
            r.IncomingBossHitDamage<0m||r.IncomingAttackPower<0m||r.IncomingBossSwingSeconds is <.25m or >600m||
            r.FriendlyTargetIncomingDamage<0m||r.FriendlyTargetIncomingIntervalSeconds is <.25m or >600m||
            r.FriendlyTargetMaximumHealth<=0m||r.SealSwitchIntervalSeconds is <1.5m or >600m||r.ManaRegenPerSecond<0m||
            r.HealTarget is not ("self" or "friendly")||r.OathTarget is not ("healing-target" or "self" or "friendly" or "none")||r.OathTarget=="friendly"&&!r.FriendlyTargetEnabled||r.HealTarget=="friendly"&&!r.FriendlyTargetEnabled||
            r.IncomingAttackType is not ("melee" or "ranged" or "spell")||
            r.IncomingDamageSchool is not ("physical" or "holy" or "shadow" or "fire" or "frost" or "nature" or "arcane")||
            r.TargetCreatureType is not ("humanoid" or "undead" or "demon" or "beast" or "elemental" or "dragonkin" or "other"))
            throw new ArgumentException("Invalid bounded Paladin character, equipment assumption or encounter settings.");
        if(new[]{r.TargetInitialHealthPercent,r.InitialHealthPercent,r.FriendlyTargetInitialHealthPercent}.Any(x=>x is <=0m or >100m)||
            new[]{r.InitialManaPercent,r.HealBelowHealthPercent,r.BaseMeleeHitPercent,r.BaseRangedHitPercent,r.BaseSpellHitPercent,
                r.CriticalChancePercent,r.IncomingBlockChancePercent,r.IncomingCriticalChancePercent,r.WindfuryTotemProcChancePercent}.Any(x=>x is <0m or >100m))
            throw new ArgumentException("Paladin percentages must be in range; starting Health must be positive.");
        if(r.Seal!="none"&&!PaladinSpellbook.Seals.Contains(r.Seal,StringComparer.Ordinal)||
            r.SecondarySeal is not null&&!PaladinSpellbook.Seals.Contains(r.SecondarySeal,StringComparer.Ordinal)||
            r.Aura!="none"&&!PaladinSpellbook.Auras.Contains(r.Aura,StringComparer.Ordinal)||
            r.SecondarySeal is not null&&(r.Seal=="none"||r.SecondarySeal==r.Seal))
            throw new ArgumentException("Choose supported, distinct Paladin seals and a supported aura.");
        r.Talents.Validate(r.Character.Level);
        if((r.Seal=="command"||r.SecondarySeal=="command")&&r.Talents.Rank("seal-of-command")==0)
            throw new ArgumentException("Seal of Command requires its allocated talent; the saved allocation remains editable.");
        if(r.SealOfJusticeProcChancePercent is <0m or >100m)throw new ArgumentException("Explicit Seal of Justice chance must be 0–100%.");
        if((r.Seal=="justice"||r.SecondarySeal=="justice"||r.CustomPriority?.Contains("seal-of-justice")==true)&&r.SealOfJusticeProcChancePercent is null)
            throw new ArgumentException("Seal of Justice stun probability is pending source evidence. Its event mechanic requires an explicitly supplied manual proc-chance assumption; no 100% client sentinel is treated as a rate.");
        ValidateUtility(r.UtilityScenario,r.FriendlyTargetEnabled,r.DurationSeconds);
        r.CombatItems.Validate("paladin");
        r.Character.SpecializationKey="retribution";
        var baseline=ForeverLevel60Baseline.Resolve(r.Level60Baseline,r.UseReviewedLevel60Baseline,
            r.Character.Race??"","paladin",r.UseProvisionalLevel60Baseline);
        var shared=ForeverSharedCharacterInputs.Prepare(r.Character,baseline,r.ExternalModifiers,r.DurationSeconds,
            supportedTargetDebuffs:["faerie-fire-rank4"]);
        r.Character=shared.Character;
        var catalogHands=r.Character.Equipment.Items.Where(x=>(x.SlotKey is "main-hand" or "off-hand")&&x.ItemId.HasValue).ToArray();
        if(r.UseManualWeaponInputs&&catalogHands.Length>0)
            throw new ArgumentException("Explicit manual weapons/shields cannot also be supplied by equipped catalog hand slots.");
        var weaponKind="none";var shield=false;
        if(shared.Weapons.TryGetValue("main-hand",out var main))
        {
            weaponKind=main.Hand=="two-hand"?"two-hand":"one-hand";
            r.WeaponMinDamage=main.MinimumDamage;r.WeaponMaxDamage=main.MaximumDamage;r.WeaponSpeedSeconds=main.SpeedSeconds;
        }
        if(shared.Weapons.ContainsKey("off-hand"))throw new ArgumentException("Paladin cannot dual wield; an off hand must be a verified shield or held item.");
        shield=catalogHands.Any(x=>x.ItemId is int id&&ForeverEquipmentCatalog.FindItem(id).ItemSubclass=="shield");
        if(r.UseManualWeaponInputs){weaponKind=r.ManualWeaponKind;shield=r.ManualShield;}
        if(shield&&weaponKind=="two-hand")throw new ArgumentException("A two-handed weapon cannot be combined with a shield.");
        var calculator=ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_=>r.MaximumHealth,
            additionalStatContributors:[new TalentPrimaryStats(r.Talents)]);
        var stats=calculator.Calculate(r.Character).EffectiveStats;
        stats.Set("attack-power",Math.Max(0m,stats.Get("attack-power")+stats.Get("melee-attack-power")));
        stats.Set("spell-power",Math.Max(0m,stats.Get("spell-power")+.12m*r.Talents.Rank("champion-of-the-light")*stats.Get("intellect")));
        stats.Set("healing-power",Math.Max(0m,stats.Get("healing-power")+stats.Get("spell-power")-
            .12m*r.Talents.Rank("champion-of-the-light")*stats.Get("intellect")));
        stats.Set("main-hand-weapon-skill",300m);stats.Set("defense-skill",300m+stats.Get("defense-skill")+stats.Get("defense")+4m*r.Talents.Rank("anticipation"));
        if(r.UseManualWeaponInputs&&shield)stats.Add("block-value",r.ManualShieldBlockValue);
        else if(shield)stats.Add("block-value",catalogHands.Where(x=>x.ItemId.HasValue).Sum(x=>ForeverEquipmentCatalog.FindItem(x.ItemId!.Value).ShieldBlockValue));
        stats.Set("paladin-shield-equipped",shield?1m:0m);stats.Set("paladin-weapon-equipped",weaponKind!="none"?1m:0m);
        stats.Set("paladin-target-distance",r.UtilityScenario?.InitialTargetDistanceYards??3m);
        var mana=baseline?.ObservedUnbuffedMaximumMana is decimal observedMana
            ? observedMana+15m*(Math.Max(0m,stats.Get("intellect")-20m)-Math.Max(0m,baseline.Intellect-20m))+stats.Get("mana")
            : ForeverClientBaseManaCatalog.GetBaseMana("paladin",60)+Math.Max(0m,stats.Get("intellect")-20m)*15m+stats.Get("mana");
        var health=(baseline?.ObservedUnbuffedMaximumHealth??r.MaximumHealth)+10m*
            (stats.Get("stamina")-(baseline?.Stamina??r.Character.BaseStats.Get("stamina")))+stats.Get("health");
        if(mana<=0m||health<=0m)throw new ArgumentException("Prepared Paladin Mana and Health must be positive.");
        var abilities=PaladinSpellbook.Create(r,weaponKind,shield,ForeverClientBaseManaCatalog.GetBaseMana("paladin",60),health);
        var rotation=BuildRotation(r,abilities);
        var notes=shared.Notes.ToList();
        notes.Add("All Holy, Protection and Retribution ranks compose in one Paladin engine. Preset origin and editable tree labels are provenance only. Legal allocation, effect execution and source confidence are separate.");
        notes.Add("Talent topology and spell-rank base values are pinned to the actual 1.60.1.70888 reference resources; independently pinned client-extractor fields and November 3/11 official overrides retain their own build/date. They are source-backed provisional values, not measured live-server certification. See docs/audits/CP175-paladin-source-ledger.json.");
        notes.Add("Base-combat gap: white-hit glancing is not implemented in this Paladin resolver. The shared provisional chance rule has no qualified current Forever glancing damage range; these simulations omit that outcome rather than silently borrowing another class's calibration. All 51 talent-component rows do not certify a complete white-hit table. Justice Judgement follows the November 11 general ranged-hit policy; the pinned original model's Always Hit exception lacks independently confirmed raw evidence and remains a source conflict.");
        notes.Add("November 11 Judgement uses ranged-hit resolution and cannot trigger melee-only item/effect procs. The Unsealed Wrath talent (official hotfix alias Unsealed Fury) stores normal eligible melee seal events; Windfury Totem, seal-proc and echo events cannot create recursive echoes.");
        notes.Add("Preparation order: raw baseline + gear/enchant/modifier inputs; total Strength/Intellect and item-only armor talent multipliers; shared primary/rating conversions; Champion spell-damage and resource caps. Live procs receive the same incremental conversions exactly once. Champion of the Light adds spell damage, not bonus healing.");
        notes.Add(baseline is null?"Explicit authored character statistics and naked scenario Health are manual assumptions. No implicit historical baseline was enabled.":
            "Baseline: "+baseline.VerificationStatus+"; "+baseline.ConfidenceClassification+". Naked observed/proxy Health and Mana plus post-baseline Stamina/Intellect/flat deltas are applied once. Racial passives/actives are not silently supplied.");
        notes.Add(r.UseManualWeaponInputs?"Weapon and shield values are explicitly opted-in manual assumptions, distinct from catalog eligibility evidence.":
            "Weapon/shield action eligibility derives from prepared catalog hand slots. Empty hands do not receive a hidden illustrative weapon or shield.");
        notes.Add($"Encounter hit assumptions: melee {r.BaseMeleeHitPercent}%, ranged/Judgement {r.BaseRangedHitPercent}%, spell {r.BaseSpellHitPercent}%. Critical and avoidance inputs use {(r.UseManualCombatAssumptions?"explicit manual probabilities":"prepared shared stats plus sourced talent modifiers")}; target-level dodge and defense conversion remain source-provisional. Spirit regeneration is an explicit {r.ManaRegenPerSecond} Mana/s scenario input, with Reverence retaining 15/30% under the shared five-second rule.");
        notes.Add("Resistance auras use the source-measured average R/(R+9×attackerLevel) model; level-60 extrapolation and average rather than partial-resist sampling remain provisional. Current level-60 spell ranks and selected historical beta weapon coefficients retain distinct source confidence. Seal of Justice requires an explicit manual proc-rate input because its client sentinel is not a rate. Fury absorbs use the sourced open-world 50% replacement/30-second model; dungeon base-level scaling is unavailable in this bounded encounter.");
        notes.Add("Focused Health/incoming-hit/friendly-target scenarios execute real healing, overhealing, absorption, block, threat, control and cooldowns. They are not full raid-healing, tank-assignment, positioning or PvP workflows. Unselected or disabled inventory never becomes an automatic unlimited effect.");
        notes.Add("Blessing of Sacrifice transfers up to 55 damage per friendly hit for 30 seconds through real damage events. Its bounded ordering is provisional: after target mitigation and before target absorption, then caster immunity/absorption with no second mitigation pass. Active Blessing of Kings changes the Paladin's current five primary statistics through shared derived deltas and live resource caps; a configured friendly target has explicit Health but no class/stat profile, so this engine does not invent allied primary statistics. Ending maximum Health/Mana distinguish live caps from prepared caps. Active Might never duplicates the same selected external Might contribution. Illumination rolls once per originating Light's Vigil cast across the bounded party; this multi-target refund policy is provisional, independently of the separate Holy Shock refund.");
        notes.Add("Spiritual Focus/Concentration consume real cast-pushback events. Whether fully absorbed hits should trigger pushback remains unmeasured; the bounded model counts damaging-or-absorbed hits before the sourced resistance roll. Sanctified Judgement uses the currently pinned measured base-cost × (1 + Benediction reduction) refund convention, distinct from discounted paid seal cost.");
        notes.Add("Seal of the Crusader uses the independently pinned historical Forever 1.4 haste/1÷1.4 auto damage and level-scaled Attack Power model, labeled provisional rather than a current realm observation. Hammer of the Righteous trainer availability is pending evidence. Shieldsworn uses the later official broad spell-block/all-block-trigger wording, including Reckoning; the client enumerates Holy Shield and Shield Specialization, so server scope remains provisional. A window starts on any shield Holy Strike attempt, while free block effects require a landed strike. This is a source conflict disclosure, not a missing implementation.");
        return new(r,raw,stats,mana,health,baseline,shared,weaponKind,shield,abilities,rotation,notes);
    }

    public static PaladinPrecombatPreview Preview(PaladinSimRequest request)
    {
        var p=Prepare(request);var stats=new StatCollection {Values=new(p.Stats.Values,StringComparer.OrdinalIgnoreCase)};
        stats.Set("mana",p.Mana);stats.Set("health",p.Health);
        return new(stats,p.Stats.Get("attack-power"),p.Stats.Get("spell-power"),p.Stats.Get("healing-power"),p.Mana,p.Health,
            p.Baseline is null?null:ForeverLevel60Baseline.Summary(p.Baseline),"current-source-provisional-with-component-disclosures",
            p.Notes,new(p.Rotation.Entries.AsReadOnly()),p.Abilities.Where(a=>!a.IsPassive).Select(a=>a.Key).ToArray(),p.WeaponKind,p.Shield,p.RawRequest);
    }

    public PaladinSimResponse Run(PaladinSimRequest request,CancellationToken cancellationToken=default)
    {
        var p=Prepare(request);var r=p.Request;
        var initialAp=p.Stats.Get("attack-power");var initialSp=p.Stats.Get("spell-power");var initialHealing=p.Stats.Get("healing-power");
        var context=new SimulationContext(new() {Seed=r.Seed,DurationSeconds=r.DurationSeconds,CaptureTimeline=r.CaptureTimeline,
            PrimaryActorKey="paladin",MaximumProcessedEvents=180000,CancellationToken=cancellationToken});
        var actor=new SimulationActorState {Key="paladin",Name=r.Character.Name,TeamKey="raid",Level=60,Stats=p.Stats,MaximumHealth=p.Health};
        actor.InitializeHealth(p.Health,p.Health*r.InitialHealthPercent/100m);
        actor.AddResource(new ResourceState("mana",p.Mana,p.Mana*r.InitialManaPercent/100m));context.AddActor(actor);
        for(var i=0;i<r.TargetCount;i++)
        {
            var target=new SimulationActorState {Key=i==0?"target":$"target-{i+1}",Name=$"Level 63 {r.TargetCreatureType} target {i+1}",
                TeamKey="enemy",Level=63,MaximumHealth=r.TargetMaximumHealth};
            target.InitializeHealth(r.TargetMaximumHealth,r.TargetMaximumHealth*r.TargetInitialHealthPercent/100m);
            target.Stats.Set("armor",r.TargetArmor);target.Stats.Set("attack-power",r.IncomingAttackPower);context.AddActor(target);
        }
        if(r.FriendlyTargetEnabled)
        {
            var friendly=new SimulationActorState {Key="friendly",Name="Bounded friendly party target",TeamKey="raid",Level=60,MaximumHealth=r.FriendlyTargetMaximumHealth};
            friendly.InitializeHealth(r.FriendlyTargetMaximumHealth,r.FriendlyTargetMaximumHealth*r.FriendlyTargetInitialHealthPercent/100m);
            context.AddActor(friendly);
        }
        foreach(var ability in p.Abilities)actor.AddAbility(ability);
        var auras=new AuraManager();var absorbs=new AbsorbManager();
        var rolls=new PaladinCombatRolls(r,p.Shield);var mitigation=new PaladinMitigation(r,p.WeaponKind,rolls);
        var executor=new AbilityExecutor(auras,rolls,mitigation,abilityTimingProvider:new PaladinTiming(r),
            completionOrderedAbilityKeys:new HashSet<string>{"holy-light","flash-of-light","lights-vigil","holy-wrath","hammer-of-wrath","turn-undead"});
        var autos=new AutoAttackProcessor(executor,new ForeverAutoAttackTimingProvider());
        var main=ForeverAutoAttackFactory.CreatePlayerMelee("main-hand-auto","Paladin main hand",r.WeaponMinDamage,r.WeaponMaxDamage,
            r.WeaponSpeedSeconds,"main-hand-weapon-skill");
        main.DamageEffect.CanGlance=false; // No unreviewed glancing distribution is silently supplied.
        var mechanic=new PaladinCombatProcessor(r,actor,auras,absorbs,executor,autos,main,p.WeaponKind,p.Shield,rolls);
        mitigation.TransferSacrifice=mechanic.TransferSacrifice;
        var regen=new FiveSecondManaRegenerationProcessor(actor.Key,r.ManaRegenPerSecond,actor.Stats.Get("mp5"),.15m*r.Talents.Rank("reverence"));
        var inventory=new CombatUseItemProcessor(r.CombatItems,executor,actor,"target",actor.Key);
        var equipment=new ForeverEquipmentEffectProcessor(r.Character.Equipment,actor.Key,"target",auras,executor,r.Seed,
            autoAttacks:autos,classKey:"paladin",onStatDelta:(ctx,a,delta)=>
            {
                ApplyEquipmentStatDelta(ctx,a,delta,r.Talents);
                mechanic.RefreshAuras(ctx);
            });
        var debuffs=new ForeverExternalTargetDebuffProcessor(r.ExternalModifiers,60,auras);
        var windfury=new TriggeredMeleeExtraAttackProcessor(executor,actor.Key,main,"windfury-totem-extra",
            r.WindfuryTotemProcChancePercent,246m,1,isEnabled:ctx=>r.UseWindfuryTotem&&p.WeaponKind!="none"&&mechanic.CanMelee(ctx));
        var rotation=new PriorityRotationExecutor(p.Rotation,actor.Key,"target",executor,reactToTargetStateChanges:true,
            autoAttackProcessor:autos,evaluateOffGlobalCooldownDuringInputLock:true);
        var engine=new SimulationEngine([executor,auras,absorbs,mechanic,autos,equipment,debuffs,new PaladinWindfuryGate(windfury),new PaladinManaRegenGate(regen),inventory,
            new ThreatManager(new PaladinThreat(r,p.WeaponKind)),rotation]);
        regen.Initialize(context);
        var run=engine.Run(context,mechanic.Initialize);
        equipment.WriteMetrics(run);mechanic.WriteMetrics(run);
        run.CustomMetrics["paladin-windfury-procs"]=windfury.ProcCount;
        run.CustomMetrics["paladin-windfury-extra-attacks"]=windfury.ExtraAttackCount;
        var summary=run.Summary.ActorSummaries[actor.Key];
        return new() {Dps=summary.DamageDone/r.DurationSeconds,DamageDone=summary.DamageDone,HealingDone=summary.HealingDone,
            OverhealingDone=summary.OverhealingDone,AbsorptionDone=summary.AbsorptionDone,DamageTaken=summary.DamageTaken,
            BlockedDamage=summary.BlockedDamageReceived,EndingMana=actor.Resources["mana"].Current,MaximumMana=p.Mana,
            MaximumHealth=p.Health,EndingMaximumMana=actor.Resources["mana"].Maximum,EndingMaximumHealth=actor.MaximumHealth,
            EndingHealth=actor.CurrentHealth,FriendlyEndingHealth=context.GetActor("friendly")?.CurrentHealth??0m,
            AttackPower=initialAp,SpellPower=initialSp,HealingPower=initialHealing,
            DamageByAbility=new(summary.DamageDoneByAbility,StringComparer.OrdinalIgnoreCase),CombatItemUses=new(inventory.Uses,StringComparer.OrdinalIgnoreCase),
            Run=run,ModelingNotes=p.Notes};
    }

    /// <summary>Incremental talent consumers after the shared equipment processor
    /// has applied its raw/derived delta. Expiration supplies the inverse delta;
    /// permanent inputs are never re-added and increased Mana capacity grants no Mana.</summary>
    public static void ApplyEquipmentStatDelta(SimulationContext context,SimulationActorState actor,
        IReadOnlyDictionary<string,decimal> delta,ForeverPaladinTalents talents)
    {
        var strengthBonus=delta.GetValueOrDefault("strength")*.02m*talents.Rank("divine-strength");
        var intellectBonus=delta.GetValueOrDefault("intellect")*.02m*talents.Rank("divine-intellect");
        actor.Stats.Add("strength",strengthBonus);actor.Stats.Add("intellect",intellectBonus);
        actor.Stats.Add("attack-power",strengthBonus*ForeverPrimaryPhysicalStatProfiles.GetRequired("paladin").AttackPowerPerStrength+
            delta.GetValueOrDefault("melee-attack-power"));
        actor.Stats.Add("block-value",ForeverPhysicalStatConversions.StrengthToBlockValue(actor.Stats.Get("strength"))-
            ForeverPhysicalStatConversions.StrengthToBlockValue(actor.Stats.Get("strength")-strengthBonus));
        actor.Stats.Add(ForeverCombatStatKeys.SpellCriticalChancePercent,intellectBonus/
            ForeverPrimarySpellStatProfiles.GetIntellectPerSpellCriticalPercent("paladin",60));
        actor.Stats.Add("spell-power",(delta.GetValueOrDefault("intellect")+intellectBonus)*.12m*talents.Rank("champion-of-the-light"));
        actor.Stats.Add("healing-power",delta.GetValueOrDefault("spell-power"));
        actor.Stats.Add("armor",(delta.GetValueOrDefault("armor")-2m*delta.GetValueOrDefault("agility"))*.02m*talents.Rank("toughness"));
        actor.Stats.Add("defense-skill",delta.GetValueOrDefault("defense"));
        ForeverEquipmentDynamicResourceCaps.Apply(context,actor,delta,intellectManaMultiplier:1m+.02m*talents.Rank("divine-intellect"));
    }

    private sealed class TalentPrimaryStats(ForeverPaladinTalents t):ICharacterSimulationStatContributor
    {
        public string Key=>"paladin-classwide-primary-talents";
        public int Order=>CharacterSimulationStatContributorOrders.Derived-1;
        public void Contribute(CharacterProfile profile,StatCollection stats)
        {
            stats.Set("strength",Math.Max(0m,stats.Get("strength"))*(1m+.02m*t.Rank("divine-strength")));
            stats.Set("intellect",Math.Max(0m,stats.Get("intellect"))*(1m+.02m*t.Rank("divine-intellect")));
            var equipmentArmor=profile.Equipment.Items.Sum(x=>x.Stats.Get("armor")+x.Modifiers.Sum(m=>m.Stats.Get("armor")));
            stats.Add("armor",Math.Max(0m,equipmentArmor)*.02m*t.Rank("toughness"));
        }
    }

    private static void ValidateUtility(PaladinUtilityScenario? s,bool friendly,decimal duration)
    {
        if(s is null)return;
        if(s.InitialTargetDistanceYards is <0m or >100m||s.BaseMovementSpeedYardsPerSecond is <=0m or >30m||
            s.OtherMovementSpeedBonusPercent is <0m or >200m||s.TargetFleeSpeedYardsPerSecond is <0m or >30m||
            s.Effects is null||s.Effects.Count>32||s.Effects.Any(e=>e is null||e.AtSeconds<0m||e.AtSeconds>=duration||
                e.DurationSeconds is <=0m or >60m||e.DamagePerSecond is <0m or >5000m||
                e.Kind is not ("silence" or "stun" or "charm" or "fear" or "disorient" or "root" or "slow" or "poison" or "disease" or "magic")||
                e.Target is not ("paladin" or "friendly")||e.Target=="friendly"&&!friendly))
            throw new ArgumentException("Invalid bounded Paladin utility scenario; every effect requires a valid target, time, kind and duration.");
    }

    public static RotationProfile BuildRotation(PaladinSimRequest r,IReadOnlyList<AbilityDefinition> abilities)
    {
        var book=abilities.ToDictionary(a=>a.Key,StringComparer.Ordinal);var automatic=r.CustomPriority is null;
        var keys=automatic?new List<string> {
            "cleanse","blessing-of-freedom","templars-bulwark","divine-shield","lay-on-hands",
            "divine-favor","oath-of-light","lights-vigil","holy-shock-heal","holy-light","flash-of-light",
            "righteous-fury","aura-"+r.Aura,"holy-shield","seal-of-"+r.Seal,
            r.SecondarySeal is null?"":"seal-of-"+r.SecondarySeal,
            "hammer-of-wrath","swift-judgement","judgement","holy-strike","exorcism","holy-shock","holy-wrath","consecration"
        }:new List<string>(r.CustomPriority!);
        if(!automatic&&(keys.Count is <1 or >64||keys.Count!=keys.Distinct(StringComparer.Ordinal).Count()))
            throw new ArgumentException("Custom Paladin priority requires 1–64 distinct action keys.");
        var rotation=new RotationProfile {Name="Paladin rank/equipment/condition priority",ClassKey="paladin",RulesetKey="wow-forever",SimulationType=SimulationType.Dps};
        foreach(var key in keys)
        {
            if(!book.TryGetValue(key,out var ability))
            {if(automatic)continue;throw new ArgumentException($"Paladin priority action '{key}' is not available for the current talents, weapon/shield or scenario.");}
            var e=new RotationEntry {Priority=rotation.Entries.Count+1,AbilityKey=key};
            void cond(string type,string? valueKey=null,decimal? value=null,string op=RotationComparisonOperators.LessThanOrEqual)=>
                e.Conditions.Add(new(){ConditionType=type,Key=valueKey,Value=value,ComparisonOperator=op,RequireSourceOwnership=true});
            // Keep the executor's real prerequisites and also tell the priority
            // scheduler which aura removals can make this action available.
            foreach(var forbidden in ability.ForbiddenSourceAuraKeys)
                e.Conditions.Add(new(){ConditionType=RotationConditionTypes.SourceAuraMissing,Key=forbidden,RequireSourceOwnership=false});
            foreach(var forbidden in ability.ForbiddenTargetAuraKeys)
                e.Conditions.Add(new(){ConditionType=RotationConditionTypes.TargetAuraMissing,Key=forbidden,RequireSourceOwnership=false});
            if(PaladinSpellbook.HealingActions.Contains(key)||key.StartsWith("blessing-of-",StringComparison.Ordinal))
                e.Target=new() {Mode=RotationTargetSelectionModes.Fixed,ActorKey=r.HealTarget=="friendly"?"friendly":"paladin"};
            if(key=="lights-vigil")e.Target=new() {Mode=RotationTargetSelectionModes.Fixed,ActorKey="friendly"};
            if(key=="blessing-of-sacrifice")e.Target=new() {Mode=RotationTargetSelectionModes.Fixed,ActorKey="friendly"};
            if(key is "blessing-of-might" or "blessing-of-wisdom" or "blessing-of-sacrifice" or "blessing-of-kings")
                cond(RotationConditionTypes.TargetAuraMissing,"paladin-"+key);
            if(key.StartsWith("aura-",StringComparison.Ordinal))cond(RotationConditionTypes.SourceAuraMissing,"paladin-"+key);
            if(key=="righteous-fury")
            {if(automatic&&!r.UseRighteousFury)continue;cond(RotationConditionTypes.SourceAuraMissing,"paladin-righteous-fury");}
            if(key.StartsWith("seal-of-",StringComparison.Ordinal))
            {
                if(key=="seal-of-"+r.Seal)cond(RotationConditionTypes.SourceStatValue,"paladin-primary-seal-needed",1m,RotationComparisonOperators.Equal);
                else if(key=="seal-of-"+r.SecondarySeal)cond(RotationConditionTypes.SourceStatValue,"paladin-secondary-seal-needed",1m,RotationComparisonOperators.Equal);
                else cond(RotationConditionTypes.SourceAuraMissing,"paladin-seal-"+key[8..]);
            }
            if(key is "judgement" or "swift-judgement")cond(RotationConditionTypes.SourceStatValue,"paladin-target-distance",10m);
            if(key=="holy-strike")cond(RotationConditionTypes.SourceStatValue,"paladin-target-distance",5m);
            if(key=="consecration")cond(RotationConditionTypes.SourceStatValue,"paladin-target-distance",8m);
            if(key is "exorcism" or "hammer-of-wrath")cond(RotationConditionTypes.SourceStatValue,"paladin-target-distance",30m);
            if(key=="holy-shock")cond(RotationConditionTypes.SourceStatValue,"paladin-target-distance",ForeverPaladinSourceData.HolyShockDamageRangeYards);
            if(key=="holy-wrath")cond(RotationConditionTypes.SourceStatValue,"paladin-target-distance",ForeverPaladinSourceData.HolyWrathRadiusYards);
            if(key=="hammer-of-justice")cond(RotationConditionTypes.SourceStatValue,"paladin-target-distance",10m);
            if(key is "repentance" or "turn-undead")cond(RotationConditionTypes.SourceStatValue,"paladin-target-distance",20m);
            if(key=="holy-shield")cond(RotationConditionTypes.SourceAuraMissing,"paladin-holy-shield");
            if(key=="oath-of-light")
            {
                if(automatic&&r.OathTarget=="none")continue;
                e.Target=new() {Mode=RotationTargetSelectionModes.Fixed,ActorKey=r.OathTarget=="friendly"?"friendly":r.OathTarget=="self"?"paladin":r.HealTarget=="friendly"?"friendly":"paladin"};
                cond(RotationConditionTypes.TargetAuraMissing,"paladin-oath-of-light");
            }
            if(key=="lights-vigil")cond(RotationConditionTypes.TargetAuraMissing,"paladin-lights-vigil");
            if(key=="divine-favor")cond(RotationConditionTypes.SourceAuraMissing,"paladin-divine-favor");
            if(key is "repentance" or "hammer-of-justice" or "turn-undead")cond(RotationConditionTypes.TargetAuraMissing,
                key=="repentance"?"paladin-incapacitated":key=="hammer-of-justice"?"paladin-stunned":"paladin-turned");
            if(automatic)
            {
                if(key=="holy-shield"&&r.IncomingBossHitDamage==0m&&r.IncomingAttackPower==0m)continue;
                if(key=="templars-bulwark")
                {if(r.IncomingBossHitDamage==0m&&r.IncomingAttackPower==0m)continue;cond(RotationConditionTypes.SourceHealthPercent,value:85m);}
                if(key=="divine-shield")cond(RotationConditionTypes.SourceHealthPercent,value:25m);
                if(key=="lay-on-hands")cond(RotationConditionTypes.TargetHealthPercent,value:20m);
                if(key is "holy-light" or "flash-of-light" or "holy-shock-heal" or "lights-vigil")
                    cond(RotationConditionTypes.TargetHealthPercent,value:key=="holy-light"?Math.Min(r.HealBelowHealthPercent,45m):r.HealBelowHealthPercent);
                if(key=="divine-favor")cond(RotationConditionTypes.SourceStatValue,"paladin-heal-target-health-percent",r.HealBelowHealthPercent);
                if(key=="oath-of-light"&&!r.FriendlyTargetEnabled&&r.IncomingBossHitDamage==0m&&r.IncomingAttackPower==0m)continue;
                if(key=="cleanse")cond(RotationConditionTypes.SourceStatValue,"paladin-heal-target-cleansable",1m,RotationComparisonOperators.GreaterThanOrEqual);
                if(key=="blessing-of-freedom")cond(RotationConditionTypes.SourceStatValue,"paladin-heal-target-movement-impaired",1m,RotationComparisonOperators.GreaterThanOrEqual);
                if(key=="holy-wrath"&&r.TargetCount<2)continue;
                if(key=="consecration")cond(RotationConditionTypes.TargetAuraMissing,"paladin-consecration");
            }
            rotation.Entries.Add(e);
        }
        return rotation;
    }
}
