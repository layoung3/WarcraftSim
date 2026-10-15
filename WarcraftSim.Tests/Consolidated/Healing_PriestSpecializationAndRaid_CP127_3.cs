using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class HealingPriestSpecializationAndRaidCp1273Tests
{
    private static HolyPriestPreviewRequest Scenario(string spec = "holy") => new()
    {
        EnableExtendedPriest = true,
        UseAdaptiveHealingPolicy = true,
        UseSpecializationHealingPolicy = true,
        PriestCharacter = new CharacterProfile
        {
            ClassKey = "priest", SpecializationKey = spec, Level = 60
        },
        DurationSeconds = 45m,
        TankMaximumHealth = 4500m,
        BossSwingDamage = 360m,
        BossSwingIntervalSeconds = 2m,
        PriestMaximumMana = 12000m,
        PriestManaRegenPerSecond = 0m,
        PriestSpiritRegenPerSecond = 0m,
        PriestMp5 = 0m,
        LitanyRefundFraction = 0m,
        AdditionalRaidMembers = 3,
        RaidMemberInitialHealthPercent = 100m,
        PriestInitialHealthPercent = 100m,
        RaidPulseDamage = 0m,
        UseDivineAegis = false,
        CaptureTimeline = true
    };

    [Fact]
    public void NewBehaviorIsOptInAndDoesNotScheduleDamageInLegacyMode()
    {
        var request = new HolyPriestPreviewRequest
        {
            EnableExtendedPriest = true,
            DurationSeconds = 12m,
            AdditionalRaidMembers = 2
        };
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.False(request.UseSpecializationHealingPolicy);
        Assert.Equal(0m, result.RaidDamageTaken);
        Assert.DoesNotContain(result.Run.Timeline,
            ev => ev.Type == CombatEventType.ScriptedDamage);
    }

    [Fact]
    public void HolyStyleMaintainsMendingAndRenewOnPressureTarget()
    {
        var request = Scenario("holy");
        request.UsePenance = false;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Contains(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.AbilityCastStarted &&
            ev.AbilityKey == "prayer-of-mending" && ev.TargetActorKey == "tank");
        Assert.Contains(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.AbilityCastStarted &&
            ev.AbilityKey == "renew" && ev.TargetActorKey == "tank");
    }

    [Fact]
    public void DisciplineStyleChannelsPenanceBeforeDeepInjury()
    {
        var request = Scenario("discipline");
        request.UsePrayerOfMending = false;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Contains(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.Healing &&
            ev.AbilityKey == "penance" && ev.TargetActorKey == "tank");
    }

    [Fact]
    public void RecurringRaidPulsesHitEachConfiguredAdditionalActor()
    {
        var request = Scenario();
        request.EnableHealer = false;
        request.DurationSeconds = 10m;
        request.RaidPulseDamage = 300m;
        request.RaidPulseFirstSeconds = 2m;
        request.RaidPulseIntervalSeconds = 4m;
        request.RaidPulseIncludePriest = false;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Equal(1800m, result.RaidDamageTaken);
        for (var i = 1; i <= 3; i++)
            Assert.Equal(600m, result.Run.Summary.ActorSummaries[$"raid-{i}"].DamageTaken);
        Assert.Equal(0m, result.Run.Summary.ActorSummaries["priest"].DamageTaken);
        Assert.Equal(0, result.RaidMemberDeaths);
    }

    [Fact]
    public void OptionalPriestRaidDamageCanBeEnabledIndependently()
    {
        var request = Scenario();
        request.EnableHealer = false;
        request.DurationSeconds = 10m;
        request.AdditionalRaidMembers = 0;
        request.RaidPulseDamage = 200m;
        request.RaidPulseIntervalSeconds = 4m;
        request.RaidPulseFirstSeconds = 2m;
        request.RaidPulseIncludePriest = true;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Equal(400m, result.Run.Summary.ActorSummaries["priest"].DamageTaken);
        Assert.Equal(400m, result.RaidDamageTaken);
    }

    [Fact]
    public void RaidDamageCausesRealSecondaryActorDeathWithoutStoppingSim()
    {
        var request = Scenario();
        request.EnableHealer = false;
        request.DurationSeconds = 10m;
        request.AdditionalRaidMembers = 1;
        request.RaidMemberMaximumHealth = 400m;
        request.RaidPulseDamage = 600m;
        request.RaidPulseFirstSeconds = 2m;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Equal(1, result.RaidMemberDeaths);
        Assert.Contains(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.ActorDied && ev.TargetActorKey == "raid-1");
        Assert.Contains(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.SimulationEnded);
    }

    [Fact]
    public void HolyGroupHealActivatesWhenThreeRaidMembersTakeDamage()
    {
        var request = Scenario("holy");
        request.RaidPulseDamage = 500m;
        request.RaidPulseFirstSeconds = 4m;
        request.RaidPulseIntervalSeconds = 30m;
        var result = new HolyPriestPreviewRunner().Run(request);
        Assert.Contains(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.Healing &&
            ev.AbilityKey == "prayer-of-healing" &&
            (ev.TargetActorKey == "raid-1" || ev.TargetActorKey == "raid-2" || ev.TargetActorKey == "raid-3"));
    }

    [Fact]
    public void BindingHealRequiresInjuredPriestAndHealsAnAlly()
    {
        var request = Scenario("holy");
        request.PriestInitialHealthPercent = 70m;
        request.RaidMemberInitialHealthPercent = 60m;
        // Isolate Binding Heal: both the Priest and raid-1 need healing when
        // the first decision fires. Otherwise a competing maintenance or group
        // heal can legitimately take priority and heal the Priest first.
        request.UsePrayerOfHealing = false;
        request.UsePrayerOfMending = false;
        request.UseRenew = false;
        request.UsePowerWordShield = false;
        request.UsePenance = false;
        var result = new HolyPriestPreviewRunner().Run(request);

        Assert.Contains(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.Healing &&
            ev.AbilityKey == "binding-heal" &&
            ev.TargetActorKey == "priest" && ev.Amount > 0m);
        Assert.Contains(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.Healing &&
            ev.AbilityKey == "binding-heal" &&
            ev.TargetActorKey == "raid-1" && ev.Amount > 0m);
        Assert.DoesNotContain(result.Run.Timeline, ev =>
            ev.Type == CombatEventType.AbilityCastStarted &&
            ev.AbilityKey == "binding-heal" && ev.TargetActorKey == "priest");

        // An injured ally alone must not trigger Binding Heal; the caster
        // must also need healing. Other spells are free to handle the ally.
        request.PriestInitialHealthPercent = 100m;
        var priestHealthy = new HolyPriestPreviewRunner().Run(request);
        Assert.DoesNotContain(priestHealthy.Run.Timeline, ev =>
            ev.Type == CombatEventType.AbilityCastStarted &&
            ev.AbilityKey == "binding-heal");
    }

    [Fact]
    public void RaidDamageAndSpecializationModesValidateDependencies()
    {
        var request = Scenario();
        request.UseAdaptiveHealingPolicy = false;
        Assert.Throws<ArgumentException>(() => new HolyPriestPreviewRunner().Run(request));
        request.UseSpecializationHealingPolicy = false;
        request.RaidPulseIntervalSeconds = 0m;
        Assert.Throws<ArgumentException>(() => new HolyPriestPreviewRunner().Run(request));
        request.RaidPulseIntervalSeconds = 12m;
        request.RaidPulseDamage = -1m;
        Assert.Throws<ArgumentException>(() => new HolyPriestPreviewRunner().Run(request));
    }
}
