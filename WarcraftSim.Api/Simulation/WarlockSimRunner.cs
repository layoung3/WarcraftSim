using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Core.Stats;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Simulation;
using WarcraftSim.Data.Forever.Warlock;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP131: three functional Warlock DPS rotations in the same event-driven
/// simulation engine as Shadow Priest and Enhancement. Pet attacks are a real
/// second actor, with independent swing timing and damage attribution.</summary>
public sealed class WarlockSimRunner
{
    public WarlockSimResponse Run(WarlockSimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Character is null || request.Talents is null || request.Tuning is null ||
            request.Character.Level != 60 ||
            !string.Equals(request.Character.ClassKey, "warlock", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.Character.SpecializationKey, request.Specialization, StringComparison.OrdinalIgnoreCase) ||
            request.DurationSeconds is <= 0m or > 1200m ||
            request.TargetMaximumHealth <= 0m || request.WarlockMaximumHealth <= 0m ||
            request.IncomingDamageAtSeconds is <0m ||
            request.IncomingDamageAmount <0m ||
            string.IsNullOrWhiteSpace(request.IncomingDamageSchool) ||
            request.AdditionalTargets is <0 or >8 ||
            request.BaseSpellHitPercent is < 0m or > 100m ||
            request.SpellCritPercent is < 0m or > 100m ||
            request.PetMeleeHitPercent is < 0m or > 100m ||
            request.TargetArmor < 0m ||
            request.TargetDodgePercent is < 0m or > 100m ||
            request.TargetParryPercent is < 0m or > 100m ||
            request.StartingManaPercent is < 0m or > 100m ||
            request.LifeTapBelowManaPercent is < 0m or > 100m ||
            request.LifeTapAboveHealthPercent is < 0m or > 100m ||
            request.CombatItems is null || request.DemonTuning is null ||
            request.Tuning.PetSwingSeconds <= 0m || request.Tuning.PetBaseDamage < 0m ||
            request.Tuning.LifeTapHealthCost < 0m || request.Tuning.LifeTapMana < 0m ||
            request.StartingSoulShards is < 0 or > 20 ||
            request.DrainSoulShardChancePercent is < 0m or > 100m ||
            request.TargetInitialHealthPercent is <= 0m or > 100m ||
            request.DemonIncomingDamageAtSeconds is < 0m ||
            request.DemonSecondIncomingDamageAtSeconds is < 0m ||
            request.DemonIncomingDamageAmount < 0m ||
            string.IsNullOrWhiteSpace(request.DemonIncomingDamageSchool) ||
            request.DemonSecondIncomingDamageAmount < 0m ||
            request.HealthFunnelBelowPetHealthPercent is <=0m or >100m ||
            request.DemonDeathAtSeconds is < 0m ||
            request.DemonSacrificeAtSeconds is < 0m ||
            request.DemonSacrificeAtSeconds.HasValue &&
                request.Talents.Rank("demonic-sacrifice")==0 ||
            request.DemonReplacementAtSeconds is < 0m ||
            request.SummonBaseMana<=0m ||
            request.ModelReplacementSummon &&
                (!request.EnablePet || !request.DemonReplacementAtSeconds.HasValue) ||
            request.UseFelDominationOnReplacement &&
                (!request.ModelReplacementSummon ||
                 request.Talents.Rank("fel-domination")==0) ||
            request.DemonReplacementAtSeconds.HasValue &&
                string.IsNullOrWhiteSpace(request.ReplacementDemonType))
            throw new ArgumentException("Invalid Warlock level-60 configuration.");
        var demonProfile=ForeverWarlockDemons.Get(request.PetType);
        request.DemonTuning.Validate();
        // Validate talent eligibility before constructing any encounter actor.
        request.Talents.Validate(60,request.Specialization);
        var prePullFamily=WarlockSacrificeRules.Normalize(request.PrePullSacrificedDemon);
        if(prePullFamily is not null && request.Talents.Rank("demonic-sacrifice")==0)
            throw new ArgumentException("Pre-pull Demonic Sacrifice requires the talent.");
        if(request.DemonReplacementAtSeconds.HasValue){
            var replacementProfile=ForeverWarlockDemons.Get(request.ReplacementDemonType!);
            if(request.ModelReplacementSummon && replacementProfile.IsTemporary)
                throw new ArgumentException("Modeled summon is available to permanent demons only.");
            if(replacementProfile.IsTemporary && (!request.AllowTemporaryDemon ||
                replacementProfile.Key=="doomguard" && !request.AllowDoomguardRitual))
                throw new ArgumentException("Replacement temporary demon requires summon/ritual permission.");
        }
        if(request.EnablePet && demonProfile.IsTemporary &&
           (!request.AllowTemporaryDemon ||
            demonProfile.Key=="doomguard" && !request.AllowDoomguardRitual))
            throw new ArgumentException("Temporary demon requires explicit summon/ritual permission.");
        ArgumentNullException.ThrowIfNull(request.ExternalModifiers);
        if(request.ExternalModifiers.TargetDebuffs?.Contains("curse-of-elements",
            StringComparer.OrdinalIgnoreCase)==true && request.UseCurseOfTheElements)
            throw new ArgumentException("Preconfigured external Curse of the Elements cannot stack with the Warlock’s own selected Curse.");
        request.CombatItems.Validate("warlock");
        request.Talents.Validate(60, request.Specialization);
        var stats = ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_ => request.WarlockMaximumHealth)
            .Calculate(ForeverRacialPassives.ApplyToProfile(
                ForeverEquipmentStatBridge.ResolveProfile(
                    ForeverExternalModifierCatalog.ApplyToProfile(
                        request.Character,request.ExternalModifiers,request.DurationSeconds)))).EffectiveStats;
        var spirit = Math.Max(0m, stats.Get("spirit"));
        var intellect = Math.Max(0m, stats.Get("intellect"));
        var ownerBaseSpellPower=Math.Max(0m,stats.Get("spell-power"));
        var spellPower = ownerBaseSpellPower +
            Warlock70425DamageModifiers.DemonicKnowledgeSpellPower(
                request.Talents.Rank("demonic-knowledge"),60,request.EnablePet);
        // Demonic Embrace increases total Stamina by 3% per rank without
        // Classic's Spirit penalty. Health gained per Stamina at level 60
        // remains provisional until Forever's client-to-server conversion
        // is independently verified. Existing request health is an input.
        var stamina=Math.Max(0m,stats.Get("stamina"));
        var embraceExtraHealth=stamina*.03m*request.Talents.Rank("demonic-embrace")*10m;
        stats.Set("stamina",stamina*(1m+.03m*request.Talents.Rank("demonic-embrace")));
        var maximumMana = (1376m + Math.Max(0m, intellect - 20m) * 15m) *
            (1m + .05m * request.Talents.Rank("fel-vitality"));
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            CaptureTimeline = request.CaptureTimeline, PrimaryActorKey = "warlock",
            MaximumProcessedEvents = 50000, CancellationToken = cancellationToken
        });
        var warlock = new SimulationActorState
        {
            Key = "warlock", Name = request.Character.Name, TeamKey = "raid",
            Level = 60, MaximumHealth = request.WarlockMaximumHealth + embraceExtraHealth, Stats = stats
        };
        warlock.InitializeHealth(warlock.MaximumHealth);
        warlock.AddResource(new ResourceState("mana", maximumMana,
            maximumMana * request.StartingManaPercent / 100m));
        warlock.AddResource(new ResourceState("soul-shard", 20m, request.StartingSoulShards));
        ForeverRacialPassives.ApplyResourcePassives(warlock,request.Character.Race);
        // Molten Skin reduces explicit incoming and Hellfire self-damage.
        warlock.Stats.Set("incoming-damage-reduction-percent",
            2m*request.Talents.Rank("molten-skin"));
        // Forever 70532 Suppression: each rank grants +1% hit and -4% threat.
        // Hit is resolved separately in WarlockRollResolver.
        warlock.Stats.Set("warlock-suppression-threat-reduction",
            .04m * request.Talents.Rank("suppression"));
        warlock.Stats.Set("demonic-brand-threat-reduction",request.Talents.Rank("demonic-brand") switch {
            1 => .17m, 2 => .33m, 3 => .50m, _ => 0m });
        var boss = new SimulationActorState
        {
            Key = "target", Name = "Level 63 Warlock training boss", TeamKey = "enemy",
            Level = 63, MaximumHealth = request.TargetMaximumHealth
        };
        boss.InitializeHealth(boss.MaximumHealth,
            boss.MaximumHealth * request.TargetInitialHealthPercent / 100m);
        boss.Stats.Set(ForeverCombatStatKeys.Armor,request.TargetArmor);
        boss.Stats.Set(ForeverCombatStatKeys.DodgeChancePercent,request.TargetDodgePercent);
        boss.Stats.Set(ForeverCombatStatKeys.ParryChancePercent,request.TargetParryPercent);
        context.AddActor(warlock);
        context.AddActor(boss);
        for (var i = 1; i <= request.AdditionalTargets; i++) {
            var add = new SimulationActorState {Key=$"target-{i}",
                Name=$"Additional Warlock enemy {i}",TeamKey="enemy",Level=63,
                MaximumHealth=request.TargetMaximumHealth};
            add.InitializeHealth(add.MaximumHealth);
            context.AddActor(add);
        }
        var spells=ForeverWarlockDpsSpellbook.Create(
            request.Specialization,request.Talents,request.Tuning,ownerBaseSpellPower,spirit).ToList();
        if(ForeverEquipmentEffectsCatalog.Select(request.Character.Equipment)
            .Any(x=>x.StatBonuses.ContainsKey("spell-power") && x.DurationSeconds>0m) ||
           request.UseRacialActive && request.Character.Race=="orc")
            ForeverEquipmentDynamicSpellScaling.Enable(spells,
                ForeverWarlockDpsSpellbook.Create(request.Specialization,request.Talents,request.Tuning,0m,spirit),
                ForeverWarlockDpsSpellbook.Create(request.Specialization,request.Talents,request.Tuning,1m,spirit),
                ownerBaseSpellPower,warlock.Stats);
        foreach (var ability in spells)
        {
            if (request.AssumeExternalHealingForLifeTap && ability.Key == "life-tap")
            {
                // Solo-DPS abstraction: an unseen raid healer immediately
                // replaces Life Tap HP. Keep the GCD, spell use, and Mana gained
                // without inventing healer mana expenditure or self-healing HPS.
                var healthCost = ability.Effects.First(effect => effect.Key == "life-tap-health");
                healthCost.MinimumValue = 0m;
                healthCost.MaximumValue = 0m;
            }
            warlock.AddAbility(ability);
        }
        if(request.ModelReplacementSummon)
        {
            warlock.AddAbility(ForeverWarlockSummonSpellbook.Replacement(
                request.ReplacementDemonType!,request.Talents,request.SummonBaseMana,
                request.UseFelDominationOnReplacement));
            if(request.UseFelDominationOnReplacement)
                warlock.AddAbility(ForeverWarlockSummonSpellbook.FelDomination());
        }
        var auras = new AuraManager();
        var externalDebuffs=new ForeverExternalTargetDebuffProcessor(
            request.ExternalModifiers,request.Character.Level,auras);
        var prePull = new WarlockPrePullSacrificeProcessor(request,warlock,auras);
        WarlockDemonCompanion? demon=null;
        WarlockDemonCompanion? replacement=null;
        var physicalRules=ForeverPhysicalCombatRulesetFactory.Create();
        var demonAutoRule=physicalRules.RollRules[ForeverCombatResolutionTypes.CreatureMeleeAuto];
        // The 95% equal-level creature table applies BEFORE inheriting the
        // owner's bonus. The +level skill adjustment remains a distinct part
        // of the physical table. Legacy final-hit overrides are converted into
        // stat deltas in WarlockRollResolver, not treated as owner hit bonuses.
        demonAutoRule.BaseHitChancePercent=
            ForeverPhysicalCombatRulesetFactory.EqualLevelSingleWeaponBaseHitChancePercent;
        demonAutoRule.HitChanceStatKey=ForeverCombatStatKeys.HitChancePercent;
        demonAutoRule.HitPenaltyPerHigherTargetLevelPercent=0m;
        // The owner crit chance is inherited live by each demon swing.
        demonAutoRule.BaseCriticalChancePercent=0m;
        demonAutoRule.CriticalChanceStatKey=ForeverCombatStatKeys.AttackCriticalChancePercent;
        physicalRules.MitigationRules[DamageMitigationTypes.Armor]=new DamageMitigationRuleDefinition{
            MitigationType=DamageMitigationTypes.Armor,
            FormulaType=DamageMitigationFormulaTypes.RationalLevelScaled,
            DefenseStatKey=ForeverCombatStatKeys.Armor,
            // Existing Hunter/BM formula; exact Forever values are still
            // scheduled for the formula audit.
            BaseConstant=400m,PerAttackerLevelConstant=85m,
            MaximumReductionPercent=75m
        };
        var decimation = new WarlockDecimationProcessor(warlock, auras,
            request.Talents.Rank("decimation"));
        var nightfall = new WarlockNightfallProcessor(warlock,auras,decimation,
            request.Talents.Rank("nightfall"));
        var rolls = new WarlockRollResolver(request,
            ()=>(demon?.IsActive==true && !demon.Family.IsTemporary) ||
                (replacement?.IsActive==true && !replacement.Family.IsTemporary),
            ()=>WarlockSacrificeRules.CurrentFamily(warlock,context.CurrentTimeSeconds),
            ()=>replacement?.IsActive==true ? replacement.Family.Key :
                demon?.IsActive==true ? demon.Family.Key : request.PetType,
            new RulesetCombatRollResolver(physicalRules,
                new ForeverCreatureLevelCombatRollAdjustmentProvider()));
        var absorbs=new AbsorbManager();
        var executor = new AbilityExecutor(auras, rolls,
            new RulesetDamageMitigationResolver(physicalRules),
            absorbManager:absorbs,abilityTimingProvider: new ForeverEquipmentCastHasteTimingProvider(nightfall));
        var incoming=new ScriptedCasterIncomingDamageProcessor(warlock,boss,absorbs,
            request.IncomingDamageAtSeconds,request.IncomingDamageAmount,
            request.IncomingDamageSchool,redirectDamage:(ctx,rawIncoming) => {
                if(request.Talents.Rank("soul-link")==0)return 0m;
                // Only the five permanent demons are eligible for Soul Link;
                // Infernal and Doomguard cannot redirect or grant its 3% DPS.
                var linked=replacement?.IsActive==true && !replacement.Family.IsTemporary ?
                    replacement : demon?.IsActive==true && !demon.Family.IsTemporary ?
                    demon : null;
                if(linked is null)return 0m;
                var moved=rawIncoming*.30m;
                var pet=linked.Actor;
                // Recipient mitigation is distinct from the owner's initial
                // mitigation. Felhunter/ Voidwalker Master Demonologist applies
                // to the redirected pet portion of an eligible school too.
                var recipientReduction=request.Talents.Rank("master-demonologist")*.02m;
                var eligibleRecipient=(linked.Family.Key=="voidwalker" &&
                    request.IncomingDamageSchool=="physical") ||
                    (linked.Family.Key=="felhunter" &&
                    request.IncomingDamageSchool!="physical");
                var toPet=moved*(eligibleRecipient?1m-recipientReduction:1m);
                var actual=pet.TakeDamage(toPet);
                ctx.EmitEvent(new CombatEvent {
                    TimeSeconds=ctx.CurrentTimeSeconds,Type=CombatEventType.Damage,
                    SourceActorKey=boss.Key,TargetActorKey=pet.Key,
                    AbilityKey="soul-link-transfer",EffectKey="soul-link-transfer",
                    SchoolKey=request.IncomingDamageSchool,ResultKey="hit",
                    RawAmount=moved,MitigatedAmount=moved-toPet,Amount=actual,
                    Description="Soul Link redirected 30% of incoming damage to active demon."
                });
                if(!pet.IsAlive)
                    ctx.EmitEvent(new CombatEvent {
                        TimeSeconds=ctx.CurrentTimeSeconds,Type=CombatEventType.ActorDied,
                        SourceActorKey=boss.Key,TargetActorKey=pet.Key,
                        AbilityKey="soul-link-transfer"
                    });
                return moved;
            },additionalReductionPercent:ctx => {
                var active=replacement?.IsActive==true ? replacement :
                    demon?.IsActive==true ? demon : null;
                var rank=request.Talents.Rank("master-demonologist");
                if(active is null || active.Family.IsTemporary)return 0m;
                return active.Family.Key switch {
                    "voidwalker" when request.IncomingDamageSchool=="physical" => 2m*rank,
                    "felhunter" when request.IncomingDamageSchool!="physical" => 2m*rank,
                    _ => 0m
                };
            });
        var casterInterruption=new WarlockIncomingCastInterruptionProcessor(
            executor,request.Talents.Rank("fel-concentration"),
            request.Talents.Rank("intensity"));
        var items = new CombatUseItemProcessor(request.CombatItems, executor, warlock, "target", "warlock");
        var rotation = WarlockAdaptiveRotation.Build(request);
        var adaptive = new WarlockAdaptivePriorityProcessor(request,rotation,warlock);
        var priority = new PriorityRotationExecutor(rotation, "warlock", "target", executor,
            reactToTargetStateChanges: true);
        decimal HarvestMultiplier()=>warlock.ActiveAuras.Any(a=>
            a.Definition.Key=="warlock-soul-harvest" &&
            a.IsActiveAt(context.CurrentTimeSeconds)) ?
            1m+.5m*request.Talents.Rank("soul-harvest") : 1m;
        decimal HarvestCastingFraction()=>HarvestMultiplier()>1m ?
            .5m*request.Talents.Rank("soul-harvest") : 0m;
        var regen = new FiveSecondManaRegenerationProcessor("warlock",
            request.Tuning.ManaRegenerationPerSecond, Math.Max(0m, stats.Get("mp5")), 0m,
            HarvestCastingFraction,HarvestMultiplier);
        var soulHarvest=new WarlockSoulHarvestProcessor(
            auras,request.Talents.Rank("soul-harvest"),request.TargetYieldsSoulShard);
        var auto = new AutoAttackProcessor(executor);
        demon = request.EnablePet ?
            new WarlockDemonCompanion(request,context,warlock,boss,auras,auto,executor,
                spellPower,spirit) : null;
        replacement = request.EnablePet && request.DemonReplacementAtSeconds.HasValue ?
            new WarlockDemonCompanion(request,context,warlock,boss,auras,auto,executor,
                spellPower,spirit,"warlock-replacement",request.ReplacementDemonType,
                request.DemonReplacementAtSeconds.Value,true) : null;
        var summon = request.ModelReplacementSummon && replacement is not null ?
            new WarlockReplacementSummonProcessor(warlock,replacement,executor,auras,
                request.DemonReplacementAtSeconds!.Value,
                request.UseFelDominationOnReplacement) : null;
        var demonicEnergies=new WarlockDemonicEnergiesProcessor(
            request.Talents.Rank("demonic-energies"),
            ()=>replacement?.IsActive==true?replacement:
                demon?.IsActive==true?demon:null);
        var healthFunnel=new WarlockHealthFunnelProcessor(request,warlock,
            ()=>replacement?.IsActive==true?replacement:
                demon?.IsActive==true?demon:null,
            rotation,executor);
        // A landed Conflagrate normally consumes our own Immolate. Shadow and Flame
        // grants a rank-dependent chance to retain it. Handle this on the landed
        // damage event, not on cast start (missed Conflagrates cannot consume it).
        var shadowAndFlame=new WarlockShadowAndFlameProcessor(
            auras,request.Talents.Rank("shadow-and-flame"));
        var conflagrateConsumption = new WarlockConflagrateConsumptionProcessor(
            auras, request.Talents.Rank("shadow-and-flame"));
        var improvedShadowBolt = new WarlockImprovedShadowBoltProcessor(
            request.Talents.Rank("improved-shadow-bolt"), auras);
        var baneOfHavoc = new WarlockBaneOfHavocProcessor(auras, executor);
        var curses=new WarlockCurseOwnershipProcessor(auras);
        var healthDrains = new DamageToSelfHealingProcessor("warlock",
            new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) {
                ["drain-life"] = 1m, ["siphon-life"] = 1m,
                ["death-coil"] = 1m });
        var amplifyCurse=new WarlockAmplifyCurseProcessor(auras);
        var shadowburnShards = new WarlockShadowburnShardProcessor(request.TargetYieldsSoulShard);
        var drainSoulShards = new WarlockDrainSoulShardProcessor(
            request.TargetYieldsSoulShard,request.DrainSoulShardChancePercent);
        var demonicBrand = new WarlockDemonicBrandProcessor(request.Talents.Rank("demonic-brand"),
            auras,executor,warlock,spellPower,
            new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
                ["warlock-pet"] = demon?.Family.Key ?? "",
                ["warlock-replacement"] = replacement?.Family.Key ?? "" },
            actorKey=>actorKey switch {
                "warlock-pet" => demon?.IsActive==true,
                "warlock-replacement" => replacement?.IsActive==true,
                _ => false
            });
        var racialActive=new ForeverRacialActiveProcessor(
            "warlock",request.Character.Race,request.UseRacialActive,auras,
            onStatDelta:(ctx,actor,delta)=>{
                demon?.ApplyOwnerDynamicDelta(delta);
                replacement?.ApplyOwnerDynamicDelta(delta);
            });
        var equipmentEffects=new ForeverEquipmentEffectProcessor(
            request.Character.Equipment,"warlock","target",auras,executor,request.Seed,
            classKey:"warlock",
            onStatDelta:(ctx,actor,delta)=>{
                ForeverEquipmentDynamicResourceCaps.Apply(ctx,actor,delta,
                    intellectManaMultiplier:1m+.05m*request.Talents.Rank("fel-vitality"),
                    staminaHealthMultiplier:1m+.03m*request.Talents.Rank("demonic-embrace"));
                demon?.ApplyOwnerDynamicDelta(delta);
                replacement?.ApplyOwnerDynamicDelta(delta);
            });
        var processors=new List<ICombatEventProcessor>{
            executor,auras,equipmentEffects,racialActive,externalDebuffs,decimation,nightfall,conflagrateConsumption,shadowAndFlame,improvedShadowBolt,baneOfHavoc,curses,
            healthDrains,shadowburnShards,drainSoulShards,soulHarvest,amplifyCurse,demonicBrand,demonicEnergies,
            incoming,casterInterruption,auto,regen,items,adaptive,healthFunnel,priority,
            new ThreatManager(new WarlockDemonThreat(request.Talents.Rank("improved-health-funnel"))),prePull
        };
        if(demon is not null){
            processors.Add(demon);
            processors.Add(demon.Rotation());
        }
        if(replacement is not null){
            processors.Add(replacement);
            processors.Add(replacement.Rotation());
        }
        if(summon is not null)processors.Insert(1,summon);
        var engine = new SimulationEngine(processors);
        regen.Initialize(context);
        var run = engine.Run(context, started =>
        {
            prePull.Initialize(started);
            incoming.Start(started);
            demon?.Start(started);
            replacement?.Start(started);
            summon?.Start(started);
            if(demon is not null && request.DemonTargetHasMagicAura)
                auras.ApplyAura(started,boss,new WarcraftSim.Core.Auras.AuraDefinition {
                    Key="demon-test-magic-buff",Name="Configured target magic aura",
                    DurationSeconds=request.DurationSeconds
                },boss.Key,"encounter-magic-buff","magic-buff");
        });
        equipmentEffects.WriteMetrics(run);
        racialActive.WriteMetrics(run);
        run.CustomMetrics["warlock-cast-interrupt-eligible-hits"]=casterInterruption.EligibleHits;
        run.CustomMetrics["warlock-cast-interrupt-resisted"]=casterInterruption.ResistedHits;
        run.CustomMetrics["warlock-channel-cancellations"]=casterInterruption.CancelledChannels;
        run.CustomMetrics["warlock-hardcast-pushback-seconds"]=casterInterruption.HardcastPushbackSeconds;
        run.CustomMetrics["warlock-replacement-summons-completed"]=summon?.CompletedSummons??0;
        run.CustomMetrics["warlock-replacement-summon-retries"]=summon?.RetryAttempts??0;
        run.CustomMetrics["warlock-shadow-and-flame-shadow-procs"]=shadowAndFlame.ShadowBuffs;
        run.CustomMetrics["warlock-shadow-and-flame-fire-procs"]=shadowAndFlame.FireBuffs;
        run.CustomMetrics["warlock-shadow-and-flame-instant-shards"]=shadowAndFlame.ImmediateShardsRestored;
        run.CustomMetrics["warlock-curse-replacements"]=curses.Replacements;
        run.CustomMetrics["warlock-target-armor-at-end"]=boss.Stats.Get(ForeverCombatStatKeys.Armor);
        run.CustomMetrics["warlock-demonic-energies-pet-healing"]=demonicEnergies.PetHealing;
        run.CustomMetrics["warlock-demonic-energies-pet-mana"]=demonicEnergies.PetManaGained;
        run.CustomMetrics["warlock-health-funnel-ticks"]=healthFunnel.SuccessfulTicks;
        run.CustomMetrics["warlock-health-funnel-health-spent"]=healthFunnel.HealthSpent;
        run.CustomMetrics["warlock-health-funnel-pet-healing"]=healthFunnel.PetHealingDone;
        run.CustomMetrics["warlock-soul-harvest-procs"] = soulHarvest.Triggers;
        run.CustomMetrics["warlock-amplified-banes"] = amplifyCurse.AmplifiedBanes;
        run.CustomMetrics["warlock-drain-soul-shards"] = drainSoulShards.Generated;
        run.CustomMetrics["warlock-nightfall-procs"] = nightfall.Procs;
        run.CustomMetrics["warlock-nightfall-consumed"] = nightfall.Consumed;
        run.CustomMetrics["warlock-decimation-triggers"] = decimation.Triggers;
        run.CustomMetrics["warlock-decimation-shard-free-casts"] = decimation.ShardFreeCasts;
        var owner = run.Summary.ActorSummaries["warlock"];
        var petDamage = run.Summary.ActorSummaries.TryGetValue("warlock-pet", out var petReport) ?
            petReport.DamageDone : 0m;
        var replacementDamage=run.Summary.ActorSummaries.TryGetValue("warlock-replacement",
            out var replacementReport) ? replacementReport.DamageDone : 0m;
        petDamage+=replacementDamage;
        var breakdown = new Dictionary<string, decimal>(owner.DamageDoneByAbility,
            StringComparer.OrdinalIgnoreCase);
        if (petReport is not null)
            foreach (var pair in petReport.DamageDoneByAbility)
                breakdown[pair.Key=="pet-melee" ? "pet-melee" : "demon:"+pair.Key]=pair.Value;
        if (replacementReport is not null)
            foreach (var pair in replacementReport.DamageDoneByAbility)
                breakdown["replacement:"+pair.Key]=pair.Value;
        return new WarlockSimResponse
        {
            Specialization = request.Specialization,
            ResolvedRotation = string.Join(" > ",rotation.Entries.Select(x=>x.AbilityKey)),
            DamageDone = owner.DamageDone + petDamage,
            Dps = (owner.DamageDone + petDamage) / request.DurationSeconds,
            PetDamage = petDamage,
            DemonType = demon?.Family.Key ?? "none",
            ActiveDemonAtPull = request.EnablePet ? demon?.Family.Key : null,
            PetSelectedAtPull = request.EnablePet,
            PrePullSacrificedDemon = prePull.SacrificedFamily,
            SacrificeBuffAtPull = prePull.InitialBuffFamily,
            SacrificeBuffAtEnd = WarlockSacrificeRules.CurrentFamily(warlock,request.DurationSeconds),
            SacrificeCanceledBeforePull = prePull.CanceledBeforePull,
            DemonicPactTalented = prePull.PactTalented,
            ReplacementDemonType = replacement?.Family.Key,
            ReplacementDemonDamage = replacementDamage,
            WarlockDamage = owner.DamageDone,
            DemonMeleeDamage = (demon?.AutoDamage ?? 0m)+(replacement?.AutoDamage ?? 0m),
            DemonSpellDamage = (demon?.SpellDamage ?? 0m)+(replacement?.SpellDamage ?? 0m),
            DemonThreat = boss.ThreatTable.GetThreat("warlock-pet")+
                boss.ThreatTable.GetThreat("warlock-replacement"),
            DemonMana = (replacement ?? demon)?.Actor.Resources["mana"].Current ?? 0m,
            DemonUptimeSeconds = (demon?.Uptime ?? 0m)+(replacement?.Uptime ?? 0m),
            DemonDied = demon?.Died==true || replacement?.Died==true,
            DemonSacrificed = demon?.Sacrificed ?? false,
            DemonSpecialCasts = (demon?.SpecialCasts ?? 0)+(replacement?.SpecialCasts ?? 0),
            DemonCriticalHits = (demon?.CriticalHits ?? 0)+(replacement?.CriticalHits ?? 0),
            DemonMisses = (demon?.Misses ?? 0)+(replacement?.Misses ?? 0),
            EndingMana = warlock.Resources["mana"].Current,
            MaximumMana = maximumMana, EndingHealth = warlock.CurrentHealth,
            SpellPower = spellPower,
            CombatItemUses = items.Uses.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            LifeTapCasts = owner.Abilities.TryGetValue("life-tap", out var tap) ?
                tap.CastCompletedCount : 0,
            DamageByAbility = breakdown, Run = run,
            ModelingNotes =
            [
                "CP131 broad Warlock DPS foundations: Corruption, Bane of Agony, Immolate, Shadow Bolt, Wrack, Soul Fire, Incinerate, Conflagrate and Spirit-scaling Life Tap.",
                "CP167: Drain Life/Siphon Life heal from effective enemy damage. Searing Pain is high threat; Shadowburn uses finite Soul Shards and grants a shard only on an eligible target death within eight seconds.",
                "Decimation: ranks 1/2 reduce Soul Fire cooldown by 45/90% and grant a ten-second faster, shard-free Soul Fire window from landed Shadow Bolt/Searing Pain against sub-35% enemies. Regular Soul Fire now consumes one real shard.",
                "Affliction, Demonology and Destruction use tested level-60 priority profiles; baseline Soul Fire is available to all three specializations.",
                "CP164: independent demon companion uses shared actor factory and engine spell/auto-attack execution. Numeric demon coefficients remain provisional.",
                "Demonic Knowledge and Soul Link now provide their level-based Spell Power / 3% pet-and-owner damage with an active pet. Unholy Power grants 2% pet damage per rank.",
                "Forever Demonic Sacrifice grants Imp Shadow damage, Sayaad Fire damage, Voidwalker Mana or Felhunter Health; Demonic Pact retains the buff across different-family replacement.",
                "CP168: optional independent Curse/Bane and Immolate/Conflagrate targeting across multiple configured enemies uses the shared rotation selector.",
                "Incinerate gains 25% damage against a currently Immolated target; Wrack grants a six-second Shadow DoT vulnerability.",
                "Landed Conflagrate requires own Immolate; Shadow and Flame grants rank-based retention and Shadow damage, while landed Shadowburn grants Fire damage and an instant shard refund chance.",
                "Warlock beta spell ranks, hit, spell coefficients, pet scaling and Mana regeneration are configurable illustrative assumptions, not calibrated DPS predictions.",
                request.AssumeExternalHealingForLifeTap
                    ? "Solo external healing assumption: Life Tap uses its GCD and yields Mana without HP limiting the rotation; external healer output and mana are not simulated."
                    : "Actual Life Tap health expenditure is enabled. Without healer support, the Warlock may eventually become HP-limited.",
                "CP140: default priority gates late DoT refreshes, channel duration, Soul Fire Mana spending, and Life Tap near fight end. User customPriority overrides spell gates.",
                "CP168: solo rotations are mechanically validated but not optimized against the future equipment, consumables, raid-buff or Warcraft Logs datasets.",
                "This is a solo training target, not a survival or raid utility encounter."
            ]
        };
    }

    private static bool HasCurseOfElements(SimulationActorState target, decimal now, string? school) =>
        school is "arcane" or "fire" or "frost" or "holy" or "nature" or "shadow" &&
        target.ActiveAuras.Any(a=>a.Definition.Key=="curse-of-the-elements" &&
            a.SourceActorKey=="warlock" && a.IsActiveAt(now));

    private sealed class WarlockRollResolver(WarlockSimRequest request,Func<bool> petActive,
        Func<string?> sacrificedFamily,Func<string> activeDemonType,
        RulesetCombatRollResolver physicalRolls) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect) =>
            Resolve(context, source, target, ability, effect, CombatEffectDeliveryType.Direct);

        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            CombatEffectDeliveryType deliveryType)
        {
            // Bane of Havoc copies already-resolved effective damage. A fresh
            // Warlock damage-multiplier roll here would double-count Shadow
            // Vulnerability, Demon Sacrifice and school-specific talents.
            // Hellfire caster self-damage is fixed per tick: never apply
            // offensive spell crit/talents or a hostile debuff to the caster.
            if(ability.Key=="hellfire" && source.Key==target.Key)
                return new CombatRollResult {
                    Landed=true,ResultKey="hit",
                    AmountMultiplier=1m-.02m*request.Talents.Rank("molten-skin") };
            if (ability.Key == WarlockBaneOfHavocProcessor.EchoKey)
                return CombatRollResult.Hit();
            var pet = source.Key is "warlock-pet" or "warlock-replacement";
            if(pet && effect.ResolutionType==ForeverCombatResolutionTypes.CreatureMeleeAuto)
            {
                // Refresh from the CURRENT owner's stats at attack resolution
                // so buffs/gear reflected in effective stats also reach demons.
                // A legacy explicit final override can still force 0/100 hit.
                var owner=context.GetActor("warlock");
                var inheritedMeleeBonus=owner is null ? 0m :
                    ForeverCompanionHitInheritance.WarlockMeleeBonus(
                        ForeverCompanionHitInheritance.OwnerSpellHitBonus(owner));
                var inherited=request.PetMeleeHitPercent is { } forced
                    ? forced-ForeverCompanionHitInheritance.CreatureMeleeBaseHit(
                        source.Level,target.Level)
                    : inheritedMeleeBonus;
                source.Stats.Set(ForeverCombatStatKeys.HitChancePercent,inherited);
                var ownerCrit=owner is null ? 0m : request.SpellCritPercent+
                    owner.Stats.Get(ForeverCombatStatKeys.SpellCriticalChancePercent);
                source.Stats.Set(ForeverCombatStatKeys.AttackCriticalChancePercent,
                    Math.Clamp(ownerCrit,0m,100m));
                // Single shared hit table: miss, dodge, parry, block and crit.
                // The class-specific talent multiplier is applied *after*
                // resolution and only on landed pet melee events.
                var melee=physicalRolls.Resolve(context,source,target,ability,effect,deliveryType);
                if(!melee.Landed)return melee;
                var meleeBonus=Warlock70425DamageModifiers.Multiplier(
                    source.Key,ability.Key,effect.SchoolKey,deliveryType,
                    false,false,activeDemonType(),
                    petActive()?request.Talents.Rank("master-demonologist"):0,
                    petActive()&&request.Talents.Rank("soul-link")>0,
                    request.Talents.Rank("agonizing-flames"));
                meleeBonus*=1m+.02m*request.Talents.Rank("unholy-power");
                if(HasCurseOfElements(target,context.CurrentTimeSeconds,effect.SchoolKey)) meleeBonus*=1.10m;
                return new CombatRollResult{
                    Landed=true,IsCritical=melee.IsCritical,ResultKey=melee.ResultKey,
                    AmountMultiplier=melee.AmountMultiplier*meleeBonus,
                    BlockValue=melee.BlockValue
                };
            }
            var demonSpell=pet && effect.ResolutionType==CombatResolutionTypes.Spell;
            var ownerActor=pet?context.GetActor("warlock"):null;
            var spellBonus=ownerActor is null?0m:
                ForeverCompanionHitInheritance.OwnerSpellHitBonus(ownerActor);
            var petHit=pet
                ? demonSpell
                    ? request.DemonTuning.DemonSpellHitPercent ??
                        ForeverCompanionHitInheritance.FinalHit(
                            ForeverCompanionHitInheritance.SpellBaseHit(source.Level,target.Level),
                            ForeverCompanionHitInheritance.WarlockSpellBonus(spellBonus))
                    : request.PetMeleeHitPercent ??
                        ForeverCompanionHitInheritance.FinalHit(
                            ForeverCompanionHitInheritance.CreatureMeleeBaseHit(source.Level,target.Level),
                            ForeverCompanionHitInheritance.WarlockMeleeBonus(spellBonus))
                : 0m;
            var hit = pet?petHit:
                Math.Min(100m, request.BaseSpellHitPercent +
                    request.Talents.Rank("suppression") + source.Stats.Get("spell-hit-percent") +
                    source.Stats.Get("spell-hit-chance-percent"));
            if (effect.CanMiss && (decimal)context.Random.NextDouble() * 100m >= hit)
                return CombatRollResult.Miss();
            var inheritedPetCrit = request.SpellCritPercent+
                (ownerActor?.Stats.Get(ForeverCombatStatKeys.SpellCriticalChancePercent) ?? 0m);
            var critChance = pet ? (request.DemonTuning.DemonSpellCritPercent ??
                inheritedPetCrit) : request.SpellCritPercent +
                source.Stats.Get("spell-critical-chance-percent") + effect.BonusCriticalChancePercent +
                (effect.SchoolKey == "shadow" ? request.Talents.Rank("malevolence") : 0) +
                (!pet && ability.Key=="searing-pain" ? request.Talents.Rank("agonizing-flames") switch {
                    1=>3m, 2=>7m, 3=>10m, _=>0m } : 0m) +
                (!pet && ability.Key=="conflagrate" ? request.Talents.Rank("fire-and-brimstone") switch {
                    1=>8m, 2=>17m, 3=>25m, _=>0m } : 0m);
            var crit = effect.CanCrit && (decimal)context.Random.NextDouble() * 100m <
                Math.Clamp(critChance, 0m, 100m);
            bool isDestructionSpell = ability.Key is "shadow-bolt" or "immolate" or
                "incinerate" or "conflagrate" or "soul-fire" or "searing-pain" or
                "shadowburn" or "rain-of-fire" or "hellfire";
            // Pandemic increases the BONUS part of periodic/channel critical damage,
            // not the entire crit multiplier. A full 3 ranks doubles the usual
            // 0.5x spell critical bonus from 0.5x to 1.0x.
            var pandemicEligible = ability.Key is "corruption" or "bane-of-agony" or
                "bane-of-doom" or "siphon-life" or "drain-life" or
                "drain-soul" or "wrack";
            var pandemicRankBonus = request.Talents.Rank("pandemic") switch {
                1 => .33m, 2 => .67m, 3 => 1m, _ => 0m
            };
            var pandemicBonus = !pet && pandemicEligible &&
                (deliveryType is CombatEffectDeliveryType.Periodic or CombatEffectDeliveryType.ChannelTick)
                ? .5m * pandemicRankBonus : 0m;
            var baseRoll = crit ? CombatRollResult.Critical(pet ? 2m :
                1.5m + pandemicBonus +
                (isDestructionSpell ? request.Talents.Rank("ruin") * .1m : 0m)) :
                CombatRollResult.Hit();
            bool immolate = target.ActiveAuras.Any(a => a.SourceActorKey == "warlock" &&
                a.Definition.Key == "immolate" && a.IsActiveAt(context.CurrentTimeSeconds));
            bool wrack = target.ActiveAuras.Any(a => a.SourceActorKey == "warlock" &&
                a.Definition.Key == "wrack-vulnerability" && a.IsActiveAt(context.CurrentTimeSeconds));
            var bonus = Warlock70425DamageModifiers.Multiplier(source.Key, ability.Key,
                effect.SchoolKey, deliveryType, immolate, wrack, activeDemonType(),
                petActive() ? request.Talents.Rank("master-demonologist") : 0,
                petActive() && request.Talents.Rank("soul-link") > 0,
                request.Talents.Rank("agonizing-flames"));
            // Forever reverses the Classic sacrifice schools: Imp grants
            // Shadow damage and Succubus/Incubus grants Fire damage.
            if(pet)bonus*=1m+.02m*request.Talents.Rank("unholy-power");
            var sacrificed=sacrificedFamily();
            if(!pet && (sacrificed=="imp" && effect.SchoolKey=="shadow" ||
                sacrificed is "succubus" or "incubus" && effect.SchoolKey=="fire"))
                bonus*=1.15m;
            // Decimation augments only its two eligible damaging casts against
            // targets already below 35% health (not Soul Fire itself).
            if (!pet && deliveryType == CombatEffectDeliveryType.Direct &&
                ability.Key is ("shadow-bolt" or "searing-pain") &&
                target.MaximumHealth > 0m &&
                target.CurrentHealth * 100m < target.MaximumHealth * 35m)
                bonus *= 1m + .03m * request.Talents.Rank("decimation");
            bonus *= WarlockImprovedShadowBoltProcessor.DamageMultiplier(
                target, context.CurrentTimeSeconds, source.Key, effect.SchoolKey,
                request.Talents.Rank("improved-shadow-bolt"));
            if(HasCurseOfElements(target,context.CurrentTimeSeconds,effect.SchoolKey)) bonus*=1.10m;
            // Applied to later hits/ticks only: landed Conflagrate or
            // Shadowburn creates an owner aura AFTER its own damage resolves.
            if(!pet && effect.SchoolKey is ("shadow" or "fire") &&
                source.ActiveAuras.Any(a=>a.Definition.Key==
                    (effect.SchoolKey=="shadow"?
                     WarlockShadowAndFlameProcessor.ShadowAura:
                     WarlockShadowAndFlameProcessor.FireAura) &&
                    a.SourceActorKey==source.Key &&
                    a.IsActiveAt(context.CurrentTimeSeconds)))
                bonus*=1m+.02m*request.Talents.Rank("shadow-and-flame");
            if(!pet && ability.Key=="bane-of-agony" &&
                deliveryType==CombatEffectDeliveryType.Periodic)
            {
                var ownerAgony=target.ActiveAuras.FirstOrDefault(a=>
                    a.Definition.Key=="bane-of-agony" &&
                    a.SourceActorKey==source.Key);
                if(ownerAgony is not null)
                {
                    var index=ForeverWarlockAgonyRamp.TickIndex(
                        context.CurrentTimeSeconds-ownerAgony.AppliedAtSeconds);
                    bonus*=ForeverWarlockAgonyRamp.FactorForTick(index);
                }
            }
            if(!pet && ability.Key=="bane-of-agony" &&
                target.ActiveAuras.Any(a=>a.Definition.Key=="warlock-amplified-agony" &&
                    a.SourceActorKey==source.Key && a.IsActiveAt(context.CurrentTimeSeconds)))
                bonus*=1.5m;
            if(!pet && ability.Key is "drain-life" or "drain-soul" or "wrack" &&
                request.Talents.Rank("soul-siphon") is >0)
            {
                // Up to three OTHER Warlock-owned active Affliction effects.
                var afflictionCount=target.ActiveAuras.Count(a=>
                    a.SourceActorKey=="warlock" && a.IsActiveAt(context.CurrentTimeSeconds) &&
                    a.Definition.Key is "corruption" or "bane-of-agony" or
                        "bane-of-doom" or "siphon-life" or "curse-of-the-elements" or
                         "curse-of-recklessness");
                bonus*=1m + .04m*request.Talents.Rank("soul-siphon")*Math.Min(3,afflictionCount);
            }
            return new CombatRollResult { Landed = true, IsCritical = baseRoll.IsCritical,
                ResultKey = baseRoll.ResultKey,
                AmountMultiplier = baseRoll.AmountMultiplier * bonus };
        }
    }
}
