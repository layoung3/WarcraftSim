namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Heroic Strike ranks exposed by the current WoW Forever beta client.
/// The rank damage values are unchanged from Classic Era in build 1.60.1.70205.
/// </summary>
public static class ForeverHeroicStrikeRanks
{
    public const int MaximumCharacterLevel = 60;

    public static IReadOnlyList<ForeverHeroicStrikeRankDefinition> All { get; } =
    [
        new(1, 78, 1, 11m, 15m),
        new(2, 284, 8, 21m, 15m),
        new(3, 285, 16, 32m, 15m),
        new(4, 1608, 24, 44m, 15m),
        new(5, 11564, 32, 58m, 15m),
        new(6, 11565, 40, 80m, 15m),
        new(7, 11566, 48, 111m, 15m),
        new(8, 11567, 56, 138m, 15m),
        new(9, 25286, 60, 157m, 15m)
    ];

    public static ForeverHeroicStrikeRankDefinition GetHighestAvailable(
        int characterLevel)
    {
        if (
            characterLevel < 1 ||
            characterLevel > MaximumCharacterLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(characterLevel),
                characterLevel,
                $"Forever Heroic Strike supports character levels 1-{MaximumCharacterLevel}."
            );
        }

        return All
            .Last(rank =>
                rank.RequiredLevel <= characterLevel);
    }

    public static ForeverHeroicStrikeRankDefinition GetByRank(
        int rank)
    {
        return All.FirstOrDefault(candidate =>
                candidate.Rank == rank)
            ?? throw new ArgumentOutOfRangeException(
                nameof(rank),
                rank,
                "Unsupported Forever Heroic Strike rank."
            );
    }
}
