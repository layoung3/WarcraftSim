namespace WarcraftSim.Data.Forever.Abilities.Warrior;

public static class ForeverSlamRanks
{
    public static IReadOnlyList<ForeverSlamRankDefinition> All { get; } =
    [
        new(1, 1240193, 20, 16m, 15m, 1.5m, 18m, 1.5m),
        new(2, 1464, 30, 32m, 15m, 1.5m, 18m, 1.5m),
        new(3, 8820, 38, 43m, 15m, 1.5m, 18m, 1.5m),
        new(4, 11604, 46, 68m, 15m, 1.5m, 18m, 1.5m),
        new(5, 11605, 54, 87m, 15m, 1.5m, 18m, 1.5m)
    ];

    public static ForeverSlamRankDefinition GetByRank(int rank)
    {
        var definition =
            All.FirstOrDefault(candidate => candidate.Rank == rank);

        return definition ??
            throw new ArgumentOutOfRangeException(
                nameof(rank),
                rank,
                "Unsupported Forever Slam rank."
            );
    }

    public static ForeverSlamRankDefinition GetHighestAvailable(
        int characterLevel)
    {
        if (characterLevel < All[0].RequiredLevel || characterLevel > 60)
        {
            throw new ArgumentOutOfRangeException(
                nameof(characterLevel),
                characterLevel,
                $"Forever Slam requires character level {All[0].RequiredLevel} through 60."
            );
        }

        return All
            .Where(definition => definition.RequiredLevel <= characterLevel)
            .OrderByDescending(definition => definition.RequiredLevel)
            .First();
    }
}
