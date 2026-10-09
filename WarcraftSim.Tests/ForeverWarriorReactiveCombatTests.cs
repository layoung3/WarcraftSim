using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverWarriorReactiveCombatTests
{
    [Theory]
    [InlineData(4, 1, 15, 9)]
    [InlineData(10, 2, 28, 12)]
    [InlineData(20, 3, 45, 15)]
    [InlineData(30, 4, 66, 18)]
    [InlineData(40, 5, 98, 21)]
    [InlineData(50, 6, 126, 21)]
    [InlineData(60, 7, 147, 21)]
    public void RendRank_DealsFullBaseDamageAndUsesThreeSecondTicks(
        int level, int expectedRank, int damage, int duration)
    {
        var ability = ForeverRendFactory.CreateForLevel(level);
        Assert.Equal($"Rend (Rank {expectedRank})", ability.Name);
        var source = Warrior(level);
        source.AddAbility(ability);
        var target = Target();
        var (_, executor, run) = Harness(source, [target], duration + 0.1m);
        var result = run(context =>
            Assert.True(executor.TryStartAbility(context, source.Key, target.Key,
                ability.Key).Success));
        var ticks = DamageEvents(result, ForeverRendFactory.AbilityKey);
        Assert.Equal(duration / 3, ticks.Count);
        Assert.Equal((decimal)damage, ticks.Sum(x => x.RawAmount ?? 0m));
        Assert.Equal(Enumerable.Range(1, duration / 3).Select(n => (decimal)n * 3m),
            ticks.Select(x => x.TimeSeconds));
        Assert.All(ticks, x => { Assert.True(x.IsPeriodic); Assert.False(x.IsCritical); });
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 112)]
    [InlineData(2, 123)]
    [InlineData(3, 135)]
    public void Rend_ImprovedRendTalentBonusUsesForeverRanks(
        int improvedRank, int expectedHundredths)
    {
        var ability = ForeverRendFactory.CreateForLevel(60, improvedRendRank: improvedRank);
        Assert.Equal(expectedHundredths / 100m, ability.Effects[0].DamageMultiplier);
    }

    [Fact]
    public void Rend_APCoefficientIsTotalOverAllTicks_NotPerTick()
    {
        const decimal configuredTotalApCoefficient = 0.15m;
        var source = Warrior(60);
        source.Stats.Set(ForeverCombatStatKeys.AttackPower, 100m);
        source.AddAbility(ForeverRendFactory.CreateForLevel(60,
            totalAttackPowerCoefficient: configuredTotalApCoefficient));
        var target = Target();
        var (_, executor, run) = Harness(source, [target], 21.1m);
        var result = run(context =>
            Assert.True(executor.TryStartAbility(context, source.Key, target.Key,
                ForeverRendFactory.AbilityKey).Success));
        Assert.Equal(162m, Math.Round(DamageEvents(result, ForeverRendFactory.AbilityKey)
            .Sum(e => e.RawAmount ?? 0m), 8));
    }

    [Theory]
    [InlineData(12, 1, 5)]
    [InlineData(28, 2, 15)]
    [InlineData(44, 3, 25)]
    [InlineData(60, 4, 35)]
    public void OverpowerRank_UsesNormalizedWeaponDamageAndHasNoAvoidance(
        int level, int rank, int bonus)
    {
        var ability = ForeverOverpowerFactory.CreateForLevel(level,
            100m, 100m, 2.4m, "weapon-skill", requireBattleStance: false);
        var effect = Assert.Single(ability.Effects);
        Assert.Equal($"Overpower (Rank {rank})", ability.Name);
        Assert.Equal(100m + bonus, effect.MinimumValue);
        Assert.False(effect.CanBeDodged);
        Assert.False(effect.CanBeParried);
        Assert.False(effect.CanBeBlocked);
        Assert.True(effect.CanMiss);
        Assert.Equal(5m, ability.CooldownSeconds);
        Assert.Equal(ForeverOverpowerFactory.OpportunityKey,
            ability.RequiredTargetOpportunityKey);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 4)]
    [InlineData(2, 8)]
    [InlineData(3, 12)]
    [InlineData(4, 16)]
    [InlineData(5, 20)]
    public void BloodthrillRank_ChanceAndWindowAreCorrect(int rank, int expectedChance)
    {
        var definition = ForeverOverpowerFactory.CreateOpportunity(rank);
        Assert.Equal(expectedChance, definition.ProcChancePercent);
        Assert.Equal(6m, definition.ProcWindowSeconds);
        Assert.Equal(5m, definition.DodgeWindowSeconds);
        Assert.Contains(ForeverCombatResolutionTypes.PlayerMeleeSpecial,
            definition.EligibleResolutionTypes);
    }

    [Fact]
    public void Overpower_RejectsUseWithoutWindowAndDoesNotSpendRage()
    {
        var source = Warrior(60);
        source.AddAbility(ForeverOverpowerFactory.CreateForLevel(
            60, 100m, 100m, 2.4m, "weapon-skill", requireBattleStance: false));
        var target = Target();
        var (context, executor, _) = Harness(source, [target], 1m);
        var before = source.Resources["rage"].Current;
        var result = executor.TryStartAbility(context, source.Key, target.Key,
            ForeverOverpowerFactory.AbilityKey);
        Assert.False(result.Success);
        Assert.Contains("opportunity", result.FailureReason ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, source.Resources["rage"].Current);
    }

    [Fact]
    public void Dodge_GrantsOnlyTargetSpecificWindow()
    {
        var source = Warrior(60);
        LearnOverpower(source);
        var target = Target();
        var other = Target("other");
        var (_, executor, run) = Harness(source, [target, other], 1m);
        var result = run(context =>
        {
            EmitAttack(context, source.Key, target.Key, CombatResultTypes.Dodge);
            Assert.False(executor.TryStartAbility(context, source.Key, other.Key,
                ForeverOverpowerFactory.AbilityKey).Success);
        });
        Assert.True(source.HasReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            target.Key, 0m));
        Assert.False(source.HasReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            other.Key, 0m));
        // A separate harness start at t=0 validates the opportunity gate and
        // successful damage delivery without depending on rotation polling.
        Assert.DoesNotContain(DamageEvents(result, ForeverOverpowerFactory.AbilityKey),
            _ => true);
    }

    [Fact]
    public void Overpower_WithOpportunityAndBattleStance_LandsAndConsumesWindow()
    {
        var source = Warrior(60);
        LearnOverpower(source, requireBattleStance: true);
        var target = Target();
        var (context, executor, run) = Harness(source, [target], 1m);
        var result = run(started =>
        {
            // The grant stands in for a previously resolved dodge/proc.
            source.GrantReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
                target.Key, 0m, 5m);
            var stance = new AuraDefinition { Key = ForeverWarriorAuraKeys.BattleStance,
                DurationSeconds = 100m };
            new AuraManager().ApplyAura(started, source, stance, source.Key,
                "battle-stance", "stance");
            Assert.True(executor.TryStartAbility(started, source.Key, target.Key,
                ForeverOverpowerFactory.AbilityKey).Success);
        });
        Assert.Single(DamageEvents(result, ForeverOverpowerFactory.AbilityKey));
        Assert.False(source.HasReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            target.Key, context.CurrentTimeSeconds));
        Assert.Equal(95m, source.Resources["rage"].Current);
    }

    [Fact]
    public void Overpower_WithoutBattleStance_CannotUseOpportunity()
    {
        var source = Warrior(60);
        LearnOverpower(source, requireBattleStance: true);
        var target = Target();
        var (context, executor, _) = Harness(source, [target], 1m);
        source.GrantReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            target.Key, 0m, 5m);
        var result = executor.TryStartAbility(context, source.Key, target.Key,
            ForeverOverpowerFactory.AbilityKey);
        Assert.False(result.Success);
        Assert.Contains(ForeverWarriorAuraKeys.BattleStance, result.FailureReason ?? "");
    }

    [Fact]
    public void DodgeWindow_ExpiresAtFiveSeconds_AndIsNotGloballyShared()
    {
        var source = Warrior(60);
        source.GrantReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            "first", 1m, 5m);
        Assert.True(source.HasReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            "first", 5.999m));
        Assert.False(source.HasReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            "first", 6m));
        Assert.False(source.HasReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            "second", 2m));
    }

    [Fact]
    public void Bloodthrill_OnlyProcsOnLandedMainHandStrikeAgainstOwnRend()
    {
        var source = Warrior(60);
        LearnOverpower(source, guaranteedBloodthrill: true);
        var target = Target();
        var other = Target("other");
        var (_, _, run) = Harness(source, [target, other], 2m);
        run(started =>
        {
            var aura = new AuraDefinition { Key = ForeverRendFactory.AuraKey,
                DurationSeconds = 21m };
            new AuraManager().ApplyAura(started, target, aura, source.Key,
                ForeverRendFactory.AbilityKey, "bleed");
            EmitAttack(started, source.Key, target.Key, CombatResultTypes.Hit,
                WeaponHandKeys.OffHand);
            EmitAttack(started, source.Key, other.Key, CombatResultTypes.Hit);
            EmitAttack(started, source.Key, target.Key, CombatResultTypes.Miss);
            EmitAttack(started, source.Key, target.Key, CombatResultTypes.Parry);
            EmitAttack(started, source.Key, target.Key, CombatResultTypes.Hit,
                WeaponHandKeys.MainHand, resolution: CombatResolutionTypes.Spell);
            EmitAttack(started, source.Key, target.Key, CombatResultTypes.Hit,
                WeaponHandKeys.MainHand, periodic: true);
        });
        Assert.False(source.HasReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            target.Key, 0m));
    }

    [Theory]
    [InlineData("heroic-strike")]
    [InlineData("cleave")]
    [InlineData("main-hand-auto")]
    public void Bloodthrill_LandedMainHandSpecialsAndAutosCreateSixSecondWindow(string ability)
    {
        var source = Warrior(60);
        LearnOverpower(source, guaranteedBloodthrill: true);
        var target = Target();
        var (_, _, run) = Harness(source, [target], 1m);
        run(started =>
        {
            new AuraManager().ApplyAura(started, target,
                new AuraDefinition { Key = ForeverRendFactory.AuraKey, DurationSeconds = 21m },
                source.Key, ForeverRendFactory.AbilityKey, "bleed");
            EmitAttack(started, source.Key, target.Key, CombatResultTypes.Hit,
                WeaponHandKeys.MainHand,
                resolution: ability == "main-hand-auto" ?
                    ForeverCombatResolutionTypes.PlayerMeleeAuto :
                    ForeverCombatResolutionTypes.PlayerMeleeSpecial,
                ability: ability);
        });
        Assert.True(source.HasReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            target.Key, 5.999m));
        Assert.False(source.HasReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            target.Key, 6m));
    }

    [Fact]
    public void OverlappingProcWindows_DoNotShortenLongerAvailability()
    {
        var source = Warrior(60);
        source.GrantReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            "enemy", 0m, 6m);
        source.GrantReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            "enemy", 0.1m, 5m);
        Assert.True(source.HasReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            "enemy", 5.9m));
        Assert.False(source.HasReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            "enemy", 6m));
    }

    [Fact]
    public void RealRendApplication_EnablesBloodthrillOnTheSameTarget()
    {
        var source = Warrior(60);
        source.AddAbility(ForeverRendFactory.CreateForLevel(60));
        LearnOverpower(source, guaranteedBloodthrill: true);
        var target = Target();
        var (_, executor, run) = Harness(source, [target], 4m);
        var result = run(started =>
        {
            Assert.True(executor.TryStartAbility(started, source.Key, target.Key,
                ForeverRendFactory.AbilityKey).Success);
            started.ScheduleEvent(new CombatEvent
            {
                Type = CombatEventType.Damage,
                TimeSeconds = 2m,
                SourceActorKey = source.Key,
                TargetActorKey = target.Key,
                AbilityKey = "heroic-strike",
                ResolutionType = ForeverCombatResolutionTypes.PlayerMeleeSpecial,
                WeaponHandKey = WeaponHandKeys.MainHand,
                ResultKey = CombatResultTypes.Hit,
                Amount = 40m
            });
        });
        Assert.Contains(result.Timeline, e => e.Type == CombatEventType.AuraApplied &&
            e.AbilityKey == ForeverRendFactory.AbilityKey);
        Assert.Contains(result.Timeline, e => e.Type == CombatEventType.ReactiveOpportunityGranted &&
            e.AbilityKey == ForeverOverpowerFactory.AbilityKey);
        Assert.True(source.HasReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            target.Key, 7.999m));
    }

    [Fact]
    public void Bloodthrill_RequiresRendCastBySameWarrior()
    {
        var source = Warrior(60);
        LearnOverpower(source, guaranteedBloodthrill: true);
        var target = Target();
        var (_, _, run) = Harness(source, [target], 1m);
        run(started =>
        {
            new AuraManager().ApplyAura(started, target,
                new AuraDefinition { Key = ForeverRendFactory.AuraKey, DurationSeconds = 21m },
                "another-warrior", ForeverRendFactory.AbilityKey, "bleed");
            EmitAttack(started, source.Key, target.Key, CombatResultTypes.Hit);
        });
        Assert.False(source.HasReactiveOpportunity(ForeverOverpowerFactory.OpportunityKey,
            target.Key, 0m));
    }

    [Fact]
    public void MissingReactiveProcessor_IsValidationError()
    {
        var source = Warrior(60);
        LearnOverpower(source);
        var target = Target();
        var context = Context(source, [target], 1m);
        var aura = new AuraManager();
        var executor = Executor(aura);
        var error = Assert.Throws<WarcraftSim.Core.Simulation.Validation.SimulationDefinitionValidationException>(
            () => new SimulationEngine([aura, executor]).Run(context));
        Assert.Contains(nameof(ReactiveAbilityOpportunityProcessor), error.Message);
    }

    [Fact]
    public void InvalidRankValuesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ForeverRendFactory.CreateForLevel(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => ForeverRendFactory.CreateForLevel(60, improvedRendRank: 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => ForeverRendFactory.CreateForLevel(60, totalAttackPowerCoefficient: -1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => ForeverOverpowerFactory.CreateForLevel(11, 10m, 20m, 2m, "skill"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ForeverOverpowerFactory.CreateOpportunity(6));
    }

    private static SimulationActorState Warrior(int level)
    {
        var actor = new SimulationActorState { Key = "warrior", Name = "Warrior",
            TeamKey = "raid", Level = level };
        actor.InitializeHealth(100000m);
        actor.AddResource(new ResourceState("rage", 100m, 100m));
        return actor;
    }

    private static SimulationActorState Target(string key = "enemy")
    {
        var actor = new SimulationActorState { Key = key, Name = key,
            TeamKey = "enemy", Level = 60 };
        actor.InitializeHealth(100000m);
        return actor;
    }

    private static void LearnOverpower(SimulationActorState warrior,
        bool requireBattleStance = false, bool guaranteedBloodthrill = false)
    {
        warrior.AddAbility(ForeverOverpowerFactory.CreateForLevel(
            warrior.Level, 100m, 100m, 2.4m, "weapon-skill", requireBattleStance));
        var definition = guaranteedBloodthrill
            ? new ReactiveAbilityOpportunityDefinition
            {
                OpportunityKey = ForeverOverpowerFactory.OpportunityKey,
                AbilityKey = ForeverOverpowerFactory.AbilityKey,
                DodgeWindowSeconds = 5m,
                ProcWindowSeconds = 6m,
                ProcChancePercent = 100m,
                RequiredTargetAuraKey = ForeverRendFactory.AuraKey
            }
            : ForeverOverpowerFactory.CreateOpportunity();
        if (guaranteedBloodthrill)
        {
            definition.EligibleResolutionTypes.Add(ForeverCombatResolutionTypes.PlayerMeleeAuto);
            definition.EligibleResolutionTypes.Add(ForeverCombatResolutionTypes.PlayerMeleeSpecial);
        }
        warrior.AddReactiveOpportunityDefinition(definition);
    }

    private static SimulationContext Context(SimulationActorState source,
        IEnumerable<SimulationActorState> others, decimal duration)
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            DurationSeconds = duration, PrimaryActorKey = source.Key,
            Seed = 12, CaptureTimeline = true
        });
        context.AddActor(source);
        foreach (var actor in others) context.AddActor(actor);
        return context;
    }

    // CP105: This test harness deliberately bypasses mitigation so that
    // Rend/Overpower and reactive-window assertions stay independent of armor.
    // Keep the resolver private to the test suite; production damage continues
    // to use RulesetDamageMitigationResolver.
    private sealed class UnmitigatedDamageResolver : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(
            SimulationContext context,
            SimulationActorState source,
            SimulationActorState target,
            AbilityDefinition ability,
            AbilityEffectDefinition effect,
            decimal rawAmount) => DamageMitigationResult.Unmitigated(rawAmount);
    }

    private static AbilityExecutor Executor(AuraManager aura) =>
        new(aura, new SimpleCombatRollResolver(), new UnmitigatedDamageResolver());

    private static (SimulationContext Context, AbilityExecutor Executor,
        Func<Action<SimulationContext>, SimulationRunResult> Run) Harness(
            SimulationActorState source, IEnumerable<SimulationActorState> others,
            decimal duration)
    {
        var context = Context(source, others, duration);
        var aura = new AuraManager();
        var executor = Executor(aura);
        var proc = new ReactiveAbilityOpportunityProcessor();
        return (context, executor, callback =>
            new SimulationEngine([aura, executor, proc]).Run(context, callback));
    }

    private static List<CombatEvent> DamageEvents(SimulationRunResult result, string ability) =>
        result.Timeline.Where(e => e.Type == CombatEventType.Damage &&
            string.Equals(e.AbilityKey, ability, StringComparison.OrdinalIgnoreCase)).ToList();

    private static void EmitAttack(SimulationContext context, string source,
        string target, string resultKey, string hand = WeaponHandKeys.MainHand,
        string resolution = ForeverCombatResolutionTypes.PlayerMeleeSpecial,
        bool periodic = false, string ability = "melee")
    {
        context.EmitEvent(new CombatEvent
        {
            Type = CombatEventType.Damage,
            TimeSeconds = context.CurrentTimeSeconds,
            SourceActorKey = source, TargetActorKey = target,
            AbilityKey = ability, WeaponHandKey = hand,
            ResolutionType = resolution, ResultKey = resultKey,
            EffectDeliveryType = periodic ? CombatEffectDeliveryType.Periodic :
                CombatEffectDeliveryType.Direct,
            Amount = resultKey == CombatResultTypes.Hit ? 10m : 0m
        });
    }
}
