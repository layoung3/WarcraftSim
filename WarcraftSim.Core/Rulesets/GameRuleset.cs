namespace WarcraftSim.Core.Rulesets;

public sealed class GameRuleset
{
    public string Key { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string Version { get; set; } = "";

    public int? MaxLevel { get; set; }

    public bool IsImplemented { get; set; }
}