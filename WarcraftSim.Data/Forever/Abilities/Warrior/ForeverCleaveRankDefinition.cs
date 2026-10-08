namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Client-visible Cleave rank data for WoW Forever.
/// </summary>
public sealed record ForeverCleaveRankDefinition(
    int Rank,
    int SpellId,
    int RequiredLevel,
    decimal BonusWeaponDamage,
    decimal RageCost);
