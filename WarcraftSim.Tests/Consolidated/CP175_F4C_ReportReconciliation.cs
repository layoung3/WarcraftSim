using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using Xunit;
namespace WarcraftSim.Tests.Consolidated;
public sealed class Cp175F4CReportReconciliation
{
    [Theory]
    [InlineData("arcane")][InlineData("fire")][InlineData("frost")]
    public void MageDamageReconcilesWithoutCountingPeriodicDriversTwice(string spec)
    {
        var request = MageSimRequest.CreatePreset(spec);
        request.DurationSeconds = 40m; request.TargetMaximumHealth = 10000000m;
        var result = new MageSimRunner().Run(request);
        Check(result, result.DamageDone, result.Dps, 40m);
    }
    [Theory]
    [InlineData("affliction")][InlineData("demonology")][InlineData("destruction")]
    public void WarlockOwnerAndDemonReconcileWithAbilityAndDamageEvents(string spec)
    {
        var request = WarlockSimRequest.CreatePreset(spec);
        request.DurationSeconds = 40m; request.TargetMaximumHealth = 10000000m;
        var result = new WarlockSimRunner().Run(request);
        Assert.Equal(result.DamageDone, result.WarlockDamage + result.PetDamage);
        Check(result, result.DamageDone, result.Dps, 40m);
    }
    [Theory]
    [InlineData("marksmanship")][InlineData("survival")][InlineData("beast-mastery")]
    public void HunterDamageIncludesIndependentCompanionsExactlyOnce(string spec)
    {
        object result = spec switch
        {
            "marksmanship" => new MarksmanshipHunterSimRunner().Run(new MarksmanshipHunterSimRequest { DurationSeconds = 40m }),
            "survival" => new SurvivalHunterSimRunner().Run(new SurvivalHunterSimRequest { DurationSeconds = 40m }),
            _ => new BeastMasteryHunterSimRunner().Run(new BeastMasteryHunterSimRequest { DurationSeconds = 40m })
        };
        var json = JsonSerializer.SerializeToElement(result);
        var total = json.GetProperty("DamageDone").GetDecimal();
        var hawk = json.TryGetProperty("HawkDamage", out var h) ? h.GetDecimal() : 0m;
        Assert.Equal(total, json.GetProperty("HunterDamage").GetDecimal() + json.GetProperty("PetDamage").GetDecimal() + hawk);
        Check(result, total, json.GetProperty("Dps").GetDecimal(), 40m);
    }
    [Theory]
    [InlineData("arms")][InlineData("fury")][InlineData("protection")]
    public void WarriorRepresentativeRunReconcilesWithItsOwnDamage(string preset)
    {
        var request = new WarriorSimRequest { Preset=preset, DurationSeconds=40m, Iterations=1, CaptureRepresentativeTimeline=true, TankMode=preset=="protection", ShieldEquipped=preset=="protection", EnableBossAutoAttacks=preset=="protection", IsTwoHanded=preset!="protection" };
        var result = new ForeverWarriorSimRunner().Run(request);
        var run=result.Batch.RepresentativeRun!;
        var owner=run.Summary.ActorSummaries["warrior"];
        // Native Warrior reports include incoming enemy damage separately.
        var outgoing=run.Timeline.Where(e => e.Type==WarcraftSim.Core.Simulation.Engine.CombatEventType.Damage && e.SourceActorKey=="warrior").Sum(e=>e.Amount??0m);
        Assert.True(owner.DamageDone>0m);
        Assert.InRange(Math.Abs(owner.DamageDone-outgoing),0m,.000001m);
        Assert.InRange(Math.Abs(owner.DamageDone-owner.DamageDoneByAbility.Values.Sum()),0m,.000001m);
        Assert.Equal(owner.DamageDone/40m,result.MeanDps);
        var replay=new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(result.MeanDps,replay.MeanDps);
    }
    private static void Check(object response, decimal total, decimal dps, decimal duration)
    {
        var json = JsonSerializer.SerializeToElement(response);
        Assert.True(total > 0m); Assert.Equal(total / duration, dps);
        Assert.InRange(Math.Abs(total - json.GetProperty("DamageByAbility").EnumerateObject().Sum(p => p.Value.GetDecimal())), 0m, .000001m);
        var run = json.GetProperty("Run");
        var actors = run.GetProperty("Summary").GetProperty("ActorSummaries");
        Assert.InRange(Math.Abs(total - actors.EnumerateObject().Sum(p => p.Value.GetProperty("DamageDone").GetDecimal())), 0m, .000001m);
        // PeriodicTick is a scheduler receipt, not an additional damage contribution.
        var damageEvents = run.GetProperty("Timeline").EnumerateArray().Where(e => e.GetProperty("Type").GetInt32() == 6).Sum(e => e.GetProperty("Amount").GetDecimal());
        Assert.InRange(Math.Abs(total - damageEvents), 0m, .000001m);
    }
}
