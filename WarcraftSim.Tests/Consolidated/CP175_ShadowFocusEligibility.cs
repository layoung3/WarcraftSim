using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Priest;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Pinned historical priest/talents.go: Focus 1 point/rank; IF crit mask is
/// distinct from its free-cast mask. Legacy aggregate builds below are not claims
/// that missing strict itemized Discipline fillers are available.</summary>
public sealed class Cp175ShadowFocusEligibilityTests
{
    private static readonly string[] Spells=["mind-blast","shadow-word-death","shadow-word-pain","devouring-plague","mind-flay"];
    private static ShadowPriestSimRequest Request(string spell="mind-blast",bool inner=false,int focus=0,int early=0)
    {
        var r=new ShadowPriestSimRequest {
            DurationSeconds=3.1m,CaptureTimeline=true,Seed=175140,
            BaseSpellHitPercent=100m,SpellPowerOverride=0m,SpellCritOverridePercent=0m,
            TargetMaximumHealth=1000000000m,TargetInitialHealthPercent=spell=="shadow-word-death"?20m:100m,
            UseInnerFocus=inner,UsePowerInfusion=false,UseVampiricEmbrace=false,
            UseMindBlast=spell=="mind-blast",UseShadowWordDeathExecute=spell=="shadow-word-death",
            UseShadowWordPain=spell=="shadow-word-pain",UseDevouringPlague=spell=="devouring-plague",UseMindFlay=spell=="mind-flay",
            Talents=new(){DisciplinePointsSpent=11,ShadowPointsSpent=40,Ranks=new(){["inner-focus"]=1,["shadow-focus"]=focus,["early-demise"]=early,["mind-flay"]=1}},
            StatAssumptions=new(){SpiritManaRegenerationPerSecondPerPoint=0m},
            Tuning=new(){MindBlastDamage=100m,ShadowWordDeathDamage=100m,ShadowWordPainTick=100m,DevouringPlagueTick=100m,MindFlayTick=100m}
        };
        r.Character.BaseStats.Set("mp5",0m);
        return r;
    }
    private static CombatEvent[] Damage(ShadowPriestSimResponse r,string spell)=>r.Run.Timeline
        .Where(e=>e.Type==CombatEventType.Damage&&e.AbilityKey==spell&&e.TargetActorKey=="target").ToArray();
    private static int SeedFor(decimal low,decimal high,int draw=0)=>Enumerable.Range(1,100000).First(seed=>{
        var random=new Random(seed);double value=0;for(var i=0;i<=draw;i++)value=random.NextDouble();
        return (decimal)value*100m>=low&&(decimal)value*100m<high;
    });
    public static IEnumerable<object[]> FocusCases()
    {
        for(var rank=0;rank<=5;rank++){
            yield return [rank,83m,0m,83m+rank];yield return [rank,83m,2m,85m+rank];
            yield return [rank,98m,1m,Math.Min(100m,99m+rank)];yield return [rank,0m,0m,(decimal)rank];
        }
    }
    [Theory][MemberData(nameof(FocusCases))]
    public void LiteralFocusCurveGearCapAndBothPreviewControllers(int rank,decimal baseHit,decimal gear,decimal expected)
    {
        var r=Request(focus:rank);r.BaseSpellHitPercent=baseHit;
        r.Character.Equipment.Items=[new(){SlotKey="head",ItemKey="authored-hit-budget",Stats=new(){Values=new(){["spell-hit-percent"]=gear}}}];
        var before=JsonSerializer.Serialize(r);var p=ShadowPriestSimRunner.Preview(r);
        Assert.Equal(expected,p.EffectiveStats.Get("spell-hit-chance-percent"));
        var direct=Assert.IsType<ShadowPrecombatPreview>(Assert.IsType<OkObjectResult>(new ForeverCharacterController().PreviewShadow(r)).Value);
        Assert.Equal(expected,direct.EffectiveStats.Get("spell-hit-chance-percent"));
        var rotation=Assert.IsType<OkObjectResult>(new ForeverSoloRotationController().Preview(new(){ClassKey="priest",Specialization="shadow",Shadow=r}));
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(rotation.Value));
        Assert.Equal(p.SpellPower,doc.RootElement.GetProperty("SpellPower").GetDecimal());
        Assert.Equal(before,JsonSerializer.Serialize(r));
    }
    public static IEnumerable<object[]> LandingCases()
    {
        foreach(var spell in Spells)for(var rank=1;rank<=5;rank++)yield return [spell,rank];
    }
    [Theory][MemberData(nameof(LandingCases))]
    public void ActualFirstLandingMissesBetweenCorrectAndOldFocusThreshold(string spell,int rank)
    {
        var r=Request(spell,focus:rank);r.BaseSpellHitPercent=83m;
        r.Seed=SeedFor(83m+rank,83m+2m*rank);
        var output=new ShadowPriestSimRunner().Run(r);
        Assert.Equal(CombatResultTypes.Miss,Damage(output,spell).First().ResultKey);
        Assert.Equal(83m+rank,ShadowPriestSimRunner.Preview(r).EffectiveStats.Get("spell-hit-chance-percent"));
    }
    [Theory][InlineData(0,0)][InlineData(0,100)][InlineData(5,100)]
    public void GuaranteedHitAndMissBoundariesRemainLiteral(int rank,int baseHit)
    {
        var r=Request(focus:rank);r.BaseSpellHitPercent=baseHit;var hit=Assert.Single(Damage(new ShadowPriestSimRunner().Run(r),"mind-blast"));
        Assert.Equal(baseHit==0?CombatResultTypes.Miss:CombatResultTypes.Hit,hit.ResultKey);
    }
    public static IEnumerable<object[]> CriticalCases()
    {
        foreach(var active in new[]{false,true})foreach(var below in new[]{false,true}){
            yield return ["mind-blast",0,100m,active,below,active?25m:0m,CombatEffectDeliveryType.Direct];
            for(var rank=0;rank<=2;rank++)foreach(var health in new[]{19.99m,20m,20.01m})
                yield return ["shadow-word-death",rank,health,active,below,health<=20m?rank*15m:0m,CombatEffectDeliveryType.Direct];
            yield return ["mind-flay",0,100m,active,below,0m,CombatEffectDeliveryType.ChannelTick];
            yield return ["shadow-word-pain",0,100m,active,below,0m,CombatEffectDeliveryType.Periodic];
            yield return ["devouring-plague",0,100m,active,below,0m,CombatEffectDeliveryType.Periodic];
        }
    }
    [Theory][MemberData(nameof(CriticalCases))]
    public void LiteralCriticalMaskAtEitherSideOfExactChance(string spell,int rank,decimal health,bool active,bool below,decimal expectedBonus,CombatEffectDeliveryType delivery)
    {
        var r=Request(spell,active,early:rank);r.TargetInitialHealthPercent=health;r.Seed=SeedFor(70m,80m);
        var (context,actor,target)=Prepare(r);var major=new PriestMajorCooldownProcessor(actor,new AuraManager());
        if(active)Reserve(major,context,spell,Guid.NewGuid());
        var ability=actor.Abilities[spell].Definition;var effect=Assert.Single(ability.Effects);effect.CanMiss=false;
        var sample=(decimal)new Random(r.Seed).NextDouble()*100m;
        var baseCrit=sample-expectedBonus+(below?.001m:-.001m);
        var result=Resolver(100m,baseCrit,major).Resolve(context,actor,target,ability,effect,delivery);
        Assert.Equal(below?CombatResultTypes.Critical:CombatResultTypes.Hit,result.ResultKey);
        Assert.Equal(below?1.5m:1m,result.AmountMultiplier);
    }
    [Theory][InlineData("mind-blast")][InlineData("shadow-word-death")][InlineData("shadow-word-pain")][InlineData("devouring-plague")][InlineData("mind-flay")]
    public void ActualFreecastConsumptionRemainsBroaderThanCriticalMask(string spell)
    {
        var r=Request(spell,true);var before=JsonSerializer.Serialize(r);var output=new ShadowPriestSimRunner().Run(r);
        var debits=output.Run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged&&e.AbilityKey==spell&&e.ResourceKey=="mana").ToArray();
        Assert.NotEmpty(debits);Assert.Equal(0m,debits[0].Amount);
        Assert.Single(output.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied&&e.AbilityKey=="inner-focus");
        Assert.Single(output.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved&&e.AbilityKey=="inner-focus");
        Assert.Equal(output.MaximumMana+debits.Sum(e=>e.Amount!.Value),output.EndingMana);
        Assert.Equal(before,JsonSerializer.Serialize(r));
        r.UseInnerFocus=false;var ordinary=new ShadowPriestSimRunner().Run(r);
        Assert.True(ordinary.Run.Timeline.First(e=>e.Type==CombatEventType.ResourceChanged&&e.AbilityKey==spell).Amount<0m);
        Assert.DoesNotContain(ordinary.Run.Timeline,e=>e.AbilityKey=="inner-focus");
    }
    [Theory][InlineData("mind-blast",0)][InlineData("shadow-word-death",0)][InlineData("shadow-word-death",1)][InlineData("shadow-word-death",2)]
    public void ActualReservedDirectSpellUsesIndependentSeededCriticalWitness(string spell,int rank)
    {
        var expected=spell=="mind-blast"?25m:15m*rank;
        var r=Request(spell,true,early:rank);r.Seed=SeedFor(expected,expected+24m,1);
        var output=new ShadowPriestSimRunner().Run(r);
        Assert.Equal(CombatResultTypes.Hit,Assert.Single(Damage(output,spell)).ResultKey);
        if(spell=="mind-blast"){
            r.Seed=SeedFor(0m,25m,1);Assert.Equal(CombatResultTypes.Critical,Assert.Single(Damage(new ShadowPriestSimRunner().Run(r),spell)).ResultKey);
        }
    }
    [Fact]
    public void CancelledReservationKeepsFreecastButOnlyBlastCanReceiveCriticalBonus()
    {
        var r=Request("mind-blast",true);var (context,actor,_)=Prepare(r);var major=new PriestMajorCooldownProcessor(actor,new AuraManager());
        var blast=actor.Abilities["mind-blast"].Definition;var death=actor.Abilities["shadow-word-death"].Definition;
        var original=blast.ResourceCosts.Single().Amount;var first=Guid.NewGuid();Reserve(major,context,"mind-blast",first);
        Assert.True(major.IsInnerFocusEmpowering(blast));Assert.Equal(0m,blast.ResourceCosts.Single().Amount);
        major.Process(context,new(){Type=CombatEventType.AbilityCastCancelled,SourceActorKey="priest",AbilityExecutionId=first,AbilityKey="mind-blast"});
        Assert.False(major.IsInnerFocusEmpowering(blast));Assert.Equal(0m,death.ResourceCosts.Single().Amount);Assert.Equal(0,major.InnerFocusConsumed);
        var second=Guid.NewGuid();major.Process(context,new(){Type=CombatEventType.AbilityCastStarted,SourceActorKey="priest",AbilityExecutionId=second,AbilityKey="shadow-word-death"});
        Assert.True(major.IsInnerFocusEmpowering(death)); // Generic reservation is intentionally broader than Shadow crit mask.
        major.Process(context,new(){Type=CombatEventType.AbilityCastCompleted,SourceActorKey="priest",AbilityExecutionId=second,AbilityKey="shadow-word-death"});
        Assert.Equal(1,major.InnerFocusConsumed);Assert.False(major.IsInnerFocusEmpowering(death));Assert.Equal(original,blast.ResourceCosts.Single().Amount);
    }
    [Fact]
    public void UnsupportedOrMissingInnerFocusNeverAppearsInFixedRotation()
    {
        var r=Request(inner:true);r.Talents.Ranks.Remove("inner-focus");
        Assert.DoesNotContain(ShadowPriestSimRunner.Preview(r).Rotation.Entries,e=>e.AbilityKey=="inner-focus");
        Assert.DoesNotContain(new ShadowPriestSimRunner().Run(r).Run.Timeline,e=>e.AbilityKey=="inner-focus");
    }
    private static (SimulationContext Context,SimulationActorState Actor,SimulationActorState Target) Prepare(ShadowPriestSimRequest r)
    {
        var p=typeof(ShadowPriestSimRunner).GetMethod("Prepare",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[r,default(CancellationToken)])!;
        return ((SimulationContext)p.GetType().GetProperty("Context")!.GetValue(p)!,
            (SimulationActorState)p.GetType().GetProperty("Actor")!.GetValue(p)!,
            (SimulationActorState)p.GetType().GetProperty("Target")!.GetValue(p)!);
    }
    private static ICombatRollResolver Resolver(decimal hit,decimal crit,PriestMajorCooldownProcessor major)=>(ICombatRollResolver)Activator.CreateInstance(
        typeof(ShadowPriestSimRunner).GetNestedType("ShadowPriestRollResolver",BindingFlags.NonPublic)!,
        BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[hit,crit,1.5m,major],null)!;
    private static void Reserve(PriestMajorCooldownProcessor major,SimulationContext c,string spell,Guid id)
    {
        major.Process(c,new(){Type=CombatEventType.AuraApplied,SourceActorKey="priest",AbilityKey="inner-focus"});
        major.Process(c,new(){Type=CombatEventType.AbilityCastStarted,SourceActorKey="priest",AbilityKey=spell,AbilityExecutionId=id});
    }
}
