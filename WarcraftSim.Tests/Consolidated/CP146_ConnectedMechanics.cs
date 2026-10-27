using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests;

/// <summary>
/// CP146 behavioral regression tests. Values are synthetic, so assertions check
/// directions, source-linked event behavior and exact verified GCD changes,
/// without asserting unverified level-60 damage tables.
/// </summary>
public sealed class Cp146ConnectedMechanicsTests
{
    [Fact]
    public void BearOutgoingDamageWritesToSharedThreatTable()
    {
        var req = new FeralDruidSimRequest { Role = "bear", DurationSeconds = 45m,
            CaptureTimeline = true };
        var result = new FeralDruidSimRunner().Run(req);
        Assert.True(result.ThreatGenerated > 0m);
        Assert.Equal(result.ThreatGenerated, result.Run.CustomMetrics["bear-shared-threat-table"]);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.ThreatChanged &&
            e.SourceActorKey == "druid" && e.TargetActorKey == "target" && e.Amount is > 0m);
    }

    [Fact]
    public void SwipeDamagesAtMostTwoAdditionalEnemies()
    {
        var req = new FeralDruidSimRequest { Role = "bear", DurationSeconds = 60m,
            AdditionalTargets = 3, CaptureTimeline = true };
        var run = new FeralDruidSimRunner().Run(req);
        Assert.True(run.Run.CustomMetrics["bear-swipe-secondary-hits"] > 0m);
        var cleaves = run.Run.Timeline.Where(e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "swipe-secondary").ToList();
        Assert.NotEmpty(cleaves);
        Assert.DoesNotContain(cleaves, e => e.TargetActorKey == "target");
        Assert.DoesNotContain(cleaves, e => e.TargetActorKey == "target-3");
    }

    [Fact]
    public void PurificationChangesDirectAndMultiTargetHealAmounts()
    {
        var req = new RestorationShamanSimRequest { DurationSeconds = 14m,
            AllyMaximumHealth = 100000m, AllyInitialHealthPercent = 20m,
            AdditionalAllies = 0, IncomingDamage = 0m,
            Tuning = new ForeverRestorationShamanTuning { HealingCritPercent = 0m } };
        var baseResult = new RestorationShamanSimRunner().Run(req);
        req.Talents.Ranks["purification"] = 5;
        var improved = new RestorationShamanSimRunner().Run(req);
        Assert.True(improved.HealingDone > baseResult.HealingDone);
        var spells = ForeverRestorationShamanSpellbook.Create(req.Talents, req.Tuning);
        var wave = Assert.Single(spells, a => a.Key == "healing-wave");
        Assert.True(wave.Effects[0].MinimumValue > req.Tuning.HealingWaveBase);
    }

    [Fact]
    public void NatureSwiftnessIsConsumedByAnEligibleHealingCast()
    {
        var request = new RestorationShamanSimRequest {
            DurationSeconds = 35m, AllyMaximumHealth = 100000m,
            AllyInitialHealthPercent = 10m, AdditionalAllies = 0,
            IncomingDamage = 0m, CaptureTimeline = true
        };
        request.Talents.Ranks["nature-swiftness"] = 1;
        var response = new RestorationShamanSimRunner().Run(request);
        Assert.Contains(response.Run.Timeline, e => e.Type == CombatEventType.AuraApplied &&
            e.AbilityKey == "nature-swiftness");
        Assert.True(response.Run.CustomMetrics["shaman-nature-swiftness-heals"] > 0m);
    }

    [Fact]
    public void RogueEvasionPreventsSomeActualScriptedIncomingPhysicalDamage()
    {
        RogueSimRequest Scenario(bool utility)
        {
            var req = RogueSimRequest.CreatePreset("combat");
            req.StartInStealth = false;
            req.DurationSeconds = 35m;
            req.EnableBossAttacks = true;
            req.BossSwingSeconds = 2m;
            req.BossHitDamage = 300m;
            req.UseUtilityAbilities = utility;
            req.Tuning.BaseDodgePercent = 0m;
            req.Tuning.EvasionBonusDodgePercent = 100m;
            return req;
        }
        var without = new RogueSimRunner().Run(Scenario(false));
        var with = new RogueSimRunner().Run(Scenario(true));
        Assert.True(with.Run.CustomMetrics["rogue-boss-attacks-dodged"] > 0m);
        Assert.True(with.Run.Summary.ActorSummaries["rogue"].DamageTaken <
                    without.Run.Summary.ActorSummaries["rogue"].DamageTaken);
    }

    [Fact]
    public void RogueFeintUsesActualEnemyThreatTable()
    {
        var req = RogueSimRequest.CreatePreset("combat");
        req.StartInStealth = false;
        req.UseUtilityAbilities = true;
        req.DurationSeconds = 90m;
        req.CaptureTimeline = true;
        var result = new RogueSimRunner().Run(req);
        Assert.True(result.Run.CustomMetrics["rogue-boss-threat"] >= 0m);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.ThreatChanged &&
            e.SourceActorKey == "rogue" && e.TargetActorKey == "target" && e.Amount is > 0m);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted &&
            e.AbilityKey == "feint");
    }

    [Fact]
    public void GiftOfEarthmotherReducesOnlyEligibleHealingGlobalCooldowns()
    {
        var talents = new ForeverRestorationDruidTalents { Ranks = new() {
            ["gift-of-the-earthmother"] = 1, ["living-spirit"] = 3,
            ["swiftmend"] = 1,
            ["wild-growth"] = 1
        } };
        var t = new ForeverRestorationDruidTuning();
        var spells = ForeverRestorationDruidSpellbook.Create(talents, t);
        foreach (var name in new[] { "rejuvenation", "swiftmend", "wild-growth" })
            Assert.Equal(1m, Assert.Single(spells, a => a.Key == name).GlobalCooldownSeconds);
        Assert.Equal(1.5m, Assert.Single(spells, a => a.Key == "healing-touch").GlobalCooldownSeconds);
    }
}
