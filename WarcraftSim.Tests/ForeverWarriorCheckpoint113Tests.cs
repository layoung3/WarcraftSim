using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverWarriorCheckpoint113Tests
{
    private static WarriorSimRequest Request() => new()
    {
        Preset = "arms", Level = 60, Iterations = 1, DurationSeconds = 8m,
        ExecutePhaseDurationSeconds = 0m, TargetArmor = 0m, TargetLevel = 60,
        TargetDodgePercent = 0m, TargetParryPercent = 0m,
        UseBloodrage = false, UseHeroicStrike = false, StartingRage = 0m,
        MeleeHitPercent = 100m,
        CaptureRepresentativeTimeline = true, TalentMode = "tree"
    };

    [Theory]
    [InlineData(0.00)]
    [InlineData(1.25)]
    [InlineData(2.75)]
    public void OpeningAutoAttackDelay_ControlsFirstActualSwing(double delay)
    {
        var r = Request();
        r.OpeningAutoAttackSeconds = (decimal)delay;
        var result = new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(result.Batch.RepresentativeRun.Timeline, e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == "main-hand-auto" &&
            e.TimeSeconds == (decimal)delay);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(8.0)]
    public void OpeningAutoAttackDelay_OutOfRangeIsRejected(double delay)
    {
        var r = Request();
        r.OpeningAutoAttackSeconds = (decimal)delay;
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(r));
    }

    [Fact]
    public void CustomTalentGatedAbility_RemainsEditableButIsSkippedWithoutTalent()
    {
        var r = Request();
        r.RotationMode = "custom";
        r.CustomRotation = [new WarriorRotationRule { Action = "mortal-strike" }];
        var profile = WarriorRotationRules.BuildCustom(r);
        Assert.Empty(profile.Entries);
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.DoesNotContain(output.Batch.Summary.AverageDamageDoneByAbility.Keys,
            x => x.Contains("mortal-strike", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AutomaticArms_WithoutMortalStrike_StillRunsAutoAttacks()
    {
        var r = Request();
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.Contains("main-hand-auto", output.Batch.Summary.AverageDamageDoneByAbility.Keys);
        Assert.DoesNotContain("mortal-strike", output.Batch.Summary.AverageDamageDoneByAbility.Keys);
    }

    [Fact]
    public void AngerManagement_AddsPeriodicRageAtThreeSecondIntervals()
    {
        var r = Request();
        r.TalentRanks["deflection"] = 5;
        r.TalentRanks["improved-tactical-mastery"] = 5;
        r.TalentRanks["anger-management"] = 1;
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(output.Batch.RepresentativeRun.Timeline, e =>
            e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == "anger-management" && e.TimeSeconds == 3m);
        Assert.Contains(output.Batch.RepresentativeRun.Timeline, e =>
            e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == "anger-management" && e.TimeSeconds == 6m);
    }

    [Fact]
    public void AngerManagement_WithoutRequiredPrerequisite_IsRejected()
    {
        var r = Request();
        r.TalentRanks["deflection"] = 5;
        r.TalentRanks["improved-heroic-strike"] = 3;
        r.TalentRanks["improved-rend"] = 3;
        r.TalentRanks["anger-management"] = 1;
        Assert.Throws<ArgumentException>(() => ForeverWarriorTalentCatalog.Validate(r));
    }

    [Fact]
    public void DeepWounds_NowValidatesImprovedRendPrerequisite()
    {
        var r = Request();
        r.TalentRanks["deflection"] = 5;
        r.TalentRanks["improved-tactical-mastery"] = 5;
        r.TalentRanks["deep-wounds"] = 1;
        Assert.Throws<ArgumentException>(() => ForeverWarriorTalentCatalog.Validate(r));
        r.TalentRanks["improved-rend"] = 3;
        ForeverWarriorTalentCatalog.Validate(r);
    }

    [Fact]
    public void ImprovedExecute_CanBeSelected_WithoutForcingBloodthirst()
    {
        var r = Request();
        r.Preset = "fury";
        r.TalentRanks["cruelty"] = 5;
        r.TalentRanks["booming-voice"] = 5;
        r.TalentRanks["unbridled-wrath"] = 5;
        r.TalentRanks["improved-execute"] = 2;
        ForeverWarriorSimRunner.Validate(r);
        Assert.DoesNotContain("bloodthirst", new ForeverWarriorSimRunner().Run(r).Batch.Summary.AverageDamageDoneByAbility.Keys);
    }

    [Fact]
    public void RagingBlowsAndImprovedSlam_AreNowModeledTalentDefinitions()
    {
        Assert.Equal("modeled", ForeverWarriorTalentCatalog.Talents.Single(x => x.Id == "raging-blows").SimulationStatus);
        Assert.Equal("modeled", ForeverWarriorTalentCatalog.Talents.Single(x => x.Id == "improved-slam").SimulationStatus);
        Assert.Equal("modeled", ForeverWarriorTalentCatalog.Talents.Single(x => x.Id == "improved-execute").SimulationStatus);
        Assert.Equal("modeled", ForeverWarriorTalentCatalog.Talents.Single(x => x.Id == "improved-bloodrage").SimulationStatus);
    }

    [Fact]
    public void CatalogMode_AllowsCatalogWeaponAndCustomNonWeaponStats()
    {
        var weapon = ForeverEquipmentCatalog.GetCatalog().Items.First(x =>
            x.Weapon is not null && x.AllowedSlots.Contains("main-hand"));
        var r = Request();
        r.EquipmentMode = "catalog";
        r.StatSourceMode = "race-base";
        r.Gear =
        [
            new WarriorGearEntry { Slot = "main-hand", ItemId = weapon.Id },
            new WarriorGearEntry
            {
                Slot = "neck", ItemKey = "custom-neck", Name = "Custom test necklace",
                Stats = new() { [ForeverCombatStatKeys.Strength] = 20m }
            }
        ];
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(r);
        Assert.Equal("gear-bonuses", resolved.EquipmentMode);
        Assert.Contains(resolved.Gear, x => x.Slot == "neck" && x.Name == "Custom test necklace");
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.True(output.EffectiveCharacterStats[ForeverCombatStatKeys.Strength] >= 20m);
    }

    [Fact]
    public void CatalogMode_RejectsFakeCustomWeaponAndClientItemOverrides()
    {
        var r = Request();
        r.EquipmentMode = "catalog";
        r.Gear = [new WarriorGearEntry
        {
            Slot = "main-hand", ItemKey = "custom-main-hand", Name = "Unsafe sword",
            Stats = new() { [ForeverCombatStatKeys.Strength] = 500m }
        }];
        Assert.Throws<ArgumentException>(() => ForeverEquipmentCatalog.ResolveWarriorLoadout(r));
    }

    [Fact]
    public void RageTalentProc_UsesOnlyWeaponDamageNotBleedTicks()
    {
        var actor = new SimulationActorState { Key = "warrior", TeamKey = "raid" };
        actor.InitializeHealth(5000m);
        actor.AddResource(new ResourceState("rage", 100m, 0m));
        var ctx = new SimulationContext(new SimulationRunOptions { Seed = 12345, DurationSeconds = 10m });
        ctx.AddActor(actor);
        var processor = new WarriorRageTalentProcessor(5);
        for (var i = 0; i < 100; i++)
            processor.Process(ctx, new CombatEvent
            {
                Type = CombatEventType.Damage, SourceActorKey = "warrior",
                WeaponHandKey = "main-hand", Amount = 1m
            });
        Assert.True(actor.Resources["rage"].Current > 0m);
        var before = actor.Resources["rage"].Current;
        for (var i = 0; i < 100; i++)
            processor.Process(ctx, new CombatEvent
            {
                Type = CombatEventType.Damage, SourceActorKey = "warrior",
                WeaponHandKey = "main-hand", Amount = 1m,
                EffectDeliveryType = CombatEffectDeliveryType.Periodic
            });
        Assert.Equal(before, actor.Resources["rage"].Current);
    }
}
