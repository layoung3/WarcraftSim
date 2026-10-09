using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

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
