using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Druid;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class Cp153DruidAndApiSmokeTests
{
    [Fact]
    public void HeartOfTheWildUsesCurrentForeverRanksAndFormSpecificDeltas()
    {
        var t = new ForeverFeralDruidTuning {
            BaseStrength = 100m, BaseStamina = 100m, BaseIntellect = 100m,
            AttackPower = 400m, AttackPowerPerStrength = 2m,
            HealthPerStamina = 10m, ManaPerIntellect = 15m
        };
        var caster = ForeverDruidDerivedStats.HeartOfTheWild(t, 5,
            ForeverDruidForm.Caster, 5000m, 3000m);
        var cat = ForeverDruidDerivedStats.HeartOfTheWild(t, 5,
            ForeverDruidForm.Cat, 5000m, 3000m);
        var bear = ForeverDruidDerivedStats.HeartOfTheWild(t, 5,
            ForeverDruidForm.DireBear, 5000m, 3000m);
        Assert.Equal(3150m, caster.Mana);
        Assert.Equal(400m, caster.AttackPower);
        Assert.Equal(420m, cat.AttackPower);
        Assert.Equal(5000m, cat.Health);
        Assert.Equal(5200m, bear.Health);
        Assert.Equal(400m, bear.AttackPower);
        var talents = new ForeverFeralDruidTalents();
        talents.Ranks["heart-of-the-wild"] = 5;
        talents.Validate();
        talents.Ranks["heart-of-the-wild"] = 6;
        Assert.Throws<ArgumentException>(() => talents.Validate());
    }

    [Fact]
    public void HeartOfTheWildActuallyChangesFeralRunResourcesAndDamage()
    {
        var talents = new ForeverFeralDruidTalents();
        var baseline = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "cat", Seed = 15302, DurationSeconds = 22m, Talents = talents,
            Tuning = new ForeverFeralDruidTuning { MeleeHitPercent = 100m, MeleeCritPercent = 0m }
        });
        talents = new ForeverFeralDruidTalents();
        talents.Ranks["heart-of-the-wild"] = 5;
        var talented = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "cat", Seed = 15302, DurationSeconds = 22m, Talents = talents,
            Tuning = new ForeverFeralDruidTuning { MeleeHitPercent = 100m, MeleeCritPercent = 0m }
        });
        Assert.True(talented.Run.CustomMetrics["feral-heart-of-the-wild-attack-power"] >
            baseline.Run.CustomMetrics["feral-heart-of-the-wild-attack-power"]);
        Assert.True(talented.Run.CustomMetrics["feral-heart-of-the-wild-maximum-mana"] >
            baseline.Run.CustomMetrics["feral-heart-of-the-wild-maximum-mana"]);
        Assert.True(talented.DamageDone > baseline.DamageDone);
    }

    [Fact]
    public void ThickHideUsesLevelAndDefenseInsteadOfOldPercent()
    {
        Assert.Equal(0m, ForeverDruidDerivedStats.ThickHideBonusArmor(0, 60, 325m, 1m));
        Assert.Equal(60m + 25m * .67m,
            ForeverDruidDerivedStats.ThickHideBonusArmor(1, 60, 325m, 1m));
        Assert.Equal(180m + 25m * 2m,
            ForeverDruidDerivedStats.ThickHideBonusArmor(3, 60, 325m, 1m));
        var normal = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "bear", Seed = 15303, DurationSeconds = 24m,
            EnableBossAttacks = true, BearDodgePercent = 0m,
            Tuning = new ForeverFeralDruidTuning { DefenseSkill = 325m }
        });
        var ranks = new ForeverFeralDruidTalents();ranks.Ranks["thick-hide"] = 3;
        var protectedBear = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "bear", Seed = 15303, DurationSeconds = 24m,
            EnableBossAttacks = true, BearDodgePercent = 0m, Talents = ranks,
            Tuning = new ForeverFeralDruidTuning { DefenseSkill = 325m }
        });
        Assert.True(protectedBear.DamageTaken < normal.DamageTaken);
        Assert.Equal(230m, protectedBear.Run.CustomMetrics["bear-thick-hide-bonus-armor"]);
    }

    [Fact]
    public async Task AllFourDruidEndpointsRunAndSerializeRealResults()
    {
        var balance = new BalanceDruidSimController();
        var balanceDefaults = Assert.IsType<OkObjectResult>(balance.Defaults().Result);
        Assert.IsType<BalanceDruidSimRequest>(balanceDefaults.Value);
        var b = await balance.Run(new BalanceDruidSimRequest {
            DurationSeconds = 16m, CaptureTimeline = true
        },CancellationToken.None);
        var bres = Assert.IsType<BalanceDruidSimResponse>(Assert.IsType<OkObjectResult>(b.Result).Value);
        Assert.True(bres.Run.ProcessedEventCount > 0);
        Assert.NotEmpty(bres.DamageByAbility);
        Assert.Contains("damageByAbility", JsonSerializer.Serialize(bres, JsonOptions));

        var feral = new FeralDruidSimController();
        Assert.IsType<FeralDruidSimRequest>(Assert.IsType<OkObjectResult>(feral.Defaults("bear").Result).Value);
        var cat = await feral.Run(new FeralDruidSimRequest { Role="cat",DurationSeconds=16m },CancellationToken.None);
        var cres = Assert.IsType<FeralDruidSimResponse>(Assert.IsType<OkObjectResult>(cat.Result).Value);
        Assert.True(cres.Run.ProcessedEventCount > 0);
        Assert.True(cres.DamageDone > 0m);
        Assert.Contains("damageDone", JsonSerializer.Serialize(cres, JsonOptions));
        var bear = await feral.Run(new FeralDruidSimRequest { Role="bear",DurationSeconds=16m,
            EnableBossAttacks=true },CancellationToken.None);
        var bbr = Assert.IsType<FeralDruidSimResponse>(Assert.IsType<OkObjectResult>(bear.Result).Value);
        Assert.True(bbr.DamageTaken > 0m);
        Assert.True(bbr.Run.CustomMetrics.ContainsKey("bear-projected-ending-health"));
        Assert.Contains("threatPerSecond", JsonSerializer.Serialize(bbr, JsonOptions));

        var restoration = new RestorationDruidSimController();
        Assert.IsType<RestorationDruidSimRequest>(Assert.IsType<OkObjectResult>(restoration.Defaults().Result).Value);
        var heal = await restoration.Run(new RestorationDruidSimRequest {
            DurationSeconds=18m, IncomingDamage=1000m, CaptureTimeline=true
        },CancellationToken.None);
        var hres = Assert.IsType<RestorationDruidSimResponse>(Assert.IsType<OkObjectResult>(heal.Result).Value);
        Assert.True(hres.Run.ProcessedEventCount > 0);
        Assert.True(hres.HealingDone > 0m);
        Assert.Contains("healingByAbility", JsonSerializer.Serialize(hres, JsonOptions));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
