namespace WarcraftSim.Data.Forever.Abilities.Warrior;

public sealed record ForeverBloodthirstRankDefinition(
    int Rank,
    int SpellId,
    int RequiredLevel,
    decimal FlatDamage,
    decimal RageCost,
    decimal CooldownSeconds,
    decimal GlobalCooldownSeconds,
    decimal MovementSpeedDurationSeconds);
