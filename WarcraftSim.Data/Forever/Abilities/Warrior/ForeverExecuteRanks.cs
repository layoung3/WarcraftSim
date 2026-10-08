namespace WarcraftSim.Data.Forever.Abilities.Warrior;

public static class ForeverExecuteRanks
{
    public static IReadOnlyList<ForeverExecuteRankDefinition> All { get; } =
    [
        new(1, 5308, 24, 125m, 3m, 15m, 1.5m),
        new(2, 20658, 32, 200m, 6m, 15m, 1.5m),
        new(3, 20660, 40, 325m, 9m, 15m, 1.5m),
        new(4, 20661, 48, 450m, 12m, 15m, 1.5m),
        new(5, 20662, 56, 600m, 15m, 15m, 1.5m)
    ];

    public static ForeverExecuteRankDefinition GetByRank(int rank)
    {
        return All.FirstOrDefault(candidate => candidate.Rank == rank) ??
            throw new ArgumentOutOfRangeException(
                nameof(rank),
                rank,
                "Unsupported Forever Execute rank."
            );
    }

    public static ForeverExecuteRankDefinition GetHighestAvailable(
        int characterLevel)
    {
        if (characterLevel < All[0].RequiredLevel || characterLevel > 60)
        {
            throw new ArgumentOutOfRangeException(
                nameof(characterLevel),
                characterLevel,
                $"Forever Execute requires character level {All[0].RequiredLevel} through 60."
            );
        }

        return All
            .Where(definition => definition.RequiredLevel <= characterLevel)
            .OrderByDescending(definition => definition.RequiredLevel)
            .First();
    }
}
