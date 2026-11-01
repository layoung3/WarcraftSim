using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175WarriorBuilderIntegration
{
    [Fact]
    public void DefaultWarriorPreparesRealSharedEquipmentAndExecutes()
    {
        var controller=new ForeverWarriorBuilderController();
        var defaults=Assert.IsType<OkObjectResult>(controller.Defaults("arms"));
        var request=Assert.IsType<WarriorSimRequest>(defaults.Value);
        Assert.Equal("tree",request.TalentMode);
        request.Race="human";
        request.DurationSeconds=12m;
        request.Iterations=1;
        request.StartingStance="battle";
        request.StartingRage=80m;
        request.CaptureRepresentativeTimeline=true;
        var prepared=Assert.IsType<OkObjectResult>(controller.Prepare(new() {
            Warrior=request,Equipment=new CharacterEquipmentLoadout()
        }));
        Assert.NotNull(prepared.Value);
        var result=new ForeverWarriorSimRunner().Run(request);
        Assert.Contains(result.Batch.RepresentativeRun.Timeline,e=>
            e.AbilityKey=="main-hand-auto" || e.AbilityKey=="bloodrage");
    }

    [Fact]
    public void WarriorEncounterStanceChangesRealOpeningAura()
    {
        WarriorSimRequest Build(string stance)=>new() {
            Preset="fury",TalentMode="tree",Iterations=1,
            DurationSeconds=12m,StartingStance=stance,StartingRage=80m,
            CaptureRepresentativeTimeline=true
        };
        var battle=new ForeverWarriorSimRunner().Run(Build("battle"));
        var berserker=new ForeverWarriorSimRunner().Run(Build("berserker"));
        Assert.Contains(battle.Batch.RepresentativeRun.Timeline,e=>
            e.Type==WarcraftSim.Core.Simulation.Engine.CombatEventType.AuraApplied &&
            e.Description?.Contains("Battle Stance applied",StringComparison.Ordinal)==true);
        Assert.Contains(berserker.Batch.RepresentativeRun.Timeline,e=>
            e.Type==WarcraftSim.Core.Simulation.Engine.CombatEventType.AuraApplied &&
            e.Description?.Contains("Berserker Stance applied",StringComparison.Ordinal)==true);
        Assert.Throws<ArgumentException>(()=>new ForeverWarriorSimRunner().Run(Build("invalid")));
    }
}
