namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Cleave ranks exposed by the current WoW Forever beta client. The damage
/// values and 20 Rage cost are unchanged from Classic Era; Forever's tooltip
/// now explicitly says the attack hits the target and a second nearby enemy.
/// </summary>
public static class ForeverCleaveRanks
{
    public const int MinimumCharacterLevel = 20;

    public const int MaximumCharacterLevel = 60;

    public static IReadOnlyList<ForeverCleaveRankDefinition> All { get; } =
    [
        new(1, 845, 20, 5m, 20m),
        new(2, 7369, 30, 10m, 20m),
        new(3, 11608, 40, 18m, 20m),
        new(4, 11609, 50, 32m, 20m),
        new(5, 20569, 60, 50m, 20m)
    ];

    public static ForeverCleaveRankDefinition GetHighestAvailable(
        int characterLevel)
    {
        if (
            characterLevel < MinimumCharacterLevel ||
            characterLevel > MaximumCharacterLevel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(characterLevel),
                characterLevel,
                $"Forever Cleave supports character levels {MinimumCharacterLevel}-{MaximumCharacterLevel}."
            );
        }

        return All
            .Last(rank => rank.RequiredLevel <= characterLevel);
    }

    public static ForeverCleaveRankDefinition GetByRank(
        int rank)
    {
        return All.FirstOrDefault(candidate => candidate.Rank == rank)
            ?? throw new ArgumentOutOfRangeException(
                nameof(rank),
                rank,
                "Unsupported Forever Cleave rank."
            );
    }
}
