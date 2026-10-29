using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BgClearcastingCostDiagnostics
{
    [Fact]
    public void ClearcastingProcActuallyMakesNextFireballCostZeroAfterDoTPulses()
    {
        var r=MageSimRequest.CreatePreset("arcane");
        r.DurationSeconds=90m;
        r.BaseSpellHitPercent=100m;
        r.UseMissileBarrage=false;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.CustomPriority=["fireball"];
        r.Tuning.FireballMana=10m;
        r.Tuning.ManaRegenPerSecond=100m;
        var result=new MageSimRunner().Run(r);
        var events=result.Run.Timeline.Where(e=>e.AbilityKey is
            ("fireball" or "arcane-concentration")).ToArray();
        var log=string.Join("; ",events.Take(75).Select(e=>
            $"{e.TimeSeconds}:{e.Type}:{e.AbilityKey}:{e.EffectKey}:{e.ResourceKey}:{e.Amount}:{e.AbilityExecutionId}"));
        Assert.True(result.ClearcastingProcs>0,log);
        Assert.True(result.ClearcastingConsumed>0,log);
        Assert.True(events.Any(e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="fireball" && e.ResourceKey=="mana" && e.Amount==0m),
            $"procs={result.ClearcastingProcs},consumed={result.ClearcastingConsumed}; "+log);
    }
}
