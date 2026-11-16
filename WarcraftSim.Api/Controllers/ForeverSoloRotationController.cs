using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WarcraftSim.Api.Simulation;

namespace WarcraftSim.Api.Controllers;

/// <summary>Read-only preview of the engine's existing typed rotation entries.
/// This endpoint does not manufacture talent or spec-specific abilities.</summary>
[ApiController]
[Route("api/forever/rotation")]
public sealed class ForeverSoloRotationController : ControllerBase
{
    [HttpPost("preview")]
    public IActionResult Preview([FromBody] ForeverLevel60SimulationRequest? request)
    {
        if(request is null)return BadRequest(new {error="A typed class configuration is required."});
        try
        {
            var cls=request.ClassKey?.Trim().ToLowerInvariant()??"";
            if(cls=="mage" && request.Mage is { } mage &&
                mage.Specialization==request.Specialization)
            {
                var profile=MageAdaptiveRotation.Build(mage);
                SoloDpsPriorityConditionBridge.Apply(profile,mage.PriorityConditions,mage.CustomPriority,
                    new[]{"target"}.Concat(Enumerable.Range(1,mage.AdditionalTargets)
                        .Select(i=>$"target-{i}")));
                return Ok(new{mode=mage.CustomPriority.Count>0?"custom":"default",
                    supported=true,execution="MageAdaptiveRotation",
                    entries=profile.Entries.Select(x=>new{x.AbilityKey,x.IsEnabled,x.Priority,
                        x.Conditions}).ToArray()});
            }
            if(cls=="warlock" && request.Warlock is { } warlock &&
                warlock.Specialization==request.Specialization)
            {
                var profile=WarlockAdaptiveRotation.Build(warlock);
                SoloDpsPriorityConditionBridge.Apply(profile,warlock.PriorityConditions,warlock.CustomPriority,
                    new[]{"target"}.Concat(Enumerable.Range(1,warlock.AdditionalTargets)
                        .Select(i=>$"target-{i}")));
                return Ok(new{mode=warlock.CustomPriority.Count>0?"custom":"default",
                    supported=true,execution="WarlockAdaptiveRotation",
                    entries=profile.Entries.Select(x=>new{x.AbilityKey,x.IsEnabled,x.Priority,
                        x.Conditions}).ToArray()});
            }
            if(cls=="priest" && request.Specialization=="shadow" && request.Shadow is { } shadow)
            {
                var prepared=ShadowPriestSimRunner.Preview(shadow);
                return Ok(new{mode="fixed-toggles",supported=true,execution="ShadowPriestSimRunner.BuildRotation",
                    prepared.SpellPower,prepared.StatPreviewScope,prepared.RotationPreviewScope,prepared.ModelingNotes,
                    entries=prepared.Rotation.Entries.Select(x=>new{x.AbilityKey,x.IsEnabled,
                        x.Priority,x.Conditions}).ToArray()});
            }
            if(cls=="druid" && request.Specialization=="balance" && request.Balance is { } balance)
            {
                var prepared=BalanceDruidSimRunner.Preview(balance);
                return Ok(new{mode=balance.CustomPriority.Count>0?"custom":"default",
                    supported=true,execution="BalanceAdaptiveRotation",
                    prepared.SpellPower,prepared.StatPreviewScope,prepared.RotationPreviewScope,prepared.ModelingNotes,
                    entries=prepared.Rotation.Entries.Select(x=>new{x.AbilityKey,x.IsEnabled,
                        x.Priority,x.Conditions}).ToArray()});
            }
            if(cls=="shaman" && request.Specialization=="elemental" && request.Elemental is { } elemental)
            {
                var prepared=ElementalShamanSimRunner.Preview(elemental);
                return Ok(new{mode=elemental.CustomPriority.Count>0?"custom":"default",
                    supported=true,execution="ElementalAdaptiveRotation",
                    prepared.SpellPower,prepared.StatPreviewScope,prepared.ModelingNotes,
                    entries=prepared.Rotation.Entries.Select(x=>new{x.AbilityKey,x.IsEnabled,
                        x.Priority,x.Conditions}).ToArray()});
            }
            if(cls=="shaman" && request.Specialization=="enhancement" && request.Enhancement is { } enhancement)
            {
                var prepared=EnhancementShamanSimRunner.Preview(enhancement);
                return Ok(new{mode="fixed-toggles",supported=true,
                    execution="EnhancementShamanSimRunner.BuildRotation",
                    prepared.AttackPower,prepared.SpellPower,prepared.StatPreviewScope,
                    prepared.RotationPreviewScope,prepared.ModelingNotes,
                    entries=prepared.Rotation.Entries.Select(x=>new{x.AbilityKey,x.IsEnabled,
                        x.Priority,x.Conditions}).ToArray()});
            }
            if(cls=="rogue" && request.Specialization=="assassination" && request.Rogue is { } rogue &&
                rogue.Specialization=="assassination" && rogue.Character is { ClassKey: "rogue", SpecializationKey: "assassination", Level: 60 })
            {
                var prepared=RogueSimRunner.Preview(rogue);
                return Ok(new{mode="fixed-configured",supported=true,execution="RogueSimRunner.BuildRotation",
                    prepared.AttackPower,prepared.MaximumEnergy,prepared.StartingEnergy,prepared.MaximumHealth,
                    prepared.StatPreviewScope,prepared.RotationPreviewScope,prepared.ModelingNotes,
                    entries=prepared.Rotation.Entries.Select(x=>new{x.AbilityKey,x.IsEnabled,
                        x.Priority,x.Conditions}).ToArray()});
            }
            if(cls=="hunter")
            {
                // Reuse the very same runner-built profile and action gates.
                // A bounded two-second, timeline-free preview checks learned
                // ranks, resources, stances and custom actions without adding
                // another Hunter-specific action catalog or scheduler.
                static T Copy<T>(T item)=>JsonSerializer.Deserialize<T>(
                    JsonSerializer.Serialize(item))!;
                if(request.Specialization=="marksmanship" && request.Marksmanship is { } mm)
                {
                    var test=Copy(mm);
                    test.DurationSeconds=2m;test.CaptureTimeline=false;
                    test.SoloEncounter=new SoloDpsEncounterScript();
                    var sim=new MarksmanshipHunterSimRunner().Run(test);
                    return Ok(new{mode=test.CustomPriority.Count>0?"custom":"default",
                        supported=true,execution="MarksmanshipHunterSimRunner",
                        entries=sim.OwnerPriority.Select(x=>new{x.AbilityKey,x.IsEnabled,
                            x.Priority,x.Conditions}).ToArray()});
                }
                if(request.Specialization=="survival" && request.Survival is { } sv)
                {
                    var test=Copy(sv);
                    test.DurationSeconds=2m;test.CaptureTimeline=false;
                    test.SoloEncounter=new SoloDpsEncounterScript();
                    if(test.CombatMode=="hybrid")test.SwitchAtSeconds=1m;
                    var sim=new SurvivalHunterSimRunner().Run(test);
                    return Ok(new{mode=test.CustomPriority.Count>0?"custom":"default",
                        supported=true,execution="SurvivalHunterSimRunner",
                        entries=sim.OwnerPriority.Select(x=>new{x.AbilityKey,x.IsEnabled,
                            x.Priority,x.Conditions}).ToArray()});
                }
                if(request.Specialization=="beast-mastery" && request.BeastMastery is { } bm)
                {
                    var test=Copy(bm);
                    test.DurationSeconds=2m;test.CaptureTimeline=false;
                    test.SoloEncounter=new SoloDpsEncounterScript();
                    var sim=new BeastMasteryHunterSimRunner().Run(test);
                    return Ok(new{mode=test.CustomPriority.Count>0?"custom":"default",
                        supported=true,execution="BeastMasteryHunterSimRunner",
                        entries=sim.OwnerPriority.Select(x=>new{x.AbilityKey,x.IsEnabled,
                            x.Priority,x.Conditions}).ToArray()});
                }
            }
            return BadRequest(new{error="Rotation preview requires a matching typed class and specialization."});
        }
        catch(ArgumentException e){return BadRequest(new{error=e.Message});}
        catch(InvalidOperationException e){return BadRequest(new{error=e.Message});}
    }
}