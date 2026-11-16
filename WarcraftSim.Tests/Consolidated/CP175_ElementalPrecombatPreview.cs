using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175ElementalPrecombatPreview
{
    private static ElementalShamanSimRequest Request(bool prepared=true)
    {
        var r=new ElementalShamanSimRequest {DurationSeconds=12m,MajorManaPotionCharges=0};
        r.Character.Race="orc";
        r.Character.Equipment=new();r.Character.Buffs=new();r.Character.Talents=new();
        r.Character.BaseStats=new();
        r.Talents.Ranks.Clear();
        if(prepared)r.Level60Baseline=new() {
            Race="orc",ClassKey="shaman",Strength=40m,Agility=40m,Stamina=100m,
            Intellect=200m,Spirit=100m,ObservedUnbuffedMaximumMana=5000m,
            ObservedUnbuffedMaximumHealth=3000m,ClientBuild="1.60.1.70707",
            SourceReference="controlled-test-observation",RecordedAtUtc="2026-11-09T00:00:00Z"
        };
        return r;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreviewAndExecutionShareResourcesPowerAndAdaptiveRotationWithoutMutation(bool prepared)
    {
        var r=Request(prepared);
        r.Character.BaseStats.Set("intellect",80m);
        r.Character.Equipment.Items=[new() {SlotKey="head",ItemKey="manual-test-budget",
            Stats=new() {Values=new(StringComparer.Ordinal) {
                ["intellect"]=20m,["spell-power"]=300m,["haste-percent"]=12m,
                ["cast-haste-percent"]=8m }}}];
        r.Talents.Ranks["ancestral-knowledge"]=5;
        if(prepared) {
            r.ExternalModifiers.Buffs=["arcane-intellect","power-word-fortitude"];
            r.ExternalModifiers.Consumables=["greater-arcane-elixir"];
        }
        var original=JsonSerializer.Serialize(r);
        var preview=ElementalShamanSimRunner.Preview(r);
        var sim=new ElementalShamanSimRunner().Run(r);
        Assert.Equal(sim.MaximumMana,preview.MaximumMana);
        Assert.Equal(sim.MaximumHealth,preview.MaximumHealth);
        Assert.Equal(sim.SpellPower,preview.SpellPower);
        Assert.Equal(sim.ResolvedRotation,string.Join(" > ",preview.Rotation.Entries.Select(x=>x.AbilityKey)));
        Assert.Equal(prepared?276.1m:110m,preview.EffectiveStats.Get("intellect"));
        Assert.Equal(prepared?20m:0m,preview.HardcastHastePercent);
        Assert.Equal(0,sim.MajorManaPotionUses);
        Assert.Equal(original,JsonSerializer.Serialize(r));
        Assert.Equal(StringComparer.Ordinal,r.Character.Equipment.Items[0].Stats.Values.Comparer);
        preview.EffectiveStats.Set("intellect",99999m);
        Assert.NotEqual(99999m,ElementalShamanSimRunner.Preview(r).EffectiveStats.Get("intellect"));
        Assert.Contains("display-only",preview.StatPreviewScope);
    }

    [Fact]
    public void PreviewIncludesManualAndTalentRollBonusesAndRuntimeHitAlias()
    {
        var r=Request();
        r.Character.Equipment.Items=[new(){SlotKey="head",ItemKey="manual-test-budget",
            Stats=new(){Values=new(){["spell-hit-chance-percent"]=2m,["spell-hit-percent"]=1m,
                ["spell-critical-chance-percent"]=3m}}}];
        var before=ElementalShamanSimRunner.Preview(r);
        Assert.Equal(86m,before.EffectiveStats.Get("spell-hit-chance-percent"));
        r.Talents.Ranks["tidal-focus"]=3;r.Talents.Ranks["thundering-strikes"]=5;
        var after=ElementalShamanSimRunner.Preview(r);
        Assert.Equal(89m,after.EffectiveStats.Get("spell-hit-chance-percent"));
        Assert.Equal(before.EffectiveStats.Get("spell-critical-chance-percent")+5m,
            after.EffectiveStats.Get("spell-critical-chance-percent"));
        r.BaseSpellHitPercent=100m;r.SpellCritPercent=100m;
        var capped=ElementalShamanSimRunner.Preview(r);
        Assert.Equal(100m,capped.EffectiveStats.Get("spell-hit-chance-percent"));
        Assert.Equal(100m,capped.EffectiveStats.Get("spell-critical-chance-percent"));
    }

    [Fact]
    public void ControllerAndRotationUseTheSamePreparation()
    {
        var r=Request();r.ExternalModifiers.Consumables=["flask-of-supreme-power"];
        var preview=Assert.IsType<ElementalPrecombatPreview>(Assert.IsType<OkObjectResult>(
            new ForeverCharacterController().PreviewElemental(r)).Value);
        var result=Assert.IsType<OkObjectResult>(new ForeverSoloRotationController().Preview(
            new(){ClassKey="shaman",Specialization="elemental",Elemental=r}));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal(preview.SpellPower,json.RootElement.GetProperty("SpellPower").GetDecimal());
        Assert.Equal(JsonSerializer.Serialize(preview.Rotation.Entries.Select(x=>new {
            x.AbilityKey,x.IsEnabled,x.Priority,x.Conditions})),json.RootElement.GetProperty("entries").GetRawText());
    }

    [Theory]
    [InlineData("target-debuff")]
    [InlineData("catalog")]
    [InlineData("potion")]
    [InlineData("priority")]
    [InlineData("tuning")]
    public void PreviewRejectsInputsRejectedByExecution(string invalid)
    {
        var r=Request();
        switch(invalid) {
            case "target-debuff":r.ExternalModifiers.TargetDebuffs=["curse-of-elements"];break;
            case "catalog":r.Character.Equipment.Items=[new(){SlotKey="head",ItemId=19102}];break;
            case "potion":r.MajorManaPotionCharges=-1;break;
            case "priority":r.CustomPriority=["unmodeled-action"];break;
            case "tuning":r.Tuning.LightningBoltDamage=-1m;break;
        }
        Assert.Throws<ArgumentException>(()=>new ElementalShamanSimRunner().Run(r));
        Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewElemental(r));
        Assert.IsType<BadRequestObjectResult>(new ForeverSoloRotationController().Preview(
            new(){ClassKey="shaman",Specialization="elemental",Elemental=r}));
    }

    [Theory]
    [InlineData("lava-burst")]
    [InlineData("LAVA-BURST")]
    [InlineData("elemental-mastery")]
    [InlineData("ElEmEnTaL-MaStErY")]
    [InlineData("lightning-bolt-rank7")]
    [InlineData("LIGHTNING-BOLT-RANK7")]
    public void CustomActionCaseCannotBypassLearnedOrDownrankingGate(string key)
    {
        var r=Request();r.CustomPriority=[key];
        Assert.Throws<ArgumentException>(()=>ElementalAdaptiveRotation.Build(r,0m));
        Assert.Throws<ArgumentException>(()=>ElementalShamanSimRunner.Preview(r));
        Assert.Throws<ArgumentException>(()=>new ElementalShamanSimRunner().Run(r));
        Assert.Equal(key,r.CustomPriority.Single());
    }

    [Theory]
    [InlineData("lava-burst")]
    [InlineData("elemental-mastery")]
    [InlineData("lightning-bolt-rank7")]
    [InlineData("flame-shock")]
    public void AdmittedCaseAliasesUseCanonicalActionsAndConditionsWithoutMutatingInput(string key)
    {
        var r=Request();r.AllowDownranking=true;
        r.Talents.Ranks["lava-burst"]=1;r.Talents.Ranks["elemental-mastery"]=1;
        r.CustomPriority=[key];
        var canonical=ElementalShamanSimRunner.Preview(r);
        var canonicalRun=new ElementalShamanSimRunner().Run(r);
        r.CustomPriority=[key.ToUpperInvariant(),key];
        var aliased=ElementalShamanSimRunner.Preview(r);
        var aliasedRun=new ElementalShamanSimRunner().Run(r);
        Assert.Equal(JsonSerializer.Serialize(canonical.Rotation.Entries.Select(x=>new {
            x.AbilityKey,x.Priority,x.IsEnabled,x.Conditions})),
            JsonSerializer.Serialize(aliased.Rotation.Entries.Select(x=>new {
                x.AbilityKey,x.Priority,x.IsEnabled,x.Conditions})));
        Assert.Equal(key,aliased.Rotation.Entries.Single().AbilityKey);
        Assert.Equal(canonicalRun.DamageDone,aliasedRun.DamageDone);
        Assert.Equal(canonicalRun.EndingMana,aliasedRun.EndingMana);
        Assert.Equal(canonicalRun.ElementalMasteryCasts,aliasedRun.ElementalMasteryCasts);
        Assert.Equal(key.ToUpperInvariant(),r.CustomPriority[0]);
        Assert.Equal(2,r.CustomPriority.Count);
    }
}
