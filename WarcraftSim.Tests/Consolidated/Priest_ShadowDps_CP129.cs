using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Priest;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class PriestShadowDpsCp129Tests
{
    private static ShadowPriestSimRequest Setup(decimal seconds = 42m) => new()
    {
        DurationSeconds = seconds, SpellCritOverridePercent = 0m,
        BaseSpellHitPercent = 100m, TargetMaximumHealth = 200000m,
        CaptureTimeline = true, Seed = 22129
    };

    [Fact]
    public void DefaultShadowBuildUsesLegalLevel60Budget()
    {
        var build = new ForeverPriestShadowTalentBuild();
        build.Validate(60);
        Assert.Equal(51, build.DisciplinePointsSpent + build.HolyPointsSpent + build.ShadowPointsSpent);
        // Forever Shadow Weaving now has three ranks (33/67/100%), not five.
        // The other two declared Shadow points remain unmodeled filler.
        Assert.Equal(39, build.Ranks.Values.Sum());
    }

    [Fact]
    public void InvalidRanksAndBudgetsAreRejected()
    {
        var over = new ForeverPriestShadowTalentBuild { ShadowPointsSpent = 52 };
        Assert.Throws<ArgumentException>(() => over.Validate(60));
        var illegal = new ForeverPriestShadowTalentBuild();
        illegal.Ranks["mind-flay"] = 2;
        Assert.Throws<ArgumentException>(() => illegal.Validate(60));
    }

    [Fact]
    public void FullShadowRotationUsesDoTsDirectCastAndChannel()
    {
        var output = new ShadowPriestSimRunner().Run(Setup());
        Assert.True(output.DamageDone > 0m);
        Assert.Contains(output.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "shadow-word-pain" && e.IsPeriodic);
        Assert.Contains(output.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "devouring-plague" && e.IsPeriodic);
        Assert.Contains(output.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "mind-blast");
        Assert.Contains(output.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "mind-flay" && e.IsChannelTick);
    }

    [Fact]
    public void ShadowformHalvesManaCosts()
    {
        var talents = new ForeverPriestShadowTalentBuild();
        var tuning = new ForeverPriestShadowTuning();
        var withForm = ForeverPriestShadowSpellbook.Create(tuning, talents, 0m);
        var blast = Assert.Single(withForm, a => a.Key == "mind-blast");
        Assert.Equal(tuning.MindBlastMana * .5m, blast.ResourceCosts.Single().Amount);
        talents.Ranks["shadowform"] = 0;
        var withoutForm = ForeverPriestShadowSpellbook.Create(tuning, talents, 0m);
        Assert.Equal(tuning.MindBlastMana, Assert.Single(withoutForm, a => a.Key == "mind-blast")
            .ResourceCosts.Single().Amount);
    }

    [Fact]
    public void DevouringContagionCostReductionIsApplied()
    {
        var talents = new ForeverPriestShadowTalentBuild();
        var tuned = new ForeverPriestShadowTuning();
        var full = ForeverPriestShadowSpellbook.Create(tuned, talents, 0m);
        Assert.Equal(tuned.DevouringPlagueMana * .25m,
            Assert.Single(full, a => a.Key == "devouring-plague").ResourceCosts.Single().Amount);
    }

    [Fact]
    public void PersonalShadowWeavingProcsAndGainsStacks()
    {
        var request = Setup(45m);
        var output = new ShadowPriestSimRunner().Run(request);
        Assert.Equal(5, output.MaximumShadowWeavingStacks);
        Assert.Contains(output.Run.Timeline, e => e.Type == CombatEventType.AuraApplied &&
            e.TargetActorKey == "priest" && e.EffectKey == "shadow-weaving-personal");
        Assert.DoesNotContain(output.Run.Timeline, e => e.Type == CombatEventType.AuraApplied &&
            e.TargetActorKey == "target" && e.EffectKey == "shadow-weaving-personal");
    }

    [Fact]
    public void VampiricEmbraceHealsThePriestWhenInjured()
    {
        var request = Setup(35m);
        request.UseShadowWordDeathExecute = true;
        request.TargetInitialHealthPercent = 19m;
        request.TargetMaximumHealth = 200000m;
        var output = new ShadowPriestSimRunner().Run(request);
        Assert.Contains(output.Run.Timeline, e => e.AbilityKey == "vampiric-embrace-heal" &&
            e.Type == CombatEventType.Healing && e.Amount > 0m);
    }

    [Fact]
    public void ShadowWordDeathIsExecuteOnlyByDefault()
    {
        var healthy = Setup(25m);
        var result = new ShadowPriestSimRunner().Run(healthy);
        Assert.DoesNotContain(result.Run.Timeline, e => e.Type == CombatEventType.AbilityCastStarted &&
            e.AbilityKey == "shadow-word-death");
        healthy.TargetInitialHealthPercent = 15m;
        var execute = new ShadowPriestSimRunner().Run(healthy);
        Assert.Contains(execute.Run.Timeline, e => e.Type == CombatEventType.AbilityCastStarted &&
            e.AbilityKey == "shadow-word-death");
    }

    [Fact]
    public void ShadowWordDeathBacklashDamagesCasterOnNonlethalHit()
    {
        var request = Setup(35m);
        request.TargetInitialHealthPercent = 15m;
        var result = new ShadowPriestSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == "shadow-word-death-backlash" &&
            e.Type == CombatEventType.Damage && e.TargetActorKey == "priest" && e.Amount > 0m);
    }

    [Fact]
    public void DisabledShadowAbilitiesDoNotAppearInCastTimeline()
    {
        var request = Setup(22m);
        request.UseVampiricEmbrace = false;
        request.UseDevouringPlague = false;
        request.UseMindBlast = false;
        var result = new ShadowPriestSimRunner().Run(request);
        Assert.DoesNotContain(result.Run.Timeline, e => e.Type == CombatEventType.AbilityCastStarted &&
            (e.AbilityKey == "vampiric-embrace" || e.AbilityKey == "devouring-plague" ||
             e.AbilityKey == "mind-blast"));
        Assert.True(result.DamageDone > 0m);
    }

    [Fact]
    public void ManaAndDamageAttributionMatchPrimaryActor()
    {
        var output = new ShadowPriestSimRunner().Run(Setup(30m));
        var actor = output.Run.Summary.ActorSummaries["priest"];
        Assert.Equal(actor.DamageDone, output.DamageDone);
        Assert.Equal(output.DamageDone / 30m, output.Dps);
        Assert.True(output.EndingMana < output.MaximumMana);
        Assert.Equal(actor.DamageDone, output.DamageByAbility.Values.Sum());
    }

    [Fact]
    public void SameSeedReproducesDamageAndSpellOutcomes()
    {
        var request = Setup(30m);
        var first = new ShadowPriestSimRunner().Run(request);
        var second = new ShadowPriestSimRunner().Run(request);
        Assert.Equal(first.DamageDone, second.DamageDone);
        Assert.Equal(first.EndingMana, second.EndingMana);
        Assert.Equal(first.DamageByAbility["mind-blast"], second.DamageByAbility["mind-blast"]);
    }
}
