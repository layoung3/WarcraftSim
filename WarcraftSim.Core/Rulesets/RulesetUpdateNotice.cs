namespace WarcraftSim.Core.Rulesets;

public sealed class RulesetUpdateNotice
{
    public string MechanicKey { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string ImplementedRevision { get; set; } = "";

    public string LatestKnownRevision { get; set; } = "";

    public string Status { get; set; } = "";

    public string Confidence { get; set; } = "";

    public string Message { get; set; } = "";
}
