// Consolidated tests for Forever Abilities.
// Original class names are retained for xUnit test filtering.

// ==================== ForeverBloodrageTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;

public sealed class ForeverBloodrageTests
{
    [Fact]
    public void Factory_UsesForeverClientValues()
    {
        var bloodrage = ForeverBloodrageFactory.Create(baseHealth: 500m);

        Assert.Equal(ForeverBloodrageFactory.AbilityKey, bloodrage.Key);
        Assert.Equal("Bloodrage", bloodrage.Name);
        Assert.Equal(ForeverCharacterClassKeys.Warrior, bloodrage.ClassKey);
        Assert.Equal(10, bloodrage.RequiredLevel);
        Assert.Equal(60m, bloodrage.CooldownSeconds);
        Assert.True(bloodrage.IsOffGlobalCooldown);
        Assert.Equal(0m, bloodrage.GlobalCooldownSeconds);
        Assert.Empty(bloodrage.ResourceCosts);

        Assert.Equal(3, bloodrage.Effects.Count);

        var healthCost = Assert.Single(
            bloodrage.Effects,
            effect => effect.Key == "health-cost"
        );
        Assert.Equal(AbilityEffectTypes.HealthChange, healthCost.EffectType);
        Assert.Equal(HealthChangeOperationTypes.Damage, healthCost.HealthChangeOperation);
        Assert.Equal(100m, healthCost.MinimumValue);
        Assert.Equal(100m, healthCost.MaximumValue);

        var immediate = Assert.Single(
            bloodrage.Effects,
            effect => effect.Key == "immediate-rage"
        );
        Assert.Equal(10m, immediate.MinimumValue);
        Assert.Equal("rage", immediate.ResourceKey);

        var periodic = Assert.Single(
            bloodrage.Effects,
            effect => effect.Key == "periodic-rage"
        );
        Assert.Equal(AbilityEffectTypes.PeriodicResourceChange, periodic.EffectType);
        Assert.Equal(1m, periodic.MinimumValue);
        Assert.Equal(10m, periodic.DurationSeconds);
        Assert.Equal(1m, periodic.TickIntervalSeconds);
        Assert.True(periodic.IncludeExpirationBoundaryTick);
    }

    [Fact]
    public void Activation_SpendsTwentyPercentBaseHealthAndGrantsImmediateRage()
    {
        var warrior = CreateWarrior(startingRage: 0m);
        warrior.AddAbility(ForeverBloodrageFactory.Create(baseHealth: 500m));

        var result = RunBloodrage(warrior, durationSeconds: 0.1m);

        Assert.Equal(900m, warrior.CurrentHealth);
        Assert.Equal(10m, warrior.Resources["rage"].Current);

        var healthEvent = Assert.Single(
            result.Timeline,
            e => e.Type == CombatEventType.HealthChanged
        );
        Assert.Equal(-100m, healthEvent.Amount);
        Assert.DoesNotContain(
            result.Timeline,
            e => e.Type == CombatEventType.Damage &&
                 e.AbilityKey == ForeverBloodrageFactory.AbilityKey
        );
    }

    [Fact]
    public void FullDuration_GrantsTenImmediateAndTenPeriodicRage()
    {
        var warrior = CreateWarrior(startingRage: 0m);
        warrior.AddAbility(ForeverBloodrageFactory.Create(baseHealth: 500m));

        var result = RunBloodrage(warrior, durationSeconds: 10.1m);

        Assert.Equal(20m, warrior.Resources["rage"].Current);

        var periodicGains = result.Timeline
            .Where(e =>
                e.Type == CombatEventType.ResourceChanged &&
                e.AbilityKey == ForeverBloodrageFactory.AbilityKey &&
                e.EffectKey == "periodic-rage")
            .ToList();

        Assert.Equal(10, periodicGains.Count);
        Assert.All(periodicGains, e => Assert.Equal(1m, e.Amount));
    }

    [Fact]
    public void ImprovedBloodrageMultiplier_AppliesToImmediateAndPeriodicRage()
    {
        var warrior = CreateWarrior(startingRage: 0m);
        warrior.AddAbility(
            ForeverBloodrageFactory.Create(
                baseHealth: 500m,
                rageGenerationMultiplier: 1.5m
            )
        );

        RunBloodrage(warrior, durationSeconds: 10.1m);

        Assert.Equal(30m, warrior.Resources["rage"].Current);
    }

    [Fact]
    public void RageGeneration_IsClampedToResourceMaximumAndReportsActualGain()
    {
        var warrior = CreateWarrior(startingRage: 95m);
        warrior.AddAbility(ForeverBloodrageFactory.Create(baseHealth: 500m));

        var result = RunBloodrage(warrior, durationSeconds: 10.1m);

        Assert.Equal(100m, warrior.Resources["rage"].Current);

        var totalReportedGain = result.Timeline
            .Where(e =>
                e.Type == CombatEventType.ResourceChanged &&
                e.AbilityKey == ForeverBloodrageFactory.AbilityKey)
            .Sum(e => e.Amount ?? 0m);

        Assert.Equal(5m, totalReportedGain);
    }

    [Fact]
    public void InvalidFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverBloodrageFactory.Create(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverBloodrageFactory.Create(500m, -0.1m));
    }

    private static SimulationRunResult RunBloodrage(
        SimulationActorState warrior,
        decimal durationSeconds)
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = durationSeconds,
            CaptureTimeline = true,
            PrimaryActorKey = warrior.Key
        });
        context.AddActor(warrior);

        var executor = new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition
            {
                RulesetKey = "bloodrage-tests",
                Version = "1"
            })
        );

        return new SimulationEngine([executor]).Run(
            context,
            started =>
            {
                var use = executor.TryStartAbility(
                    started,
                    warrior.Key,
                    warrior.Key,
                    ForeverBloodrageFactory.AbilityKey
                );
                Assert.True(use.Success, use.FailureReason ?? "Ability use failed.");
            }
        );
    }

    private static SimulationActorState CreateWarrior(decimal startingRage)
    {
        var warrior = new SimulationActorState
        {
            Key = "warrior",
            Name = "Warrior",
            Level = 30
        };
        warrior.InitializeHealth(1000m);
        warrior.AddResource(new ResourceState("rage", 100m, startingRage));
        return warrior;
    }
}
}

// ==================== ForeverBloodthirstTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverBloodthirstTests
{
    [Theory]
    [InlineData(1, 23881, 40, 30)]
    [InlineData(2, 23892, 48, 37)]
    [InlineData(3, 23893, 54, 43)]
    [InlineData(4, 23894, 60, 48)]
    public void RankTable_UsesForeverFlatDamageValues(
        int rank,
        int spellId,
        int requiredLevel,
        decimal flatDamage)
    {
        var definition = ForeverBloodthirstRanks.GetByRank(rank);

        Assert.Equal(rank, definition.Rank);
        Assert.Equal(spellId, definition.SpellId);
        Assert.Equal(requiredLevel, definition.RequiredLevel);
        Assert.Equal(flatDamage, definition.FlatDamage);
        Assert.Equal(30m, definition.RageCost);
        Assert.Equal(6m, definition.CooldownSeconds);
        Assert.Equal(1.5m, definition.GlobalCooldownSeconds);
        Assert.Equal(10m, definition.MovementSpeedDurationSeconds);
    }

    [Theory]
    [InlineData(40, 1)]
    [InlineData(47, 1)]
    [InlineData(48, 2)]
    [InlineData(60, 4)]
    public void HighestAvailableRank_UsesCharacterLevel(
        int characterLevel,
        int expectedRank)
    {
        Assert.Equal(
            expectedRank,
            ForeverBloodthirstRanks.GetHighestAvailable(characterLevel).Rank
        );
    }

    [Fact]
    public void LevelSixtyFactory_UsesCurrentFortyFivePercentAttackPowerRatio()
    {
        var ability = ForeverBloodthirstFactory.CreateForLevel(60, "sword-skill");

        Assert.Equal(ForeverBloodthirstFactory.AbilityKey, ability.Key);
        Assert.Equal("Bloodthirst (Rank 4)", ability.Name);
        Assert.Equal(ForeverCharacterClassKeys.Warrior, ability.ClassKey);
        Assert.Equal(60, ability.RequiredLevel);
        Assert.Equal(6m, ability.CooldownSeconds);
        Assert.Equal(30m, Assert.Single(ability.ResourceCosts).Amount);

        var damage = Assert.Single(ability.Effects, effect => effect.Key == "damage");
        Assert.Equal(48m, damage.MinimumValue);
        Assert.Equal(48m, damage.MaximumValue);
        Assert.Equal(ForeverCombatStatKeys.AttackPower, damage.ScalingStatKey);
        Assert.Equal(0.45m, damage.ScalingCoefficient);
        Assert.Null(damage.WeaponHandKey);
        Assert.Equal(ForeverCombatResolutionTypes.PlayerMeleeSpecial, damage.ResolutionType);

        var movement = Assert.Single(ability.Effects, effect => effect.Key == "movement-speed");
        Assert.Equal(ForeverWarriorAuraKeys.BloodthirstMovementSpeed, movement.AuraKey);
        Assert.Equal(10m, movement.DurationSeconds);
        Assert.Equal(AbilityTargetTypes.Self, movement.TargetType);
    }

    [Fact]
    public void RageCostReduction_ChangesCostWithoutChangingDamageRatio()
    {
        var ability = ForeverBloodthirstFactory.CreateForLevel(
            60,
            "skill",
            rageCostReduction: 3m
        );

        Assert.Equal(27m, Assert.Single(ability.ResourceCosts).Amount);
        Assert.Equal(
            0.45m,
            Assert.Single(ability.Effects, effect => effect.Key == "damage").ScalingCoefficient
        );
    }

    [Fact]
    public void LevelSixtyExecution_DealsFlatPlusFortyFivePercentAttackPower()
    {
        var source = CreateActor("warrior", "raid", 100m);
        var target = CreateActor("boss", "enemy", 0m);
        source.Stats.Set(ForeverCombatStatKeys.AttackPower, 1000m);
        source.AddAbility(ForeverBloodthirstFactory.CreateForLevel(60, "skill"));

        var context = new SimulationContext(
            new SimulationRunOptions
            {
                DurationSeconds = 0.1m,
                CaptureTimeline = true,
                PrimaryActorKey = source.Key,
                Seed = 1
            }
        );
        context.AddActor(source);
        context.AddActor(target);
        var executor = CreateExecutor();

        var result = new SimulationEngine([executor]).Run(
            context,
            started =>
            {
                var use = executor.TryStartAbility(
                    started,
                    source.Key,
                    target.Key,
                    ForeverBloodthirstFactory.AbilityKey
                );
                Assert.True(use.Success, use.FailureReason ?? "Ability use failed.");
            }
        );

        var damage = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == ForeverBloodthirstFactory.AbilityKey
        );
        Assert.Equal(498m, damage.RawAmount);
        Assert.Equal(70m, source.Resources["rage"].Current);
        Assert.Contains(
            source.ActiveAuras,
            aura => aura.Definition.Key == ForeverWarriorAuraKeys.BloodthirstMovementSpeed
        );
    }

    [Fact]
    public void UnsupportedFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverBloodthirstFactory.CreateForLevel(39, "skill"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverBloodthirstFactory.CreateForLevel(61, "skill"));
        Assert.Throws<ArgumentException>(() =>
            ForeverBloodthirstFactory.CreateForLevel(60, ""));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverBloodthirstFactory.CreateForLevel(60, "skill", -1m));
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        decimal rage)
    {
        var actor = new SimulationActorState
        {
            Key = key,
            Name = key,
            TeamKey = teamKey,
            Level = 60
        };
        actor.InitializeHealth(10000m);
        actor.AddResource(new ResourceState("rage", 100m, rage));
        return actor;
    }

    private static AbilityExecutor CreateExecutor()
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(100m, 0m),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey = "bloodthirst-tests",
                    Version = "1",
                    MitigationRules =
                    {
                        [DamageMitigationTypes.Armor] = new DamageMitigationRuleDefinition
                        {
                            MitigationType = DamageMitigationTypes.Armor,
                            FormulaType = DamageMitigationFormulaTypes.RationalLevelScaled,
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
}
}

// ==================== ForeverCleaveTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverCleaveTests
{
    [Theory]
    [InlineData(1, 845, 20, 5)]
    [InlineData(2, 7369, 30, 10)]
    [InlineData(3, 11608, 40, 18)]
    [InlineData(4, 11609, 50, 32)]
    [InlineData(5, 20569, 60, 50)]
    public void RankTable_UsesForeverClientValues(
        int rank,
        int spellId,
        int requiredLevel,
        decimal bonusWeaponDamage)
    {
        var definition =
            ForeverCleaveRanks.GetByRank(rank);

        Assert.Equal(rank, definition.Rank);
        Assert.Equal(spellId, definition.SpellId);
        Assert.Equal(requiredLevel, definition.RequiredLevel);
        Assert.Equal(bonusWeaponDamage, definition.BonusWeaponDamage);
        Assert.Equal(20m, definition.RageCost);
    }

    [Theory]
    [InlineData(20, 1)]
    [InlineData(29, 1)]
    [InlineData(30, 2)]
    [InlineData(60, 5)]
    public void HighestAvailableRank_UsesCharacterLevel(
        int characterLevel,
        int expectedRank)
    {
        var rank =
            ForeverCleaveRanks.GetHighestAvailable(characterLevel);

        Assert.Equal(expectedRank, rank.Rank);
    }

    [Fact]
    public void LevelThirtyFactory_CreatesRankTwoMainHandTwoTargetSpecial()
    {
        var cleave =
            ForeverCleaveFactory.CreateForLevel(
                characterLevel: 30,
                minimumWeaponDamage: 100m,
                maximumWeaponDamage: 120m,
                weaponSpeedSeconds: 2.8m,
                attackSkillStatKey: "sword-skill"
            );

        Assert.Equal(ForeverCleaveFactory.AbilityKey, cleave.Key);
        Assert.Equal("Cleave (Rank 2)", cleave.Name);
        Assert.Equal(WeaponHandKeys.MainHand, cleave.WeaponHandKey);
        Assert.Equal(1, cleave.MaximumAdditionalTargets);

        var effect = cleave.DamageEffect;

        Assert.Equal(WeaponHandKeys.MainHand, effect.WeaponHandKey);
        Assert.Equal(110m, effect.MinimumValue);
        Assert.Equal(130m, effect.MaximumValue);
        Assert.Equal(ForeverCombatStatKeys.AttackPower, effect.ScalingStatKey);
        Assert.Equal(0.2m, effect.ScalingCoefficient);
        Assert.Equal(
            ForeverCombatResolutionTypes.PlayerMeleeSpecial,
            effect.ResolutionType
        );
        Assert.Equal("sword-skill", effect.AttackSkillStatKey);
        Assert.True(effect.CanMiss);
        Assert.True(effect.CanBeDodged);
        Assert.True(effect.CanBeParried);
        Assert.True(effect.CanBeBlocked);
        Assert.True(effect.CanCrit);
        Assert.False(effect.CanGlance);
        Assert.False(effect.CanCrush);
        Assert.Equal(DamageMitigationTypes.Armor, effect.MitigationType);

        var cost = Assert.Single(cleave.ResourceCosts);

        Assert.Equal(ForeverCleaveFactory.RageResourceKey, cost.ResourceKey);
        Assert.Equal(20m, cost.Amount);
        Assert.False(cost.IsPercentOfMaximum);
        Assert.Empty(cleave.ResourceRefunds);
    }

    [Fact]
    public void RageCostReduction_ChangesOnlyTheConfiguredCost()
    {
        var cleave =
            ForeverCleaveFactory.CreateForLevel(
                characterLevel: 30,
                minimumWeaponDamage: 100m,
                maximumWeaponDamage: 100m,
                weaponSpeedSeconds: 2.8m,
                attackSkillStatKey: "sword-skill",
                rageCostReduction: 3m
            );

        var cost = Assert.Single(cleave.ResourceCosts);

        Assert.Equal(17m, cost.Amount);
        Assert.Equal(110m, cleave.DamageEffect.MinimumValue);
        Assert.Equal(1, cleave.MaximumAdditionalTargets);
    }

    [Fact]
    public void UnsupportedFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverCleaveFactory.CreateForLevel(
                19,
                100m,
                100m,
                2.8m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverCleaveFactory.CreateForLevel(
                61,
                100m,
                100m,
                2.8m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverCleaveFactory.CreateForLevel(
                30,
                100m,
                100m,
                0m,
                "sword-skill"
            )
        );

        Assert.Throws<ArgumentException>(() =>
            ForeverCleaveFactory.CreateForLevel(
                30,
                100m,
                100m,
                2.8m,
                ""
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverCleaveFactory.CreateForLevel(
                30,
                100m,
                100m,
                2.8m,
                "sword-skill",
                -1m
            )
        );
    }
}
}

// ==================== ForeverDeepWoundsTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverDeepWoundsTests
{
    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 40)]
    [InlineData(3, 60)]
    public void EachRank_UsesBaseWeaponAverageAndDealsFullTwelveSecondPool(
        int rank, int expectedDamage)
    {
        var source = CreateWarrior("warrior", rank);
        var target = CreateTarget("target");
        var (result, _) = Run(source, [target], 12.1m,
            started => EmitCritical(started, source.Key, target.Key));

        var ticks = DeepWoundsTicks(result);
        Assert.Equal(6, ticks.Count);
        Assert.Equal((decimal)expectedDamage, ticks.Sum(tick => tick.RawAmount ?? 0m));
        Assert.Equal(new[] { 2m, 4m, 6m, 8m, 10m, 12m },
            ticks.Select(tick => tick.TimeSeconds).ToArray());
        Assert.All(ticks, tick =>
        {
            Assert.True(tick.IsPeriodic);
            Assert.False(tick.IsCritical);
        });
    }

    [Fact]
    public void SnapshotIgnoresAttackPower_AndSupportsDistinctOffHandWeaponDamage()
    {
        var source = CreateWarrior("warrior", 3, offHandAverageDamage: 50m);
        source.Stats.Set(ForeverCombatStatKeys.AttackPower, 10000m);
        var main = CreateTarget("main");
        var off = CreateTarget("off");

        var (result, _) = Run(source, [main, off], 12.1m, started =>
        {
            EmitCritical(started, source.Key, main.Key, WeaponHandKeys.MainHand);
            EmitCritical(started, source.Key, off.Key, WeaponHandKeys.OffHand,
                ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto);
        });

        Assert.Equal(60m, DeepWoundsTicks(result)
            .Where(tick => tick.TargetActorKey == main.Key)
            .Sum(tick => tick.RawAmount ?? 0m));
        Assert.Equal(30m, DeepWoundsTicks(result)
            .Where(tick => tick.TargetActorKey == off.Key)
            .Sum(tick => tick.RawAmount ?? 0m));
    }

    [Fact]
    public void Reapplication_CarriesUndeliveredDamageAndInvalidatesOldTickSchedule()
    {
        var source = CreateWarrior("warrior", 3);
        var target = CreateTarget("target");

        var (result, _) = Run(source, [target], 16m, started =>
        {
            EmitCritical(started, source.Key, target.Key);
            ScheduleCritical(started, source.Key, target.Key, 3m);
        });

        var ticks = DeepWoundsTicks(result);
        Assert.Equal(new[] { 2m, 5m, 7m, 9m, 11m, 13m, 15m },
            ticks.Select(tick => tick.TimeSeconds).ToArray());
        // First tick was 10. Remaining 50 rolls into the next 60,
        // giving 110 spread across the six refreshed ticks.
        Assert.Equal(10m, ticks[0].RawAmount!.Value);
        Assert.Equal(120m, ticks.Sum(tick => tick.RawAmount ?? 0m));
        Assert.Single(result.Timeline, combatEvent =>
            combatEvent.Type == CombatEventType.AuraRemoved &&
            combatEvent.AbilityKey == ForeverDeepWoundsFactory.AbilityKey);
    }

    [Fact]
    public void FractionalDamagePool_ConservesDamageAcrossRollingRefresh()
    {
        var source = new SimulationActorState
        {
            Key = "warrior", Name = "warrior", TeamKey = "raid", Level = 60
        };
        source.InitializeHealth(1000000m);
        source.AddCriticalStrikeRollingDamageProc(
            ForeverDeepWoundsFactory.Create(1, 101m, 101m));
        var target = CreateTarget("target");

        var (result, _) = Run(source, [target], 16m, started =>
        {
            EmitCritical(started, source.Key, target.Key);
            ScheduleCritical(started, source.Key, target.Key, 3m);
        });

        var ticks = DeepWoundsTicks(result);
        Assert.Equal(7, ticks.Count);
        // Two 20.2-damage procs, including the fraction carried on refresh.
        Assert.Equal(40.4m, ticks.Sum(tick => tick.RawAmount ?? 0m));
    }

    [Fact]
    public void TargetsAndSources_HaveIndependentRollingPools()
    {
        var first = CreateWarrior("warrior-1", 3);
        var second = CreateWarrior("warrior-2", 1);
        var targetOne = CreateTarget("target-1");
        var targetTwo = CreateTarget("target-2");
        var (result, _) = Run(first, [targetOne, targetTwo, second], 12.1m, started =>
        {
            EmitCritical(started, first.Key, targetOne.Key);
            EmitCritical(started, first.Key, targetTwo.Key);
            EmitCritical(started, second.Key, targetOne.Key);
        });

        Assert.Equal(60m, DeepWoundsTicks(result)
            .Where(e => e.SourceActorKey == first.Key && e.TargetActorKey == targetOne.Key)
            .Sum(e => e.RawAmount ?? 0m));
        Assert.Equal(60m, DeepWoundsTicks(result)
            .Where(e => e.SourceActorKey == first.Key && e.TargetActorKey == targetTwo.Key)
            .Sum(e => e.RawAmount ?? 0m));
        Assert.Equal(20m, DeepWoundsTicks(result)
            .Where(e => e.SourceActorKey == second.Key && e.TargetActorKey == targetOne.Key)
            .Sum(e => e.RawAmount ?? 0m));
    }

    [Theory]
    [InlineData(false, "forever-player-melee-special", "main-hand", false)]
    [InlineData(true, "forever-player-spell", "main-hand", false)]
    [InlineData(true, "forever-player-melee-special", "unknown", false)]
    [InlineData(true, "forever-player-melee-special", "main-hand", true)]
    public void OnlyEligibleDirectWeaponCriticalStrikesTrigger(
        bool isCritical, string resolution, string hand, bool isPeriodic)
    {
        var source = CreateWarrior("warrior", 3);
        var target = CreateTarget("target");
        var (result, _) = Run(source, [target], 12.1m, started =>
            EmitCritical(started, source.Key, target.Key, hand, resolution,
                isCritical, isPeriodic));
        Assert.Empty(DeepWoundsTicks(result));
    }

    [Fact]
    public void PassiveAbility_CannotBeCastDirectly()
    {
        var source = CreateWarrior("warrior", 3);
        var target = CreateTarget("target");
        var context = CreateContext(source, [target], 3m);
        var executor = CreateExecutor(new AuraManager());

        var result = executor.TryStartAbility(context, source.Key, target.Key,
            ForeverDeepWoundsFactory.AbilityKey);
        Assert.False(result.Success);
        Assert.Contains("Passive", result.FailureReason ?? "");
    }

    [Fact]
    public void MissingCriticalProcProcessor_IsRejectedAtSimulationValidation()
    {
        var source = CreateWarrior("warrior", 3);
        var target = CreateTarget("target");
        var auraManager = new AuraManager();
        var context = CreateContext(source, [target], 3m);

        var error = Assert.Throws<WarcraftSim.Core.Simulation.Validation.SimulationDefinitionValidationException>(
            () => new SimulationEngine([auraManager, CreateExecutor(auraManager)])
                .Run(context));
        Assert.Contains("CriticalStrikeRollingDamageProcessor", error.Message);
    }

    [Fact]
    public void NoTalentProc_NoDeepWoundsEvenWhenAttackIsCritical()
    {
        var source = new SimulationActorState
        {
            Key = "warrior", Name = "warrior", TeamKey = "raid", Level = 60
        };
        source.InitializeHealth(1000000m);
        var target = CreateTarget("target");
        var (result, _) = Run(source, [target], 12.1m,
            started => EmitCritical(started, source.Key, target.Key));
        Assert.Empty(DeepWoundsTicks(result));
    }

    [Fact]
    public void AutoAttackCritical_TriggersProcThroughRealDamageEvent()
    {
        var source = CreateWarrior("warrior", 3);
        var target = CreateTarget("target");
        var auraManager = new AuraManager();
        var executor = new AbilityExecutor(auraManager,
            new SimpleCombatRollResolver(criticalChancePercent: 100m,
                useSingleRollTable: true), new UnmitigatedDamageResolver());
        var auto = new AutoAttackProcessor(executor);
        var proc = new CriticalStrikeRollingDamageProcessor(auraManager);
        var context = CreateContext(source, [target], 2.2m);

        var definition = ForeverWarriorAutoAttackFactory.CreateTwoHandedMainHand(
            "main-hand-auto", "Main-Hand Auto", 100m, 100m, 3m, "weapon-skill");
        var result = new SimulationEngine([auraManager, executor, auto, proc]).Run(
            context, started => Assert.True(auto.Start(
                started, source.Key, target.Key, definition,
                firstSwingDelaySeconds: 0.1m)));

        Assert.Contains(result.Timeline, combatEvent =>
            combatEvent.Type == CombatEventType.Damage &&
            combatEvent.AbilityKey == "main-hand-auto" &&
            combatEvent.IsCritical &&
            combatEvent.WeaponHandKey == WeaponHandKeys.MainHand);
        var tick = Assert.Single(DeepWoundsTicks(result));
        Assert.Equal(2.1m, tick.TimeSeconds);
        Assert.Equal(10m, tick.RawAmount!.Value);
    }

    [Fact]
    public void InvalidTalentRankAndIncompleteOffHandRangesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverDeepWoundsFactory.Create(0, 100m, 100m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverDeepWoundsFactory.Create(4, 100m, 100m));
        Assert.Throws<ArgumentException>(() =>
            ForeverDeepWoundsFactory.Create(3, 100m, 100m, 50m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverDeepWoundsFactory.Create(3, 100m, 100m,
                tickIntervalSeconds: 5m));
    }

    private static SimulationActorState CreateWarrior(
        string key, int rank, decimal? offHandAverageDamage = null)
    {
        var actor = new SimulationActorState
        {
            Key = key, Name = key, TeamKey = "raid", Level = 60
        };
        actor.InitializeHealth(1000000m);
        actor.AddResource(new ResourceState("rage", 100m, 0m));
        actor.AddCriticalStrikeRollingDamageProc(
            ForeverDeepWoundsFactory.Create(
                rank, 100m, 100m, offHandAverageDamage, offHandAverageDamage));
        return actor;
    }

    private static SimulationActorState CreateTarget(string key)
    {
        var actor = new SimulationActorState
        {
            Key = key, Name = key, TeamKey = "enemy", Level = 60
        };
        actor.InitializeHealth(1000000m);
        return actor;
    }

    private static SimulationContext CreateContext(
        SimulationActorState source,
        IEnumerable<SimulationActorState> otherActors,
        decimal duration)
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = duration,
            CaptureTimeline = true,
            PrimaryActorKey = source.Key,
            Seed = 12
        });
        context.AddActor(source);
        foreach (var other in otherActors)
            context.AddActor(other);
        return context;
    }

    private static (SimulationRunResult Result, SimulationContext Context) Run(
        SimulationActorState source,
        IEnumerable<SimulationActorState> otherActors,
        decimal duration,
        Action<SimulationContext> onStart)
    {
        var auraManager = new AuraManager();
        var executor = CreateExecutor(auraManager);
        var proc = new CriticalStrikeRollingDamageProcessor(auraManager);
        var context = CreateContext(source, otherActors, duration);
        var result = new SimulationEngine([auraManager, executor, proc])
            .Run(context, onStart);
        return (result, context);
    }

    private static AbilityExecutor CreateExecutor(AuraManager auraManager) =>
        new(auraManager, new SimpleCombatRollResolver(),
            new UnmitigatedDamageResolver());

    private static List<CombatEvent> DeepWoundsTicks(SimulationRunResult result) =>
        result.Timeline.Where(e =>
            e.Type == CombatEventType.Damage &&
            e.AbilityKey == ForeverDeepWoundsFactory.AbilityKey)
            .ToList();

    private static void EmitCritical(
        SimulationContext context, string source, string target,
        string hand = WeaponHandKeys.MainHand,
        string resolution = ForeverCombatResolutionTypes.PlayerMeleeSpecial,
        bool isCritical = true, bool isPeriodic = false)
    {
        context.EmitEvent(CreateCriticalEvent(context.CurrentTimeSeconds,
            source, target, hand, resolution, isCritical, isPeriodic));
    }

    private static void ScheduleCritical(
        SimulationContext context, string source, string target, decimal when)
    {
        context.ScheduleEvent(CreateCriticalEvent(when, source, target,
            WeaponHandKeys.MainHand,
            ForeverCombatResolutionTypes.PlayerMeleeSpecial, true, false));
    }

    private static CombatEvent CreateCriticalEvent(
        decimal when, string source, string target,
        string hand, string resolution, bool isCritical, bool isPeriodic) =>
        new()
        {
            Type = CombatEventType.Damage,
            TimeSeconds = when,
            SourceActorKey = source,
            TargetActorKey = target,
            AbilityKey = "critical-strike-test",
            EffectKey = "damage",
            IsCritical = isCritical,
            WeaponHandKey = hand,
            ResolutionType = resolution,
            Amount = 100m,
            EffectDeliveryType = isPeriodic
                ? CombatEffectDeliveryType.Periodic
                : CombatEffectDeliveryType.Direct
        };

    private sealed class UnmitigatedDamageResolver : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(
            SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability,
            AbilityEffectDefinition effect, decimal rawAmount) =>
            DamageMitigationResult.Unmitigated(rawAmount);
    }
}
}

// ==================== ForeverEquipmentCatalogTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Combat;

public sealed class ForeverEquipmentCatalogTests
{
    private static WarriorSimRequest Request(params WarriorGearEntry[] items) => new()
    {
        Preset = "fury",
        EquipmentMode = "catalog",
        Gear = [.. items],
        Level = 60,
        Iterations = 1,
        DurationSeconds = 8m,
        ExecutePhaseDurationSeconds = 0m,
        TargetLevel = 60,
        DeepWoundsRank = 0,
        ImpaleRank = 0,
        UseBloodrage = false
    };

    private static WarriorGearEntry Gear(string slot, int id, int? enchant = null) =>
        new() { Slot = slot, ItemId = id, EnchantSpellId = enchant };

    [Fact]
    public void Catalog_HasVersionedServerOwnedItemsAndProvenance()
    {
        var document = ForeverEquipmentCatalog.GetCatalog();
        Assert.NotEmpty(document.CatalogVersion);
        Assert.True(document.Items.Count >= 10);
        Assert.All(document.Items, item => Assert.True(
            item.SourceUrl.StartsWith("https://www.wowhead.com/forever/",StringComparison.Ordinal) ||
            item.SourceUrl.StartsWith("https://theforeverera.com/en/items/",StringComparison.Ordinal),
            $"Unknown item provenance provider for {item.Id}: {item.SourceUrl}"));
        Assert.Equal("Lionheart Helm", ForeverEquipmentCatalog.FindItem(12640).Name);
    }

    [Fact]
    public void CatalogWeapon_ReplacesManualDamageAndSpeed_WithoutMutatingIncomingSetup()
    {
        var request = Request(Gear("main-hand", 279259));
        request.MainHandMinimumDamage = 900m;
        request.MainHandMaximumDamage = 1000m;
        request.MainHandSpeedSeconds = 5m;
        request.IsTwoHanded = true;
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(request);
        Assert.Equal(79m, resolved.MainHandMinimumDamage);
        Assert.Equal(148m, resolved.MainHandMaximumDamage);
        Assert.Equal(2.4m, resolved.MainHandSpeedSeconds);
        Assert.False(resolved.IsTwoHanded);
        Assert.False(resolved.DualWield);
        Assert.Equal(900m, request.MainHandMinimumDamage);
        Assert.Equal("catalog", request.EquipmentMode);
        Assert.Equal("gear-bonuses", resolved.EquipmentMode);
    }

    [Fact]
    public void TwoHandedWeapon_TakesRealDamageAndCannotEquipAnOffHand()
    {
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(Request(Gear("main-hand", 279260)));
        Assert.True(resolved.IsTwoHanded);
        Assert.Equal(162m, resolved.MainHandMinimumDamage);
        Assert.Equal(244m, resolved.MainHandMaximumDamage);
        Assert.Equal(3.3m, resolved.MainHandSpeedSeconds);
        Assert.Throws<ArgumentException>(() => ForeverEquipmentCatalog.ResolveWarriorLoadout(
            Request(Gear("main-hand", 279260), Gear("off-hand", 19168))));
    }

    [Fact]
    public void DifferentRealOneHanders_EnableDualWieldAndUseOwnWeaponDamage()
    {
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(
            Request(Gear("main-hand", 279259), Gear("off-hand", 19168)));
        Assert.True(resolved.DualWield);
        Assert.False(resolved.IsTwoHanded);
        Assert.Equal(65m, resolved.OffHandMinimumDamage);
        Assert.Equal(121m, resolved.OffHandMaximumDamage);
        Assert.Equal(1.8m, resolved.OffHandSpeedSeconds);
    }

    [Fact]
    public void SuperiorStriking_AddsFiveToDamageRangeWithoutAddingAttackPower()
    {
        var request = Request(Gear("main-hand", 279259, 20031));
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(request);
        Assert.Equal(84m, resolved.MainHandMinimumDamage);
        Assert.Equal(153m, resolved.MainHandMaximumDamage);
        Assert.Equal(2.4m, resolved.MainHandSpeedSeconds);
        Assert.Equal(request.AttackPower + 16m,
            WarriorGearStatCalculator.Calculate(resolved).EffectiveStats.Get(ForeverCombatStatKeys.AttackPower));
    }

    [Fact]
    public void LionheartAndSuperiorAgilityEnchant_FlowThroughSharedStatPipeline()
    {
        var request = Request(Gear("main-hand", 279259), Gear("head", 12640), Gear("hands", 15063, 25080));
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(1200m + 16m + 36m + 28m, result.EffectiveCharacterStats[ForeverCombatStatKeys.AttackPower]);
        Assert.Equal(4m + 1m + 2m, result.EffectiveCharacterStats[ForeverCombatStatKeys.HitChancePercent]);
        Assert.Contains(result.StatContributions, x => x.SourceName == "Enchant Gloves - Superior Agility");
        Assert.Contains(result.StatContributions, x => x.SourceName == "Lionheart Helm");
    }

    [Fact]
    public void DevilsaurTwoPieceBonus_AppliesExactlyOnceAndDoesNotApplyWithOnePiece()
    {
        var alone = Request(Gear("main-hand", 19168), Gear("legs", 15062));
        var together = Request(Gear("main-hand", 19168), Gear("legs", 15062), Gear("hands", 15063));
        var a = WarriorGearStatCalculator.Calculate(alone);
        var b = WarriorGearStatCalculator.Calculate(together);
        Assert.Equal(2m, b.EffectiveStats.Get(ForeverCombatStatKeys.HitChancePercent) -
            a.EffectiveStats.Get(ForeverCombatStatKeys.HitChancePercent));
        Assert.Contains(b.StatContributions, x => x.SourceName == "Devilsaur Armor (2)");
        Assert.DoesNotContain(a.StatContributions, x => x.SourceName == "Devilsaur Armor (2)");
    }

    [Fact]
    public void Catalog_RejectsForgedClientStatsAndUnknownGameIds()
    {
        var forged = Request(Gear("main-hand", 279259));
        forged.Gear[0].Stats["strength"] = 9000m;
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(forged));
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("main-hand", 999999999))));
    }

    [Fact]
    public void Catalog_RejectsWrongSlotsAndDuplicateSelections()
    {
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("main-hand", 279259), Gear("head", 279260))));
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("main-hand", 279259), Gear("main-hand", 19168))));
    }

    [Fact]
    public void Catalog_RejectsRepeatedUniqueEquippedItem()
    {
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("main-hand", 279259), Gear("off-hand", 279259))));
    }

    [Fact]
    public void Catalog_RejectsEnchantForWrongSlotAndUnmodeledProc()
    {
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("main-hand", 279259, 25080))));
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("main-hand", 279259, 20034))));
    }

    [Fact]
    public void Catalog_RequiresRealMainHandWeapon()
    {
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("head", 12640))));
    }
}
}
