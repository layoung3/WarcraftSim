namespace WarcraftSim.Data.Forever.Abilities.Warrior;

public sealed record ForeverSlamRankDefinition(
    int Rank,
    int SpellId,
    int RequiredLevel,
    decimal BonusWeaponDamage,
    decimal RageCost,
    decimal CastTimeSeconds,
    decimal CooldownSeconds,
    decimal GlobalCooldownSeconds);
