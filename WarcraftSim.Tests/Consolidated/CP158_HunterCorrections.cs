using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp158HunterCorrectionsTests
{
    private static ForeverMarksmanshipHunterTalents Talents(int arcaneRanks = 2,
        int rapidKillingRanks = 2, int recuperationRanks = 2, int mortalShotsRanks = 5)
    {
        var baseBuild = new ForeverMarksmanshipHunterTalents();
        baseBuild.Ranks["rapid-killing"] = rapidKillingRanks;
        baseBuild.Ranks["rapid-recuperation"] = recuperationRanks;
        baseBuild.Ranks["improved-arcane-shot"] = arcaneRanks;
        baseBuild.Ranks["mortal-shots"] = mortalShotsRanks;
        baseBuild.Validate();
        return baseBuild;
    }

    private static Dictionary<string, WarcraftSim.Core.Abilities.AbilityDefinition> Spells(
        ForeverMarksmanshipHunterTalents talents, ForeverMarksmanshipHunterTuning? tuning = null)
    {
        tuning ??= new ForeverMarksmanshipHunterTuning();
        return ForeverMarksmanshipHunterSpellbook.Create(talents, tuning,
            tuning.RangedAttackPower + tuning.Intellect * talents.Rank("careful-aim") * .20m +
            (talents.Rank("trueshot-aura") > 0 ? tuning.TrueshotAttackPower : 0m))
            .ToDictionary(x => x.Key);
    }

    [Fact]
    public void ForeverLevel60ShotTimingRankDataAndSharedCooldowns()
    {
        var book = Spells(Talents());
        Assert.Equal(2m, book["aimed-shot"].CastTimeSeconds);
        Assert.Equal(6m, book["aimed-shot"].CooldownSeconds);
        Assert.Equal("hunter-aimed-multi", book["aimed-shot"].SharedCooldownGroupKey);
        Assert.Equal("hunter-aimed-multi", book["multi-shot"].SharedCooldownGroupKey);
        Assert.Equal(6m, book["multi-shot"].SharedCooldownSeconds);
        Assert.Equal(4m, book["sniper-shot"].CastTimeSeconds);
        Assert.Equal(15m, book["sniper-shot"].CooldownSeconds);
        Assert.Equal(365m * .90m, book["sniper-shot"].ResourceCosts.Single().Amount);
        Assert.Equal(295m, new ForeverMarksmanshipHunterTuning().SniperShotBonusDamage);
        Assert.Equal(50m, new ForeverMarksmanshipHunterTuning().TrueshotAttackPower);
    }

    [Fact]
    public void ImprovedArcaneShotFiveRanksReduceOnlyItsCooldown()
    {
        Assert.Equal(6m, Spells(Talents(arcaneRanks: 0))["arcane-shot"].CooldownSeconds);
        Assert.Equal(5.4m, Spells(Talents(arcaneRanks: 2))["arcane-shot"].CooldownSeconds);
        var max = Spells(Talents(arcaneRanks: 5));
        Assert.Equal(4.5m, max["arcane-shot"].CooldownSeconds);
        Assert.Equal(6m, max["aimed-shot"].CooldownSeconds);
        Assert.Equal(6m, max["multi-shot"].CooldownSeconds);
        var invalid = Talents();
        invalid.Ranks["improved-arcane-shot"] = 6;
        Assert.Throws<ArgumentException>(invalid.Validate);
    }

    [Fact]
    public void BarrageAndImprovedStingsMatchForeverRankThreeBonus()
    {
        var build = Talents();
        var best = Spells(build);
        build.Ranks["barrage"] = 0;
        build.Ranks["improved-stings"] = 0;
        var plain = Spells(build);
        var aimed = best["aimed-shot"].Effects.Single().MinimumValue /
            plain["aimed-shot"].Effects.Single().MinimumValue;
        var sting = best["serpent-sting"].Effects.Single(e =>
            e.Key == "serpent-sting-tick").MinimumValue /
            plain["serpent-sting"].Effects.Single(e =>
            e.Key == "serpent-sting-tick").MinimumValue;
        Assert.Equal(1.10m, aimed);
        Assert.Equal(1.20m, sting);
    }

    [Fact]
    public void RapidKillingRanksReduceRapidFireCooldownByOneMinuteEach()
    {
        Assert.Equal(300m, Spells(Talents(rapidKillingRanks: 0, recuperationRanks: 0))["rapid-fire"].CooldownSeconds);
        Assert.Equal(240m, Spells(Talents(rapidKillingRanks: 1))["rapid-fire"].CooldownSeconds);
        Assert.Equal(180m, Spells(Talents(rapidKillingRanks: 2))["rapid-fire"].CooldownSeconds);
    }

    [Fact]
    public void MultiShotOnThreeTargetsAndAimedShotHaveNoSharedCooldownViolation()
    {
        var request = new MarksmanshipHunterSimRequest {
            DurationSeconds = 60m, CaptureTimeline = true, AdditionalTargets = 2,
            UseSerpentSting = false, UseSniperShot = false, UseRapidFire = false,
            Talents = Talents()
        };
        var withMulti = new MarksmanshipHunterSimRunner().Run(request);
        Assert.Contains(withMulti.Run.Timeline, ev => ev.AbilityKey == "multi-shot" &&
            ev.Type == CombatEventType.Damage && ev.TargetActorKey == "add-2");
        var sharedStarts = withMulti.Run.Timeline.Where(ev =>
            ev.Type == CombatEventType.AbilityCastStarted &&
            (ev.AbilityKey is "aimed-shot" or "multi-shot"))
            .OrderBy(ev => ev.TimeSeconds).ToArray();
        for (var i = 1; i < sharedStarts.Length; i++)
            Assert.True(sharedStarts[i].TimeSeconds - sharedStarts[i - 1].TimeSeconds >= 6m);
        request.UseMultiShot = false;
        var withAimed = new MarksmanshipHunterSimRunner().Run(request);
        Assert.True(withAimed.Run.Summary.Abilities.GetValueOrDefault("aimed-shot")?.CastCompletedCount > 0);
        var starts = withAimed.Run.Timeline.Where(ev => ev.Type == CombatEventType.AbilityCastStarted &&
            ev.AbilityKey == "aimed-shot").ToArray();
        var completions = withAimed.Run.Timeline.Where(ev => ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey == "aimed-shot").ToArray();
        Assert.NotEmpty(starts);
        Assert.Equal(starts.Length, completions.Length);
        for (var i = 0; i < starts.Length; i++)
            Assert.Equal(2m, completions[i].TimeSeconds - starts[i].TimeSeconds);
    }

    [Fact]
    public void LoneWolfIncreasesIdenticalAutoShotDamageByExactlyTwentyPercent()
    {
        static MarksmanshipHunterSimRequest Setup(bool lone) {
            var build = Talents();
            build.Ranks["lone-wolf"] = lone ? 1 : 0;
            return new MarksmanshipHunterSimRequest {
                DurationSeconds = 20m, CaptureTimeline = true, Talents = build,
                UseSerpentSting = false, UseRapidFire = false, UseAimedShot = false,
                UseMultiShot = false, UseArcaneShot = false, UseSniperShot = false
            };
        }
        var a = new MarksmanshipHunterSimRunner().Run(Setup(false));
        var b = new MarksmanshipHunterSimRunner().Run(Setup(true));
        var baseline = a.Run.Summary.Abilities["auto-shot"].DamageDone;
        var buffed = b.Run.Summary.Abilities["auto-shot"].DamageDone;
        Assert.InRange(buffed / baseline, 1.199999m, 1.200001m);
    }

    [Fact]
    public void MortalShotsAppliesToArcaneAndSerpentCriticalDamage()
    {
        static MarksmanshipHunterSimRequest Setup(int mortal) => new() {
            DurationSeconds = 20m, CaptureTimeline = true,
            Talents = Talents(mortalShotsRanks: mortal),
            Tuning = new ForeverMarksmanshipHunterTuning {
                HitChancePercent = 100m, CriticalChancePercent = 100m
            },
            UseRapidFire = false, UseAimedShot = false, UseMultiShot = false,
            UseSniperShot = false
        };
        var lower = new MarksmanshipHunterSimRunner().Run(Setup(0));
        var higher = new MarksmanshipHunterSimRunner().Run(Setup(5));
        foreach (var ability in new[] { "arcane-shot", "serpent-sting" }) {
            var low = lower.Run.Timeline.First(e => e.AbilityKey == ability &&
                e.Type == CombatEventType.Damage && e.IsCritical);
            var high = higher.Run.Timeline.First(e => e.AbilityKey == ability &&
                e.Type == CombatEventType.Damage && e.IsCritical);
            Assert.InRange(high.Amount!.Value / low.Amount!.Value, 1.149999m, 1.150001m);
        }
    }

    [Fact]
    public void KillingAnAddGrantsRapidKillingAndBuffsTheNextShotAndRecuperation()
    {
        var request = new MarksmanshipHunterSimRequest {
            DurationSeconds = 35m, CaptureTimeline = true,
            TargetMaximumHealth = 1000000m, AdditionalTargets = 2,
            AdditionalTargetMaximumHealth = 300m,
            UseRapidFire = false, UseSniperShot = false,
            Tuning = new ForeverMarksmanshipHunterTuning {
                HitChancePercent = 100m, CriticalChancePercent = 0m
            },
            Talents = Talents()
        };
        var result = new MarksmanshipHunterSimRunner().Run(request);
        Assert.True(result.Run.CustomMetrics["hunter-rapid-killing-procs"] > 0m);
        Assert.True(result.Run.CustomMetrics["hunter-rapid-killing-consumes"] > 0m);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == "rapid-killing" &&
            e.EffectKey == "hunter-rapid-killing-consumed");
        // 205 Arcane * 1.2 Lone Wolf * 1.2 Rapid Killing = 295.2 raw damage.
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "arcane-shot" && e.RawAmount is >= 295.2m);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == "rapid-recuperation" &&
            e.Amount is >= 9.99m);
        // Export format must retain both per-ability combat and proc metrics.
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.True(json.RootElement.GetProperty("run").GetProperty("customMetrics")
            .GetProperty("hunter-rapid-killing-procs").GetDecimal() > 0m);
    }

    [Fact]
    public void RecuperationNeverSchedulesDuplicateManaTicksOnStingRefresh()
    {
        var result = new MarksmanshipHunterSimRunner().Run(new MarksmanshipHunterSimRequest {
            DurationSeconds = 75m, CaptureTimeline = true, UseRapidFire = false,
            Talents = Talents()
        });
        var ticks = result.Run.Timeline.Where(e => e.AbilityKey == "rapid-recuperation" &&
            e.Type == CombatEventType.ResourceChanged && e.Amount is > 0m).ToArray();
        Assert.NotEmpty(ticks);
        Assert.All(ticks.GroupBy(e => e.TimeSeconds), group => Assert.Single(group));
    }
}
