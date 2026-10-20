using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class Cp155DruidExportCorrectionsTests
{
    [Fact]
    public void MissedRakeNeverAppliesBleedOrGeneratesComboPoints()
    {
        var result = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "cat", DurationSeconds = 50m, Seed = 15501, CaptureTimeline = true,
            Tuning = new ForeverFeralDruidTuning { MeleeHitPercent = 0m, MeleeCritPercent = 0m }
        });
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "rake" && e.ResultKey == "miss");
        Assert.DoesNotContain(result.Run.Timeline, e => e.Type == CombatEventType.AuraApplied &&
            e.AbilityKey == "rake");
        Assert.DoesNotContain(result.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "rake" && e.IsPeriodic);
        Assert.Equal(0, result.ComboPoints);
    }

    [Fact]
    public void MissedLacerateNeverAppliesOrStacksItsBleed()
    {
        var result = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "bear", DurationSeconds = 45m, Seed = 15502, CaptureTimeline = true,
            Tuning = new ForeverFeralDruidTuning { MeleeHitPercent = 0m, MeleeCritPercent = 0m }
        });
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "lacerate" && e.ResultKey == "miss");
        Assert.DoesNotContain(result.Run.Timeline, e => e.Type == CombatEventType.AuraApplied &&
            e.AbilityKey == "lacerate");
        Assert.Equal(0, result.LacerateStacks);
    }

    [Fact]
    public void LandedFeralBleedTicksDoNotRerollMissButRetainCriticalEligibility()
    {
        var talents = new ForeverFeralDruidTalents();
        var spellbook = ForeverFeralDruidSpellbook.Create(talents, new ForeverFeralDruidTuning());
        foreach (var ability in new[] { "rake", "rip", "lacerate" })
        {
            var bleed = Assert.Single(spellbook.Single(a => a.Key == ability).Effects,
                e => e.EffectType == AbilityEffectTypes.PeriodicDamage);
            Assert.False(bleed.CanMiss);
            Assert.True(bleed.CanCrit);
        }
        Assert.Equal("rake-hit", spellbook.Single(a => a.Key == "rake").Effects[1].DependsOnEffectKey);
        Assert.Equal("lacerate-impact", spellbook.Single(a => a.Key == "lacerate").Effects[1].DependsOnEffectKey);
        var hit = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "cat", Seed = 15503, DurationSeconds = 50m, CaptureTimeline = true,
            Tuning = new ForeverFeralDruidTuning { MeleeHitPercent = 100m, MeleeCritPercent = 0m }
        });
        Assert.Contains(hit.Run.Timeline, e => e.Type == CombatEventType.AuraApplied && e.AbilityKey == "rake");
        Assert.Contains(hit.Run.Timeline, e => e.Type == CombatEventType.Damage && e.AbilityKey == "rake" && e.IsPeriodic && e.Amount > 0m);
        Assert.DoesNotContain(hit.Run.Timeline, e => e.Type == CombatEventType.Damage && e.AbilityKey == "rake" && e.IsPeriodic && e.ResultKey == "miss");
    }

    [Fact]
    public void ShiftingPowerUsesBaseManaAndVerifiedThreeRankPrerequisite()
    {
        var talents = new ForeverFeralDruidTalents();
        talents.Ranks["shredding-attacks"] = 2;
        talents.Ranks["shifting-power"] = 1;
        Assert.Throws<ArgumentException>(() => talents.Validate());
        talents.Ranks["shredding-attacks"] = 3;
        talents.Ranks["improved-shifting-power"] = 2;
        talents.Validate();
        var tuning = new ForeverFeralDruidTuning {
            ShiftingPowerBaseMana = 1244m, ShapeshiftMana = 250m
        };
        var spells = ForeverFeralDruidSpellbook.Create(talents, tuning);
        var ability = Assert.Single(spells, a => a.Key == "shifting-power");
        Assert.Equal(684.2m, Assert.Single(ability.ResourceCosts).Amount);
        Assert.Equal(8m, ability.CooldownSeconds);
        Assert.Equal(42m, Assert.Single(spells.Single(a => a.Key == "shred").ResourceCosts).Amount);
        Assert.Equal(12m, Assert.Single(spells.Single(a => a.Key == "lacerate").ResourceCosts).Amount);
        tuning.ShiftingPowerCostReductionPercent = 20m;
        Assert.Equal(547.36m, Assert.Single(ForeverFeralDruidSpellbook.Create(talents,tuning)
            .Single(a => a.Key == "shifting-power").ResourceCosts).Amount);
    }

    [Fact]
    public void ShiftingPowerActuallyRestoresEnergyAndRecordsResourceEvents()
    {
        var talents = new ForeverFeralDruidTalents();
        talents.Ranks["shredding-attacks"] = 3;
        talents.Ranks["shifting-power"] = 1;
        var sim = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "cat", Seed = 15504, DurationSeconds = 36m,
            CaptureTimeline = true, Talents = talents
        });
        Assert.Contains(sim.Run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted &&
            e.AbilityKey == "shifting-power");
        Assert.Contains(sim.Run.Timeline, e => e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == "shifting-power" && e.ResourceKey == "energy" && e.Amount > 0m);
        Assert.Contains(sim.Run.Timeline, e => e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == "shifting-power" && e.ResourceKey == "mana" && e.Amount < 0m);
    }

    [Fact]
    public void BearIncomingTimelineKeepsConfiguredRawHitAndMitigation()
    {
        var sim = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "bear", Seed = 15505, DurationSeconds = 120m, CaptureTimeline = true,
            EnableBossAttacks = true, BossHitDamage = 850m, BearDodgePercent = 0m
        });
        Assert.Contains(sim.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "encounter:feral-boss-melee" &&
            e.RawAmount == 850m && e.MitigatedAmount > 0m && e.Amount < 850m);
        Assert.Equal(0m, sim.ProjectedEndingHealth);
        Assert.NotNull(sim.ProjectedHealthZeroAtSeconds);
        Assert.InRange(sim.ProjectedHealthZeroAtSeconds!.Value, 1m, 120m);
        Assert.False(sim.Run.Summary.PrimaryActorDied);
    }

    [Fact]
    public void BearProjectedRecoveryCountsAsEffectiveHealingAndOverhealing()
    {
        var sim = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "bear", Seed = 15506, DurationSeconds = 35m, CaptureTimeline = true,
            EnableBossAttacks = true, BearDodgePercent = 0m, UseFeralCooldowns = true
        });
        Assert.True(sim.FrenziedRegenerationHealing > 0m);
        Assert.Equal(sim.FrenziedRegenerationHealing,
            sim.EffectiveSelfHealing + sim.WastedSelfHealing);
        Assert.Equal(sim.EffectiveSelfHealing, sim.Run.Summary.HealingDone);
        Assert.Contains(sim.Run.Timeline, e => e.Type == CombatEventType.Healing &&
            e.AbilityKey == "frenzied-regeneration" && e.Amount > 0m);
        Assert.Contains(sim.Run.Timeline, e => e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == "frenzied-regeneration" && e.ResourceKey == "rage" && e.Amount < 0m);
    }

    [Fact]
    public void CatGeneratesOrdinaryThreatForItsDamage()
    {
        var sim = new FeralDruidSimRunner().Run(new FeralDruidSimRequest {
            Role = "cat", Seed = 15507, DurationSeconds = 40m,
            CaptureTimeline = true
        });
        Assert.True(sim.DamageDone > 0m);
        Assert.True(sim.ThreatGenerated > 0m);
        Assert.Equal(sim.DamageDone, sim.ThreatGenerated);
        Assert.Equal(sim.ThreatGenerated / 40m, sim.ThreatPerSecond);
    }
    [Fact]
    public void RestorationWithGroupHealingTalentsRunsModerateScenario()
    {
        var talents = new ForeverRestorationDruidTalents();
        talents.Ranks["wild-growth"] = 1;
        talents.Ranks["swiftmend"] = 1;
        var sim = new RestorationDruidSimRunner().Run(new RestorationDruidSimRequest {
            DurationSeconds = 40m, CaptureTimeline = true, Seed = 15508,
            AdditionalAllies = 4, AllyStartingHealthPercent = 70m,
            IncomingDamage = 300m, DamageEverySeconds = 4m,
            SwiftmendHealthThresholdPercent = 85m, Talents = talents
        });
        Assert.True(sim.HealingDone > 0m);
        Assert.Contains(sim.Run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted &&
            e.AbilityKey == "wild-growth");
        Assert.Contains(sim.Run.Timeline, e => e.Type == CombatEventType.Healing &&
            e.Amount > 0m);
    }
}
