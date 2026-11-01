using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175SavedWarriorPriorityDiagnostics
{
    [Fact]
    public void UnlearnedTalentActionIsReportedInsteadOfDisappearingSilently()
    {
        var r=new WarriorSimRequest {
            Preset="arms",TalentMode="tree",RotationMode="custom",
            Iterations=1,DurationSeconds=10m,
            CustomRotation=[new WarriorRotationRule {Action="mortal-strike",Enabled=true}]
        };
        WarriorRotationRules.Validate(r);
        Assert.Contains(WarriorRotationRules.GetUnavailableTalentActions(r),
            s=>s.Contains("mortal-strike",StringComparison.OrdinalIgnoreCase));
        var result=new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(result.ModelingNotes,s=>s.Contains(
            "UNAVAILABLE CUSTOM ACTION: mortal-strike",StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NonTalentActionsAndDisabledRowsDoNotGenerateFalseUnavailableWarnings()
    {
        var r=new WarriorSimRequest {
            Preset="arms",TalentMode="tree",RotationMode="custom",
            CustomRotation=[
                new WarriorRotationRule {Action="whirlwind",Enabled=true},
                new WarriorRotationRule {Action="mortal-strike",Enabled=false}
            ]
        };
        Assert.Empty(WarriorRotationRules.GetUnavailableTalentActions(r));
    }
}
