using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverExecuteTests
{
    [Theory]
    [InlineData(1, 5308, 24, 125, 3)]
    [InlineData(2, 20658, 32, 200, 6)]
    [InlineData(3, 20660, 40, 325, 9)]
    [InlineData(4, 20661, 48, 450, 12)]
    [InlineData(5, 20662, 56, 600, 15)]
    public void RankTable_UsesForeverClientValues(
        int rank,
        int spellId,
        int requiredLevel,
        decimal baseDamage,
        decimal damagePerExtraRage)
    {
        var definition = ForeverExecuteRanks.GetByRank(rank);

        Assert.Equal(rank, definition.Rank);
        Assert.Equal(spellId, definition.SpellId);
        Assert.Equal(requiredLevel, definition.RequiredLevel);
        Assert.Equal(baseDamage, definition.BaseDamage);
        Assert.Equal(damagePerExtraRage, definition.DamagePerExtraRage);
        Assert.Equal(15m, definition.RageCost);
        Assert.Equal(1.5m, definition.GlobalCooldownSeconds);
    }

    [Theory]
    [InlineData(24, 1)]
    [InlineData(31, 1)]
    [InlineData(32, 2)]
    [InlineData(60, 5)]
    public void HighestAvailableRank_UsesCharacterLevel(
        int characterLevel,
        int expectedRank)
    {
        var rank = ForeverExecuteRanks.GetHighestAvailable(characterLevel);
        Assert.Equal(expectedRank, rank.Rank);
    }

    [Fact]
    public void LevelThirtyFactory_CreatesRankOneExecute()
    {
        var execute = ForeverExecuteFactory.CreateForLevel(
            30,
            "sword-skill"
        );

        Assert.Equal(ForeverExecuteFactory.AbilityKey, execute.Key);
        Assert.Equal("Execute (Rank 1)", execute.Name);
        Assert.Equal(ForeverCharacterClassKeys.Warrior, execute.ClassKey);
        Assert.Equal(24, execute.RequiredLevel);
        Assert.Equal(20m, execute.MaximumTargetHealthPercent);
        Assert.Equal(1.5m, execute.GlobalCooldownSeconds);
        Assert.False(execute.IsOffGlobalCooldown);

        var cost = Assert.Single(execute.ResourceCosts);
        Assert.Equal(ForeverExecuteFactory.RageResourceKey, cost.ResourceKey);
        Assert.Equal(15m, cost.Amount);

        var additional = Assert.Single(execute.AdditionalResourceConsumptions);
        Assert.Equal(ForeverExecuteFactory.RageResourceKey, additional.ResourceKey);
        Assert.Null(additional.MaximumAmount);

        var effect = Assert.Single(execute.Effects);
        Assert.Equal(WeaponHandKeys.MainHand, effect.WeaponHandKey);
        Assert.Equal(125m, effect.MinimumValue);
        Assert.Equal(125m, effect.MaximumValue);
        Assert.Equal("rage", effect.ConsumedResourceScalingKey);
        Assert.Equal(3m, effect.ConsumedResourceScalingCoefficient);
        Assert.Equal(ForeverCombatResolutionTypes.PlayerMeleeSpecial, effect.ResolutionType);
        Assert.Equal("sword-skill", effect.AttackSkillStatKey);
        Assert.True(effect.CanMiss);
        Assert.True(effect.CanBeDodged);
        Assert.True(effect.CanBeParried);
        Assert.True(effect.CanBeBlocked);
        Assert.True(effect.CanCrit);
        Assert.False(effect.CanGlance);
        Assert.Equal(DamageMitigationTypes.Armor, effect.MitigationType);
    }

    [Fact]
    public void RageCostReduction_ChangesBaseCostWithoutChangingExtraRageConversion()
    {
        var execute = ForeverExecuteFactory.CreateForLevel(
            30,
            "sword-skill",
            rageCostReduction: 3m
        );

        Assert.Equal(12m, Assert.Single(execute.ResourceCosts).Amount);
        Assert.Equal(3m, Assert.Single(execute.Effects).ConsumedResourceScalingCoefficient);
    }

    [Fact]
    public void RankOneExecution_ConsumesRemainingRageAndAddsThreeDamagePerPoint()
    {
        var source = CreateActor("warrior", 1000m, 30m);
        var target = CreateActor("target", 1000m, 0m, 200m);
        source.AddAbility(ForeverExecuteFactory.CreateForLevel(30, "sword-skill"));

        var result = RunExecute(source, target);

        Assert.Equal(0m, source.Resources["rage"].Current);

        var damage = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == ForeverExecuteFactory.AbilityKey
        );

        // 30 starting Rage - 15 base cost = 15 extra Rage.
        Assert.Equal(170m, damage.RawAmount);
        Assert.Equal(170m, damage.Amount);
    }

    [Fact]
    public void RankOneExecution_WithOnlyBaseCostDealsBaseDamage()
    {
        var source = CreateActor("warrior", 1000m, 15m);
        var target = CreateActor("target", 1000m, 0m, 200m);
        source.AddAbility(ForeverExecuteFactory.CreateForLevel(30, "sword-skill"));

        var result = RunExecute(source, target);

        var damage = Assert.Single(
            result.Timeline,
            combatEvent => combatEvent.Type == CombatEventType.Damage
        );

        Assert.Equal(125m, damage.RawAmount);
        Assert.Equal(0m, source.Resources["rage"].Current);
    }

    [Fact]
    public void ExecuteAboveTwentyPercent_FailsWithoutSpendingRage()
    {
        var source = CreateActor("warrior", 1000m, 30m);
        var target = CreateActor("target", 1000m, 0m, 201m);
        source.AddAbility(ForeverExecuteFactory.CreateForLevel(30, "sword-skill"));

        var context = CreateContext(source, target);
        var executor = CreateExecutor();
        var result = executor.TryStartAbility(
            context,
            source.Key,
            target.Key,
            ForeverExecuteFactory.AbilityKey
        );

        Assert.False(result.Success);
        Assert.Equal(30m, source.Resources["rage"].Current);
    }

    [Fact]
    public void UnsupportedFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverExecuteFactory.CreateForLevel(23, "sword-skill"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverExecuteFactory.CreateForLevel(61, "sword-skill"));
        Assert.Throws<ArgumentException>(() =>
            ForeverExecuteFactory.CreateForLevel(30, ""));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverExecuteFactory.CreateForLevel(30, "sword-skill", -1m));
    }

    private static SimulationRunResult RunExecute(
        SimulationActorState source,
        SimulationActorState target)
    {
        var context = CreateContext(source, target);
        var executor = CreateExecutor();

        return new SimulationEngine([executor]).Run(
            context,
            started =>
            {
                var use = executor.TryStartAbility(
                    started,
                    source.Key,
                    target.Key,
                    ForeverExecuteFactory.AbilityKey
                );
                Assert.True(use.Success, use.FailureReason ?? "Ability use failed.");
            }
        );
    }

    private static SimulationContext CreateContext(
        SimulationActorState source,
        SimulationActorState target)
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = 0.1m,
            CaptureTimeline = true,
            PrimaryActorKey = source.Key,
            Seed = 1
        });
        context.AddActor(source);
        context.AddActor(target);
        return context;
    }

    private static AbilityExecutor CreateExecutor()
    {
        var ruleset = new CombatRulesetDefinition
        {
            RulesetKey = "execute-tests",
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
        };

        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(
                hitChancePercent: 100m,
                criticalChancePercent: 0m
            ),
            new RulesetDamageMitigationResolver(ruleset)
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        decimal maximumHealth,
        decimal rage,
        decimal? startingHealth = null)
    {
        var actor = new SimulationActorState
        {
            Key = key,
            Name = key,
            TeamKey = key,
            Level = 30
        };
        actor.InitializeHealth(maximumHealth, startingHealth);
        actor.AddResource(new ResourceState("rage", 100m, rage));
        return actor;
    }
}
