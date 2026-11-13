using System.Text.Json;
using System.Text.Json.Nodes;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Simulation;

namespace WarcraftSim.Tests.Consolidated;

public sealed partial class Cp175SharedCharacterInputs
{
    private static CharacterProfile Profile(string cls="rogue")=>new() {
        RulesetKey="forever",ClassKey=cls,Level=60,Race=cls=="druid"?"night-elf":"orc"
    };
    private static ForeverLevel60BaselineObservation Baseline(string cls="rogue")=>new() {
        ClassKey=cls,Race=cls=="druid"?"night-elf":"orc",Strength=100m,Agility=100m,
        Stamina=100m,Intellect=150m,Spirit=75m,ObservedUnbuffedMeleeAttackPower=700m,
        ObservedUnbuffedMaximumMana=5000m,ObservedUnbuffedMaximumHealth=3000m,
        ClientBuild="1.60.1.70707",SourceReference="controlled-test-observation",
        RecordedAtUtc="2026-11-12T00:00:00Z"
    };
    private static StatCollection Stats(params (string Key,decimal Value)[] entries)=>new() {
        Values=entries.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase)
    };
    private static CharacterEquipmentItem Manual(params (string Key,decimal Value)[] entries)=>new() {
        SlotKey="chest",ItemKey="authored-test-budget",Stats=Stats(entries)
    };
    private static ForeverSharedCharacterInputPreparation Prepare(CharacterProfile p,
        ForeverExternalModifierSelection? modifiers=null,ForeverLevel60BaselineObservation? baseline=null,
        decimal duration=30m)=>ForeverSharedCharacterInputs.Prepare(p,baseline,modifiers??new(),duration);
    private static StatCollection Calculate(CharacterProfile p)=>ForeverCharacterSimulationRuntimeCalculatorFactory
        .CreateStandard(_=>3000m).Calculate(p).EffectiveStats;

    [Theory]
    [InlineData("rogue")]
    [InlineData("shaman")]
    public void RawGearAndSelectedModifiersDeriveOnceFromObservedBaseline(string cls)
    {
        var source=Profile(cls);
        source.BaseStats.Set("attack-power",9999m); // Observation owns naked totals.
        source.Equipment.Items=[Manual(("strength",15m),("agility",20m),("stamina",10m))];
        var modifiers=new ForeverExternalModifierSelection {
            Buffs=["battle-shout","power-word-fortitude"],
            Consumables=["elixir-of-greater-strength","elixir-of-the-mongoose"]
        };
        var original=JsonSerializer.Serialize(source);
        var originalModifiers=JsonSerializer.Serialize(modifiers);
        var prepared=Prepare(source,modifiers,Baseline(cls));
        var runtime=ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_=>3000m)
            .Calculate(prepared.Character);
        var stats=runtime.EffectiveStats;
        Assert.Equal(140m,stats.Get("strength"));
        Assert.Equal(145m,stats.Get("agility"));
        Assert.Equal(180m,stats.Get("stamina"));
        Assert.Equal(cls=="rogue"?785m:780m,stats.Get("attack-power"));
        Assert.Equal(139m,stats.Get("melee-attack-power"));
        Assert.Single(runtime.StatContributions,x=>x.StatKey=="strength" &&
            x.SourceKey=="equipment:chest:authored-test-budget");
        Assert.Single(runtime.StatContributions,x=>x.StatKey=="stamina" &&
            x.SourceKey=="buff:cp173-power-word-fortitude");
        Assert.Equal(original,JsonSerializer.Serialize(source));
        Assert.Equal(originalModifiers,JsonSerializer.Serialize(modifiers));
        Assert.Contains(prepared.Notes,x=>x.Contains("manual inputs"));
        Assert.Contains(prepared.Notes,x=>x.Contains("elixir-of-the-mongoose") && x.Contains("provisional"));
        Assert.Empty(prepared.Weapons);Assert.Empty(prepared.Effects);Assert.Empty(prepared.Eligibility);

        // Editing and re-preparing the original creates no permanent contribution.
        source.Equipment.Items.Clear();
        var removed=Calculate(Prepare(source,baseline:Baseline(cls)).Character);
        Assert.Equal(700m,removed.Get("attack-power"));
        Assert.Equal(0m,removed.Get("melee-attack-power"));
        Assert.Equal(100m,removed.Get("stamina"));
        Assert.Equal(9999m,source.BaseStats.Get("attack-power"));
    }

    [Fact]
    public void TalentPrimaryMultiplierRunsAfterGearAndBuffsBeforeDerivedCrit()
    {
        var source=Profile("shaman");
        var item=Manual(("intellect",20m));
        item.Modifiers.Add(new() {PlacementKey="authored",ModifierKey="manual-intellect-enhancement",
            ModifierType="enchant",Stats=Stats(("intellect",4m))});
        source.Equipment.Items=[item];
        var prepared=Prepare(source,new(){Buffs=["arcane-intellect"]},Baseline("shaman"));
        var runtime=ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_=>3000m,
            additionalStatContributors:[new IntellectMultiplier(1.1m)]).Calculate(prepared.Character);
        Assert.Equal(225.5m,runtime.EffectiveStats.Get("intellect"));
        Assert.Equal(225.5m/ForeverPrimarySpellStatProfiles.GetIntellectPerSpellCriticalPercent("shaman",60),
            runtime.EffectiveStats.Get("spell-critical-chance-percent"));
        Assert.Equal(20m,source.Equipment.Items[0].Stats.Get("intellect"));
        Assert.Equal(4m,source.Equipment.Items[0].Modifiers[0].Stats.Get("intellect"));
    }

    private sealed class IntellectMultiplier(decimal factor):ICharacterSimulationStatContributor
    {
        public string Key=>"test-intellect-before-derived";
        public int Order=>CharacterSimulationStatContributorOrders.Derived-1;
        public void Contribute(CharacterProfile p,StatCollection s)=>s.Set("intellect",s.Get("intellect")*factor);
    }

    [Fact]
    public void SharedElixirComponentAndDistinctContributorsDoNotDuplicate()
    {
        var p=Profile("shaman");
        p.Equipment.Items=[Manual(("mp5",3m),("mana-per-5",3m))];
        p.Buffs.Selections.Add(new(){BuffKey="manual-mana-source",Stats=Stats(("mana-per-5",4m))});
        var result=Prepare(p,new(){Consumables=["elixir-of-the-mongoose","elixir-of-the-owl","grilled-squid"]});
        var stats=Calculate(result.Character);
        Assert.Equal(7m,stats.Get("mp5"));Assert.Equal(0m,stats.Get("mana-per-5"));
        Assert.Equal(3m,stats.Get("attack-critical-chance-percent")-25m/19.7m);
        Assert.Single(result.Character.Buffs.Selections,x=>x.BuffKey==ForeverExternalModifierCatalog.ElixirCriticalContributor);
        result.Character.Equipment.Items[0].Stats.Set("mp5",999m);
        Assert.Equal(3m,p.Equipment.Items[0].Stats.Get("mp5"));
        Assert.Equal(3m,p.Equipment.Items[0].Stats.Get("mana-per-5"));
    }

    [Theory]
    [InlineData("battle-shout","battle-shout")]
    [InlineData("cp173-battle-shout","blessing-of-might")]
    [InlineData(" CP173-ARCANE-INTELLECT ","arcane-intellect")]
    public void AuthoredKnownModifierAliasesCannotStackWithSelectedEffects(string alias,string selected)
    {
        var p=Profile();p.Buffs.Selections.Add(new(){BuffKey=alias,Stats=Stats(("attack-power",9m))});
        var before=JsonSerializer.Serialize(p);
        Assert.Throws<ArgumentException>(()=>Prepare(p,new(){Buffs=[selected]}));
        Assert.Equal(before,JsonSerializer.Serialize(p));
    }

    [Fact]
    public void InvalidPreparationIsTransactionalAndUnavailableEffectsRemainExplicit()
    {
        var p=Profile();p.Equipment.Items=[Manual(("strength",10m)),Manual(("agility",10m))];
        var before=JsonSerializer.Serialize(p);
        Assert.Throws<ArgumentException>(()=>Prepare(p));
        Assert.Equal(before,JsonSerializer.Serialize(p));
        p.Equipment.Items.RemoveAt(1);
        p.Equipment.Items[0].Stats.Set("mp5",2m);p.Equipment.Items[0].Stats.Set("mana-per-5",3m);
        before=JsonSerializer.Serialize(p);
        Assert.Throws<ArgumentException>(()=>Prepare(p));Assert.Equal(before,JsonSerializer.Serialize(p));
        p.Equipment.Items.Clear();
        Assert.Throws<ArgumentException>(()=>Prepare(p,new(){TargetDebuffs=["faerie-fire-rank4"]}));
        Assert.Throws<ArgumentException>(()=>Prepare(p,new(){Buffs=["battle-shout"]},duration:181m));
        p.Buffs.Selections.Add(null!);
        Assert.Throws<ArgumentException>(()=>Prepare(p));
    }

    [Fact]
    public void TargetDebuffsRequireExplicitAdapterAndNeverEnterSelfStats()
    {
        var p=Profile();p.BaseStats.Set("armor",1000m);
        var selected=new ForeverExternalModifierSelection {
            TargetDebuffs=["faerie-fire-rank4"],TargetDebuffMode="maintained"
        };
        Assert.Throws<ArgumentException>(()=>Prepare(p,selected));
        var prepared=ForeverSharedCharacterInputs.Prepare(p,null,selected,50m,["faerie-fire-rank4"]);
        Assert.Equal(1000m,Calculate(prepared.Character).Get("armor"));
        Assert.Empty(prepared.Character.Buffs.Selections);
        Assert.Contains(prepared.Notes,x=>x.Contains("faerie-fire-rank4") && x.Contains("maintained") && x.Contains("40s"));
        selected.TargetDebuffs=["curse-of-recklessness"];
        Assert.Throws<ArgumentException>(()=>ForeverSharedCharacterInputs.Prepare(p,null,selected,50m,
            ["curse-of-recklessness"]));
    }

    [Theory]
    [InlineData("warrior")][InlineData("paladin")][InlineData("hunter")]
    [InlineData("rogue")][InlineData("priest")][InlineData("shaman")]
    [InlineData("mage")][InlineData("warlock")][InlineData("druid")]
    public void UnknownRealItemRestrictionStaysPendingForEveryClass(string cls)
    {
        var item=ForeverEquipmentCatalog.FindItem(19682);
        var assessment=ForeverEquipmentEligibility.Assess(item,cls);
        Assert.Equal("pending",assessment.RestrictionStatus);
        Assert.Equal("proven",assessment.ProficiencyStatus);
        Assert.Equal("supported",assessment.EffectStatus);Assert.False(assessment.CanExecute);
        Assert.Null(assessment.RestrictionSourceSha256);
        Assert.Equal(item.SourceUrl,assessment.ValueSourceUrl);
        var p=Profile(cls);p.Equipment.Items=[new(){ItemId=19682,SlotKey="chest"}];
        var before=JsonSerializer.Serialize(p);
        var error=Assert.Throws<ArgumentException>(()=>Prepare(p));
        Assert.Contains("pending",error.Message);Assert.Equal(before,JsonSerializer.Serialize(p));
    }

    [Fact]
    public void RestrictionProficiencyAndEffectCoverageAreIndependentEvidence()
    {
        var item=SyntheticEquipmentRestrictionFixture.Item();
        var proven=ForeverEquipmentEligibility.Assess(item,"druid");
        Assert.True(proven.CanExecute);Assert.Equal("proven",proven.RestrictionStatus);
        Assert.Equal(SyntheticEquipmentRestrictionFixture.SparseDigest,proven.RestrictionSourceSha256);
        Assert.Equal("provisional",proven.RestrictionConfidence);
        Assert.Equal("denied",ForeverEquipmentEligibility.Assess(item,"shaman").RestrictionStatus);

        item.ClassRestrictionEvidence!.SourceSha256="unreviewed";
        Assert.Equal("pending",ForeverEquipmentEligibility.Assess(item,"druid").RestrictionStatus);
        item=SyntheticEquipmentRestrictionFixture.Item(weapon:true);
        var weapon=ForeverEquipmentEligibility.Assess(item,"druid");
        Assert.Equal("proven",weapon.RestrictionStatus);Assert.Equal("pending",weapon.ProficiencyStatus);
        Assert.False(weapon.CanExecute);
        item=SyntheticEquipmentRestrictionFixture.Item();
        item.ItemSubclass="unclassified";
        Assert.Equal("pending",ForeverEquipmentEligibility.Assess(item,"druid").ProficiencyStatus);

        // Raw restriction permits Mage and Warrior; armor proficiency independently excludes Mage.
        item=SyntheticEquipmentRestrictionFixture.Item();item.ItemSubclass="plate";
        item.AllowedClasses=["warrior"];
        item.ClassRestrictionEvidence=SyntheticEquipmentRestrictionFixture.Evidence(item.Id,129L,["warrior","mage"]);
        var armor=ForeverEquipmentEligibility.Assess(item,"mage");
        Assert.Equal("proven",armor.RestrictionStatus);Assert.Equal("denied",armor.ProficiencyStatus);
        Assert.False(armor.CanExecute);
    }

    [Fact]
    public void TalismanMetadataMatchesProvisionalExecutableAuraAndReversibleSelection()
    {
        var item=ForeverEquipmentCatalog.FindItem(18820);
        var effect=Assert.Single(ForeverEquipmentEffectsCatalog.GetCatalog().Effects,x=>x.ItemId==18820);
        Assert.Equal("fully-implemented",item.ImplementationStatus);
        Assert.Equal("provisional",item.DataConfidence);
        Assert.Equal("modeled",Assert.Single(item.Effects).ExecutionStatus);
        Assert.True(effect.Modeled);Assert.True(effect.Provisional);
        var eligibility=ForeverEquipmentEligibility.Assess(item,"shaman");
        Assert.Equal("supported",eligibility.EffectStatus);
        Assert.Equal("pending",eligibility.RestrictionStatus);Assert.False(eligibility.CanExecute);

        var request=MageSimRequest.CreatePreset("fire");
        request.DurationSeconds=110m;request.TargetMaximumHealth=10000000m;
        request.BaseSpellHitPercent=100m;request.SpellCritPercent=0m;request.CaptureTimeline=true;
        var naked=new MageSimRunner().Run(request);
        request.Character.Equipment.Items.Add(new(){ItemId=18820,SlotKey="trinket1"});
        var original=JsonSerializer.Serialize(request);
        var equipped=new MageSimRunner().Run(request);
        Assert.Equal(original,JsonSerializer.Serialize(request));
        Assert.True(equipped.DamageDone>naked.DamageDone);
        Assert.Equal(new[]{0m,90m},equipped.Run.Timeline.Where(x=>x.Type==CombatEventType.AuraApplied &&
            x.EffectKey=="ephemeral-power@trinket1").Select(x=>x.TimeSeconds));
        Assert.Equal(new[]{15m,105m},equipped.Run.Timeline.Where(x=>x.Type==CombatEventType.AuraRemoved &&
            x.EffectKey=="ephemeral-power@trinket1").Select(x=>x.TimeSeconds));
        Assert.Equal(0m,ForeverSharedEquipmentResolver.Resolve(request.Character.Equipment,"mage").RawStats.Get("spell-power"));
        request.Character.Equipment.Items.Clear();
        var removed=new MageSimRunner().Run(request);
        Assert.Equal(naked.DamageDone,removed.DamageDone);
        Assert.Equal(CanonicalRun(naked.Run),CanonicalRun(removed.Run));
    }

    private static string CanonicalRun(SimulationRunResult run)
    {
        var node=JsonSerializer.SerializeToNode(run)!;
        var ids=new Dictionary<string,string>();
        void Visit(JsonNode? value)
        {
            if(value is JsonObject obj)
                foreach(var pair in obj.ToArray())
                {
                    if(pair.Key is "AbilityExecutionId" or "AutoAttackInstanceId" or "AuraInstanceId" or
                        "AbsorbInstanceId" or "ForcedTargetInstanceId" && pair.Value is JsonValue primitive &&
                       primitive.TryGetValue<string>(out var id) && Guid.TryParse(id,out var guid) && guid!=Guid.Empty)
                    {
                        if(!ids.TryGetValue(id,out var stable))ids[id]=stable="generated-"+ids.Count;
                        obj[pair.Key]=stable;
                    }
                    else Visit(pair.Value);
                }
            else if(value is JsonArray array)foreach(var child in array)Visit(child);
        }
        Visit(node);return node.ToJsonString();
    }
}
