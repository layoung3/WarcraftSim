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
            request.TargetInitialHealthPercent is <= 0m or > 100m ||
            request.DemonDeathAtSeconds is < 0m ||
            request.DemonSacrificeAtSeconds is < 0m ||
            request.DemonSacrificeAtSeconds.HasValue &&
                request.Talents.Rank("demonic-sacrifice")==0 ||
            request.DemonReplacementAtSeconds is < 0m ||
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
            if(replacementProfile.IsTemporary && (!request.AllowTemporaryDemon ||
                replacementProfile.Key=="doomguard" && !request.AllowDoomguardRitual))
                throw new ArgumentException("Replacement temporary demon requires summon/ritual permission.");
        }
        if(request.EnablePet && demonProfile.IsTemporary &&
           (!request.AllowTemporaryDemon ||
            demonProfile.Key=="doomguard" && !request.AllowDoomguardRitual))
            throw new ArgumentException("Temporary demon requires explicit summon/ritual permission.");
        request.CombatItems.Validate("warlock");
        request.Talents.Validate(60, request.Specialization);
        var stats = ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_ => request.WarlockMaximumHealth)
            .Calculate(request.Character).EffectiveStats;
        var spirit = Math.Max(0m, stats.Get("spirit"));
        var intellect = Math.Max(0m, stats.Get("intellect"));
        var spellPower = Math.Max(0m, stats.Get("spell-power")) +
            Warlock70425DamageModifiers.DemonicKnowledgeSpellPower(
                request.Talents.Rank("demonic-knowledge"),60,request.EnablePet);
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
            Level = 60, MaximumHealth = request.WarlockMaximumHealth, Stats = stats
        };
        warlock.InitializeHealth(warlock.MaximumHealth);
        warlock.AddResource(new ResourceState("mana", maximumMana,
            maximumMana * request.StartingManaPercent / 100m));
        warlock.AddResource(new ResourceState("soul-shard", 20m, request.StartingSoulShards));
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
        foreach (var ability in ForeverWarlockDpsSpellbook.Create(
            request.Specialization, request.Talents, request.Tuning, spellPower, spirit))
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
        var auras = new AuraManager();
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
        demonAutoRule.BaseCriticalChancePercent=10m;
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
            ()=>demon?.IsActive==true || replacement?.IsActive==true,
            ()=>WarlockSacrificeRules.CurrentFamily(warlock,context.CurrentTimeSeconds),
            ()=>replacement?.IsActive==true ? replacement.Family.Key :
                demon?.IsActive==true ? demon.Family.Key : request.PetType,
            new RulesetCombatRollResolver(physicalRules,
                new ForeverCreatureLevelCombatRollAdjustmentProvider()));
        var executor = new AbilityExecutor(auras, rolls,
            new RulesetDamageMitigationResolver(physicalRules),
            abilityTimingProvider: nightfall);
        var items = new CombatUseItemProcessor(request.CombatItems, executor, warlock, "target", "warlock");
        var rotation = WarlockAdaptiveRotation.Build(request);
        var adaptive = new WarlockAdaptivePriorityProcessor(request,rotation,warlock);
        var priority = new PriorityRotationExecutor(rotation, "warlock", "target", executor,
            reactToTargetStateChanges: true);
        var regen = new FiveSecondManaRegenerationProcessor("warlock",
            request.Tuning.ManaRegenerationPerSecond, Math.Max(0m, stats.Get("mp5")), 0m);
        var auto = new AutoAttackProcessor(executor);
        demon = request.EnablePet ?
            new WarlockDemonCompanion(request,context,warlock,boss,auras,auto,executor,
                spellPower,spirit) : null;
        replacement = request.EnablePet && request.DemonReplacementAtSeconds.HasValue ?
            new WarlockDemonCompanion(request,context,warlock,boss,auras,auto,executor,
                spellPower,spirit,"warlock-replacement",request.ReplacementDemonType,
                request.DemonReplacementAtSeconds.Value,true) : null;
        // A landed Conflagrate normally consumes our own Immolate. Shadow and Flame
        // grants a rank-dependent chance to retain it. Handle this on the landed
        // damage event, not on cast start (missed Conflagrates cannot consume it).
        var conflagrateConsumption = new WarlockConflagrateConsumptionProcessor(
            auras, request.Talents.Rank("shadow-and-flame"));
        var improvedShadowBolt = new WarlockImprovedShadowBoltProcessor(
            request.Talents.Rank("improved-shadow-bolt"), auras);
        var baneOfHavoc = new WarlockBaneOfHavocProcessor(auras, executor);
        var healthDrains = new DamageToSelfHealingProcessor("warlock",
            new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) {
                ["drain-life"] = 1m, ["siphon-life"] = 1m,
                ["death-coil"] = 1m });
        var shadowburnShards = new WarlockShadowburnShardProcessor(request.TargetYieldsSoulShard);
        var demonicBrand = new WarlockDemonicBrandProcessor(request.Talents.Rank("demonic-brand"),
            auras,executor,warlock,spellPower,
            new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
                ["warlock-pet"] = demon?.Family.Key ?? "",
                ["warlock-replacement"] = replacement?.Family.Key ?? "" });
        var processors=new List<ICombatEventProcessor>{
            executor,auras,decimation,nightfall,conflagrateConsumption,improvedShadowBolt,baneOfHavoc,
            healthDrains,shadowburnShards,demonicBrand,auto,regen,items,adaptive,priority,
            new ThreatManager(new WarlockDemonThreat()),prePull
        };
        if(demon is not null){
            processors.Add(demon);
            processors.Add(demon.Rotation());
        }
        if(replacement is not null){
            processors.Add(replacement);
            processors.Add(replacement.Rotation());
        }
        var engine = new SimulationEngine(processors);
        regen.Initialize(context);
        var run = engine.Run(context, started =>
        {
            prePull.Initialize(started);
            demon?.Start(started);
            replacement?.Start(started);
            if(demon is not null && request.DemonTargetHasMagicAura)
                auras.ApplyAura(started,boss,new WarcraftSim.Core.Auras.AuraDefinition {
                    Key="demon-test-magic-buff",Name="Configured target magic aura",
                    DurationSeconds=request.DurationSeconds
                },boss.Key,"encounter-magic-buff","magic-buff");
        });
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
                "Affliction, Demonology and Destruction use separate modeled talent subsets and rotation priorities.",
                "CP164: independent demon companion uses shared actor factory and engine spell/auto-attack execution. Numeric demon coefficients remain provisional.",
                "Demonic Knowledge and Soul Link now provide their level-based Spell Power / 3% pet-and-owner damage with an active pet. Unholy Power grants 2% pet damage per rank.",
                "Forever Demonic Sacrifice grants Imp Shadow damage, Sayaad Fire damage, Voidwalker Mana or Felhunter Health; Demonic Pact retains the buff across different-family replacement.",
                "Warlock Banes are selected as one active damage Bane for the solo rotation; utility Curses and multiple targets are deferred.",
                "Incinerate gains 25% damage against a currently Immolated target; Wrack grants a six-second Shadow DoT vulnerability.",
                "Landed Conflagrate consumes own Immolate unless Shadow and Flame prevents it (20% retention per talent rank). Other Shadow and Flame procs remain pending.",
                "Warlock beta spell ranks, hit, spell coefficients, pet scaling and Mana regeneration are configurable illustrative assumptions, not calibrated DPS predictions.",
                request.AssumeExternalHealingForLifeTap
                    ? "Solo external healing assumption: Life Tap uses its GCD and yields Mana without HP limiting the rotation; external healer output and mana are not simulated."
                    : "Actual Life Tap health expenditure is enabled. Without healer support, the Warlock may eventually become HP-limited.",
                "CP140: default priority gates late DoT refreshes, channel duration, Soul Fire Mana spending, and Life Tap near fight end. User customPriority overrides spell gates.",
                "CP140: These are talent-aware solo defaults, not yet automatically optimized with all real pre-raid item stats, raid debuffs, trinket activation, add timers or per-pet abilities.",
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
            var critChance = pet ? (demonSpell ? request.DemonTuning.DemonSpellCritPercent : 10m) : request.SpellCritPercent +
                source.Stats.Get("spell-critical-chance-percent") + effect.BonusCriticalChancePercent +
                (effect.SchoolKey == "shadow" ? request.Talents.Rank("malevolence") : 0);
            var crit = effect.CanCrit && (decimal)context.Random.NextDouble() * 100m <
                Math.Clamp(critChance, 0m, 100m);
            bool isDestructionSpell = ability.Key is "shadow-bolt" or "immolate" or
                "incinerate" or "conflagrate" or "soul-fire";
            // Pandemic increases the BONUS part of periodic/channel critical damage,
            // not the entire crit multiplier. A full 3 ranks doubles the usual
            // 0.5x spell critical bonus from 0.5x to 1.0x.
            var pandemicEligible = ability.Key is "corruption" or "bane-of-agony" or "wrack";
            var pandemicBonus = !pet && pandemicEligible &&
                (deliveryType is CombatEffectDeliveryType.Periodic or CombatEffectDeliveryType.ChannelTick)
                ? .5m * request.Talents.Rank("pandemic") / 3m : 0m;
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
            return new CombatRollResult { Landed = true, IsCritical = baseRoll.IsCritical,
                ResultKey = baseRoll.ResultKey,
                AmountMultiplier = baseRoll.AmountMultiplier * bonus };
        }
    }
}
