// Consolidated tests for Warrior Talents Stances.
// Original class names are retained for xUnit test filtering.

// ==================== ForeverWarriorArmsPreferenceAndActionLockTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverWarriorArmsPreferenceAndActionLockTests
{
    [Fact]
    public void OffGcdInputLock_HasItsOwnTimingIndependentOfAbilityGcd()
    {
        var actor = new SimulationActorState { Key = "warrior", Name = "Warrior", TeamKey = "raid" };
        actor.ConfigureActionTiming(0m, 0.25m);
        actor.StartGlobalCooldown(0m, 1.5m);
        actor.RegisterActionStarted(0m, 0m, 1.5m);
        Assert.Equal(1.75m, actor.InputReadyAtSeconds);
        Assert.Equal(0.25m, actor.OffGlobalCooldownInputReadyAtSeconds);
        Assert.False(actor.IsOffGlobalCooldownInputReady(0m));
        Assert.True(actor.IsOffGlobalCooldownInputReady(0.25m));
        Assert.False(actor.IsInputReady(0.25m));
    }

    [Fact]
    public void BerserkerStance_GrantsThreeCritWithoutAccumulation_WhenUpdatingStats()
    {
        var warrior = new SimulationActorState { Key = "warrior", Name = "Warrior", TeamKey = "raid" };
        warrior.InitializeHealth(1000m);
        warrior.Stats.Set(ForeverCombatStatKeys.AttackCriticalChancePercent, 17m);
        var target = new SimulationActorState { Key = "target", Name = "Target", TeamKey = "enemy" };
        target.InitializeHealth(1000m);
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = 30m, PrimaryActorKey = warrior.Key
        });
        context.AddActor(warrior);
        context.AddActor(target);
        var auras = new AuraManager();
        var executor = new AbilityExecutor(auras, new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition
            { RulesetKey = "stance-dynamic-crit", Version = "1" }));
        var processor = new WarriorRuntimeActionProcessor(
            executor, auras, new WarriorSimRequest(), warrior, target);
        auras.ApplyAura(context, warrior, new AuraDefinition
        { Key = ForeverWarriorAuraKeys.BerserkerStance, DurationSeconds = 30m }, warrior.Key, null, null);
        processor.RefreshDynamicStats(context);
        Assert.Equal(20m, warrior.Stats.Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
        processor.RefreshDynamicStats(context);
        Assert.Equal(20m, warrior.Stats.Get(ForeverCombatStatKeys.AttackCriticalChancePercent));

        warrior.ActiveAuras.Clear();
        auras.ApplyAura(context, warrior, new AuraDefinition
        { Key = ForeverWarriorAuraKeys.BattleStance, DurationSeconds = 30m }, warrior.Key, null, null);
        processor.RefreshDynamicStats(context);
        Assert.Equal(17m, warrior.Stats.Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
    }

    [Fact]
    public void ArmsAutomatic_OpensInBerserkerWhenNoBattleOnlyActionIsEnabled()
    {
        var request = new WarriorSimRequest
        {
            Preset = "arms", TalentMode = "legacy", EquipmentMode = "manual",
            StatSourceMode = "manual", RotationMode = "automatic",
            DurationSeconds = 8m, ExecutePhaseDurationSeconds = 0m,
            StartingRage = 20m, Iterations = 1, Seed = 57821,
            TargetLevel = 60, TargetDodgePercent = 0m,
            UseBloodrage = false, UseRend = false, UseExecute = false,
            UseHeroicStrike = false, UseSlam = false,
            CaptureRepresentativeTimeline = true
        };
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Contains(result.Batch.RepresentativeRun!.Timeline, ev =>
            ev.Type == CombatEventType.AuraApplied &&
            ev.AbilityKey is null && ev.EffectKey is null &&
            ev.Description?.Contains("berserker", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(result.Batch.RepresentativeRun!.Timeline, ev =>
            ev.Type == CombatEventType.AbilityCastStarted &&
            ev.AbilityKey == "enter-battle-stance");
    }

    [Fact]
    public void ArmsAutomatic_DancesToBattleForRendThenReturnsToBerserker()
    {
        var request = new WarriorSimRequest
        {
            Preset = "arms", TalentMode = "legacy", EquipmentMode = "manual",
            StatSourceMode = "manual", RotationMode = "automatic",
            DurationSeconds = 12m, ExecutePhaseDurationSeconds = 0m,
            StartingRage = 100m, Iterations = 1, Seed = 57821,
            TargetLevel = 60, TargetDodgePercent = 0m,
            UseBloodrage = false, UseRend = true, UseExecute = false,
            UseHeroicStrike = false, UseSlam = false,
            CaptureRepresentativeTimeline = true
        };
        var result = new ForeverWarriorSimRunner().Run(request);
        var timeline = result.Batch.RepresentativeRun!.Timeline;
        Assert.Contains(timeline, ev => ev.Type == CombatEventType.AbilityCastStarted &&
            ev.AbilityKey == "enter-battle-stance");
        Assert.Contains(timeline, ev => ev.Type == CombatEventType.AbilityCastStarted &&
            ev.AbilityKey == "rend");
        Assert.Contains(timeline, ev => ev.Type == CombatEventType.AbilityCastStarted &&
            ev.AbilityKey == "enter-berserker-stance");
    }
}
}

// ==================== ForeverWarriorArmsTalentTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverWarriorArmsTalentTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 25)]
    [InlineData(2, 50)]
    public void ImprovedOverpower_OnlyChangesOverpowerCriticalChance(int rank, int expected)
    {
        var ability = Overpower(improvedRank: rank);
        Assert.Equal(expected, Assert.Single(ability.Effects).BonusCriticalChancePercent);
        Assert.Equal(0m, Assert.Single(ForeverRendFactory.CreateForLevel(60).Effects)
            .BonusCriticalChancePercent);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    public void Impale_IncreasesOnlyEligibleDirectAttackCriticalBonus(int rank, int expected)
    {
        var ability = Overpower(impaleRank: rank);
        var effect = Assert.Single(ability.Effects);
        Assert.Equal(expected, effect.BonusCriticalDamagePercent);
        Assert.Equal(rank == 0 ? 0 : 3,
            effect.CriticalDamageBonusRequiredSourceAuraKeys.Count);
        Assert.Equal(0m, Assert.Single(ForeverRendFactory.CreateForLevel(60).Effects)
            .BonusCriticalDamagePercent);
    }

    [Fact]
    public void Impale_RequiresThreePointsInDeepWounds()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Overpower(impaleRank: 1, deepWoundsRank: 2));
        Assert.Equal(20m, Assert.Single(Overpower(impaleRank: 2).Effects)
            .BonusCriticalDamagePercent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImprovedOverpower_BonusWorksWithEitherCombatRollTable(bool singleRoll)
    {
        var ability = Overpower(improvedRank: 2);
        var source = Warrior();
        source.Stats.Set(ForeverCombatStatKeys.AttackCriticalChancePercent, 50m);
        var target = Target();
        var context = Context(source, target);
        var roll = Resolver(singleRoll).Resolve(context, source, target,
            ability, Assert.Single(ability.Effects));
        Assert.True(roll.IsCritical);
        Assert.Equal(2m, roll.AmountMultiplier);
    }

    [Theory]
    [InlineData(false, 2.0)]
    [InlineData(true, 2.2)]
    public void Impale_BoostsOnlyCriticalBonusWhenAStanceIsActive(
        bool hasStance, double expectedMultiplier)
    {
        var ability = Overpower(impaleRank: 2);
        var effect = Assert.Single(ability.Effects);
        // Guarantee a crit to isolate the crit damage bonus calculation.
        effect.BonusCriticalChancePercent = 100m;
        var source = Warrior();
        var target = Target();
        var context = Context(source, target);
        if (hasStance)
            AddStance(context, source, ForeverWarriorAuraKeys.DefensiveStance);
        var roll = Resolver(true).Resolve(context, source, target, ability, effect);
        Assert.True(roll.IsCritical);
        Assert.Equal((decimal)expectedMultiplier, roll.AmountMultiplier);
    }

    [Fact]
    public void Impale_DoesNotAffectNonCriticalDirectEffectsOrBleeds()
    {
        var ability = Overpower();
        ability.Effects.Add(new AbilityEffectDefinition
        {
            Key = "non-crit", EffectType = AbilityEffectTypes.DirectDamage,
            CanCrit = false
        });
        ability.Effects.Add(new AbilityEffectDefinition
        {
            Key = "bleed", EffectType = AbilityEffectTypes.PeriodicDamage,
            CanCrit = true
        });
        ForeverWarriorArmsTalentConfigurator.ApplyImpale(ability, 2, 3);
        Assert.Equal(20m, ability.Effects[0].BonusCriticalDamagePercent);
        Assert.Equal(0m, ability.Effects[1].BonusCriticalDamagePercent);
        Assert.Equal(0m, ability.Effects[2].BonusCriticalDamagePercent);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 3)]
    [InlineData(3, 9)]
    [InlineData(5, 15)]
    public void TacticalMastery_RageRetentionTracksForeverTalentRanks(int rank, int expected)
    {
        var stance = ForeverWarriorStanceFactory.Create(
            ForeverWarriorAuraKeys.BerserkerStance, rank);
        var cap = Assert.Single(stance.Effects, e => e.ResourceKey == "rage");
        Assert.Equal(expected, cap.MinimumValue);
        Assert.Equal(expected, cap.MaximumValue);
        Assert.Equal(ResourceChangeOperationTypes.Cap, cap.ResourceChangeOperation);
    }

    [Fact]
    public void TacticalMastery_SeparatelyConfiguredBaselineIsAddedOnce()
    {
        var stance = ForeverWarriorStanceFactory.Create(
            ForeverWarriorAuraKeys.DefensiveStance, 5, baseRetainedRage: 10m);
        Assert.Equal(25m, Assert.Single(stance.Effects, e => e.ResourceKey == "rage")
            .MinimumValue);
    }

    [Fact]
    public void ResourceCap_ClampsExcessButNeverGrantsRage()
    {
        var resource = new ResourceState("rage", 100m, 50m);
        ResourceManipulator.Apply(resource, ResourceChangeOperationTypes.Cap, 15m);
        Assert.Equal(15m, resource.Current);
        ResourceManipulator.Apply(resource, ResourceChangeOperationTypes.Cap, 90m);
        Assert.Equal(15m, resource.Current);
    }

    [Theory]
    [InlineData(90, 5, 15)]
    [InlineData(7, 5, 7)]
    [InlineData(90, 0, 0)]
    public void StanceSwitch_ThroughSimulationReplacesAuraAndRetainsOnlyAllowedRage(
        int startingRage, int tacticalRank, int expectedRage)
    {
        var warrior = Warrior(startingRage);
        var stance = ForeverWarriorStanceFactory.Create(
            ForeverWarriorAuraKeys.BerserkerStance, tacticalRank);
        warrior.AddAbility(stance);
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = 0.1m, PrimaryActorKey = warrior.Key
        });
        context.AddActor(warrior);
        var aura = new AuraManager();
        var executor = new AbilityExecutor(aura, new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition
            { RulesetKey = "stance-talent-test", Version = "1" }));
        new SimulationEngine([aura, executor]).Run(context, started =>
        {
            AddStance(started, warrior, ForeverWarriorAuraKeys.BattleStance);
            Assert.True(executor.TryStartAbility(started, warrior.Key, warrior.Key,
                stance.Key).Success);
        });
        Assert.Equal(expectedRage, warrior.Resources["rage"].Current);
        Assert.DoesNotContain(warrior.ActiveAuras,
            a => a.Definition.Key == ForeverWarriorAuraKeys.BattleStance);
        Assert.Contains(warrior.ActiveAuras,
            a => a.Definition.Key == ForeverWarriorAuraKeys.BerserkerStance);
    }

    [Fact]
    public void StanceSwitch_RejectsReenteringActiveStanceWithoutSpendingRage()
    {
        var warrior = Warrior(90);
        var ability = ForeverWarriorStanceFactory.Create(
            ForeverWarriorAuraKeys.BattleStance, 5);
        warrior.AddAbility(ability);
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = 1m, PrimaryActorKey = warrior.Key
        });
        context.AddActor(warrior);
        AddStance(context, warrior, ForeverWarriorAuraKeys.BattleStance);
        var executor = new AbilityExecutor(new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition
            { RulesetKey = "already-in-stance", Version = "1" }));
        Assert.False(executor.TryStartAbility(context, warrior.Key, warrior.Key,
            ability.Key).Success);
        Assert.Equal(90m, warrior.Resources["rage"].Current);
    }

    [Fact]
    public void ImprovedOverpowerCritical_HasRealDeepWoundsProcThroughEngine()
    {
        var warrior = Warrior();
        warrior.Stats.Set(ForeverCombatStatKeys.AttackCriticalChancePercent, 50m);
        warrior.AddAbility(Overpower(improvedRank: 2));
        warrior.AddReactiveOpportunityDefinition(ForeverOverpowerFactory.CreateOpportunity());
        warrior.AddCriticalStrikeRollingDamageProc(
            ForeverDeepWoundsFactory.Create(1, 100m, 100m));
        var target = Target();
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = 12.1m, PrimaryActorKey = warrior.Key,
            CaptureTimeline = true, Seed = 9
        });
        context.AddActor(warrior);
        context.AddActor(target);
        var aura = new AuraManager();
        var executor = new AbilityExecutor(aura, Resolver(true), new UnmitigatedResolver());
        var run = new SimulationEngine([
            aura, executor, new ReactiveAbilityOpportunityProcessor(),
            new CriticalStrikeRollingDamageProcessor(aura)
        ]).Run(context, started =>
        {
            warrior.GrantReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
                target.Key, 0m, 5m);
            Assert.True(executor.TryStartAbility(started, warrior.Key, target.Key,
                ForeverOverpowerFactory.AbilityKey).Success);
        });
        Assert.Contains(run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == ForeverOverpowerFactory.AbilityKey && e.IsCritical);
        var bleedTicks = run.Timeline.Where(e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == ForeverDeepWoundsFactory.AbilityKey).ToList();
        Assert.Equal(6, bleedTicks.Count);
        Assert.Equal(20m, bleedTicks.Sum(e => e.RawAmount ?? 0m));
    }

    private sealed class UnmitigatedResolver : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect,
            decimal rawAmount) => DamageMitigationResult.Unmitigated(rawAmount);
    }

    [Fact]
    public void TalentConfiguration_RejectsInvalidRanksAndUnknownStances()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Overpower(improvedRank: 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Overpower(impaleRank: 3));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverWarriorStanceFactory.Create(ForeverWarriorAuraKeys.BattleStance, 6));
        Assert.Throws<ArgumentException>(() =>
            ForeverWarriorStanceFactory.Create("unknown-stance"));
    }

    private static AbilityDefinition Overpower(int improvedRank = 0,
        int impaleRank = 0, int deepWoundsRank = 3) =>
        ForeverOverpowerFactory.CreateForLevel(60, 100m, 100m, 2.4m,
            "weapon-skill", requireBattleStance: false,
            improvedOverpowerRank: improvedRank, impaleRank: impaleRank,
            deepWoundsRank: deepWoundsRank);

    private static SimulationActorState Warrior(int initialRage = 100)
    {
        var actor = new SimulationActorState
        {
            Key = "warrior", Name = "Warrior", TeamKey = "raid", Level = 60
        };
        actor.InitializeHealth(10000m);
        actor.AddResource(new ResourceState("rage", 100m, initialRage));
        return actor;
    }

    private static SimulationActorState Target()
    {
        var actor = new SimulationActorState
        {
            Key = "target", Name = "Target", TeamKey = "enemies", Level = 60
        };
        actor.InitializeHealth(10000m);
        return actor;
    }

    private static SimulationContext Context(SimulationActorState source,
        SimulationActorState target)
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = 1m, Seed = 17, PrimaryActorKey = source.Key
        });
        context.AddActor(source);
        context.AddActor(target);
        return context;
    }

    private static RulesetCombatRollResolver Resolver(bool singleRoll)
    {
        var rule = new CombatRollRuleDefinition
        {
            ResolutionType = ForeverCombatResolutionTypes.PlayerMeleeSpecial,
            UseSingleRollTable = singleRoll,
            BaseHitChancePercent = 100m,
            BaseCriticalChancePercent = 0m,
            CriticalChanceStatKey = ForeverCombatStatKeys.AttackCriticalChancePercent,
            CriticalMultiplier = 2m
        };
        var ruleset = new CombatRulesetDefinition
        {
            RulesetKey = "forever-arms-talent-tests", Version = "1"
        };
        ruleset.RollRules[rule.ResolutionType] = rule;
        return new RulesetCombatRollResolver(ruleset);
    }

    private static void AddStance(SimulationContext context,
        SimulationActorState source, string auraKey) =>
        new AuraManager().ApplyAura(context, source, new AuraDefinition
        {
            Key = auraKey, DurationSeconds = 60m
        }, source.Key, "stance-setup", "stance");
}
}

// ==================== ForeverWarriorCP116Tests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverWarriorCP116Tests
{
    private static (SimulationContext Context, SimulationActorState Warrior,
        AutoAttackProcessor Autos, AbilityExecutor Executor, AutoAttackDefinition Weapon) BuildContext()
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = 321, DurationSeconds = 90m, PrimaryActorKey = "warrior", CaptureTimeline = true
        });
        var actor = new SimulationActorState { Key = "warrior", TeamKey = "raid", Name = "Warrior", Level = 60 };
        actor.InitializeHealth(1000m, 500m);
        actor.AddResource(new ResourceState("rage", 100m, 20m));
        var target = new SimulationActorState { Key = "target", TeamKey = "enemy", Name = "Boss", Level = 60 };
        target.InitializeHealth(100000m);
        context.AddActor(actor);
        context.AddActor(target);
        var ruleset = ForeverCombatRulesetFactory.Create();
        var executor = new AbilityExecutor(new AuraManager(),
            new RulesetCombatRollResolver(ruleset, new ForeverCombatRollContextAdjustmentProvider()),
            new RulesetDamageMitigationResolver(ruleset));
        var autos = new AutoAttackProcessor(executor, new ForeverAutoAttackTimingProvider());
        var mainHand = ForeverWarriorAutoAttackFactory.CreateOneHandedMainHand(
            "main-hand-auto", "MH", 10m, 20m, 2m, "main-hand-weapon-skill");
        return (context, actor, autos, executor, mainHand);
    }

    [Fact]
    public void ResourceEvent_RecordsBalanceImmediatelyAfterChange()
    {
        var (context, warrior, _, _, _) = BuildContext();
        var resource = warrior.Resources["rage"];
        Assert.True(resource.Spend(8m));
        var spend = new CombatEvent
        {
            TimeSeconds = 0m, Type = CombatEventType.ResourceChanged,
            SourceActorKey = "warrior", TargetActorKey = "warrior", Amount = -8m
        };
        context.EmitEvent(spend);
        resource.Gain(5m);
        var gain = new CombatEvent
        {
            TimeSeconds = 0m, Type = CombatEventType.ResourceChanged,
            SourceActorKey = "warrior", TargetActorKey = "warrior", Amount = 5m
        };
        context.EmitEvent(gain);
        Assert.Equal(12m, spend.ResourceTotalAfter);
        Assert.Equal(17m, gain.ResourceTotalAfter);
        Assert.Equal("rage", spend.ResourceKey);
    }

    [Fact]
    public void Flurry_TriggersOnCritAndExpiresAfterThreeSwingEvents()
    {
        var (ctx, warrior, autos, executor, main) = BuildContext();
        var request = new WarriorSimRequest
        {
            TalentMode = "tree", TalentRanks = new() { ["flurry"] = 5 }
        };
        var processor = new WarriorWeaponTalentProcessor(request, autos, executor, main, null);
        Assert.True(autos.Start(ctx, "warrior", "target", main, 2m));
        processor.Process(ctx, new CombatEvent
        {
            Type = CombatEventType.Damage, SourceActorKey = "warrior",
            WeaponHandKey = "main-hand", Amount = 100m, IsCritical = true
        });
        Assert.Equal(25m, warrior.Stats.Get(WarriorWeaponTalentProcessor.FlurryHasteStat));
        for (var i = 0; i < 3; i++)
            processor.Process(ctx, new CombatEvent
            {
                Type = CombatEventType.AutoAttackSwing, SourceActorKey = "warrior",
                AbilityKey = "main-hand-auto", AutoAttackInstanceId = warrior.AutoAttacks["main-hand-auto"].InstanceId
            });
        Assert.Equal(0m, warrior.Stats.Get(WarriorWeaponTalentProcessor.FlurryHasteStat));
    }

    [Fact]
    public void HasteChange_ReschedulesPendingSwingWithoutEndingStream()
    {
        var (ctx, warrior, autos, _, main) = BuildContext();
        Assert.True(autos.Start(ctx, "warrior", "target", main, 2m));
        var state = warrior.AutoAttacks["main-hand-auto"];
        var originalId = state.InstanceId;
        autos.RescheduleForHasteChange(ctx, warrior.Key, 0m, 25m);
        Assert.Equal(1.6m, state.NextSwingAtSeconds);
        Assert.NotEqual(originalId, state.InstanceId);
        Assert.True(state.IsActive);
        autos.RescheduleForHasteChange(ctx, warrior.Key, 25m, 0m);
        Assert.Equal(2m, state.NextSwingAtSeconds);
    }

    [Fact]
    public void GoreDrinker_HealsThreeSuccessfulMeleeAttacksNotPeriodicTicks()
    {
        var (ctx, warrior, autos, executor, main) = BuildContext();
        var request = new WarriorSimRequest { TalentMode = "tree", TalentRanks = new() { ["gore-drinker"] = 2 } };
        var processor = new WarriorWeaponTalentProcessor(request, autos, executor, main, null);
        processor.Process(ctx, new CombatEvent
        {
            Type = CombatEventType.AbilityCastCompleted, SourceActorKey = "warrior", AbilityKey = "bloodthirst"
        });
        for (var i = 0; i < 3; i++)
            processor.Process(ctx, new CombatEvent
            {
                Type = CombatEventType.Damage, SourceActorKey = "warrior",
                WeaponHandKey = "main-hand", Amount = 20m
            });
        Assert.Equal(530m, warrior.CurrentHealth);
        processor.Process(ctx, new CombatEvent
        {
            Type = CombatEventType.Damage, SourceActorKey = "warrior",
            WeaponHandKey = "main-hand", Amount = 20m
        });
        Assert.Equal(530m, warrior.CurrentHealth);
    }

    [Fact]
    public void ResourceEvent_CanSpecifyResourceOnMultiResourceActor()
    {
        var (ctx, actor, _, _, _) = BuildContext();
        actor.AddResource(new ResourceState("energy", 100m, 50m));
        Assert.True(actor.Resources["rage"].Spend(8m));
        var changed = new CombatEvent
        {
            Type = CombatEventType.ResourceChanged, TimeSeconds = 0m,
            SourceActorKey = "warrior", TargetActorKey = "warrior",
            ResourceKey = "rage", Amount = -8m
        };
        ctx.EmitEvent(changed);
        Assert.Equal(12m, changed.ResourceTotalAfter);
    }

    [Fact]
    public void Flurry_PeriodicCriticalTickDoesNotTriggerMeleeHaste()
    {
        var (ctx, actor, autos, executor, main) = BuildContext();
        var request = new WarriorSimRequest
        {
            TalentMode = "tree", TalentRanks = new() { ["flurry"] = 5 }
        };
        var talentProcessor = new WarriorWeaponTalentProcessor(request, autos, executor, main, null);
        talentProcessor.Process(ctx, new CombatEvent
        {
            Type = CombatEventType.Damage, SourceActorKey = "warrior",
            WeaponHandKey = "main-hand", IsCritical = true, Amount = 100m,
            EffectDeliveryType = CombatEffectDeliveryType.Periodic
        });
        Assert.Equal(0m, actor.Stats.Get(WarriorWeaponTalentProcessor.FlurryHasteStat));
    }

    [Fact]
    public void ArmsCustomRotation_AcceptsBaselineWhirlwind()
    {
        var request = new WarriorSimRequest
        {
            Preset = "arms", RotationMode = "custom", EnableStanceDancing = false,
            CustomRotation = [new WarriorRotationRule { Action = "whirlwind" }]
        };
        WarriorRotationRules.Validate(request);
    }

    [Fact]
    public void ArmsCustomWhirlwind_UsesStartingBerserkerWithoutRedundantSwitch()
    {
        var request = new WarriorSimRequest
        {
            Preset = "arms", RotationMode = "custom", TalentMode = "legacy",
            DurationSeconds = 30m, ExecutePhaseDurationSeconds = 0m,
            Iterations = 1, CaptureRepresentativeTimeline = true,
            StartingRage = 100m, TargetLevel = 60, TargetDodgePercent = 0m,
            UseBloodrage = false, UseRend = false, UseHeroicStrike = false,
            CustomRotation = [new WarriorRotationRule { Action = "whirlwind" }]
        };
        var result = new ForeverWarriorSimRunner().Run(request);
        // Arms now starts in Berserker for its crit bonus. Casting the
        // Berserker-only Whirlwind must NOT require a redundant stance swap.
        var timeline = result.Batch.RepresentativeRun!.Timeline;
        Assert.Contains(timeline, e =>
            e.Type == CombatEventType.AuraApplied &&
            e.Description?.Contains("berserker", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(timeline, e =>
            e.Type == CombatEventType.AbilityCastStarted && e.AbilityKey == "enter-berserker-stance");
        Assert.Contains(timeline, e =>
            e.Type == CombatEventType.AbilityCastStarted && e.AbilityKey == "whirlwind");
    }
}

public sealed class ForeverWarriorCP116IncomingTests
{
    [Fact]
    public void Enrage_RespondsOnlyToIncomingDamageAndGrantsRankScaledPhysicalBonus()
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = 9, DurationSeconds = 60m, PrimaryActorKey = "warrior"
        });
        var actor = new SimulationActorState { Key = "warrior", Name = "Warrior", TeamKey = "raid" };
        actor.InitializeHealth(1000m);
        context.AddActor(actor);
        var request = new WarriorSimRequest
        {
            TalentMode = "tree", TalentRanks = new() { ["enrage"] = 3 }
        };
        var processor = new WarriorIncomingTalentProcessor(request);
        // Dealing damage is never enough to proc Enrage.
        processor.Process(context, new CombatEvent
        {
            Type = CombatEventType.Damage, SourceActorKey = "warrior",
            TargetActorKey = "boss", Amount = 10m
        });
        Assert.Equal(0m, actor.Stats.Get(WarriorIncomingTalentProcessor.EnrageBonusKey));
        // At 30% proc chance, 100 seeded eligible hits reliably exercise a proc.
        for (var i = 0; i < 100; i++)
            processor.Process(context, new CombatEvent
            {
                Type = CombatEventType.Damage, SourceActorKey = "boss",
                TargetActorKey = "warrior", Amount = 10m
            });
        Assert.Equal(6m, actor.Stats.Get(WarriorIncomingTalentProcessor.EnrageBonusKey));
    }
}
}

// ==================== ForeverWarriorDamageTakenRageTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverWarriorDamageTakenRageTests
{
    private const decimal ExpectedCreatureHealth = 1000m;
    private const decimal ExpectedArmorReductionPercent = 30m;

    [Fact]
    public void Factory_UsesExplicitPostOctoberEightCalibration()
    {
        var definition =
            ForeverWarriorDamageTakenRageFactory.Create(
                ExpectedCreatureHealth,
                ExpectedArmorReductionPercent
            );

        Assert.Equal(
            "rage",
            definition.ResourceKey
        );

        Assert.Equal(
            ExpectedCreatureHealth,
            definition.ReferenceHealth
        );

        Assert.Equal(
            ForeverWarriorDamageTakenRageFactory
                .ProvisionalUnmitigatedRagePerReferenceHealth,
            definition.ResourcePerReferenceHealthOfEligibleDamage
        );

        Assert.Equal(
            ExpectedArmorReductionPercent,
            definition.IgnoredArmorReplacementReductionPercent
        );

        Assert.True(definition.IgnoreArmorMitigation);
        Assert.True(definition.IgnoreAbsorbs);
        Assert.True(definition.BlockReducesEligibleDamage);
        Assert.True(definition.RequiresExternalSourceActor);
        Assert.True(
            ForeverWarriorDamageTakenRageFactory.FormulaShapeVerifiedByBlizzard
        );
        Assert.False(
            ForeverWarriorDamageTakenRageFactory.CalibrationCurveVerifiedByBlizzard
        );
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(25, 150)]
    [InlineData(50, 100)]
    [InlineData(75, 50)]
    public void ActualArmorMitigation_IsReplacedByConfiguredExpectedArmor(
        double mitigationPercent,
        double healthDamage)
    {
        var target = CreateWarrior(1000m, 0m);

        var result =
            RunDamageEvent(
                target,
                new CombatEvent
                {
                    TimeSeconds = 0.1m,
                    Type = CombatEventType.Damage,
                    SourceActorKey = "enemy",
                    TargetActorKey = target.Key,
                    AbilityKey = "enemy-swing",
                    MitigationType = DamageMitigationTypes.Armor,
                    ResultKey = CombatResultTypes.Hit,
                    RawAmount = 200m,
                    MitigationPercent = (decimal)mitigationPercent,
                    MitigatedAmount =
                        200m -
                        (decimal)healthDamage,
                    Amount = (decimal)healthDamage
                }
            );

        // 200 pre-Armor damage is normalized through the configured 30%
        // expected reduction: 140 / 1000 * 20 = 2.8 Rage.
        Assert.Equal(
            2.8m,
            target.Resources["rage"].Current
        );

        var resourceEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type == CombatEventType.ResourceChanged &&
                    combatEvent.AbilityKey ==
                        ForeverWarriorDamageTakenRageFactory.DefinitionKey
            );

        Assert.Equal(2.8m, resourceEvent.Amount);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2500)]
    public void PlayerMaximumHealth_DoesNotChangeNormalizedIncomingRage(
        double playerMaximumHealth)
    {
        var target =
            CreateWarrior(
                (decimal)playerMaximumHealth,
                0m
            );

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "enemy",
                TargetActorKey = target.Key,
                AbilityKey = "enemy-swing",
                MitigationType = DamageMitigationTypes.Armor,
                RawAmount = 200m,
                MitigationPercent = 50m,
                Amount = 100m
            }
        );

        Assert.Equal(
            2.8m,
            target.Resources["rage"].Current
        );
    }

    [Fact]
    public void AbsorbedArmorDamage_DoesNotReduceForeverIncomingRage()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "enemy",
                TargetActorKey = target.Key,
                AbilityKey = "enemy-swing",
                MitigationType = DamageMitigationTypes.Armor,
                RawAmount = 200m,
                MitigationPercent = 50m,
                AbsorbedAmount = 75m,
                Amount = 25m
            }
        );

        Assert.Equal(
            2.8m,
            target.Resources["rage"].Current
        );
    }

    [Fact]
    public void AbsorbedNonArmorDamage_DoesNotReduceForeverIncomingRage()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "enemy",
                TargetActorKey = target.Key,
                AbilityKey = "enemy-spell",
                MitigationType = DamageMitigationTypes.None,
                RawAmount = 200m,
                AbsorbedAmount = 150m,
                Amount = 50m
            }
        );

        Assert.Equal(
            4m,
            target.Resources["rage"].Current
        );
    }

    [Fact]
    public void Block_ReducesEligibleDamageBeforeExpectedArmorReplacement()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "enemy",
                TargetActorKey = target.Key,
                AbilityKey = "enemy-swing",
                MitigationType = DamageMitigationTypes.Armor,
                RawAmount = 200m,
                MitigationPercent = 50m,
                MitigatedAmount = 100m,
                BlockedAmount = 20m,
                Amount = 80m
            }
        );

        // 20 blocked after 50% actual Armor corresponds to 40 pre-Armor
        // damage. The remaining 160 is then normalized through 30% expected
        // Armor: 112 / 1000 * 20 = 2.24 Rage.
        Assert.Equal(
            2.24m,
            target.Resources["rage"].Current
        );
    }

    [Fact]
    public void NonArmorMitigation_RemainsRespectedWithoutArmorReplacement()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "enemy",
                TargetActorKey = target.Key,
                AbilityKey = "enemy-fire",
                MitigationType = DamageMitigationTypes.Resistance,
                RawAmount = 200m,
                MitigationPercent = 50m,
                MitigatedAmount = 100m,
                Amount = 100m
            }
        );

        Assert.Equal(
            2m,
            target.Resources["rage"].Current
        );
    }

    [Fact]
    public void CriticalOrCrushingRawDamage_RemainsInNormalizedDamageBasis()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "enemy",
                TargetActorKey = target.Key,
                AbilityKey = "enemy-crit",
                MitigationType = DamageMitigationTypes.Armor,
                ResultKey = CombatResultTypes.Critical,
                RawAmount = 400m,
                MitigationPercent = 50m,
                Amount = 200m
            }
        );

        Assert.Equal(
            5.6m,
            target.Resources["rage"].Current
        );
    }

    [Fact]
    public void DamageWithoutSourceActor_GeneratesNoRage()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                TargetActorKey = target.Key,
                AbilityKey = "environment",
                MitigationType = DamageMitigationTypes.None,
                RawAmount = 200m,
                Amount = 200m
            },
            addEnemy: false
        );

        Assert.Equal(0m, target.Resources["rage"].Current);
    }

    [Fact]
    public void SelfDamage_GeneratesNoRage()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = target.Key,
                TargetActorKey = target.Key,
                AbilityKey = "self-damage",
                MitigationType = DamageMitigationTypes.None,
                RawAmount = 200m,
                Amount = 200m
            },
            addEnemy: false
        );

        Assert.Equal(0m, target.Resources["rage"].Current);
    }

    [Fact]
    public void UnknownSourceActor_GeneratesNoRage()
    {
        var target = CreateWarrior(1000m, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "missing-enemy",
                TargetActorKey = target.Key,
                AbilityKey = "unknown-source",
                MitigationType = DamageMitigationTypes.None,
                RawAmount = 200m,
                Amount = 200m
            },
            addEnemy: false
        );

        Assert.Equal(0m, target.Resources["rage"].Current);
    }

    [Fact]
    public void IncomingRage_IsClampedAndReportsActualGain()
    {
        var target = CreateWarrior(1000m, 99.5m);

        var result =
            RunDamageEvent(
                target,
                new CombatEvent
                {
                    TimeSeconds = 0.1m,
                    Type = CombatEventType.Damage,
                    SourceActorKey = "enemy",
                    TargetActorKey = target.Key,
                    AbilityKey = "enemy-swing",
                    MitigationType = DamageMitigationTypes.Armor,
                    RawAmount = 200m,
                    Amount = 100m,
                    MitigationPercent = 50m
                }
            );

        Assert.Equal(100m, target.Resources["rage"].Current);

        var resourceEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type == CombatEventType.ResourceChanged &&
                    combatEvent.AbilityKey ==
                        ForeverWarriorDamageTakenRageFactory.DefinitionKey
            );

        Assert.Equal(0.5m, resourceEvent.Amount);
    }

    [Fact]
    public void Factory_RejectsMissingOrInvalidCalibration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverWarriorDamageTakenRageFactory.Create(
                0m,
                30m
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverWarriorDamageTakenRageFactory.Create(
                1000m,
                -1m
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverWarriorDamageTakenRageFactory.Create(
                1000m,
                101m
            )
        );
    }

    [Fact]
    public void ConfiguredIncomingGenerationWithoutResource_FailsExplicitly()
    {
        var target = CreateActor("warrior", 1000m);
        ConfigureIncomingRage(target);

        Assert.Throws<InvalidOperationException>(() =>
            RunDamageEvent(
                target,
                new CombatEvent
                {
                    TimeSeconds = 0.1m,
                    Type = CombatEventType.Damage,
                    SourceActorKey = "enemy",
                    TargetActorKey = target.Key,
                    AbilityKey = "enemy-swing",
                    MitigationType = DamageMitigationTypes.None,
                    RawAmount = 100m,
                    Amount = 100m
                }
            )
        );
    }

    [Fact]
    public void ActorWithoutIncomingGenerationConfiguration_GainsNoRage()
    {
        var target = CreateActor("warrior", 1000m);
        AddRage(target, 0m);

        RunDamageEvent(
            target,
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "enemy",
                TargetActorKey = target.Key,
                AbilityKey = "enemy-swing",
                MitigationType = DamageMitigationTypes.None,
                RawAmount = 100m,
                Amount = 100m
            }
        );

        Assert.Equal(0m, target.Resources["rage"].Current);
    }

    [Fact]
    public void DuplicateIncomingGenerationConfiguration_FailsExplicitly()
    {
        var target = CreateActor("warrior", 1000m);

        ConfigureIncomingRage(target);

        Assert.Throws<InvalidOperationException>(() =>
            ConfigureIncomingRage(target)
        );
    }

    [Fact]
    public void ScriptedArmorDamage_IntegratesWithIncomingRageProcessor()
    {
        var context = CreateContext();
        var enemy = CreateActor("enemy", 1000m);
        var target = CreateWarrior(1000m, 0m);
        target.Stats.Set("armor", 400m);

        context.AddActor(enemy);
        context.AddActor(target);

        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey = "incoming-rage-integration",
                Version = "1",
                MitigationRules =
                {
                    [DamageMitigationTypes.Armor] =
                        new DamageMitigationRuleDefinition
                        {
                            MitigationType = DamageMitigationTypes.Armor,
                            FormulaType =
                                DamageMitigationFormulaTypes.RationalLevelScaled,
                            DefenseStatKey = "armor",
                            BaseConstant = 400m,
                            PerAttackerLevelConstant = 85m,
                            MaximumReductionPercent = 75m
                        }
                }
            };

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.ScriptedDamage,
                SourceActorKey = enemy.Key,
                TargetActorKey = target.Key,
                EncounterEventKey = "tank-hit",
                RawAmount = 200m,
                MitigationType = DamageMitigationTypes.Armor,
                SchoolKey = "physical"
            }
        );

        var result =
            new SimulationEngine(
                [
                    new ScriptedEncounterDamageProcessor(
                        new RulesetDamageMitigationResolver(ruleset)
                    ),
                    new DamageTakenResourceGenerationProcessor()
                ]
            ).Run(context);

        Assert.Equal(
            2.8m,
            target.Resources["rage"].Current
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.RawAmount == 200m &&
                combatEvent.Amount < 200m
        );
    }

    private static SimulationRunResult RunDamageEvent(
        SimulationActorState target,
        CombatEvent damageEvent,
        bool addEnemy = true)
    {
        var context = CreateContext();

        if (addEnemy)
        {
            context.AddActor(
                CreateActor("enemy", 1000m)
            );
        }

        context.AddActor(target);
        context.ScheduleEvent(damageEvent);

        return new SimulationEngine(
            [new DamageTakenResourceGenerationProcessor()]
        ).Run(context);
    }

    private static SimulationActorState CreateWarrior(
        decimal maximumHealth,
        decimal rage)
    {
        var actor = CreateActor("warrior", maximumHealth);
        AddRage(actor, rage);
        ConfigureIncomingRage(actor);
        return actor;
    }

    private static void ConfigureIncomingRage(
        SimulationActorState actor)
    {
        ForeverWarriorDamageTakenRageFactory.Configure(
            actor,
            ExpectedCreatureHealth,
            ExpectedArmorReductionPercent
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        decimal health)
    {
        var actor =
            new SimulationActorState
            {
                Key = key,
                Name = key,
                TeamKey =
                    key == "enemy"
                        ? "enemies"
                        : "players",
                Level = 30
            };

        actor.InitializeHealth(health);
        return actor;
    }

    private static void AddRage(
        SimulationActorState actor,
        decimal current)
    {
        actor.AddResource(
            new ResourceState(
                "rage",
                100m,
                current
            )
        );
    }

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = 0.5m,
                PrimaryActorKey = "warrior",
                CaptureTimeline = true
            }
        );
    }
}
}

// ==================== ForeverWarriorRageGenerationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverWarriorRageGenerationTests
{
    [Theory]
    [InlineData("one-hand", 2.6, 8.996)]
    [InlineData("two-hand", 3.6, 16.2)]
    [InlineData("dual-main", 2.4, 8.304)]
    [InlineData("dual-off", 1.8, 3.114)]
    public void WarriorFactories_UseNormalizedRagePerBaseWeaponSpeed(
        string profile,
        double weaponSpeedSeconds,
        double expectedRagePerLandedSwing)
    {
        var definition =
            profile switch
            {
                "one-hand" =>
                    ForeverWarriorAutoAttackFactory.CreateOneHandedMainHand(
                        "main-hand",
                        "Main Hand",
                        10m,
                        10m,
                        (decimal)weaponSpeedSeconds,
                        "sword-skill"
                    ),

                "two-hand" =>
                    ForeverWarriorAutoAttackFactory.CreateTwoHandedMainHand(
                        "main-hand",
                        "Main Hand",
                        10m,
                        10m,
                        (decimal)weaponSpeedSeconds,
                        "two-hand-sword-skill"
                    ),

                "dual-main" =>
                    ForeverWarriorAutoAttackFactory.CreateDualWieldMainHand(
                        "main-hand",
                        "Main Hand",
                        10m,
                        10m,
                        (decimal)weaponSpeedSeconds,
                        "sword-skill"
                    ),

                "dual-off" =>
                    ForeverWarriorAutoAttackFactory.CreateDualWieldOffHand(
                        "off-hand",
                        "Off Hand",
                        10m,
                        10m,
                        (decimal)weaponSpeedSeconds,
                        "sword-skill"
                    ),

                _ => throw new InvalidOperationException()
            };

        var generation =
            Assert.Single(
                definition.ResourceGenerations
            );

        Assert.Equal(
            ForeverWarriorAutoAttackFactory.RageResourceKey,
            generation.ResourceKey
        );

        Assert.Equal(
            (decimal)expectedRagePerLandedSwing,
            generation.AmountPerLandedSwing
        );
    }

    [Fact]
    public void OneHandedCriticalBasicAttack_DoublesGeneratedRage()
    {
        var definition =
            ForeverWarriorAutoAttackFactory.CreateOneHandedMainHand(
                "main-hand",
                "Main Hand",
                10m,
                10m,
                2m,
                "sword-skill"
            );

        var generation =
            Assert.Single(
                definition.ResourceGenerations
            );

        Assert.Equal(1m, generation.CriticalMultiplier);
        Assert.Equal(
            2m *
            ForeverWarriorAutoAttackFactory.OneHandedCriticalBonusRagePerSecond,
            generation.CriticalBonusAmountPerLandedSwing
        );
    }

    [Fact]
    public void TwoHandedCriticalBasicAttack_AddsOneHandedBaseShare()
    {
        var definition =
            ForeverWarriorAutoAttackFactory.CreateTwoHandedMainHand(
                "main-hand",
                "Main Hand",
                10m,
                10m,
                3.6m,
                "two-hand-sword-skill"
            );

        var generation =
            Assert.Single(
                definition.ResourceGenerations
            );

        var expectedNormalRage =
            3.6m *
            ForeverWarriorAutoAttackFactory.TwoHandedMainHandRagePerSecond;

        var expectedCriticalBonus =
            3.6m *
            ForeverWarriorAutoAttackFactory.TwoHandedCriticalBonusRagePerSecond;

        Assert.Equal(1m, generation.CriticalMultiplier);
        Assert.Equal(
            expectedCriticalBonus,
            generation.CriticalBonusAmountPerLandedSwing
        );
        Assert.Equal(
            expectedNormalRage + expectedCriticalBonus,
            generation.AmountPerLandedSwing +
            generation.CriticalBonusAmountPerLandedSwing
        );
    }

    [Fact]
    public void TwoHandedCriticalSwing_UsesObservedNormalizedCriticalBonus()
    {
        var source = CreateActor("source");
        AddRage(source, 0m);

        var definition =
            ForeverWarriorAutoAttackFactory.CreateTwoHandedMainHand(
                "main-hand",
                "Main Hand",
                1m,
                1m,
                3.6m,
                "two-hand-sword-skill"
            );

        RunSingleSwing(
            source,
            CreateActor("target", 1000m),
            definition,
            CombatRollResult.Critical(2m)
        );

        Assert.Equal(
            28.656m,
            source.Resources["rage"].Current
        );
    }

    [Theory]
    [InlineData("hit", 6.92)]
    [InlineData("glancing", 6.92)]
    [InlineData("block", 6.92)]
    [InlineData("critical", 13.84)]
    public void LandedBasicAttack_GeneratesExpectedNormalizedRage(
        string resultKey,
        double expectedRage)
    {
        var roll =
            resultKey switch
            {
                "hit" => CombatRollResult.Hit(),
                "glancing" => CombatRollResult.Glancing(0.7m),
                "block" => CombatRollResult.Block(5m),
                "critical" => CombatRollResult.Critical(2m),
                _ => throw new InvalidOperationException()
            };

        var source = CreateActor("source");
        AddRage(source, 0m);

        var result =
            RunSingleSwing(
                source,
                CreateActor("target", 1000m),
                CreateOneHandedAttack(2m),
                roll
            );

        Assert.Equal(
            (decimal)expectedRage,
            source.Resources["rage"].Current
        );

        var resourceEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type == CombatEventType.ResourceChanged &&
                    combatEvent.AbilityKey == "main-hand"
            );

        Assert.Equal((decimal)expectedRage, resourceEvent.Amount);
        Assert.Equal(resultKey, resourceEvent.ResultKey);
    }

    [Theory]
    [InlineData("miss")]
    [InlineData("dodge")]
    [InlineData("parry")]
    public void AvoidedBasicAttack_GeneratesNoRage(
        string resultKey)
    {
        var roll =
            resultKey switch
            {
                "miss" => CombatRollResult.Miss(),
                "dodge" => CombatRollResult.Dodge(),
                "parry" => CombatRollResult.Parry(),
                _ => throw new InvalidOperationException()
            };

        var source = CreateActor("source");
        AddRage(source, 0m);

        var result =
            RunSingleSwing(
                source,
                CreateActor("target", 1000m),
                CreateOneHandedAttack(2m),
                roll
            );

        Assert.Equal(
            0m,
            source.Resources["rage"].Current
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.ResourceChanged &&
                combatEvent.AbilityKey == "main-hand"
        );
    }

    [Fact]
    public void SuccessfulNextSwingReplacement_DoesNotGenerateBasicAttackRage()
    {
        var source = CreateActor("source");
        AddRage(source, 0m);
        var target = CreateActor("target", 1000m);
        var definition = CreateOneHandedAttack(2m);
        var replacement = CreateReplacement(resourceCost: 0m);

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor(CombatRollResult.Hit());

        var result =
            new SimulationEngine([processor])
                .Run(
                    context,
                    startedContext =>
                    {
                        Assert.True(
                            processor.Start(
                                startedContext,
                                source.Key,
                                target.Key,
                                definition,
                                firstSwingDelaySeconds: 0m
                            )
                        );

                        Assert.True(
                            processor.QueueNextSwingReplacement(
                                startedContext,
                                source.Key,
                                definition.Key,
                                replacement
                            )
                        );
                    }
                );

        Assert.Equal(0m, source.Resources["rage"].Current);

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == replacement.Key
        );
    }

    [Fact]
    public void UnaffordableQueuedReplacement_FallsBackToWhiteSwingAndGeneratesRage()
    {
        var source = CreateActor("source");
        AddRage(source, 15m);
        var target = CreateActor("target", 1000m);
        var definition = CreateOneHandedAttack(2m);
        var replacement = CreateReplacement(resourceCost: 15m);

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor(CombatRollResult.Hit());

        var result =
            new SimulationEngine([processor])
                .Run(
                    context,
                    startedContext =>
                    {
                        Assert.True(
                            processor.Start(
                                startedContext,
                                source.Key,
                                target.Key,
                                definition,
                                firstSwingDelaySeconds: 0m
                            )
                        );

                        Assert.True(
                            processor.QueueNextSwingReplacement(
                                startedContext,
                                source.Key,
                                definition.Key,
                                replacement
                            )
                        );

                        Assert.True(
                            source.Resources["rage"].Spend(15m)
                        );
                    }
                );

        Assert.Equal(6.92m, source.Resources["rage"].Current);

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == definition.Key
        );
    }

    [Fact]
    public void BasicAttackRage_IsClampedToResourceMaximumAndReportsActualGain()
    {
        var source = CreateActor("source");
        AddRage(source, 98m);

        var result =
            RunSingleSwing(
                source,
                CreateActor("target", 1000m),
                CreateOneHandedAttack(1m),
                CombatRollResult.Hit()
            );

        Assert.Equal(100m, source.Resources["rage"].Current);

        var resourceEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type == CombatEventType.ResourceChanged &&
                    combatEvent.AbilityKey == "main-hand"
            );

        Assert.Equal(2m, resourceEvent.Amount);
    }

    [Fact]
    public void ConfiguredGenerationWithoutActorResource_FailsExplicitly()
    {
        var source = CreateActor("source");

        Assert.Throws<InvalidOperationException>(() =>
            RunSingleSwing(
                source,
                CreateActor("target", 1000m),
                CreateOneHandedAttack(1m),
                CombatRollResult.Hit()
            )
        );
    }

    private static AutoAttackDefinition CreateOneHandedAttack(
        decimal weaponSpeedSeconds)
    {
        return ForeverWarriorAutoAttackFactory.CreateOneHandedMainHand(
            "main-hand",
            "Main Hand",
            1m,
            1m,
            weaponSpeedSeconds,
            "sword-skill"
        );
    }

    private static NextSwingReplacementDefinition CreateReplacement(
        decimal resourceCost)
    {
        var definition =
            new NextSwingReplacementDefinition
            {
                Key = "heroic-strike",
                Name = "Heroic Strike",
                WeaponHandKey = WeaponHandKeys.MainHand,
                DamageEffect =
                    new AbilityEffectDefinition
                    {
                        Key = "damage",
                        EffectType = AbilityEffectTypes.DirectDamage,
                        TargetType = AbilityTargetTypes.Enemy,
                        WeaponHandKey = WeaponHandKeys.MainHand,
                        MinimumValue = 10m,
                        MaximumValue = 10m,
                        MitigationType = DamageMitigationTypes.None
                    }
            };

        definition.ResourceCosts.Add(
            new AbilityResourceCost
            {
                ResourceKey = "rage",
                Amount = resourceCost
            }
        );

        return definition;
    }

    private static SimulationRunResult RunSingleSwing(
        SimulationActorState source,
        SimulationActorState target,
        AutoAttackDefinition definition,
        CombatRollResult roll)
    {
        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor(roll);

        return new SimulationEngine([processor])
            .Run(
                context,
                startedContext =>
                {
                    Assert.True(
                        processor.Start(
                            startedContext,
                            source.Key,
                            target.Key,
                            definition,
                            firstSwingDelaySeconds: 0m
                        )
                    );
                }
            );
    }

    private static AutoAttackProcessor CreateProcessor(
        CombatRollResult result)
    {
        return new AutoAttackProcessor(
            new AbilityExecutor(
                new AuraManager(),
                new FixedCombatRollResolver(result),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey = "forever-warrior-rage-tests",
                        Version = "1",
                        MitigationRules =
                        {
                            [DamageMitigationTypes.Armor] =
                                new DamageMitigationRuleDefinition
                                {
                                    MitigationType = DamageMitigationTypes.Armor,
                                    FormulaType =
                                        DamageMitigationFormulaTypes.RationalLevelScaled,
                                    DefenseStatKey = "armor",
                                    BaseConstant = 400m,
                                    PerAttackerLevelConstant = 85m,
                                    MaximumReductionPercent = 75m
                                }
                        }
                    }
                )
            )
        );
    }

    private static void AddRage(
        SimulationActorState actor,
        decimal current)
    {
        actor.AddResource(
            new ResourceState(
                "rage",
                100m,
                current
            )
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        decimal health = 100m)
    {
        var actor =
            new SimulationActorState
            {
                Key = key,
                Name = key,
                TeamKey =
                    key == "source"
                        ? "players"
                        : "enemies",
                Level = 30
            };

        actor.InitializeHealth(health);

        return actor;
    }

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = 0.5m,
                PrimaryActorKey = "source",
                CaptureTimeline = true
            }
        );
    }

    private sealed class FixedCombatRollResolver(
        CombatRollResult result) : ICombatRollResolver
    {
        public CombatRollResult Resolve(
            SimulationContext context,
            SimulationActorState source,
            SimulationActorState target,
            AbilityDefinition ability,
            AbilityEffectDefinition effect)
        {
            return result;
        }
    }
}
}
