using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175PreparedShadowInputs
{
    private static ShadowPriestSimRequest Input() => new() { DurationSeconds=20m, CaptureTimeline=true,
        BaseSpellHitPercent=100m, SpellCritOverridePercent=0m };
    private static ForeverLevel60BaselineObservation Baseline(decimal intellect=100m) => new() {
        Race="human", ClassKey="priest", Strength=40m, Agility=40m, Stamina=100m,
        Intellect=intellect, Spirit=100m, ClientBuild="controlled-test-build",
        SourceReference="synthetic-test-observation", RecordedAtUtc="2026-11-09T00:00:00Z"
    };
    private static ShadowPriestSimRequest Prepared(decimal intellect=100m)
    {
        var r=Input(); r.Character.Race="human"; r.Level60Baseline=Baseline(intellect); return r;
    }
    private static void EqualRun(ShadowPriestSimResponse a, ShadowPriestSimResponse b)
    {
        Assert.Equal(a.DamageDone,b.DamageDone); Assert.Equal(a.EndingMana,b.EndingMana);
        Assert.Equal(a.MaximumMana,b.MaximumMana); Assert.Equal(a.HealingDone,b.HealingDone);
        Assert.Equal(a.Run.Timeline.Select(e=>(e.TimeSeconds,e.Type,e.AbilityKey,e.Amount,e.ResultKey)),
            b.Run.Timeline.Select(e=>(e.TimeSeconds,e.Type,e.AbilityKey,e.Amount,e.ResultKey)));
    }
    [Theory][InlineData(false,2846,3650)][InlineData(true,4250,3150)]
    public void ResourceContributionsApplyOnceWithObservedOrManualFallback(bool observed,int mana,int health)
    {
        var r=Prepared();
        if(observed) { r.Level60Baseline!.ObservedUnbuffedMaximumMana=4000m; r.Level60Baseline.ObservedUnbuffedMaximumHealth=3000m; }
        r.Character.Equipment.Items.Add(new() { SlotKey="chest",ItemKey="authored-budget",
            Stats=new() { Values=new() { ["intellect"]=10m,["mana"]=100m,["stamina"]=10m,["health"]=50m,["spell-power"]=40m } } });
        var before=JsonSerializer.Serialize(r); var result=new ShadowPriestSimRunner().Run(r);
        // Fallback:1376+20+90*15+100=2846. Observed:4000+10*15+100=4250.
        Assert.Equal(mana,result.MaximumMana); Assert.Equal(health,result.MaximumHealth);
        Assert.Equal(mana,result.Run.Summary.ActorSummaries["priest"].StartingResources["mana"]);
        Assert.Equal(40m,result.SpellPower); Assert.Equal(before,JsonSerializer.Serialize(r));
        Assert.Contains(result.ModelingNotes,n=>n.Contains(observed?"Supplied naked Mana":"not observed naked Mana"));
        Assert.Contains(result.ModelingNotes,n=>n.Contains(observed?"Supplied naked Health":"manual scenario cap"));
        Assert.Contains(result.ModelingNotes,n=>n.Contains("not server attestation"));
        Assert.Contains(result.ModelingNotes,n=>n.Contains("synthetic-test-observation"));
    }
    [Theory][InlineData(10,15,2055)][InlineData(10,5,2005)][InlineData(30,-15,1905)]
    public void ObservedManaUsesPriestFirstTwentyIntellectAndConfiguredConversion(int naked,int delta,int expected)
    {
        var r=Prepared(naked); r.StatAssumptions.ManaPerIntellectAboveTwenty=9m;
        r.Level60Baseline!.ObservedUnbuffedMaximumMana=2000m;
        r.Character.BaseStats.Set("mana",9999m); // Baseline replaces legacy aggregate base stats.
        r.Character.Equipment.Items.Add(new() { SlotKey="chest",ItemKey="authored",
            Stats=new() { Values=new() { ["intellect"]=delta } } });
        var result=new ShadowPriestSimRunner().Run(r);
        Assert.Equal(expected,result.MaximumMana);
    }
    [Fact]
    public void SnapshotIsolatesAllNestedInputsAndPreservesComparers()
    {
        var r=Prepared(); r.Character.BaseStats.Values=new(StringComparer.Ordinal){["intellect"]=190m};
        r.Talents.Ranks=new(r.Talents.Ranks,StringComparer.Ordinal);
        r.Character.Equipment.Items.Add(new() { SlotKey="chest",ItemKey="authored",
            Stats=new() { Values=new(StringComparer.OrdinalIgnoreCase){["STAMINA"]=4m} } });
        var copy=ShadowPriestSimulationRequestSnapshot.Create(r);
        Assert.Same(r.Talents.Ranks.Comparer,copy.Talents.Ranks.Comparer);
        Assert.Same(r.Character.BaseStats.Values.Comparer,copy.Character.BaseStats.Values.Comparer);
        Assert.Same(r.Character.Equipment.Items[0].Stats.Values.Comparer,copy.Character.Equipment.Items[0].Stats.Values.Comparer);
        copy.Talents.Ranks.Clear(); copy.Tuning.MindBlastDamage+=100m; copy.StatAssumptions.BaseMana=1m;
        copy.Level60Baseline!.Intellect=999m; copy.Character.Equipment.Items[0].Stats.Set("stamina",999m);
        Assert.NotEmpty(r.Talents.Ranks); Assert.NotEqual(copy.Tuning.MindBlastDamage,r.Tuning.MindBlastDamage);
        Assert.Equal(1376m,r.StatAssumptions.BaseMana); Assert.Equal(100m,r.Level60Baseline!.Intellect);
        Assert.Equal(4m,r.Character.Equipment.Items[0].Stats.Get("stamina"));
    }
    [Fact]
    public async Task LegacyAndPreparedRunsRepeatWithoutMutatingCaller()
    {
        foreach(var r in new[]{Input(),Prepared()})
        {
            var before=JsonSerializer.Serialize(r);
            var results=await Task.WhenAll(Enumerable.Range(0,3).Select(_=>Task.Run(()=>new ShadowPriestSimRunner().Run(r))));
            EqualRun(results[0],results[1]); EqualRun(results[1],results[2]);
            Assert.Equal(before,JsonSerializer.Serialize(r));
        }
        Assert.Equal(3946m,new ShadowPriestSimRunner().Run(Input()).MaximumMana);
    }
    [Fact]
    public void PreparedMatchesEquivalentAuthoredPriestAndPreservesInitialTargetHealth()
    {
        var r=Prepared(); r.TargetInitialHealthPercent=19m;
        var authored=Input(); authored.TargetInitialHealthPercent=19m;
        authored.Character=ForeverLevel60Baseline.Generate(r.Character,r.Level60Baseline!);
        var result=new ShadowPriestSimRunner().Run(r);
        EqualRun(new ShadowPriestSimRunner().Run(authored),result);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage && e.AbilityKey=="shadow-word-death");
        Assert.True(result.BossEndingHealthPercent<19m);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void PreparedCatalogSelectionsFailClosed(int kind)
    {
        var r=Prepared(); r.Character.Equipment.Items.Add(new() { SlotKey="main-hand",ItemKey="unsupported",
            ItemId=kind==0?19102:null,EnchantSpellId=kind==1?20034:null,TemporaryEnchantSpellId=kind==2?25122:null });
        Assert.Throws<ArgumentException>(()=>new ShadowPriestSimRunner().Run(r));
    }
    [Fact]
    public void BaselineModesAndIdentityAreValidatedAndProxiesRemainExplicit()
    {
        var r=Prepared(); r.Character.Race="dwarf";
        Assert.Throws<ArgumentException>(()=>new ShadowPriestSimRunner().Run(r));
        r=Prepared(); r.Level60Baseline!.ClassKey="mage";
        Assert.Throws<ArgumentException>(()=>new ShadowPriestSimRunner().Run(r));
        r=Prepared(); r.Level60Baseline!.Level=59;
        Assert.Throws<ArgumentException>(()=>new ShadowPriestSimRunner().Run(r));
        r=Prepared(); r.UseReviewedLevel60Baseline=true;
        Assert.Throws<ArgumentException>(()=>new ShadowPriestSimRunner().Run(r));
        r.Level60Baseline=null; r.UseProvisionalLevel60Baseline=true;
        Assert.Throws<ArgumentException>(()=>new ShadowPriestSimRunner().Run(r));
        r.UseReviewedLevel60Baseline=false;
        var result=new ShadowPriestSimRunner().Run(r);
        Assert.Contains(result.ModelingNotes,n=>n.Contains("provisional-documented-proxy") && n.Contains("NOT verified for Forever"));
        r=Prepared(); r.Level60Baseline!.VerificationStatus="provisional-documented-proxy";
        Assert.Throws<ArgumentException>(()=>new ShadowPriestSimRunner().Run(r));
    }
    [Theory][InlineData("ranks")][InlineData("base")][InlineData("map")][InlineData("equipment")]
    [InlineData("items")][InlineData("item")][InlineData("modifiers")][InlineData("modifier")]
    [InlineData("talents")][InlineData("talent")][InlineData("buffs")][InlineData("buff")]
    public async Task MalformedNestedInputsReturnHttp400WithoutMutation(string kind)
    {
        var r=Prepared();
        switch(kind)
        {
            case "ranks": r.Talents.Ranks=null!; break;
            case "base": r.Character.BaseStats=null!; break;
            case "map": r.Character.BaseStats.Values=null!; break;
            case "equipment": r.Character.Equipment=null!; break;
            case "items": r.Character.Equipment.Items=null!; break;
            case "item": r.Character.Equipment.Items.Add(null!); break;
            case "modifiers": r.Character.Equipment.Items.Add(new(){Modifiers=null!}); break;
            case "modifier": r.Character.Equipment.Items.Add(new(){Modifiers=[null!]}); break;
            case "talents": r.Character.Talents.Selections=null!; break;
            case "talent": r.Character.Talents.Selections.Add(null!); break;
            case "buffs": r.Character.Buffs.Selections=null!; break;
            case "buff": r.Character.Buffs.Selections.Add(null!); break;
        }
        var json=JsonSerializer.Serialize(r); var request=JsonSerializer.Deserialize<ShadowPriestSimRequest>(json)!;
        var response=await new ShadowPriestSimController().Run(request,default);
        Assert.IsType<BadRequestObjectResult>(response.Result); Assert.Equal(json,JsonSerializer.Serialize(request));
    }
    [Fact]
    public void SeparateAuthoredSourcesContributePrimaryAndFlatManaExactlyOnce()
    {
        var r=Prepared(); r.Level60Baseline!.ObservedUnbuffedMaximumMana=2000m;
        r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="authored",Stats=new(){Values=new(){["intellect"]=2m,["mana"]=3m}}});
        r.Character.Talents.Selections.Add(new(){TalentKey="authored-budget",Stats=new(){Values=new(){["intellect"]=4m,["mana"]=5m}}});
        r.Character.Buffs.Selections.Add(new(){BuffKey="authored-budget",Stats=new(){Values=new(){["intellect"]=6m,["mana"]=7m}}});
        var result=new ShadowPriestSimRunner().Run(r);
        Assert.Equal(2195m,result.MaximumMana); // 2000+(2+4+6)*15+(3+5+7).
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void NonpositivePreparedResourcesAreRejected(bool mana)
    {
        var r=Prepared();
        if(mana)r.Level60Baseline!.ObservedUnbuffedMaximumMana=0m;
        else r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="authored",Stats=new(){Values=new(){["health"]=-3500m}}});
        Assert.Throws<ArgumentException>(()=>new ShadowPriestSimRunner().Run(r));
    }    [Fact]
    public void ObservedManaValidatesFinalCapInsteadOfUnusedFallback()
    {
        var r=Prepared(10m); r.Level60Baseline!.ObservedUnbuffedMaximumMana=4000m;
        r.StatAssumptions.BaseMana=0m;
        r.Character.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="authored",Stats=new(){Values=new(){["mana"]=-100m}}});
        Assert.Equal(3900m,new ShadowPriestSimRunner().Run(r).MaximumMana);
        r.Level60Baseline.ObservedUnbuffedMaximumMana=null;
        Assert.Throws<ArgumentException>(()=>new ShadowPriestSimRunner().Run(r));
    }
    [Theory][InlineData(1000,null)][InlineData(null,10)][InlineData(0,10)]
    [InlineData(-1,10)][InlineData(1000,-1)]
    public void PriestCalculatorValidatesObservedInputPair(int? mana,int? intellect)
    {
        Assert.Throws<ArgumentException>(()=>WarcraftSim.Data.Forever.Priest.ForeverPriestCharacterCalculator.Calculate(
            Input().Character,observedNakedMaximumMana:mana,nakedIntellect:intellect));
    }
    [Fact]
    public void PriestCalculatorPreservesLegacyHealingAndTalentConversions()
    {
        var r=Input(); r.Character.BaseStats.Set("mana",100m); r.Character.BaseStats.Set("healing-power",50m);
        var defaultStats=WarcraftSim.Data.Forever.Priest.ForeverPriestCharacterCalculator.Calculate(r.Character);
        Assert.Equal(4046m,defaultStats.MaximumMana); Assert.Equal(50m,defaultStats.HealingPower);
        var talented=WarcraftSim.Data.Forever.Priest.ForeverPriestCharacterCalculator.Calculate(r.Character,
            mentalStrengthRanks:5,spiritualGuidanceRanks:5);
        Assert.Equal(209m,talented.EffectiveStats["intellect"]);
        Assert.Equal(4331m,talented.MaximumMana); //1376+20+(209-20)*15+100.
        Assert.Equal(102.5m,talented.HealingPower); //50+210*.25.
        Assert.Equal(15.75m,talented.SpiritRegenerationPerSecond);
    }}
