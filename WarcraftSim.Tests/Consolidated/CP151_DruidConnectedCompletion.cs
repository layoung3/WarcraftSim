using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class Cp151DruidConnectedCompletionTests
{
    [Fact]
    public void BearProjectionKeepsTheFixedDurationAfterLethalDamage()
    {
        var result = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "bear", DurationSeconds = 28m, CaptureTimeline = true,
            EnableBossAttacks = true, BossHitDamage = 8500m,
            BossSwingSeconds = 2m, BearBaseArmor = 0m, BearDodgePercent = 0m
        });
        Assert.Equal(0m, result.ProjectedEndingHealth);
        Assert.Equal(0m, result.MinimumProjectedHealth);
        Assert.True(result.DamageTaken > 0m);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.SimulationEnded);
        Assert.Equal(result.ProjectedEndingHealth,
            result.Run.CustomMetrics["bear-projected-ending-health"]);
    }

    [Fact]
    public void FrenziedRegenerationHasEffectiveAndWastedProjectedRecovery()
    {
        var request = new FeralDruidSimRequest {
            Role = "bear", DurationSeconds = 34m, UseFeralCooldowns = true,
            EnableBossAttacks = true, BossHitDamage = 350m,
            BossSwingSeconds = 2.5m, BearBaseArmor = 0m, BearDodgePercent = 0m
        };
        var result = new FeralDruidSimRunner().Run(request);
        Assert.True(result.FrenziedRegenerationHealing > 0m);
        Assert.True(result.EffectiveSelfHealing > 0m);
        Assert.Equal(result.FrenziedRegenerationHealing,
            result.EffectiveSelfHealing + result.WastedSelfHealing);
        Assert.InRange(result.ProjectedEndingHealth, 0m, request.MaximumHealth);
        Assert.Equal(result.EffectiveSelfHealing,
            result.Run.CustomMetrics["bear-projected-effective-self-healing"]);
    }

    [Fact]
    public void GrowlUsesSharedForcedTargetStateAndExpires()
    {
        var result = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "bear", DurationSeconds = 24m, CaptureTimeline = true,
            UseBearUtility = true, CompetingThreatOnBoss = 100000m
        });
        Assert.Contains(result.Run.Timeline, e =>
            e.Type == CombatEventType.ForcedTargetApplied &&
            e.AbilityKey == "growl" && e.ForcedTargetActorKey == "druid");
        Assert.Contains(result.Run.Timeline, e =>
            e.Type == CombatEventType.ForcedTargetRemoved &&
            e.ForcedTargetActorKey == "druid");
    }

    [Fact]
    public void FeralChargeUsesABearOnlyTimedTargetAura()
    {
        var talents = new ForeverFeralDruidTalents();
        talents.Ranks["feral-charge"] = 1;
        var ability = Assert.Single(ForeverFeralDruidSpellbook.Create(talents,new()),
            x => x.Key == "feral-charge");
        Assert.Contains("druid-dire-bear-form", ability.RequiredSourceAuraKeys);
        Assert.Equal(4m, Assert.Single(ability.Effects).DurationSeconds);
        var result = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "bear", DurationSeconds = 25m, UseBearUtility = true,
            CaptureTimeline = true, CompetingThreatOnBoss = 100000m,
            Talents = talents
        });
        Assert.Contains(result.Run.Timeline, e =>
            e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == "feral-charge");
    }

    [Fact]
    public void FaerieFireChangesActualPhysicalDamageAfterArmor()
    {
        static FeralDruidSimResponse Sim(bool faerie) =>
            new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
                Role = "bear", DurationSeconds = 30m,
                UseFaerieFire = faerie, TargetArmor = 8500m,
                CaptureTimeline = true,
                Tuning = new ForeverFeralDruidTuning {
                    MeleeHitPercent = 100m, MeleeCritPercent = 0m
                }
            });
        var baseline = Sim(false);
        var debuffed = Sim(true);
        Assert.Contains(debuffed.Run.Timeline, e =>
            e.Type == CombatEventType.AuraApplied && e.AbilityKey == "faerie-fire");
        var plainAuto = baseline.Run.Timeline.First(e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "feral-auto");
        var boostedAuto = debuffed.Run.Timeline.First(e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "feral-auto");
        Assert.True(boostedAuto.Amount > plainAuto.Amount);
    }

    [Fact]
    public void ThornsDealsReactiveDamageAndAttributesThreat()
    {
        var result = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "bear", DurationSeconds = 22m,
            UseThorns = true, EnableBossAttacks = true,
            BearDodgePercent = 0m, BossHitDamage = 100m,
            BossSwingSeconds = 2m, CaptureTimeline = true
        });
        Assert.Contains(result.Run.Timeline, e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "thorns" && e.Amount > 0m);
        Assert.True(result.Run.CustomMetrics["druid-thorns-hits"] > 0m);
        Assert.True(result.DamageByAbility.GetValueOrDefault("thorns") > 0m);
    }

    [Fact]
    public void TranquilSpiritReducesHealingTouchAndTranquilityManaCosts()
    {
        var plain = ForeverRestorationDruidSpellbook.Create(new(),new());
        var talented = new ForeverRestorationDruidTalents();
        talented.Ranks["tranquil-spirit"] = 5;
        var spells = ForeverRestorationDruidSpellbook.Create(talented,new());
        foreach (var key in new[] { "healing-touch", "tranquility" })
        {
            var original = Assert.Single(plain, x => x.Key == key).ResourceCosts[0].Amount;
            var discounted = Assert.Single(spells, x => x.Key == key).ResourceCosts[0].Amount;
            Assert.Equal(original * .9m, discounted);
        }
    }

    [Fact]
    public void ImprovedTranquilityReducesActualHealingThreatContributions()
    {
        var ctx = new SimulationContext(new SimulationRunOptions {
            DurationSeconds = 10m, PrimaryActorKey = "druid"
        });
        var druid = new SimulationActorState {
            Key = "druid", TeamKey = "raid", MaximumHealth = 1000m
        };
        druid.InitializeHealth(1000m);
        var boss = new SimulationActorState {
            Key = "boss", TeamKey = "enemy", MaximumHealth = 1000m
        };
        boss.InitializeHealth(1000m);
        ctx.AddActor(druid);
        ctx.AddActor(boss);
        var heal = new CombatEvent {
            TimeSeconds = 1m, Type = CombatEventType.Healing,
            AbilityKey = "tranquility", SourceActorKey = "druid",
            TargetActorKey = "druid", Amount = 500m
        };
        decimal ThreatAtRank(int rank) => Assert.Single(
            new RestorationDruidSimRunner.DruidHealingThreatResolver(.5m, rank)
                .Resolve(ctx, heal)).Amount;
        Assert.Equal(250m, ThreatAtRank(0));
        Assert.Equal(125m, ThreatAtRank(1));
        Assert.Equal(0m, ThreatAtRank(2));
    }

    [Fact]
    public void LivingSpiritIncreasesExecutableManaRegenRate()
    {
        var low = new RestorationDruidSimRunner().Run(new RestorationDruidSimRequest {
            DurationSeconds = 25m
        });
        var talented = new RestorationDruidSimRequest { DurationSeconds = 25m };
        talented.Talents.Ranks["living-spirit"] = 3;
        var high = new RestorationDruidSimRunner().Run(talented);
        Assert.True(high.Run.CustomMetrics["druid-mana-regeneration-per-second"] >
            low.Run.CustomMetrics["druid-mana-regeneration-per-second"]);
        Assert.Equal(talented.Tuning.BaseSpirit * 1.15m,
            high.Run.CustomMetrics["druid-spirit-after-living-spirit"]);
    }
}
