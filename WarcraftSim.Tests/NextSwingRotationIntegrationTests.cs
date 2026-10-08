using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;
using WarcraftSim.Data.Forever.Abilities.Warrior;

namespace WarcraftSim.Tests;

public sealed class NextSwingRotationIntegrationTests
{
    [Fact]
    public void RotationEntry_DefaultsToNormalAbilityAction()
    {
        var entry = new RotationEntry();

        Assert.Equal(
            RotationActionTypes.Ability,
            entry.ActionType
        );
    }

    [Fact]
    public void Rotation_QueuesNextSwingReplacementAndConsumesItOnSwing()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);
        var replacement = CreateReplacement("heroic-strike", 25m);

        source.AddNextSwingReplacement(replacement);

        var result = Run(
            source,
            target,
            CreateQueueRotation(mainHand.Key, replacement.Key),
            durationSeconds: 1.1m,
            mainHand
        );

        var damage = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == replacement.Key
        );

        Assert.Equal(25m, damage.Amount);
        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == mainHand.Key
        );
    }

    [Fact]
    public void QueuedAction_DoesNotPreventForegroundAbilityInSameDecision()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);
        var replacement = CreateReplacement("heroic-strike", 25m);
        var foreground = CreateDirectAbility("slam", 5m);

        source.AddNextSwingReplacement(replacement);
        source.AddAbility(foreground);

        var rotation = CreateQueueRotation(mainHand.Key, replacement.Key);
        rotation.Entries.Add(
            new RotationEntry
            {
                ActionType = RotationActionTypes.Ability,
                AbilityKey = foreground.Key,
                Priority = 2
            }
        );

        var result = Run(
            source,
            target,
            rotation,
            durationSeconds: 1.1m,
            mainHand
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.AbilityCastStarted &&
                combatEvent.AbilityKey == foreground.Key &&
                combatEvent.TimeSeconds == 0m
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == replacement.Key
        );
    }

    [Fact]
    public void Rotation_RequeuesReplacementAfterEachMatchingSwing()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);
        var replacement = CreateReplacement("heroic-strike", 25m);

        source.AddNextSwingReplacement(replacement);

        var result = Run(
            source,
            target,
            CreateQueueRotation(mainHand.Key, replacement.Key),
            durationSeconds: 2.1m,
            mainHand
        );

        Assert.Equal(
            2,
            result.Timeline.Count(combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == replacement.Key)
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == mainHand.Key
        );
    }

    [Fact]
    public void Rotation_DoesNotQueueReplacementWhenResourceIsUnavailable()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);
        var replacement = CreateReplacement(
            "heroic-strike",
            25m,
            rageCost: 15m
        );

        source.AddResource(
            new ResourceState(
                "rage",
                100m,
                0m,
                0m
            )
        );
        source.AddNextSwingReplacement(replacement);

        var result = Run(
            source,
            target,
            CreateQueueRotation(mainHand.Key, replacement.Key),
            durationSeconds: 1.1m,
            mainHand
        );

        var whiteSwing = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == mainHand.Key
        );

        Assert.Equal(10m, whiteSwing.Amount);
        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == replacement.Key
        );
    }

    [Fact]
    public void ResourceChange_ReevaluatesQueuedActionAffordability()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);
        var replacement = CreateReplacement(
            "heroic-strike",
            25m,
            rageCost: 15m
        );
        var rageGenerator = CreateRageGenerator("bloodrage-test", 20m);

        source.AddResource(
            new ResourceState(
                "rage",
                100m,
                0m,
                0m
            )
        );
        source.AddNextSwingReplacement(replacement);
        source.AddAbility(rageGenerator);

        var rotation = CreateQueueRotation(mainHand.Key, replacement.Key);
        rotation.Entries.Add(
            new RotationEntry
            {
                AbilityKey = rageGenerator.Key,
                Priority = 2
            }
        );

        var result = Run(
            source,
            target,
            rotation,
            durationSeconds: 1.1m,
            mainHand
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.ResourceChanged &&
                combatEvent.AbilityKey == rageGenerator.Key &&
                combatEvent.Amount == 20m
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == replacement.Key
        );
    }

    [Fact]
    public void QueueAction_RequiresItsSelectedTargetToMatchSwingTarget()
    {
        var source = CreateActor("source");
        var swingTarget = CreateActor("target", 1000m);
        var otherTarget = CreateActor("other-target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);
        var replacement = CreateReplacement("heroic-strike", 25m);

        source.AddNextSwingReplacement(replacement);

        var rotation = CreateQueueRotation(mainHand.Key, replacement.Key);
        rotation.Entries[0].Target =
            new RotationTargetDefinition
            {
                Mode = RotationTargetSelectionModes.Fixed,
                ActorKey = otherTarget.Key
            };

        var result = Run(
            source,
            swingTarget,
            rotation,
            durationSeconds: 1.1m,
            mainHand,
            additionalActors: [otherTarget]
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == mainHand.Key
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == replacement.Key
        );
    }

    [Fact]
    public void ForeverHeroicStrike_CanBeSelectedByRotation()
    {
        var source = CreateActor("source");
        var target = CreateActor("target", 1000m);
        var mainHand = CreateAutoAttack("main-hand", 1m, 10m);

        source.AddResource(
            new ResourceState(
                ForeverHeroicStrikeFactory.RageResourceKey,
                100m,
                100m,
                0m
            )
        );

        var heroicStrike =
            ForeverHeroicStrikeFactory.CreateForLevel(
                characterLevel: 30,
                minimumWeaponDamage: 10m,
                maximumWeaponDamage: 10m,
                weaponSpeedSeconds: 1m,
                attackSkillStatKey: "sword-skill"
            );

        source.AddNextSwingReplacement(heroicStrike);

        var result = Run(
            source,
            target,
            CreateQueueRotation(mainHand.Key, heroicStrike.Key),
            durationSeconds: 1.1m,
            mainHand
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == ForeverHeroicStrikeFactory.AbilityKey
        );

        Assert.Equal(
            85m,
            source.Resources[ForeverHeroicStrikeFactory.RageResourceKey].Current
        );
    }

    [Fact]
    public void Validation_RejectsUnknownRotationActionType()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var rotation = new RotationProfile
        {
            Name = "Invalid Action",
            Entries =
            [
                new RotationEntry
                {
                    ActionType = "invented-action",
                    Priority = 1
                }
            ]
        };

        var exception = RunExpectingValidationFailure(
            source,
            target,
            rotation,
            includeAutoAttackProcessor: true
        );

        Assert.Contains(
            exception.Errors,
            error => error.Contains(
                "unsupported action type",
                StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validation_RejectsUnknownNextSwingReplacement()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var rotation = CreateQueueRotation(
            "main-hand",
            "missing-special"
        );

        var exception = RunExpectingValidationFailure(
            source,
            target,
            rotation,
            includeAutoAttackProcessor: true
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains("missing-special", StringComparison.OrdinalIgnoreCase) &&
                error.Contains("does not have", StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validation_RejectsQueuedActionWithoutAutoAttackProcessor()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var replacement = CreateReplacement("heroic-strike", 25m);
        source.AddNextSwingReplacement(replacement);

        var exception = RunExpectingValidationFailure(
            source,
            target,
            CreateQueueRotation("main-hand", replacement.Key),
            includeAutoAttackProcessor: false
        );

        Assert.Contains(
            exception.Errors,
            error => error.Contains(
                "no auto-attack processor",
                StringComparison.OrdinalIgnoreCase)
        );
    }

    [Fact]
    public void Validation_RejectsInterruptFlagOnQueuedAction()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var replacement = CreateReplacement("heroic-strike", 25m);
        source.AddNextSwingReplacement(replacement);

        var rotation = CreateQueueRotation("main-hand", replacement.Key);
        rotation.Entries[0].InterruptCurrentCast = true;

        var exception = RunExpectingValidationFailure(
            source,
            target,
            rotation,
            includeAutoAttackProcessor: true
        );

        Assert.Contains(
            exception.Errors,
            error => error.Contains(
                "cannot interrupt",
                StringComparison.OrdinalIgnoreCase)
        );
    }

    private static SimulationRunResult Run(
        SimulationActorState source,
        SimulationActorState target,
        RotationProfile rotation,
        decimal durationSeconds,
        AutoAttackDefinition mainHand,
        IReadOnlyCollection<SimulationActorState>? additionalActors = null)
    {
        var context = CreateContext(durationSeconds);
        context.AddActor(source);
        context.AddActor(target);

        if (additionalActors is not null)
        {
            foreach (var actor in additionalActors)
            {
                context.AddActor(actor);
            }
        }

        var auraManager = new AuraManager();
        var abilityExecutor = CreateAbilityExecutor(auraManager);
        var autoAttackProcessor = new AutoAttackProcessor(abilityExecutor);
        var rotationExecutor =
            new PriorityRotationExecutor(
                rotation,
                source.Key,
                target.Key,
                abilityExecutor,
                autoAttackProcessor: autoAttackProcessor
            );

        var engine = new SimulationEngine(
            [
                abilityExecutor,
                autoAttackProcessor,
                rotationExecutor
            ]
        );

        return engine.Run(
            context,
            startedContext =>
                Assert.True(
                    autoAttackProcessor.Start(
                        startedContext,
                        source.Key,
                        target.Key,
                        mainHand
                    )
                )
        );
    }

    private static SimulationDefinitionValidationException
        RunExpectingValidationFailure(
            SimulationActorState source,
            SimulationActorState target,
            RotationProfile rotation,
            bool includeAutoAttackProcessor)
    {
        var context = CreateContext(1m);
        context.AddActor(source);
        context.AddActor(target);

        var auraManager = new AuraManager();
        var abilityExecutor = CreateAbilityExecutor(auraManager);
        var autoAttackProcessor = new AutoAttackProcessor(abilityExecutor);

        var rotationExecutor =
            new PriorityRotationExecutor(
                rotation,
                source.Key,
                target.Key,
                abilityExecutor,
                autoAttackProcessor:
                    includeAutoAttackProcessor
                        ? autoAttackProcessor
                        : null
            );

        var processors =
            new List<ICombatEventProcessor>
            {
                abilityExecutor
            };

        if (includeAutoAttackProcessor)
        {
            processors.Add(
                autoAttackProcessor
            );
        }

        processors.Add(
            rotationExecutor
        );

        return Assert.Throws<SimulationDefinitionValidationException>(() =>
            new SimulationEngine(
                processors
            ).Run(context)
        );
    }

    private static AbilityExecutor CreateAbilityExecutor(
        AuraManager auraManager)
    {
        return new AbilityExecutor(
            auraManager,
            new SimpleCombatRollResolver(
                hitChancePercent: 100m,
                dodgeChancePercent: 0m,
                parryChancePercent: 0m,
                blockChancePercent: 0m,
                criticalChancePercent: 0m
            ),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey = "next-swing-rotation-tests",
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
        );
    }

    private static RotationProfile CreateQueueRotation(
        string autoAttackKey,
        string replacementKey)
    {
        return new RotationProfile
        {
            Name = "Next Swing Rotation",
            Entries =
            [
                new RotationEntry
                {
                    ActionType =
                        RotationActionTypes.QueueNextSwingReplacement,
                    NextSwingReplacementKey = replacementKey,
                    AutoAttackKey = autoAttackKey,
                    Priority = 1
                }
            ]
        };
    }

    private static AutoAttackDefinition CreateAutoAttack(
        string key,
        decimal swingIntervalSeconds,
        decimal damage)
    {
        return new AutoAttackDefinition
        {
            Key = key,
            Name = key,
            SwingIntervalSeconds = swingIntervalSeconds,
            WeaponHandKey = WeaponHandKeys.MainHand,
            DamageEffect =
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    WeaponHandKey = WeaponHandKeys.MainHand,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = damage,
                    MaximumValue = damage,
                    MitigationType = DamageMitigationTypes.None
                }
        };
    }

    private static NextSwingReplacementDefinition CreateReplacement(
        string key,
        decimal damage,
        decimal rageCost = 0m)
    {
        var replacement = new NextSwingReplacementDefinition
        {
            Key = key,
            Name = key,
            WeaponHandKey = WeaponHandKeys.MainHand,
            DamageEffect =
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    WeaponHandKey = WeaponHandKeys.MainHand,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = damage,
                    MaximumValue = damage,
                    MitigationType = DamageMitigationTypes.None
                }
        };

        if (rageCost > 0m)
        {
            replacement.ResourceCosts.Add(
                new AbilityResourceCost
                {
                    ResourceKey = "rage",
                    Amount = rageCost
                }
            );
        }

        return replacement;
    }

    private static AbilityDefinition CreateDirectAbility(
        string key,
        decimal damage)
    {
        return new AbilityDefinition
        {
            Key = key,
            Name = key,
            GlobalCooldownSeconds = 1.5m,
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = damage,
                    MaximumValue = damage,
                    MitigationType = DamageMitigationTypes.None
                }
            ]
        };
    }

    private static AbilityDefinition CreateRageGenerator(
        string key,
        decimal amount)
    {
        return new AbilityDefinition
        {
            Key = key,
            Name = key,
            GlobalCooldownSeconds = 1.5m,
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = "rage",
                    EffectType = AbilityEffectTypes.ResourceChange,
                    TargetType = AbilityTargetTypes.Self,
                    ResourceKey = "rage",
                    ResourceChangeOperation = ResourceChangeOperationTypes.Gain,
                    MinimumValue = amount,
                    MaximumValue = amount
                }
            ]
        };
    }

    private static SimulationActorState CreateActor(
        string key,
        decimal health = 100m)
    {
        var actor = new SimulationActorState
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
        decimal durationSeconds)
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
}
