using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

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
