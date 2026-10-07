using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class RulesetThreatGenerationResolverTests
{
    [Fact]
    public void DamageRule_GeneratesThreatOnEventTarget()
    {
        var context =
            CreateContext();

        var source =
            AddActor(
                context,
                "player",
                "raid"
            );

        var target =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var resolver =
            CreateResolver(
                damageRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            2m
                    }
            );

        var contribution =
            Assert.Single(
                resolver.Resolve(
                    context,
                    new CombatEvent
                    {
                        Type =
                            CombatEventType.Damage,

                        SourceActorKey =
                            source.Key,

                        TargetActorKey =
                            target.Key,

                        Amount =
                            100m
                    }
                )
            );

        Assert.Equal(
            target.Key,
            contribution.ThreatOwnerActorKey
        );

        Assert.Equal(
            source.Key,
            contribution.ThreatSourceActorKey
        );

        Assert.Equal(
            200m,
            contribution.Amount
        );
    }

    [Fact]
    public void DamageRule_CanUseSourceThreatMultiplierStat()
    {
        var context =
            CreateContext();

        var source =
            AddActor(
                context,
                "player",
                "raid"
            );

        var target =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        source.Stats.Set(
            "threat-multiplier",
            1.5m
        );

        var resolver =
            CreateResolver(
                damageRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            1m,

                        SourceMultiplierStatKey =
                            "threat-multiplier"
                    }
            );

        var contribution =
            Assert.Single(
                resolver.Resolve(
                    context,
                    new CombatEvent
                    {
                        Type =
                            CombatEventType.Damage,

                        SourceActorKey =
                            source.Key,

                        TargetActorKey =
                            target.Key,

                        Amount =
                            100m
                    }
                )
            );

        Assert.Equal(
            150m,
            contribution.Amount
        );
    }

    [Fact]
    public void DamageRule_CanApplyAbilitySpecificMultiplier()
    {
        var context =
            CreateContext();

        var source =
            AddActor(
                context,
                "player",
                "raid"
            );

        var target =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var resolver =
            CreateResolver(
                damageRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            1m,

                        AbilityMultipliers =
                            new Dictionary<string, decimal>(
                                StringComparer.OrdinalIgnoreCase
                            )
                            {
                                ["high-threat-strike"] =
                                    3m
                            }
                    }
            );

        var contribution =
            Assert.Single(
                resolver.Resolve(
                    context,
                    new CombatEvent
                    {
                        Type =
                            CombatEventType.Damage,

                        SourceActorKey =
                            source.Key,

                        TargetActorKey =
                            target.Key,

                        AbilityKey =
                            "HIGH-THREAT-STRIKE",

                        Amount =
                            50m
                    }
                )
            );

        Assert.Equal(
            150m,
            contribution.Amount
        );
    }

    [Fact]
    public void DamageRule_CanUseRawAmountInsteadOfEffectiveAmount()
    {
        var context =
            CreateContext();

        var source =
            AddActor(
                context,
                "player",
                "raid"
            );

        var target =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var resolver =
            CreateResolver(
                damageRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            1m,

                        AmountBasis =
                            ThreatAmountBasisTypes.RawAmount
                    }
            );

        var contribution =
            Assert.Single(
                resolver.Resolve(
                    context,
                    new CombatEvent
                    {
                        Type =
                            CombatEventType.Damage,

                        SourceActorKey =
                            source.Key,

                        TargetActorKey =
                            target.Key,

                        RawAmount =
                            200m,

                        Amount =
                            125m
                    }
                )
            );

        Assert.Equal(
            200m,
            contribution.Amount
        );
    }

    [Fact]
    public void HealingRule_CanGenerateFullThreatOnEveryLivingEnemy()
    {
        var context =
            CreateContext();

        var healer =
            AddActor(
                context,
                "healer",
                "raid"
            );

        var ally =
            AddActor(
                context,
                "ally",
                "raid"
            );

        AddActor(
            context,
            "boss-a",
            "enemy"
        );

        AddActor(
            context,
            "boss-b",
            "enemy"
        );

        var resolver =
            CreateResolver(
                healingRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            0.5m,

                        ThreatOwnerSelectionMode =
                            ThreatOwnerSelectionModes.AllEnemiesOfSource,

                        ThreatOwnerDistributionMode =
                            ThreatOwnerDistributionModes.FullAmountPerOwner
                    }
            );

        var contributions =
            resolver.Resolve(
                context,
                new CombatEvent
                {
                    Type =
                        CombatEventType.Healing,

                    SourceActorKey =
                        healer.Key,

                    TargetActorKey =
                        ally.Key,

                    Amount =
                        100m
                }
            );

        Assert.Equal(
            2,
            contributions.Count
        );

        Assert.All(
            contributions,
            contribution =>
                Assert.Equal(
                    50m,
                    contribution.Amount
                )
        );

        Assert.Equal(
            new[]
            {
                "boss-a",
                "boss-b"
            },
            contributions
                .Select(
                    contribution =>
                        contribution.ThreatOwnerActorKey
                )
                .ToArray()
        );
    }

    [Fact]
    public void HealingRule_CanSplitThreatEvenlyAcrossEnemies()
    {
        var context =
            CreateContext();

        var healer =
            AddActor(
                context,
                "healer",
                "raid"
            );

        var ally =
            AddActor(
                context,
                "ally",
                "raid"
            );

        AddActor(
            context,
            "boss-a",
            "enemy"
        );

        AddActor(
            context,
            "boss-b",
            "enemy"
        );

        var resolver =
            CreateResolver(
                healingRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            1m,

                        ThreatOwnerSelectionMode =
                            ThreatOwnerSelectionModes.AllEnemiesOfSource,

                        ThreatOwnerDistributionMode =
                            ThreatOwnerDistributionModes.SplitEvenly
                    }
            );

        var contributions =
            resolver.Resolve(
                context,
                new CombatEvent
                {
                    Type =
                        CombatEventType.Healing,

                    SourceActorKey =
                        healer.Key,

                    TargetActorKey =
                        ally.Key,

                    Amount =
                        100m
                }
            );

        Assert.Equal(
            2,
            contributions.Count
        );

        Assert.All(
            contributions,
            contribution =>
                Assert.Equal(
                    50m,
                    contribution.Amount
                )
        );
    }

    [Fact]
    public void UnconfiguredEventType_GeneratesNoThreat()
    {
        var context =
            CreateContext();

        var source =
            AddActor(
                context,
                "player",
                "raid"
            );

        var target =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var resolver =
            CreateResolver(
                damageRule:
                    null,

                healingRule:
                    null
            );

        var contributions =
            resolver.Resolve(
                context,
                new CombatEvent
                {
                    Type =
                        CombatEventType.Damage,

                    SourceActorKey =
                        source.Key,

                    TargetActorKey =
                        target.Key,

                    Amount =
                        100m
                }
            );

        Assert.Empty(
            contributions
        );
    }

    [Fact]
    public void RulesetResolver_IntegratesWithThreatManager()
    {
        var context =
            CreateContext(
                captureTimeline:
                    true
            );

        var player =
            AddActor(
                context,
                "player",
                "raid"
            );

        var boss =
            AddActor(
                context,
                "boss",
                "enemy"
            );

        var resolver =
            CreateResolver(
                damageRule:
                    new ThreatGenerationRuleDefinition
                    {
                        BaseMultiplier =
                            1.25m
                    }
            );

        var engine =
            new SimulationEngine(
                [
                    new CurrentTimeEventDriver(
                        new CombatEvent
                        {
                            Type =
                                CombatEventType.Damage,

                            SourceActorKey =
                                player.Key,

                            TargetActorKey =
                                boss.Key,

                            AbilityKey =
                                "strike",

                            Amount =
                                80m
                        }
                    ),

                    new ThreatManager(
                        resolver
                    )
                ]
            );

        var result =
            engine.Run(
                context
            );

        Assert.Equal(
            100m,
            boss.ThreatTable.GetThreat(
                player.Key
            )
        );

        var threatEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.ThreatChanged
            );

        Assert.Equal(
            100m,
            threatEvent.Amount
        );
    }

    private static RulesetThreatGenerationResolver CreateResolver(
        ThreatGenerationRuleDefinition? damageRule = null,
        ThreatGenerationRuleDefinition? healingRule = null)
    {
        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "threat-test",

                Version =
                    "1"
            };

        if (damageRule is not null)
        {
            ruleset.ThreatGenerationRules[
                ThreatGenerationEventTypes.Damage
            ] = damageRule;
        }

        if (healingRule is not null)
        {
            ruleset.ThreatGenerationRules[
                ThreatGenerationEventTypes.Healing
            ] = healingRule;
        }

        return new RulesetThreatGenerationResolver(
            ruleset
        );
    }

    private static SimulationContext CreateContext(
        bool captureTimeline = false)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    10m,

                PrimaryActorKey =
                    "player",

                CaptureTimeline =
                    captureTimeline
            }
        );
    }

    private static SimulationActorState AddActor(
        SimulationContext context,
        string key,
        string teamKey)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    teamKey
            };

        actor.InitializeHealth(
            maximumHealth:
                1000m
        );

        context.AddActor(
            actor
        );

        return actor;
    }

    private sealed class CurrentTimeEventDriver :
        ICombatEventProcessor
    {
        private readonly CombatEvent
            _event;

        public CurrentTimeEventDriver(
            CombatEvent combatEvent)
        {
            _event =
                combatEvent;
        }

        public void Process(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            if (
                combatEvent.Type !=
                    CombatEventType.SimulationStarted
            )
            {
                return;
            }

            _event.TimeSeconds =
                context.CurrentTimeSeconds;

            context.EmitEvent(
                _event
            );
        }
    }
}
