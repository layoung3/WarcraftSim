using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;
using Xunit;

namespace WarcraftSim.Tests;

/// <summary>
/// CP149 asserts actual connected behavior where possible. Synthetic level-60
/// coefficients are NOT a claim of measured live-game balance.
/// </summary>
public sealed class Cp149DruidConnectedMechanicsTests
{
    [Fact]
    public void ShiftingPowerCooldownGetsFourSecondsPerImprovedRank()
    {
        var talents = new ForeverFeralDruidTalents();
        talents.Ranks["shredding-attacks"] = 3;
        talents.Ranks["shifting-power"] = 1;
        var normal = Assert.Single(ForeverFeralDruidSpellbook.Create(talents, new()),
            a => a.Key == "shifting-power");
        Assert.Equal(16m, normal.CooldownSeconds);
        talents.Ranks["improved-shifting-power"] = 2;
        var improved = Assert.Single(ForeverFeralDruidSpellbook.Create(talents, new()),
            a => a.Key == "shifting-power");
        Assert.Equal(8m, improved.CooldownSeconds);
        talents.Ranks.Remove("shifting-power");
        Assert.Throws<ArgumentException>(() => talents.Validate());
    }

    [Fact]
    public void FeralCatOpenerRequiresProwlAndCorrectForm()
    {
        var spells = ForeverFeralDruidSpellbook.Create(new(), new());
        foreach (var key in new[] { "pounce", "ravage" })
        {
            var a = Assert.Single(spells, x => x.Key == key);
            Assert.Contains("druid-prowl", a.RequiredSourceAuraKeys);
            Assert.Contains("druid-cat-form", a.RequiredSourceAuraKeys);
        }
        var request = new FeralDruidSimRequest { Role = "cat", StartInProwl = true,
            Tuning = new ForeverFeralDruidTuning { MeleeHitPercent = 100m },
            CaptureTimeline = true, DurationSeconds = 35m };
        var result = new FeralDruidSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted &&
            e.AbilityKey == "ravage");
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.AuraRemoved &&
            e.AbilityKey == "prowl");
    }

    [Fact]
    public void CatAutoAttackDoesNotStartBeforeStealthOpener()
    {
        var request = new FeralDruidSimRequest { Role = "cat", StartInProwl = true,
            DurationSeconds = 35m, CaptureTimeline = true,
            Tuning = new ForeverFeralDruidTuning { MeleeHitPercent = 100m } };
        var result = new FeralDruidSimRunner().Run(request);
        var opener = Assert.Single(result.Run.Timeline, e =>
            e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == "ravage");
        Assert.DoesNotContain(result.Run.Timeline,e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "feral-auto" &&
            e.TimeSeconds < opener.TimeSeconds);
        Assert.Contains(result.Run.Timeline,e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "feral-auto" &&
            e.TimeSeconds > opener.TimeSeconds);
    }

    [Fact]
    public void PounceFromProwlGrantsAComboPointOnDebuffApplication()
    {
        var req=new FeralDruidSimRequest { Role="cat",StartInProwl=true,
            PreferredCatOpener="pounce",CaptureTimeline=true,DurationSeconds=9m,
            Tuning=new ForeverFeralDruidTuning { MeleeHitPercent=100m } };
        var response=new FeralDruidSimRunner().Run(req);
        Assert.Contains(response.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="pounce");
        Assert.True(response.ComboPoints>=1);
    }

    [Fact]
    public void BearUtilityCreatesRealTauntAndDemoralizingState()
    {
        var request = new FeralDruidSimRequest { Role="bear",UseBearUtility=true,
            CompetingThreatOnBoss=100000m, CaptureTimeline=true,DurationSeconds=45m,
            Tuning = new ForeverFeralDruidTuning { MeleeHitPercent = 100m } };
        var outcome = new FeralDruidSimRunner().Run(request);
        Assert.Contains(outcome.Run.Timeline, e => e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="growl");
        Assert.Contains(outcome.Run.Timeline, e => e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="demoralizing-roar");
        Assert.True(outcome.Run.CustomMetrics["bear-shared-threat-table"] >= 100000m);
    }

    [Fact]
    public void FeralPredatoryStrikesIncreasesResolvedCriticalDamage()
    {
        var req = new FeralDruidSimRequest { Role="cat",DurationSeconds=35m,
            Tuning=new ForeverFeralDruidTuning { MeleeHitPercent=100m,
                MeleeCritPercent=100m } };
        var without = new FeralDruidSimRunner().Run(req);
        req.Talents.Ranks["predatory-strikes"] = 3;
        var with = new FeralDruidSimRunner().Run(req);
        Assert.True(with.DamageDone > without.DamageDone);
    }

    [Fact]
    public void BalanceHurricaneIsARealChannelWithNoCooldown()
    {
        var tuning = new ForeverBalanceDruidTuning();
        var talents = new ForeverBalanceDruidTalents();
        var hurricane = Assert.Single(ForeverBalanceDruidSpellbook.Create(talents,tuning,200m),
            a => a.Key=="hurricane");
        Assert.Equal(0m,hurricane.CooldownSeconds);
        Assert.Equal(10m,hurricane.ChannelDurationSeconds);
        Assert.Equal(2m,hurricane.ChannelTickIntervalSeconds);
        Assert.True(hurricane.Effects[0].ApplyOnChannelTick);
        var req = new BalanceDruidSimRequest { CaptureTimeline=true,
            DurationSeconds=28m,BaseSpellHitPercent=100m,CustomPriority=["hurricane"] };
        var result = new BalanceDruidSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="hurricane" && e.IsChannelTick && e.Amount is >0m);
    }

    [Fact]
    public void NaturalistImprovesHealingTouchCastAndSubtletyControlsHealingThreat()
    {
        var t = new ForeverRestorationDruidTalents();
        var normal = Assert.Single(ForeverRestorationDruidSpellbook.Create(t,new()),
            a=>a.Key=="healing-touch");
        Assert.Equal(3.5m,normal.CastTimeSeconds);
        t.Ranks["naturalist"]=5;
        var enhanced=Assert.Single(ForeverRestorationDruidSpellbook.Create(t,new()),
            a=>a.Key=="healing-touch");
        Assert.Equal(3m,enhanced.CastTimeSeconds);
        var baseReq=new RestorationDruidSimRequest{DurationSeconds=40m};
        var plain=new RestorationDruidSimRunner().Run(baseReq);
        baseReq.Talents.Ranks["subtlety"]=3;
        var lowerThreat=new RestorationDruidSimRunner().Run(baseReq);
        Assert.Equal(plain.HealingDone,lowerThreat.HealingDone);
        Assert.True(plain.Run.CustomMetrics["druid-healing-threat"] > 0m);
        Assert.True(lowerThreat.Run.CustomMetrics["druid-healing-threat"] <
            plain.Run.CustomMetrics["druid-healing-threat"]);
    }

    [Fact]
    public void NaturesSwiftnessIsConsumedByAnEligibleHealInRealSimulation()
    {
        var req=new RestorationDruidSimRequest { CaptureTimeline=true,
            AdditionalAllies=0,DurationSeconds=55m,AllyStartingHealthPercent=10m,
            IncomingDamage=300m,DamageEverySeconds=5m };
        req.Talents.Ranks["natures-swiftness"]=1;
        var result=new RestorationDruidSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="natures-swiftness");
        Assert.True(result.Run.CustomMetrics["druid-natures-swiftness-consumed"]>0m);
    }

    [Fact]
    public void SeededRestorationHealingStillReproducesExactly()
    {
        var req=new RestorationDruidSimRequest {Seed=149101,DurationSeconds=60m};
        req.Talents.Ranks["natures-swiftness"]=1;
        var first=new RestorationDruidSimRunner().Run(req);
        var second=new RestorationDruidSimRunner().Run(req);
        Assert.Equal(first.HealingDone,second.HealingDone);
        Assert.Equal(first.EndingMana,second.EndingMana);
    }
}
