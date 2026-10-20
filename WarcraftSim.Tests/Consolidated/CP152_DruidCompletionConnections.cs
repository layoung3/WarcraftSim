using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class Cp152DruidCompletionConnectionsTests
{
    [Fact]
    public void FeralRankCapsAndSwipeDamageMatchClientDerivedThreeRanks()
    {
        var talents = new ForeverFeralDruidTalents();
        talents.Ranks["feral-instinct"] = 3;
        var standard = Assert.Single(ForeverFeralDruidSpellbook.Create(new(), new()),
            a => a.Key == "swipe").Effects[0].MinimumValue;
        var modified = Assert.Single(ForeverFeralDruidSpellbook.Create(talents, new()),
            a => a.Key == "swipe").Effects[0].MinimumValue;
        Assert.Equal(standard * 1.30m, modified);
        talents.Ranks["feral-instinct"] = 4;
        Assert.Throws<ArgumentException>(() => talents.Validate());
        talents.Ranks.Clear();
        talents.Ranks["thick-hide"] = 4;
        Assert.Throws<ArgumentException>(() => talents.Validate());
    }

    [Fact]
    public void NaturalInstinctChangesActualFeralCriticalDamage()
    {
        static decimal FirstCrit(int rank)
        {
            var talents = new ForeverFeralDruidTalents();
            talents.Ranks["natural-instinct"] = rank;
            var r = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
                Role = "cat", Seed = 15255, DurationSeconds = 18m,
                CaptureTimeline = true, Talents = talents,
                Tuning = new ForeverFeralDruidTuning { MeleeHitPercent = 100m,
                    MeleeCritPercent = 100m }
            });
            return r.Run.Timeline.First(e => e.Type == CombatEventType.Damage &&
                e.AbilityKey == "feral-auto" && e.Amount > 0m).Amount!.Value;
        }
        Assert.True(FirstCrit(2) > FirstCrit(0));
        var invalid = new ForeverFeralDruidTalents();
        invalid.Ranks["natural-instinct"] = 3;
        Assert.Throws<ArgumentException>(() => invalid.Validate());
    }

    [Fact]
    public void BalanceFaerieFireAppliesAndRefreshesTargetDebuff()
    {
        var result = new BalanceDruidSimRunner().Run(new BalanceDruidSimRequest {
            Seed = 15211, DurationSeconds = 45m, CaptureTimeline = true,
            BaseSpellHitPercent = 100m, SpellCritPercent = 0m,
            UseFaerieFire = true, CustomPriority = ["faerie-fire", "wrath"]
        });
        Assert.Contains(result.Run.Timeline, e =>
            e.Type == CombatEventType.AuraApplied && e.AbilityKey == "faerie-fire" &&
            e.TargetActorKey == "target");
        Assert.True(result.Run.Timeline.Count(e =>
            e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == "faerie-fire") >= 2);
        var ability = Assert.Single(ForeverBalanceDruidSpellbook.Create(new(), new(), 0m),
            a => a.Key == "faerie-fire");
        Assert.Equal(115m, Assert.Single(ability.ResourceCosts).Amount);
        Assert.Equal(40m, Assert.Single(ability.Effects).DurationSeconds);
    }

    [Fact]
    public void HurricaneDealsIndependentlyResolvedDamageToAdditionalEnemies()
    {
        var result = new BalanceDruidSimRunner().Run(new BalanceDruidSimRequest {
            Seed = 15222, DurationSeconds = 18m, CaptureTimeline = true,
            AdditionalTargets = 2, HurricaneSecondaryTargetLimit = 2,
            BaseSpellHitPercent = 100m, SpellCritPercent = 0m,
            CustomPriority = ["hurricane"]
        });
        var secondaries = result.Run.Timeline.Where(e =>
            e.Type == CombatEventType.Damage &&
            e.AbilityKey == "hurricane-secondary" && e.Amount > 0m).ToList();
        Assert.NotEmpty(secondaries);
        Assert.Contains(secondaries, e => e.TargetActorKey == "target-1");
        Assert.Contains(secondaries, e => e.TargetActorKey == "target-2");
        Assert.True(result.Run.CustomMetrics["balance-hurricane-secondary-hits"] > 0m);
    }

    [Fact]
    public void BalanceThornsTriggersOnlyWhenScriptedBossHitsArePresent()
    {
        static BalanceDruidSimResponse Run(decimal hit) =>
            new BalanceDruidSimRunner().Run(new BalanceDruidSimRequest {
                Seed = 15233, DurationSeconds = 12m, CaptureTimeline = true,
                UseThorns = true, IncomingBossHitDamage = hit,
                BossSwingIntervalSeconds = 3m
            });
        var passive = Run(0m);
        var attacked = Run(40m);
        Assert.Equal(0m, passive.Run.CustomMetrics["balance-thorns-hits"]);
        Assert.True(attacked.Run.CustomMetrics["balance-thorns-hits"] > 0m);
        Assert.Contains(attacked.Run.Timeline, e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "thorns" && e.Amount > 0m);
    }

    [Fact]
    public void SwiftmendHealsForTheFullCasterOwnedRejuvenationAndDoesNotConsumeIt()
    {
        var talents = new ForeverRestorationDruidTalents();
        talents.Ranks["swiftmend"] = 1;
        var tuning = new ForeverRestorationDruidTuning {
            CriticalChancePercent = 0m, SpellHealingPower = 0m,
            RejuvenationTick = 200m, RegrowthTick = 50m
        };
        var result = new RestorationDruidSimRunner().Run(new RestorationDruidSimRequest {
            Seed = 15244, DurationSeconds = 20m, CaptureTimeline = true,
            IncomingDamage = 0m, AllyStartingHealthPercent = 30m,
            SwiftmendHealthThresholdPercent = 100m,
            Talents = talents, Tuning = tuning
        });
        // Rejuvenation has four 3s ticks across its 12s full duration.
        Assert.Contains(result.Run.Timeline, e =>
            e.Type == CombatEventType.Healing && e.AbilityKey == "swiftmend" &&
            e.RawAmount == 800m);
        var swift = result.Run.Timeline.First(e =>
            e.Type == CombatEventType.Healing && e.AbilityKey == "swiftmend");
        Assert.Contains(result.Run.Timeline, e =>
            e.Type == CombatEventType.Healing && e.AbilityKey == "rejuvenation" &&
            e.TargetActorKey == swift.TargetActorKey &&
            e.TimeSeconds > swift.TimeSeconds);
    }

    [Fact]
    public void SwiftmendAmountRejectsAnotherDruidsHot()
    {
        var talents = new ForeverRestorationDruidTalents();
        talents.Ranks["swiftmend"] = 1;
        var caster = new SimulationActorState { Key = "druid", MaximumHealth = 1000m };
        caster.InitializeHealth(1000m);
        foreach (var ability in ForeverRestorationDruidSpellbook.Create(talents, new()))
            caster.AddAbility(ability);
        var recipient = new SimulationActorState { Key = "ally", MaximumHealth = 1000m };
        recipient.InitializeHealth(1000m);
        recipient.ActiveAuras.Add(new AuraInstance {
            SourceActorKey = "other-druid", TargetActorKey = "ally",
            Definition = new AuraDefinition { Key = "rejuvenation", DurationSeconds = 12m },
            AppliedAtSeconds = 0m, ExpiresAtSeconds = 12m
        });
        Assert.Equal(0m, DruidSwiftmendHealing.FullDurationAmount(caster,recipient,2m));
        recipient.ActiveAuras[0].SourceActorKey = "druid";
        Assert.True(DruidSwiftmendHealing.FullDurationAmount(caster,recipient,2m) > 0m);
    }
}
