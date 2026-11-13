using System.Text.Json;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Shaman;
using WarcraftSim.Data.Forever.Simulation;

namespace WarcraftSim.Api.Simulation;

/// <summary>Canonical class-wide Shaman execution. Preset origin is deliberately
/// absent. The old typed runner dispatches here only after explicit opt-in.</summary>
public sealed class ShamanClassWideSimRunner
{
    private sealed record Prepared(EnhancementShamanSimRequest Request,StatCollection Stats,
        decimal AttackPower,decimal SpellPower,decimal Mana,decimal Health,
        ForeverLevel60BaselineObservation? Baseline,ForeverSharedCharacterInputPreparation Shared,
        ForeverRestorationShamanTuning Restoration,IReadOnlyList<AbilityDefinition> Abilities,
        RotationProfile Rotation,List<string> Notes);

    private static Prepared Prepare(EnhancementShamanSimRequest source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var r=EnhancementShamanSimulationRequestSnapshot.Create(source);
        if(!r.UseClassWideTalents || r.Character is null || r.Talents?.Ranks is null || r.Tuning is null ||
            r.Character.ClassKey!="shaman" || r.Character.Level!=60 || r.DurationSeconds is <=0m or >1200m ||
            r.MaximumHealth<=0m || r.TargetMaximumHealth<=0m || r.TargetArmor<0m ||
            r.WeaponSpeedSeconds<=0m || r.WeaponMinDamage<0m || r.WeaponMaxDamage<r.WeaponMinDamage ||
            r.DualWield && (r.OffHandWeaponSpeedSeconds<=0m || r.OffHandWeaponMinDamage<0m ||
                r.OffHandWeaponMaxDamage<r.OffHandWeaponMinDamage) ||
            r.IncomingBossHitDamage<0m || r.IncomingBossSwingSeconds<=0m ||
            r.ClassWideTargetCount is <1 or >9 || r.ClassWideTargetInitialHealthPercent is <=0m or >100m ||
            r.TargetDistanceYards is <0m or >100m ||
            r.ClassWideRotationPolicy is not (null or "automatic" or "custom") ||
            r.BaseMeleeHitPercent is <0m or >100m || r.BaseSpellHitPercent is <0m or >100m ||
            r.CriticalChancePercent is <0m or >100m)
            throw new ArgumentException("Invalid bounded class-wide Shaman setup.");
        ForeverShamanClassTalents.Validate(r.Talents.Ranks,r.Talents.ElementalPointsSpent,
            r.Talents.EnhancementPointsSpent,r.Talents.RestorationPointsSpent,r.Character.Level);
        r.Talents.Ranks=new(r.Talents.Ranks,StringComparer.OrdinalIgnoreCase);
        r.UtilityScenario?.Validate();
        r.CombatItems.Validate("shaman");
        r.Character.SpecializationKey="enhancement";
        r.ExternalModifiers??=new();
        var baseline=ForeverLevel60Baseline.Resolve(r.Level60Baseline,r.UseReviewedLevel60Baseline,
            r.Character.Race??"","shaman",r.UseProvisionalLevel60Baseline);
        var shared=ForeverSharedCharacterInputs.Prepare(r.Character,baseline,r.ExternalModifiers,r.DurationSeconds,
            supportedTargetDebuffs:["faerie-fire-rank4"]);
        r.Character=shared.Character;
        if(shared.Weapons.TryGetValue("main-hand",out var main))
        {r.WeaponMinDamage=main.MinimumDamage;r.WeaponMaxDamage=main.MaximumDamage;r.WeaponSpeedSeconds=main.SpeedSeconds;}
        if(shared.Weapons.TryGetValue("off-hand",out var off))
        {
            r.DualWield=true;r.OffHandWeaponMinDamage=off.MinimumDamage;
            r.OffHandWeaponMaxDamage=off.MaximumDamage;r.OffHandWeaponSpeedSeconds=off.SpeedSeconds;
        }
        else if(shared.Weapons.Count>0)r.DualWield=false;
        var mainImbue=r.MainHandImbue??(r.UseWindfuryWeapon?"windfury":"none");
        if(mainImbue is not ("none" or "windfury" or "flametongue" or "rockbiter" or "frostbrand") ||
            r.OffHandImbue is not ("none" or "flametongue" or "rockbiter" or "frostbrand") ||
            !r.DualWield && r.OffHandImbue!="none")
            throw new ArgumentException("Invalid weapon-specific Shaman imbue configuration.");
        r.MainHandImbue=mainImbue;
        var calculator=ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_=>r.MaximumHealth,
            additionalStatContributors:[new TalentPrimaryStats(r.Talents)]);
        var stats=calculator.Calculate(r.Character).EffectiveStats;
        var intellect=stats.Get("intellect");
        var ap=Math.Max(0m,stats.Get("attack-power")+stats.Get("melee-attack-power")+
            intellect*ForeverShamanClassTalents.MentalDexterityFraction(r.Talents.Rank("mental-dexterity")));
        var sp=Math.Max(0m,stats.Get("spell-power")+intellect*.15m*r.Talents.Rank("mental-quickness"));
        var healing=Math.Max(0m,stats.Get("healing-power")+sp);
        stats.Set("attack-power",ap);stats.Set("spell-power",sp);stats.Set("healing-power",healing);
        stats.Set("main-hand-weapon-skill",300m);
        if(r.DualWield)stats.Set("off-hand-weapon-skill",300m);
        stats.Set("shaman-target-distance",r.TargetDistanceYards??3m);
        var mana=baseline?.ObservedUnbuffedMaximumMana is decimal observedMana
            ? observedMana+15m*(Math.Max(0m,intellect-20m)-Math.Max(0m,baseline.Intellect-20m))+stats.Get("mana")
            : ForeverClientBaseManaCatalog.GetBaseMana("shaman",60)+Math.Max(0m,intellect-20m)*15m+stats.Get("mana");
        var staminaDelta=stats.Get("stamina")-(baseline?.Stamina??r.Character.BaseStats.Get("stamina"));
        var health=((baseline?.ObservedUnbuffedMaximumHealth??r.MaximumHealth)+10m*staminaDelta+stats.Get("health"))*
            (1m+.02m*r.Talents.Rank("improved-reincarnation"));
        if(mana<=0m || health<=0m)throw new ArgumentException("Prepared Shaman Mana and Health must be positive.");
        var restoration=r.ClassWideRestorationTuning??ForeverShamanClassSpellbook.ReviewedRestorationTuning();
        restoration.Validate();
        // All direct/periodic spell scaling reads the prepared live actor stats.
        // Tuning HealingPower is not another independent character stat source.
        restoration.HealingPower=0m;
        var elemental=r.ClassWideElementalTuning??ForeverShamanClassSpellbook.ReviewedElementalTuning();
        var abilities=ForeverShamanClassSpellbook.Create(r.Talents,r.Tuning,elemental,restoration,
            r.WeaponSpeedSeconds,r.WeaponMinDamage,r.WeaponMaxDamage,r.DualWield,
            ForeverClientBaseManaCatalog.GetBaseMana("shaman",60)).ToList();
        if(r.UtilityScenario?.IsIndoors==true&&r.Talents.Rank("improved-ghost-wolf")==0)
            abilities.RemoveAll(x=>x.Key=="ghost-wolf");
        foreach(var (key,selected) in new[]{("call-of-elements",r.CallOfElementsTotems),
            ("call-of-ancestors",r.CallOfAncestorsTotems),("call-of-spirits",r.CallOfSpiritsTotems)})
            abilities.Add(ForeverShamanTotemCalls.Call(key,abilities,selected));
        abilities.Add(ForeverShamanTotemCalls.Recall());
        foreach(var ability in abilities.Where(x=>!x.IsPassive&&x.Key is not ("ghost-wolf" or "cancel-ghost-wolf")))
            if(!ability.ForbiddenSourceAuraKeys.Contains("shaman-ghost-wolf"))ability.ForbiddenSourceAuraKeys.Add("shaman-ghost-wolf");
        var rotation=BuildRotation(r,abilities);
        var notes=shared.Notes.ToList();
        notes.Add("One class-wide Shaman engine and conditional policy is selected only by effective talents, character inputs and encounter. Preset origin has no gameplay input. All 50 current talent nodes are legal editable allocations; mechanical coverage is reported independently.");
        notes.Add("Client talent topology build 1.60.1.70888 is mirrored by https://theforeverera.com/en/talents/shaman/ (reviewed 2026-11-12); official November 2/10 notes supersede stale tooltip values for renamed talents, Stormstrike, Flurry, Overload, Maelstrom, Mental Dexterity and Riptide. This is provisional source confidence, not server certification.");
        notes.Add("Mana uses client class base Mana plus Intellect or supplied naked observed Mana plus deltas. Authored health is the naked scenario cap unless a baseline observation supplies it; Stamina, flat Health and Improved Reincarnation apply once. Manual weapon budgets remain explicit when catalog eligibility evidence is pending.");
        notes.Add("Current70888 client spellbook mirror https://theforeverera.com/en/spells/shaman/ supplies Bolt334–372, Chain482–538/495Mana, EarthShock517–545, FrostShock492–520, FlameShock292+320/12s/410Mana and Riptide805/15s/230Mana/12s cooldown. Current FireNova226–252 midpoint239 takes precedence over historical upstream232–258 midpoint245. Magma73base every2s and20s duration gains10s/rank Trial. Historical SP coefficients remain independently provisional; client mirror evidence is not server attestation.");
        notes.Add("Current mechanics: Maelstrom6–10PPM by rank with main-hand speed, every rank20% cost/time per stack; Flurry6% attack speed/rank for3swings; Lightning Overload10%/rank, half damage and zero threat; Stormstrike100% normalized weapon damage/8s and next eligible Nature spell +20%; Windfury Weapon and Totem coexist. Cast haste formula and shield internal cooldowns remain provisional.");
        notes.Add("Focused utility scenarios use explicit one-dimensional player/target speeds, controllable-target flags, incoming melee/ranged/spell categories and opt-in Reincarnation. Boss control immunity remains the default. They execute Earthbind/root radius, Stoneclaw health/redirection, Ghost Wolf travel/indoor gates, Windwall/Grounding and totem relocation. Ghost Wolf weapon attacks, full group geometry and raid tank/healer workflows are outside this bounded scenario. Spirit regeneration remains the explicit configurable per-second input; no unsupported Spirit formula is inferred.");
        return new(r,stats,ap,sp,mana,health,baseline,shared,restoration,abilities,rotation,notes);
    }

    public static EnhancementPrecombatPreview Preview(EnhancementShamanSimRequest request)
    {
        var p=Prepare(request);
        var stats=new StatCollection{Values=new(p.Stats.Values,p.Stats.Values.Comparer)};
        stats.Set("mana",p.Mana);stats.Set("health",p.Health);
        return new(stats,p.AttackPower,p.SpellPower,p.Mana,p.Health,
            p.Baseline is null?null:ForeverLevel60Baseline.Summary(p.Baseline),
            "reviewed-current-talent-mechanics-with-provisional-values",p.Notes,new(p.Rotation.Entries.AsReadOnly()))
        {ClassWide=true};
    }

    public EnhancementShamanSimResponse Run(EnhancementShamanSimRequest request,CancellationToken cancellationToken=default)
    {
        var p=Prepare(request);var r=p.Request;
        var context=new SimulationContext(new SimulationRunOptions
        {
            Seed=r.Seed,DurationSeconds=r.DurationSeconds,CaptureTimeline=r.CaptureTimeline,
            PrimaryActorKey="shaman",MaximumProcessedEvents=150000,CancellationToken=cancellationToken
        });
        var actor=new SimulationActorState{Key="shaman",Name=r.Character.Name,TeamKey="raid",Level=60,
            MaximumHealth=p.Health,Stats=p.Stats};
        actor.InitializeHealth(p.Health);actor.AddResource(new ResourceState("mana",p.Mana,p.Mana));
        var healCrit=(r.UseManualCombatAssumptions?r.CriticalChancePercent:actor.Stats.Get(ForeverCombatStatKeys.SpellCriticalChancePercent))+
            r.Talents.Rank("thundering-strikes");
        actor.Stats.Set("shaman-healing-critical-percent",healCrit);
        context.AddActor(actor);
        for(var i=0;i<(r.ClassWideTargetCount??1);i++)
        {
            var target=new SimulationActorState{Key=i==0?"target":$"target-{i+1}",Name=$"Level63 target{i+1}",
                Level=63,TeamKey="enemy",MaximumHealth=r.TargetMaximumHealth};
            target.Stats.Set("armor",r.TargetArmor);
            target.InitializeHealth(r.TargetMaximumHealth,r.TargetMaximumHealth*(r.ClassWideTargetInitialHealthPercent??100m)/100m);
            context.AddActor(target);
        }
        foreach(var ability in p.Abilities)actor.AddAbility(ability);
        var auras=new AuraManager();
        var devastation=new ShamanElementalDevastationProcessor(actor,auras,r.Talents.Rank("elemental-devastation"),classWide:true);
        ForeverEnhancementShamanReactiveProcessor? reactive=null;
        ShamanClassWideCombatProcessor? elemental=null;
        var mitigation=new ShamanClassWideMitigation(r.Talents);
        var executor=new AbilityExecutor(auras,new ShamanClassWideRolls(r,devastation),mitigation,
            abilityTimingProvider:new ShamanClassWideTimingProvider(()=>reactive?.CurrentMaelstromStacks??0),
            completionOrderedAbilityKeys:new HashSet<string>{"lightning-bolt","chain-lightning","healing-wave","lesser-healing-wave","chain-heal"},
            resourceCostPolicy:()=>elemental);
        var autos=new AutoAttackProcessor(executor,new ForeverAutoAttackTimingProvider());
        var main=ForeverAutoAttackFactory.CreatePlayerMelee("main-hand-auto","Shaman main hand",r.WeaponMinDamage,
            r.WeaponMaxDamage,r.WeaponSpeedSeconds,"main-hand-weapon-skill",usesDualWieldHitTable:r.DualWield);
        main.DamageEffect.ScalingStatKey="shaman-main-hand-attack-power";
        var off=r.DualWield?ForeverAutoAttackFactory.CreatePlayerDualWieldOffHand("off-hand-auto","Shaman off hand",
            r.OffHandWeaponMinDamage,r.OffHandWeaponMaxDamage,r.OffHandWeaponSpeedSeconds,"off-hand-weapon-skill"):null;
        if(off is not null)off.DamageEffect.ScalingStatKey="shaman-off-hand-attack-power";
        void setMelee(SimulationContext ctx,bool active)
        {
            foreach(var definition in new[]{main,off}.Where(x=>x is not null))
            {
                var running=actor.AutoAttacks.GetValueOrDefault(definition!.Key)?.IsActive==true;
                if(active&&!running)autos.Start(ctx,actor.Key,"target",definition,0m);
                else if(!active&&running)autos.Stop(ctx,actor.Key,definition.Key);
            }
        }
        if(actor.Abilities.TryGetValue("stormstrike",out var strike))strike.Definition.Effects[0].ScalingStatKey="shaman-main-hand-attack-power";
        var imbues=new ShamanWeaponImbueProcessor(actor,auras,executor,p.AttackPower,r.WeaponSpeedSeconds,
            r.OffHandWeaponSpeedSeconds,r.Talents.Rank("elemental-weapons"),r.FrostbrandProcChancePercent);
        var slots=new ShamanTotemLifecycleProcessor(actor,auras,imbues.RefreshHandAttackPower);
        var fire=new ShamanFireTotemProcessor(actor,auras,classWide:true);
        var shield=new ShamanLightningShieldProcessor(actor,auras,executor,r.LightningShieldInternalCooldownSeconds,
            damageMultiplier:1m+.05m*r.Talents.Rank("improved-lightning-shield"));
        reactive=new ForeverEnhancementShamanReactiveProcessor(actor.Key,autos,auras,r.Talents,r.Tuning,
            classWide:true,mainHandSpeed:r.WeaponSpeedSeconds);
        elemental=new ShamanClassWideCombatProcessor(actor,auras,executor,r.Talents,reactive);
        var resto=new RestorationShamanCombatProcessor(actor.Key,p.Restoration,auras,
            ForeverShamanClassSpellbook.RestorationTalents(r.Talents),classWide:true);
        var utility=new ShamanClassWideUtilityProcessor(r,actor,auras,executor,setMelee);
        utility.Initialize(context);
        var wfBonus=r.Tuning.WindfuryWeaponBonusAttackPower*(1m+(r.Talents.Rank("elemental-weapons") switch
            {1=>.13m,2=>.27m,3=>.4m,_=>0m}));
        var weapon=r.MainHandImbue=="windfury"?new TriggeredMeleeExtraAttackProcessor(executor,actor.Key,main,
            "windfury-weapon-extra",r.Tuning.WindfuryWeaponProcChancePercent,wfBonus,2,r.Tuning.WindfuryWeaponInternalCooldownSeconds):null;
        var windfury=new TriggeredMeleeExtraAttackProcessor(executor,actor.Key,main,"windfury-totem-extra",
            r.WindfuryTotemProcChancePercent,246m,1,isEnabled:ctx=>actor.ActiveAuras.Any(a=>
                a.Definition.Key=="shaman-windfury-totem"&&a.IsActiveAt(ctx.CurrentTimeSeconds))&&utility.InTotemRange(ctx,"windfury-totem"));
        var spiritRegen=r.Tuning.ManaRegenPerSecond;
        var regen=new FiveSecondManaRegenerationProcessor(actor.Key,spiritRegen,actor.Stats.Get("mp5"),
            ForeverShamanClassTalents.MindfulnessFraction(r.Talents.Rank("mindfulness")),()=>reactive.ImprovedStormstrikeCastingSpiritFraction);
        reactive.AttachManaRegeneration(regen);
        var items=new CombatUseItemProcessor(r.CombatItems,executor,actor,"target",actor.Key);
        var equipment=new ForeverEquipmentEffectProcessor(r.Character.Equipment,actor.Key,"target",auras,executor,r.Seed,
            autoAttacks:autos,classKey:"shaman",onStatDelta:(ctx,a,delta)=>
            {
                var bonusInt=delta.GetValueOrDefault("intellect")*.02m*r.Talents.Rank("ancestral-knowledge");
                var effectiveInt=delta.GetValueOrDefault("intellect")+bonusInt;
                var bonusStamina=delta.GetValueOrDefault("stamina")*.02m*r.Talents.Rank("toughness");
                a.Stats.Set("intellect",a.Stats.Get("intellect")+bonusInt);
                a.Stats.Set("stamina",a.Stats.Get("stamina")+bonusStamina);
                a.Stats.Set("attack-power",a.Stats.Get("attack-power")+delta.GetValueOrDefault("melee-attack-power")+effectiveInt*
                    ForeverShamanClassTalents.MentalDexterityFraction(r.Talents.Rank("mental-dexterity")));
                var mq=effectiveInt*.15m*r.Talents.Rank("mental-quickness");
                a.Stats.Set("spell-power",a.Stats.Get("spell-power")+mq);
                a.Stats.Set("healing-power",a.Stats.Get("healing-power")+delta.GetValueOrDefault("spell-power")+mq);
                a.Stats.Set(ForeverCombatStatKeys.SpellCriticalChancePercent,
                    a.Stats.Get(ForeverCombatStatKeys.SpellCriticalChancePercent)+bonusInt/
                    ForeverPrimarySpellStatProfiles.GetIntellectPerSpellCriticalPercent("shaman",60));
                a.Stats.Set("shaman-healing-critical-percent",(r.UseManualCombatAssumptions?r.CriticalChancePercent:
                    a.Stats.Get(ForeverCombatStatKeys.SpellCriticalChancePercent))+r.Talents.Rank("thundering-strikes"));
                var weaponHaste=(1m+a.Stats.Get("shaman-farseer-haste-percent")/100m)*
                    (1m+a.Stats.Get("shaman-flurry-haste-percent")/100m);
                a.Stats.Set(ForeverCombatStatKeys.HastePercent,a.Stats.Get(ForeverCombatStatKeys.HastePercent)+
                    delta.GetValueOrDefault(ForeverCombatStatKeys.HastePercent)*(weaponHaste-1m));
                var capacityDelta=new Dictionary<string,decimal>(delta,StringComparer.OrdinalIgnoreCase)
                    {["stamina"]=delta.GetValueOrDefault("stamina")+bonusStamina};
                ForeverEquipmentDynamicResourceCaps.Apply(ctx,a,capacityDelta,
                    intellectManaMultiplier:1m+.02m*r.Talents.Rank("ancestral-knowledge"),
                    staminaHealthMultiplier:1m+.02m*r.Talents.Rank("improved-reincarnation"));
                imbues.RefreshHandAttackPower();
            });
        var debuffs=new ForeverExternalTargetDebuffProcessor(r.ExternalModifiers!,60,auras);
        var threat=new ThreatManager(new ShamanClassWideThreat(r,imbues));
        var rotation=new PriorityRotationExecutor(p.Rotation,actor.Key,"target",executor,autoAttackProcessor:autos);
        var engine=new SimulationEngine([
            executor,auras,elemental,reactive,devastation,slots,fire,imbues,shield,resto,autos,utility,
            new ShamanClassWideIncomingDamage(r,mitigation,utility),equipment,debuffs,
            ..(weapon is null?Array.Empty<ICombatEventProcessor>():new ICombatEventProcessor[]{weapon}),
            windfury,regen,items,threat,rotation
        ]);
        regen.Initialize(context);
        var run=engine.Run(context,ctx=>
        {
            if((r.TargetDistanceYards??3m)<=5m)
            {
                autos.Start(ctx,actor.Key,"target",main,0m);
                if(off is not null)autos.Start(ctx,actor.Key,"target",off,0m);
            }
            if(r.IncomingBossHitDamage>0m)
                for(var at=2m;at<r.DurationSeconds;at+=r.IncomingBossSwingSeconds)
                    ctx.ScheduleEvent(new CombatEvent
                    {
                        TimeSeconds=at,Type=CombatEventType.ScriptedDamage,SourceActorKey="target",TargetActorKey=actor.Key,
                        SchoolKey=r.IncomingDamageSchool??"physical",RawAmount=r.IncomingBossHitDamage,
                        EncounterEventKey="shaman-classwide-incoming"
                    });
        });
        equipment.WriteMetrics(run);
        run.CustomMetrics["shaman-clearcasting-procs"]=elemental.ClearcastingProcs;
        run.CustomMetrics["shaman-clearcasting-consumed"]=elemental.ClearcastingConsumed;
        run.CustomMetrics["shaman-lightning-overload-procs"]=elemental.LightningOverloadProcs;
        run.CustomMetrics["shaman-water-shield-mana"]=resto.ShieldManaRestored;
        run.CustomMetrics["shaman-mana-tide-mana"]=resto.ManaTideRestored;
        run.CustomMetrics["shaman-nature-swiftness-consumed"]=resto.NatureSwiftnessConsumed;
        run.CustomMetrics["shaman-healing-stream-pulses"]=resto.HealingStreamPulses;
        run.CustomMetrics["shaman-pushback-resisted"]=elemental.PushbackResisted;
        run.CustomMetrics["shaman-pushback-seconds"]=elemental.PushbackSeconds;
        run.CustomMetrics["shaman-stormstrike-charges-consumed"]=elemental.StormstrikeChargesConsumed;
        run.CustomMetrics["shaman-elemental-devastation-procs"]=devastation.Procs;
        run.CustomMetrics["shaman-totem-replacements"]=slots.Replacements;
        run.CustomMetrics["shaman-totemic-recall-mana"]=slots.RecallManaGained;
        run.CustomMetrics["shaman-lightning-shield-triggers"]=shield.Triggers;
        run.CustomMetrics["shaman-earthbound-roots"]=utility.EarthboundRoots;
        run.CustomMetrics["shaman-grounded-spells"]=utility.GroundedSpells;
        run.CustomMetrics["shaman-stoneclaw-intercepts"]=utility.StoneclawIntercepts;
        run.CustomMetrics["shaman-relocated-totems"]=utility.RelocatedTotems;
        run.CustomMetrics["shaman-reincarnations"]=utility.Reincarnations;
        run.CustomMetrics["shaman-reincarnation-ready-at"]=utility.ReincarnationReadyAtSeconds;
        run.CustomMetrics["shaman-flametongue-totem-procs"]=utility.FlametongueTotemProcs;
        run.CustomMetrics["shaman-player-distance-travelled"]=utility.PlayerDistanceTravelled;
        run.CustomMetrics["shaman-target-distance-travelled"]=utility.TargetDistanceTravelled;
        var report=run.Summary.ActorSummaries[actor.Key];
        return new EnhancementShamanSimResponse
        {
            Dps=report.DamageDone/r.DurationSeconds,DamageDone=report.DamageDone,EndingMana=actor.Resources["mana"].Current,
            MaximumMana=p.Mana,MaximumHealth=p.Health,EndingHealth=actor.CurrentHealth,AttackPower=p.AttackPower,SpellPower=p.SpellPower,
            WindfuryWeaponProcs=weapon?.ProcCount??0,WindfuryWeaponExtraAttacks=weapon?.ExtraAttackCount??0,
            WindfuryTotemProcs=windfury.ProcCount,DualWieldActive=r.DualWield,
            ActiveMainHandImbue=r.MainHandImbue=="windfury"?"windfury":imbues.MainHand,ActiveOffHandImbue=imbues.OffHand,
            OffHandAutoDamage=report.DamageDoneByAbility.GetValueOrDefault("off-hand-auto"),
            MaelstromInstantBolts=reactive.MaelstromInstantBolts,MaxMaelstromStacks=reactive.MaxMaelstromStacks,
            ImprovedStormstrikeRegenProcs=reactive.ImprovedStormstrikeRegenProcs,
            ImprovedStormstrikeCooldownResets=reactive.ImprovedStormstrikeCooldownResets,
            DamageByAbility=new(report.DamageDoneByAbility,StringComparer.OrdinalIgnoreCase),Run=run,ModelingNotes=p.Notes,
            CombatItemUses=items.Uses.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase)
        };
    }

    private sealed class TalentPrimaryStats(ForeverEnhancementShamanTalents talents):ICharacterSimulationStatContributor
    {
        public string Key=>"shaman-classwide-primary-talents";
        public int Order=>CharacterSimulationStatContributorOrders.Derived-1;
        public void Contribute(CharacterProfile profile,StatCollection stats)
        {
            stats.Set("intellect",Math.Max(0m,stats.Get("intellect"))*(1m+.02m*talents.Rank("ancestral-knowledge")));
            stats.Set("stamina",Math.Max(0m,stats.Get("stamina"))*(1m+.02m*talents.Rank("toughness")));
        }
    }

    public static RotationProfile BuildRotation(EnhancementShamanSimRequest request,IReadOnlyList<AbilityDefinition> abilities)
    {
        var book=abilities.ToDictionary(x=>x.Key,StringComparer.OrdinalIgnoreCase);
        var rotation=new RotationProfile{Name="Shaman class-wide conditional priority",ClassKey="shaman",
            RulesetKey="wow-forever",SimulationType=SimulationType.Dps};
        var automatic=request.ClassWideRotationPolicy!="custom";
        var keys=automatic?new List<string>
        {
            "nature-swiftness","riptide","lesser-healing-wave","water-shield","mana-tide-totem",
            "healing-stream-totem","mana-spring-totem","stoneskin-totem","windfury-totem",
            "strength-of-earth-totem","lightning-mastery","rage-of-the-farseer","maelstrom-lightning-bolt",
            "stormstrike","magma-totem","searing-totem","fire-nova","flame-shock","chain-lightning","earth-shock","lightning-bolt"
        }:request.CustomPriority??throw new ArgumentException("Custom class-wide policy requires a priority list.");
        if(keys.Count is <1 or >64 || keys.Count!=keys.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new ArgumentException("Class-wide priority must contain 1–64 unique actions.");
        void condition(RotationEntry entry,string type,string? key=null,decimal? value=null,string? op=null)
            =>entry.Conditions.Add(new RotationConditionDefinition{ConditionType=type,Key=key,Value=value,
                ComparisonOperator=op??RotationComparisonOperators.LessThanOrEqual,RequireSourceOwnership=true});
        var requested=new List<string>(keys);
        if(automatic)
        {
            if(request.UtilityScenario?.PlayerMovesTowardTarget==true)
            {requested.Insert(0,"cancel-ghost-wolf");requested.Insert(0,"ghost-wolf");}
            foreach(var (hand,imbue) in new[]{("main-hand",request.MainHandImbue),("off-hand",request.OffHandImbue)})
                if(imbue is "flametongue" or "rockbiter" or "frostbrand")requested.Insert(0,$"{imbue}-weapon-{hand}");
        }
        foreach(var key in requested)
        {
            if(!book.TryGetValue(key,out var ability) || ability.IsPassive)
            {if(automatic)continue;throw new ArgumentException($"Priority action '{key}' is not learned or is a passive proc.");}
            var entry=new RotationEntry{Priority=rotation.Entries.Count+1,AbilityKey=key};
            if(ability.Effects.Any(x=>x.TargetType==AbilityTargetTypes.Friendly))entry.Target=new(){Mode=RotationTargetSelectionModes.Self};
            // Simple custom priorities preserve the same live state guards;
            // placing a maintenance action first must not recast it every GCD.
            if(!automatic)
            {
                var aura=key switch
                {
                    "magma-totem" or "searing-totem"=>"shaman-fire-totem-active",
                    "mana-spring-totem"=>"mana-spring-totem",
                    "healing-stream-totem"=>"healing-stream-totem",
                    "stoneskin-totem"=>"shaman-stoneskin-totem",
                    "strength-of-earth-totem"=>"shaman-strength-of-earth-totem",
                    "windfury-totem"=>"shaman-windfury-totem",
                    "earthbind-totem"=>"shaman-earthbind-totem",
                    "stoneclaw-totem"=>"shaman-stoneclaw-totem",
                    "windwall-totem"=>"shaman-windwall-totem",
                    "grounding-totem"=>"shaman-grounding-totem",
                    "flametongue-totem"=>"shaman-flametongue-totem",
                    "call-of-elements"=>ForeverShamanTotemCalls.SelectedPresetAura(request.CallOfElementsTotems),
                    "call-of-ancestors"=>ForeverShamanTotemCalls.SelectedPresetAura(request.CallOfAncestorsTotems),
                    "call-of-spirits"=>ForeverShamanTotemCalls.SelectedPresetAura(request.CallOfSpiritsTotems),
                    _=>null
                };
                if(aura is not null)condition(entry,RotationConditionTypes.SourceAuraMissing,aura);
            }
            if(key=="stormstrike")condition(entry,RotationConditionTypes.SourceStatValue,"shaman-target-distance",5m);
            if(key is "lightning-bolt" or "chain-lightning" or "maelstrom-lightning-bolt")condition(entry,RotationConditionTypes.SourceStatValue,
                "shaman-target-distance",30m+3m*request.Talents.Rank("elemental-reach"));
            if(key is "earth-shock" or "flame-shock" or "frost-shock")condition(entry,RotationConditionTypes.SourceStatValue,
                "shaman-target-distance",20m);
            if(key=="fire-nova")condition(entry,RotationConditionTypes.SourceStatValue,
                "shaman-fire-target-distance",10m+3m*request.Talents.Rank("elemental-reach"));
            if(automatic)
            {
                if(key=="ghost-wolf")condition(entry,RotationConditionTypes.SourceStatValue,"shaman-target-distance",30m,RotationComparisonOperators.GreaterThan);
                if(key=="cancel-ghost-wolf")condition(entry,RotationConditionTypes.SourceStatValue,"shaman-target-distance",30m);
                if(key=="nature-swiftness")condition(entry,RotationConditionTypes.SourceHealthPercent,value:45m);
                if(key is "riptide" or "lesser-healing-wave")condition(entry,RotationConditionTypes.SourceHealthPercent,value:key=="riptide"?75m:55m);
                if(key=="riptide")condition(entry,RotationConditionTypes.SourceAuraMissing,"riptide");
                if(key=="mana-tide-totem")condition(entry,RotationConditionTypes.SourceResourcePercent,"mana",60m);
                if(key=="water-shield")condition(entry,RotationConditionTypes.SourceAuraMissing,"water-shield");
                if(key=="healing-stream-totem")
                {
                    condition(entry,RotationConditionTypes.SourceHealthPercent,value:80m);
                    condition(entry,RotationConditionTypes.SourceAuraMissing,"healing-stream-totem");
                }
                if(key=="mana-spring-totem")
                {
                    condition(entry,RotationConditionTypes.SourceAuraMissing,"mana-spring-totem");
                    condition(entry,RotationConditionTypes.SourceAuraMissing,"healing-stream-totem");
                    condition(entry,RotationConditionTypes.SourceAuraMissing,"mana-tide-active");
                }
                if(key=="stoneskin-totem")
                {
                    if(request.IncomingBossHitDamage==0m)continue;
                    condition(entry,RotationConditionTypes.SourceAuraMissing,"shaman-stoneskin-totem");
                }
                if(key=="strength-of-earth-totem")
                {
                    if(request.IncomingBossHitDamage>0m)continue;
                    condition(entry,RotationConditionTypes.SourceAuraMissing,"shaman-strength-of-earth-totem");
                }
                if(key=="windfury-totem")condition(entry,RotationConditionTypes.SourceAuraMissing,"shaman-windfury-totem");
                if(key=="magma-totem")
                {if((request.ClassWideTargetCount??1)<2)continue;condition(entry,RotationConditionTypes.SourceAuraMissing,"shaman-fire-totem-active");}
                if(key=="searing-totem")condition(entry,RotationConditionTypes.SourceAuraMissing,"shaman-fire-totem-active");
                if(key=="fire-nova" && (request.ClassWideTargetCount??1)<2)continue;
                if(key=="flame-shock")condition(entry,RotationConditionTypes.TargetAuraMissing,"flame-shock");
                if(key=="chain-lightning" && (request.ClassWideTargetCount??1)<2)continue;
                if(key=="earth-shock" && request.Talents.Rank("stormstrike")==0)continue;
            }
            rotation.Entries.Add(entry);
        }
        return rotation;
    }
}
