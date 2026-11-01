using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175WarlockMixedTreeExecution
{
    [Fact]
    public void DestructionStartingPresetCanExecuteLearnedAfflictionTalentOnAdditionalTarget()
    {
        var r=WarlockSimRequest.CreatePreset("destruction");
        r.Talents=ForeverWarlockTalentBuild.Default("affliction");
        r.DurationSeconds=40m;
        r.TargetMaximumHealth=10000000m;
        r.AdditionalTargets=1;
        r.UseLifeTap=false;
        r.AssumeExternalHealingForLifeTap=false;
        r.EnablePet=false;
        r.CustomPriority=["shadow-bolt"];
        r.AfflictionAdditionalTargetPriority["target-1"]=["siphon-life"];
        var result=new WarlockSimRunner().Run(r);
        Assert.Contains(result.Run.Timeline,e=>e.AbilityKey=="siphon-life" &&
            e.Type==CombatEventType.AbilityCastCompleted && e.TargetActorKey=="target-1");
        // Removing a learned talent invalidates the saved action rather than
        // turning it into a speculative class-wide ability.
        r.Talents.Ranks.Remove("siphon-life");
        Assert.Throws<ArgumentException>(()=>WarlockAdaptiveRotation.Build(r));
    }

    [Fact]
    public void AfflictionStartingPresetCanUseLearnedDestructionActionOnAdditionalTarget()
    {
        var r=WarlockSimRequest.CreatePreset("affliction");
        r.Talents=ForeverWarlockTalentBuild.Default("destruction");
        r.DurationSeconds=40m;
        r.TargetMaximumHealth=10000000m;
        r.AdditionalTargets=1;
        r.UseLifeTap=false;
        r.AssumeExternalHealingForLifeTap=false;
        r.EnablePet=false;
        r.CustomPriority=["shadow-bolt"];
        r.DestructionAdditionalTargetPriority["target-1"]=["incinerate"];
        var result=new WarlockSimRunner().Run(r);
        Assert.Contains(result.Run.Timeline,e=>e.AbilityKey=="incinerate" &&
            e.Type==CombatEventType.AbilityCastCompleted && e.TargetActorKey=="target-1");
        r.Talents.Ranks.Remove("incinerate");
        Assert.Throws<ArgumentException>(()=>WarlockAdaptiveRotation.Build(r));
    }
}
