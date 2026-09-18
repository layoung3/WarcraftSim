namespace WarcraftSim.Core.Rulesets;

public sealed class RulesetMechanicStatus
{
    public string MechanicKey { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string Status { get; set; } =
        RulesetMechanicStatuses.Unknown;

    public string Confidence { get; set; } =
        RulesetConfidenceLevels.Unknown;

    // Revision currently used by the simulator.
    public string ImplementedRevision { get; set; } = "";

    // Latest revision we currently believe is established.
    // This may be newer than ImplementedRevision.
    public string LatestKnownRevision { get; set; } = "";

    // Explicit instead of comparing revision strings so revisions
    // can be dates, semantic versions, hashes, etc.
    public bool HasKnownUpdate { get; set; }

    public DateTimeOffset? LastVerifiedUtc { get; set; }

    public string? Notes { get; set; }
}
