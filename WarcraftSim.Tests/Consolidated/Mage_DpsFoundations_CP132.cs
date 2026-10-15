using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class MageDpsFoundationsCp132Tests
{
    private static MageSimResponse Run(string spec) =>
        new MageSimRunner().Run(MageSimRequest.CreatePreset(spec));

    [Theory]
    [InlineData("arcane")]
    [InlineData("fire")]
    [InlineData("frost")]
    public void EachSpecCastsAndDealsDamage(string spec)
    {
        var result = Run(spec);
        Assert.True(result.DamageDone > 0m);
        Assert.True(result.Dps > 0m);
        Assert.Equal(result.DamageDone, result.Run.Summary.ActorSummaries["mage"].DamageDone);
        Assert.InRange(Math.Abs(result.DamageDone - result.DamageByAbility.Values.Sum()), 0m, .000001m);
    }

    [Fact]
    public void ArcaneBlastBuildsStacksAndThenReleasesWithAnotherSpell()
    {
        var request = MageSimRequest.CreatePreset("arcane");
        request.UseMissileBarrage = false;
        var result = new MageSimRunner().Run(request);
        Assert.True(result.ArcaneBlastCasts >= 3);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "fireball" &&
            ev.Type == CombatEventType.AbilityCastCompleted);
    }

    [Fact]
    public void ArcaneBlastIncreasingManaCostsCanBeObservedOnResourceEvents()
    {
        var r = MageSimRequest.CreatePreset("arcane");
        r.UseMissileBarrage = false;
        var result = new MageSimRunner().Run(r);
        var costs = result.Run.Timeline.Where(e => e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == "arcane-blast" && e.Amount < 0m).Select(e => -e.Amount!.Value).ToList();
        Assert.True(costs.Count >= 3);
        Assert.True(costs[1] > costs[0]);
        Assert.True(costs[2] > costs[1]);
    }

    [Fact]
    public void MissileBarrageMakesArcaneMissilesFreeAndFast()
    {
        var r = MageSimRequest.CreatePreset("arcane");
        r.Tuning.MissileBarrageArcaneBlastChancePercent = 100m;
        r.Tuning.MissileBarrageOtherChancePercent = 100m;
        var result = new MageSimRunner().Run(r);
        Assert.True(result.MissileBarrageProcs > 0);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "arcane-missiles" &&
            ev.Type == CombatEventType.AbilityChannelStarted);
        // Once the discounted channel begins, its completion should not debit mana.
        Assert.DoesNotContain(result.Run.Timeline, ev => ev.Type == CombatEventType.ResourceChanged &&
            ev.AbilityKey == "arcane-missiles" && ev.Amount < 0m);
    }

    [Fact]
    public void DisablingMissileBarrageRemovesChannelFromArcaneRotation()
    {
        var r = MageSimRequest.CreatePreset("arcane");
        r.UseMissileBarrage = false;
        var result = new MageSimRunner().Run(r);
        Assert.Equal(0, result.MissileBarrageProcs);
        Assert.DoesNotContain(result.Run.Timeline, ev => ev.AbilityKey == "arcane-missiles" &&
            ev.Type == CombatEventType.AbilityChannelStarted);
    }

    [Fact]
    public void FireHeatsUpAndUsesPyroblast()
    {
        var r = MageSimRequest.CreatePreset("fire");
        r.SpellCritPercent = 100m;
        r.BaseSpellHitPercent = 100m;
        var result = new MageSimRunner().Run(r);
        Assert.True(result.HeatingUpPyroblasts > 0);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "pyroblast" &&
            ev.Type == CombatEventType.Damage);
    }

    [Fact]
    public void FireHasASeparateIgniteDamageLine()
    {
        var r = MageSimRequest.CreatePreset("fire");
        r.SpellCritPercent = 100m;
        r.BaseSpellHitPercent = 100m;
        var result = new MageSimRunner().Run(r);
        Assert.True(result.IgniteDamage > 0m);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "mage-ignite" &&
            ev.Type == CombatEventType.Damage);
    }

    [Fact]
    public void FrostChargesAreGeneratedAndUsedByIceLance()
    {
        var r = MageSimRequest.CreatePreset("frost");
        r.Tuning.FingersOfFrostChancePercentPerRank = 50m;
        r.BaseSpellHitPercent = 100m;
        var result = new MageSimRunner().Run(r);
        Assert.True(result.FingersOfFrostProcs > 0);
        Assert.True(result.FingersOfFrostIceLances > 0);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "ice-lance" &&
            ev.Type == CombatEventType.Damage);
    }

    [Fact]
    public void FrostfireBoltHasBothAHitAndTicks()
    {
        var r = MageSimRequest.CreatePreset("frost");
        r.UseFrostfireBolt = true;
        var result = new MageSimRunner().Run(r);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "frostfire-bolt" &&
            ev.Type == CombatEventType.Damage && !ev.IsPeriodic);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "frostfire-bolt" &&
            ev.Type == CombatEventType.Damage && ev.IsPeriodic);
    }

    [Fact]
    public void NegativeManaCostFailsEarly()
    {
        var r = MageSimRequest.CreatePreset("fire");
        r.Tuning.FireballMana = -1m;
        Assert.Throws<ArgumentException>(() => new MageSimRunner().Run(r));
    }

    [Fact]
    public void InvalidTalentRankIsRejected()
    {
        var r = MageSimRequest.CreatePreset("frost");
        r.Talents.Ranks["shatter"] = 6;
        Assert.Throws<ArgumentException>(() => new MageSimRunner().Run(r));
    }

    [Fact]
    public void HeatingUpRequiresPyroblast()
    {
        var r = MageSimRequest.CreatePreset("fire");
        r.Talents.Ranks.Remove("pyroblast");
        Assert.Throws<ArgumentException>(() => new MageSimRunner().Run(r));
    }

    [Fact]
    public void EqualSeedsGiveEqualDamageAndResource()
    {
        var a = Run("arcane");
        var b = Run("arcane");
        Assert.Equal(a.DamageDone, b.DamageDone);
        Assert.Equal(a.EndingMana, b.EndingMana);
    }

    [Fact]
    public void CharacterSpecializationMustMatchRequest()
    {
        var r = MageSimRequest.CreatePreset("frost");
        r.Character.SpecializationKey = "fire";
        Assert.Throws<ArgumentException>(() => new MageSimRunner().Run(r));
    }

    [Fact]
    public void ManaStarvationIsReportedWithoutKillingMage()
    {
        var r = MageSimRequest.CreatePreset("fire");
        r.DurationSeconds = 180m;
        r.Tuning.ManaRegenPerSecond = 0m;
        var result = new MageSimRunner().Run(r);
        Assert.True(result.Run.Summary.ActorSummaries["mage"].ResourceStarvedSeconds > 0m);
        Assert.False(result.Run.Summary.ActorSummaries["mage"].Died);
    }
}
