namespace WarcraftSim.Data.Forever.Abilities.Warrior;

public sealed record ForeverMortalStrikeRankDefinition(
    int Rank,
    int SpellId,
    int RequiredLevel,
    decimal BonusWeaponDamage,
    decimal RageCost,
    decimal CooldownSeconds,
    decimal GlobalCooldownSeconds,
    decimal HealingReductionDurationSeconds);
