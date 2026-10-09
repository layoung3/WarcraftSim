namespace WarcraftSim.Data.Forever.Abilities.Warrior;

public static class ForeverMortalStrikeRanks
{
    public static IReadOnlyList<ForeverMortalStrikeRankDefinition> All { get; } =
    [
        new(1, 12294, 40, 85m, 30m, 6m, 1.5m, 10m),
        new(2, 21551, 48, 110m, 30m, 6m, 1.5m, 10m),
        new(3, 21552, 54, 135m, 30m, 6m, 1.5m, 10m),
        new(4, 21553, 60, 160m, 30m, 6m, 1.5m, 10m)
    ];

    public static ForeverMortalStrikeRankDefinition GetByRank(int rank)
    {
        return All.FirstOrDefault(candidate => candidate.Rank == rank) ??
            throw new ArgumentOutOfRangeException(
                nameof(rank),
                rank,
                "Unsupported Forever Mortal Strike rank."
            );
    }

    public static ForeverMortalStrikeRankDefinition GetHighestAvailable(
        int characterLevel)
    {
        if (characterLevel < All[0].RequiredLevel || characterLevel > 60)
        {
            throw new ArgumentOutOfRangeException(
                nameof(characterLevel),
                characterLevel,
                $"Forever Mortal Strike requires character level {All[0].RequiredLevel} through 60."
            );
        }

        return All
            .Where(definition => definition.RequiredLevel <= characterLevel)
            .OrderByDescending(definition => definition.RequiredLevel)
            .First();
    }
}
