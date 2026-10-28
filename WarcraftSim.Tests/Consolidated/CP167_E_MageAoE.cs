using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167EMageAoe
{
    [Fact]
    public void AoESpellbooksHaveCurrentClientRanksAndExplicitTargetCaps()
    {
        var spells = ForeverMageDpsSpellbook.Create(ForeverMageTalentBuild.Default("frost"),
            new ForeverMageTuning(), 200m);
        var arcane = Assert.Single(spells, x => x.Key == "arcane-explosion");
        Assert.Equal(390m, arcane.ResourceCosts.Single().Amount);
        Assert.Equal(242m, arcane.Effects.Single().MinimumValue);
        Assert.Equal(9, arcane.Effects.Single().MaxTargets);
        var blizzard = Assert.Single(spells, x => x.Key == "blizzard");
        Assert.Equal(1400m * .85m, blizzard.ResourceCosts.Single().Amount);
        Assert.Equal(8m, blizzard.ChannelDurationSeconds);
        Assert.Equal(1m, blizzard.ChannelTickIntervalSeconds);
        Assert.Equal(146m, blizzard.Effects.Single().MinimumValue);
        Assert.True(blizzard.Effects.Single().ApplyOnChannelTick);
    }

    [Theory]
    [InlineData("arcane", "arcane-explosion")]
    [InlineData("frost", "blizzard")]
    public void MultitargetAoEDamagesAllExplicitEnemiesWithAccounting(string spec, string ability)
    {
        var req = MageSimRequest.CreatePreset(spec);
        req.DurationSeconds = 18m;
        req.AdditionalTargets = 2;
        req.BaseSpellHitPercent = 100m;
        req.SpellCritPercent = 0m;
        req.UseEvocation = false;
        req.UseMajorManaPotion = false;
        req.CombatItems.ManaRubyCharges = 0;
        req.CaptureTimeline = true;
        req.CustomPriority = [ability];
        var sim = new MageSimRunner().Run(req);
        var damage = sim.Run.Timeline.Where(e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == ability && e.Amount > 0m).ToList();
        Assert.Contains(damage, e => e.TargetActorKey == "target");
        Assert.Contains(damage, e => e.TargetActorKey == "target-1");
        Assert.Contains(damage, e => e.TargetActorKey == "target-2");
        if (ability == "blizzard")
            Assert.Contains(damage, e => e.IsChannelTick);
        Assert.InRange(Math.Abs(sim.DamageDone - sim.DamageByAbility.Values.Sum()),
            0m, .000001m);
    }
}
