using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175EnhancementInputValidation
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void NegativeManaCostsAreRejectedEvenWhenSpellIsDisabled(bool earthShock, bool enabled)
    {
        var request = new EnhancementShamanSimRequest { DurationSeconds = 5m };
        request.UseEarthShock = enabled;
        request.UseLightningBoltFiller = enabled;
        request.UseMaelstromLightningBolt = enabled;
        if (earthShock) request.Tuning.EarthShockMana = -1m;
        else request.Tuning.LightningBoltMana = -1m;

        Assert.Throws<ArgumentException>(() => new EnhancementShamanSimRunner().Run(request));
    }

    [Fact]
    public void ZeroManaCostsRemainValid()
    {
        var request = new EnhancementShamanSimRequest { DurationSeconds = 5m };
        request.Tuning.EarthShockMana = 0m;
        request.Tuning.LightningBoltMana = 0m;
        request.UseLightningBoltFiller = true;

        var result = new EnhancementShamanSimRunner().Run(request);

        Assert.True(result.MaximumMana > 0m);
        Assert.InRange(result.EndingMana, 0m, result.MaximumMana);
    }

    [Theory]
    [InlineData("character")]
    [InlineData("talents")]
    [InlineData("ranks")]
    [InlineData("tuning")]
    [InlineData("items")]
    public void MissingRequiredInputThrowsArgumentException(string missing)
    {
        var request = new EnhancementShamanSimRequest { DurationSeconds = 5m };
        switch (missing)
        {
            case "character": request.Character = null!; break;
            case "talents": request.Talents = null!; break;
            case "ranks": request.Talents.Ranks = null!; break;
            case "tuning": request.Tuning = null!; break;
            case "items": request.CombatItems = null!; break;
        }

        Assert.Throws<ArgumentException>(() => new EnhancementShamanSimRunner().Run(request));
    }
}
