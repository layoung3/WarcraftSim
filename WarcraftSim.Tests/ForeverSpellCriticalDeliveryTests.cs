using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverSpellCriticalDeliveryTests
{
    [Fact]
    public void NonPeriodicSpellCriticalBonus_AppliesToDirectSpell()
    {
        var result = Resolve(
            CombatEffectDeliveryType.Direct,
            nonPeriodicCriticalChancePercent: 100m
        );

        Assert.True(result.IsCritical);
    }

    [Fact]
    public void NonPeriodicSpellCriticalBonus_AppliesToChannelTick()
    {
        var result = Resolve(
            CombatEffectDeliveryType.ChannelTick,
            nonPeriodicCriticalChancePercent: 100m
        );

        Assert.True(result.IsCritical);
    }

    [Fact]
    public void NonPeriodicSpellCriticalBonus_DoesNotApplyToPeriodicTick()
    {
        var result = Resolve(
            CombatEffectDeliveryType.Periodic,
            nonPeriodicCriticalChancePercent: 100m
        );

        Assert.False(result.IsCritical);
        Assert.Equal(
            CombatResultTypes.Hit,
            result.ResultKey
        );
    }

    [Fact]
    public void PeriodicSpellCriticalBonus_AppliesToPeriodicTick()
    {
        var result = Resolve(
            CombatEffectDeliveryType.Periodic,
            periodicCriticalChancePercent: 100m
        );

        Assert.True(result.IsCritical);
    }

    [Theory]
    [InlineData(CombatEffectDeliveryType.Direct)]
    [InlineData(CombatEffectDeliveryType.ChannelTick)]
    public void PeriodicSpellCriticalBonus_DoesNotApplyToNonPeriodicDelivery(
        CombatEffectDeliveryType deliveryType)
    {
        var result = Resolve(
            deliveryType,
            periodicCriticalChancePercent: 100m
        );

        Assert.False(result.IsCritical);
        Assert.Equal(
            CombatResultTypes.Hit,
            result.ResultKey
        );
    }

    [Theory]
    [InlineData(CombatEffectDeliveryType.Direct)]
    [InlineData(CombatEffectDeliveryType.Periodic)]
    [InlineData(CombatEffectDeliveryType.ChannelTick)]
    public void GeneralSpellCriticalChance_StillAppliesToEveryDeliveryType(
        CombatEffectDeliveryType deliveryType)
    {
        var result = Resolve(
            deliveryType,
            generalCriticalChancePercent: 100m
        );

        Assert.True(result.IsCritical);
    }

    [Theory]
    [InlineData(CombatEffectDeliveryType.Direct)]
    [InlineData(CombatEffectDeliveryType.Periodic)]
    [InlineData(CombatEffectDeliveryType.ChannelTick)]
    public void CanCritFalse_PreventsCriticalRegardlessOfDeliveryBonuses(
        CombatEffectDeliveryType deliveryType)
    {
        var result = Resolve(
            deliveryType,
            canCrit: false,
            generalCriticalChancePercent: 100m,
            nonPeriodicCriticalChancePercent: 100m,
            periodicCriticalChancePercent: 100m
        );

        Assert.False(result.IsCritical);
        Assert.Equal(
            CombatResultTypes.Hit,
            result.ResultKey
        );
    }

    [Fact]
    public void DeliverySpecificCriticalProvider_IgnoresPhysicalResolution()
    {
        var source = CreateActor(
            "player",
            "raid"
        );

        source.Stats.Set(
            ForeverCombatStatKeys.PeriodicSpellCriticalChancePercent,
            100m
        );

        var effect = new AbilityEffectDefinition
        {
            Key = "physical",
            ResolutionType =
                ForeverCombatResolutionTypes.PlayerMeleeSpecial
        };

        var rule =
            ForeverPhysicalCombatRulesetFactory
                .Create()
                .GetRollRule(
                    ForeverCombatResolutionTypes.PlayerMeleeSpecial
                )!;

        var adjustment =
            new ForeverSpellCriticalDeliveryAdjustmentProvider()
                .GetAdjustment(
                    CreateContext(),
                    source,
                    CreateActor(
                        "target",
                        "enemy"
                    ),
                    CreateAbility(),
                    effect,
                    rule,
                    CombatEffectDeliveryType.Periodic
                );

        Assert.Same(
            CombatRollContextAdjustment.None,
            adjustment
        );
    }

    private static CombatRollResult Resolve(
        CombatEffectDeliveryType deliveryType,
        bool canCrit = true,
        decimal generalCriticalChancePercent = 0m,
        decimal nonPeriodicCriticalChancePercent = 0m,
        decimal periodicCriticalChancePercent = 0m)
    {
        var source = CreateActor(
            "player",
            "raid"
        );

        var target = CreateActor(
            "target",
            "enemy"
        );

        source.Stats.Set(
            ForeverCombatStatKeys.SpellCriticalChancePercent,
            generalCriticalChancePercent
        );

        source.Stats.Set(
            ForeverCombatStatKeys.NonPeriodicSpellCriticalChancePercent,
            nonPeriodicCriticalChancePercent
        );

        source.Stats.Set(
            ForeverCombatStatKeys.PeriodicSpellCriticalChancePercent,
            periodicCriticalChancePercent
        );

        var effect = new AbilityEffectDefinition
        {
            Key = "spell-effect",
            EffectType =
                AbilityEffectTypes.DirectDamage,
            TargetType =
                AbilityTargetTypes.Enemy,
            ResolutionType =
                ForeverCombatResolutionTypes.PlayerSpell,
            CanMiss = false,
            CanCrit = canCrit
        };

        var resolver =
            new RulesetCombatRollResolver(
                ForeverCombatRulesetFactory.Create(),
                new ForeverCombatRollContextAdjustmentProvider()
            );

        return resolver.Resolve(
            CreateContext(),
            source,
            target,
            CreateAbility(),
            effect,
            deliveryType
        );
    }

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = 1m,
                PrimaryActorKey = "player"
            }
        );
    }

    private static AbilityDefinition CreateAbility()
    {
        return new AbilityDefinition
        {
            Key = "spell",
            Name = "Spell"
        };
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey)
    {
        var actor = new SimulationActorState
        {
            Key = key,
            Name = key,
            TeamKey = teamKey,
            Level = 30
        };

        actor.InitializeHealth(
            maximumHealth: 5000m
        );

        return actor;
    }
}
