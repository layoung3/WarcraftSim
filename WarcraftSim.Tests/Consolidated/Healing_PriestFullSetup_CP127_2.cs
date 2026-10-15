using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Data.Forever.Priest;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class HealingPriestFullSetupCp1272Tests
{
    [Fact]
    public void FullSetupAccidentallyPlacedInCharacterFieldGetsActionableError()
    {
        // JSON deserialization of a full request as CharacterProfile leaves
        // ClassKey unset and Level at its default value of 1.
        var request = new HolyPriestPreviewRequest
        {
            UsePriestTalentBuild = true,
            UseCharacterDerivedPriestStats = true,
            PriestCharacter = new CharacterProfile(),
            PriestTalents = new ForeverPriestHealingTalentBuild
            {
                DisciplinePointsSpent = 18,
                HolyPointsSpent = 33
            }
        };

        var error = Assert.Throws<ArgumentException>(
            () => new HolyPriestPreviewRunner().Run(request));
        Assert.Contains("Complete healer setup importer", error.Message);
        Assert.Contains("CharacterProfile", error.Message);
    }

    [Fact]
    public void HolyPresetAllocationFitsLevelSixtyBudget()
    {
        var talents = new ForeverPriestHealingTalentBuild
        {
            DisciplinePointsSpent = 18,
            HolyPointsSpent = 33,
            Ranks = new Dictionary<string, int>
            {
                ["twin-disciplines"] = 3,
                ["mental-agility"] = 2,
                ["meditation"] = 3,
                ["soul-warding"] = 1,
                ["improved-renew"] = 3,
                ["divine-fury"] = 5,
                ["improved-healing"] = 3,
                ["binding-heal"] = 1,
                ["spiritual-guidance"] = 5,
                ["spiritual-healing"] = 3,
                ["litany-of-light"] = 2,
                ["prayer-of-mending"] = 1
            }
        };
        talents.Validate(60);
        Assert.Equal(51, talents.DisciplinePointsSpent + talents.HolyPointsSpent);
    }

    [Fact]
    public void DisciplinePresetAllocationFitsLevelSixtyBudget()
    {
        var talents = new ForeverPriestHealingTalentBuild
        {
            DisciplinePointsSpent = 31,
            HolyPointsSpent = 20,
            Ranks = new Dictionary<string, int>
            {
                ["twin-disciplines"] = 5,
                ["improved-power-word-shield"] = 3,
                ["mental-agility"] = 3,
                ["meditation"] = 3,
                ["mental-strength"] = 5,
                ["soul-warding"] = 1,
                ["penance"] = 1,
                ["divine-aegis"] = 3,
                ["improved-renew"] = 3,
                ["divine-fury"] = 5,
                ["improved-healing"] = 3,
                ["binding-heal"] = 1
            }
        };
        talents.Validate(60);
        Assert.Equal(51, talents.DisciplinePointsSpent + talents.HolyPointsSpent);
    }
}
