using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Client 70707 provisional Frost: real talent ranks, casts, procs and absorbs.</summary>
public sealed class Cp175F3B3MageFrostMechanics
{
    private static MageSimRequest Frost(string spell="frostbolt")
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.DurationSeconds=20m;r.TargetMaximumHealth=10000000m;
        r.BaseSpellHitPercent=100m;r.SpellCritPercent=0m;
        r.UseEvocation=false;r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.Tuning.ManaRegenPerSecond=1000m;
        r.UseFingersOfFrost=false;r.UseWintersChill=false;
        r.CustomPriority=[spell];
        return r;
    }

    [Theory]
    [InlineData(0,25)]
    [InlineData(1,23)]
    [InlineData(2,21)]
    public void ImprovedFrostNovaReducesLiveSpellCooldownByRank(int ranks,int cooldown)
    {
        var r=Frost("frost-nova");
        r.Talents.Ranks["improved-frost-nova"]=ranks;
        var book=ForeverMageDpsSpellbook.Create(r.Talents,r.Tuning,0m);
        Assert.Equal((decimal)cooldown,book.Single(x=>x.Key=="frost-nova").CooldownSeconds);
        r.DurationSeconds=46m;
        var sim=new MageSimRunner().Run(r);
        var events=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="frost-nova").OrderBy(e=>e.TimeSeconds).ToArray();
        Assert.True(events.Length>=2);
        Assert.Equal((decimal)cooldown,events[1].TimeSeconds-events[0].TimeSeconds);
    }

    [Fact]
    public void IceLanceAndFireBlastHaveReal70707EightSecondSharedCategory()
    {
        var r=Frost("ice-lance");
        var book=ForeverMageDpsSpellbook.Create(r.Talents,r.Tuning,0m);
        var lance=Assert.Single(book,x=>x.Key=="ice-lance");
        var blast=Assert.Single(book,x=>x.Key=="fire-blast");
        Assert.Equal(8m,lance.CooldownSeconds);
        Assert.Equal(8m,lance.SharedCooldownSeconds);
        Assert.Equal("mage-fire-blast-ice-lance",lance.SharedCooldownGroupKey);
        Assert.Equal(lance.SharedCooldownGroupKey,blast.SharedCooldownGroupKey);
        Assert.Equal(8m,blast.SharedCooldownSeconds);
        r.CustomPriority=["ice-lance","fire-blast","frostbolt"];
        var sim=new MageSimRunner().Run(r);
        var shared=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastStarted &&
            e.AbilityKey is "ice-lance" or "fire-blast").OrderBy(e=>e.TimeSeconds).ToArray();
        Assert.NotEmpty(shared);
        for(var i=1;i<shared.Length;i++)
            Assert.True(shared[i].TimeSeconds-shared[i-1].TimeSeconds>=8m,
                "Ice Lance/Fire Blast cannot bypass the shared category lockout.");
    }

    [Fact]
    public void WintersChillNextConeDealsTwentyPercentPerTargetOwnedStack()
    {
        var r=Frost("cone-of-cold");
        r.DurationSeconds=19m;r.AdditionalTargets=1;
        r.Character.BaseStats.Values["spell-power"]=0m;
        r.UseWintersChill=true;r.Talents.Ranks["winters-chill"]=5;
        r.CustomPriority=["cone-of-cold","frostbolt"];
        var sim=new MageSimRunner().Run(r);
        var cones=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="cone-of-cold" && e.TargetActorKey=="target" &&
            e.Amount>0m).OrderBy(e=>e.TimeSeconds).ToArray();
        Assert.True(cones.Length>=2);
        var applications=sim.Run.Timeline.Count(e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="winters-chill" && e.TargetActorKey=="target" &&
            e.TimeSeconds>cones[0].TimeSeconds && e.TimeSeconds<cones[1].TimeSeconds);
        Assert.InRange(applications,1,5);
        Assert.Equal(cones[0].Amount*(1m+Math.Min(5,applications)*.20m),
            cones[1].Amount);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="winters-chill" && e.TargetActorKey=="target");
        Assert.DoesNotContain(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="winters-chill" && e.TargetActorKey=="target-1");
    }

    [Fact]
    public void IceBarrierUsesSourcedFrostPowerAndTwoTierDamageBonus()
    {
        var r=Frost("ice-barrier");
        r.Talents.Ranks["ice-barrier"]=1;
        r.Talents.Ranks["frost-warding"]=2;
        var book=ForeverMageDpsSpellbook.Create(r.Talents,r.Tuning,200m);
        var ability=Assert.Single(book,a=>a.Key=="ice-barrier");
        var absorb=Assert.Single(ability.Effects,e=>e.AbsorbKey=="mage-ice-barrier");
        Assert.Equal((819m+20m)*1.2m,absorb.MinimumValue);
        Assert.Equal(480m,ability.ResourceCosts.Single().Amount);
        Assert.Equal(30m,ability.CooldownSeconds);
        var sim=new MageSimRunner().Run(r);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AbsorbApplied &&
            e.AbilityKey=="ice-barrier" && e.Amount==(819m+20m)*1.2m);
    }

    [Fact]
    public void ActualFrostbiteProcsAreTargetOwnedAndReplayDeterministically()
    {
        MageSimRequest Build(int rank,int seed)
        {
            var r=Frost("frostbolt");
            r.DurationSeconds=90m;r.Seed=seed;
            r.Talents.Ranks["frostbite"]=rank;
            r.Talents.Ranks["shatter"]=3;
            return r;
        }
        var baseline=new MageSimRunner().Run(Build(0,175701));
        Assert.Equal(0m,baseline.Run.CustomMetrics["mage-frostbite-procs"]);
        Assert.DoesNotContain(baseline.Run.Timeline,e=>e.AbilityKey=="frostbite");
        MageSimResponse? observed=null;
        for(var seed=175701;seed<175711;seed++)
        {
            var candidate=new MageSimRunner().Run(Build(3,seed));
            if(candidate.Run.CustomMetrics["mage-frostbite-procs"]>0m)
            {
                observed=candidate;
                var repeat=new MageSimRunner().Run(Build(3,seed));
                var events=(MageSimResponse x)=>x.Run.Timeline.Where(e=>
                    e.AbilityKey=="frostbite"&&e.Type==CombatEventType.AuraApplied)
                    .Select(e=>(e.TimeSeconds,e.AbilityKey,e.EffectKey,e.TargetActorKey)).ToArray();
                Assert.Equal(events(candidate),events(repeat));
                break;
            }
        }
        Assert.NotNull(observed);
        Assert.Contains(observed.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="frostbite" && e.EffectKey=="frostbite-freeze" &&
            e.TargetActorKey=="target" && e.SourceActorKey=="mage");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void FingersOfFrostCreatesRealAuraAndRankSizedChargeGrants(int ranks)
    {
        var r=Frost();
        r.Talents.Ranks["fingers-of-frost"]=ranks;
        r.UseFingersOfFrost=true;
        r.Tuning.FingersOfFrostChancePercentPerRank=50m; // Max legal test override; 100% is rejected by tuning validation.
        r.CustomPriority=[];r.DurationSeconds=25m;
        var sim=new MageSimRunner().Run(r);
        Assert.True(sim.FingersOfFrostProcs>0);
        Assert.Equal(ranks*sim.FingersOfFrostProcs,
            (int)sim.Run.CustomMetrics["mage-fingers-charges-granted"]);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="fingers-of-frost" && e.TargetActorKey=="mage");
        Assert.True(sim.FingersOfFrostIceLances>0);
    }

    [Fact]
    public void ImprovedBlizzardHasEightSeparateDamageTicksAndTargetOwnedSlow()
    {
        var r=Frost("blizzard");
        r.DurationSeconds=8.5m;r.Talents.Ranks["improved-blizzard"]=3;
        r.Talents.Ranks["frostbite"]=0;
        var sim=new MageSimRunner().Run(r);
        var ticks=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="blizzard"&&e.IsChannelTick&&e.TargetActorKey=="target").ToArray();
        Assert.Equal(8,ticks.Length);
        Assert.Equal(1168m,ticks.Sum(e=>e.Amount??0m));
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="blizzard"&&e.EffectKey=="improved-blizzard-chill"&&
            e.TargetActorKey=="target");
    }

    [Fact]
    public void FirePresetExecutesLearnedFrostAbilitiesWithoutPresetBonuses()
    {
        var r=MageSimRequest.CreatePreset("fire");
        r.Talents=new ForeverMageTalentBuild {
            FirePointsSpent=16,FrostPointsSpent=35,
            Ranks=new(StringComparer.OrdinalIgnoreCase){
                ["precision"]=3,["improved-frostbolt"]=5,["frostbite"]=3,
                ["ice-lance"]=1,["fingers-of-frost"]=2,
                ["shatter"]=3,["cold-snap"]=1,["ice-barrier"]=1
            }
        };
        r.Talents.Validate("fire");
        r.DurationSeconds=12m;r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;r.UseEvocation=false;
        r.UseMajorManaPotion=false;r.CombatItems.ManaRubyCharges=0;
        r.CustomPriority=["ice-barrier","ice-lance","frostbolt"];
        var sim=new MageSimRunner().Run(r);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AbsorbApplied &&
            e.AbilityKey=="ice-barrier");
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="ice-lance");
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="frostbolt"&&e.Amount>0m);
        r.Talents.Ranks.Remove("ice-lance");
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(r));
    }
}
