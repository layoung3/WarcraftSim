namespace WarcraftSim.Data.Forever.Abilities.Warrior;

public sealed record ForeverExecuteRankDefinition(
    int Rank,
    int SpellId,
    int RequiredLevel,
    decimal BaseDamage,
    decimal DamagePerExtraRage,
    decimal RageCost,
    decimal GlobalCooldownSeconds);
