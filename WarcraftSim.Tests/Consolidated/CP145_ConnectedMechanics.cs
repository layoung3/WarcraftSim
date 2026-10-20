using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;
using WarcraftSim.Data.Forever.Rogue;
using Xunit;

namespace WarcraftSim.Tests;

/// <summary>
/// CP145 integration tests deliberately assert changed combat outcomes, not merely
/// the existence of registered abilities/talent identifiers. Constants are
/// provisional where the WoW Forever level-60 client data is not published.
/// </summary>
public sealed class Cp145ConnectedMechanicsTests
{
    [Fact]
    public void ImprovedExposeArmorLowersActualEnergyCostByRank()
    {
        var req = RogueSimRequest.CreatePreset("assassination");
        var original = ForeverRogueSpellbook.Create("assassination", req.Talents,
            req.Tuning, 400m, bothDaggers: true);
        req.Talents.Ranks["improved-expose-armor"] = 2;
        var modified = ForeverRogueSpellbook.Create("assassination", req.Talents,
            req.Tuning, 400m, bothDaggers: true);
        var regularCost = Assert.Single(original, a => a.Key == "expose-armor")
            .ResourceCosts[0].Amount;
        var discountedCost = Assert.Single(modified, a => a.Key == "expose-armor")
            .ResourceCosts[0].Amount;
        Assert.Equal(regularCost - 2m * req.Tuning.ImprovedExposeArmorEnergyDiscountPerRank,
            discountedCost);
    }

    [Fact]
    public void LethalityChangesActualAssassinationCriticalDamage()
    {
        var baseline = RogueSimRequest.CreatePreset("assassination");
        baseline.DurationSeconds = 90m;
        baseline.Tuning.MainHandHitPercent = 100m;
        baseline.Tuning.OffHandHitPercent = 100m;
        baseline.Tuning.PhysicalCritPercent = 100m;
        var first = new RogueSimRunner().Run(baseline);
        Assert.True(first.DamageByAbility.TryGetValue("mutilate", out var normal));
        baseline.Talents.Ranks["lethality"] = 5;
        var second = new RogueSimRunner().Run(baseline);
        Assert.True(second.DamageByAbility.TryGetValue("mutilate", out var boosted));
        Assert.True(boosted > normal);
    }

    [Fact]
    public void ObsoleteDeadlinessIsRejectedInsteadOfApplyingAnInventedForeverBonus()
    {
        var request = RogueSimRequest.CreatePreset("subtlety");
        request.Talents.Ranks["deadliness"] = 5;
        Assert.Throws<ArgumentException>(() => request.Talents.Validate(60, "subtlety"));
    }

    [Fact]
    public void UtilityPreviewUsesTheActualAbilityAndAuraPipeline()
    {
        var req = RogueSimRequest.CreatePreset("combat");
        req.CaptureTimeline = true;
        req.UseUtilityAbilities = true;
        req.DurationSeconds = 45m;
        var result = new RogueSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted &&
            e.AbilityKey == "evasion");
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.AuraApplied &&
            e.AbilityKey == "evasion");
    }

    [Fact]
    public void AncestralHealingArmorChangesIncomingDamageAfterCriticalHeals()
    {
        RestorationShamanSimRequest Scenario() => new() {
            Seed = 145001, CaptureTimeline = true, DurationSeconds = 45m,
            AllyPhysicalArmor = 3500m, IncomingDamage = 1000m,
            FirstDamageAtSeconds = 2m, DamageEverySeconds = 2m,
            AllyInitialHealthPercent = 45m, AdditionalAllies = 0,
            Tuning = new() { HealingCritPercent = 100m }
        };
        var without = Scenario();
        var first = new RestorationShamanSimRunner().Run(without);
        var withArmorBuff = Scenario();
        withArmorBuff.Talents.Ranks["ancestral-healing"] = 3;
        var second = new RestorationShamanSimRunner().Run(withArmorBuff);
        Assert.Contains(second.Run.Timeline, e => e.Type == CombatEventType.AuraApplied &&
            e.AbilityKey == "ancestral-healing");
        Assert.True(second.ArmorPreventedDamage > first.ArmorPreventedDamage);
        Assert.Contains(second.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "encounter:restoration-training-pressure" &&
            e.MitigatedAmount is > 0m && e.RawAmount > e.Amount);
    }

    [Fact]
    public void ChainHealBouncesRespectExplicitFriendlyEligibility()
    {
        var req = new RestorationShamanSimRequest {
            CaptureTimeline = true, Seed = 145002, DurationSeconds = 90m,
            AdditionalAllies = 2, DamageAdditionalAllies = true,
            AllyInitialHealthPercent = 40m, IncomingDamage = 1100m,
            DamageEverySeconds = 2m,
            ChainHealEligibleTargetKeys = ["raid-1"]
        };
        var outcome = new RestorationShamanSimRunner().Run(req);
        var bounces = outcome.Run.Timeline.Where(e => e.Type == CombatEventType.Healing &&
            e.EffectKey is not null && e.EffectKey.StartsWith("chain-heal-bounce-", StringComparison.Ordinal));
        Assert.NotEmpty(bounces);
        Assert.All(bounces, b => Assert.Equal("raid-1", b.TargetActorKey));
    }

    [Fact]
    public void DruidCanShiftFromCatToBearAndBackDuringTheSimulation()
    {
        var req = new FeralDruidSimRequest {
            Role = "cat", CaptureTimeline = true, DurationSeconds = 50m,
            FormTransitions = [
                new() { AtSeconds = 8m, Form = "bear" },
                new() { AtSeconds = 22m, Form = "cat" }
            ]
        };
        var result = new FeralDruidSimRunner().Run(req);
        Assert.Equal(3, result.FormChanges);
        Assert.Equal(0m, result.Run.CustomMetrics["feral-failed-form-shifts"]);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.AuraApplied &&
            e.AbilityKey == "shapeshift" && e.EffectKey == "form-transition");
        Assert.True(result.Run.Timeline.Count(e => e.Type == CombatEventType.AutoAttackStarted &&
            e.AbilityKey == "feral-auto") >= 3);
    }

    [Fact]
    public void BearTankPreservesHealthAndTracksIncomingBurstAndThreat()
    {
        var req = new FeralDruidSimRequest {
            Role = "bear", EnableBossAttacks = true, DurationSeconds = 60m,
            BossSwingSeconds = 2m, BossHitDamage = 1500m,
            BearDodgePercent = 0m, CaptureTimeline = true
        };
        var result = new FeralDruidSimRunner().Run(req);
        Assert.True(result.DamageTaken > 0m);
        Assert.True(result.MaximumBurstDamage > 0m);
        Assert.True(result.MaximumBurstPercentOfHealth > 0m);
        Assert.True(result.ThreatGenerated > 0m);
        Assert.True(result.Run.Summary.ActorSummaries["druid"].Died == false);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.TargetActorKey == "druid" && e.AbilityKey == "encounter:feral-boss-melee");
    }

    [Fact]
    public void ThickHideMakesBearMitigateMoreActualIncomingDamage()
    {
        var request = new FeralDruidSimRequest {
            Role = "bear", EnableBossAttacks = true, DurationSeconds = 40m,
            BossHitDamage = 1000m, BossSwingSeconds = 2m, BearDodgePercent = 0m
        };
        var without = new FeralDruidSimRunner().Run(request);
        request.Talents.Ranks["thick-hide"] = 3;
        var buffed = new FeralDruidSimRunner().Run(request);
        Assert.True(buffed.DamageTaken < without.DamageTaken);
        Assert.True(buffed.ArmorPreventedDamage > without.ArmorPreventedDamage);
    }

    [Fact]
    public void FeralInstinctIncreasesSwipeDamageWithoutApplyingObsoleteThreatMultiplier()
    {
        var request = new FeralDruidSimRequest { Role = "bear", DurationSeconds = 60m };
        var normal = ForeverFeralDruidSpellbook.Create(request.Talents, request.Tuning);
        var baseline = Assert.Single(normal, a => a.Key == "swipe").Effects[0].MinimumValue;
        request.Talents.Ranks["feral-instinct"] = 3;
        var modified = ForeverFeralDruidSpellbook.Create(request.Talents, request.Tuning);
        var bonus = Assert.Single(modified, a => a.Key == "swipe").Effects[0].MinimumValue;
        Assert.True(bonus > baseline);
        Assert.Equal(baseline * (1m + 3m * request.Tuning.FeralInstinctSwipeDamagePercentPerRank / 100m), bonus);
    }

    [Fact]
    public void DruidShapeshiftEnforcesVerifiedSixSecondCooldown()
    {
        var actor = new SimulationActorState { Key = "druid", MaximumHealth = 5000m };
        actor.InitializeHealth(5000m);
        actor.AddResource(new ResourceState("mana", 1000m, 1000m));
        actor.AddResource(new ResourceState("energy", 100m, 0m));
        var state = new ForeverDruidFormState(5, 100m);
        Assert.True(state.Shift(actor, ForeverDruidForm.Cat, 0m));
        Assert.False(state.Shift(actor, ForeverDruidForm.DireBear, 5.99m));
        Assert.True(state.Shift(actor, ForeverDruidForm.DireBear, 6m));
        Assert.Equal(800m, actor.Resources["mana"].Current);
    }
}
