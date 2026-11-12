using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
namespace WarcraftSim.Tests.Consolidated;
public sealed class Cp175ShadowPrecombatPreview
{
    private static ShadowPriestSimRequest Request(int mode)
    {
        var r=new ShadowPriestSimRequest {DurationSeconds=90m,Seed=175902,CaptureTimeline=false};
        if(mode>0){r.Character.Race="human";r.UseProvisionalLevel60Baseline=mode==1;}
        if(mode==2)r.Level60Baseline=new(){Race="human",ClassKey="priest",Strength=40m,
            Agility=40m,Stamina=100m,Intellect=200m,Spirit=100m,
            ObservedUnbuffedMaximumMana=5000m,ObservedUnbuffedMaximumHealth=3000m,
            ClientBuild="1.60.1.70707",SourceReference="controlled-test-observation",
            RecordedAtUtc="2026-11-10T00:00:00Z"};
        return r;
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CanonicalSeededMechanicsPreserved(int mode)
    {
        var r=new ShadowPriestSimRunner().Run(Request(mode));
        var evidence=JsonSerializer.Serialize(new {r.DamageDone,r.HealingDone,r.EndingMana,
            r.MaximumMana,r.MaximumHealth,r.EndingHealth,r.SpellPower,r.SpellCritPercent,
            r.MaximumShadowWeavingStacks,r.PriestDied,r.DamageByAbility,r.Run.ProcessedEventCount});
        // Accepted catalog-head goldens were reproduced with the archived runtime.
        // Every changed full envelope matches that old runtime with base hit83-5:
        // old78+2*5 equals corrected83+1*5, preserving the complete RNG path.
        var expected=mode switch {
            0=>"""{"DamageDone":27774.216580000000,"HealingDone":0,"EndingMana":21.37500,"MaximumMana":3946.00,"MaximumHealth":3500,"EndingHealth":3500,"SpellPower":150,"SpellCritPercent":3.1932773109243697478991596639,"MaximumShadowWeavingStacks":5,"PriestDied":false,"DamageByAbility":{"shadow-word-pain":5824.48020000,"devouring-plague":4497.71520000,"mind-blast":5902.71880000,"mind-flay":11549.302380000000},"ProcessedEventCount":1038}""",
            1=>"""{"DamageDone":19301.895800000000,"HealingDone":0,"EndingMana":26.31250000000000000000000000,"MaximumMana":2876.00,"MaximumHealth":1897,"EndingHealth":1897,"SpellPower":0,"SpellCritPercent":2.0168067226890756302521008403,"MaximumShadowWeavingStacks":5,"PriestDied":false,"DamageByAbility":{"shadow-word-pain":4035.37420000,"devouring-plague":3940.14720000,"mind-blast":3913.14000000,"mind-flay":7413.234400000000},"ProcessedEventCount":883}""",
            _=>"""{"DamageDone":24355.436600000000,"HealingDone":0,"EndingMana":417.50,"MaximumMana":5000.00,"MaximumHealth":3000,"EndingHealth":3000,"SpellPower":0,"SpellCritPercent":3.3613445378151260504201680672,"MaximumShadowWeavingStacks":5,"PriestDied":false,"DamageByAbility":{"shadow-word-pain":4711.52220000,"devouring-plague":3940.14720000,"mind-blast":5217.52000000,"mind-flay":10486.247200000000},"ProcessedEventCount":1077}"""
        };
        Assert.Equal(expected,evidence);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void PreviewSharesExactResourcesAndIsolatesCaller(int mode)
    {
        var r=Request(mode);
        r.Character.Equipment.Items=[new(){SlotKey="head",ItemKey="manual-test-budget",
            Stats=new(){Values=new(StringComparer.Ordinal){["intellect"]=20m,["stamina"]=10m,
                ["spell-power"]=80m,["shadow-power"]=30m,["haste-percent"]=15m}}}];
        var before=JsonSerializer.Serialize(r);
        var p=ShadowPriestSimRunner.Preview(r);var sim=new ShadowPriestSimRunner().Run(r);
        Assert.Equal(sim.MaximumMana,p.MaximumMana);Assert.Equal(sim.MaximumHealth,p.MaximumHealth);
        Assert.Equal(sim.SpellPower,p.SpellPower);Assert.Equal(sim.SpellCritPercent,p.EffectiveStats.Get("spell-critical-chance-percent"));
        Assert.Equal(0m,p.HardcastHastePercent);
        Assert.Equal(before,JsonSerializer.Serialize(r));
        Assert.Equal(StringComparer.Ordinal,r.Character.Equipment.Items[0].Stats.Values.Comparer);
        p.EffectiveStats.Set("intellect",99999m);
        Assert.NotEqual(99999m,ShadowPriestSimRunner.Preview(r).EffectiveStats.Get("intellect"));
    }
    [Fact]
    public void ObservedManaCrossesFirstTwentyIntellectBoundaryUsingPriestFormula()
    {
        var r=Request(2);r.Level60Baseline!.Intellect=10m;
        r.Character.Equipment.Items=[new(){SlotKey="head",ItemKey="manual-test-budget",
            Stats=new(){Values=new(){["intellect"]=20m,["mana"]=100m}}}];
        // Ten points at one Mana plus ten at fifteen, then the explicit flat Mana.
        Assert.Equal(5260m,ShadowPriestSimRunner.Preview(r).MaximumMana);
        Assert.Equal(5260m,new ShadowPriestSimRunner().Run(r).MaximumMana);
    }
    [Fact]
    public void ManualOverridesAndPassiveHitUseActualRuntimeInputs()
    {
        var r=Request(2);r.SpellPowerOverride=321m;r.SpellCritOverridePercent=17m;
        r.Character.BaseStats.Set("spell-hit-percent",99m); // baseline replaces raw base sheet
        r.Character.Equipment.Items=[new(){SlotKey="head",ItemKey="manual-test-budget",
            Stats=new(){Values=new(){["spell-hit-percent"]=2m}}}];
        var p=ShadowPriestSimRunner.Preview(r);
        Assert.Equal(321m,p.SpellPower);Assert.Equal(17m,p.EffectiveStats.Get("spell-critical-chance-percent"));
        Assert.Equal(90m,p.EffectiveStats.Get("spell-hit-chance-percent"));
        r.BaseSpellHitPercent=100m;
        Assert.Equal(100m,ShadowPriestSimRunner.Preview(r).EffectiveStats.Get("spell-hit-chance-percent"));
    }
    [Fact]
    public void ControllersExposeOnlyFixedLearnedTogglePriority()
    {
        var r=Request(2);r.UseMindBlast=false;r.UseInnerFocus=true;r.UsePowerInfusion=true;
        var p=Assert.IsType<ShadowPrecombatPreview>(Assert.IsType<OkObjectResult>(
            new ForeverCharacterController().PreviewShadow(r)).Value);
        Assert.DoesNotContain(p.Rotation.Entries,x=>x.AbilityKey is "mind-blast" or "inner-focus" or "power-infusion");
        Assert.Contains(p.Rotation.Entries,x=>x.AbilityKey=="shadow-word-death"&&x.Conditions.Count>0);
        Assert.Equal("fixed-toggle-priority-no-custom-rotation",p.RotationPreviewScope);
        var result=Assert.IsType<OkObjectResult>(new ForeverSoloRotationController().Preview(
            new(){ClassKey="priest",Specialization="shadow",Shadow=r}));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal(p.SpellPower,json.RootElement.GetProperty("SpellPower").GetDecimal());
        Assert.Equal(JsonSerializer.Serialize(p.Rotation.Entries.Select(x=>new{x.AbilityKey,x.IsEnabled,x.Priority,x.Conditions})),
            json.RootElement.GetProperty("entries").GetRawText());
    }
    [Theory]
    [InlineData("catalog")]
    [InlineData("assumptions")]
    [InlineData("health")]
    [InlineData("spell-power")]
    public void RejectionMatchesRuntime(string invalid)
    {
        var r=Request(2);
        switch(invalid){
            case "catalog":r.Character.Equipment.Items=[new(){SlotKey="head",ItemId=19102}];break;
            case "assumptions":r.StatAssumptions.IntellectPerSpellCritPercent=0m;break;
            case "health":r.PriestMaximumHealth=0m;break;
            case "spell-power":r.SpellPowerOverride=-2m;break;
        }
        Assert.Throws<ArgumentException>(()=>new ShadowPriestSimRunner().Run(r));
        Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewShadow(r));
        Assert.IsType<BadRequestObjectResult>(new ForeverSoloRotationController().Preview(
            new(){ClassKey="priest",Specialization="shadow",Shadow=r}));
    }
}
