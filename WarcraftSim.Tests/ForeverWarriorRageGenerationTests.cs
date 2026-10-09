using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

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
