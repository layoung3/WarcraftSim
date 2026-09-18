namespace WarcraftSim.Core.Rulesets;

public static class RulesetFreshnessEvaluator
{
    public static IReadOnlyList<RulesetUpdateNotice>
        GetUpdateNotices(
            RulesetFreshnessMetadata metadata)
    {
        return metadata.Mechanics
            .Where(mechanic =>
                mechanic.HasKnownUpdate)
            .Select(mechanic =>
                new RulesetUpdateNotice
                {
                    MechanicKey =
                        mechanic.MechanicKey,

                    DisplayName =
                        mechanic.DisplayName,

                    ImplementedRevision =
                        mechanic.ImplementedRevision,

                    LatestKnownRevision =
                        mechanic.LatestKnownRevision,

                    Status =
                        mechanic.Status,

                    Confidence =
                        mechanic.Confidence,

                    Message =
                        $"{mechanic.DisplayName} has a newer known ruleset revision. " +
                        $"Simulator: {mechanic.ImplementedRevision}; " +
                        $"latest known: {mechanic.LatestKnownRevision}."
                })
            .ToList();
    }
}
