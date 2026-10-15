using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class ShamanEnhancementWindfuryCp130Tests
{
    private static EnhancementShamanSimRequest Setup(decimal seconds = 45m) => new()
    {
        DurationSeconds = seconds, CaptureTimeline = true,
        Seed = 130013, BaseMeleeHitPercent = 100m,
        BaseSpellHitPercent = 100m, CriticalChancePercent = 0m,
        ProvisionalGlancingChancePercent = 0m
    };

    [Fact]
    public void DefaultEnhancementTalentBudgetIsLegal()
    {
        var talents = new ForeverEnhancementShamanTalents();
        talents.Validate(60);
        Assert.Equal(51, talents.ElementalPointsSpent + talents.EnhancementPointsSpent +
            talents.RestorationPointsSpent);
    }

    [Fact]
    public void InvalidEnhancementRanksAndBudgetsAreRejected()
    {
        var talents = new ForeverEnhancementShamanTalents { EnhancementPointsSpent = 52 };
        Assert.Throws<ArgumentException>(() => talents.Validate(60));
        talents = new ForeverEnhancementShamanTalents();
        talents.Ranks["flurry"] = 6;
        Assert.Throws<ArgumentException>(() => talents.Validate(60));
    }

    [Fact]
    public void WindfuryWeaponCreatesTwoRealExtraAttacksPerProc()
    {
        var request = Setup(16m);
        request.UseEarthShock = false; request.UseStormstrike = false;
        request.UseMaelstromLightningBolt = false;
        request.Tuning.WindfuryWeaponProcChancePercent = 100m;
        request.Tuning.WindfuryWeaponInternalCooldownSeconds = 0m;
        var outcome = new EnhancementShamanSimRunner().Run(request);
        Assert.True(outcome.WindfuryWeaponProcs > 0);
        Assert.Equal(outcome.WindfuryWeaponProcs * 2, outcome.WindfuryWeaponExtraAttacks);
        Assert.Contains(outcome.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "windfury-weapon-extra");
        Assert.Equal(0, outcome.WindfuryTotemProcs);
    }

    [Fact]
    public void ShamanTotemOnlyProcsIfWeaponImbueIsAbsent()
    {
        var request = Setup(180m);
        request.UseWindfuryWeapon = false;
        request.UseEarthShock = false; request.UseStormstrike = false;
        request.UseMaelstromLightningBolt = false;
        var totem = new EnhancementShamanSimRunner().Run(request);
        Assert.Equal(0, totem.WindfuryWeaponProcs);
        Assert.True(totem.WindfuryTotemProcs > 0);
        request.UseWindfuryTotem = false;
        var noProc = new EnhancementShamanSimRunner().Run(request);
        Assert.Equal(0, noProc.WindfuryTotemProcs);
        Assert.True(totem.DamageDone > noProc.DamageDone);
    }

    [Fact]
    public void StormstrikeAndShockUseNormalCombatReporting()
    {
        var result = new EnhancementShamanSimRunner().Run(Setup(38m));
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == "stormstrike" &&
            e.Type == CombatEventType.Damage);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == "earth-shock" &&
            e.Type == CombatEventType.Damage);
    }

    [Fact]
    public void MaelstromCanUnlockAnInstantLightningBolt()
    {
        var request = Setup(35m);
        request.UseWindfuryWeapon = true;
        request.Tuning.WindfuryWeaponProcChancePercent = 100m;
        request.Tuning.WindfuryWeaponInternalCooldownSeconds = 0m;
        request.Tuning.MaelstromChancePerMeleePercentPerRank = 20m;
        var result = new EnhancementShamanSimRunner().Run(request);
        Assert.Equal(5, result.MaxMaelstromStacks);
        Assert.True(result.MaelstromInstantBolts > 0);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == "maelstrom-lightning-bolt" &&
            e.Type == CombatEventType.Damage);
    }

    [Fact]
    public void ManaSpentAndDamageReportingReconcile()
    {
        var result = new EnhancementShamanSimRunner().Run(Setup(35m));
        Assert.True(result.DamageDone > 0m);
        Assert.True(result.EndingMana < result.MaximumMana);
        // Damage events are accumulated in a different order from per-ability subtotals.
        // Decimal rounding at the final representable place must not fail reconciliation.
        var attributedDamage = result.DamageByAbility.Values.Sum();
        Assert.InRange(Math.Abs(result.DamageDone - attributedDamage), 0m, 0.000001m);
        Assert.Equal(result.DamageDone / 35m, result.Dps);
    }

    [Fact]
    public void SeededEnhancementIsReproducible()
    {
        var request = Setup(35m);
        var a = new EnhancementShamanSimRunner().Run(request);
        var b = new EnhancementShamanSimRunner().Run(request);
        Assert.Equal(a.DamageDone, b.DamageDone);
        Assert.Equal(a.WindfuryWeaponProcs, b.WindfuryWeaponProcs);
        Assert.Equal(a.EndingMana, b.EndingMana);
    }

    [Fact]
    public void WarriorWindfuryGeneratesExtraMeleeDamageAndRage()
    {
        var request = new WarriorSimRequest
        {
            Preset = "arms", TalentMode = "legacy", EquipmentMode = "manual",
            Iterations = 1, DurationSeconds = 45m, Seed = 1100130,
            CaptureRepresentativeTimeline = true, UseBloodrage = false,
            UseHeroicStrike = false, UseRend = false, UseExecute = false,
            EnableBossAutoAttacks = false, TargetDodgePercent = 0m,
            TargetParryPercent = 0m, BehindTarget = true,
            EnableWindfuryTotem = true, WindfuryTotemProcChancePercent = 100m
        };
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Contains(result.Batch.RepresentativeRun.Timeline, e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "windfury-totem-extra");
        Assert.Contains(result.Batch.RepresentativeRun.Timeline, e =>
            e.Type == CombatEventType.ResourceChanged && e.AbilityKey == "windfury-totem-extra" &&
            e.ResourceKey == "rage" && e.Amount > 0m);
    }

    [Fact]
    public void WarriorWindfuryDisabledPreservesBaseline()
    {
        var request = new WarriorSimRequest
        {
            Preset = "fury", TalentMode = "legacy", EquipmentMode = "manual",
            Iterations = 1, DurationSeconds = 20m, Seed = 13099,
            EnableWindfuryTotem = false,
            CaptureRepresentativeTimeline = true
        };
        var run = new ForeverWarriorSimRunner().Run(request);
        Assert.DoesNotContain(run.Batch.RepresentativeRun.Timeline, e =>
            e.AbilityKey == "windfury-totem-extra");
    }
}
