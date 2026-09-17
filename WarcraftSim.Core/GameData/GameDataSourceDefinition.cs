namespace WarcraftSim.Core.GameData;

public sealed class GameDataSourceDefinition
{
    public string Key { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public bool IsOfficial { get; set; }

    public bool SupportsItems { get; set; }

    public bool SupportsAbilities { get; set; }

    public bool SupportsTalents { get; set; }

    public bool SupportsIcons { get; set; }

    public bool SupportsTooltips { get; set; }
}