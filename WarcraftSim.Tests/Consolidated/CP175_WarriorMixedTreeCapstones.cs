using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175WarriorMixedTreeCapstones
{
    // Source catalog tiers: 31 Fury (including Bloodthirst), 20 Arms.
    // Starting preset is deliberately Arms to verify talent-derived action use.
    private static WarriorSimRequest Mixed()
    {
        var r=new WarriorSimRequest {
            Preset="arms",TalentMode="tree",Iterations=1,Seed=175275,
            DurationSeconds=15m,StartingRage=100m,CaptureRepresentativeTimeline=true,
            TargetLevel=60,TargetDodgePercent=0m,TargetParryPercent=0m,
            UseBloodrage=false,UseRend=false,UseExecute=false,UseHeroicStrike=false
        };
        r.TalentRanks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase) {
            ["cruelty"]=5,["unbridled-wrath"]=5,
            ["blood-craze"]=3,["furious-precision"]=2,
            ["enrage"]=5,["death-wish"]=1,
            ["improved-berserker-rage"]=2,["improved-intercept"]=2,
            ["flurry"]=5,["bloodthirst"]=1,
            ["improved-rend"]=3,["deflection"]=2,["improved-heroic-strike"]=1,
            ["improved-tactical-mastery"]=5,
            ["deep-wounds"]=3,["anger-management"]=1,
            ["two-handed-weapon-specialization"]=2,
            ["spearing-strike"]=1,["impale"]=2
        };
        ForeverWarriorTalentCatalog.Validate(r);
        Assert.Equal(51,r.TalentRanks.Values.Sum());
        return r;
    }

    [Fact]
    public void ArmsStartingPresetExecutesLearnedOffTreeBloodthirst()
    {
        var r=Mixed();
        var run=new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(run.Batch.RepresentativeRun.Timeline,
            x=>x.AbilityKey=="bloodthirst" &&
               x.Type==CombatEventType.AbilityCastCompleted);
        r.TalentRanks.Remove("bloodthirst");
        ForeverWarriorTalentCatalog.Validate(r);
        var removed=new ForeverWarriorSimRunner().Run(r);
        Assert.DoesNotContain(removed.Batch.RepresentativeRun.Timeline,
            x=>x.AbilityKey=="bloodthirst" &&
               x.Type==CombatEventType.AbilityCastCompleted);
        Assert.NotEqual(run.MeanDps,removed.MeanDps);
    }

    [Fact]
    public void SavedCustomBloodthirstActionFollowsActualRankNotArmsPreset()
    {
        var r=Mixed();
        r.RotationMode="custom";
        r.CustomRotation=[new WarriorRotationRule {Action="bloodthirst",Enabled=true}];
        WarriorRotationRules.Validate(r);
        var run=new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(run.Batch.RepresentativeRun.Timeline,
            x=>x.AbilityKey=="bloodthirst" &&
               x.Type==CombatEventType.AbilityCastCompleted);
        r.TalentRanks.Remove("bloodthirst");
        Assert.Throws<ArgumentException>(()=>WarriorRotationRules.Validate(r));
    }
}
