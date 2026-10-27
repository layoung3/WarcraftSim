using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever Improved Shadow Bolt (not Classic shared/charge-based Shadow Vulnerability).
/// Only the Warlock's own landed critical Shadow Bolts apply a 12s debuff.
/// Auras are target- and source-owned; damage multipliers are evaluated at impact.
/// </summary>
public sealed class WarlockImprovedShadowBoltProcessor(int talentRank, AuraManager auras)
    : ICombatEventProcessor
{
    public const string AuraKey = "warlock-improved-shadow-bolt";
    public const decimal DurationSeconds = 12m;

    public void Process(SimulationContext context, CombatEvent combatEvent)
    {
        if (talentRank <= 0 || combatEvent.Type != CombatEventType.Damage ||
            combatEvent.IsPeriodic || !combatEvent.IsCritical ||
            combatEvent.Amount is not > 0m ||
            !string.Equals(combatEvent.SourceActorKey, "warlock", StringComparison.OrdinalIgnoreCase) ||
            combatEvent.AbilityKey != "shadow-bolt" ||
            combatEvent.TargetActorKey is not { } targetKey)
            return;

        var target = context.GetActor(targetKey);
        if (target is null || !target.IsAlive) return;
        auras.ApplyAura(context, target, new AuraDefinition
        {
            Key = AuraKey, Name = "Improved Shadow Bolt",
            DurationSeconds = DurationSeconds
        }, "warlock", "improved-shadow-bolt", "shadow-vulnerability");
    }

    public static decimal DamageMultiplier(SimulationActorState target, decimal at,
        string sourceKey, string? school, int talentRank)
    {
        if (talentRank <= 0 ||
            !string.Equals(sourceKey, "warlock", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(school, "shadow", StringComparison.OrdinalIgnoreCase))
            return 1m;

        return target.ActiveAuras.Any(a =>
            a.Definition.Key == AuraKey &&
            a.SourceActorKey == "warlock" && a.IsActiveAt(at))
            ? 1m + Math.Clamp(talentRank, 0, 5) * .04m : 1m;
    }
}
