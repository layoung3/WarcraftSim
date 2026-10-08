using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class NextSwingOutcomeResourceRefundTests
{
    [Theory]
    [InlineData(CombatResultTypes.Miss)]
    [InlineData(CombatResultTypes.Dodge)]
    [InlineData(CombatResultTypes.Parry)]
    public void ConfiguredAvoidedOutcome_RefundsPaidResource(
        string resultKey)
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack();
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            80m,
            CombatResultTypes.Miss,
            CombatResultTypes.Dodge,
            CombatResultTypes.Parry
        );

        var result = Run(
            source,
            target,
            mainHand,
            replacement,
            CombatRollResult.Avoided(resultKey)
        );

        Assert.Equal(27m, source.Resources["rage"].Current);

        var damage = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage
        );

        Assert.Equal(resultKey, damage.ResultKey);
        Assert.Equal(0m, damage.Amount);
    }

    [Fact]
    public void DefaultReplacement_DoesNotRefundAvoidedOutcome()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);

        Run(
            source,
            target,
            CreateAutoAttack(),
            CreateReplacement(),
            CombatRollResult.Miss()
        );

        Assert.Equal(15m, source.Resources["rage"].Current);
    }

    [Fact]
    public void UnconfiguredAvoidedOutcome_DoesNotRefund()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            80m,
            CombatResultTypes.Miss
        );

        Run(
            source,
            target,
            CreateAutoAttack(),
            replacement,
            CombatRollResult.Dodge()
        );

        Assert.Equal(15m, source.Resources["rage"].Current);
    }

    [Fact]
    public void LandedHit_DoesNotRefundAvoidanceRule()
    {
        AssertNoRefundForLandedResult(
            CombatRollResult.Hit()
        );
    }

    [Fact]
    public void CriticalHit_DoesNotRefundAvoidanceRule()
    {
        AssertNoRefundForLandedResult(
            CombatRollResult.Critical(2m)
        );
    }

    [Fact]
    public void BlockedHit_DoesNotRefundAvoidanceRule()
    {
        AssertNoRefundForLandedResult(
            CombatRollResult.Block(5m)
        );
    }

    [Fact]
    public void RefundUsesAmountActuallyPaidForPercentCost()
    {
        var source = CreateActor("source");
        AddResource(source, "energy", 120m, 120m);
        var target = CreateActor("target", 1000m);
        var replacement = CreateReplacement(
            resourceKey: "energy",
            resourceAmount: 25m,
            isPercentOfMaximum: true
        );
        AddRefundRule(
            replacement,
            "energy",
            80m,
            CombatResultTypes.Miss
        );

        Run(
            source,
            target,
            CreateAutoAttack(),
            replacement,
            CombatRollResult.Miss()
        );

        // Paid 30 (25% of 120), then refunded 24 (80% of 30).
        Assert.Equal(114m, source.Resources["energy"].Current);
    }

    [Fact]
    public void MultipleResourceCosts_CanRefundIndependently()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        AddResource(source, "mana", 100m, 40m);
        var target = CreateActor("target", 1000m);
        var replacement = CreateReplacement();

        replacement.ResourceCosts.Add(
            new AbilityResourceCost
            {
                ResourceKey = "mana",
                Amount = 20m
            }
        );

        AddRefundRule(
            replacement,
            "rage",
            80m,
            CombatResultTypes.Miss
        );
        AddRefundRule(
            replacement,
            "mana",
            50m,
            CombatResultTypes.Miss
        );

        Run(
            source,
            target,
            CreateAutoAttack(),
            replacement,
            CombatRollResult.Miss()
        );

        Assert.Equal(27m, source.Resources["rage"].Current);
        Assert.Equal(30m, source.Resources["mana"].Current);
    }

    [Fact]
    public void RefundEvent_ReportsPositiveAmountResultAndAbilityKey()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            80m,
            CombatResultTypes.Miss
        );

        var result = Run(
            source,
            target,
            CreateAutoAttack(),
            replacement,
            CombatRollResult.Miss()
        );

        var refundEvent = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.ResourceChanged &&
                combatEvent.Amount > 0m
        );

        Assert.Equal(12m, refundEvent.Amount);
        Assert.Equal(replacement.Key, refundEvent.AbilityKey);
        Assert.Equal(CombatResultTypes.Miss, refundEvent.ResultKey);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void RefundPercentOutsideZeroToHundred_IsRejected(
        int refundPercent)
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack();
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            refundPercent,
            CombatResultTypes.Miss
        );

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor(CombatRollResult.Hit());

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            processor.QueueNextSwingReplacement(
                context,
                source.Key,
                mainHand.Key,
                replacement
            )
        );
    }

    [Fact]
    public void RefundRuleForResourceWithoutCost_IsRejected()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack();
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "mana",
            80m,
            CombatResultTypes.Miss
        );

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor(CombatRollResult.Hit());

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));

        Assert.Throws<ArgumentException>(() =>
            processor.QueueNextSwingReplacement(
                context,
                source.Key,
                mainHand.Key,
                replacement
            )
        );
    }

    [Fact]
    public void BlankRefundResultKey_IsRejected()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack();
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            80m,
            " "
        );

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor(CombatRollResult.Hit());

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));

        Assert.Throws<ArgumentException>(() =>
            processor.QueueNextSwingReplacement(
                context,
                source.Key,
                mainHand.Key,
                replacement
            )
        );
    }

    [Fact]
    public void DuplicateRefundRulesForSameResource_AreRejected()
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack();
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            80m,
            CombatResultTypes.Miss
        );
        AddRefundRule(
            replacement,
            "RAGE",
            50m,
            CombatResultTypes.Dodge
        );

        var context = CreateContext();
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor(CombatRollResult.Hit());

        Assert.True(processor.Start(
            context,
            source.Key,
            target.Key,
            mainHand
        ));

        Assert.Throws<ArgumentException>(() =>
            processor.QueueNextSwingReplacement(
                context,
                source.Key,
                mainHand.Key,
                replacement
            )
        );
    }

    private static void AssertNoRefundForLandedResult(
        CombatRollResult combatRollResult)
    {
        var source = CreateActor("source");
        AddResource(source, "rage", 100m, 30m);
        var target = CreateActor("target", 1000m);
        var replacement = CreateReplacement();
        AddRefundRule(
            replacement,
            "rage",
            80m,
            CombatResultTypes.Miss,
            CombatResultTypes.Dodge,
            CombatResultTypes.Parry
        );

        Run(
            source,
            target,
            CreateAutoAttack(),
            replacement,
            combatRollResult
        );

        Assert.Equal(15m, source.Resources["rage"].Current);
    }

    private static SimulationRunResult Run(
        SimulationActorState source,
        SimulationActorState target,
        AutoAttackDefinition mainHand,
        NextSwingReplacementDefinition replacement,
        CombatRollResult combatRollResult)
    {
        var context = CreateContext(1.1m);
        context.AddActor(source);
        context.AddActor(target);
        var processor = CreateProcessor(combatRollResult);

        return new SimulationEngine([processor])
            .Run(
                context,
                startedContext =>
                {
                    Assert.True(processor.Start(
                        startedContext,
                        source.Key,
                        target.Key,
                        mainHand
                    ));
                    Assert.True(processor.QueueNextSwingReplacement(
                        startedContext,
                        source.Key,
                        mainHand.Key,
                        replacement
                    ));
                }
            );
    }

    private static AutoAttackProcessor CreateProcessor(
        CombatRollResult combatRollResult)
    {
        return new AutoAttackProcessor(
            new AbilityExecutor(
                new AuraManager(),
                new FixedCombatRollResolver(combatRollResult),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey = "next-swing-refund-tests",
                        Version = "1"
                    }
                )
            )
        );
    }

    private static AutoAttackDefinition CreateAutoAttack()
    {
        return new AutoAttackDefinition
        {
            Key = "main-hand",
            Name = "Main Hand",
            SwingIntervalSeconds = 1m,
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
    }

    private static NextSwingReplacementDefinition CreateReplacement(
        string resourceKey = "rage",
        decimal resourceAmount = 15m,
        bool isPercentOfMaximum = false)
    {
        var replacement =
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
                        MinimumValue = 25m,
                        MaximumValue = 25m,
                        MitigationType = DamageMitigationTypes.None
                    }
            };

        replacement.ResourceCosts.Add(
            new AbilityResourceCost
            {
                ResourceKey = resourceKey,
                Amount = resourceAmount,
                IsPercentOfMaximum = isPercentOfMaximum
            }
        );

        return replacement;
    }

    private static void AddRefundRule(
        NextSwingReplacementDefinition replacement,
        string resourceKey,
        decimal refundPercent,
        params string[] resultKeys)
    {
        replacement.ResourceRefunds.Add(
            new NextSwingResourceRefundDefinition
            {
                ResourceKey = resourceKey,
                RefundPercent = refundPercent,
                ResultKeys = [.. resultKeys]
            }
        );
    }

    private static void AddResource(
        SimulationActorState actor,
        string key,
        decimal maximum,
        decimal current)
    {
        actor.AddResource(
            new ResourceState(
                key,
                maximum,
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

    private static SimulationContext CreateContext(
        decimal durationSeconds = 5m)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = durationSeconds,
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
