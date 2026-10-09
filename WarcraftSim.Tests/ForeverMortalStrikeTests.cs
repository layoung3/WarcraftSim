using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverMortalStrikeTests
{
    [Theory]
    [InlineData(1, 12294, 40, 85)]
    [InlineData(2, 21551, 48, 110)]
    [InlineData(3, 21552, 54, 135)]
    [InlineData(4, 21553, 60, 160)]
    public void RankTable_UsesForeverValues(
        int rank,
        int spellId,
        int requiredLevel,
        decimal bonusWeaponDamage)
    {
        var definition = ForeverMortalStrikeRanks.GetByRank(rank);

        Assert.Equal(rank, definition.Rank);
        Assert.Equal(spellId, definition.SpellId);
        Assert.Equal(requiredLevel, definition.RequiredLevel);
        Assert.Equal(bonusWeaponDamage, definition.BonusWeaponDamage);
        Assert.Equal(30m, definition.RageCost);
        Assert.Equal(6m, definition.CooldownSeconds);
        Assert.Equal(1.5m, definition.GlobalCooldownSeconds);
        Assert.Equal(10m, definition.HealingReductionDurationSeconds);
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
            ForeverMortalStrikeRanks.GetHighestAvailable(characterLevel).Rank
        );
    }

    [Fact]
    public void LevelSixtyFactory_CreatesRankFourNormalizedMainHandSpecial()
    {
        var ability = ForeverMortalStrikeFactory.CreateForLevel(
            60,
            100m,
            120m,
            ForeverNormalizedWeaponSpeeds.TwoHanded,
            "two-hand-sword-skill"
        );

        Assert.Equal(ForeverMortalStrikeFactory.AbilityKey, ability.Key);
        Assert.Equal("Mortal Strike (Rank 4)", ability.Name);
        Assert.Equal(ForeverCharacterClassKeys.Warrior, ability.ClassKey);
        Assert.Equal(60, ability.RequiredLevel);
        Assert.Equal(6m, ability.CooldownSeconds);
        Assert.Equal(1.5m, ability.GlobalCooldownSeconds);
        Assert.Equal(30m, Assert.Single(ability.ResourceCosts).Amount);

        var damage = Assert.Single(
            ability.Effects,
            effect => effect.Key == "damage"
        );
        Assert.Equal(WeaponHandKeys.MainHand, damage.WeaponHandKey);
        Assert.Equal(260m, damage.MinimumValue);
        Assert.Equal(280m, damage.MaximumValue);
        Assert.Equal(ForeverCombatStatKeys.AttackPower, damage.ScalingStatKey);
        Assert.Equal(3.3m / 14m, damage.ScalingCoefficient);
        Assert.Equal(ForeverCombatResolutionTypes.PlayerMeleeSpecial, damage.ResolutionType);
        Assert.Equal("two-hand-sword-skill", damage.AttackSkillStatKey);

        var marker = Assert.Single(
            ability.Effects,
            effect => effect.Key == "healing-reduction"
        );
        Assert.Equal(ForeverWarriorAuraKeys.MortalStrikeHealingReduction, marker.AuraKey);
        Assert.Equal(10m, marker.DurationSeconds);
        Assert.Equal("damage", marker.DependsOnEffectKey);
    }

    [Fact]
    public void RageCostReduction_ChangesCostOnly()
    {
        var ability = ForeverMortalStrikeFactory.CreateForLevel(
            60,
            100m,
            100m,
            ForeverNormalizedWeaponSpeeds.TwoHanded,
            "skill",
            rageCostReduction: 3m
        );

        Assert.Equal(27m, Assert.Single(ability.ResourceCosts).Amount);
        Assert.Equal(260m, Assert.Single(ability.Effects, effect => effect.Key == "damage").MinimumValue);
    }

    [Fact]
    public void LevelSixtyExecution_UsesNormalizedAttackPowerAndAppliesMarkerAura()
    {
        var source = CreateActor("warrior", "raid", 100m);
        var target = CreateActor("boss", "enemy", 0m);
        source.Stats.Set(ForeverCombatStatKeys.AttackPower, 140m);
        source.AddAbility(
            ForeverMortalStrikeFactory.CreateForLevel(
                60,
                100m,
                100m,
                ForeverNormalizedWeaponSpeeds.TwoHanded,
                "skill"
            )
        );

        var result = RunAbility(source, target, ForeverMortalStrikeFactory.AbilityKey);

        var damage = Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                combatEvent.AbilityKey == ForeverMortalStrikeFactory.AbilityKey
        );
        Assert.Equal(293m, damage.RawAmount);
        Assert.Equal(70m, source.Resources["rage"].Current);
        Assert.Contains(
            target.ActiveAuras,
            aura => aura.Definition.Key == ForeverWarriorAuraKeys.MortalStrikeHealingReduction
        );
    }

    [Fact]
    public void UnsupportedFactoryInputs_FailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverMortalStrikeFactory.CreateForLevel(39, 1m, 1m, 3.3m, "skill"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverMortalStrikeFactory.CreateForLevel(61, 1m, 1m, 3.3m, "skill"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverMortalStrikeFactory.CreateForLevel(60, 1m, 1m, 0m, "skill"));
        Assert.Throws<ArgumentException>(() =>
            ForeverMortalStrikeFactory.CreateForLevel(60, 1m, 1m, 3.3m, ""));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ForeverMortalStrikeFactory.CreateForLevel(60, 1m, 1m, 3.3m, "skill", -1m));
    }

    private static SimulationRunResult RunAbility(
        SimulationActorState source,
        SimulationActorState target,
        string abilityKey)
    {
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

        return new SimulationEngine([executor]).Run(
            context,
            started =>
            {
                var use = executor.TryStartAbility(started, source.Key, target.Key, abilityKey);
                Assert.True(use.Success, use.FailureReason ?? "Ability use failed.");
            }
        );
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
                    RulesetKey = "mortal-strike-tests",
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
