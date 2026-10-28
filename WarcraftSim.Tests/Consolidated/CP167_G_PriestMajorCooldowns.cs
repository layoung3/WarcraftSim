using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Priest;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167GPriestMajorCooldowns
{
    private static ForeverPriestHealingTalentBuild Talents() => new() {
        DisciplinePointsSpent=41, HolyPointsSpent=10,
        Ranks=new(StringComparer.OrdinalIgnoreCase) {
            ["inner-focus"]=1, ["penance"]=1, ["power-infusion"]=1
        }
    };

    [Fact]
    public void DisciplineTalentPrerequisitesAndCooldownsAreEnforced()
    {
        var talents=Talents();
        talents.Validate();
        talents.Ranks.Remove("penance");
        Assert.Throws<ArgumentException>(()=>talents.Validate());
        talents.Ranks["penance"]=1;
        talents.DisciplinePointsSpent=20;
        Assert.Throws<ArgumentException>(()=>talents.Validate());

        Assert.Equal(180m,ForeverPriestMajorCooldowns.InnerFocus().CooldownSeconds);
        var power=ForeverPriestMajorCooldowns.PowerInfusion();
        Assert.Equal(180m,power.CooldownSeconds);
        Assert.Equal(15m,power.Effects.Single().DurationSeconds);
        Assert.Equal(1376m*.20m,power.ResourceCosts.Single().Amount);
    }

    [Fact]
    public void HealerCooldownsApplyBuffsAndConsumeInnerFocusWithOutcomeAccounting()
    {
        var req=new HolyPriestPreviewRequest {
            EnableExtendedPriest=true,UsePriestTalentBuild=true,
            PriestTalents=Talents(),UseMajorCooldowns=true,
            UseAdaptiveHealingPolicy=true,
            DurationSeconds=35m,CaptureTimeline=true,
            EnableHealingCriticals=false,
            BossSwingDamage=500m,
            UsePenance=false,UsePrayerOfMending=false,UseBindingHeal=false,
            UsePowerWordShield=false,UseRenew=false,UsePrayerOfHealing=false
        };
        var sim=new HolyPriestPreviewRunner().Run(req);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="power-infusion");
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="inner-focus");
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="inner-focus");
        Assert.True(sim.HealingDone>0m);
    }

    [Fact]
    public void ShadowInnerFocusCanBeSelectedFromDisciplineAndConsumedByDamageSpell()
    {
        var req=new ShadowPriestSimRequest {
            DurationSeconds=30m,CaptureTimeline=true,
            UseInnerFocus=true,UsePowerInfusion=false,
            UseVampiricEmbrace=false,UseShadowWordPain=false,
            UseDevouringPlague=false,UseShadowWordDeathExecute=false,
            UseMindBlast=true,UseMindFlay=false,
            BaseSpellHitPercent=100m, SpellCritOverridePercent=0m
        };
        req.Talents.DisciplinePointsSpent=11;
        req.Talents.ShadowPointsSpent=40;
        req.Talents.Ranks=new(StringComparer.OrdinalIgnoreCase) {
            ["inner-focus"]=1,["mind-blast"]=0,["mind-flay"]=1
        };
        req.Talents.Ranks.Remove("mind-blast");
        var result=new ShadowPriestSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="inner-focus");
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="inner-focus");
        Assert.True(result.DamageDone>0m);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }
}
