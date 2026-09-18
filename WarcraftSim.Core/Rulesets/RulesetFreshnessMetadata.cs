namespace WarcraftSim.Core.Rulesets;

public sealed class RulesetFreshnessMetadata
{
    public string RulesetKey { get; set; } = "";

    public string RulesetVersion { get; set; } = "";

    public DateTimeOffset? LastVerifiedUtc { get; set; }

    public List<RulesetMechanicStatus> Mechanics { get; set; } = [];

    public bool HasKnownUpdates =>
        Mechanics.Any(mechanic =>
            mechanic.HasKnownUpdate);
}
