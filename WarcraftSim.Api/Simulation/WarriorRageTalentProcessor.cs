using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Api.Simulation;

/// <summary>Forever Warrior Rage talent hooks for the timed DPS alpha.
/// Always uses the simulation's seeded RNG; never creates a new Random.
/// Only swing-timer attacks are eligible for Unbridled Wrath: auto-attacks
/// and queued next-swing replacements (Heroic Strike / Cleave). Direct
/// instant specials such as Whirlwind must not trigger this proc. Forever's
/// exact proc mask has not yet been independently combat-verified.
/// </summary>
public sealed class WarriorRageTalentProcessor : ICombatEventProcessor
{
    public const string AngerManagementEffectKey = "anger-management-tick";
    private readonly int _unbridledWrathRank;
    private readonly bool _twoHandedWeapon;

    public WarriorRageTalentProcessor(int configuredRageTalentRank, bool twoHandedWeapon = false)
    {
        if (configuredRageTalentRank is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(configuredRageTalentRank));
        _unbridledWrathRank = configuredRageTalentRank;
        _twoHandedWeapon = twoHandedWeapon;
    }

    public void Process(SimulationContext context, CombatEvent combatEvent)
    {
        if (!string.Equals(combatEvent.SourceActorKey, "warrior", StringComparison.OrdinalIgnoreCase)) return;
        if (combatEvent.Type == CombatEventType.RotationDecision &&
            combatEvent.IsInternal &&
            combatEvent.EffectKey == AngerManagementEffectKey)
        {
            GainRage(context, combatEvent.SourceActorKey, "anger-management", 1m);
            return;
        }
        if (_unbridledWrathRank == 0 ||
            combatEvent.Type != CombatEventType.Damage ||
            combatEvent.IsPeriodic ||
            string.IsNullOrEmpty(combatEvent.WeaponHandKey) ||
            !IsSwingTimerAttack(combatEvent.AbilityKey) ||
            (combatEvent.Amount ?? 0m) <= 0m) return;
        // Percentage is sourced from the Forever client talent rank text.
        if (context.Random.NextDouble() < _unbridledWrathRank * 0.12d)
            GainRage(context, combatEvent.SourceActorKey, "unbridled-wrath", _twoHandedWeapon ? 2m : 1m);
    }

    private static bool IsSwingTimerAttack(string? abilityKey) => abilityKey is not null &&
        (abilityKey.Equals("main-hand-auto", StringComparison.OrdinalIgnoreCase) ||
         abilityKey.Equals("off-hand-auto", StringComparison.OrdinalIgnoreCase) ||
         abilityKey.Equals("heroic-strike", StringComparison.OrdinalIgnoreCase) ||
         abilityKey.Equals("cleave", StringComparison.OrdinalIgnoreCase));

    private static void GainRage(SimulationContext context, string sourceKey,
        string abilityKey, decimal requestedAmount)
    {
        var actor = context.GetActor(sourceKey);
        if (actor is null || !actor.IsAlive ||
            !actor.Resources.TryGetValue("rage", out var resource)) return;
        var gained = resource.Gain(requestedAmount);
        if (gained == 0m) return;
        context.EmitEvent(new CombatEvent
        {
            TimeSeconds = context.CurrentTimeSeconds,
            Type = CombatEventType.ResourceChanged,
            SourceActorKey = sourceKey, TargetActorKey = sourceKey,
            AbilityKey = abilityKey, Amount = gained,
            ResourceKey = "rage", ResourceTotalAfter = resource.Current,
            Description = $"{abilityKey} generated {gained} Rage."
        });
    }
}
