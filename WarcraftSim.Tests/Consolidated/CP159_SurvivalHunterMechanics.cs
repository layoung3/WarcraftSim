using System.Text.Json;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Microsoft.AspNetCore.Mvc;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp159SurvivalHunterMechanicsTests
{
    private static SurvivalHunterSimRequest Setup(string mode="hybrid", decimal duration=90m) => new() {
        CombatMode = mode, DurationSeconds = duration, SwitchAtSeconds = duration / 2m,
        TargetMaximumHealth = 1000000m,
        AdditionalTargets = 2, ScriptedAvoidanceEverySeconds = 9m,
        CaptureTimeline = true,
        Talents = new ForeverSurvivalHunterTalents {
            Ranks = new Dictionary<string,int> {
                ["improved-tracking"] = 5, ["deflection"] = 5, ["entrapment"] = 3,
                ["savage-strikes"] = 2, ["survivalist"] = 3, ["improved-wing-clip"] = 1,
                ["clever-traps"] = 2, ["surefooted"] = 3, ["deterrence"] = 1,
                ["survival-tactics"] = 2, ["predators-edge"] = 5, ["counterattack"] = 1,
                ["resourcefulness"] = 2, ["expose-prey"] = 2,
                ["survivalists-discipline"] = 2, ["strider-kick"] = 1,
                ["lightning-reflexes"] = 5, ["lacerating-strikes"] = 1
            }
        },
        Tuning = new ForeverSurvivalHunterTuning { MaximumMana=24000m, BaseMana=4200m,
            CriticalChancePercent=40m, HitChancePercent=100m, TargetDodgePercent=0m,
            TargetParryPercent=0m }
    };

    [Fact]
    public void DefaultPresetLoadsAndValidatesAgainstCurrentForeverTree()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        string? absolute = null;
        while (cursor is not null) {
            var candidate = Path.Combine(cursor.FullName, "WarcraftSim.Api", "wwwroot",
                "default-talents", "hunter-survival.json");
            if (File.Exists(candidate)) { absolute = candidate; break; }
            cursor = cursor.Parent;
        }
        Assert.NotNull(absolute);
        using var document = JsonDocument.Parse(File.ReadAllText(absolute));
        var preset = document.RootElement;
        Assert.Equal("hunter-survival", preset.GetProperty("id").GetString());
        var ranks = JsonSerializer.Deserialize<Dictionary<string,int>>(
            preset.GetProperty("overrides").GetProperty("talents")
                .GetProperty("ranks").GetRawText())!;
        Assert.Equal(51, ranks.Values.Sum());
        var request = new SurvivalHunterSimRequest { Talents = new ForeverSurvivalHunterTalents { Ranks = ranks } };
        request.Talents.Validate();
        Assert.Equal(1, request.Talents.Rank("lacerating-strikes"));
        Assert.Equal(2, request.Talents.Rank("expose-prey"));
        Assert.Equal(1, request.Talents.Rank("strider-kick"));
        Assert.True(new SurvivalHunterSimRunner().Run(request).DamageDone > 0m);
    }

    [Fact]
    public void InvalidTalentRankOrLockedCapstoneIsRejected()
    {
        var talents = Setup().Talents;
        talents.Ranks["strider-kick"] = 2;
        Assert.Throws<ArgumentException>(() => talents.Validate());
        talents.Ranks["strider-kick"] = 1;
        talents.Ranks["expose-prey"] = 1;
        Assert.Throws<ArgumentException>(() => talents.Validate());
    }

    [Fact]
    public void SurvivalMeleeAndRangedPhasesAreExclusiveAndDealDamage()
    {
        var response = new SurvivalHunterSimRunner().Run(Setup());
        Assert.True(response.MeleeDamage > 0m);
        Assert.True(response.RangedDamage > 0m);
        Assert.True(response.TrapDamage > 0m);
        Assert.Equal(response.DamageDone, response.MeleeDamage + response.RangedDamage + response.TrapDamage);
        var events = response.Run.Timeline;
        Assert.Contains(events, x => x.AbilityKey == "melee-auto" && x.Type == CombatEventType.Damage &&
            x.TimeSeconds < 45m);
        Assert.Contains(events, x => x.AbilityKey == "auto-shot" && x.Type == CombatEventType.Damage &&
            x.TimeSeconds >= 45m);
        Assert.DoesNotContain(events, x => x.AbilityKey == "melee-auto" &&
            x.Type == CombatEventType.Damage && x.TimeSeconds >= 45m);
        Assert.DoesNotContain(events, x => x.AbilityKey == "auto-shot" &&
            x.Type == CombatEventType.Damage && x.TimeSeconds < 45m);
        Assert.Contains(events, x => x.AbilityKey == "strider-kick" && x.Type == CombatEventType.Damage);
    }

    [Fact]
    public void MongooseBiteAndLaceratingStrikesUseRealDamageEvents()
    {
        var response = new SurvivalHunterSimRunner().Run(Setup("melee"));
        Assert.True(response.Run.Summary.Abilities.GetValueOrDefault("mongoose-bite")?.CastCompletedCount > 0);
        Assert.True(response.Run.CustomMetrics["survival-lacerating-bleed-ticks"] > 0);
        Assert.Contains(response.Run.Timeline, x => x.AbilityKey == "lacerating-strikes" &&
            x.Type == CombatEventType.Damage && x.IsPeriodic && x.Amount > 0);
        Assert.True(response.Run.CustomMetrics["survival-scripted-avoids"] > 0);
        Assert.True(response.Run.CustomMetrics["survival-expose-prey-procs"] > 0);
    }

    [Fact]
    public void RaptorStrikeReplacesNextMeleeSwingAndPaysManaOnConsumption()
    {
        var request = Setup("melee", 45m);
        request.UseFireTraps = false;
        request.UseFrostTraps = false;
        var run = new SurvivalHunterSimRunner().Run(request);
        Assert.Contains(run.Run.Timeline, e => e.AbilityKey == "raptor-strike" &&
            e.Type == CombatEventType.Damage && e.Amount > 0m);
        Assert.Contains(run.Run.Timeline, e => e.AbilityKey == "raptor-strike" &&
            e.Type == CombatEventType.ResourceChanged && e.ResourceKey == "mana" && e.Amount < 0m);
        Assert.True(run.Run.Summary.Abilities.GetValueOrDefault("raptor-strike")?.DamageDone > 0m);
    }

    [Fact]
    public void FireAndFrostSharedCooldownsRemainIndependent()
    {
        var book = ForeverSurvivalHunterSpellbook.Create(Setup().Talents, Setup().Tuning)
            .ToDictionary(x=>x.Key);
        Assert.Equal("hunter-fire-traps", book["explosive-trap"].SharedCooldownGroupKey);
        Assert.Equal("hunter-fire-traps", book["immolation-trap"].SharedCooldownGroupKey);
        Assert.Equal("hunter-frost-traps", book["frost-trap"].SharedCooldownGroupKey);
        Assert.Equal("hunter-frost-traps", book["freezing-trap"].SharedCooldownGroupKey);
        Assert.NotEqual(book["explosive-trap"].SharedCooldownGroupKey,
            book["frost-trap"].SharedCooldownGroupKey);
        Assert.Equal(18m, book["explosive-trap"].SharedCooldownSeconds);
        var run = new SurvivalHunterSimRunner().Run(Setup());
        Assert.True(run.Run.Summary.Abilities.GetValueOrDefault("explosive-trap")?.CastCompletedCount > 0);
        Assert.True(run.Run.Summary.Abilities.GetValueOrDefault("frost-trap")?.CastCompletedCount > 0);
        Assert.True(run.Run.CustomMetrics["survival-trap-triggers"] >= 2m);
    }

    [Fact]
    public void ImmolationAndExplosiveTrapTickAsFireAndFrostDoesNotFabricateDamage()
    {
        var immolate = Setup("melee");
        immolate.FireTrapKind = "immolation";
        var result = new SurvivalHunterSimRunner().Run(immolate);
        Assert.True(result.TrapDamage > 0m);
        Assert.Contains(result.Run.Timeline, x => x.AbilityKey == "immolation-trap" &&
            x.IsPeriodic && x.Type == CombatEventType.Damage);
        var frostOnly = Setup("melee");
        frostOnly.UseFireTraps = false;
        var frostResult = new SurvivalHunterSimRunner().Run(frostOnly);
        Assert.Equal(0m, frostResult.TrapDamage);
        Assert.Contains(frostResult.Run.Timeline, x => x.AbilityKey == "frost-trap" &&
            x.Type == CombatEventType.AuraApplied);
    }

    [Fact]
    public void ResourcefulnessReducesCostsAndRestoresManaFromAbilityCrits()
    {
        var talents = Setup().Talents;
        var book = ForeverSurvivalHunterSpellbook.Create(talents, Setup().Tuning).ToDictionary(x=>x.Key);
        Assert.Equal(0m, book["raptor-strike"].ResourceCosts.Single().Amount);
        Assert.Equal(95m*.40m, book["lacerate"].ResourceCosts.Single().Amount);
        Assert.Equal(520m*.40m, book["explosive-trap"].ResourceCosts.Single().Amount);
        Assert.Equal(8m, book["strider-kick"].CooldownSeconds);
        var resp = new SurvivalHunterSimRunner().Run(Setup("melee"));
        Assert.True(resp.Run.CustomMetrics["survival-resourcefulness-procs"] > 0m);
        Assert.True(resp.Run.CustomMetrics["survival-resourcefulness-mana"] > 0m);
        Assert.Contains(resp.Run.Timeline, x => x.AbilityKey == "resourcefulness" &&
            x.Type == CombatEventType.ResourceChanged && x.Amount > 0m);
    }

    [Fact]
    public void TrackingAndSavageStrikesChangeResolvedOutcomes()
    {
        var baseSetup = Setup("melee",60m);
        baseSetup.Tuning.HitChancePercent = 100m;
        baseSetup.TargetIsTracked = false;
        var plain = new SurvivalHunterSimRunner().Run(baseSetup);
        baseSetup.TargetIsTracked = true;
        var tracked = new SurvivalHunterSimRunner().Run(baseSetup);
        Assert.True(tracked.DamageDone > plain.DamageDone);
        var book = ForeverSurvivalHunterSpellbook.Create(baseSetup.Talents,baseSetup.Tuning)
            .ToDictionary(x=>x.Key);
        Assert.Equal(0m, book["raptor-strike"].CastTimeSeconds);
        Assert.Empty(book["raptor-strike"].Effects); // queued, resolved by AutoAttackProcessor
        Assert.True(book["mongoose-bite"].Effects.Single().CanBeDodged);
        Assert.True(book["mongoose-bite"].Effects.Single().CanBeParried);
        Assert.Equal(5m, book["mongoose-bite"].CooldownSeconds);
    }

    [Fact]
    public async Task ControllerApiRoundtripAndJsonSerialization()
    {
        var controller = new SurvivalHunterSimController();
        Assert.IsType<OkObjectResult>(controller.Defaults().Result);
        var action = await controller.Run(Setup("melee",45m),CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<SurvivalHunterSimResponse>(ok.Value);
        Assert.True(response.Dps > 0m);
        var json = JsonSerializer.Serialize(response);
        Assert.Contains("lacerating-strikes",json);
        Assert.Contains("Summary",json);
    }
}
