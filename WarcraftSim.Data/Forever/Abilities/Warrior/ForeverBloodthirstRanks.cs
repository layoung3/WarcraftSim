namespace WarcraftSim.Data.Forever.Abilities.Warrior;

public static class ForeverBloodthirstRanks
{
    public static IReadOnlyList<ForeverBloodthirstRankDefinition> All { get; } =
    [
        new(1, 23881, 40, 30m, 30m, 6m, 1.5m, 10m),
        new(2, 23892, 48, 37m, 30m, 6m, 1.5m, 10m),
        new(3, 23893, 54, 43m, 30m, 6m, 1.5m, 10m),
        new(4, 23894, 60, 48m, 30m, 6m, 1.5m, 10m)
    ];

    public static ForeverBloodthirstRankDefinition GetByRank(int rank)
    {
        return All.FirstOrDefault(candidate => candidate.Rank == rank) ??
            throw new ArgumentOutOfRangeException(
                nameof(rank),
                rank,
                "Unsupported Forever Bloodthirst rank."
            );
    }

    public static ForeverBloodthirstRankDefinition GetHighestAvailable(
        int characterLevel)
    {
        if (characterLevel < All[0].RequiredLevel || characterLevel > 60)
        {
            throw new ArgumentOutOfRangeException(
                nameof(characterLevel),
                characterLevel,
                $"Forever Bloodthirst requires character level {All[0].RequiredLevel} through 60."
            );
        }

        return All
            .Where(definition => definition.RequiredLevel <= characterLevel)
            .OrderByDescending(definition => definition.RequiredLevel)
            .First();
    }
}
