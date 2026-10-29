using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BaMageImprovedChanneling
{
    private static MageSimRequest Arcane(int rank,string action)
    {
        var r=MageSimRequest.CreatePreset("arcane");
        r.Talents.Ranks["improved-channeling"]=rank;
        r.Talents.Ranks["arcane-concentration"]=0;
        r.Talents.Ranks["arcane-meditation"]=0;
        r.UseMissileBarrage=false;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.CaptureTimeline=true;
        r.DurationSeconds=5.5m;
        r.IncomingDamageAtSeconds=action=="arcane-missiles"?1.75m:.5m;
        r.IncomingDamageAmount=50m;
        r.CustomPriority=[action];
        return r;
    }

    [Fact]
    public void ImprovedChannelingRankFivePreventsRealArcaneMissilesChannelCancellation()
    {
        var baseline=new MageSimRunner().Run(Arcane(0,"arcane-missiles"));
        var protectedRun=new MageSimRunner().Run(Arcane(5,"arcane-missiles"));
        Assert.Equal(1m,baseline.Run.CustomMetrics["mage-arcane-channel-cancellations"]);
        Assert.Equal(0m,protectedRun.Run.CustomMetrics["mage-arcane-channel-cancellations"]);
        Assert.Equal(1m,protectedRun.Run.CustomMetrics["mage-improved-channeling-resisted"]);
        Assert.Contains(protectedRun.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityChannelCompleted &&
            e.AbilityKey=="arcane-missiles");
        Assert.Contains(baseline.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityChannelCancelled &&
            e.AbilityKey=="arcane-missiles");
        Assert.True(protectedRun.DamageDone>baseline.DamageDone);
    }

    [Fact]
    public void ImprovedChannelingProtectsArcaneBlastHardcastByRankWithDeterministicSeeds()
    {
        var normal=new MageSimRunner().Run(Arcane(0,"arcane-blast"));
        Assert.True(normal.Run.CustomMetrics["mage-arcane-cast-pushback-seconds"]>0m);
        int prevented=0;
        for(var seed=1;seed<=30;seed++)
        {
            var request=Arcane(5,"arcane-blast");
            request.Seed=seed;
            var protectedRun=new MageSimRunner().Run(request);
            if(protectedRun.Run.CustomMetrics["mage-improved-channeling-resisted"]>0m)
            {
                prevented++;
                Assert.Equal(0m,protectedRun.Run.CustomMetrics["mage-arcane-cast-pushback-seconds"]);
            }
        }
        Assert.True(prevented>0);
    }

    [Fact]
    public void ImprovedChannelingRankSixIsInvalid()
    {
        var r=Arcane(6,"arcane-missiles");
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(r));
    }

    [Fact]
    public void ArcanePushbackProtectionNeverPreventsRealHealthLoss()
    {
        var req=Arcane(5,"arcane-missiles");
        var result=new MageSimRunner().Run(req);
        Assert.Equal(req.MageMaximumHealth-req.IncomingDamageAmount,
            result.EndingHealth);
    }
}
