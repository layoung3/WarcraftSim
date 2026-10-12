using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverWarriorTalentRaceBuilderTests
{
    private static WarriorSimRequest Request() => new()
    {
        Level = 60, Preset = "arms", Iterations = 1, DurationSeconds = 12m,
        ExecutePhaseDurationSeconds = 0m, TargetLevel = 60,
        TargetDodgePercent = 0m, TargetParryPercent = 0m, TargetArmor = 0m,
        UseBloodrage = false, DeepWoundsRank = 0, ImpaleRank = 0
    };

    [Fact]
    public void Catalog_CoversCurrentFiftyTwoTalentThreeTreeLayout()
    {
        var all = ForeverWarriorTalentCatalog.Talents;
        Assert.Equal(52, all.Count);
        Assert.Equal(17, all.Count(x => x.Tree == "arms"));
        Assert.Equal(17, all.Count(x => x.Tree == "fury"));
        Assert.Equal(18, all.Count(x => x.Tree == "protection"));
        Assert.Equal(all.Count, all.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(all, x => x.Id == "spearing-strike" && x.Row == 4);
        Assert.Contains(all, x => x.Id == "raging-blows" && x.Row == 4);
        Assert.Contains(all, x => x.Id == "vanguard" && x.Row == 4);
        Assert.DoesNotContain(all, x => x.Id == "improved-cleave");
        Assert.DoesNotContain(all, x => x.Id == "toughness");
    }

    [Theory]
    [InlineData("arms", 17)]
    [InlineData("fury", 17)]
    [InlineData("protection", 18)]
    public void TalentDefinitions_HaveValidRowsColumnsAndRankCaps(string tree, int count)
    {
        var definitions = ForeverWarriorTalentCatalog.Talents.Where(x => x.Tree == tree).ToList();
        Assert.Equal(count, definitions.Count);
        Assert.All(definitions, d =>
        {
            Assert.InRange(d.Row, 1, 7);
            Assert.InRange(d.Column, 1, 4);
            Assert.InRange(d.MaxRank, 1, 5);
            Assert.NotEmpty(d.Description);
        });
    }

    [Fact]
    public void TreeMode_EnforcesPrerequisitesAndTierUnlocks()
    {
        var r = Request();
        r.TalentMode = "tree";
        r.TalentRanks["impale"] = 1;
        Assert.Throws<ArgumentException>(() => ForeverWarriorTalentCatalog.Validate(r));
        r.TalentRanks.Clear();
        r.TalentRanks["improved-rend"] = 3;
        r.TalentRanks["deflection"] = 5;
        r.TalentRanks["improved-heroic-strike"] = 3;
        r.TalentRanks["improved-tactical-mastery"] = 5;
        r.TalentRanks["deep-wounds"] = 3;
        r.TalentRanks["impale"] = 2;
        ForeverWarriorTalentCatalog.Validate(r);
        r.TalentRanks["deep-wounds"] = 2;
        Assert.Throws<ArgumentException>(() => ForeverWarriorTalentCatalog.Validate(r));
    }

    [Fact]
    public void UnrecognizedAndOverCapTalentRanks_RejectWithoutChangingRequest()
    {
        var r = Request();
        r.TalentMode = "tree";
        r.TalentRanks["cruelty"] = 6;
        Assert.Throws<ArgumentException>(() => ForeverWarriorTalentCatalog.Validate(r));
        r.TalentRanks.Clear();
        r.TalentRanks["invented-talent"] = 1;
        Assert.Throws<ArgumentException>(() => ForeverWarriorTalentCatalog.Validate(r));
        Assert.Equal(1, r.TalentRanks["invented-talent"]);
    }

    [Fact]
    public void BaseRaces_AreAvailableAndProvisional()
    {
        Assert.Equal(9, ForeverWarriorRaceProfiles.Races.Count);
        Assert.All(ForeverWarriorRaceProfiles.Races, x => Assert.False(x.IsVerifiedForever));
        Assert.Equal(120m, ForeverWarriorRaceProfiles.Get("human").Strength);
        Assert.Equal(123m, ForeverWarriorRaceProfiles.Get("orc").Strength);
    }

    [Fact]
    public void RaceBaseMode_IgnoresManualSheetValuesAndCalculatesStrengthAndAgility()
    {
        var r = Request();
        r.StatSourceMode = "race-base";
        r.Race = "human";
        r.AttackPower = 123456m;
        r.MeleeCritPercent = 99m;
        var stats = WarriorGearStatCalculator.Calculate(r).EffectiveStats;
        Assert.Equal(120m, stats.Get(ForeverCombatStatKeys.Strength));
        Assert.Equal(80m, stats.Get(ForeverCombatStatKeys.Agility));
        Assert.Equal(240m, stats.Get(ForeverCombatStatKeys.AttackPower));
        Assert.Equal(4m, stats.Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
        Assert.Equal(110m, stats.Get("stamina"));
    }

    [Fact]
    public void RaceMode_WithCruelty_StacksTalentAndRaceBonuses()
    {
        var r = Request();
        r.StatSourceMode = "race-base";
        r.Race = "orc";
        r.TalentMode = "tree";
        r.TalentRanks["cruelty"] = 5;
        var stats = WarriorGearStatCalculator.Calculate(r);
        Assert.Equal(246m, stats.EffectiveStats.Get(ForeverCombatStatKeys.AttackPower));
        Assert.Equal(77m / 20m + 5m,
            stats.EffectiveStats.Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
        Assert.Contains(stats.StatContributions, x => x.SourceName == "Cruelty");
    }

    [Fact]
    public void RaceMode_ChangesAnActualSimulationWithoutMutatingInput()
    {
        var r = Request();
        r.StatSourceMode = "race-base";
        r.Race = "human";
        var baselineAP = r.AttackPower;
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.Equal(240m, output.EffectiveCharacterStats[ForeverCombatStatKeys.AttackPower]);
        Assert.Equal(baselineAP, r.AttackPower);
        Assert.Contains(output.ModelingNotes, x => x.Contains("RACIAL BASE PREVIEW"));
    }

    [Fact]
    public void TreeMode_WithoutMortalStrike_DoesNotReportMortalStrikeDamage()
    {
        var r = Request();
        r.TalentMode = "tree";
        var result = new ForeverWarriorSimRunner().Run(r);
        Assert.DoesNotContain(result.Batch.Summary.AverageDamageDoneByAbility.Keys,
            x => x.Contains("Mortal Strike", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TreeMode_NotesPlannerOnlyTalentSelections()
    {
        var r = Request();
        r.TalentMode = "tree";
        r.TalentRanks["deflection"] = 5;
        var result = new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(result.ModelingNotes, x =>
            x.Contains("TALENT NOT YET SIMULATED: Deflection"));
    }

    [Fact]
    public void PartiallyModeledSpecializations_AreReportedWithoutCallingThemUnsupported()
    {
        var twoHanded = ForeverWarriorTalentCatalog.Talents.Single(x => x.Id == "two-handed-weapon-specialization");
        var dualWield = ForeverWarriorTalentCatalog.Talents.Single(x => x.Id == "dual-wield-specialization");
        Assert.Equal("partial", twoHanded.SimulationStatus);
        Assert.Equal("partial", dualWield.SimulationStatus);
        var request = Request();
        request.TalentMode = "tree";
        request.TalentRanks["two-handed-weapon-specialization"] = 1;
        Assert.Contains(ForeverWarriorTalentCatalog.UnmodeledSelections(request),
            x => x.Contains("TALENT PARTIALLY SIMULATED: Two-Handed Weapon Specialization"));
    }

    [Fact]
    public void SpearingStrike_DamageScalesWithExplicitCreatureCategory()
    {
        var normal = WarcraftSim.Data.Forever.Abilities.Warrior.ForeverSpearingStrikeFactory.Create(
            100m, 200m, 3.3m, "main-hand-weapon-skill", false);
        var bonus = WarcraftSim.Data.Forever.Abilities.Warrior.ForeverSpearingStrikeFactory.Create(
            100m, 200m, 3.3m, "main-hand-weapon-skill", true);
        Assert.Equal(40m, normal.Effects.Single().MinimumValue);
        Assert.Equal(120m, bonus.Effects.Single().MinimumValue);
        Assert.Equal(20m, bonus.CooldownSeconds);
        Assert.Equal(15m, bonus.ResourceCosts.Single().Amount);
    }

    [Fact]
    public void SpearingStrike_RequiresSelectionAndSupportedCreatureType()
    {
        var r = Request();
        r.TalentMode = "tree";
        r.TalentRanks["improved-heroic-strike"] = 3;
        r.TalentRanks["deflection"] = 5;
        r.TalentRanks["improved-rend"] = 3;
        r.TalentRanks["improved-overpower"] = 2;
        r.TalentRanks["improved-tactical-mastery"] = 2;
        r.TalentRanks["spearing-strike"] = 1;
        ForeverWarriorSimRunner.Validate(r);
        r.TargetCreatureType = "invented";
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(r));
    }

    [Fact]
    public void CatalogController_ExposesTalentsAndRaces()
    {
        var controller = new WarriorSimulationController();
        Assert.IsType<OkObjectResult>(controller.TalentCatalog());
        Assert.IsType<OkObjectResult>(controller.RaceCatalog());
    }
}
