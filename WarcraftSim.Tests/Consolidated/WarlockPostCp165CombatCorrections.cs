using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests.Consolidated;

public sealed class WarlockPostCp165CombatCorrectionsTests
{
    private static WarlockSimRequest Request(string demon="succubus",string spec="demonology")
    {
        var request=WarlockSimRequest.CreatePreset(spec);
        request.PetType=demon;
        request.PrePullSacrificedDemon=null; // isolate physical/mechanical changes
        request.Seed=131003;
        request.DurationSeconds=120m;
        request.CaptureTimeline=true;
        return request;
    }

    [Theory]
    [InlineData("imp","destruction")]
    [InlineData("imp","demonology")]
    [InlineData("succubus","affliction")]
    public void PassiveManaEventsReflectActualRegenerationAndConserveResource(
        string demon,string spec)
    {
        var request=Request(demon,spec);
        var result=new WarlockSimRunner().Run(request);
        var actor=result.Run.Summary.ActorSummaries["warlock-pet"];
        var manaEvents=result.Run.Timeline.Where(e=>
            e.SourceActorKey=="warlock-pet" &&
            e.Type==CombatEventType.ResourceChanged &&
            e.ResourceKey=="mana").ToArray();
        var gains=manaEvents.Where(e=>e.AbilityKey=="demon-mana-regeneration").ToArray();
        Assert.NotEmpty(gains);
        Assert.All(gains,e=>
        {
            Assert.True(e.Amount is >0m,"Every emitted gain must be positive.");
            Assert.InRange(e.Amount!.Value,0m,request.DemonTuning.ManaRegenPerSecond+.0001m);
        });
        // The last fractional second of passive regeneration is applied by
        // SimulationContext at termination, after the final report sampler.
        var accounted=actor.StartingResources["mana"]+
            manaEvents.Sum(e=>e.Amount??0m);
        Assert.InRange(actor.EndingResources["mana"]-accounted,0m,
            request.DemonTuning.ManaRegenPerSecond+.0001m);
    }

    [Fact]
    public void DemonMeleeArmorUsesTheSharedRationalMitigationFormula()
    {
        var unarmored=Request();
        var armored=Request();
        armored.TargetArmor=4000m;
        var noArmor=new WarlockSimRunner().Run(unarmored);
        var withArmor=new WarlockSimRunner().Run(armored);
        Assert.True(withArmor.DemonMeleeDamage<noArmor.DemonMeleeDamage);
        Assert.Equal(noArmor.WarlockDamage,withArmor.WarlockDamage);
        Assert.Equal(noArmor.DemonSpellDamage,withArmor.DemonSpellDamage);
        var hits=withArmor.Run.Timeline.Where(e=>
            e.Type==CombatEventType.Damage && e.SourceActorKey=="warlock-pet" &&
            e.AbilityKey=="pet-melee" && e.Amount>0m).ToArray();
        Assert.NotEmpty(hits);
        var reduction=4000m/(4000m+400m+85m*60m);
        Assert.All(hits,e=>
        {
            // The common Damage event exposes mitigation percent and raw/final
            // amount; it does not currently populate MitigationType metadata.
            Assert.InRange(Math.Abs((e.Amount??0m)-
                (e.RawAmount??0m)*(1m-reduction)),0m,.001m);
            Assert.InRange(Math.Abs((e.MitigationPercent??0m)-reduction*100m),
                0m,.001m);
        });
        Assert.InRange(Math.Abs(withArmor.DamageDone-
            withArmor.WarlockDamage-withArmor.PetDamage),0m,.001m);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DemonMeleeUsesSharedDodgeAndParryTable(bool dodge)
    {
        var request=Request();
        request.PetMeleeHitPercent=100m;
        request.TargetDodgePercent=dodge?100m:0m;
        request.TargetParryPercent=dodge?0m:100m;
        var result=new WarlockSimRunner().Run(request);
        var expected=dodge?"dodge":"parry";
        Assert.Equal(0m,result.DemonMeleeDamage);
        Assert.True(result.DemonSpellDamage>0m);
        Assert.Contains(result.Run.Timeline,e=>
            e.SourceActorKey=="warlock-pet"&&e.AbilityKey=="pet-melee"&&
            e.Type==CombatEventType.Damage&&e.ResultKey==expected&&e.Amount==0m);
    }

    [Fact]
    public void PhysicalTargetDefensesCannotChangeImpFirebolt()
    {
        var baseReq=Request("imp","destruction");
        var defended=Request("imp","destruction");
        defended.TargetArmor=10000m;
        defended.TargetDodgePercent=50m;
        defended.TargetParryPercent=40m;
        var first=new WarlockSimRunner().Run(baseReq);
        var second=new WarlockSimRunner().Run(defended);
        Assert.Equal(0m,first.DemonMeleeDamage);
        Assert.Equal(0m,second.DemonMeleeDamage);
        Assert.Equal(first.DemonSpellDamage,second.DemonSpellDamage);
        Assert.Equal(first.WarlockDamage,second.WarlockDamage);
    }

    [Fact]
    public void WarlockPhysicalConfigRejectsInvalidBossDefenseValues()
    {
        var request=Request();
        request.TargetArmor=-1m;
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(request));
        request.TargetArmor=0m;request.TargetDodgePercent=101m;
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(request));
        request.TargetDodgePercent=0m;request.TargetParryPercent=-1m;
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(request));
    }

    [Fact]
    public void HunterCompanionStillProducesDamageWithoutWarlockRuleChanges()
    {
        var run=new BeastMasteryHunterSimRunner().Run(new BeastMasteryHunterSimRequest{
            PetFamily="bear",AdditionalTargets=2,DurationSeconds=35m,Seed=163001});
        Assert.True(run.PetAutoDamage>0m);
        Assert.True(run.PetSpecialDamage>0m);
        Assert.InRange(Math.Abs(run.DamageDone-run.HunterDamage-run.PetDamage-run.HawkDamage),
            0m,.001m);
    }
}
