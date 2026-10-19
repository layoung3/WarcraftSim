using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests;

/// <summary>
/// CP147: outcomes and explicit constraints, not just the existence of ability names.
/// Provisional coefficients are set explicitly in deterministic scenarios.
/// </summary>
public sealed class Cp147RogueRestorationClosureTests
{
    [Fact]
    public void CooldownResetRestoresAllChargesWithoutShorteningSharedGcd()
    {
        var state = new AbilityState(new AbilityDefinition {
            Key = "test-charge", MaxCharges = 2, CooldownSeconds = 120m
        });
        Assert.True(state.ConsumeCharge(0m));
        Assert.True(state.ConsumeCharge(0m));
        Assert.False(state.IsReady(1m));
        state.ResetCooldown(2m);
        Assert.Equal(2, state.CurrentCharges);
        Assert.True(state.ConsumeCharge(2m));
        Assert.True(state.ConsumeCharge(2m));
        Assert.False(state.IsReady(3m));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.ResetCooldown(-1m));
    }

    [Fact]
    public void PreparationResetsEligibleAbilitiesButNotItsOwnCooldown()
    {
        var req = RogueSimRequest.CreatePreset("subtlety");
        req.Talents.Ranks["preparation"] = 1;
        req.StartInStealth = false;
        req.UseUtilityAbilities = true;
        req.DurationSeconds = 45m;
        req.CaptureTimeline = true;
        var result = new RogueSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline, e =>
            e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == "preparation");
        Assert.True(result.Run.CustomMetrics["rogue-preparation-cooldowns-reset"] >= 4m);
        var spells = ForeverRogueSpellbook.Create("subtlety", req.Talents, req.Tuning,
            400m, true);
        Assert.Equal(600m, Assert.Single(spells, a => a.Key == "preparation").CooldownSeconds);
    }

    [Fact]
    public void WeaponDependentHackAndSlashProducesRealExtraAttacks()
    {
        var req = RogueSimRequest.CreatePreset("combat");
        req.StartInStealth = false;
        req.CaptureTimeline = true;
        req.DurationSeconds = 35m;
        req.Talents.Ranks["hack-and-slash"] = 5;
        req.Tuning.HackAndSlashExtraAttackPercentPerRank = 20m;
        req.Tuning.MainHandHitPercent = 100m;
        req.Tuning.OffHandHitPercent = 100m;
        var result = new RogueSimRunner().Run(req);
        Assert.True(result.Run.CustomMetrics["rogue-hack-and-slash-attacks"] > 0m);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "hack-and-slash-extra" && e.Amount > 0m);
        Assert.Contains("hack-and-slash-extra", result.DamageByAbility.Keys);
        req.MainHandWeaponType = "dagger";
        req.OffHandWeaponType = "dagger";
        var dagger = new RogueSimRunner().Run(req);
        Assert.Equal(0m, dagger.Run.CustomMetrics["rogue-hack-and-slash-attacks"]);
    }

    [Fact]
    public void PositionalConstraintsAndDirtyDeedsExceptionAreExecutable()
    {
        var req = RogueSimRequest.CreatePreset("subtlety");
        var front = ForeverRogueSpellbook.Create("subtlety", req.Talents,
            req.Tuning, 400m, true, behindTarget: false);
        Assert.DoesNotContain(front, a => a.Key == "backstab" ||
            a.Key == "ambush" || a.Key == "garrote");
        req.Talents.Ranks["dirty-deeds"] = 1;
        var dirtyDeeds = ForeverRogueSpellbook.Create("subtlety", req.Talents,
            req.Tuning, 400m, true, behindTarget: false);
        Assert.Contains(dirtyDeeds, a => a.Key == "garrote");
        Assert.DoesNotContain(dirtyDeeds, a => a.Key == "backstab");
    }

    [Fact]
    public void WeaponExpertiseReducesActualConfiguredAvoidance()
    {
        RogueSimRequest NewScenario()
        {
            var request = RogueSimRequest.CreatePreset("combat");
            request.StartInStealth = false;
            request.DurationSeconds = 40m;
            request.TargetDodgePercent = 100m;
            request.Tuning.MainHandHitPercent = 100m;
            request.Tuning.OffHandHitPercent = 100m;
            request.Tuning.PhysicalCritPercent = 0m;
            return request;
        }
        var blocked = new RogueSimRunner().Run(NewScenario());
        var skilled = NewScenario();
        skilled.Talents.Ranks["weapon-expertise"] = 2;
        skilled.Tuning.WeaponExpertiseAvoidanceReductionPerRank = 50m;
        var connects = new RogueSimRunner().Run(skilled);
        Assert.True(connects.DamageDone > blocked.DamageDone);
    }

    [Fact]
    public void RogueCooldownTalentsChangeRealAbilityDefinitions()
    {
        var req = RogueSimRequest.CreatePreset("subtlety");
        req.Talents.Ranks["elusiveness"] = 2;
        req.Talents.Ranks["dirty-tricks"] = 2;
        var spells = ForeverRogueSpellbook.Create("subtlety", req.Talents,
            req.Tuning, 400m, true);
        Assert.Equal(240m, Assert.Single(spells, a => a.Key == "vanish").CooldownSeconds);
        Assert.Equal(150m, Assert.Single(spells, a => a.Key == "blind").CooldownSeconds);
        Assert.Equal(20m, Assert.Single(spells, a => a.Key == "blind").ResourceCosts[0].Amount);
    }

    [Fact]
    public void DeflectionAndRiposteHaveAnIncomingParryExecutionPath()
    {
        var req = RogueSimRequest.CreatePreset("combat");
        req.Talents.Ranks["deflection"] = 5;
        req.Talents.Ranks["riposte"] = 1;
        req.StartInStealth = false;
        req.DurationSeconds = 35m;
        req.CaptureTimeline = true;
        req.EnableBossAttacks = true;
        req.BossSwingSeconds = 2m;
        req.Tuning.BaseDodgePercent = 0m;
        req.Tuning.BaseParryPercent = 100m;
        var result = new RogueSimRunner().Run(req);
        Assert.True(result.Run.CustomMetrics["rogue-boss-attacks-parried"] > 0m);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.AuraApplied &&
            e.AbilityKey == "riposte" && e.EffectKey == "riposte-parry-proc");
        var ability = ForeverRogueSpellbook.Create("combat", req.Talents,
            req.Tuning, 400m, false).Single(a => a.Key == "riposte");
        Assert.Contains("riposte-ready", ability.RequiredSourceAuraKeys);
    }

    [Fact]
    public void HealingStreamActuallyHealsInjuredFriendlyActors()
    {
        var req = new RestorationShamanSimRequest {
            CaptureTimeline = true, DurationSeconds = 32m,
            AllyMaximumHealth = 100000m, AllyInitialHealthPercent = 40m,
            IncomingDamage = 0m, AdditionalAllies = 2,
            Tuning = new ForeverRestorationShamanTuning { HealingCritPercent = 0m, MaximumMana = 40000m }
        };
        var result = new RestorationShamanSimRunner().Run(req);
        Assert.True(result.Run.CustomMetrics["shaman-healing-stream-pulses"] > 0m);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Healing &&
            e.AbilityKey == "healing-stream-totem" && e.Amount is > 0m &&
            e.TargetActorKey != "shaman");
    }

    [Fact]
    public void RestorativeTotemsAndPurificationChangeActualTotemPulseMagnitude()
    {
        var baseline = new ForeverRestorationShamanTalents();
        baseline.Ranks["restorative-totems"] = 0;
        var buffed = new ForeverRestorationShamanTalents();
        buffed.Ranks["restorative-totems"] = 5;
        buffed.Ranks["purification"] = 5;
        var t = new ForeverRestorationShamanTuning { HealingCritPercent = 0m, MaximumMana = 40000m };
        RestorationShamanSimRequest Request(ForeverRestorationShamanTalents talents) => new() {
            Talents = talents, Tuning = t, CaptureTimeline = true, DurationSeconds = 18m,
            IncomingDamage = 0m, AdditionalAllies = 1,
            AllyMaximumHealth = 100000m, AllyInitialHealthPercent = 10m
        };
        var a = new RestorationShamanSimRunner().Run(Request(baseline));
        var b = new RestorationShamanSimRunner().Run(Request(buffed));
        decimal Pulse(RestorationShamanSimResponse x) => x.Run.Timeline.First(e =>
            e.Type == CombatEventType.Healing && e.AbilityKey == "healing-stream-totem" &&
            e.Amount is > 0m).RawAmount!.Value;
        Assert.True(Pulse(b) > Pulse(a));
    }

    [Fact]
    public void HealingFocusUsesUpdatedThreePointCapWithoutInventingPushback()
    {
        var talents = new ForeverRestorationShamanTalents();
        talents.Ranks["healing-focus"] = 3;
        talents.Validate();
        talents.Ranks["healing-focus"] = 4;
        Assert.Throws<ArgumentException>(() => talents.Validate());
        Assert.DoesNotContain("tidal-waves", new ForeverRestorationShamanTalents().Ranks.Keys);
    }
}
