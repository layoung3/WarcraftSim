namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationStatContribution
{
    public CharacterSimulationStatContribution(
        string contributorKey,
        string sourceKey,
        string sourceType,
        string sourceName,
        string statKey,
        decimal amount)
    {
        if (string.IsNullOrWhiteSpace(
                contributorKey))
        {
            throw new ArgumentException(
                "Stat contribution requires a contributor key.",
                nameof(contributorKey)
            );
        }

        if (string.IsNullOrWhiteSpace(
                sourceKey))
        {
            throw new ArgumentException(
                "Stat contribution requires a source key.",
                nameof(sourceKey)
            );
        }

        if (string.IsNullOrWhiteSpace(
                sourceType))
        {
            throw new ArgumentException(
                "Stat contribution requires a source type.",
                nameof(sourceType)
            );
        }

        if (string.IsNullOrWhiteSpace(
                sourceName))
        {
            throw new ArgumentException(
                "Stat contribution requires a source name.",
                nameof(sourceName)
            );
        }

        if (string.IsNullOrWhiteSpace(
                statKey))
        {
            throw new ArgumentException(
                "Stat contribution requires a stat key.",
                nameof(statKey)
            );
        }

        ContributorKey =
            contributorKey.Trim();

        SourceKey =
            sourceKey.Trim();

        SourceType =
            sourceType.Trim();

        SourceName =
            sourceName.Trim();

        StatKey =
            statKey.Trim();

        Amount =
            amount;
    }

    public string ContributorKey { get; }

    public string SourceKey { get; }

    public string SourceType { get; }

    public string SourceName { get; }

    public string StatKey { get; }

    public decimal Amount { get; }
}
