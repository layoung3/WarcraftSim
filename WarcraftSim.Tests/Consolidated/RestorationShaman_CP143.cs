using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class RestorationShamanCp143Tests
{
    [Fact]
    public void InitialDamageAndRepeatedPressureProduceRealEffectiveHealing()
    {
        var request = new RestorationShamanSimRequest { CaptureTimeline = true };
        var response = new RestorationShamanSimRunner().Run(request);
        Assert.True(response.HealingDone > 0m);
        Assert.InRange(response.EndingMana, 0m, response.MaximumMana);
        Assert.Contains(response.Run.Timeline, x => x.Type == CombatEventType.ScriptedDamage);
        Assert.Contains(response.Run.Timeline, x => x.Type == CombatEventType.Healing && x.Amount is > 0m);
        Assert.Contains("tank", response.AllyEndingHealth.Keys);
    }

    [Fact]
    public void ChainHealCreatesSeparateAttributedBouncesForInjuredTeammates()
    {
        var response = new RestorationShamanSimRunner().Run(new RestorationShamanSimRequest {
            CaptureTimeline = true, AdditionalAllies = 4, AllyInitialHealthPercent = 35m
        });
        Assert.True(response.ChainHealBounces > 0);
        Assert.Contains(response.Run.Timeline, x => x.Type == CombatEventType.Healing &&
            x.AbilityKey == "chain-heal" && x.EffectKey == "chain-heal-bounce-1");
        Assert.Contains(response.HealingByAbility.Keys, x => x == "chain-heal");
    }

    [Fact]
    public void WaterShieldRestoresManaFromCriticalHealsAndDoesNotGenerateFreeMana()
    {
        var request = new RestorationShamanSimRequest { CaptureTimeline = true };
        request.Tuning.HealingCritPercent = 100m;
        var response = new RestorationShamanSimRunner().Run(request);
        Assert.True(response.ManaRestoredFromWaterShield > 0m);
        Assert.Contains(response.Run.Timeline, x => x.Type == CombatEventType.ResourceChanged &&
            x.AbilityKey == "water-shield" && x.ResourceKey == "mana" && x.Amount is > 0m);
    }

    [Fact]
    public void RiptideTalentExposesInstantHealAndPeriodicHealing()
    {
        var kit = ForeverRestorationShamanSpellbook.Create(new(), new());
        var riptide = Assert.Single(kit, x => x.Key == "riptide");
        Assert.Equal(6m, riptide.CooldownSeconds);
        Assert.Contains(riptide.Effects, x => x.Key == "riptide-initial");
        Assert.Contains(riptide.Effects, x => x.Key == "riptide-tick" &&
            x.TickIntervalSeconds == 3m && x.DurationSeconds == 15m);
    }

    [Fact]
    public void UnsupportedTalentsFailInsteadOfSilentlyDoingNothing()
    {
        var build = new ForeverRestorationShamanTalents();
        build.Ranks["unfinished-restoration-talent"] = 1;
        Assert.Throws<ArgumentException>(() => build.Validate());
    }

    [Fact]
    public void DamageFreeScenarioDoesNotInventHealing()
    {
        var result = new RestorationShamanSimRunner().Run(new RestorationShamanSimRequest {
            IncomingDamage = 0m, AllyInitialHealthPercent = 100m
        });
        Assert.Equal(0m, result.HealingDone);
    }

    [Fact]
    public void SeededHealingScenarioIsRepeatable()
    {
        var a = new RestorationShamanSimRunner().Run(new RestorationShamanSimRequest());
        var b = new RestorationShamanSimRunner().Run(new RestorationShamanSimRequest());
        Assert.Equal(a.HealingDone, b.HealingDone);
        Assert.Equal(a.OverhealingDone, b.OverhealingDone);
        Assert.Equal(a.EndingMana, b.EndingMana);
        Assert.Equal(a.ChainHealBounces, b.ChainHealBounces);
    }
}
