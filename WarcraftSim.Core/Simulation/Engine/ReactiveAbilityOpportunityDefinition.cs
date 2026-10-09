namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Defines dodge-triggered and optional landed main-hand proc opportunities.
/// The ruleset supplies the reaction windows and eligible attack types.
/// </summary>
public sealed class ReactiveAbilityOpportunityDefinition
{
    public string OpportunityKey { get; init; } = "";
    public string AbilityKey { get; init; } = "";
    public decimal DodgeWindowSeconds { get; init; }
    public decimal ProcWindowSeconds { get; init; }
    public decimal ProcChancePercent { get; init; }
    public string? RequiredTargetAuraKey { get; init; }
    public HashSet<string> EligibleResolutionTypes { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}
