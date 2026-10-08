namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Client-visible Heroic Strike rank data for WoW Forever.
/// Server-side behavior such as exact bonus threat and outcome refunds is
/// intentionally not represented here until it is verified separately.
/// </summary>
public sealed record ForeverHeroicStrikeRankDefinition(
    int Rank,
    int SpellId,
    int RequiredLevel,
    decimal BonusWeaponDamage,
    decimal RageCost);
