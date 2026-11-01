using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175WarriorSharedModifiers
{
    private static WarriorSimRequest Build()=>new() {
        Level=60,Iterations=1,DurationSeconds=60m,Seed=1752027,
        TargetArmor=3500m,CaptureRepresentativeTimeline=true
    };

    [Fact]
    public void SharedMongooseAndFoodUseTheSameSourceStatsAsCasterAndHunter()
    {
        var r=Build();
        var baseline=WarriorGearStatCalculator.Calculate(r).EffectiveStats;
        r.ExternalModifiers.Consumables.Add("elixir-of-the-mongoose");
        r.ExternalModifiers.Consumables.Add("smoked-desert-dumplings");
        var chosen=ForeverWarriorSharedModifierAdapter.Resolve(r);
        Assert.Equal(2,chosen.Count);
        var raw=ForeverExternalModifierCatalog.SumSelfRawStats(r.ExternalModifiers,60,60m);
        Assert.Equal(25m,raw["agility"]);
        Assert.Equal(20m,raw["strength"]);
        var augmented=WarriorGearStatCalculator.Calculate(r).EffectiveStats;
        Assert.Equal(25m,augmented.Get("agility")-baseline.Get("agility"));
        Assert.Equal(20m,augmented.Get("strength")-baseline.Get("strength"));
        Assert.True(augmented.Get("attack-power")>baseline.Get("attack-power"));
    }

    [Fact]
    public void WarriorUsesSameBattleShoutDefinitionWithoutDuplicatingLegacyBuff()
    {
        var r=Build();
        var baseAp=WarriorGearStatCalculator.Calculate(r).EffectiveStats.Get("attack-power");
        r.ExternalModifiers.Buffs.Add("battle-shout");
        var newAp=WarriorGearStatCalculator.Calculate(r).EffectiveStats.Get("attack-power");
        Assert.Equal(139m,newAp-baseAp);
        r.RaidBuffIds.Add("battle-shout");
        Assert.Throws<ArgumentException>(()=>ForeverWarriorSharedModifierAdapter.Resolve(r));
        Assert.Throws<ArgumentException>(()=>WarriorGearStatCalculator.Calculate(r));
    }

    [Fact]
    public void WarriorSharedFlasksFoodAndElixirsEnforceExclusivity()
    {
        var r=Build();
        r.ExternalModifiers.Consumables.AddRange(["flask-of-the-titans",
            "elixir-of-the-mongoose","smoked-desert-dumplings"]);
        Assert.Equal(3,ForeverWarriorSharedModifierAdapter.Resolve(r).Count);
        r.ExternalModifiers.Consumables.Add("grilled-squid");
        Assert.Throws<ArgumentException>(()=>ForeverWarriorSharedModifierAdapter.Resolve(r));
        r.ExternalModifiers.Consumables.Remove("grilled-squid");
        r.ExternalModifiers.Consumables.Add("flask-of-supreme-power");
        Assert.Throws<ArgumentException>(()=>ForeverWarriorSharedModifierAdapter.Resolve(r));
    }

    [Fact]
    public void CrossCatalogDuplicateAndExclusiveSupportFailsClosed()
    {
        var r=Build();
        r.RaidBuffIds.Add("battle-shout");
        r.ExternalModifiers.Buffs.Add("blessing-of-might");
        Assert.Throws<ArgumentException>(()=>ForeverWarriorSharedModifierAdapter.Resolve(r));
        r.RaidBuffIds.Clear();
        r.ConsumableIds.Add("elixir-of-the-mongoose");
        r.ExternalModifiers.Consumables.Add("elixir-of-the-mongoose");
        Assert.Throws<ArgumentException>(()=>ForeverWarriorSharedModifierAdapter.Resolve(r));
    }

    [Fact]
    public void WarriorFaerieFireUsesSharedExpiryAndMaintainedProcessor()
    {
        var r=Build();
        r.ExternalModifiers.TargetDebuffs.Add("faerie-fire-rank4");
        var initial=new ForeverWarriorSimRunner().Run(r);
        Assert.Equal(2995m,initial.EffectiveBossArmor);
        Assert.Contains(initial.Batch.RepresentativeRun.Timeline,x=>
            x.AbilityKey=="faerie-fire-rank4" && x.Type==CombatEventType.AuraApplied &&
            x.TimeSeconds==0m);
        Assert.Contains(initial.Batch.RepresentativeRun.Timeline,x=>
            x.Type==CombatEventType.AuraRemoved && x.TimeSeconds==40m &&
            x.AuraInstanceId.HasValue);
        r.ExternalModifiers.TargetDebuffMode="maintained";
        var maintained=new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(maintained.Batch.RepresentativeRun.Timeline,x=>
            x.AbilityKey=="faerie-fire-rank4" && x.Type==CombatEventType.AuraApplied &&
            x.TimeSeconds==40m);
    }
}
