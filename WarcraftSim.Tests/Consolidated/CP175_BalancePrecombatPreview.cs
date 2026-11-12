using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
namespace WarcraftSim.Tests.Consolidated;
public sealed class Cp175BalancePrecombatPreview
{
    private static BalanceDruidSimRequest Request(bool prepared)
    {
        var r=new BalanceDruidSimRequest {DurationSeconds=90m,Seed=175901,CaptureTimeline=false};
        if(prepared) {
            r.Character.Race="night-elf";r.UseProvisionalLevel60Baseline=true;
            r.MajorManaPotionCharges=0;
            r.ExternalModifiers.Buffs=["arcane-intellect"];
            r.ExternalModifiers.Consumables=["greater-arcane-elixir"];
        }
        return r;
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeededCanonicalMechanicsRemainUnchanged(bool prepared)
    {
        var r=new BalanceDruidSimRunner().Run(Request(prepared));
        var evidence=JsonSerializer.Serialize(new {r.DamageDone,r.EndingMana,r.MaximumMana,
            r.MaximumHealth,r.SpellPower,r.EclipseChargesGenerated,r.EclipseEmpoweredStarfires,
            r.OmenProcs,r.OmenConsumed,r.NaturesGraceProcs,r.MajorManaPotionUses,
            r.DamageByAbility,r.Run.ProcessedEventCount});
        // Pre-refactor 80007ee nondamage contract, excluding random event identities.
        // Vengeance damage leaves independently reprojected from complete old/new
        // timelines: only rank5 critical events scale by .8; costs/timing stay equal.
        var expected=prepared
            ? """{"DamageDone":7746.932050000,"EndingMana":24.300000000000000000000000000,"MaximumMana":2909,"MaximumHealth":2173,"SpellPower":35,"EclipseChargesGenerated":10,"EclipseEmpoweredStarfires":6,"OmenProcs":1,"OmenConsumed":1,"NaturesGraceProcs":0,"MajorManaPotionUses":0,"DamageByAbility":{"moonfire":1125.898950000,"insect-swarm":1434.53310000,"wrath":2440.90000,"starfire":2745.600},"ProcessedEventCount":425}"""
            : """{"DamageDone":20055.929850000,"EndingMana":473.70200623644100000000000000,"MaximumMana":4526,"MaximumHealth":3400,"SpellPower":210,"EclipseChargesGenerated":20,"EclipseEmpoweredStarfires":20,"OmenProcs":3,"OmenConsumed":3,"NaturesGraceProcs":0,"MajorManaPotionUses":1,"DamageByAbility":{"moonfire":3183.745950000,"insect-swarm":3039.24390000,"wrath":2781.24000,"starfire":11051.700},"ProcessedEventCount":559}""";
        Assert.Equal(expected,evidence);
    }

    [Theory]
    [InlineData("insect-swarm")]
    [InlineData("INSECT-SWARM")]
    [InlineData("InSeCt-SwArM")]
    public void LearnedGateCannotBeBypassedByCase(string key)
    {
        var r=Request(true);r.Talents.Ranks.Remove("insect-swarm");r.CustomPriority=[key];
        Assert.Throws<ArgumentException>(()=>BalanceAdaptiveRotation.Build(r));
        Assert.Throws<ArgumentException>(()=>BalanceDruidSimRunner.Preview(r));
        Assert.Throws<ArgumentException>(()=>new BalanceDruidSimRunner().Run(r));
        Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewBalance(r));
    }
    [Theory]
    [InlineData("insect-swarm")]
    [InlineData("moonfire")]
    [InlineData("innervate")]
    [InlineData("faerie-fire")]
    public void ValidAliasesKeepConditionsAndSeededGameplayWithoutInputMutation(string key)
    {
        var r=Request(true);r.CustomPriority=[key,"wrath"];
        var canonical=BalanceDruidSimRunner.Preview(r);
        var first=new BalanceDruidSimRunner().Run(r);
        r.CustomPriority=[key.ToUpperInvariant(),key,"WRATH"];
        var original=JsonSerializer.Serialize(r);
        var alias=BalanceDruidSimRunner.Preview(r);
        var second=new BalanceDruidSimRunner().Run(r);
        Assert.Equal(JsonSerializer.Serialize(canonical.Rotation.Entries.Select(x=>new {
            x.AbilityKey,x.Priority,x.Conditions})),JsonSerializer.Serialize(alias.Rotation.Entries.Select(x=>new {
            x.AbilityKey,x.Priority,x.Conditions})));
        Assert.Equal(first.DamageDone,second.DamageDone);Assert.Equal(first.EndingMana,second.EndingMana);
        Assert.Equal(first.Run.ProcessedEventCount,second.Run.ProcessedEventCount);
        Assert.Equal(original,JsonSerializer.Serialize(r));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactPreviewMatchesRuntimeAndLeavesCallerIsolated(bool prepared)
    {
        var r=Request(prepared);
        r.Character.Equipment.Items=[new(){SlotKey="head",ItemKey="manual-test-budget",
            Stats=new(){Values=new(StringComparer.Ordinal){["intellect"]=20m,["spell-power"]=80m,
                ["haste-percent"]=12m,["cast-haste-percent"]=8m}}}];
        var before=JsonSerializer.Serialize(r);
        var preview=BalanceDruidSimRunner.Preview(r);
        var sim=new BalanceDruidSimRunner().Run(r);
        Assert.Equal(sim.MaximumMana,preview.MaximumMana);
        Assert.Equal(sim.MaximumHealth,preview.MaximumHealth);Assert.Equal(sim.SpellPower,preview.SpellPower);
        Assert.Equal(prepared?20m:0m,preview.HardcastHastePercent);
        Assert.Contains("initial-priority",preview.RotationPreviewScope);
        Assert.Contains(preview.ModelingNotes,n=>n.Contains("reorders"));
        Assert.Equal(before,JsonSerializer.Serialize(r));
        Assert.Equal(StringComparer.Ordinal,r.Character.Equipment.Items[0].Stats.Values.Comparer);
        preview.EffectiveStats.Set("intellect",99999m);
        Assert.NotEqual(99999m,BalanceDruidSimRunner.Preview(r).EffectiveStats.Get("intellect"));
    }
    [Fact]
    public void PreviewsUseActualPassiveRollBonusesAndTypedRoutes()
    {
        var r=Request(true);r.Talents.Ranks.Clear();
        r.Character.Equipment.Items=[new(){SlotKey="head",ItemKey="manual-test-budget",
            Stats=new(){Values=new(){["spell-hit-percent"]=1m,["spell-hit-chance-percent"]=2m}}}];
        var initial=BalanceDruidSimRunner.Preview(r);
        r.Talents.Ranks["natures-reach"]=2;r.Talents.Ranks["natures-majesty"]=2;
        r.Talents.Ranks["moonkin-form"]=1;
        var after=Assert.IsType<BalancePrecombatPreview>(Assert.IsType<OkObjectResult>(
            new ForeverCharacterController().PreviewBalance(r)).Value);
        Assert.Equal(90m,after.EffectiveStats.Get("spell-hit-chance-percent"));
        Assert.Equal(initial.EffectiveStats.Get("spell-critical-chance-percent")+7m,
            after.EffectiveStats.Get("spell-critical-chance-percent"));
        var result=Assert.IsType<OkObjectResult>(new ForeverSoloRotationController().Preview(
            new(){ClassKey="druid",Specialization="balance",Balance=r}));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal(after.SpellPower,json.RootElement.GetProperty("SpellPower").GetDecimal());
        Assert.Equal(after.RotationPreviewScope,json.RootElement.GetProperty("RotationPreviewScope").GetString());
    }
    [Theory]
    [InlineData("catalog")]
    [InlineData("debuff")]
    [InlineData("potion")]
    [InlineData("priority")]
    public void PreviewAndExecutionRejectUnsupportedInputs(string invalid)
    {
        var r=Request(true);
        switch(invalid){
            case "catalog":r.Character.Equipment.Items=[new(){SlotKey="head",ItemId=19102}];break;
            case "debuff":r.ExternalModifiers.TargetDebuffs=["curse-of-elements"];break;
            case "potion":r.MajorManaPotionCharges=-1;break;
            case "priority":r.CustomPriority=["unknown"];break;
        }
        Assert.Throws<ArgumentException>(()=>new BalanceDruidSimRunner().Run(r));
        Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewBalance(r));
        Assert.IsType<BadRequestObjectResult>(new ForeverSoloRotationController().Preview(
            new(){ClassKey="druid",Specialization="balance",Balance=r}));
    }
}
