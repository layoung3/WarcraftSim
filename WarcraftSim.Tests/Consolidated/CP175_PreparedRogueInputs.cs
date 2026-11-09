using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175PreparedRogueInputs
{
    private static RogueSimRequest Input(string spec="assassination")
    {
        var r=RogueSimRequest.CreatePreset(spec);r.DurationSeconds=20m;r.CaptureTimeline=true;
        return r;
    }
    private static ForeverLevel60BaselineObservation Baseline()=>new() {
        Race="human",ClassKey="rogue",Strength=100m,Agility=200m,Stamina=100m,
        Intellect=40m,Spirit=40m,ClientBuild="controlled-test-build",
        SourceReference="synthetic-test-observation",RecordedAtUtc="2026-11-09T00:00:00Z"
    };
    private static void EqualRun(RogueSimResponse a,RogueSimResponse b)
    {
        Assert.Equal(a.DamageDone,b.DamageDone);Assert.Equal(a.EndingEnergy,b.EndingEnergy);
        Assert.Equal(a.PoisonProcs,b.PoisonProcs);Assert.Equal(a.FinishersUsed,b.FinishersUsed);
        Assert.Equal(a.Run.Timeline.Select(e=>(e.TimeSeconds,e.Type,e.AbilityKey,e.Amount,e.ResultKey)),
            b.Run.Timeline.Select(e=>(e.TimeSeconds,e.Type,e.AbilityKey,e.Amount,e.ResultKey)));
    }
    [Theory][InlineData("assassination")][InlineData("combat")][InlineData("subtlety")]
    public void PoisonToggleDoesNotMutateCallerAndCanBeReenabled(string spec)
    {
        var r=Input(spec);r.UseInstantPoison=false;var before=JsonSerializer.Serialize(r);
        var off=new RogueSimRunner().Run(r);Assert.Equal(0,off.PoisonProcs);
        Assert.Equal(before,JsonSerializer.Serialize(r));r.UseInstantPoison=true;
        var on=new RogueSimRunner().Run(r);Assert.True(on.PoisonProcs>0);
        EqualRun(new RogueSimRunner().Run(Input(spec)),on);
        EqualRun(on,new RogueSimRunner().Run(r));
    }
    [Fact]
    public void SnapshotPreservesComparersAndIsolatesNestedState()
    {
        var r=Input();r.Character.BaseStats.Values=new(StringComparer.Ordinal){["agility"]=200};
        r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="authored",Stats=new(){Values=new(StringComparer.OrdinalIgnoreCase){["STAMINA"]=4}}});
        r.Talents.Ranks=new(r.Talents.Ranks,StringComparer.Ordinal);
        var copy=RogueSimulationRequestSnapshot.Create(r);
        Assert.Same(r.Talents.Ranks.Comparer,copy.Talents.Ranks.Comparer);
        Assert.Same(r.Character.BaseStats.Values.Comparer,copy.Character.BaseStats.Values.Comparer);
        Assert.Same(r.Character.Equipment.Items[0].Stats.Values.Comparer,copy.Character.Equipment.Items[0].Stats.Values.Comparer);
        copy.Tuning.MainHandWeaponDamage+=100;copy.Talents.Ranks.Clear();
        copy.Character.Equipment.Items[0].Stats.Values["STAMINA"]=999;
        Assert.NotEqual(copy.Tuning.MainHandWeaponDamage,r.Tuning.MainHandWeaponDamage);
        Assert.NotEmpty(r.Talents.Ranks);Assert.Equal(4,r.Character.Equipment.Items[0].Stats.Get("stamina"));
    }
    [Theory][InlineData(false,4150)][InlineData(true,3150)]
    public void PreparedHealthUsesObservedOrExplicitManualCap(bool observed,int expected)
    {
        var r=Input();r.Character.Race="human";r.Level60Baseline=Baseline();
        if(observed)r.Level60Baseline.ObservedUnbuffedMaximumHealth=3000;
        r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="authored-budget",Stats=new(){Values=new(){["stamina"]=10,["health"]=50}}});
        var before=JsonSerializer.Serialize(r);var result=new RogueSimRunner().Run(r);
        Assert.Equal(expected,result.MaximumHealth);Assert.Equal(expected,result.EndingHealth);
        Assert.Equal(110,result.MaximumEnergy);Assert.Equal(before,JsonSerializer.Serialize(r));
        Assert.Contains(result.ModelingNotes,n=>n.Contains(observed?"Supplied naked Health":"manual scenario cap"));
    }
    [Theory][InlineData("assassination")][InlineData("combat")][InlineData("subtlety")]
    public void PreparedObservedApMatchesEquivalentAuthoredProfileOnce(string spec)
    {
        var r=Input(spec);r.Character.Race="human";r.Level60Baseline=Baseline();
        r.Level60Baseline.ObservedUnbuffedMeleeAttackPower=700;
        var expected=Input(spec);expected.Character=ForeverLevel60Baseline.Generate(r.Character,r.Level60Baseline);
        EqualRun(new RogueSimRunner().Run(expected),new RogueSimRunner().Run(r));
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void PreparedCatalogSelectionsFailClosed(int kind)
    {
        var r=Input();r.Character.Race="human";r.Level60Baseline=Baseline();
        r.Character.Equipment.Items.Add(new(){SlotKey="main-hand",ItemKey="unsupported",ItemId=kind==0?19102:null,
            EnchantSpellId=kind==1?20034:null,TemporaryEnchantSpellId=kind==2?25122:null});
        Assert.Throws<ArgumentException>(()=>new RogueSimRunner().Run(r));
    }
    [Fact]
    public void BaselineMustMatchRaceAndCannotCombineReviewedModes()
    {
        var r=Input();r.Character.Race="orc";r.Level60Baseline=Baseline();
        Assert.Throws<ArgumentException>(()=>new RogueSimRunner().Run(r));
        r.Level60Baseline=null;r.UseReviewedLevel60Baseline=true;r.UseProvisionalLevel60Baseline=true;
        Assert.Throws<ArgumentException>(()=>new RogueSimRunner().Run(r));
    }
    [Fact]
    public void ExplicitProvisionalModeKeepsSourceWarning()
    {
        var r=Input();r.Character.Race="human";r.UseProvisionalLevel60Baseline=true;
        var result=new RogueSimRunner().Run(r);
        Assert.Contains(result.ModelingNotes,n=>n.Contains("provisional-documented-proxy"));
        Assert.Contains(result.ModelingNotes,n=>n.Contains("NOT verified for Forever"));
    }
    [Fact]
    public async Task ConcurrentRunsDoNotShareNormalizedInputs()
    {
        var r=Input();r.UseInstantPoison=false;var before=JsonSerializer.Serialize(r);
        var results=await Task.WhenAll(Enumerable.Range(0,3).Select(_=>Task.Run(()=>new RogueSimRunner().Run(r))));
        EqualRun(results[0],results[1]);EqualRun(results[1],results[2]);
        Assert.Equal(before,JsonSerializer.Serialize(r));
    }
    [Theory][InlineData("equipment")][InlineData("items")][InlineData("entry")]
    public async Task PreparedNullEquipmentReturnsHttpBadRequest(string kind)
    {
        var r=Input();r.Character.Race="human";r.Level60Baseline=Baseline();
        if(kind=="equipment")r.Character.Equipment=null!;
        else if(kind=="items")r.Character.Equipment.Items=null!;
        else r.Character.Equipment.Items.Add(null!);
        // Exercise a JSON-shaped request and the controller's exception-to-400 boundary.
        var json=JsonSerializer.Serialize(r);
        var request=JsonSerializer.Deserialize<RogueSimRequest>(json)!;
        var response=await new WarcraftSim.Api.Controllers.RogueSimController().Run(request,default);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(response.Result);
        Assert.Equal(json,JsonSerializer.Serialize(request));
    }
    [Fact]
    public void PreparedObservedApAndRawContributionsHaveIndependentNumericalOracle()
    {
        var r=Input("combat");r.Character.Race="human";r.Level60Baseline=Baseline();
        r.Level60Baseline.ObservedUnbuffedMeleeAttackPower=700;
        r.Talents=new();r.StartInStealth=false;r.UseInstantPoison=false;
        r.StartingEnergyPercent=0;r.Tuning.EnergyPerSecond=.01m;
        r.Tuning.MainHandWeaponDamage=100;r.Tuning.MainHandSpeedSeconds=1.4m;
        r.Tuning.MainHandHitPercent=100;r.Tuning.PhysicalCritPercent=0;
        r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="synthetic-budget",
            Stats=new(){Values=new(){["strength"]=50,["agility"]=25,["attack-power"]=125}}});
        var result=new RogueSimRunner().Run(r);
        // Naked Str100/Agi200 already contribute300 inside observedAP700.
        // Raw +50Str/+25Agi/+125AP adds200 exactly once: finalAP900.
        // Existing modeled auto formula is100+900*1.4/14=190, or380 on crit.
        var swings=result.Run.Timeline.Where(e=>e.Type==WarcraftSim.Core.Simulation.Engine.CombatEventType.Damage &&
            e.AbilityKey=="rogue-main-hand").ToArray();
        Assert.NotEmpty(swings);
        Assert.All(swings,e=>Assert.Equal(e.ResultKey=="critical"?380m:190m,e.Amount));
    }

}
