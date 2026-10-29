using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BpMageChillSourceOwnership
{
    private static MageSimRequest Frost(string spell,decimal hit,int rank)
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.Talents.Ranks["permafrost"]=rank;
        r.BaseSpellHitPercent=hit;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.CustomPriority=[spell];
        r.CaptureTimeline=true;
        r.DurationSeconds=4m;
        return r;
    }

    [Theory]
    [InlineData("frostbolt","frostbolt-chill")]
    [InlineData("frost-blast","frost-blast-chill")]
    [InlineData("frostfire-bolt","frostfire-chill")]
    public void AllCurrentChillSpellsRequireLandedImpact(string key,string effect)
    {
        var hit=new MageSimRunner().Run(Frost(key,100m,0));
        Assert.Contains(hit.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey==key && e.EffectKey==effect &&
            e.TargetActorKey=="target" && e.SourceActorKey=="mage");
        var missed=new MageSimRunner().Run(Frost(key,0m,0));
        Assert.DoesNotContain(missed.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey==key);
    }

    [Theory]
    [InlineData(0,9)]
    [InlineData(1,9.99)]
    [InlineData(2,10.98)]
    [InlineData(3,11.97)]
    public void PermafrostRankAffectsAllThreeLandOnlyChillDurations(int rank,double duration)
    {
        var build=ForeverMageTalentBuild.Default("frost");
        build.Ranks["permafrost"]=rank;
        build.Validate("frost");
        var spells=ForeverMageDpsSpellbook.Create(build,new ForeverMageTuning(),0m);
        foreach(var key in new[]{"frostbolt","frost-blast","frostfire-bolt"})
        {
            var aura=Assert.Single(spells.Single(x=>x.Key==key).Effects,
                x=>x.EffectType==WarcraftSim.Core.Abilities.AbilityEffectTypes.ApplyAura);
            Assert.Equal((decimal)duration,aura.DurationSeconds);
            Assert.NotNull(aura.DependsOnEffectKey);
        }
    }
}