using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175WarriorWeaponmasterHands
{
    private static WarriorSimRequest Request(string main, string off, int rank = 5) => new()
    {
        TalentMode = "tree", Preset = "fury", DualWield = true, IsTwoHanded = false,
        MainHandWeaponType = main, OffHandWeaponType = off,
        Iterations = 1, Seed = 17570707, DurationSeconds = 15m,
        CaptureRepresentativeTimeline = true, StartingStance = "battle",
        StartingRage = 100m, AttackPower = 0m, MeleeCritPercent = 0m,
        MeleeHitPercent = 100m, TargetLevel = 60, TargetArmor = 3000m,
        TargetDodgePercent = 0m, TargetParryPercent = 0m, TargetBlockPercent = 0m,
        MainHandMinimumDamage = 1000m, MainHandMaximumDamage = 1000m,
        OffHandMinimumDamage = 1000m, OffHandMaximumDamage = 1000m,
        MainHandSpeedSeconds = 2m, OffHandSpeedSeconds = 2m,
        RotationMode = "custom", CustomRotation = [new() { Action = "execute" }],
        ExecutePhaseDurationSeconds = 0m, UseBloodrage = false, UseRend = false,
        UseExecute = true, UseHeroicStrike = false, UseSlam = false,
        TalentRanks = new(StringComparer.OrdinalIgnoreCase)
        {
            ["improved-heroic-strike"] = 3, ["deflection"] = 5,
            ["improved-charge"] = 2, ["improved-tactical-mastery"] = 5,
            ["improved-overpower"] = 2, ["two-handed-weapon-specialization"] = 3,
            ["weaponmaster"] = rank
        }
    };

    private static CombatRulesetDefinition Rules(decimal baseConstant = 400m,
        decimal perLevel = 85m, decimal cap = 75m)
    {
        var rules = ForeverCombatRulesetFactory.Create();
        rules.MitigationRules[DamageMitigationTypes.Armor] = new()
        {
            MitigationType = DamageMitigationTypes.Armor,
            FormulaType = DamageMitigationFormulaTypes.RationalLevelScaled,
            DefenseStatKey = ForeverCombatStatKeys.Armor,
            BaseConstant = baseConstant, PerAttackerLevelConstant = perLevel,
            MaximumReductionPercent = cap
        };
        return rules;
    }

    private static (SimulationContext context, SimulationActorState warrior,
        SimulationActorState target) Actors(decimal armor = 3000m)
    {
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = 17570707, DurationSeconds = 180m, PrimaryActorKey = "warrior"
        });
        var warrior = new SimulationActorState { Key = "warrior", Level = 60, TeamKey = "raid" };
        warrior.InitializeHealth(1000m);
        warrior.AddResource(new ResourceState("rage", 100m, 0m));
        var target = new SimulationActorState { Key = "target", Level = 60, TeamKey = "enemy" };
        target.InitializeHealth(100000000m);
        target.Stats.Set(ForeverCombatStatKeys.Armor, armor);
        context.AddActor(warrior);
        context.AddActor(target);
        return (context, warrior, target);
    }

    private static AbilityDefinition Whirlwind() => ForeverWhirlwindFactory.CreateDualWield(
        1000m, 1000m, 2.4m, "main-hand-weapon-skill",
        1000m, 1000m, 2.4m, "off-hand-weapon-skill");

    private static void Close(decimal expected, decimal actual) =>
        Assert.InRange(Math.Abs(expected - actual), 0m, .000001m);

    [Theory]
    [InlineData("axe", "sword")]
    [InlineData("sword", "axe")]
    [InlineData("axe", "mace")]
    [InlineData("mace", "axe")]
    [InlineData("polearm", "sword")]
    [InlineData("sword", "polearm")]
    public void MixedCritRanksApplyOnlyToTheEligibleExplicitHand(string main, string off)
    {
        var (context, warrior, target) = Actors();
        var rules = Rules();
        var ability = Whirlwind();
        foreach (var rank in new[] { 0, 1, 5 })
        {
            var request = Request(main, off, rank);
            ForeverWarriorTalentCatalog.Validate(request);
            var modifier = new WarriorWeaponmasterCombatModifiers(request, rules);
            // A single sidebar aggregate must not become a false per-hand stat.
            Assert.Equal(0m, WarriorGearStatCalculator.Calculate(request).EffectiveStats
                .Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
            foreach (var effect in ability.Effects)
            {
                var type = effect.WeaponHandKey == WeaponHandKeys.MainHand ? main : off;
                var delta = modifier.GetAdjustment(context, warrior, target, ability,
                    effect, rules.GetRollRule(effect.ResolutionType)!).CriticalChancePercentDelta;
                Assert.Equal(type is "axe" or "polearm" ? (decimal)rank : 0m, delta);
            }
        }
    }

    [Theory]
    [InlineData("axe", "axe", true)]
    [InlineData("axe", "polearm", true)]
    [InlineData("polearm", "axe", true)]
    [InlineData("axe", "sword", false)]
    public void ExistingHomogeneousCritIsNotCountedAgain(string main, string off, bool dual)
    {
        var (context, warrior, target) = Actors();
        var rules = Rules();
        var ability = Whirlwind();
        foreach (var rank in new[] { 0, 1, 5 })
        {
            var request = Request(main, off, rank);
            request.DualWield = dual;
            var modifier = new WarriorWeaponmasterCombatModifiers(request, rules);
            Assert.Equal((decimal)rank, WarriorGearStatCalculator.Calculate(request).EffectiveStats
                .Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
            Assert.All(ability.Effects, effect => Assert.Equal(0m,
                modifier.GetAdjustment(context, warrior, target, ability, effect,
                    rules.GetRollRule(effect.ResolutionType)!).CriticalChancePercentDelta));
        }
    }

    [Theory]
    [InlineData("mace", "sword")]
    [InlineData("sword", "mace")]
    [InlineData("staff", "sword")]
    [InlineData("sword", "staff")]
    [InlineData("mace", "mace")]
    public void DualWieldArmorRanksFollowEachWhirlwindHandWithoutMutatingTarget(string main, string off)
    {
        var (context, warrior, target) = Actors();
        var ability = Whirlwind();
        foreach (var rank in new[] { 0, 1, 5 })
        {
            var modifier = new WarriorWeaponmasterCombatModifiers(Request(main, off, rank), Rules());
            foreach (var effect in ability.Effects)
            {
                var type = effect.WeaponHandKey == WeaponHandKeys.MainHand ? main : off;
                var effectiveRank = type is "mace" or "staff" ? rank : 0;
                var expected = effectiveRank switch
                {
                    1 => 653.983353m, 5 => 683.229814m, _ => 647.058824m
                };
                var result = modifier.Resolve(context, warrior, target, ability, effect, 1000m);
                Close(expected, result.FinalAmount);
                Assert.Equal(3000m * (1m - .03m * effectiveRank), result.DefenseValue);
                Assert.Equal(3000m, target.Stats.Get(ForeverCombatStatKeys.Armor));
            }
        }
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("single-weapon")]
    [InlineData("other-actor")]
    [InlineData("spell")]
    [InlineData("periodic")]
    [InlineData("channel")]
    [InlineData("handless-bloodthirst")]
    [InlineData("invalid-hand")]
    [InlineData("unmitigated-bleed")]
    [InlineData("shield-bash")]
    [InlineData("shield-slam")]
    public void UnsupportedEffectsAreDelegatedWithoutNewEligibility(string exclusion)
    {
        var (context, warrior, target) = Actors();
        var rules = Rules();
        foreach (var main in new[] { "axe", "mace" })
        {
            var request = Request(main, "sword");
            var ability = Whirlwind();
            var effect = ability.Effects[0];
            if (exclusion == "legacy") request.TalentMode = "legacy";
            if (exclusion == "single-weapon") request.DualWield = false;
            if (exclusion == "other-actor") warrior.Key = "priest";
            if (exclusion == "spell") effect.ResolutionType = ForeverCombatResolutionTypes.PlayerSpell;
            if (exclusion == "periodic") effect.EffectType = AbilityEffectTypes.PeriodicDamage;
            if (exclusion == "channel") effect.ApplyOnChannelTick = true;
            if (exclusion == "handless-bloodthirst") { ability.Key = "bloodthirst"; effect.WeaponHandKey = null; }
            if (exclusion == "invalid-hand") effect.WeaponHandKey = "ranged";
            if (exclusion == "unmitigated-bleed") effect.MitigationType = DamageMitigationTypes.None;
            if (exclusion is "shield-bash" or "shield-slam") ability.Key = exclusion;
            var modifier = new WarriorWeaponmasterCombatModifiers(request, rules);
            Assert.Equal(0m, modifier.GetAdjustment(context, warrior, target, ability,
                effect, rules.GetRollRule(effect.ResolutionType)!).CriticalChancePercentDelta);
            var actual = modifier.Resolve(context, warrior, target, ability, effect, 1000m);
            var expected = new RulesetDamageMitigationResolver(rules)
                .Resolve(context, warrior, target, ability, effect, 1000m);
            Assert.Equal(expected.FinalAmount, actual.FinalAmount);
            Assert.Equal(expected.DefenseValue, actual.DefenseValue);
            Assert.Equal(expected.ReductionPercent, actual.ReductionPercent);
            Assert.Equal(3000m, target.Stats.Get(ForeverCombatStatKeys.Armor));
        }
    }

    [Theory]
    [InlineData(CombatEffectDeliveryType.Periodic)]
    [InlineData(CombatEffectDeliveryType.ChannelTick)]
    public void ExplicitNonDirectDeliveryNeverReceivesConditionalCrit(CombatEffectDeliveryType delivery)
    {
        var (context, warrior, target) = Actors();
        var rules = Rules();
        var ability = Whirlwind();
        var modifier = new WarriorWeaponmasterCombatModifiers(Request("axe", "sword"), rules);
        Assert.Equal(0m, modifier.GetAdjustment(context, warrior, target, ability, ability.Effects[0],
            rules.GetRollRule(ability.Effects[0].ResolutionType)!, delivery).CriticalChancePercentDelta);
    }

    [Fact]
    public void ArmorResolverReadsCurrentTargetArmorAndRetainsConfiguredConstantsAndCap()
    {
        var (context, warrior, target) = Actors();
        var ability = Whirlwind();
        var modifier = new WarriorWeaponmasterCombatModifiers(Request("mace", "sword"), Rules(700m, 70m, 60m));
        foreach (var armor in new[] { 3000m, 2550m, 5000m, 1000000m, 0m, -10m })
        {
            target.Stats.Set(ForeverCombatStatKeys.Armor, armor);
            var effective = Math.Max(0m, armor) * .85m;
            var reduction = Math.Min(.6m, effective / (effective + 4900m));
            var result = modifier.Resolve(context, warrior, target, ability, ability.Effects[0], 1000m);
            Close(1000m * (1m - reduction), result.FinalAmount);
            Assert.Equal(effective, result.DefenseValue);
            Assert.Equal(armor, target.Stats.Get(ForeverCombatStatKeys.Armor));
        }
        target.Stats.Set(ForeverCombatStatKeys.Armor, 1000000m);
        var standard = new WarriorWeaponmasterCombatModifiers(Request("mace", "sword"), Rules());
        Assert.Equal(250m, standard.Resolve(context, warrior, target, ability, ability.Effects[0], 1000m).FinalAmount);
        foreach (var raw in new[] { 0m, -100m })
            Assert.Equal(0m, modifier.Resolve(context, warrior, target, ability, ability.Effects[0], raw).FinalAmount);
    }

    [Theory]
    [InlineData("mace", "sword")]
    [InlineData("sword", "mace")]
    public void RunnerUsesLiveSunderAndEachTargetsArmorForBothWhirlwindHandsAndReplays(string main, string off)
    {
        var request = Request(main, off);
        request.StartingStance = "berserker";
        request.CustomRotation = [new() { Action = "whirlwind" }];
        request.MaintenancePolicies = [new() { AbilityKey = "sunder-armor", DesiredStacks = 1 }];
        request.AdditionalTargets = [new()
        {
            Key = "add-1", Name = "Armored add", Level = 60, Armor = 5000m,
            SpawnTimeSeconds = 0m, DespawnTimeSeconds = 15m,
            DodgePercent = 0m, ParryPercent = 0m, BlockPercent = 0m
        }];
        var first = new ForeverWarriorSimRunner().Run(request);
        var timeline = first.Batch.RepresentativeRun.Timeline;
        Assert.Contains(timeline, e => e.AbilityKey == "sunder-armor" && e.Type == CombatEventType.AuraApplied);
        var hits = timeline.Where(e => e.AbilityKey == "whirlwind" &&
            e.Type == CombatEventType.Damage && e.Amount > 0m && e.TimeSeconds >= 2m).ToArray();
        foreach (var target in new[] { "target", "add-1" })
        foreach (var hand in new[] { WeaponHandKeys.MainHand, WeaponHandKeys.OffHand })
        {
            var handHits = hits.Where(e => e.TargetActorKey == target && e.WeaponHandKey == hand).ToArray();
            Assert.NotEmpty(handHits);
            var type = hand == WeaponHandKeys.MainHand ? main : off;
            var armor = (target == "target" ? 2550m : 5000m) * (type == "mace" ? .85m : 1m);
            Assert.All(handHits, e => Close(100m * armor / (armor + 5500m), e.MitigationPercent!.Value));
        }
        var replay = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(first.MeanDps, replay.MeanDps);
        Assert.Equal(timeline.Select(e => (e.TimeSeconds, e.Type, e.AbilityKey, e.TargetActorKey, e.WeaponHandKey, e.Amount)),
            replay.Batch.RepresentativeRun.Timeline.Select(e => (e.TimeSeconds, e.Type, e.AbilityKey, e.TargetActorKey, e.WeaponHandKey, e.Amount)));
        Assert.Equal(3000m, request.TargetArmor);
        Assert.Equal(5000m, request.AdditionalTargets[0].Armor);
        Assert.Contains(first.ModelingNotes, note => note.Contains("Sidebar aggregate crit"));
        var talent = Assert.Single(ForeverWarriorTalentCatalog.Talents, t => t.Id == "weaponmaster");
        Assert.Equal("partial", talent.SimulationStatus);
        Assert.Equal("provisional", talent.DataConfidence);
        Assert.Equal("1.60.1.70707", talent.SourceBuild);
    }

    [Theory]
    [InlineData("axe", "mace", "main-hand")]
    [InlineData("mace", "axe", "off-hand")]
    public void RunnerActuallyRollsMixedHandCritWithoutChangingNonAxeCrit(string main, string off, string axeHand)
    {
        var request = Request(main, off);
        request.DurationSeconds = 180m;
        var run = new ForeverWarriorSimRunner().Run(request);
        var hits = run.Batch.RepresentativeRun.Timeline.Where(e => e.Type == CombatEventType.Damage &&
            e.AbilityKey is "main-hand-auto" or "off-hand-auto").ToArray();
        Assert.Contains(hits, e => e.WeaponHandKey == axeHand && e.IsCritical);
        Assert.DoesNotContain(hits, e => e.WeaponHandKey != axeHand && e.IsCritical);
        request.TalentRanks["weaponmaster"] = 0;
        var baseline = new ForeverWarriorSimRunner().Run(request);
        Assert.DoesNotContain(baseline.Batch.RepresentativeRun.Timeline, e => e.Type == CombatEventType.Damage && e.IsCritical);
    }

    [Theory]
    [InlineData("sword", "mace", "main-hand")]
    [InlineData("mace", "sword", "off-hand")]
    public void SwordExtrasRemainRestrictedToTheSwordHand(string main, string off, string swordHand)
    {
        var (context, warrior, target) = Actors();
        var request = Request(main, off);
        var executor = new AbilityExecutor(new AuraManager(), new RulesetCombatRollResolver(Rules()),
            new WarriorWeaponmasterCombatModifiers(request, Rules()));
        var autos = new AutoAttackProcessor(executor, new ForeverAutoAttackTimingProvider());
        var mh = ForeverWarriorAutoAttackFactory.CreateDualWieldMainHand(
            "main-hand-auto", "MH", 100m, 100m, 2m, "main-hand-weapon-skill");
        var oh = ForeverWarriorAutoAttackFactory.CreateDualWieldOffHand(
            "off-hand-auto", "OH", 100m, 100m, 2m, "off-hand-weapon-skill");
        var processor = new WarriorWeaponTalentProcessor(request, autos, executor, mh, oh);
        void Hit(string hand, string ability) => processor.Process(context, new CombatEvent
        {
            Type = CombatEventType.Damage, SourceActorKey = warrior.Key, TargetActorKey = target.Key,
            WeaponHandKey = hand, AbilityKey = ability, Amount = 100m
        });
        for (var i = 0; i < 500; i++)
        {
            Hit(swordHand == "main-hand" ? "off-hand" : "main-hand", "whirlwind");
            Hit(swordHand, "shield-bash");
            Hit(swordHand, "shield-slam");
        }
        Assert.False(context.TryGetNextEvent(out _));
        for (var i = 0; i < 500; i++) Hit(swordHand, "whirlwind");
        var extras = new List<CombatEvent>();
        while (context.TryGetNextEvent(out var next))
        {
            if (next?.AbilityKey != WarriorWeaponTalentProcessor.SwordExtraAttackKey ||
                next.Type != CombatEventType.Damage) continue;
            extras.Add(next);
            processor.Process(context, next); // Extra attacks cannot recursively proc themselves.
        }
        Assert.NotEmpty(extras);
        Assert.All(extras, e => Assert.Equal(swordHand, e.WeaponHandKey));
    }
}
